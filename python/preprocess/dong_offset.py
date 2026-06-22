"""
공간 오프셋(base_offset) 및 intensity_multiplier 계산.

━━━ MVP (현재) ━━━
  입력: S-DoT 구별 일별 기온 (data/file/sdot_wether_data/)
  해상도: 25개 구 단위
  함수: load_sdot_daily / calc_gu_offset / expand_gu_to_dong
        fit_intensity_multiplier_gu

━━━ 전체 구현 (추후 교체) ━━━
  입력: 동별 단기예보 아카이브 (data/file/dong_weather_data/)
  해상도: 424개 동 단위
  함수: load_dong_archive / calc_base_offset (아래에 그대로 유지)

모델 구조 변경 없음. base_offset dict의 key가 구→동으로 바뀌는 것뿐.
dong_temp.synthesize()는 두 경우 모두 동일하게 동작.

[공통 출력]
  base_offset_table : {"구이름 또는 동이름": offset_degC, ...}
  multiplier_model  : sklearn LinearRegression (풍속, 기온편차 → 잔차)
"""

from __future__ import annotations

from pathlib import Path
from typing import Any

import pandas as pd

ROOT = Path(__file__).resolve().parents[2]

DONG_DATA_DIR = ROOT / "data" / "file" / "dong_weather_data"
AWS_DATA_DIR  = ROOT / "data" / "file" / "aws_weather_data"
SDOT_DATA_DIR = ROOT / "data" / "file" / "sdot_wether_data"

# 일별 집계 결과 (data/extract/aws_hourly_to_daily.py 로 생성)
# columns: gu, date(YYYY-MM-DD), ta_min, ta_max, ta_mean, ws_mean
AWS_GU_DAILY_CSV = AWS_DATA_DIR / "aws_seoul_gu_daily.csv"

# KMA 기상자료개방포털 OBS_AWS_TIM 시간별 원시 파일
AWS_HOURLY_GLOB = "OBS_AWS_TIM_*.csv"

# 서울 AWS 지점번호 → 행정구 매핑 (25개 구 1:1)
# 기상청(410)=종로구, 현충원(889)=동작구. 남현·한강은 구 대표값 아님 → 제외
AWS_STN_TO_GU: dict[int, str] = {
    400: "강남구", 401: "서초구", 402: "강동구", 403: "송파구", 404: "강서구",
    405: "양천구", 406: "도봉구", 407: "노원구", 408: "동대문구", 409: "중랑구",
    410: "종로구", 411: "마포구", 412: "서대문구", 413: "광진구", 414: "성북구",
    415: "용산구", 416: "은평구", 417: "금천구", 419: "중구", 421: "성동구",
    423: "구로구", 424: "강북구", 509: "관악구", 510: "영등포구", 889: "동작구",
}

# S-DoT 자치구(영문) → 한국어 구명
SDOT_GU_EN_TO_KO: dict[str, str] = {
    "Gangnam-gu":      "강남구",
    "Seocho-gu":       "서초구",
    "Gangdong-gu":     "강동구",
    "Songpa-gu":       "송파구",
    "Gangseo-gu":      "강서구",
    "Yangcheon-gu":    "양천구",
    "Dobong-gu":       "도봉구",
    "Nowon-gu":        "노원구",
    "Dongdaemun-gu":   "동대문구",
    "Jungnang-gu":     "중랑구",
    "Jongno-gu":       "종로구",
    "Mapo-gu":         "마포구",
    "Seodaemun-gu":    "서대문구",
    "Gwangjin-gu":     "광진구",
    "Seongbuk-gu":     "성북구",
    "Yongsan-gu":      "용산구",
    "Eunpyeong-gu":    "은평구",
    "Geumcheon-gu":    "금천구",
    "Jung-gu":         "중구",
    "Seongdong-gu":    "성동구",
    "Guro-gu":         "구로구",
    "Gangbuk-gu":      "강북구",
    "Gwanak-gu":       "관악구",
    "Yeongdeungpo-gu": "영등포구",
    "Dongjak-gu":      "동작구",
}

HEAT_ISLAND_THRESHOLD_C = 2.0

DONG_GRID_CSV = ROOT / "data" / "file" / "seoul_dong_grid.csv"


def load_dong_archive() -> pd.DataFrame:
    """
    동별 초단기실황 일별 기온 아카이브.
    data/extract/dong_ncst_archive.py 실행 결과 사용.
    """
    merged = DONG_DATA_DIR / "seoul_dong_daily_temp.csv"
    if merged.exists():
        df = pd.read_csv(merged, encoding="utf-8-sig", parse_dates=["date"])
        return df.rename(columns={"date": "day"})[["dong", "day", "ta_min", "ta_max"]]

    frames: list[pd.DataFrame] = []
    for csv in sorted(DONG_DATA_DIR.glob("seoul_dong_daily_temp_??????.csv")):
        raw = pd.read_csv(csv, encoding="utf-8-sig", parse_dates=["date"])
        frames.append(raw[["dong", "date", "ta_min", "ta_max"]].rename(columns={"date": "day"}))

    if not frames:
        return pd.DataFrame(columns=["dong", "day", "ta_min", "ta_max"])

    return pd.concat(frames, ignore_index=True)


def calc_base_offset(
    dong_archive: pd.DataFrame,
    asos_daily: pd.DataFrame,
) -> dict[str, float]:
    """
    base_offset(dong) = 동별 평년 일최저기온 − ASOS 평년 일최저기온.

    동별 아카이브가 충분히 수집된 후 호출.
    """
    asos_mean_min = asos_daily["ta_min"].mean()

    offsets: dict[str, float] = {}
    for dong, grp in dong_archive.groupby("dong"):
        dong_mean_min = pd.to_numeric(grp["ta_min"], errors="coerce").mean()
        offsets[str(dong)] = round(dong_mean_min - asos_mean_min, 3)

    return offsets


def is_heat_island(offset: float) -> bool:
    """아침 최저기온 기준 열섬 판정: 오프셋 >= 2℃."""
    return offset >= HEAT_ISLAND_THRESHOLD_C


def fit_intensity_multiplier(
    residuals: pd.Series,
    wind_speed: pd.Series,
    temp_anomaly: pd.Series,
) -> Any:
    """
    intensity_multiplier 회귀.
    잔차 = 실제동기온 − (ASOS기온 + base_offset)
    설명변수: 풍속(ws_mean), 기온편차(ta_anomaly = ta_mean − ta_climatology)

    sklearn LinearRegression 반환.
    """
    from sklearn.linear_model import LinearRegression
    import numpy as np

    X = pd.DataFrame({"ws": wind_speed, "ta_anom": temp_anomaly}).dropna()
    y = residuals.loc[X.index]

    model = LinearRegression()
    model.fit(X, y)
    return model


def predict_multiplier(
    model: Any,
    ws_mean: float,
    ta_anomaly: float,
) -> float:
    """학습된 승수 모델로 특정 조건의 multiplier 예측."""
    import numpy as np
    X = pd.DataFrame({"ws": [ws_mean], "ta_anom": [ta_anomaly]})
    return float(model.predict(X)[0])


# ============================================================================
# MVP: AWS 구별 기온 기반 오프셋 (2020~, 25개 구 단위)
# ============================================================================

def _normalize_aws_daily_columns(df: pd.DataFrame) -> pd.DataFrame:
    """한국어/영문 혼재 컬럼명 → 내부 표준명."""
    df = df.copy()
    df.columns = df.columns.str.strip()
    rename_map = {
        "지점명": "stn_nm", "지점번호": "stn_id", "지점": "stn_id",
        "일시": "dt", "날짜": "date", "date": "date",
        "기온(°C)": "ta", "평균기온(°C)": "ta_mean", "평균기온": "ta_mean",
        "최저기온(°C)": "ta_min",  "최저기온": "ta_min",
        "최고기온(°C)": "ta_max",  "최고기온": "ta_max",
        "평균풍속(m/s)": "ws_mean", "평균풍속": "ws_mean",
        "구": "gu",
    }
    return df.rename(columns={k: v for k, v in rename_map.items() if k in df.columns})


def load_aws_hourly(data_dir: Path | None = None) -> pd.DataFrame:
    """
    KMA OBS_AWS_TIM 시간별 원시 CSV 통합 로드.

    기대 컬럼: 지점, 지점명, 일시, 기온(°C)
    """
    directory = Path(data_dir) if data_dir else AWS_DATA_DIR
    files = sorted(directory.glob(AWS_HOURLY_GLOB))
    if not files:
        raise FileNotFoundError(
            f"AWS 시간별 파일 없음: {directory / AWS_HOURLY_GLOB}\n"
            "  KMA 기상자료개방포털에서 서울 AWS 시간자료(OBS_AWS_TIM)를 내려받으세요."
        )

    frames: list[pd.DataFrame] = []
    for csv in files:
        raw = pd.read_csv(csv, encoding="utf-8-sig")
        df = _normalize_aws_daily_columns(raw)
        required = {"stn_id", "stn_nm", "dt", "ta"}
        missing = required - set(df.columns)
        if missing:
            raise ValueError(f"{csv.name}: 필수 컬럼 없음 {missing}")
        frames.append(df[["stn_id", "stn_nm", "dt", "ta"]])

    hourly = pd.concat(frames, ignore_index=True)
    hourly["stn_id"] = pd.to_numeric(hourly["stn_id"], errors="coerce").astype("Int64")
    hourly["dt"] = pd.to_datetime(hourly["dt"], errors="coerce")
    hourly["ta"] = pd.to_numeric(hourly["ta"], errors="coerce")
    hourly = hourly.dropna(subset=["stn_id", "dt", "ta"])
    hourly["stn_id"] = hourly["stn_id"].astype(int)
    hourly["gu"] = hourly["stn_id"].map(AWS_STN_TO_GU)
    hourly = hourly.dropna(subset=["gu"])
    return hourly.drop_duplicates(subset=["stn_id", "dt"]).reset_index(drop=True)


def aggregate_aws_hourly_to_daily(hourly: pd.DataFrame) -> pd.DataFrame:
    """AWS 시간별 → 구별 일별 (ta_min, ta_max, ta_mean). ws_mean은 AWS에 없어 NaN."""
    df = hourly.copy()
    df["date"] = df["dt"].dt.normalize()
    daily = df.groupby(["gu", "date"], as_index=False).agg(
        ta_min=("ta", "min"),
        ta_max=("ta", "max"),
        ta_mean=("ta", "mean"),
    )
    daily["ws_mean"] = float("nan")
    return daily


def build_aws_gu_daily(
    data_dir: Path | None = None,
    output_path: Path | None = None,
) -> pd.DataFrame:
    """OBS_AWS_TIM 시간별 → aws_seoul_gu_daily.csv 생성."""
    directory = Path(data_dir) if data_dir else AWS_DATA_DIR
    out = Path(output_path) if output_path else AWS_GU_DAILY_CSV
    daily = aggregate_aws_hourly_to_daily(load_aws_hourly(directory))
    out.parent.mkdir(parents=True, exist_ok=True)
    daily.to_csv(out, index=False, encoding="utf-8-sig")
    return daily


def _load_aws_daily_csv(csv: Path) -> pd.DataFrame:
    df = _normalize_aws_daily_columns(pd.read_csv(csv, encoding="utf-8-sig"))

    if "gu" not in df.columns:
        raise ValueError("'gu'(구 이름) 컬럼이 없습니다. 파일에 구 이름 컬럼을 추가하세요.")

    date_col = "date" if "date" in df.columns else None
    if date_col is None:
        raise ValueError(f"날짜 컬럼 없음: {csv}")

    df["date"] = pd.to_datetime(df[date_col], errors="coerce")
    for col in ("ta_min", "ta_max", "ta_mean", "ws_mean"):
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors="coerce")

    return df.dropna(subset=["gu", "date", "ta_min"]).reset_index(drop=True)


def load_aws_daily(path: Path | None = None, data_dir: Path | None = None) -> pd.DataFrame:
    """
    AWS 구별 일별 기온 로드.

    우선순위:
      1. aws_seoul_gu_daily.csv (일별 집계본)
      2. OBS_AWS_TIM_*.csv 시간별 원시 → 메모리에서 일별 집계

    ws_mean이 없으면 fit_intensity_multiplier_gu()에서 ASOS 풍속으로 대체.
    """
    directory = Path(data_dir) if data_dir else AWS_DATA_DIR
    csv = Path(path) if path else AWS_GU_DAILY_CSV

    if csv.exists():
        return _load_aws_daily_csv(csv)

    if directory.glob(AWS_HOURLY_GLOB):
        return aggregate_aws_hourly_to_daily(load_aws_hourly(directory))

    raise FileNotFoundError(
        f"AWS 데이터 없음: {directory}\n"
        "  aws_seoul_gu_daily.csv 또는 OBS_AWS_TIM_*.csv 가 필요합니다.\n"
        "  python data/extract/aws_hourly_to_daily.py 로 일별 파일을 생성할 수 있습니다."
    )


def calc_gu_offset(
    aws_daily: pd.DataFrame,
    asos_daily: pd.DataFrame,
) -> dict[str, float]:
    """
    base_offset(gu) = AWS 구별 평년 일최저기온 − ASOS(108) 평년 일최저기온.

    calc_base_offset()과 동일 로직. 단위가 구(MVP) vs 동(전체).
    """
    asos_mean_min = asos_daily["ta_min"].mean()

    offsets: dict[str, float] = {}
    for gu, grp in aws_daily.groupby("gu"):
        gu_mean_min = pd.to_numeric(grp["ta_min"], errors="coerce").mean()
        offsets[str(gu)] = round(gu_mean_min - asos_mean_min, 3)

    return offsets


def expand_gu_to_dong(
    gu_offsets: dict[str, float],
    dong_grid_path: Path | None = None,
) -> dict[str, float]:
    """
    구 단위 오프셋 → 동 단위 dict 확장.

    dong_temp.synthesize()는 dict key를 그대로 사용하므로,
    MVP에서 구 해상도로 실행할 때는 이 함수 없이 gu_offsets를 직접 전달해도 됨.
    버드뷰 동 단위 색상이 필요할 때만 사용.
    """
    grid_path = dong_grid_path or DONG_GRID_CSV
    if not grid_path.exists():
        raise FileNotFoundError(f"seoul_dong_grid.csv 없음: {grid_path}")

    grid = pd.read_csv(grid_path, encoding="utf-8-sig")
    if "gu" not in grid.columns or "dong" not in grid.columns:
        raise ValueError("seoul_dong_grid.csv에 'gu', 'dong' 컬럼이 필요합니다.")

    dong_offsets: dict[str, float] = {}
    for _, row in grid.iterrows():
        gu   = str(row["gu"]).strip()
        dong = str(row["dong"]).strip()
        dong_offsets[dong] = gu_offsets.get(gu, 0.0)

    return dong_offsets


# ============================================================================
# S-DoT 구별 기온 기반 오프셋 (2020~, 25개 구 단위)
# ============================================================================

def _load_sdot_csv(path: Path) -> pd.DataFrame:
    """S-DoT 단일 CSV → 내부 표준 컬럼(gu, datetime, ta_max, ta_mean, ta_min, ws_mean)."""
    df = pd.read_csv(path, encoding="utf-8-sig")
    df = df.rename(columns={
        "자치구":        "gu_en",
        "측정시간":      "dt",
        "온도 최대(℃)":  "ta_max",
        "온도 평균(℃)":  "ta_mean",
        "온도 최소(℃)":  "ta_min",
        "풍속 평균(m/s)": "ws_mean",
    })
    df["gu"] = df["gu_en"].map(SDOT_GU_EN_TO_KO)
    df = df.dropna(subset=["gu"])
    df["dt"] = pd.to_datetime(df["dt"].str.replace("_", " "), errors="coerce")
    for col in ("ta_max", "ta_mean", "ta_min", "ws_mean"):
        df[col] = pd.to_numeric(df[col], errors="coerce")
    return df[["gu", "dt", "ta_max", "ta_mean", "ta_min", "ws_mean"]].dropna(subset=["dt", "ta_mean"])


def load_sdot_daily(data_dir: Path | None = None) -> pd.DataFrame:
    """
    S-DoT 구별 일별 기온 로드.

    data/file/sdot_wether_data/ 하위 연도별 폴더의 모든 CSV를 읽어
    구별 일별 ta_min / ta_max / ta_mean / ws_mean 으로 집계.

    반환 컬럼: gu, date, ta_min, ta_max, ta_mean, ws_mean
    """
    directory = Path(data_dir) if data_dir else SDOT_DATA_DIR
    csv_files = sorted(directory.rglob("*.csv"))
    if not csv_files:
        raise FileNotFoundError(
            f"S-DoT CSV 파일 없음: {directory}\n"
            "  data/file/sdot_wether_data/ 하위에 연도별 폴더가 있어야 합니다."
        )

    frames: list[pd.DataFrame] = []
    for csv in csv_files:
        try:
            frames.append(_load_sdot_csv(csv))
        except Exception:
            continue

    if not frames:
        raise RuntimeError("S-DoT CSV를 하나도 읽지 못했습니다.")

    hourly = pd.concat(frames, ignore_index=True)
    hourly["date"] = hourly["dt"].dt.normalize()
    daily = hourly.groupby(["gu", "date"], as_index=False).agg(
        ta_min=("ta_min", "min"),
        ta_max=("ta_max", "max"),
        ta_mean=("ta_mean", "mean"),
        ws_mean=("ws_mean", "mean"),
    )
    return daily.reset_index(drop=True)


def fit_intensity_multiplier_gu(
    aws_daily: pd.DataFrame,
    asos_daily: pd.DataFrame,
    gu_offsets: dict[str, float],
) -> Any:
    """
    intensity_multiplier 회귀 (MVP: 구 단위 AWS 잔차 기반).

    잔차(residual) = ta_mean_aws(gu, date) − (ta_mean_asos(date) + base_offset(gu))
    설명변수: ws_mean(AWS 관측소 풍속), ta_anomaly(ASOS 기온 − 월 평년값)

    fit_intensity_multiplier()와 동일 반환 타입 (sklearn LinearRegression).
    """
    from sklearn.linear_model import LinearRegression
    import numpy as np

    # ASOS 일별 기온: date 인덱스로 변환
    asos = asos_daily.set_index("date")[["ta_mean", "ws_mean"]].copy()

    # 월 평년 기온 (ASOS 전체 기간 평균)
    asos["month"] = asos.index.month
    climatology = asos.groupby("month")["ta_mean"].mean().to_dict()

    rows = []
    for gu, grp in aws_daily.groupby("gu"):
        offset = gu_offsets.get(str(gu), 0.0)
        for _, r in grp.iterrows():
            dt = r["date"]
            if dt not in asos.index:
                continue
            ta_asos = asos.at[dt, "ta_mean"]
            ws = r.get("ws_mean")
            if ws is None or pd.isna(ws):
                ws = asos.at[dt, "ws_mean"]
            residual = r["ta_mean"] - (ta_asos + offset)
            ta_anom  = ta_asos - climatology.get(dt.month, ta_asos)
            rows.append({"ws": ws, "ta_anom": ta_anom, "residual": residual})

    df_r = pd.DataFrame(rows).dropna()
    if df_r.empty:
        raise RuntimeError("잔차 데이터가 없습니다. AWS와 ASOS 날짜 범위를 확인하세요.")

    model = LinearRegression()
    model.fit(df_r[["ws", "ta_anom"]], df_r["residual"])
    return model

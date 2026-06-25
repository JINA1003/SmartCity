"""
S-DoT 기반 구별 base_offset 계산 및 미래 기온 예측.

설계 원칙 :
  base_offset(gu)      = S-DoT 평년 일평균기온 - ASOS 동기간 평균기온  [고정, 구의 구조적 특성]
  intensity_multiplier = f(ws_clim_month)                      [월별 ASOS 평년 풍속 기반]

풍속은 S-DoT 결측률 ~80% 로 사용 불가 → ASOS 월별 평년 풍속으로 대체.
(근거: 미래 풍속 예측 신뢰도가 낮으므로 평년값이 더 정직한 가정)

구별 기온 예측 공식 (Delta Method):
  ta_gu(gu, year, month) = ta_asos(year, month, ONI)
                         + offset(gu, month) × multiplier(month)
  - ta_asos  : temp_trend.predict() 결과 (ASOS 기준 기온)
  - offset   : S-DoT 5년 실측에서 계산한 구별 월별 오프셋
  - multiplier: ASOS 평년 풍속 기반 (평년=1.0, 약풍>1.0, 강풍<1.0)

공개 함수:
  load_sdot_gu_daily()        → S-DoT 구별 일별 DataFrame
  calc_base_offset()          → {구: offset_degC}
  calc_monthly_offset()       → {(구, month): offset_degC}
  calc_dynamic_multiplier()   → DataFrame(year, month, multiplier)
  calc_ws_beta()              → β_ws float
  calc_beta_gu()              → {구: beta_gu}
  save_gu_params() / load_gu_params()  → 파라미터 직렬화
  predict_gu_temp()           → {구: ta_gu}  ← API/Unity 용
"""
import re
from pathlib import Path

import pandas as pd
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SDOT_DIR  = ROOT / "data" / "file" / "sdot_weather_data"
DIST_XLSX = SDOT_DIR / "서울시 도시데이터 센서(S-DoT) 환경정보 설치 위치정보.xlsx"

GU_EN_TO_KO: dict[str, str] = {
    "Gangnam-gu": "강남구", "Seocho-gu": "서초구", "Gangdong-gu": "강동구",
    "Songpa-gu": "송파구", "Gangseo-gu": "강서구", "Yangcheon-gu": "양천구",
    "Dobong-gu": "도봉구", "Nowon-gu": "노원구", "Dongdaemun-gu": "동대문구",
    "Jungnang-gu": "중랑구", "Jongno-gu": "종로구", "Mapo-gu": "마포구",
    "Seodaemun-gu": "서대문구", "Gwangjin-gu": "광진구", "Seongbuk-gu": "성북구",
    "Yongsan-gu": "용산구", "Eunpyeong-gu": "은평구", "Geumcheon-gu": "금천구",
    "Jung-gu": "중구", "Seongdong-gu": "성동구", "Guro-gu": "구로구",
    "Gangbuk-gu": "강북구", "Gwanak-gu": "관악구", "Yeongdeungpo-gu": "영등포구",
    "Dongjak-gu": "동작구",
}

def _clean(c: str) -> str:
    return re.sub(r"\s+", "", str(c).strip())


def _serial_to_gu() -> dict[str, str]:
    df = pd.read_excel(DIST_XLSX)
    df["구"] = df["주소"].str.split().str[1]
    return (
        df.rename(columns={"모델 시리얼(*)": "시리얼"})
        .dropna(subset=["시리얼", "구"])
        .set_index("시리얼")["구"]
        .astype(str).str.strip()
        .to_dict()
    )


def _read_csv(path: Path, serial_map: dict[str, str]) -> pd.DataFrame:
    """단일 S-DoT CSV → (gu, dt, ta, hm, ws) 반환. 두 포맷 자동 감지."""
    try:
        df = pd.read_csv(path, encoding="utf-8", low_memory=False)
    except UnicodeDecodeError:
        df = pd.read_csv(path, encoding="cp949", low_memory=False)

    df.columns = [_clean(c) for c in df.columns]

    # ── 2023+ 포맷: 측정시간 + 온도평균(℃) ──────────────────────────────
    if "측정시간" in df.columns and "온도평균(℃)" in df.columns:
        df = df.rename(columns={
            "온도평균(℃)": "ta", "습도평균(%)": "hm", "풍속평균(m/s)": "ws",
            "측정시간": "dt", "자치구": "gu_en",
        })
        df["gu"] = df["gu_en"].map(GU_EN_TO_KO)

    # ── 2020~2022 포맷: 시리얼 + 기온(℃) ────────────────────────────────
    elif "시리얼" in df.columns and "기온(℃)" in df.columns:
        df = df.rename(columns={
            "기온(℃)": "ta", "상대습도(%)": "hm", "풍속(m/s)": "ws", "등록일자": "dt",
        })
        df["시리얼"] = df["시리얼"].astype(str).str.strip()
        df["gu"] = df["시리얼"].map(serial_map)
    else:
        return pd.DataFrame()

    keep = [c for c in ["gu", "dt", "ta", "hm", "ws"] if c in df.columns]
    df = df[keep].copy()
    if "ws" not in df.columns:
        df["ws"] = float("nan")
    if "hm" not in df.columns:
        df["hm"] = float("nan")

    df["dt"] = pd.to_datetime(df["dt"].astype(str).str.replace("_", " "), errors="coerce", format="mixed")
    df["ta"] = pd.to_numeric(df["ta"], errors="coerce")
    df["hm"] = pd.to_numeric(df["hm"], errors="coerce")
    df["ws"] = pd.to_numeric(df["ws"], errors="coerce")

    return df[["gu", "dt", "ta", "hm", "ws"]].dropna(subset=["gu", "dt", "ta"])


def load_sdot_gu_daily(data_dir: Path | None = None) -> pd.DataFrame:
    """
    S-DoT 전체 → 구별 일별 집계.

    반환: gu, date, ta_min, ta_max, ta_mean, hm_mean, ws_mean
    ws_mean 결측률 ~80% 는 정상 (센서 미탑재) — ASOS 평년값으로 대체 예정.

    최초 실행 시 parquet 캐시 생성 → 이후 실행은 캐시에서 즉시 로드.
    원본 CSV 중 하나라도 캐시보다 최신이면 자동 재생성.
    """
    directory  = Path(data_dir) if data_dir else SDOT_DIR
    cache_path = ROOT / "data" / "output" / "sdot_gu_daily_cache.csv"

    csv_files = sorted(directory.rglob("*.csv"))
    if not csv_files:
        raise FileNotFoundError(f"S-DoT CSV 없음: {directory}")

    # 캐시 유효성 검사: 캐시가 있고 모든 CSV보다 최신이면 캐시 사용
    if cache_path.exists():
        cache_mtime = cache_path.stat().st_mtime
        latest_csv  = max(f.stat().st_mtime for f in csv_files)
        if cache_mtime >= latest_csv:
            print(f"      [캐시] {cache_path.name}")
            df = pd.read_csv(cache_path, parse_dates=["date"])
            return df

    print(f"      [파싱] S-DoT {len(csv_files)}개 CSV → 최초 실행 수 분 소요")
    serial_map = _serial_to_gu()

    frames: list[pd.DataFrame] = []
    for csv in csv_files:
        try:
            chunk = _read_csv(csv, serial_map)
            if not chunk.empty:
                frames.append(chunk)
        except Exception:
            continue

    hourly = pd.concat(frames, ignore_index=True)

    # 이상값 제거
    hourly.loc[hourly["ta"].lt(-25) | hourly["ta"].gt(45),  "ta"] = float("nan")
    hourly.loc[hourly["hm"].lt(5)   | hourly["hm"].gt(100), "hm"] = float("nan")
    hourly.loc[hourly["ws"].gt(20),                          "ws"] = float("nan")
    hourly = hourly.dropna(subset=["ta"])

    hourly["date"] = hourly["dt"].dt.normalize()
    daily = hourly.groupby(["gu", "date"], as_index=False).agg(
        ta_min=("ta", "min"),
        ta_max=("ta", "max"),
        ta_mean=("ta", "mean"),
        hm_mean=("hm", "mean"),
        ws_mean=("ws", "mean"),
    ).reset_index(drop=True)

    cache_path.parent.mkdir(parents=True, exist_ok=True)
    daily.to_csv(cache_path, index=False, encoding="utf-8-sig")
    print(f"      [캐시 저장] {cache_path.name}")

    return daily


def calc_base_offset(
    sdot_daily: pd.DataFrame,
    asos_daily: pd.DataFrame,
) -> dict[str, float]:
    """
    base_offset(gu) = S-DoT 구별 전체 평균기온 - ASOS 동기간 평균기온 (연간 단일값).

    월별 offset이 주 사용 경로이며, 이 함수는 참고용으로 유지.
    """
    sdot_dates = set(sdot_daily["date"].dt.normalize())
    asos_sub   = asos_daily[asos_daily["date"].isin(sdot_dates)]["ta_mean"].mean()
    return {
        str(gu): round(grp["ta_mean"].mean() - asos_sub, 3)
        for gu, grp in sdot_daily.groupby("gu")
    }


def calc_monthly_offset(
    sdot_daily: pd.DataFrame,
    asos_daily: pd.DataFrame,
) -> dict[tuple[str, int], float]:
    """
    offset(gu, month) = S-DoT 구별 월평균기온 - ASOS 동기간 월평균기온.

    연간 단일 offset을 쓰면 여름/겨울 계절성 편향이 생김:
      - 여름 잔차 -0.4℃ (S-DoT가 ASOS 대비 낮게 측정)
      - 겨울 잔차 +0.6℃ (S-DoT가 ASOS 대비 높게 측정)
    월별로 나누면 이 편향이 흡수되어 ta_dev가 실제 계절에 따라 변동함.

    반환: {(구이름, month): offset_degC}
    """
    sdot = sdot_daily.copy()
    sdot["month"] = sdot["date"].dt.month

    asos = asos_daily.copy()
    asos["month"] = asos["date"].dt.month

    # ASOS 월별 평균 (S-DoT 수집 기간만)
    sdot_dates = set(sdot["date"].dt.normalize())
    asos_sub   = (
        asos[asos["date"].isin(sdot_dates)]
        .groupby("month")["ta_mean"].mean()
        .to_dict()
    )

    result: dict[tuple[str, int], float] = {}
    for (gu, month), grp in sdot.groupby(["gu", "month"]):
        asos_m = asos_sub.get(int(month), np.nan)
        if np.isnan(asos_m):
            continue
        result[(str(gu), int(month))] = round(grp["ta_mean"].mean() - asos_m, 3)
    return result


def calc_ws_beta(
    sdot_daily: pd.DataFrame,
    asos_daily: pd.DataFrame,
    monthly_offset: dict[tuple[str, int], float],
) -> float:
    """
    β_ws 추정: 잔차(월별offset 제거 후) ~ ASOS 실제 풍속 회귀.

    월별 offset으로 계절 편향을 흡수한 뒤 남은 잔차에서
    풍속의 추가 설명력(β_ws)을 추정.

    반환: β_ws (float, 음수여야 정상: 풍속↑ → 열섬 잔차↓)
    """
    from sklearn.linear_model import LinearRegression

    asos_idx = asos_daily.set_index("date")["ws_mean"]
    sdot = sdot_daily.copy()
    sdot["month"] = sdot["date"].dt.month

    rows = []
    for _, r in sdot.iterrows():
        dt  = r["date"]
        gu  = str(r["gu"])
        mon = int(r["month"])
        off = monthly_offset.get((gu, mon), np.nan)
        if np.isnan(off) or dt not in asos_idx.index:
            continue
        asos_ta = asos_daily.loc[asos_daily["date"] == dt, "ta_mean"].values
        if len(asos_ta) == 0:
            continue
        resid = r["ta_mean"] - (asos_ta[0] + off)
        ws    = asos_idx[dt]
        rows.append({"resid": resid, "ws": ws})

    df = pd.DataFrame(rows).dropna()
    if df.empty or df["ws"].std() < 1e-6:
        return 0.0

    model = LinearRegression()
    model.fit(df[["ws"]], df["resid"])
    return round(float(model.coef_[0]), 4)


def calc_beta_gu(
    sdot_daily: pd.DataFrame,
    asos_daily: pd.DataFrame,
) -> dict[str, float]:
    """
    beta_gu: 구별 ASOS 기온편차 반응 계수.

    S-DoT 실측(2020~2025)에서 추출.
    sdot_dev(gu,y,m) = beta_gu × asos_anom(y,m)

    - sdot_dev: S-DoT 구별 월평균 - 서울 S-DoT 전체 월평균
    - asos_anom: ASOS 월평균 - ASOS 동기간 월별 평년

    beta_gu > 0: 폭염 때 서울 평균보다 더 올라가는 구 (열섬 증폭형)
    beta_gu < 0: 폭염 때 덜 올라가는 구 (열섬 완충형)

    반환: {구이름: beta_gu}
    """
    from sklearn.linear_model import LinearRegression

    sdot = sdot_daily.copy()
    sdot["year"]  = sdot["date"].dt.year
    sdot["month"] = sdot["date"].dt.month

    # S-DoT 구별 월평균 & 서울 전체 월평균
    sdot_m = sdot.groupby(["gu", "year", "month"])["ta_mean"].mean().reset_index()
    sdot_seoul = (
        sdot_m.groupby(["year", "month"])["ta_mean"]
        .mean()
        .reset_index()
        .rename(columns={"ta_mean": "sdot_seoul"})
    )
    sdot_m = sdot_m.merge(sdot_seoul, on=["year", "month"])
    sdot_m["sdot_dev"] = sdot_m["ta_mean"] - sdot_m["sdot_seoul"]

    # ASOS 월평균 & 동기간 월별 평년 편차
    asos = asos_daily.copy()
    asos["year"]  = asos["date"].dt.year
    asos["month"] = asos["date"].dt.month
    asos_m = (
        asos.groupby(["year", "month"])["ta_mean"]
        .mean()
        .reset_index()
        .rename(columns={"ta_mean": "asos_ta"})
    )
    sdot_years = sdot_m["year"].unique()
    asos_clim = (
        asos_m[asos_m["year"].isin(sdot_years)]
        .groupby("month")["asos_ta"]
        .mean()
        .to_dict()
    )
    asos_m["asos_anom"] = asos_m["asos_ta"] - asos_m["month"].map(asos_clim)

    df = sdot_m.merge(asos_m[["year", "month", "asos_anom"]], on=["year", "month"])

    result: dict[str, float] = {}
    for gu, grp in df.groupby("gu"):
        grp = grp.dropna(subset=["sdot_dev", "asos_anom"])
        if len(grp) < 6:
            result[str(gu)] = 0.0
            continue
        model = LinearRegression(fit_intercept=False)
        model.fit(grp[["asos_anom"]], grp["sdot_dev"])
        result[str(gu)] = round(float(model.coef_[0]), 4)

    return result


def calc_dynamic_multiplier(asos_daily: pd.DataFrame) -> pd.DataFrame:
    """
    동적 열섬 강도 multiplier.

    근거: Oke(1973) — 열섬 강도 ∝ 1/풍속
    multiplier(y,m) = ws_clim(m) / ws_actual(y,m)

    - ws_clim(m):   ASOS 월별 평년 풍속 (S-DoT 수집 기간 기준)
    - ws_actual(y,m): 해당 연월 ASOS 실제 풍속

    평년 풍속이면 multiplier=1.0,
    약풍(열섬 강화)이면 >1.0, 강풍(열섬 약화)이면 <1.0.

    반환: DataFrame — year, month, multiplier
    """
    asos = asos_daily.copy()
    asos["year"]  = asos["date"].dt.year
    asos["month"] = asos["date"].dt.month

    # 월별 실제 풍속
    ws_actual = (
        asos.groupby(["year", "month"])["ws_mean"]
        .mean()
        .reset_index()
        .rename(columns={"ws_mean": "ws_actual"})
    )

    # 월별 평년 풍속
    ws_clim = ws_actual.groupby("month")["ws_actual"].mean().to_dict()
    ws_actual["ws_clim"] = ws_actual["month"].map(ws_clim)

    # multiplier = 평년 / 실제 (풍속 약하면 multiplier↑)
    ws_actual["multiplier"] = ws_actual["ws_clim"] / ws_actual["ws_actual"]

    # 이상값 클리핑 (0.5~2.0)
    ws_actual["multiplier"] = ws_actual["multiplier"].clip(0.5, 2.0).round(4)

    return ws_actual[["year", "month", "multiplier"]]


# ============================================================================
# 파라미터 저장 / 로드  (train_pipeline 에서 한 번 계산 → API 서버에서 재사용)
# ============================================================================

GU_PARAMS_PATH = Path(__file__).resolve().parents[2] / "data" / "output" / "gu_offset_params.pkl"


def save_gu_params(
    monthly_offset: dict[tuple[str, int], float],
    ws_clim: dict[int, float],
) -> None:
    """
    monthly_offset : {(구, month): offset_degC}
    ws_clim        : {month: 평년_풍속_m/s}  → 미래 multiplier=1.0 기준

    미래 예측에서는 풍속을 알 수 없으므로 평년 multiplier=1.0 을 사용.
    """
    import pickle
    GU_PARAMS_PATH.parent.mkdir(parents=True, exist_ok=True)
    with open(GU_PARAMS_PATH, "wb") as f:
        pickle.dump({"monthly_offset": monthly_offset, "ws_clim": ws_clim}, f)


def load_gu_params() -> tuple[dict[tuple[str, int], float], dict[int, float]]:
    """저장된 파라미터 로드. 반환: (monthly_offset, ws_clim)"""
    import pickle
    with open(GU_PARAMS_PATH, "rb") as f:
        data = pickle.load(f)
    return data["monthly_offset"], data["ws_clim"]


# ============================================================================
# 미래 기온 예측  (API / Unity 용)
# ============================================================================

def predict_gu_temp(
    ta_asos: float,
    month: int,
    monthly_offset: dict[tuple[str, int], float] | None = None,
    multiplier: float = 1.0,
) -> dict[str, float]:
    """
    ASOS 예측 기온(ta_asos) → 25개 구별 기온 dict.

    공식:
      ta_gu(gu) = ta_asos + offset(gu, month) × multiplier

    파라미터:
      ta_asos        : temp_trend.predict(year, month, oni) 결과
      month          : 예측 월 (1~12)
      monthly_offset : {(구, month): offset}  — None 이면 파일에서 자동 로드
      multiplier     : 열섬 강도 스케일 (미래 예측 시 1.0, 과거 재현 시 실측 multiplier)

    반환: {"강남구": 31.2, "강북구": 29.8, ...}
    """
    if monthly_offset is None:
        monthly_offset, _ = load_gu_params()

    result: dict[str, float] = {}
    for (gu, m), offset in monthly_offset.items():
        if m != month:
            continue
        result[gu] = round(ta_asos + offset * multiplier, 3)
    return result

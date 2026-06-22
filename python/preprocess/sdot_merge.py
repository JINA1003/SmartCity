import pandas as pd
import openpyxl
from __future__ import annotations

import json
import re
from pathlib import Path

df_district = pd.read_excel("../data/file/sdot_weather_data/서울시 도시데이터 센서(S-DoT) 환경정보 설치 위치정보.xlsx")
df_district["구"] = df_district["주소"].str.split(" ").str[1]


# df_district.sort_values("모델 시리얼(*)")
df_district.groupby("구").size()

df_gu = df_district.loc[:,["모델 시리얼(*)","구"]]
df_gu.head(2)


SDOT_DIR = Path("../data/file/sdot_weather_data")
DISTRICT_XLSX = SDOT_DIR / "서울시 도시데이터 센서(S-DoT) 환경정보 설치 위치정보.xlsx"

# 신규 포맷(2025~) 자치구 영문 → 한글 (xlsx 시리얼과 불일치 시 보조)
GU_EN_TO_KO = {
    "Jongno-gu": "종로구", "Jung-gu": "중구", "Yongsan-gu": "용산구",
    "Seongdong-gu": "성동구", "Gwangjin-gu": "광진구", "Dongdaemun-gu": "동대문구",
    "Jungnang-gu": "중랑구", "Seongbuk-gu": "성북구", "Gangbuk-gu": "강북구",
    "Dobong-gu": "도봉구", "Nowon-gu": "노원구", "Eunpyeong-gu": "은평구",
    "Seodaemun-gu": "서대문구", "Mapo-gu": "마포구", "Yangcheon-gu": "양천구",
    "Gangseo-gu": "강서구", "Guro-gu": "구로구", "Geumcheon-gu": "금천구",
    "Yeongdeungpo-gu": "영등포구", "Dongjak-gu": "동작구", "Gwanak-gu": "관악구",
    "Seocho-gu": "서초구", "Gangnam-gu": "강남구", "Songpa-gu": "송파구",
    "Gangdong-gu": "강동구",
}


def _clean_col(name: str) -> str:
    return re.sub(r"\s+", "", str(name).strip())


def load_district() -> pd.DataFrame:
    """센서 위치 xlsx → 시리얼·구 매핑."""
    df = pd.read_excel(DISTRICT_XLSX)
    df["구"] = df["주소"].str.split(" ").str[1]
    return df.rename(columns={"모델 시리얼(*)": "시리얼"})[["시리얼", "구", "주소"]]


def _read_sdot_csv(path: Path) -> pd.DataFrame:
    """연도별로 다른 S-DoT CSV 스키마를 통일 컬럼으로 읽기."""
    header = pd.read_csv(path, nrows=0, encoding="utf-8").columns.tolist()
    try:
        df = pd.read_csv(path, encoding="utf-8", usecols=header, low_memory=False)
    except UnicodeDecodeError:
        df = pd.read_csv(path, encoding="cp949", usecols=header, low_memory=False)

    df.columns = [_clean_col(c) for c in df.columns]

    rename = {
        "기온(℃)": "기온",
        "온도평균(℃)": "기온",
        "상대습도(%)": "상대습도",
        "습도평균(%)": "상대습도",
        "풍속(m/s)": "풍속",
        "풍속평균(m/s)": "풍속",
        "등록일시": "등록일자",
    }
    df = df.rename(columns={k: v for k, v in rename.items() if k in df.columns})

    if "시리얼" not in df.columns:
        raise ValueError(f"시리얼 컬럼 없음: {path.name}")

    keep = [c for c in ["시리얼", "기온", "상대습도", "풍속", "등록일자", "자치구"] if c in df.columns]
    df = df[keep].copy()
    df["시리얼"] = df["시리얼"].astype(str).str.strip()
    df["등록일자"] = pd.to_datetime(df["등록일자"], errors="coerce")
    for col in ["기온", "상대습도", "풍속"]:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors="coerce")
    df["year"] = df["등록일자"].dt.year
    return df


def load_sdot_weather(
    data_dir: Path | str = SDOT_DIR,
    years: list[int] | None = None,
    limit_files: int | None = None,
) -> pd.DataFrame:
    """sdot_weather_data 하위 모든 CSV 로드 후 통합."""
    data_dir = Path(data_dir)
    paths = sorted(data_dir.rglob("*.csv"))
    if limit_files:
        paths = paths[:limit_files]

    frames: list[pd.DataFrame] = []
    for path in paths:
        df = _read_sdot_csv(path)
        if years is not None:
            df = df[df["year"].isin(years)]
        if not df.empty:
            frames.append(df)

    if not frames:
        return pd.DataFrame(columns=["시리얼", "기온", "상대습도", "풍속", "등록일자", "year", "구"])

    return pd.concat(frames, ignore_index=True)


def merge_sdot_with_district(
    weather: pd.DataFrame,
    district: pd.DataFrame | None = None,
) -> pd.DataFrame:
    """시리얼 = 모델 시리얼(*) 기준 left merge, 신규 데이터는 자치구 보조."""
    district = district if district is not None else load_district()
    district = district.copy()
    district["시리얼"] = district["시리얼"].astype(str).str.strip()

    merged = weather.merge(district[["시리얼", "구"]], on="시리얼", how="left")

    if "자치구" in merged.columns:
        fallback = merged["자치구"].map(GU_EN_TO_KO)
        merged["구"] = merged["구"].fillna(fallback)

    return merged


def to_year_json(
    df: pd.DataFrame,
    columns: list[str] | None = None,
) -> dict[str, list[dict]]:
    """연도별 JSON: {"2026": [{기온, 상대습도, ...}, ...], "2025": [...]}"""
    columns = columns or ["시리얼", "구", "기온", "상대습도", "풍속", "등록일자"]
    out: dict[str, list[dict]] = {}

    for year, group in df.dropna(subset=["year"]).groupby("year"):
        chunk = group[columns].copy()
        chunk["등록일자"] = chunk["등록일자"].dt.strftime("%Y-%m-%d %H:%M:%S")
        out[str(int(year))] = chunk.to_dict(orient="records")

    return dict(sorted(out.items(), key=lambda x: x[0], reverse=True))
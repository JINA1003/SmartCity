"""
KEPCO 구별·용도별 전력판매량 xlsx 통합 전처리.

입력: data/file/kepco_electricity_sales/*.xlsx
  두 가지 파일 포맷 모두 header=2, wide format (1월~12월 컬럼):
    - 연간 파일: 연도·시도·시군구·계약종별 + 1월~12월
    - 월별 파일: 동일 구조, 해당 월만 값 있고 나머지 0

출력: year, month, district(구), usage_type(용도), consumption_mwh
"""

from __future__ import annotations

import re
import warnings
from pathlib import Path

import pandas as pd

SALES_DIR = (
    Path(__file__).resolve().parents[2]
    / "data" / "file" / "kepco_electricity_sales"
)

SEOUL_DISTRICTS = [
    "종로구", "중구", "용산구", "성동구", "광진구", "동대문구", "중랑구",
    "성북구", "강북구", "도봉구", "노원구", "은평구", "서대문구", "마포구",
    "양천구", "강서구", "구로구", "금천구", "영등포구", "동작구", "관악구",
    "서초구", "강남구", "송파구", "강동구",
]

USAGE_MAP = {
    "주택용": "주택용",
    "일반용": "일반용",
    "교육용": "교육용",
    "산업용": "산업용",
    "농사용": "농사용",
    "가로등": "가로등",
    "심 야": "심야",
    "심  야": "심야",
    "심야": "심야",
}

MONTH_COLS = {f"{m}월": m for m in range(1, 13)}

# 2004~2013 파일은 MWh, 2014+ 는 kWh → 2014+ 기준으로 ×1000
MWH_THRESHOLD_YEAR = 2014


def _extract_year_month_from_filename(name: str) -> tuple[int | None, int | None]:
    """
    파일명에서 (year, month) 추출. month는 연간 파일이면 None.
    연도/월은 시트 내 컬럼에서 읽는 게 우선이고, 이 함수는 보조용.
    """
    # _YYYYMM 패턴 (월별 파일, e.g. _202201)
    m = re.search(r"_(\d{4})(\d{2})\b", name)
    if m:
        return int(m.group(1)), int(m.group(2))
    # (YYYY) 패턴 (e.g. (2019)시군구별)
    m = re.search(r"\((\d{4})\)", name)
    if m:
        return int(m.group(1)), None
    # YYYY년 패턴
    m = re.search(r"(\d{4})년", name)
    if m:
        return int(m.group(1)), None
    return None, None


def _find_header_row(raw: pd.DataFrame) -> int | None:
    """
    실제 컬럼 헤더 행 번호 반환.
    '시군구' 단독 셀이 있는 행을 찾음 (제목 행의 '시군구별...' 문자열 제외).
    """
    for i in range(min(15, len(raw))):
        row = raw.iloc[i].fillna("").astype(str)
        # 셀 값 중 하나가 정확히 '시군구'인 경우만 헤더로 인정
        if any(v.strip() == "시군구" for v in row):
            return i
    return None


def _parse_sheet(xf: pd.ExcelFile, sheet: str) -> pd.DataFrame | None:
    """
    단일 시트 파싱. 성공하면 wide DataFrame 반환, 실패하면 None.
    컬럼: 연도, 시도, 시군구, 계약종별, 1월 ~ 12월
    """
    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
        raw = xf.parse(sheet, header=None)

    header_row = _find_header_row(raw)
    if header_row is None:
        return None

    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
        df = xf.parse(sheet, header=header_row)

    df.columns = df.columns.astype(str).str.strip()

    if "시군구" not in df.columns:
        return None
    if not any(c in df.columns for c in MONTH_COLS):
        return None

    return df


def load_single_xlsx(path: Path) -> pd.DataFrame:
    """
    단일 xlsx → tidy DataFrame.
    반환 컬럼: year, month, district, usage_type, consumption_mwh
    """
    year_hint, month_hint = _extract_year_month_from_filename(path.name)

    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
        xf = pd.ExcelFile(path)

    rows: list[dict] = []

    for sheet in xf.sheet_names:
        df = _parse_sheet(xf, sheet)
        if df is None:
            continue

        # 서울 행만 필터
        df = df[df["시군구"].astype(str).str.contains("|".join(SEOUL_DISTRICTS), na=False)]
        if df.empty:
            continue

        year_col = "연도" if "연도" in df.columns else None

        for _, row in df.iterrows():
            district_raw = str(row["시군구"]).strip()
            district = next((d for d in SEOUL_DISTRICTS if d in district_raw), None)
            if district is None:
                continue

            usage_raw = str(row.get("계약종별", "")).strip()
            usage = USAGE_MAP.get(usage_raw)
            if usage is None:
                continue

            year = int(row[year_col]) if year_col and pd.notna(row[year_col]) else year_hint
            if year is None:
                continue

            for col_name, month in MONTH_COLS.items():
                if col_name not in df.columns:
                    continue
                val = pd.to_numeric(row.get(col_name), errors="coerce")
                if pd.isna(val) or val == 0:
                    continue

                # 특정 월 파일이면 해당 월만 취함
                if month_hint is not None and month != month_hint:
                    continue

                # 단위 통일: 2014 미만은 MWh → kWh 변환
                if year < MWH_THRESHOLD_YEAR:
                    val = val * 1000

                rows.append({
                    "year": year,
                    "month": month,
                    "district": district,
                    "usage_type": usage,
                    "consumption_mwh": val / 1000,  # kWh → MWh 저장
                })

    return pd.DataFrame(rows)


def load_all() -> pd.DataFrame:
    """
    전체 xlsx 순회 → 서울 구별·용도별 월간 소비량 DataFrame.

    반환 컬럼: year, month, district, usage_type, consumption_mwh
    """
    frames: list[pd.DataFrame] = []
    for path in sorted(SALES_DIR.glob("*.xlsx")):
        try:
            df = load_single_xlsx(path)
            if not df.empty:
                frames.append(df)
        except Exception:
            continue

    if not frames:
        return pd.DataFrame(columns=["year", "month", "district", "usage_type", "consumption_mwh"])

    result = (
        pd.concat(frames, ignore_index=True)
        .drop_duplicates(subset=["year", "month", "district", "usage_type"])
        .sort_values(["year", "month", "district"])
        .reset_index(drop=True)
    )
    return result


if __name__ == "__main__":
    df = load_all()
    print(df.shape)
    print("연도:", df["year"].min(), "~", df["year"].max())
    print("구 수:", df["district"].nunique())
    print("용도:", sorted(df["usage_type"].unique()))
    print(df.head())

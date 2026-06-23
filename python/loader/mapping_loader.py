"""
KEPCO 매핑 데이터 로드.

입력: data/output/kepco_mapping_final_20052026.csv
      한 행 = (구, usage_type, building_type) 조합
      컬럼 — year, month, sigungu, usage_type, building_type,
              power_mwh, usage_ratio, reduction_need_score, ...

구조:
  - usage_type (7종): 가로등·교육용·농사용·산업용·심야·일반용·주택용
      → 용도별 소비량(power_mwh) 집계에 사용
  - building_type (34종): 용도 아래 여러 건물유형이 행으로 펼쳐짐
      → 건물유형별 reduction_need_score 보유

주요 함수:
  get_usage_mwh_map(year, month)
      {(district, usage_type): power_mwh}  — 용도별 실측 소비량

  get_building_score_map(year, month)
      {district: {"공장": score, "교육연구시설": score, ...}}
      구별 건물유형 → reduction_need_score 딕셔너리.
      NaN building_type 행은 제외.
      해당 연월 데이터가 없으면 가장 가까운 과거 연월로 fallback.
"""

from __future__ import annotations

from pathlib import Path

import pandas as pd

MAPPING_CSV = (
    Path(__file__).resolve().parents[2]
    / "data" / "output" / "kepco_mapping_final_20052026.csv"
)

_cache: pd.DataFrame | None = None


def load_mapping() -> pd.DataFrame:
    global _cache
    if _cache is None:
        df = pd.read_csv(MAPPING_CSV, encoding="utf-8-sig")
        df = df.rename(columns={"sigungu": "district"})
        _cache = df[df["usage_type"] != "합계"].reset_index(drop=True)
    return _cache


def _nearest_ym(df: pd.DataFrame, year: int, month: int) -> tuple[int, int]:
    avail = df[["year", "month"]].drop_duplicates()
    avail = avail[
        (avail["year"] < year) | ((avail["year"] == year) & (avail["month"] <= month))
    ]
    if avail.empty:
        row = df[["year", "month"]].drop_duplicates().sort_values(["year", "month"]).iloc[0]
    else:
        row = avail.sort_values(["year", "month"]).iloc[-1]
    return int(row["year"]), int(row["month"])


def _get_sub(year: int, month: int) -> pd.DataFrame:
    df = load_mapping()
    sub = df[(df["year"] == year) & (df["month"] == month)]
    if sub.empty:
        y, m = _nearest_ym(df, year, month)
        sub = df[(df["year"] == y) & (df["month"] == m)]
    return sub


def get_usage_mwh_map(year: int, month: int) -> dict[tuple[str, str], float]:
    """
    반환: {(district, usage_type): power_mwh}
    usage_type 행 중 building_type 첫 번째 행(또는 NaN 행)의 power_mwh 사용.
    같은 usage_type에 building_type이 여러 행이어도 power_mwh는 동일하므로 첫 값만 취함.
    """
    sub = _get_sub(year, month)
    result = {}
    for (district, usage_type), grp in sub.groupby(["district", "usage_type"]):
        result[(str(district), str(usage_type))] = float(grp["power_mwh"].iloc[0])
    return result


def get_building_score_map(year: int, month: int) -> dict[str, dict[str, float]]:
    """
    반환: {district: {building_type: reduction_need_score, ...}}
    building_type이 NaN인 행은 제외.
    예) {"강남구": {"공장": 4.57, "교육연구시설": 0.58, ...}, ...}
    """
    sub = _get_sub(year, month)
    sub = sub[sub["building_type"].notna() & (sub["building_type"].str.strip() != "")]

    result: dict[str, dict[str, float]] = {}
    for _, row in sub.iterrows():
        d  = str(row["district"])
        bt = str(row["building_type"]).strip()
        sc = float(row["reduction_need_score"])
        result.setdefault(d, {})[bt] = sc
    return result


if __name__ == "__main__":
    bmap = get_building_score_map(2023, 8)
    print("강남구 building_type 수:", len(bmap.get("강남구", {})))
    for bt, sc in list(bmap["강남구"].items())[:5]:
        print(f"  {bt}: {sc:.4f}")

"""
KEPCO 구별·용도별 전력 소비량 로드.

입력: data/output/kepco_final_20052026.csv
      컬럼 — year, sido, sigungu, usage_type, month, power_mwh, ...

출력:
  load_consumption() → year, month, district, usage_type, consumption_mwh
"""

from __future__ import annotations

from pathlib import Path

import pandas as pd

KEPCO_CSV = (
    Path(__file__).resolve().parents[2]
    / "data" / "output" / "kepco_final_20052026.csv"
)


def load_consumption() -> pd.DataFrame:
    """
    반환 컬럼: year, month, district, usage_type, consumption_mwh
    (consumption_xgb.train() 에서 기대하는 컬럼명)
    """
    df = pd.read_csv(KEPCO_CSV, encoding="utf-8-sig")
    df = df.rename(columns={
        "sigungu":   "district",
        "power_mwh": "consumption_mwh",
    })
    df["district"] = df["district"].astype(str).str.strip()
    return (
        df[["year", "month", "district", "usage_type", "consumption_mwh"]]
        .dropna()
        [lambda d: d["usage_type"] != "합계"]   # 합계 행 제거
        .sort_values(["year", "month", "district"])
        .reset_index(drop=True)
    )


if __name__ == "__main__":
    df = load_consumption()
    print(df.shape)
    print("연도:", df["year"].min(), "~", df["year"].max())
    print("구 수:", df["district"].nunique())
    print("용도:", sorted(df["usage_type"].unique()))
    print(df.head())

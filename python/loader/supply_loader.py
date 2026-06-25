"""
EPSIS 전국 전력 공급량/예비율 로드.

입력: data/output/epsis_supply_rate_final_20052026.csv
      컬럼 — 년, 월, 공급능력(MW), 최대전력(MW), 공급예비력(MW), 공급예비율(%)

출력:
  load_monthly() → year, month, supply_mw_mean, reserve_rate_mean
"""

from __future__ import annotations

from pathlib import Path

import pandas as pd

SUPPLY_CSV = (
    Path(__file__).resolve().parents[2]
    / "data" / "output" / "epsis_supply_rate_final_20052026.csv"
)


def load_monthly() -> pd.DataFrame:
    """
    반환 컬럼: year, month, supply_mw_mean, reserve_rate_mean
    (supply_regression.train() 에서 기대하는 컬럼명)
    """
    df = pd.read_csv(SUPPLY_CSV, encoding="utf-8-sig")
    df.columns = df.columns.str.strip()

    df = df.rename(columns={
        "년":          "year",
        "월":          "month",
        "공급능력(MW)":  "supply_mw_mean",
        "최대전력(MW)":  "peak_mw",
        "공급예비력(MW)": "reserve_mw",
        "공급예비율(%)":  "reserve_rate_mean",
    })

    for col in ("supply_mw_mean", "peak_mw", "reserve_mw", "reserve_rate_mean"):
        if col in df.columns:
            df[col] = pd.to_numeric(
                df[col].astype(str).str.replace(",", "", regex=False),
                errors="coerce",
            )

    return (
        df[["year", "month", "supply_mw_mean", "reserve_rate_mean"]]
        .dropna()
        .sort_values(["year", "month"])
        .reset_index(drop=True)
    )


if __name__ == "__main__":
    print(load_monthly().tail())

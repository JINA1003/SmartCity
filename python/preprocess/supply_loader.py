"""
EPSIS 전국 전력 공급량/예비율 로드 및 월별 집계.

입력: data/file/epsis_elec_supply.csv
      컬럼 — 년, 월, 일, 설비용량(MW), 공급능력(MW), 최대전력(MW),
              공급예비력(MW), 공급예비율(%)

출력:
  daily   — date, capacity_mw, supply_mw, peak_mw, reserve_mw, reserve_rate
  monthly — year, month, supply_mw_mean, peak_mw_max, reserve_rate_mean
"""

from __future__ import annotations

from pathlib import Path

import pandas as pd

SUPPLY_CSV = (
    Path(__file__).resolve().parents[2]
    / "data" / "file" / "epsis_elec_supply.csv"
)


def load_daily() -> pd.DataFrame:
    df = pd.read_csv(SUPPLY_CSV, encoding="utf-8-sig")
    df.columns = df.columns.str.strip()

    # 최소전력 컬럼이 있을 수도 있으나 모델에선 불필요 — 핵심 컬럼만 추출
    rename = {
        "년": "year",
        "월": "month",
        "일": "day",
        "공급능력(MW)": "supply_mw",
        "최대전력(MW)": "peak_mw",
        "공급예비력(MW)": "reserve_mw",
        "공급예비율(%)": "reserve_rate",
    }
    df = df.rename(columns={k: v for k, v in rename.items() if k in df.columns})

    for col in ("supply_mw", "peak_mw", "reserve_mw", "reserve_rate"):
        if col in df.columns:
            df[col] = (
                df[col].astype(str).str.replace(",", "", regex=False)
            )
            df[col] = pd.to_numeric(df[col], errors="coerce")

    df["date"] = pd.to_datetime(
        df[["year", "month", "day"]].rename(columns={"year": "year", "month": "month", "day": "day"}),
        errors="coerce",
    )
    df = df.dropna(subset=["date"]).sort_values("date").reset_index(drop=True)
    return df


def load_monthly() -> pd.DataFrame:
    daily = load_daily()
    monthly = daily.groupby(["year", "month"]).agg(
        supply_mw_mean=("supply_mw", "mean"),
        peak_mw_max=("peak_mw", "max"),
        reserve_rate_mean=("reserve_rate", "mean"),
        reserve_rate_min=("reserve_rate", "min"),
    ).reset_index()
    return monthly


if __name__ == "__main__":
    print(load_monthly().tail())

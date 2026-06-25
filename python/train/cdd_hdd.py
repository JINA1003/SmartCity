"""
CDD / HDD (냉방·난방도일) 계산.

과거(2006~현재): ASOS 일별 실측 ta_mean 사용
미래(2025~2030): temp_trend.predict() 로 얻은 월평균기온 근사식 사용
  월간 CDD ≈ max(0, ta_mean_monthly - COOL_BASE) × days_in_month
  월간 HDD ≈ max(0, HEAT_BASE - ta_mean_monthly) × days_in_month

기준온도:
  냉방(CDD): 24℃  (한국가스공사 컨벤션)
  난방(HDD): 18℃
"""

from __future__ import annotations

import calendar

import pandas as pd

COOL_BASE = 24.0
HEAT_BASE = 18.0


def daily_cdd(ta_mean: float) -> float:
    return max(0.0, ta_mean - COOL_BASE)


def daily_hdd(ta_mean: float) -> float:
    return max(0.0, HEAT_BASE - ta_mean)


def monthly_cdd_from_daily(daily: pd.DataFrame) -> pd.DataFrame:
    """
    daily: date, ta_mean 컬럼 포함
    반환: year, month, cdd, hdd
    """
    df = daily.copy()
    df["year"] = df["date"].dt.year
    df["month"] = df["date"].dt.month
    df["cdd_d"] = df["ta_mean"].apply(daily_cdd)
    df["hdd_d"] = df["ta_mean"].apply(daily_hdd)
    return (
        df.groupby(["year", "month"])
        .agg(cdd=("cdd_d", "sum"), hdd=("hdd_d", "sum"))
        .reset_index()
    )


def monthly_cdd_from_monthly_mean(monthly: pd.DataFrame) -> pd.DataFrame:
    """
    미래 구간용 근사.
    monthly: year, month, ta_mean_pred (temp_trend.predict_batch 결과 포함)
    """
    df = monthly.copy()
    df["days"] = df.apply(
        lambda r: calendar.monthrange(int(r["year"]), int(r["month"]))[1], axis=1
    )
    df["cdd"] = df.apply(
        lambda r: max(0.0, r["ta_mean_pred"] - COOL_BASE) * r["days"], axis=1
    )
    df["hdd"] = df.apply(
        lambda r: max(0.0, HEAT_BASE - r["ta_mean_pred"]) * r["days"], axis=1
    )
    return df[["year", "month", "cdd", "hdd"]]


if __name__ == "__main__":
    from python.loader.asos_daily import load_monthly, to_daily, load_hourly
    daily = to_daily(load_hourly())
    result = monthly_cdd_from_daily(daily)
    print(result[result["month"] == 8].tail())

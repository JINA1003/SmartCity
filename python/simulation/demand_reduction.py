"""
수요감축필요도 지수 계산.

공식:
  지수(구, 용도) = consumption_mwh × crisis_coef × (1 - blackout_immunity)

  crisis_coef      : 공급 경보단계별 위기계수 (alert_level.AlertLevel.crisis_coef)
  blackout_immunity: 용도별 블랙아웃 면역계수 (0=완전차단불가, 1=완전차단가능)

용도별 면역계수 (blackout_immunity):
  병원/응급     : 0.0  (차단 불가)
  주택용        : 0.3
  교육용        : 0.4
  일반용        : 0.6
  가로등        : 0.5
  농사용        : 0.5
  산업용        : 0.9  (차단 우선)
  심야          : 0.8

점수가 높을수록 블랙아웃 우선 대상.
"""

from __future__ import annotations

import pandas as pd

from python.simulation.alert_level import AlertLevel

BLACKOUT_IMMUNITY: dict[str, float] = {
    "병원": 0.0,
    "응급": 0.0,
    "주택용": 0.3,
    "교육용": 0.4,
    "일반용": 0.6,
    "가로등": 0.5,
    "농사용": 0.5,
    "산업용": 0.9,
    "심야": 0.8,
}

DEFAULT_IMMUNITY = 0.5


def calc_index(
    consumption_mwh: float,
    usage_type: str,
    alert: AlertLevel,
) -> float:
    """단일 (구, 용도) 수요감축필요도 지수."""
    immunity = BLACKOUT_IMMUNITY.get(usage_type, DEFAULT_IMMUNITY)
    return consumption_mwh * alert.crisis_coef * (1.0 - immunity)


def calc_district_table(
    consumption_df: pd.DataFrame,
    alert: AlertLevel,
) -> pd.DataFrame:
    """
    consumption_df: district, usage_type, consumption_mwh 컬럼 포함
    반환: district, usage_type, consumption_mwh, demand_reduction_index
    """
    df = consumption_df.copy()
    df["demand_reduction_index"] = df.apply(
        lambda r: calc_index(r["consumption_mwh"], r["usage_type"], alert),
        axis=1,
    )
    df["district_total"] = df.groupby("district")["demand_reduction_index"].transform("sum")
    return df.sort_values(
        ["district_total", "demand_reduction_index"], ascending=False
    ).reset_index(drop=True)

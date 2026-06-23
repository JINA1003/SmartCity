"""
블랙아웃 우선순위 순회 시뮬레이션.

로직:
  1. 구 단위 수요감축필요도 합산 → 높은 구부터 순위
  2. 구 내 용도 단위 지수 → 높은 용도부터 순위
  3. 경보단계가 '경계' 이상일 때만 블랙아웃 실행
  4. 목표 감축량(target_mwh)만큼 순회하며 블랙아웃 적용

출력: 블랙아웃 적용된 (district, usage_type) 목록 + 감축량 누계
"""

from __future__ import annotations

import pandas as pd

from python.simulation.alert_level import AlertLevel


BLACKOUT_MIN_LEVEL = AlertLevel.ALERT


def run_blackout(
    demand_table: pd.DataFrame,
    alert: AlertLevel,
    target_reduction_mwh: float | None = None,
) -> pd.DataFrame:
    """
    demand_table: demand_reduction.calc_district_table() 결과
                  컬럼 — district, usage_type, consumption_mwh,
                          demand_reduction_index, district_total

    target_reduction_mwh: None이면 경보단계에 따라 자동 산정
                          (심각=30%, 경계=15% 감축 목표)
    반환: district, usage_type, consumption_mwh, demand_reduction_index,
           blackout (bool), cumulative_cut_mwh
    """
    df = demand_table.copy()
    df["blackout"] = False
    df["cumulative_cut_mwh"] = 0.0

    if alert < BLACKOUT_MIN_LEVEL:
        return df

    total_consumption = df["consumption_mwh"].sum()
    if target_reduction_mwh is None:
        target_pct = {AlertLevel.ALERT: 0.15, AlertLevel.CRITICAL: 0.30}.get(alert, 0.0)
        target_reduction_mwh = total_consumption * target_pct

    cumulative = 0.0
    for idx in df.index:
        if cumulative >= target_reduction_mwh:
            break
        df.at[idx, "blackout"] = True
        cumulative += df.at[idx, "consumption_mwh"]
        df.at[idx, "cumulative_cut_mwh"] = cumulative

    return df


def blackout_summary(result: pd.DataFrame) -> dict:
    cut = result[result["blackout"]]
    return {
        "blackout_count": int(cut.shape[0]),
        "total_cut_mwh": float(cut["consumption_mwh"].sum()),
        "districts_affected": cut["district"].nunique(),
        "items": cut[["district", "usage_type", "consumption_mwh"]].to_dict("records"),
    }

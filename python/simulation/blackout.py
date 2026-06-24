"""
블랙아웃 순회 시뮬레이션.

순회 기준:
  1. 구(district) 순서  → 구별 total_consumption_mwh 내림차순
  2. 구 내 정전 순서   → building_type별 reduction_need_score 내림차순

경보단계(AlertLevel)별 목표 감축량:
  경계(ALERT)   : 전체 소비량의 15%
  심각(CRITICAL): 전체 소비량의 30%
  그 미만       : 블랙아웃 미실행
"""

from __future__ import annotations

import pandas as pd

from python.simulation.alert_level import AlertLevel

BLACKOUT_MIN_LEVEL = AlertLevel.ALERT

REDUCTION_TARGET_PCT: dict[AlertLevel, float] = {
    AlertLevel.ALERT:    0.15,
    AlertLevel.CRITICAL: 0.30,
}


def run_blackout(
    regions: list[dict],
    building_score_map: dict[str, dict[str, float]],
    alert: AlertLevel,
    target_reduction_mwh: float | None = None,
) -> dict:
    """
    Parameters
    ----------
    regions : _predict_one_month() 결과의 regions 리스트
    building_score_map : get_building_score_map(year, month) 결과
        {district: {building_type: reduction_need_score, ...}}
    alert : AlertLevel
    target_reduction_mwh : None이면 경보단계에 따라 자동 산정

    Returns
    -------
    {
      "districts_order":  [...25개 구, 소비량 내림차순...],
      "districts_affected": int,
      "blackout_items": [{"gu": ..., "building_type": ..., "reduction_need_score": ...}, ...]
    }
    경보단계 미달 시 blackout_items 빈 리스트.
    """
    # 전체 구를 소비량 내림차순으로 정렬 (경보 여부 무관하게 항상 반환)
    sorted_regions = sorted(regions, key=lambda r: r["total_consumption_mwh"], reverse=True)
    districts_order = [r["gu"] for r in sorted_regions]

    if alert < BLACKOUT_MIN_LEVEL:
        return {
            "districts_order":    districts_order,
            "districts_affected": 0,
            "blackout_items":     [],
        }

    total_consumption = sum(r["total_consumption_mwh"] for r in regions)
    if target_reduction_mwh is None:
        pct = REDUCTION_TARGET_PCT.get(alert, 0.0)
        target_reduction_mwh = total_consumption * pct

    blackout_items = []
    cumulative = 0.0
    affected_gu: set[str] = set()

    for region in sorted_regions:
        if cumulative >= target_reduction_mwh:
            break

        gu = region["gu"]
        bt_scores = building_score_map.get(gu, {})

        # building_type → reduction_need_score 내림차순
        for bt, score in sorted(bt_scores.items(), key=lambda x: x[1], reverse=True):
            if cumulative >= target_reduction_mwh:
                break

            total_score = sum(bt_scores.values())
            cut_mwh = (
                region["total_consumption_mwh"] * (score / total_score)
                if total_score > 0 else 0.0
            )
            cumulative += cut_mwh
            affected_gu.add(gu)
            blackout_items.append({
                "gu":                   gu,
                "building_type":        bt,
                "reduction_need_score": round(score, 4),
            })

    return {
        "districts_order":    districts_order,
        "districts_affected": len(affected_gu),
        "blackout_items":     blackout_items,
    }

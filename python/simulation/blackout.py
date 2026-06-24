"""
블랙아웃 순회 시뮬레이션.

순회 기준:
  1. 구(district) 순서  → 구별 total_consumption_mwh 내림차순
  2. 구 내 정전 순서   → building_type별 reduction_need_score 내림차순

경보단계(AlertLevel)별 동작:
  정상/관심/주의 (< 경계) : 블랙아웃 미실행 — blackout_items 빈 리스트
  경계 (ALERT)            : 전체 소비량의 15% 감축 대상 순차 출력
  심각 (CRITICAL)         : 전체 소비량의 30% 감축 대상 순차 출력
"""

from __future__ import annotations

from python.simulation.alert_level import AlertLevel

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
        각 항목에 gu, ta_gu, total_consumption_mwh 포함.
    building_score_map : get_building_score_map(year, month) 결과
        {district: {building_type: reduction_need_score, ...}}
    alert : AlertLevel
    target_reduction_mwh : None이면 경보단계에 따라 자동 산정

    Returns
    -------
    {
      "districts_order":    [...25개 구 이름, 소비량 내림차순...],
      "districts_affected": int,
      "blackout_items": [
        {
          "gu": str,
          "building_type": str,
          "reduction_need_score": float,
          "ta_gu": float,   ← 해당 구 예측 기온
        }, ...
      ]
    }

    단계별 blackout_items 내용:
      정상/관심/주의 : 빈 리스트
      경계/심각      : reduction_need_score 내림차순으로 감축 목표 달성까지 순차 나열
    """
    sorted_regions = sorted(regions, key=lambda r: r["total_consumption_mwh"], reverse=True)
    districts_order = [r["gu"] for r in sorted_regions]

    # 정상/관심/주의: 블랙아웃 없음
    if alert < AlertLevel.ALERT:
        return {
            "districts_order":    districts_order,
            "districts_affected": 0,
            "blackout_items":     [],
        }

    # 경계/심각: 감축 목표량 달성까지 순차 단전
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

        gu    = region["gu"]
        ta_gu = region.get("ta_gu", 0.0)
        bt_scores = building_score_map.get(gu, {})
        total_score = sum(bt_scores.values())

        for bt, score in sorted(bt_scores.items(), key=lambda x: x[1], reverse=True):
            if cumulative >= target_reduction_mwh:
                break

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
                "ta_gu":                round(ta_gu, 2),
            })

    return {
        "districts_order":    districts_order,
        "districts_affected": len(affected_gu),
        "blackout_items":     blackout_items,
    }

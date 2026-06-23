"""
전국 전력 공급 경보단계 판정.

예비율(%) 기준 (한전/에너지부 기준 반영):
  정상  : 예비율 >= 15%
  관심  : 10% <= 예비율 < 15%
  주의  : 7%  <= 예비율 < 10%
  경계  : 5%  <= 예비율 < 7%
  심각  : 예비율 < 5%

전국 단위 고정 (Unity 좌상단 패널 표시용).
"""

from __future__ import annotations

from enum import IntEnum


class AlertLevel(IntEnum):
    NORMAL = 0   # 정상
    CAUTION = 1  # 관심
    WARNING = 2  # 주의
    ALERT = 3    # 경계
    CRITICAL = 4 # 심각

    @property
    def label_ko(self) -> str:
        return {0: "정상", 1: "관심", 2: "주의", 3: "경계", 4: "심각"}[self.value]

    @property
    def crisis_coef(self) -> float:
        """수요감축필요도 지수 계산에 쓰이는 공급위기계수 (0~1)."""
        return {0: 0.0, 1: 0.2, 2: 0.4, 3: 0.7, 4: 1.0}[self.value]


def get_alert_level(reserve_rate: float) -> AlertLevel:
    if reserve_rate >= 15.0:
        return AlertLevel.NORMAL
    elif reserve_rate >= 10.0:
        return AlertLevel.CAUTION
    elif reserve_rate >= 7.0:
        return AlertLevel.WARNING
    elif reserve_rate >= 5.0:
        return AlertLevel.ALERT
    else:
        return AlertLevel.CRITICAL


if __name__ == "__main__":
    for r in [20.0, 12.0, 8.5, 6.0, 3.0]:
        lv = get_alert_level(r)
        print(f"예비율 {r:5.1f}% → {lv.label_ko} (위기계수 {lv.crisis_coef})")

"""
전체 모델 학습 파이프라인 진입점.

실행:
  python -m python.train_pipeline

폴더 구조:
  python/loader/    → 원시 데이터 로드 전용
  python/preprocess/→ 전처리/파라미터 계산 (gu_offset, build_temperature_gu)
  python/train/     → 모델 학습 (temp_trend, supply_regression, consumption_xgb, cdd_hdd)
  python/model/     → 학습된 artifact 저장 (pkl 파일)
  python/api/       → Flask API 서버
  python/simulation/→ 경보/수요감축/정전 시뮬레이션

학습 순서:
  1. 데이터 로드 (ASOS, ONI, output CSV)
  2. [모델 1] 기준기온 회귀 (temp_trend)
       입력: year, month, ONI  →  출력: ta_asos
  3. 구별 오프셋 파라미터 추정 (S-DoT → gu_offset) → 파일 저장
       [모델 2는 파라미터 기반: ta_gu = ta_asos + offset(gu,month) × multiplier]
  4. [모델 3] 소비량 XGBoost
       입력: year, month, oni, cdd_gu, hdd_gu, district, usage_type
  5. [모델 4] 공급량 회귀
       입력: year, month, oni, cdd, hdd

[전제조건]
  data/output/temperature_gu_monthly.csv
  data/output/kepco_final_20052026.csv
  data/output/epsis_supply_rate_final_20052026.csv
  data/output/sdot_gu_daily_cache.csv
  data/file/oni.csv
  data/file/asos_weather_data/asos_108_*.csv  (연도별 자동 합산)
"""

from __future__ import annotations

import logging

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
log = logging.getLogger(__name__)


def main() -> None:
    # ── 1. 데이터 로드 ──────────────────────────────────────────────────────
    log.info("=== Step 1: 데이터 로드 ===")
    from python.loader.asos_daily import load_hourly, to_daily, to_monthly
    from python.loader.oni_loader import load_oni

    hourly       = load_hourly()
    daily        = to_daily(hourly)
    monthly_asos = to_monthly(daily)
    oni          = load_oni()

    log.info(
        "ASOS monthly: %d행 | ONI: %d행",
        len(monthly_asos), len(oni),
    )

    # ── 2. [모델 1] 기준기온 회귀 ────────────────────────────────────────────
    log.info("=== Step 2: [모델 1] 기준기온 회귀 학습 (ASOS + ONI) ===")
    from python.train.temp_trend import train as train_temp, predict as pred_temp
    temp_model = train_temp(monthly_asos, oni)

    log.info("  2030년 8월 ONI=1.0 → %.2f℃", pred_temp(2030, 8, 1.0, temp_model))
    log.info("  2015년 8월 ONI=1.0 → %.2f℃  (두 값이 달라야 정상)", pred_temp(2015, 8, 1.0, temp_model))

    # ── 3. [모델 2] 구별 오프셋 파라미터 추정 ──────────────────────────────
    log.info("=== Step 3: [모델 2] 구별 오프셋 파라미터 추정 (S-DoT) ===")
    from python.preprocess.gu_offset import (
        load_sdot_gu_daily,
        calc_monthly_offset,
        calc_dynamic_multiplier,
        save_gu_params,
        predict_gu_temp,
    )

    sdot_daily   = load_sdot_gu_daily()
    month_offset = calc_monthly_offset(sdot_daily, daily)
    calc_dynamic_multiplier(daily)

    daily_tmp = daily.copy()
    daily_tmp["month_"] = daily_tmp["date"].dt.month
    ws_clim = {
        int(m): round(float(v), 3)
        for m, v in daily_tmp.groupby("month_")["ws_mean"].mean().items()
    }
    save_gu_params(month_offset, ws_clim)
    log.info("  오프셋 %d개 (구×월) → data/output/gu_offset_params.pkl 저장", len(month_offset))

    ta_2015 = pred_temp(2015, 8, 1.0, temp_model)
    ta_2030 = pred_temp(2030, 8, 1.0, temp_model)
    gu_2015 = predict_gu_temp(ta_2015, 8, month_offset)
    gu_2030 = predict_gu_temp(ta_2030, 8, month_offset)
    sample  = next(iter(gu_2015))
    log.info("  검증 — %s 8월 ONI=1.0 | 2015: %.2f℃  2030: %.2f℃",
             sample, gu_2015[sample], gu_2030[sample])

    # ── 4. [모델 3] 소비량 XGBoost ──────────────────────────────────────────
    log.info("=== Step 4: [모델 3] 소비량 XGBoost 학습 ===")
    from python.train.consumption_xgb import train as train_cons
    train_cons(oni)
    log.info("  피처: year, month, oni, cdd_gu, hdd_gu, district, usage_type → 완료")

    # ── 5. [모델 4] 공급량 회귀 ─────────────────────────────────────────────
    log.info("=== Step 5: [모델 4] 공급량 회귀 학습 ===")
    from python.train.cdd_hdd import monthly_cdd_from_daily
    from python.train.supply_regression import train as train_supply, predict as pred_supply

    cdd_hdd = monthly_cdd_from_daily(daily)
    train_supply(cdd_hdd, oni)

    r2015 = pred_supply(2015, 8, 1.0,
                        float(cdd_hdd[(cdd_hdd["year"]==2015)&(cdd_hdd["month"]==8)]["cdd"].mean()
                              if not cdd_hdd[(cdd_hdd["year"]==2015)&(cdd_hdd["month"]==8)].empty else 120.0),
                        0.0)
    r2030 = pred_supply(2030, 8, 1.0, 120.0, 0.0)
    log.info("  검증 — 2015년 8월: supply_mw=%.0f, reserve=%.1f%%",
             r2015["supply_mw"], r2015["reserve_rate"])
    log.info("  검증 — 2030년 8월: supply_mw=%.0f, reserve=%.1f%%",
             r2030["supply_mw"], r2030["reserve_rate"])

    log.info("=== 완료 ===")
    log.info("  모델1: python/train/artifacts/temp_trend_model.pkl")
    log.info("  모델2: data/output/gu_offset_params.pkl")
    log.info("  모델3: python/train/artifacts/consumption_xgb.pkl + consumption_encoders.pkl")
    log.info("  모델4: python/train/artifacts/supply_model.pkl")


if __name__ == "__main__":
    main()

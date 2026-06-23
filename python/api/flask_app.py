"""
Flask REST API — Unity ↔ Python 브릿지.

엔드포인트:
  GET  /health
      반환: {"status": "ok"}

  GET  /predict?year=2030&month=8&oni=1.2
      [입력] year, month, oni (슬라이더)
      [예측] 전체 25구 결과 한 번에 반환.
      반환:
        {
          "input":  {"year": 2030, "month": 8, "oni": 1.2},
          "is_simulated": true,    ← 2026년 이후 또는 ONI 실측과 다를 때 true

          "predicted": {
            "asos_temp": 28.4,     ← [예측] 서울 기준기온 (ASOS 지점)
            "supply": {"supply_mw": 89500.0, "reserve_rate": 12.3},
            "regions": [
              {
                "gu": "강남구",
                "ta_gu": 29.8,           ← [예측] 구별 월평균기온
                "cdd_gu": 178.2,
                "hdd_gu": 0.0,
                "total_consumption_mwh": 4821.3,
                "reduction_need_score": null,   ← 추후 외부 데이터 연결
                "usage": {"주택용": {"consumption_mwh": 412.3}, ...},
                "building_type": {}             ← 추후 34종 건물유형 데이터 연결
              }, ...
            ]
          }
        }

  GET  /predict/annual?year=2030&oni=1.2
      [입력] year, oni
      [예측] 1~12월 전체 — 연간 그래프용. 과거 연도도 동일하게 동작.
      반환:
        {
          "input": {"year": 2030, "oni": 1.2},
          "months": [
            {
              "month": 1,
              "is_simulated": true,
              "predicted": {
                "asos_temp": -1.8,
                "supply": {"supply_mw": 72000.0, "reserve_rate": 18.2},
                "regions": [
                  {"gu": "강남구", "ta_gu": -0.9, "cdd_gu": 0.0,
                   "hdd_gu": 588.1, "total_consumption_mwh": 4821.3,
                   "reduction_need_score": null,
                   "building_type": {}}, ...
                ]
              }
            }, ...12개
          ]
        }

  POST /blackout_simulation
      body: {year, month, oni}
      반환: {alert_level, alert_label, blackout_items: [...]}
"""

from __future__ import annotations

import calendar

from flask import Flask, jsonify, request

app = Flask(__name__)

# ---------------------------------------------------------------------------
# 모델/파라미터 로드 (서버 기동 시 1회)
# ---------------------------------------------------------------------------

_temp_model         = None
_consumption_model  = None
_consumption_encoders = None
_supply_model       = None
_monthly_offset: dict | None = None   # {(gu, month): offset_degC}
_ws_clim: dict | None = None          # {month: 평년풍속}


def _lazy_load() -> None:
    global _temp_model, _consumption_model, _consumption_encoders
    global _supply_model, _monthly_offset, _ws_clim

    if _temp_model is not None:
        return

    from python.train.temp_trend import load_model as load_temp
    from python.train.consumption_xgb import load_model as load_cons
    from python.train.supply_regression import load_model as load_supply
    from python.preprocess.gu_offset import load_gu_params

    _temp_model                             = load_temp()
    _consumption_model, _consumption_encoders = load_cons()
    _supply_model                           = load_supply()
    _monthly_offset, _ws_clim              = load_gu_params()


# ---------------------------------------------------------------------------
# 헬퍼
# ---------------------------------------------------------------------------

# 실측 ONI 데이터 (서버 기동 시 1회 로드, is_simulated 판별용)
_oni_actual: dict[tuple[int, int], float] | None = None

def _get_oni_actual() -> dict[tuple[int, int], float]:
    global _oni_actual
    if _oni_actual is None:
        from python.loader.oni_loader import load_oni
        oni_df = load_oni()
        _oni_actual = {
            (int(r["year"]), int(r["month"])): float(r["oni"])
            for _, r in oni_df.iterrows()
        }
    return _oni_actual


def _is_simulated(year: int, month: int, oni: float) -> bool:
    """
    입력값이 실제 과거 기록과 다른 '시뮬레이션'인지 판별.
    - 미래(데이터 없는 연월): True
    - 과거인데 ONI를 실측과 다르게 설정: True
    - 과거 실측 ONI 그대로: False
    """
    actual = _get_oni_actual()
    if (year, month) not in actual:
        return True  # 미래
    return abs(actual[(year, month)] - oni) > 0.05  # ONI 변경 여부 (±0.05 허용)


def _asos_cdd_hdd(ta_asos: float, year: int, month: int) -> tuple[float, float]:
    """ASOS 예측 기온 → 월간 CDD/HDD (공급량 모델 입력용)."""
    days = calendar.monthrange(year, month)[1]
    cdd  = max(0.0, ta_asos - 24.0) * days
    hdd  = max(0.0, 18.0 - ta_asos) * days
    return round(cdd, 2), round(hdd, 2)


def _predict_one_month(year: int, month: int, oni: float) -> dict:
    """
    단일 (year, month, oni) → 예측 결과 dict.
    /predict 와 /predict/annual 양쪽에서 공용 사용.
    """
    from python.train.temp_trend import predict as pred_temp
    from python.train.supply_regression import predict as pred_supply
    from python.train.consumption_xgb import predict_all_districts
    from python.preprocess.gu_offset import predict_gu_temp

    USAGE_TYPES = ["가로등", "교육용", "농사용", "산업용", "심야", "일반용", "주택용"]

    ta_asos         = pred_temp(year, month, oni, _temp_model)
    cdd_asos, hdd_asos = _asos_cdd_hdd(ta_asos, year, month)
    supply_info     = pred_supply(year, month, oni, cdd_asos, hdd_asos, _supply_model)
    gu_temps        = predict_gu_temp(ta_asos, month, _monthly_offset, multiplier=1.0)
    cons_df         = predict_all_districts(
        year=year, month=month, oni=oni,
        gu_temps=gu_temps, usage_types=USAGE_TYPES,
        model=_consumption_model, encoders=_consumption_encoders,
    )

    days = calendar.monthrange(year, month)[1]
    regions = []
    for gu, ta_gu in sorted(gu_temps.items()):
        cdd_gu = round(max(0.0, ta_gu - 24.0) * days, 2)
        hdd_gu = round(max(0.0, 18.0 - ta_gu) * days, 2)
        gu_rows = cons_df[cons_df["district"] == gu]
        usage_dict = {
            row["usage_type"]: {"consumption_mwh": round(float(row["consumption_mwh"]), 2)}
            for _, row in gu_rows.iterrows()
        }
        total = round(float(gu_rows["consumption_mwh"].sum()), 2)
        regions.append({
            "gu":                    gu,
            "ta_gu":                 round(ta_gu, 2),
            "cdd_gu":                cdd_gu,
            "hdd_gu":                hdd_gu,
            "total_consumption_mwh": total,
            "reduction_need_score":  None,   # 추후 외부 데이터 연결
            "usage":                 usage_dict,
            "building_type":         {},     # 추후 34종 건물유형 데이터 연결
        })

    return {
        "asos_temp": round(ta_asos, 2),
        "supply": {
            "supply_mw":    round(supply_info["supply_mw"], 1),
            "reserve_rate": round(supply_info["reserve_rate"], 2),
        },
        "regions": regions,
    }


# ---------------------------------------------------------------------------
# 엔드포인트
# ---------------------------------------------------------------------------

@app.get("/health")
def health():
    return jsonify({"status": "ok"})


@app.get("/predict")
def predict():
    """
    단일 월 예측.  GET /predict?year=2030&month=8&oni=1.2
    """
    _lazy_load()
    try:
        year  = int(request.args["year"])
        month = int(request.args["month"])
        oni   = float(request.args["oni"])
    except (KeyError, ValueError) as e:
        return jsonify({"error": f"파라미터 오류: {e}"}), 400

    predicted = _predict_one_month(year, month, oni)
    return jsonify({
        "input":        {"year": year, "month": month, "oni": oni},
        "is_simulated": _is_simulated(year, month, oni),
        "predicted":    predicted,
    })


@app.get("/predict/annual")
def predict_annual():
    """
    연간 그래프용. GET /predict/annual?year=2030&oni=1.2

    해당 연도 1~12월 전체를 한 번에 반환.
    - 과거 연도(2005~2025)도 동작: ONI를 실측과 다르게 주면 반사실적 시나리오
    - is_simulated: 해당 연도+ONI 조합이 실측 기록과 다를 때 true
    - 구별 usage 상세는 포함하지 않음 (그래프용 집계치만)
    """
    _lazy_load()
    try:
        year = int(request.args["year"])
        oni  = float(request.args["oni"])
    except (KeyError, ValueError) as e:
        return jsonify({"error": f"파라미터 오류: {e}"}), 400

    months_data = []
    for m in range(1, 13):
        predicted = _predict_one_month(year, m, oni)
        # 연간 그래프용: usage 상세는 제외, 구별 집계치 + stub 필드만
        regions_summary = [
            {
                "gu":                    reg["gu"],
                "ta_gu":                 reg["ta_gu"],
                "cdd_gu":                reg["cdd_gu"],
                "hdd_gu":                reg["hdd_gu"],
                "total_consumption_mwh": reg["total_consumption_mwh"],
                "reduction_need_score":  reg["reduction_need_score"],
                "building_type":         reg["building_type"],
            }
            for reg in predicted["regions"]
        ]
        months_data.append({
            "month":        m,
            "is_simulated": _is_simulated(year, m, oni),
            "predicted": {
                "asos_temp": predicted["asos_temp"],
                "supply":    predicted["supply"],
                "regions":   regions_summary,
            },
        })

    return jsonify({
        "input":  {"year": year, "oni": oni},
        "months": months_data,
    })


@app.post("/blackout_simulation")
def blackout_simulation():
    _lazy_load()
    data = request.get_json(force=True)

    try:
        year  = int(data["year"])
        month = int(data["month"])
        oni   = float(data["oni"])
    except (KeyError, ValueError) as e:
        return jsonify({"error": f"파라미터 오류: {e}"}), 400

    from python.train.temp_trend import predict as pred_temp
    from python.train.supply_regression import predict as pred_supply
    from python.train.consumption_xgb import predict_all_districts
    from python.preprocess.gu_offset import predict_gu_temp
    from python.simulation.alert_level import get_alert_level
    from python.simulation.demand_reduction import calc_district_table
    from python.simulation.blackout import run_blackout, blackout_summary

    import pandas as pd

    ta_asos         = pred_temp(year, month, oni, _temp_model)
    cdd_asos, hdd_asos = _asos_cdd_hdd(ta_asos, year, month)
    supply_info     = pred_supply(year, month, oni, cdd_asos, hdd_asos, _supply_model)
    alert           = get_alert_level(supply_info["reserve_rate"])

    gu_temps = predict_gu_temp(ta_asos, month, _monthly_offset, multiplier=1.0)
    cons_df  = predict_all_districts(
        year=year, month=month, oni=oni,
        gu_temps=gu_temps, usage_types=["가로등", "교육용", "농사용", "산업용", "심야", "일반용", "주택용"],
        model=_consumption_model, encoders=_consumption_encoders,
    )
    cons_df = cons_df.rename(columns={"district": "district"})

    demand_table = calc_district_table(cons_df, alert)
    result       = run_blackout(demand_table, alert)
    summary      = blackout_summary(result)
    summary["alert_level"] = int(alert)
    summary["alert_label"] = alert.label_ko

    return jsonify(summary)


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=True)

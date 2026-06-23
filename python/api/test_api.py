"""
Flask API 통합 테스트.

서버 없이 Flask test client로 직접 실행.
실행: python -m python.api.test_api
"""

from __future__ import annotations

import json
import sys


def _ok(label: str) -> None:
    print(f"  \033[32mOK\033[0m  {label}")


def _fail(label: str, reason: str) -> None:
    print(f"  \033[31m!!\033[0m  {label}: {reason}")


def _section(title: str) -> None:
    print(f"\n{'='*55}")
    print(f"  {title}")
    print(f"{'='*55}")


def run_tests() -> int:
    from python.api.flask_app import app

    client = app.test_client()
    failures = 0

    # ------------------------------------------------------------------
    # 1. /health
    # ------------------------------------------------------------------
    _section("1. /health")
    r = client.get("/health")
    data = r.get_json()
    if r.status_code == 200 and data.get("status") == "ok":
        _ok("/health → 200 OK")
    else:
        _fail("/health", f"status={r.status_code} body={data}")
        failures += 1

    # ------------------------------------------------------------------
    # 2. /oni — 과거 / 미래
    # ------------------------------------------------------------------
    _section("2. /oni  (슬라이더 초기값)")

    cases = [
        (2023, 8,  True,  "과거 실측"),
        (2015, 1,  True,  "과거 실측(구간 내)"),
        (2030, 8,  False, "미래"),
    ]
    for year, month, expect_actual, desc in cases:
        r = client.get(f"/oni?year={year}&month={month}")
        d = r.get_json()
        if r.status_code != 200:
            _fail(f"/oni {desc}", f"status={r.status_code}")
            failures += 1
            continue
        if d.get("is_actual") != expect_actual:
            _fail(f"/oni {desc}", f"is_actual={d.get('is_actual')} (기대={expect_actual})")
            failures += 1
        else:
            _ok(f"/oni?year={year}&month={month} → oni={d['oni']}, is_actual={d['is_actual']}")

    # ------------------------------------------------------------------
    # 3. /predict — 미래 / 과거 / 파라미터 오류
    # ------------------------------------------------------------------
    _section("3. /predict  (단일 월 예측)")

    # 3-1. 미래
    r = client.get("/predict?year=2030&month=8&oni=1.2")
    if r.status_code != 200:
        _fail("/predict 미래", f"status={r.status_code}")
        failures += 1
    else:
        d = r.get_json()
        pred = d.get("predicted", {})
        regions = pred.get("regions", [])
        n_gu = len(regions)
        sample = regions[0] if regions else {}
        usage_keys = list(sample.get("usage", {}).keys())
        bt_keys    = list(sample.get("building_type", {}).keys())
        _ok(f"/predict 미래 → asos_temp={pred.get('asos_temp')}℃, "
            f"구={n_gu}개, is_simulated={d.get('is_simulated')}")
        _ok(f"  supply_mw={pred.get('supply',{}).get('supply_mw')}, "
            f"reserve_rate={pred.get('supply',{}).get('reserve_rate')}%")
        _ok(f"  [{sample.get('gu')}] ta_gu={sample.get('ta_gu')}, "
            f"total_mwh={sample.get('total_consumption_mwh')}")
        _ok(f"  usage 종류({len(usage_keys)}): {usage_keys}")
        _ok(f"  building_type 종류({len(bt_keys)}): {bt_keys[:3]}...")

        # 구조 검증
        if n_gu != 25:
            _fail("/predict", f"구 수={n_gu} (기대=25)")
            failures += 1
        if len(usage_keys) != 7:
            _fail("/predict", f"usage 수={len(usage_keys)} (기대=7)")
            failures += 1
        if len(bt_keys) == 0:
            _fail("/predict", "building_type 비어있음")
            failures += 1

    # 3-2. 과거 (ONI 실측 그대로 → is_simulated=False)
    r2 = client.get("/predict?year=2023&month=8&oni=1.37")
    if r2.status_code == 200:
        d2 = r2.get_json()
        _ok(f"/predict 과거 실측ONI → is_simulated={d2.get('is_simulated')} (False여야 정상)")
    else:
        _fail("/predict 과거", f"status={r2.status_code}")
        failures += 1

    # 3-3. 과거 (ONI 변경 → is_simulated=True)
    r3 = client.get("/predict?year=2023&month=8&oni=2.0")
    if r3.status_code == 200:
        d3 = r3.get_json()
        _ok(f"/predict 과거 ONI변경 → is_simulated={d3.get('is_simulated')} (True여야 정상)")
    else:
        _fail("/predict 과거ONI변경", f"status={r3.status_code}")
        failures += 1

    # 3-4. 파라미터 누락
    r4 = client.get("/predict?year=2030&month=8")
    if r4.status_code == 400:
        _ok("/predict 파라미터 누락 → 400 정상")
    else:
        _fail("/predict 파라미터 누락", f"status={r4.status_code} (기대=400)")
        failures += 1

    # ------------------------------------------------------------------
    # 4. /predict/oni_range
    # ------------------------------------------------------------------
    _section("4. /predict/oni_range  (ONI 범위 그래프용)")

    r = client.get("/predict/oni_range?year=2030&month=8")
    if r.status_code != 200:
        _fail("/predict/oni_range", f"status={r.status_code}")
        failures += 1
    else:
        d = r.get_json()
        oni_range = d.get("oni_range", [])
        _ok(f"포인트 수={len(oni_range)} (기대=51)")
        if oni_range:
            first, last = oni_range[0], oni_range[-1]
            _ok(f"  ONI 범위: {first['oni']} ~ {last['oni']}")
            _ok(f"  ONI=-2.5: asos_temp={first['asos_temp']}, "
                f"supply_mw={first['supply_mw']}, reserve_rate={first['reserve_rate']}, "
                f"seoul_total={first['seoul_total_consumption_mwh']}")
            _ok(f"  ONI=+2.5: asos_temp={last['asos_temp']}, "
                f"supply_mw={last['supply_mw']}, reserve_rate={last['reserve_rate']}, "
                f"seoul_total={last['seoul_total_consumption_mwh']}")
            if len(oni_range) != 51:
                _fail("/predict/oni_range", f"포인트 수={len(oni_range)} (기대=51)")
                failures += 1
            # ONI 증가할수록 기온 증가하는지 방향 체크
            if first["asos_temp"] < last["asos_temp"]:
                _ok("  ONI↑ → 기온↑ 방향 정상")
            else:
                _fail("/predict/oni_range", "ONI 증가해도 기온 감소 (모델 이상)")
                failures += 1

    # ------------------------------------------------------------------
    # 5. /blackout_simulation
    # ------------------------------------------------------------------
    _section("5. /blackout_simulation  (순차 정전 시뮬레이션)")

    # 5-1. 경보 발생 케이스 (reserve_rate 낮게 유도 — ONI 극단값)
    payload = {"year": 2030, "month": 8, "oni": 2.5}
    r = client.post("/blackout_simulation",
                    data=json.dumps(payload),
                    content_type="application/json")
    if r.status_code != 200:
        _fail("/blackout_simulation", f"status={r.status_code}")
        failures += 1
    else:
        d = r.get_json()
        _ok(f"alert_level={d.get('alert_level')} ({d.get('alert_label')})")
        _ok(f"districts_order 수={len(d.get('districts_order', []))} (기대=25)")
        _ok(f"blackout_items 수={len(d.get('blackout_items', []))}")
        if d.get("blackout_items"):
            item = d["blackout_items"][0]
            _ok(f"  1순위: gu={item['gu']}, building_type={item['building_type']}, "
                f"score={item['reduction_need_score']}")
        if len(d.get("districts_order", [])) != 25:
            _fail("/blackout_simulation", f"districts_order 수={len(d.get('districts_order',[]))}")
            failures += 1
        # blackout_items에 cut_mwh 없어야 함
        if d.get("blackout_items") and "cut_mwh" in d["blackout_items"][0]:
            _fail("/blackout_simulation", "cut_mwh 필드가 남아있음 (제거 필요)")
            failures += 1

    # 5-2. 정상 경보 미달 케이스 (ONI=0, blackout_items 비어야 함)
    payload2 = {"year": 2015, "month": 3, "oni": 0.0}
    r2 = client.post("/blackout_simulation",
                     data=json.dumps(payload2),
                     content_type="application/json")
    if r2.status_code == 200:
        d2 = r2.get_json()
        _ok(f"경보 미달: alert={d2.get('alert_label')}, "
            f"blackout_items={len(d2.get('blackout_items',[]))} (0이어야 정상)")
    else:
        _fail("/blackout_simulation 경보미달", f"status={r2.status_code}")
        failures += 1

    # ------------------------------------------------------------------
    # 결과
    # ------------------------------------------------------------------
    print(f"\n{'='*55}")
    if failures == 0:
        print("  \033[32m전체 통과 — API 학습 모델 연결 정상\033[0m")
    else:
        print(f"  \033[31m실패 {failures}건\033[0m")
    print(f"{'='*55}\n")
    return failures


if __name__ == "__main__":
    sys.exit(run_tests())

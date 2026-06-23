"""
구별 월별 기온 피처 파일 생성.

출력: data/output/temperature_gu_monthly.csv
컬럼: gu, year, month, ta_mean, ta_min, ta_max, hm_mean, ws_mean, cdd, hdd, thi

구간별 기온 추정 방식:
  [2020~2025] S-DoT 실측 직접 사용

  [2005~2019, 2026~] Delta Method + 동적 multiplier
    ta(gu,y,m) = ASOS(y,m) + offset(gu,m) × multiplier(y,m)

    - offset(gu,m):      S-DoT 5년 평균 - ASOS 동기간 평균 (구의 구조적 열섬)
    - multiplier(y,m):   ws_clim(m) / ws_actual(y,m)
                         Oke(1973): 열섬 강도 ∝ 1/풍속
                         약풍 해 → multiplier↑ → 열섬 강화
                         강풍 해 → multiplier↓ → 열섬 약화

  → offset이 큰 구(성동구 등)는 multiplier 변동에 더 크게 반응
    → 매년 매월 구별 기온 차이가 실제로 달라짐
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
import pandas as pd

from python.loader.asos_daily import load_hourly, to_daily
from python.preprocess.gu_offset import (
    load_sdot_gu_daily,
    calc_monthly_offset,
    calc_dynamic_multiplier,
)

ROOT      = Path(__file__).resolve().parents[2]
COOL_BASE = 24.0
HEAT_BASE = 18.0
OUTPUT    = ROOT / "data" / "output" / "temperature_gu_monthly.csv"

SEOUL_GU = [
    "강남구","강동구","강북구","강서구","관악구","광진구","구로구","금천구",
    "노원구","도봉구","동대문구","동작구","마포구","서대문구","서초구","성동구",
    "성북구","송파구","양천구","영등포구","용산구","은평구","종로구","중구","중랑구",
]

SDOT_START = 2020


def _thi(ta: pd.Series, rh: pd.Series) -> pd.Series:
    return 1.8 * ta - 0.55 * (1 - rh / 100) * (1.8 * ta - 26) + 32


def build(start_year: int = 2005) -> pd.DataFrame:
    # ── 1. ASOS 로드 ─────────────────────────────────────────────────────────
    print("[1/6] ASOS 로드 중...")
    asos_daily = to_daily(load_hourly())
    asos_daily = asos_daily[asos_daily["date"].dt.year >= start_year].copy()
    asos_daily["year"]  = asos_daily["date"].dt.year
    asos_daily["month"] = asos_daily["date"].dt.month

    hm_clim = asos_daily.groupby("month")["hm_mean"].mean().to_dict()
    ws_clim = asos_daily.groupby("month")["ws_mean"].mean().to_dict()

    # ── 2. S-DoT 로드 및 파라미터 추정 ──────────────────────────────────────
    print("[2/6] S-DoT 로드 및 파라미터 추정 중...")
    sdot_daily = load_sdot_gu_daily()

    month_offset = calc_monthly_offset(sdot_daily, asos_daily)
    multiplier_df = calc_dynamic_multiplier(asos_daily)   # year, month, multiplier

    # offset 연간 평균 (S-DoT 없는 구 보완용)
    annual_offset = {}
    for (gu, _), v in month_offset.items():
        annual_offset.setdefault(gu, []).append(v)
    annual_offset = {gu: float(np.mean(vals)) for gu, vals in annual_offset.items()}

    mult_map = multiplier_df.set_index(["year", "month"])["multiplier"].to_dict()

    print("      월별 평년 multiplier (=1.0 기준):")
    for m in range(1, 13):
        clim_mult = ws_clim[m] / ws_clim[m]  # =1.0 (평년은 항상 1.0)
        print(f"        {m:2d}월 ws_clim={ws_clim[m]:.2f} m/s")
    print()
    print("      연도별 7월 multiplier 예시:")
    jul_mult = multiplier_df[multiplier_df["month"]==7].sort_values("year")
    for _, row in jul_mult.iterrows():
        print(f"        {int(row['year'])}년: {row['multiplier']:.4f}")

    # ── 3. [S-DoT 실측] 직접 집계 ────────────────────────────────────────────
    sdot_daily["year"]  = sdot_daily["date"].dt.year
    sdot_daily["month"] = sdot_daily["date"].dt.month
    sdot_avail = sdot_daily[sdot_daily["year"] >= SDOT_START]
    sdot_end   = int(sdot_avail["year"].max()) if not sdot_avail.empty else SDOT_START - 1
    print(f"\n[3/6] S-DoT 실측 집계 중 ({SDOT_START}~{sdot_end})...")

    sdot_monthly = sdot_avail.groupby(["gu","year","month"], as_index=False).agg(
        ta_mean=("ta_mean","mean"),
        ta_min =("ta_min", "min"),
        ta_max =("ta_max", "max"),
        hm_obs =("hm_mean","mean"),
    )
    sdot_cdd = sdot_avail.copy()
    sdot_cdd["cdd_d"] = (sdot_cdd["ta_mean"] - COOL_BASE).clip(lower=0)
    sdot_cdd["hdd_d"] = (HEAT_BASE - sdot_cdd["ta_mean"]).clip(lower=0)
    cdd_hdd = sdot_cdd.groupby(["gu","year","month"], as_index=False).agg(
        cdd=("cdd_d","sum"), hdd=("hdd_d","sum")
    )
    sdot_monthly = sdot_monthly.merge(cdd_hdd, on=["gu","year","month"])
    sdot_monthly["hm_mean"] = sdot_monthly["hm_obs"].combine_first(
        sdot_monthly["month"].map(hm_clim)
    ).round(2)
    sdot_monthly["ws_mean"] = sdot_monthly["month"].map(ws_clim).round(3)
    sdot_monthly["thi"]     = _thi(sdot_monthly["ta_mean"], sdot_monthly["hm_mean"])

    # ── 4. [Delta × 동적 multiplier] 추정 ────────────────────────────────────
    delta_mask = (asos_daily["year"] < SDOT_START) | (asos_daily["year"] > sdot_end)
    asos_hist  = asos_daily[delta_mask].copy()
    yr_range   = f"{asos_hist['year'].min()}~{asos_hist['year'].max()}"
    print(f"[4/6] Delta×multiplier 추정 중 ({yr_range})...")

    dates_df = asos_hist[["date","year","month","ta_mean","ta_min","ta_max"]].copy()
    gu_daily = dates_df.merge(pd.DataFrame({"gu": SEOUL_GU}), how="cross")

    # offset(gu, month)
    gu_daily["offset"] = [
        month_offset.get((gu, int(m)), annual_offset.get(gu, 0.0))
        for gu, m in zip(gu_daily["gu"], gu_daily["month"])
    ]

    # multiplier(year, month)
    gu_daily["multiplier"] = [
        mult_map.get((int(y), int(m)), 1.0)
        for y, m in zip(gu_daily["year"], gu_daily["month"])
    ]

    adj = gu_daily["offset"] * gu_daily["multiplier"]
    gu_daily["ta_mean_gu"] = gu_daily["ta_mean"] + adj
    gu_daily["ta_min_gu"]  = gu_daily["ta_min"]  + adj
    gu_daily["ta_max_gu"]  = gu_daily["ta_max"]  + adj

    gu_daily["cdd_d"] = (gu_daily["ta_mean_gu"] - COOL_BASE).clip(lower=0)
    gu_daily["hdd_d"] = (HEAT_BASE - gu_daily["ta_mean_gu"]).clip(lower=0)

    hist_monthly = gu_daily.groupby(["gu","year","month"], as_index=False).agg(
        ta_mean=("ta_mean_gu","mean"),
        ta_min =("ta_min_gu", "min"),
        ta_max =("ta_max_gu", "max"),
        cdd    =("cdd_d",     "sum"),
        hdd    =("hdd_d",     "sum"),
    )
    hist_monthly["hm_mean"] = hist_monthly["month"].map(hm_clim).round(2)
    hist_monthly["ws_mean"] = hist_monthly["month"].map(ws_clim).round(3)
    hist_monthly["thi"]     = _thi(hist_monthly["ta_mean"], hist_monthly["hm_mean"])

    # ── 5. 합치기 & 저장 ──────────────────────────────────────────────────────
    print("[5/6] 구간 합치기...")
    cols = ["gu","year","month","ta_mean","ta_min","ta_max","hm_mean","ws_mean","cdd","hdd","thi"]
    result = pd.concat(
        [hist_monthly[cols], sdot_monthly[cols]], ignore_index=True
    ).sort_values(["gu","year","month"]).reset_index(drop=True)

    for col in ["ta_mean","ta_min","ta_max","thi"]:
        result[col] = result[col].round(3)
    for col in ["cdd","hdd"]:
        result[col] = result[col].round(2)

    print("[6/6] 저장 중...")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    result.to_csv(OUTPUT, index=False, encoding="utf-8-sig")

    print()
    print("=" * 60)
    print(f"  출력: {OUTPUT.name}")
    print(f"  shape: {result.shape}  |  {result['year'].min()}~{result['year'].max()}  |  {result['gu'].nunique()}개 구")
    print(f"  [{SDOT_START}~{sdot_end}] S-DoT 실측  |  [{start_year}~{SDOT_START-1}, {sdot_end+1}~] Delta×multiplier")
    print("=" * 60)

    return result


if __name__ == "__main__":
    df = build(start_year=2005)

    print()
    print("=== 검증: 성동구 vs 강북구 7월 기온 차이 ===")
    jul = df[df["month"]==7].pivot(index="year", columns="gu", values="ta_mean")
    diff = (jul["성동구"] - jul["강북구"]).round(3)
    print(diff.to_string())
    print(f"\n  std: {diff.std():.4f}℃  (>0이면 연도별 변동)")

    print()
    print("=== 7월 구별 기온 상위/하위 5구 ===")
    for yr in [2013, 2018, 2020, 2023]:
        if yr not in jul.index:
            continue
        row = jul.loc[yr].round(2).sort_values(ascending=False)
        top = ", ".join(f"{g} {v}" for g, v in row.head().items())
        bot = ", ".join(f"{g} {v}" for g, v in row.tail().items())
        print(f"  {yr}년  상위: {top}")
        print(f"        하위: {bot}")

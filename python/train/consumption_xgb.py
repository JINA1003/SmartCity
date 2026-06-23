"""
구별·용도별 전력 소비량 예측 모델 (XGBoost).

피처: year, month, oni, cdd_gu, hdd_gu, district(label), usage_type(label)
  - cdd_gu / hdd_gu : 구별 예측 기온(ta_gu)에서 파생
      ta_gu = ASOS_pred + offset(gu, month) × multiplier
      cdd_gu = max(0, ta_gu - 24) × days_in_month
      hdd_gu = max(0, 18 - ta_gu) × days_in_month
  - oni 직접 포함 → 같은 year라도 ONI 슬라이더 변동 반영
  - year 포함 → 2015 ONI=1 ≠ 2030 ONI=1 (시계열 추세)

학습 데이터:
  data/output/kepco_final_20052026.csv      → consumption (district, usage_type)
  data/output/temperature_gu_monthly.csv    → 구별 월별 ta_mean (CDD/HDD 계산 기반)
  data/file/oni.csv                         → oni

[의존]
  temperature_gu_monthly.csv 에 ta_mean 컬럼 필요
"""

from __future__ import annotations

import calendar
import pickle
from pathlib import Path
from typing import Any

import numpy as np
import pandas as pd
from sklearn.preprocessing import LabelEncoder

MODEL_PATH    = Path(__file__).resolve().parents[1] / "model" / "artifacts" / "consumption_xgb.pkl"
ENCODERS_PATH = Path(__file__).resolve().parents[1] / "model" / "artifacts" / "consumption_encoders.pkl"

ROOT        = Path(__file__).resolve().parents[2]
TEMP_GU_CSV = ROOT / "data" / "output" / "temperature_gu_monthly.csv"

COOL_BASE = 24.0
HEAT_BASE = 18.0

FEATURE_COLS = ["year", "month", "oni", "cdd_gu", "hdd_gu", "district_id", "usage_id"]


def _cdd_hdd_from_ta(ta: float, year: int, month: int) -> tuple[float, float]:
    """월평균기온 → 월간 CDD/HDD (근사식)."""
    days = calendar.monthrange(int(year), int(month))[1]
    cdd = max(0.0, ta - COOL_BASE) * days
    hdd = max(0.0, HEAT_BASE - ta) * days
    return cdd, hdd


def _load_temp_gu() -> pd.DataFrame:
    """temperature_gu_monthly.csv 로드. district 컬럼명을 district로 정규화."""
    df = pd.read_csv(TEMP_GU_CSV, encoding="utf-8-sig")
    if "gu" in df.columns and "district" not in df.columns:
        df = df.rename(columns={"gu": "district"})
    return df[["district", "year", "month", "ta_mean"]]


def _encode_categoricals(
    df: pd.DataFrame,
    district_enc: LabelEncoder | None = None,
    usage_enc: LabelEncoder | None = None,
    fit: bool = False,
) -> tuple[pd.DataFrame, LabelEncoder, LabelEncoder]:
    df = df.copy()
    if fit or district_enc is None:
        district_enc = LabelEncoder()
        df["district_id"] = district_enc.fit_transform(df["district"].astype(str))
    else:
        df["district_id"] = district_enc.transform(df["district"].astype(str))

    if fit or usage_enc is None:
        usage_enc = LabelEncoder()
        df["usage_id"] = usage_enc.fit_transform(df["usage_type"].astype(str))
    else:
        df["usage_id"] = usage_enc.transform(df["usage_type"].astype(str))

    return df, district_enc, usage_enc


def train(oni: pd.DataFrame) -> Any:
    """
    oni: year, month, oni  (loader.oni_loader.load_oni() 결과)

    kepco_final_20052026.csv, temperature_gu_monthly.csv 는 내부에서 직접 로드.
    """
    import xgboost as xgb
    from python.loader.kepco_loader import load_consumption

    consumption = load_consumption()
    temp_gu     = _load_temp_gu()

    df = (
        consumption
        .merge(temp_gu, on=["district", "year", "month"], how="left")
        .merge(oni, on=["year", "month"], how="left")
        .dropna(subset=["consumption_mwh", "ta_mean", "oni"])
    )

    # 구별 CDD/HDD
    df["cdd_gu"] = df.apply(
        lambda r: max(0.0, r["ta_mean"] - COOL_BASE) * calendar.monthrange(int(r["year"]), int(r["month"]))[1],
        axis=1,
    )
    df["hdd_gu"] = df.apply(
        lambda r: max(0.0, HEAT_BASE - r["ta_mean"]) * calendar.monthrange(int(r["year"]), int(r["month"]))[1],
        axis=1,
    )

    df, d_enc, u_enc = _encode_categoricals(df, fit=True)

    X = df[FEATURE_COLS].values
    y = df["consumption_mwh"].values

    model = xgb.XGBRegressor(
        n_estimators=500,
        max_depth=6,
        learning_rate=0.05,
        subsample=0.8,
        colsample_bytree=0.8,
        random_state=42,
    )
    model.fit(X, y)

    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    with open(MODEL_PATH, "wb") as f:
        pickle.dump(model, f)
    with open(ENCODERS_PATH, "wb") as f:
        pickle.dump({"district": d_enc, "usage": u_enc}, f)

    return model


def load_model() -> tuple[Any, dict]:
    with open(MODEL_PATH, "rb") as f:
        model = pickle.load(f)
    with open(ENCODERS_PATH, "rb") as f:
        encoders = pickle.load(f)
    return model, encoders


def predict(
    district: str,
    usage_type: str,
    year: int,
    month: int,
    oni: float,
    ta_gu: float,
    model: Any | None = None,
    encoders: dict | None = None,
) -> float:
    """
    단일 (구, 용도, year, month, oni, ta_gu) → 소비량(MWh) 예측.

    ta_gu: 해당 구의 예측 월평균기온 (gu_offset.predict_gu_temp() 결과)
    """
    if model is None or encoders is None:
        model, encoders = load_model()

    cdd_gu, hdd_gu = _cdd_hdd_from_ta(ta_gu, year, month)

    row = pd.DataFrame([{
        "district": district,
        "usage_type": usage_type,
        "year": year,
        "month": month,
        "oni": oni,
        "cdd_gu": cdd_gu,
        "hdd_gu": hdd_gu,
    }])
    row, _, _ = _encode_categoricals(
        row,
        district_enc=encoders["district"],
        usage_enc=encoders["usage"],
        fit=False,
    )
    return float(model.predict(row[FEATURE_COLS].values)[0])


def predict_all_districts(
    year: int,
    month: int,
    oni: float,
    gu_temps: dict[str, float],
    usage_types: list[str],
    model: Any | None = None,
    encoders: dict | None = None,
) -> pd.DataFrame:
    """
    전체 구 × 용도 소비량을 한번에 예측.

    gu_temps: {"강남구": 31.2, "강북구": 29.8, ...}  (predict_gu_temp 결과)
    반환: district, usage_type, ta_gu, cdd_gu, hdd_gu, consumption_mwh
    """
    if model is None or encoders is None:
        model, encoders = load_model()

    rows = []
    for district, ta_gu in gu_temps.items():
        cdd_gu, hdd_gu = _cdd_hdd_from_ta(ta_gu, year, month)
        for usage in usage_types:
            rows.append({
                "district": district,
                "usage_type": usage,
                "year": year,
                "month": month,
                "oni": oni,
                "cdd_gu": cdd_gu,
                "hdd_gu": hdd_gu,
                "ta_gu": ta_gu,
            })

    df = pd.DataFrame(rows)
    df, _, _ = _encode_categoricals(
        df,
        district_enc=encoders["district"],
        usage_enc=encoders["usage"],
        fit=False,
    )
    df["consumption_mwh"] = model.predict(df[FEATURE_COLS].values)
    return df[["district", "usage_type", "ta_gu", "cdd_gu", "hdd_gu", "consumption_mwh"]]

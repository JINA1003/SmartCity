"""
전국 전력 공급량/예비율 회귀.

한국 전력계통은 전국 통합망 → 구별 분리 불필요.

피처: year, month, oni, cdd, hdd
  - cdd/hdd 는 API 호출 시 ASOS 예측 기온(temp_trend)에서 파생
  - year 피처로 설비증가 추세 반영 → 2015 ONI=1 ≠ 2030 ONI=1
  - oni 직접 포함 → 같은 연도라도 ONI 슬라이더 변동 즉시 반영

타겟: supply_mw_mean, reserve_rate_mean

[의존]
  preprocess.supply_loader.load_monthly()
  model.cdd_hdd.monthly_cdd_from_daily()
  preprocess.oni_loader.load_oni()
"""

from __future__ import annotations

import pickle
from pathlib import Path
from typing import Any

import pandas as pd
from sklearn.linear_model import LinearRegression
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import PolynomialFeatures

MODEL_PATH = Path(__file__).parent / "artifacts" / "supply_model.pkl"

FEATURE_COLS = ["year", "month", "oni", "cdd", "hdd"]


def train(
    cdd_hdd: pd.DataFrame,
    oni: pd.DataFrame,
) -> Any:
    """
    cdd_hdd: year, month, cdd, hdd  (ASOS 실측 기반, train.cdd_hdd.monthly_cdd_from_daily())
    oni    : year, month, oni

    epsis_supply_rate_final_20052026.csv 는 내부에서 직접 로드.
    """
    from python.loader.supply_loader import load_monthly as load_supply
    supply_monthly = load_supply()

    df = (
        supply_monthly
        .merge(cdd_hdd, on=["year", "month"], how="inner")
        .merge(oni, on=["year", "month"], how="inner")
        .dropna(subset=["supply_mw_mean", "reserve_rate_mean", "cdd", "hdd", "oni"])
    )

    X = df[FEATURE_COLS].values
    y = df[["supply_mw_mean", "reserve_rate_mean"]].values

    model = Pipeline([
        ("poly", PolynomialFeatures(degree=2, include_bias=False)),
        ("reg", LinearRegression()),
    ])
    model.fit(X, y)

    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    with open(MODEL_PATH, "wb") as f:
        pickle.dump(model, f)

    return model


def load_model() -> Any:
    with open(MODEL_PATH, "rb") as f:
        return pickle.load(f)


def predict(
    year: int,
    month: int,
    oni: float,
    cdd: float,
    hdd: float,
    model: Any | None = None,
) -> dict[str, float]:
    """
    반환: {"supply_mw": float, "reserve_rate": float}

    cdd/hdd 는 호출 전에 temp_trend 예측 기온에서 계산해서 넘길 것.
    (flask_app._get_cdd_hdd 참고)
    """
    if model is None:
        model = load_model()
    X = pd.DataFrame([{
        "year": year,
        "month": month,
        "oni": oni,
        "cdd": cdd,
        "hdd": hdd,
    }])
    pred = model.predict(X[FEATURE_COLS].values)[0]
    return {"supply_mw": float(pred[0]), "reserve_rate": float(pred[1])}

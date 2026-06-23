"""
기준기온 회귀 (ASOS 지점 108 기반).

모델:
  ta_mean(year, month, ONI) = β0 + β1·ONI + β2·year + Σ γ_m·month_dummy

학습: 2006~현재 ASOS 월별 실측 + ONI 월별 수치
예측: year·ONI·month 를 넣으면 임의 연도(2030 포함) 기온 추정치 반환

[의존]
  preprocess.asos_daily.load_monthly()
  preprocess.oni_loader.load_oni()
"""

from __future__ import annotations

import pickle
from pathlib import Path
from typing import Any

import numpy as np
import pandas as pd
from sklearn.linear_model import LinearRegression
from sklearn.preprocessing import OneHotEncoder
from sklearn.pipeline import Pipeline
from sklearn.compose import ColumnTransformer

MODEL_PATH = Path(__file__).resolve().parents[1] / "model" / "artifacts" / "temp_trend_model.pkl"


def build_features(df: pd.DataFrame) -> pd.DataFrame:
    """year, month, oni → 학습/추론용 feature DataFrame."""
    return df[["year", "month", "oni"]].copy()


def train(monthly_asos: pd.DataFrame, oni: pd.DataFrame) -> Any:
    """
    monthly_asos: year, month, ta_mean (preprocess.asos_daily.load_monthly())
    oni         : year, month, oni     (preprocess.oni_loader.load_oni())

    반환: 학습된 Pipeline (저장 포함)
    """
    df = monthly_asos.merge(oni, on=["year", "month"], how="inner").dropna(
        subset=["ta_mean", "oni"]
    )

    X = build_features(df)
    y = df["ta_mean"].values

    month_enc = ColumnTransformer(
        [("month_ohe", OneHotEncoder(drop="first", sparse_output=False), ["month"])],
        remainder="passthrough",
    )
    model = Pipeline([("enc", month_enc), ("reg", LinearRegression())])
    model.fit(X, y)

    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    with open(MODEL_PATH, "wb") as f:
        pickle.dump(model, f)

    return model


def load_model() -> Any:
    with open(MODEL_PATH, "rb") as f:
        return pickle.load(f)


def predict(year: int, month: int, oni: float, model: Any | None = None) -> float:
    """단일 (year, month, ONI) 조합의 월평균기온 추정치 반환."""
    if model is None:
        model = load_model()
    X = pd.DataFrame({"year": [year], "month": [month], "oni": [oni]})
    return float(model.predict(X)[0])


def predict_batch(df: pd.DataFrame, model: Any | None = None) -> pd.Series:
    """year, month, oni 컬럼을 가진 DataFrame → ta_mean 추정 Series."""
    if model is None:
        model = load_model()
    return pd.Series(model.predict(build_features(df)), index=df.index, name="ta_mean_pred")


if __name__ == "__main__":
    from python.loader.asos_daily import load_monthly as asos_monthly
    from python.loader.oni_loader import load_oni

    m = train(asos_monthly(), load_oni())
    print("2030년 8월 ONI=1.0:", predict(2030, 8, 1.0, m))
    print("2020년 8월 ONI=1.0:", predict(2020, 8, 1.0, m))

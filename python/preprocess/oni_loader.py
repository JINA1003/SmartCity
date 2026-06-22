"""
ONI 월별 수치 로드.

입력: data/file/oni.csv
      컬럼 — Date(YYYY-MM-DD), ONI값(float)

출력: year, month, oni 컬럼 DataFrame
"""

from __future__ import annotations

from pathlib import Path

import pandas as pd

ONI_CSV = (
    Path(__file__).resolve().parents[2]
    / "data" / "file" / "oni.csv"
)


def load_oni() -> pd.DataFrame:
    df = pd.read_csv(ONI_CSV, header=0, skipinitialspace=True)
    df.columns = ["date", "oni"]
    df["date"] = pd.to_datetime(df["date"], errors="coerce")
    df = df.dropna(subset=["date"])
    df["oni"] = pd.to_numeric(df["oni"], errors="coerce")
    # 유효 범위 [-2.5, 2.5] 밖은 결측 처리
    df.loc[(df["oni"] < -2.5) | (df["oni"] > 2.5), "oni"] = pd.NA
    df = df.dropna(subset=["oni"])
    df["year"] = df["date"].dt.year
    df["month"] = df["date"].dt.month
    return df[["year", "month", "oni"]].reset_index(drop=True)


if __name__ == "__main__":
    print(load_oni().tail())

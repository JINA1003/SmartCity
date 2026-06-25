"""
ASOS 시간자료 → 일별/월별 집계.

입력: data/file/asos_weather_data/
      asos_108_YYYYMMDDHHH_YYYYMMDDHHH.csv 형태의 연도별 파일 자동 합산
      컬럼 — tm(YYYY-MM-DD HH), ta(기온℃), ws(풍속m/s), hm(습도%)

출력:
  daily  — date, ta_mean, ta_max, ta_min, ws_mean, hm_mean
  monthly — year, month, ta_mean, ta_max, ta_min, ws_mean, hm_mean
"""

from __future__ import annotations

from pathlib import Path

import pandas as pd

ASOS_DIR = (
    Path(__file__).resolve().parents[2]
    / "data" / "file" / "asos_weather_data"
)


def load_hourly() -> pd.DataFrame:
    """연도별 CSV 파일을 모두 합쳐 시간별 DataFrame 반환."""
    files = sorted(ASOS_DIR.glob("asos_108_*.csv"))
    if not files:
        raise FileNotFoundError(f"ASOS CSV 없음: {ASOS_DIR}")

    frames = []
    for f in files:
        try:
            chunk = pd.read_csv(f, encoding="utf-8-sig")
            frames.append(chunk)
        except Exception:
            continue

    df = pd.concat(frames, ignore_index=True)
    df["tm"] = pd.to_datetime(df["tm"], format="%Y-%m-%d %H", errors="coerce")
    df = df.dropna(subset=["tm"]).sort_values("tm").drop_duplicates("tm").reset_index(drop=True)
    for col in ("ta", "ws", "hm"):
        df[col] = pd.to_numeric(df[col], errors="coerce")
    return df


def to_daily(df: pd.DataFrame) -> pd.DataFrame:
    df = df.copy()
    df["date"] = df["tm"].dt.normalize()
    daily = df.groupby("date").agg(
        ta_mean=("ta", "mean"),
        ta_max=("ta", "max"),
        ta_min=("ta", "min"),
        ws_mean=("ws", "mean"),
        hm_mean=("hm", "mean"),
    ).reset_index()
    return daily


def to_monthly(daily: pd.DataFrame) -> pd.DataFrame:
    df = daily.copy()
    df["year"] = df["date"].dt.year
    df["month"] = df["date"].dt.month
    monthly = df.groupby(["year", "month"]).agg(
        ta_mean=("ta_mean", "mean"),
        ta_max=("ta_max", "max"),
        ta_min=("ta_min", "min"),
        ws_mean=("ws_mean", "mean"),
        hm_mean=("hm_mean", "mean"),
    ).reset_index()
    return monthly


def load_monthly() -> pd.DataFrame:
    return to_monthly(to_daily(load_hourly()))


if __name__ == "__main__":
    monthly = load_monthly()
    print(monthly.tail())

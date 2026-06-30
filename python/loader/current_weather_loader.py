import os
import requests
import pandas as pd
from io import StringIO

KMA_API_KEY = "2CXA78d-TKelwO_HfoynZg"

KMA_URL = "https://apihub.kma.go.kr/api/typ01/url/kma_sfctm2.php"

COLUMNS = [
    "TM", "STN", "WD", "WS", "GST_WD", "GST_WS", "GST_TM",
    "PA", "PS", "PT", "PR", "TA", "TD", "HM", "PV",
    "RN", "RN_DAY", "RN_JUN", "RN_INT", "SD_HR3",
    "SD_DAY", "SD_TOT", "WC", "WP", "WW", "CA_TOT",
    "CA_MID", "CH_MIN", "CT", "CT_TOP", "CT_MID",
    "CT_LOW", "VS", "SS", "SI", "ST_GD", "TS",
    "TE_005", "TE_01", "TE_02", "TE_03",
    "ST_SEA", "WH", "BF", "IR", "IX"
]


def load_current_weather():
    params = {
        "stn": 108,
        "help": 1,
        "authKey": KMA_API_KEY
    }

    response = requests.get(KMA_URL, params=params, timeout=20)
    response.raise_for_status()

    data_lines = []

    for line in response.text.splitlines():
        line = line.strip()

        if not line or line.startswith("#"):
            continue

        data_lines.append(line)

    if not data_lines:
        raise ValueError("기상청 응답에서 실제 데이터 행을 찾지 못했습니다.")

    df = pd.read_csv(
        StringIO("\n".join(data_lines)),
        sep=r"\s+",
        names=COLUMNS
    )

    df = df.replace([-9, -9.0, -99, -99.0], pd.NA)

    needs_columns = ["TM", "STN", "TA", "HM", "RN", "WS"]

    df_needs = df[needs_columns].rename(columns={
        "TM": "time",
        "STN": "station",
        "TA": "temperature",
        "HM": "humidity",
        "RN": "rainfall",
        "WS": "wind_speed"
    })

    row = df_needs.iloc[0]

    return {
        "time": str(row["time"]),
        "station": int(row["station"]),
        "station_name": "서울",
        "temperature": None if pd.isna(row["temperature"]) else float(row["temperature"]),
        "humidity": None if pd.isna(row["humidity"]) else float(row["humidity"]),
        "rainfall": "NONE" if pd.isna(row["rainfall"]) else float(row["rainfall"]),
        "wind_speed": None if pd.isna(row["wind_speed"]) else float(row["wind_speed"]),
    }
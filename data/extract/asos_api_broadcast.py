"""
ASOS 시간자료 API 수집 (풍속, 기온, 습도).

API: https://apis.data.go.kr/1360000/AsosHourlyInfoService/getWthrDataList
기간: 2006-01 ~ 2026-05 (시간별)
지점: 108 (서울 종관기상관측, 고정)

저장: data/file/asos_weather_data/asos_108_hourly.csv
진행 상태: data/extract/asos_api_state.json
"""

from __future__ import annotations

import json
import logging
import os
import time
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

import pandas as pd

# ---------------------------------------------------------------------------
# 환경 변수 (.env)
# ---------------------------------------------------------------------------

ENV_FILE = Path(__file__).resolve().parents[2] / ".env"


def _load_env() -> None:
    if not ENV_FILE.exists():
        return
    for line in ENV_FILE.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        os.environ.setdefault(key.strip(), value.strip().strip('"').strip("'"))


_load_env()
SERVICE_KEY = os.environ.get("OPEN_API")
if not SERVICE_KEY:
    raise RuntimeError(f"OPEN_API 키가 없습니다. {ENV_FILE} 파일을 확인하세요.")

# ---------------------------------------------------------------------------
# 수집 대상
# ---------------------------------------------------------------------------

API_BASE = "https://apis.data.go.kr/1360000/AsosHourlyInfoService/getWthrDataList"

STN_ID = "108"  # 서울 종관기상관측 (변경하지 않음)
START_DT = datetime(2006, 1, 1, 0)
END_DT = datetime(2026, 5, 31, 23)

# API 응답에서 추출·저장할 컬럼
# tm     : 시간 (일시, YYYY-MM-DD HH)
# ta     : 기온 (°C)
# ws     : 풍속 (m/s)
# hm     : 습도 (%)
COLUMNS = ["tm", "ta", "ws", "hm"]
CHUNK_MONTHS = 12
NUM_OF_ROWS = 999  # API numOfRows 상한 999 (1000이면 오류 99)
REQUEST_INTERVAL_SEC = 0.3
MAX_RETRIES = 3
REQUEST_TIMEOUT_SEC = 60

# ---------------------------------------------------------------------------
# 경로
# ---------------------------------------------------------------------------

DOWNLOAD_DIR = Path(__file__).parent.parent / "file" / "asos_weather_data"
STATE_FILE = Path(__file__).parent / "asos_api_state.json"

# ---------------------------------------------------------------------------
# 로깅
# ---------------------------------------------------------------------------

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[logging.StreamHandler()],
)
log = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# 진행 상태
# ---------------------------------------------------------------------------

def chunk_id(start: datetime, end: datetime) -> str:
    return f"{STN_ID}_{start:%Y%m%d%H}_{end:%Y%m%d%H}"


def chunk_path(start: datetime, end: datetime) -> Path:
    return DOWNLOAD_DIR / f"asos_{chunk_id(start, end)}.csv"


def output_path() -> Path:
    return DOWNLOAD_DIR / f"asos_{STN_ID}_hourly.csv"


def load_state() -> dict:
    if STATE_FILE.exists():
        with STATE_FILE.open(encoding="utf-8") as f:
            return json.load(f)
    return {"completed_chunks": [], "failed_chunks": []}


def save_state(state: dict) -> None:
    STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
    with STATE_FILE.open("w", encoding="utf-8") as f:
        json.dump(state, f, ensure_ascii=False, indent=2)


def sync_state_with_files(state: dict) -> dict:
    completed = {item["id"]: item for item in state.get("completed_chunks", [])}
    for path in DOWNLOAD_DIR.glob(f"asos_{STN_ID}_*.csv"):
        if path.name == output_path().name:
            continue
        stem = path.stem.removeprefix("asos_")
        completed[stem] = {"id": stem, "file": path.name}
    state["completed_chunks"] = list(completed.values())
    completed_ids = set(completed)
    state["failed_chunks"] = [
        item for item in state.get("failed_chunks", [])
        if item["id"] not in completed_ids
    ]
    return state


# ---------------------------------------------------------------------------
# 기간 분할 (12개월 단위 + numOfRows 999 페이지네이션)
# ---------------------------------------------------------------------------

def add_months(dt: datetime, months: int) -> datetime:
    month_index = dt.month - 1 + months
    year = dt.year + month_index // 12
    month = month_index % 12 + 1
    return datetime(year, month, 1, dt.hour)


def iter_chunks(
    start: datetime,
    end: datetime,
    months: int = CHUNK_MONTHS,
) -> list[tuple[datetime, datetime]]:
    chunks: list[tuple[datetime, datetime]] = []
    cur = start
    while cur <= end:
        chunk_end = add_months(cur.replace(day=1, hour=0), months) - timedelta(hours=1)
        if chunk_end > end:
            chunk_end = end
        chunks.append((cur, chunk_end))
        cur = chunk_end + timedelta(hours=1)
    return chunks


# ---------------------------------------------------------------------------
# API 호출
# ---------------------------------------------------------------------------

def _build_url(
    start: datetime,
    end: datetime,
    page_no: int,
) -> str:
    params = {
        "serviceKey": SERVICE_KEY,
        "numOfRows": NUM_OF_ROWS,
        "pageNo": page_no,
        "dataType": "JSON",
        "dataCd": "ASOS",
        "dateCd": "HR",
        "startDt": start.strftime("%Y%m%d"),
        "startHh": start.strftime("%H"),
        "endDt": end.strftime("%Y%m%d"),
        "endHh": end.strftime("%H"),
        "stnIds": STN_ID,
    }
    return f"{API_BASE}?{urlencode(params)}"


def _request_json(url: str) -> dict[str, Any]:
    req = Request(url, headers={"User-Agent": "SmartCity-ASOS-Extractor/1.0"})
    with urlopen(req, timeout=REQUEST_TIMEOUT_SEC) as resp:
        return json.loads(resp.read().decode("utf-8"))


def _normalize_items(raw_items: Any) -> list[dict[str, Any]]:
    if not raw_items:
        return []
    if isinstance(raw_items, list):
        return raw_items
    return [raw_items]


def fetch_chunk(
    start: datetime,
    end: datetime,
) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    page_no = 1
    total_count: int | None = None

    while True:
        url = _build_url(start, end, page_no)
        last_error: Exception | None = None

        for attempt in range(1, MAX_RETRIES + 1):
            try:
                payload = _request_json(url)
                last_error = None
                break
            except (HTTPError, URLError, TimeoutError, json.JSONDecodeError) as exc:
                last_error = exc
                log.warning(
                    "  API 실패 (시도 %d/%d): %s — %s",
                    attempt,
                    MAX_RETRIES,
                    chunk_id(start, end),
                    exc,
                )
                time.sleep(REQUEST_INTERVAL_SEC * attempt * 5)

        if last_error is not None:
            raise RuntimeError(f"API 요청 실패: {last_error}") from last_error

        header = payload.get("response", {}).get("header", {})
        result_code = header.get("resultCode")
        result_msg = header.get("resultMsg", "")
        if result_code != "00":
            raise RuntimeError(f"API 오류 [{result_code}] {result_msg}")

        body = payload.get("response", {}).get("body", {})
        if total_count is None:
            total_count = int(body.get("totalCount", 0))

        page_rows = _normalize_items(body.get("items", {}).get("item"))
        rows.extend(page_rows)

        if total_count == 0 or len(rows) >= total_count:
            break

        page_no += 1
        time.sleep(REQUEST_INTERVAL_SEC)

    return rows


def rows_to_dataframe(rows: list[dict[str, Any]]) -> pd.DataFrame:
    if not rows:
        return pd.DataFrame(columns=COLUMNS)

    df = pd.DataFrame(rows)
    for col in COLUMNS:
        if col not in df.columns:
            df[col] = pd.NA

    df = df[COLUMNS].copy()
    # API tm 형식이 "2008-01-01 00" / "2008-01-01 00:00" 등으로 달라질 수 있음
    parsed_tm = pd.to_datetime(df["tm"], errors="coerce")
    if parsed_tm.isna().all():
        raise RuntimeError("tm(시간) 파싱 실패 — API 응답 형식을 확인하세요.")
    df["tm"] = parsed_tm.dt.strftime("%Y-%m-%d %H")
    for col in ("ta", "ws", "hm"):
        df[col] = pd.to_numeric(df[col], errors="coerce")
    return df.sort_values("tm").reset_index(drop=True)


# ---------------------------------------------------------------------------
# 수집 실행
# ---------------------------------------------------------------------------

def download_chunk(
    start: datetime,
    end: datetime,
) -> Path:
    dest = chunk_path(start, end)
    if dest.exists():
        log.info("  건너뜀 (이미 존재): %s", dest.name)
        return dest

    log.info("  조회: %s ~ %s", start.strftime("%Y-%m-%d %H"), end.strftime("%Y-%m-%d %H"))
    rows = fetch_chunk(start, end)
    df = rows_to_dataframe(rows)

    DOWNLOAD_DIR.mkdir(parents=True, exist_ok=True)
    df.to_csv(dest, index=False, encoding="utf-8-sig")
    log.info("  저장: %s (%d행)", dest.name, len(df))
    time.sleep(REQUEST_INTERVAL_SEC)
    return dest


def merge_chunks() -> Path:
    chunk_files = sorted(
        p for p in DOWNLOAD_DIR.glob(f"asos_{STN_ID}_*.csv")
        if p.name != output_path().name
    )
    if not chunk_files:
        raise RuntimeError("병합할 청크 파일이 없습니다.")

    frames = [pd.read_csv(path, encoding="utf-8-sig") for path in chunk_files]
    merged = pd.concat(frames, ignore_index=True)
    merged["tm"] = pd.to_datetime(merged["tm"], errors="coerce")
    merged = merged.dropna(subset=["tm"]).drop_duplicates(subset=["tm"]).sort_values("tm")
    merged["tm"] = merged["tm"].dt.strftime("%Y-%m-%d %H")
    merged = merged.reset_index(drop=True)

    dest = output_path()
    merged.to_csv(dest, index=False, encoding="utf-8-sig")
    log.info("병합 완료: %s (%d행)", dest, len(merged))
    return dest


def run(
    start: datetime = START_DT,
    end: datetime = END_DT,
) -> Path:
    DOWNLOAD_DIR.mkdir(parents=True, exist_ok=True)
    state = sync_state_with_files(load_state())
    completed_ids = {item["id"] for item in state.get("completed_chunks", [])}

    chunks = iter_chunks(start, end)
    queue = [
        (s, e) for s, e in chunks
        if chunk_id(s, e) not in completed_ids
        and not chunk_path(s, e).exists()
    ]
    skipped = len(chunks) - len(queue)

    log.info(
        "ASOS 수집 — 지점 %s | 기간 %s ~ %s | 청크 %d개 (스킵 %d, 실행 %d)",
        STN_ID,
        start.strftime("%Y-%m-%d"),
        end.strftime("%Y-%m-%d"),
        len(chunks),
        skipped,
        len(queue),
    )

    for i, (chunk_start, chunk_end) in enumerate(queue, 1):
        cid = chunk_id(chunk_start, chunk_end)
        log.info("[%d/%d] %s", i, len(queue), cid)
        try:
            download_chunk(chunk_start, chunk_end)
            state["completed_chunks"] = [
                item for item in state.get("completed_chunks", [])
                if item["id"] != cid
            ]
            state["completed_chunks"].append({"id": cid})
            state["failed_chunks"] = [
                item for item in state.get("failed_chunks", [])
                if item["id"] != cid
            ]
            save_state(state)
        except Exception as exc:
            log.error("  청크 실패: %s — %s", cid, exc)
            state["failed_chunks"] = [
                item for item in state.get("failed_chunks", [])
                if item["id"] != cid
            ]
            state["failed_chunks"].append({"id": cid, "error": str(exc)})
            save_state(state)

    return merge_chunks()


if __name__ == "__main__":
    run()

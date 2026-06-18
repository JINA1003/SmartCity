"""
건축HUB 건물에너지(전기에너지) 데이터 크롤러.

흐름:
  1. 페이지 접속 (REPORT_VIEW iframe)
  2. 시도 → 서울특별시
  3. 시군구 → 각 구
  4. 연도 입력 (input.Calendar_textbox, value가 4자리 연도인 것)
  5. 맞춤형 항목조회 → 원하는 컬럼만 체크
  6. 검색 (btn_검색.png 래퍼 div 클릭)
  7. JSON 다운로드 (btn_JSON.png 래퍼 div 클릭)
  8. 사용목적 모달 → 확인

저장: data/file/building_energy_data/{year}_{구}.json
진행 상태: data/extract/crawling_state.json (completed / failed 목록)
"""

import asyncio
import json
import logging
from datetime import datetime, timezone
from pathlib import Path

from playwright.async_api import Page, Frame, TimeoutError as PlaywrightTimeoutError
from playwright.async_api import async_playwright

# ---------------------------------------------------------------------------
# 수집 대상
# ---------------------------------------------------------------------------

TARGET_CITY = "서울특별시"
TARGET_DISTRICTS = [
    "종로구", "중구", "용산구", "성동구", "광진구", "동대문구", "중랑구",
    "성북구", "강북구", "도봉구", "노원구", "은평구", "서대문구", "마포구",
    "양천구", "강서구", "구로구", "금천구", "영등포구", "동작구", "관악구",
    "서초구", "강남구", "송파구", "강동구",
]
TARGET_YEARS = list(range(2020, 2027))  # 2020 ~ 2026

# 맞춤형 항목조회에서 선택할 컬럼
CUSTOM_COLUMNS = ["대지위치", "도로명대지위치", "사용량(KWh)"]

# ---------------------------------------------------------------------------
# 경로·URL
# ---------------------------------------------------------------------------

DOWNLOAD_DIR = Path(__file__).parent.parent / "file" / "building_energy_data"
STATE_FILE = Path(__file__).parent / "crawling_state.json"
BASE_URL = "https://www.hub.go.kr/portal/opn/tyb/idx-nbem-elcty.do"
DOWNLOAD_PURPOSE = "웹사이트개발"

REQUEST_INTERVAL_SEC = 3
MAX_RETRIES = 3

# ---------------------------------------------------------------------------
# 콘솔 출력
# ---------------------------------------------------------------------------

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[logging.StreamHandler()],
)
log = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# 진행 상태 (completed / failed)
# ---------------------------------------------------------------------------

def item_id(year: int, district: str) -> str:
    return f"{year}_{district}"


def dest_path(year: int, district: str) -> Path:
    return DOWNLOAD_DIR / f"{item_id(year, district)}.json"


def make_item(year: int, district: str, **extra) -> dict:
    return {"id": item_id(year, district), "year": year, "district": district, **extra}


def _now_iso() -> str:
    return datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds")


def load_state() -> dict:
    if STATE_FILE.exists():
        with STATE_FILE.open(encoding="utf-8") as f:
            return json.load(f)
    return {"completed": [], "failed": []}


def save_state(state: dict) -> None:
    STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
    with STATE_FILE.open("w", encoding="utf-8") as f:
        json.dump(state, f, ensure_ascii=False, indent=2)


def sync_state_with_files(state: dict) -> dict:
    """디스크에 있는 파일 기준으로 completed를 맞추고, failed에서 제거."""
    completed_map = {item["id"]: item for item in state.get("completed", [])}

    for path in DOWNLOAD_DIR.glob("*.json"):
        year_str, district = path.stem.split("_", 1)
        entry = make_item(int(year_str), district, completed_at=_now_iso())
        completed_map[entry["id"]] = entry

    state["completed"] = list(completed_map.values())
    completed_ids = set(completed_map)

    state["failed"] = [
        item for item in state.get("failed", [])
        if item["id"] not in completed_ids
    ]
    return state


def build_run_queue(
    state: dict,
    districts: list[str],
    years: list[int],
) -> tuple[list[dict], list[dict], list[dict]]:
    """
    실행 큐를 만든다.
    반환: (실행할 목록, 스킵 목록, 전체 목록)
    우선순위: failed 재시도 → 아직 안 한 pending
    """
    all_tasks = [make_item(year, district) for year in years for district in districts]
    completed_ids = {item["id"] for item in state.get("completed", [])}
    failed_map = {item["id"]: item for item in state.get("failed", [])}

    skipped = []
    retry = []
    pending = []

    for task in all_tasks:
        tid = task["id"]
        if dest_path(task["year"], task["district"]).exists() or tid in completed_ids:
            skipped.append(task)
            continue
        if tid in failed_map:
            retry.append({**task, "last_error": failed_map[tid].get("error")})
        else:
            pending.append(task)

    # failed 먼저, 그다음 미처리
    queue = retry + pending
    return queue, skipped, all_tasks


def mark_completed(state: dict, year: int, district: str) -> None:
    tid = item_id(year, district)
    state["completed"] = [
        item for item in state.get("completed", []) if item["id"] != tid
    ]
    state["completed"].append(make_item(year, district, completed_at=_now_iso()))
    state["failed"] = [
        item for item in state.get("failed", []) if item["id"] != tid
    ]


def mark_failed(state: dict, year: int, district: str, error: str) -> None:
    tid = item_id(year, district)
    state["failed"] = [
        item for item in state.get("failed", []) if item["id"] != tid
    ]
    state["failed"].append(
        make_item(year, district, error=str(error), failed_at=_now_iso())
    )


# ---------------------------------------------------------------------------
# 헬퍼
# ---------------------------------------------------------------------------

async def dismiss_leftovers(frame: Frame):
    """잔존 모달·오버레이를 닫는다."""
    try:
        await frame.evaluate("""() => {
            document.querySelectorAll('.dvMsgBoxBTN').forEach(btn => {
                if (btn.textContent.trim() === '확인') btn.click();
            });
            const x = Array.from(document.querySelectorAll('.sd-label-txt'))
                .find(el => el.textContent.trim() === 'X');
            if (x) x.closest('[id^="Label"]')?.click();
        }""")
    except Exception:
        pass


async def get_report_frame(page: Page) -> Frame:
    for frame in page.frames:
        if frame.name == "REPORT_VIEW":
            return frame
    raise RuntimeError("REPORT_VIEW iframe 없음")


async def find_select_id(frame, sample_labels: list[str]) -> str:
    """sample_labels 중 하나를 옵션으로 갖는 select의 id 반환."""
    selects = await frame.evaluate(
        "() => Array.from(document.querySelectorAll('select')).map(s => ({"
        "  id: s.id,"
        "  options: Array.from(s.options).map(o => o.text.trim())"
        "}))"
    )
    for s in selects:
        if any(lbl in s["options"] for lbl in sample_labels):
            return s["id"]
    raise RuntimeError(f"select 없음: {sample_labels}")


async def click_img_btn(frame, src_keyword: str):
    """src에 src_keyword가 포함된 img를 감싸는 래퍼 div[id^=Image]를 Playwright로 클릭."""
    wrapper = frame.locator(f'div[id^="Image"]:has(img[src*="{src_keyword}"])').first
    await wrapper.wait_for(state="visible", timeout=10_000)
    await wrapper.click()


async def handle_purpose_modal(frame, page: Page):
    """'대용량 제공 서비스 다운로드' 모달 처리."""
    try:
        await frame.wait_for_selector("text=대용량 제공 서비스 다운로드", timeout=8_000)
        log.info("  사용목적 모달 감지")
        await frame.locator(".dvMsgBoxBTN", has_text="확인").click()
        log.info("  확인 클릭")
        await page.wait_for_timeout(1_000)
    except PlaywrightTimeoutError:
        log.debug("  모달 없음")


# ---------------------------------------------------------------------------
# 맞춤형 항목조회 설정
# ---------------------------------------------------------------------------

async def setup_custom_columns(frame, page: Page):
    """맞춤형 항목조회 모달에서 CUSTOM_COLUMNS만 체크."""
    log.info("  맞춤형 항목조회 설정")

    await click_img_btn(frame, "btn_%EB%A7%9E%EC%B6%A4%ED%98%95%ED%95%AD%EB%AA%A9%EC%A1%B0%ED%9A%8C.png")
    await page.wait_for_timeout(800)

    await frame.evaluate("""() => {
        document.querySelectorAll('.checkbox[check="true"]').forEach(cb => cb.click());
    }""")
    await page.wait_for_timeout(500)

    for col in CUSTOM_COLUMNS:
        cb = frame.locator(".sd-control-nowrap").filter(has_text=col).locator(".checkbox").first
        await cb.click()
        await page.wait_for_timeout(300)
        log.info("  항목 체크: %s", col)

    await frame.evaluate("""() => {
        const els = Array.from(document.querySelectorAll('.sd-label-txt'));
        const x = els.find(el => el.textContent.trim() === 'X');
        if (x) x.closest('[id^="Label"]').click();
    }""")
    await page.wait_for_timeout(500)
    log.info("  맞춤형 항목조회 완료")


# ---------------------------------------------------------------------------
# 한 건(구 + 연도) 다운로드 로직
# ---------------------------------------------------------------------------

async def download_one(page: Page, district: str, year: int):
    log.info("▶ %s %s %d년", TARGET_CITY, district, year)
    frame = await get_report_frame(page)

    await dismiss_leftovers(frame)

    city_id = await find_select_id(frame, ["서울특별시", "부산광역시"])
    await frame.select_option(f"#{city_id}", label=TARGET_CITY)
    await page.wait_for_timeout(1_000)

    gu_id = await find_select_id(frame, TARGET_DISTRICTS)
    await frame.select_option(f"#{gu_id}", label=district)
    await page.wait_for_timeout(500)

    await frame.evaluate(f"""() => {{
        const inputs = Array.from(document.querySelectorAll('input.Calendar_textbox'));
        const target = inputs.find(i => /^\\d{{4}}$/.test(i.value.trim()));
        if (target) {{
            target.value = '{year}';
            target.dispatchEvent(new Event('change', {{bubbles: true}}));
            target.dispatchEvent(new Event('blur', {{bubbles: true}}));
        }}
    }}""")
    await page.wait_for_timeout(400)

    await setup_custom_columns(frame, page)
    await page.wait_for_timeout(500)

    await click_img_btn(frame, "btn_%EA%B2%80%EC%83%89.png")
    await page.wait_for_load_state("networkidle", timeout=30_000)
    await page.wait_for_timeout(1_500)

    DOWNLOAD_DIR.mkdir(parents=True, exist_ok=True)
    async with page.expect_download(timeout=60_000) as dl_info:
        await click_img_btn(frame, "btn_JSON.png")
        await handle_purpose_modal(frame, page)

    dl = await dl_info.value
    dest = dest_path(year, district)
    await dl.save_as(dest)
    log.info("  저장: %s", dest)


# ---------------------------------------------------------------------------
# 크롤링 실행
# ---------------------------------------------------------------------------

async def run(districts: list[str], years: list[int]):
    DOWNLOAD_DIR.mkdir(parents=True, exist_ok=True)

    state = sync_state_with_files(load_state())
    queue, skipped, all_tasks = build_run_queue(state, districts, years)
    save_state(state)

    log.info(
        "대상 %d건 | 스킵 %d건 | 실행 %d건 (재시도 우선)",
        len(all_tasks), len(skipped), len(queue),
    )
    if not queue:
        log.info("처리할 항목 없음. 상태 파일: %s", STATE_FILE)
        return

    succeeded = 0

    async with async_playwright() as pw:
        browser = await pw.chromium.launch(headless=True)
        context = await browser.new_context(
            accept_downloads=True,
            viewport={"width": 1600, "height": 1000},
        )
        page = await context.new_page()

        log.info("접속: %s", BASE_URL)
        await page.goto(BASE_URL, wait_until="networkidle", timeout=60_000)
        await page.wait_for_timeout(2_000)

        for i, task in enumerate(queue, 1):
            year, district = task["year"], task["district"]
            label = "재시도" if "last_error" in task else "신규"
            log.info("[%d/%d] %s — %s", i, len(queue), label, task["id"])

            for attempt in range(1, MAX_RETRIES + 1):
                try:
                    await download_one(page, district, year)
                    mark_completed(state, year, district)
                    save_state(state)
                    succeeded += 1
                    await page.wait_for_timeout(REQUEST_INTERVAL_SEC * 1_000)
                    break
                except Exception as exc:
                    log.warning(
                        "  실패 (시도 %d/%d): %s — %s",
                        attempt, MAX_RETRIES, task["id"], exc,
                    )
                    try:
                        await dismiss_leftovers(await get_report_frame(page))
                    except Exception:
                        pass
                    if attempt == MAX_RETRIES:
                        mark_failed(state, year, district, str(exc))
                        save_state(state)
                        log.error("  최종 실패 기록: %s", task["id"])
                    else:
                        await page.wait_for_timeout(5_000 * attempt)

        await browser.close()

    state = load_state()
    log.info(
        "완료 — 이번 실행 성공 %d건 | 누적 완료 %d건 | 실패 %d건",
        succeeded,
        len(state.get("completed", [])),
        len(state.get("failed", [])),
    )
    if state.get("failed"):
        log.info("실패 목록: %s", [f["id"] for f in state["failed"]])
    log.info("상태 파일: %s", STATE_FILE)


# ---------------------------------------------------------------------------
# 진입점
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    asyncio.run(run(TARGET_DISTRICTS, TARGET_YEARS))

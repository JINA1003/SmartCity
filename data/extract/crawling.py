"""
건축HUB 건물에너지(전기에너지) 데이터 크롤러.

흐름:
  1. 페이지 접속 (REPORT_VIEW iframe)
  2. 시도 → 서울특별시
  3. 시군구 → 각 구
  4. 연도 입력 (input.Calendar_textbox, value가 4자리 연도인 것)
  5. 검색 (btn_검색.png 래퍼 div 클릭)
  6. 맞춤형 항목조회 클릭 → 전체해제 → 도로명대지위치·사용량(KWh) 체크 → 닫기
  7. JSON 다운로드 (btn_JSON.png 래퍼 div 클릭)
  8. 사용목적 모달 → 웹사이트개발 → 확인

저장: data/file/building_energy_data/{year}_{구}.json
"""

import asyncio
import logging
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
CUSTOM_COLUMNS = ["도로명대지위치", "사용량(KWh)"]

# ---------------------------------------------------------------------------
# 경로·URL
# ---------------------------------------------------------------------------

DOWNLOAD_DIR     = Path(__file__).parent.parent / "file" / "building_energy_data"
BASE_URL         = "https://www.hub.go.kr/portal/opn/tyb/idx-nbem-elcty.do"
DOWNLOAD_PURPOSE = "웹사이트개발"

REQUEST_INTERVAL_SEC = 3
MAX_RETRIES          = 3

# ---------------------------------------------------------------------------
# 로깅
# ---------------------------------------------------------------------------

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[
        logging.FileHandler(Path(__file__).parent / "crawling.log", encoding="utf-8"),
        logging.StreamHandler(),
    ],
)
log = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# 헬퍼
# ---------------------------------------------------------------------------

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
    """'대용량 제공 서비스 다운로드' 모달 처리.
    모달은 iframe 안에 뜸. 웹사이트개발 이미 선택된 상태이므로 확인만 클릭.
    버튼 구조: <div class="dvMsgBoxBTN">확인</div>
    """
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
    """
    맞춤형 항목조회 모달:
      1. 맞춤형 항목조회 버튼 클릭
      2. 전체해제 라디오 클릭
      3. CUSTOM_COLUMNS 항목만 체크
      4. 모달 닫기 (X 클릭)
    """
    log.info("  맞춤형 항목조회 설정")

    # 맞춤형 항목조회 버튼 클릭
    await click_img_btn(frame, "btn_%EB%A7%9E%EC%B6%A4%ED%98%95%ED%95%AD%EB%AA%A9%EC%A1%B0%ED%9A%8C.png")
    await page.wait_for_timeout(800)

    # 전체해제 라디오 클릭 (텍스트 "전체해제" 포함 div)
    await frame.evaluate("""() => {
        const els = Array.from(document.querySelectorAll('.control_etc1_text'));
        const target = els.find(el => el.textContent.trim() === '전체해제');
        if (target) target.click();
    }""")
    await page.wait_for_timeout(400)

    # 원하는 항목만 체크
    for col in CUSTOM_COLUMNS:
        await frame.evaluate(f"""() => {{
            const els = Array.from(document.querySelectorAll('.control_etc1_text'));
            const target = els.find(el => el.textContent.trim() === {repr(col)});
            if (target) target.click();
        }}""")
        await page.wait_for_timeout(200)
        log.info("  항목 체크: %s", col)

    # 모달 닫기 (X 라벨)
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

    # 시도 선택
    city_id = await find_select_id(frame, ["서울특별시", "부산광역시"])
    await frame.select_option(f"#{city_id}", label=TARGET_CITY)
    await page.wait_for_timeout(1_000)

    # 시군구 선택
    gu_id = await find_select_id(frame, TARGET_DISTRICTS)
    await frame.select_option(f"#{gu_id}", label=district)
    await page.wait_for_timeout(500)

    # 연도 입력 — 4자리 숫자 value를 가진 Calendar_textbox
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

    # 검색
    await click_img_btn(frame, "btn_%EA%B2%80%EC%83%89.png")
    await page.wait_for_load_state("networkidle", timeout=30_000)
    await page.wait_for_timeout(1_500)

    # 맞춤형 항목조회 (도로명대지위치 + 사용량만 선택)
    await setup_custom_columns(frame, page)
    await page.wait_for_timeout(500)

    # JSON 다운로드
    DOWNLOAD_DIR.mkdir(parents=True, exist_ok=True)
    async with page.expect_download(timeout=60_000) as dl_info:
        await click_img_btn(frame, "btn_JSON.png")
        await handle_purpose_modal(frame, page)

    dl = await dl_info.value
    dest = DOWNLOAD_DIR / f"{year}_{district}.json"
    await dl.save_as(dest)
    log.info("  저장: %s", dest)


# ---------------------------------------------------------------------------
# 크롤링 실행
# ---------------------------------------------------------------------------

async def run(districts: list[str], years: list[int]):
    DOWNLOAD_DIR.mkdir(parents=True, exist_ok=True)

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

        total = len(districts) * len(years)
        done  = 0

        for year in years:
            for district in districts:
                for attempt in range(1, MAX_RETRIES + 1):
                    try:
                        await download_one(page, district, year)
                        done += 1
                        log.info("  진행: %d / %d", done, total)
                        await page.wait_for_timeout(REQUEST_INTERVAL_SEC * 1_000)
                        break
                    except Exception as exc:
                        log.warning(
                            "  실패 (시도 %d/%d): %s %d년 — %s",
                            attempt, MAX_RETRIES, district, year, exc,
                        )
                        if attempt == MAX_RETRIES:
                            log.error("  최종 실패: %s %d년", district, year)
                        else:
                            await page.wait_for_timeout(5_000 * attempt)

        log.info("완료: %d / %d 건", done, total)
        await browser.close()


# ---------------------------------------------------------------------------
# 진입점
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    asyncio.run(run(TARGET_DISTRICTS, TARGET_YEARS))

"""
한국전력공사(KEPCO) 전력판매량 게시판 첨부파일 크롤러.

대상 페이지:
  https://www.kepco.co.kr/home/customer/library/electricity-statistics/sales-volume/boardList.do

흐름 (각 게시글 카드마다 반복):
  1. 목록 페이지 접속
  2. 페이지 1 ~ MAX_PAGES 순회
  3. 카드별 '첨부파일' 팝업 열기 (AJAX 페이지는 JS fallback)
  4. '다운로드' 클릭
  5. 세션 최초 1회: 수요조사 모달 작성 → '제출 및 다운로드'
     이후: 모달 없이 바로 다운로드
  6. xlsx 파일 저장

저장 경로: data/file/kepco_electricity_sales/{파일명}.xlsx
"""

import asyncio
import logging
import re
from pathlib import Path

from playwright.async_api import Page, TimeoutError as PlaywrightTimeoutError
from playwright.async_api import async_playwright

# ---------------------------------------------------------------------------
# 수집 대상
# ---------------------------------------------------------------------------

MAX_PAGES = 7  # 1페이지부터 7페이지까지 수집

# 자료 다운로드 수요조사 양식에 입력할 값
SURVEY_USAGE_PLACE = "교육"           # 사용처
SURVEY_USAGE_PURPOSE = "학술/연구 목적"  # 사용목적
SURVEY_STAT_ITEM = "판매부문"          # 활용 통계 항목

# ---------------------------------------------------------------------------
# 경로·URL
# ---------------------------------------------------------------------------

BASE_URL = (
    "https://www.kepco.co.kr/home/customer/library/"
    "electricity-statistics/sales-volume/boardList.do"
)
DOWNLOAD_DIR = Path(__file__).parent.parent / "file" / "kepco_electricity_sales"

REQUEST_INTERVAL_SEC = 2  # 건 사이 대기 (서버 부하 완화)
MAX_RETRIES = 3

# ---------------------------------------------------------------------------
# 로깅
# ---------------------------------------------------------------------------

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[
        logging.FileHandler(
            Path(__file__).parent / "kepco_electricity_sales.log",
            encoding="utf-8",
        ),
        logging.StreamHandler(),
    ],
)
log = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# 헬퍼
# ---------------------------------------------------------------------------

def sanitize_filename(name: str) -> str:
    """OS에서 사용할 수 없는 문자를 제거."""
    return re.sub(r'[\\/:*?"<>|]', "_", name).strip()


async def close_open_popups(page: Page):
    """열려 있는 첨부·수요조사 팝업을 닫는다."""
    for selector in (
        ".layer-popup-container.file-download.open .close-btn",
        ".board-list-file-popup.open .close-btn",
    ):
        btn = page.locator(selector).first
        if await btn.count() and await btn.is_visible():
            try:
                await btn.click(timeout=2_000)
            except Exception:
                pass
            await page.wait_for_timeout(300)

    # AJAX 페이지 이동 후에는 닫기 버튼 핸들러가 동작하지 않을 수 있음
    await page.evaluate(
        "() => document.querySelectorAll('.board-list-file-popup.open')"
        ".forEach(el => el.classList.remove('open'))"
    )


async def open_file_popup(card) -> None:
    """
    카드의 첨부파일 팝업을 연다.
    2페이지 이후 AJAX로 목록이 갱신되면 버튼 클릭 핸들러가 묶이지 않아
    팝업이 안 뜨므로, 실패 시 DOM에 open 클래스를 직접 추가한다.
    """
    popup = card.locator(".board-list-file-popup")
    await card.locator("button.file-popup-open-pc").click()

    opened = card.locator(".board-list-file-popup.open")
    try:
        await opened.wait_for(state="visible", timeout=1_500)
        return
    except PlaywrightTimeoutError:
        pass

    await card.evaluate(
        "el => el.querySelector('.board-list-file-popup')?.classList.add('open')"
    )
    await opened.wait_for(state="visible", timeout=5_000)


async def submit_download_survey(page: Page):
    """
    '자료 다운로드 수요조사' 모달이 뜬 경우에만 양식을 작성하고 제출한다.
    세션당 최초 1회만 모달이 표시된다.
    """
    survey = page.locator(".layer-popup-container.file-download.open")
    await survey.locator("label", has_text=SURVEY_USAGE_PLACE).click()
    await page.wait_for_timeout(200)
    await survey.locator("label", has_text=SURVEY_USAGE_PURPOSE).click()
    await page.wait_for_timeout(200)
    await survey.locator("label", has_text=SURVEY_STAT_ITEM).click()
    await page.wait_for_timeout(200)
    await survey.locator("button.btn:has-text('제출 및 다운로드')").click()


async def go_to_page(page: Page, page_num: int):
    """페이지네이션에서 지정한 페이지로 이동."""
    if page_num == 1:
        return

    # 숫자 링크 클릭 (다음 버튼과 onclick이 겹치므로 number-box 안만 사용)
    link = page.locator(".pagination .number-box a").filter(
        has_text=str(page_num)
    ).first
    await link.click()
    await page.wait_for_timeout(1_500)
    await page.locator(".card.board.has-file-item").first.wait_for(
        state="visible", timeout=15_000
    )
    log.info("  %d페이지 로드 완료", page_num)


async def get_card_posts(page: Page) -> list[dict]:
    """현재 페이지의 게시글 카드 메타정보를 수집."""
    cards = page.locator(".card.board.has-file-item")
    count = await cards.count()
    posts = []

    for i in range(count):
        card = cards.nth(i)
        post_no = (await card.locator(".badge.blue").text_content() or "").strip()
        post_date = (await card.locator(".badge.gray").text_content() or "").strip()
        title = (await card.locator("a.title").text_content() or "").strip()
        posts.append({"index": i, "no": post_no, "date": post_date, "title": title})

    return posts


# ---------------------------------------------------------------------------
# 첨부파일 1건 다운로드
# ---------------------------------------------------------------------------

async def download_attachment(page: Page, card_index: int, meta: dict) -> bool:
    """
    카드 1건의 첨부 xlsx를 다운로드한다.
    이미 저장된 파일이면 건너뛴다. 성공 시 True, 스킵 시 False.
    """
    card = page.locator(".card.board.has-file-item").nth(card_index)
    log.info("▶ %s | %s | %s", meta["no"], meta["date"], meta["title"])

    await close_open_popups(page)

    # 첨부파일 팝업 열기 (2페이지+ AJAX 갱신 시 버튼 핸들러 미동작 → fallback)
    await open_file_popup(card)
    popup = card.locator(".board-list-file-popup.open")

    file_name = (await popup.locator(".file-name").first.text_content() or "").strip()
    if not file_name:
        raise RuntimeError("첨부파일명을 찾을 수 없음")

    dest = DOWNLOAD_DIR / f"{sanitize_filename(file_name)}.xlsx"
    if dest.exists():
        log.info("  건너뜀 (이미 존재): %s", dest.name)
        await card.evaluate(
            "el => el.querySelector('.board-list-file-popup.open')"
            "?.classList.remove('open')"
        )
        return False

    # 다운로드 클릭 — 최초 1회는 수요조사 모달, 이후는 바로 다운로드
    DOWNLOAD_DIR.mkdir(parents=True, exist_ok=True)
    async with page.expect_download(timeout=60_000) as dl_info:
        await popup.locator('a.txt-btn:has-text("다운로드")').first.click()

        survey = page.locator(".layer-popup-container.file-download.open")
        try:
            await survey.wait_for(state="visible", timeout=2_500)
            log.info("  수요조사 모달 작성")
            await submit_download_survey(page)
        except PlaywrightTimeoutError:
            log.debug("  수요조사 생략 — 바로 다운로드")

    download = await dl_info.value
    await download.save_as(dest)
    log.info("  저장: %s", dest)

    await close_open_popups(page)
    return True


# ---------------------------------------------------------------------------
# 페이지 단위 크롤링
# ---------------------------------------------------------------------------

async def crawl_page(page: Page, page_num: int) -> tuple[int, int]:
    """
    한 페이지의 모든 첨부파일을 다운로드한다.
    반환: (다운로드 건수, 스킵 건수)
    """
    await go_to_page(page, page_num)
    posts = await get_card_posts(page)
    log.info("%d페이지 — 게시글 %d건", page_num, len(posts))

    downloaded = 0
    skipped = 0

    for meta in posts:
        for attempt in range(1, MAX_RETRIES + 1):
            try:
                saved = await download_attachment(page, meta["index"], meta)
                if saved:
                    downloaded += 1
                else:
                    skipped += 1
                await page.wait_for_timeout(REQUEST_INTERVAL_SEC * 1_000)
                break
            except Exception as exc:
                log.warning(
                    "  실패 (시도 %d/%d): %s — %s",
                    attempt, MAX_RETRIES, meta["title"], exc,
                )
                await close_open_popups(page)
                if attempt == MAX_RETRIES:
                    log.error("  최종 실패: %s", meta["title"])
                else:
                    await page.wait_for_timeout(3_000 * attempt)

    return downloaded, skipped


# ---------------------------------------------------------------------------
# 크롤링 실행
# ---------------------------------------------------------------------------

async def run(max_pages: int = MAX_PAGES):
    DOWNLOAD_DIR.mkdir(parents=True, exist_ok=True)
    total_downloaded = 0
    total_skipped = 0

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

        for page_num in range(1, max_pages + 1):
            downloaded, skipped = await crawl_page(page, page_num)
            total_downloaded += downloaded
            total_skipped += skipped
            log.info(
                "%d페이지 완료 — 다운로드 %d건, 스킵 %d건",
                page_num, downloaded, skipped,
            )

        await browser.close()

    log.info(
        "전체 완료 — 다운로드 %d건, 스킵 %d건 | 저장 위치: %s",
        total_downloaded, total_skipped, DOWNLOAD_DIR,
    )


# ---------------------------------------------------------------------------
# 진입점
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    asyncio.run(run())

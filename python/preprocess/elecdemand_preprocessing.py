"""
[전력 사용량 전처리]

입력:
- data/file/kepco_electricity_sales
- data/output/epsis_supply_rate_final_20052026.csv

출력:
- data/output/kepco_preprocessed.csv
- data/output/kepco_final_20052026.csv
"""

import os
import re
import pandas as pd


# =========================
# 0. 경로 설정
# =========================
INPUT_FOLDER = "data/file/kepco_electricity_sales"
SUPPLY_RATE_PATH = "data/output/epsis_supply_rate_final_20052026.csv"

PREPROCESSED_PATH = "data/output/kepco_preprocessed.csv"
FINAL_PATH = "data/output/kepco_final_20052026.csv"


# =========================
# 1. 파일 필터링
# =========================
def is_valid(file_name: str) -> bool:

    # 임시파일 제외
    if file_name.startswith("~$"):
        return False

    # 엑셀만 허용
    if not file_name.endswith((".xlsx", ".xls")):
        return False

    # -------------------------
    # 1) 2021 파일 처리
    # -------------------------
    if ("2021" in file_name) or file_name.startswith("21"):
        return "202112" in file_name

    # -------------------------
    # 2) 2026 파일 처리
    # -------------------------
    if "2026" in file_name:
        return "202604" in file_name

    # -------------------------
    # 3) 홈페이지 게시용 파일
    # -------------------------
    if "홈페이지" in file_name:
        m = re.search(r"(\d{6})", file_name)
        if not m:
            return False

        month = int(m.group(1)[4:])
        return month == 12

    # -------------------------
    # 4) 2004년 파일 제거
    # -------------------------
    if "2004" in file_name:
        return False

    return True


# =========================
# 2. 개별 파일 전처리
# =========================
def process_file(file_path: str) -> pd.DataFrame:
    df_raw = pd.read_excel(file_path, header=None)

    header_row_idx = df_raw[df_raw.iloc[:, 0] == "연도"].index[0]

    df_raw.columns = df_raw.iloc[header_row_idx]
    df = df_raw.iloc[header_row_idx + 1:].reset_index(drop=True)
    df.columns = df.columns.astype(str).str.strip()

    # 파일의 연도 확인
    year = int(
        str(df.iloc[0]["연도"])
        .replace("년", "")
        .strip()
    )

    # 월 컬럼 설정
    if year == 2026:
        months = [
            "1월", "2월", "3월", "4월"
        ]
    else:
        months = [
            "1월", "2월", "3월", "4월", "5월", "6월",
            "7월", "8월", "9월", "10월", "11월", "12월"
        ]

    df[months] = df[months].apply(pd.to_numeric, errors="coerce")

    df_long = df.melt(
        id_vars=["연도", "시도", "시군구", "계약종별"],
        value_vars=months,
        var_name="월",
        value_name="전력사용량"
    )

    # 서울만 필터링
    df_long = df_long[df_long["시도"].str.startswith("서울", na=False)].copy()

    # 계약종별 정리
    df_long["계약종별"] = (
        df_long["계약종별"]
        .astype(str)
        .str.replace(" ", "", regex=False)
        .str.strip()
    )

    # 총계 -> 합계 통일
    df_long["계약종별"] = df_long["계약종별"].replace("총계", "합계")

    # 컬럼명 변경
    df_long = df_long.rename(columns={"계약종별": "전력용도"})

    # 음수 제거
    df_long = df_long[df_long["전력사용량"] >= 0].copy()

    # 월 정리
    df_long["월"] = (
        df_long["월"]
        .str.replace("월", "", regex=False)
        .astype(int)
    )

    # 연도 정리
    df_long["연도"] = (
        df_long["연도"]
        .astype(str)
        .str.replace("년", "", regex=False)
        .str.strip()
        .astype(int)
    )

    # 2014년 이후 단위 변환
    if int(df_long["연도"].iloc[0]) >= 2014:
        df_long["전력사용량"] = df_long["전력사용량"] / 1000

    df_long = df_long.rename(
        columns={"전력사용량": "전력사용량(MWh)"}
    )

    return df_long


# =========================
# 3. 전체 폴더 전처리
# =========================
def preprocess_kepco_folder(
    folder_path: str,
    output_path: str | None = None
) -> pd.DataFrame:

    all_data = []

    files = sorted([f for f in os.listdir(folder_path) if is_valid(f)])

    print(f"총 처리 파일 수: {len(files)}")

    for file in files:
        file_path = os.path.join(folder_path, file)

        try:
            df_long = process_file(file_path)
            all_data.append(df_long)
            print(f"처리 완료: {file}")

        except Exception as e:
            print(f"❌ 오류 발생 ({file}): {e}")

    if not all_data:
        raise ValueError("처리된 파일이 없습니다. 입력 폴더와 파일명을 확인하세요.")

    result = pd.concat(all_data, ignore_index=True)

    result = result.sort_values(
        by=["연도", "월", "시군구", "전력용도"],
        ascending=[True, True, True, True]
    ).reset_index(drop=True)

    print("전처리 완료")
    print("전체 데이터 크기:", result.shape)

    if output_path is not None:
        os.makedirs(os.path.dirname(output_path), exist_ok=True)
        result.to_csv(output_path, index=False, encoding="utf-8-sig")
        print(f"저장 완료: {output_path}")

    return result


# =========================
# 4. 합계 기준 전력사용율 계산
# =========================
def add_usage_ratio(df: pd.DataFrame) -> pd.DataFrame:
    total_df = df[df["전력용도"] == "합계"][
        ["연도", "월", "시군구", "전력사용량(MWh)"]
    ].copy()

    total_df = total_df.rename(
        columns={"전력사용량(MWh)": "합계전력사용량"}
    )

    df = df.merge(
        total_df,
        on=["연도", "월", "시군구"],
        how="left"
    )

    df["전력사용율"] = (
        df["전력사용량(MWh)"] / df["합계전력사용량"] * 100
    ).round(3).fillna(0)

    df = df.drop(columns=["합계전력사용량"])

    return df


# =========================
# 5. 수요 감축 필요도 계산
# =========================
def add_reduction_need(df: pd.DataFrame, rate_path: str) -> pd.DataFrame:
    rate = pd.read_csv(rate_path)

    idx_df = df.merge(
        rate,
        left_on=["연도", "월"],
        right_on=["년", "월"],
        how="left"
    ).copy()

    idx_df = idx_df.drop(columns=["년"])

    idx_df["공급위험도"] = 1 - idx_df["공급예비율(%)"] / 100

    # 합계 행은 정규화 계산에서 제외
    idx_df["용도정규화사용률"] = 0.0

    mask = idx_df["전력용도"] != "합계"

    idx_df.loc[mask, "용도정규화사용률"] = (
        idx_df[mask]
        .groupby(["시군구", "연도", "월"])["전력사용율"]
        .transform(lambda x: x / x.max())
        .fillna(0)
    )

    idx_df["수요감축필요도_1st"] = (
        idx_df["용도정규화사용률"] * idx_df["공급위험도"]
    )

    return idx_df


# =========================
# 6. 컬럼명 영어로 정리
# =========================
def rename_columns(df: pd.DataFrame) -> pd.DataFrame:
    return df.rename(columns={
        "연도": "year",
        "시도": "sido",
        "시군구": "sigungu",
        "전력용도": "usage_type",
        "월": "month",
        "전력사용량(MWh)": "power_mwh",
        "전력사용율": "usage_ratio",
        "공급능력(MW)": "capacity_mw",
        "최대전력(MW)": "peak_mw",
        "공급예비력(MW)": "reserve_mw",
        "공급예비율(%)": "reserve_rate",
        "공급위험도": "risk_score",
        "용도정규화사용률": "norm_usage",
        "수요감축필요도_1st": "reduction_need_draft"
    })


# =========================
# 7. 실행
# =========================
if __name__ == "__main__":

    df = preprocess_kepco_folder(
        folder_path=INPUT_FOLDER,
        output_path=PREPROCESSED_PATH
    )

    print("전처리된 데이터 저장 완료")
    print(f"저장위치: {PREPROCESSED_PATH}")

    df = add_usage_ratio(df)

    idx_df = add_reduction_need(
        df=df,
        rate_path=SUPPLY_RATE_PATH
    )

    idx_df = rename_columns(idx_df)

    os.makedirs(os.path.dirname(FINAL_PATH), exist_ok=True)

    idx_df.to_csv(
        FINAL_PATH,
        index=False,
        encoding="utf-8-sig"
    )

    print("수요 감축 필요도 계산 완료")
    print("최종 데이터 저장 완료")
    print(f"저장위치: {FINAL_PATH}")
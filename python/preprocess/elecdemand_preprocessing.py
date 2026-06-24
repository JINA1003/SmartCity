"""
[전력 사용량 전처리]

입력:
- data/file/kepco_electricity_sales
- data/output/epsis_supply_rate_final_20052026.csv

출력:
- data/output/kepco_preprocessed.csv
- data/output/kepco_final_20052026.csv
- data/output/kepco_mapping_final_20052026.csv
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
MAPPING_FINAL_PATH = "data/output/kepco_mapping_final_20052026.csv"


# =========================
# 1. 파일 필터링
# =========================
def is_valid(file_name: str) -> bool:
    if file_name.startswith("~$"):
        return False

    if not file_name.endswith((".xlsx", ".xls")):
        return False

    if ("2021" in file_name) or file_name.startswith("21"):
        return "202112" in file_name

    if "2026" in file_name:
        return "202604" in file_name

    if "홈페이지" in file_name:
        m = re.search(r"(\d{6})", file_name)
        if not m:
            return False

        month = int(m.group(1)[4:])
        return month == 12

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

    year = int(
        str(df.iloc[0]["연도"])
        .replace("년", "")
        .strip()
    )

    if year == 2026:
        months = ["1월", "2월", "3월", "4월"]
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

    df_long = df_long[df_long["시도"].str.startswith("서울", na=False)].copy()

    df_long["계약종별"] = (
        df_long["계약종별"]
        .astype(str)
        .str.replace(" ", "", regex=False)
        .str.strip()
    )

    df_long["계약종별"] = df_long["계약종별"].replace("총계", "합계")
    df_long = df_long.rename(columns={"계약종별": "전력용도"})

    df_long = df_long[df_long["전력사용량"] >= 0].copy()

    df_long["월"] = (
        df_long["월"]
        .str.replace("월", "", regex=False)
        .astype(int)
    )

    df_long["연도"] = (
        df_long["연도"]
        .astype(str)
        .str.replace("년", "", regex=False)
        .str.strip()
        .astype(int)
    )

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
# 7. 건축용도 - 전력용도 매핑 생성
# =========================
def create_building_mapping() -> dict:
    return {
        "단독주택": "주택용",
        "공동주택": "주택용",
        "다가구주택": "주택용",

        "제1종근린생활시설": "일반용",
        "제2종근린생활시설": "일반용",
        "업무시설": "일반용",
        "의료시설": "일반용",
        "노유자시설": "일반용",
        "종교시설": "일반용",
        "문화및집회시설": "일반용",
        "판매시설": "일반용",
        "판매및영업시설": "일반용",
        "위락시설": "일반용",
        "관광휴게시설": "일반용",
        "숙박시설": "일반용",
        "운동시설": "일반용",
        "근린생활시설": "일반용",
        "공공용시설": "일반용",
        "장례식장": "일반용",
        "교육연구및복지시설": "일반용",
        "방송통신시설": "일반용",
        "운수시설": "일반용",
        "수련시설": "일반용",
        "교정및군사시설": "일반용",

        "교육연구시설": "교육용",

        "공장": "산업용",
        "창고시설": "산업용",
        "자동차관련시설": "산업용",
        "위험물저장및처리시설": "산업용",
        "동.식물 관련시설": "산업용",
        "발전시설": "산업용",
        "분뇨.쓰레기처리시설": "산업용",
        "묘지관련시설": "산업용",
        "가설건축물": "산업용",
    }


# =========================
# 8. 건축용도별 가중치 생성
# =========================
def create_weight_map() -> dict:
    return {
        # 국가 핵심시설
        "의료시설": 0.0,
        "발전시설": 0.0,
        "방송통신시설": 0.0,
        "운수시설": 0.0,
        "공공용시설": 0.0,
        "교정및군사시설": 0.0,

        # 주거
        "단독주택": 0.8,
        "공동주택": 0.8,
        "다가구주택": 0.8,
        "노유자시설": 0.4,

        # 교육
        "교육연구시설": 0.6,
        "교육연구및복지시설": 0.6,

        # 업무/문화
        "업무시설": 0.8,
        "종교시설": 0.8,
        "문화및집회시설": 0.8,
        "운동시설": 0.8,
        "수련시설": 0.8,

        # 상업
        "판매시설": 0.8,
        "판매및영업시설": 0.8,
        "위락시설": 0.9,
        "관광휴게시설": 0.8,
        "숙박시설": 0.8,
        "제1종근린생활시설": 0.7,
        "제2종근린생활시설": 0.7,
        "근린생활시설": 0.7,

        # 산업
        "공장": 0.8,
        "창고시설": 0.9,
        "자동차관련시설": 0.8,
        "위험물저장및처리시설": 0.3,
        "장례식장": 0.8,
        "동.식물 관련시설": 0.6,
        "분뇨.쓰레기처리시설": 0.3,
        "묘지관련시설": 0.8,
        "가설건축물": 1.0,
    }


# =========================
# 9. 전력 데이터에 건축용도 추가
# =========================
def add_building_type(df: pd.DataFrame) -> pd.DataFrame:
    mapping = create_building_mapping()

    mapping_df = pd.DataFrame(
        [(v, k) for k, v in mapping.items()],
        columns=["usage_type", "building_type"]
    )

    df["sigungu"] = df["sigungu"].astype(str).str.strip()
    df["usage_type"] = df["usage_type"].astype(str).str.strip()

    return df.merge(
        mapping_df,
        on="usage_type",
        how="left"
    )


# =========================
# 10. 단전 우선순위 점수 계산
# =========================
def add_reduction_score(df: pd.DataFrame) -> pd.DataFrame:
    weight_map = create_weight_map()

    df["weight"] = (
        df["building_type"]
        .map(weight_map)
        .fillna(0)
    )

    df["reduction_need_score"] = (
        df["reduction_need_draft"] * df["weight"] * 100
    )

    return df


# =========================
# 11. 매핑 최종 컬럼 정리
# =========================
def select_mapping_columns(df: pd.DataFrame) -> pd.DataFrame:
    return df[
        [
            "year",
            "month",
            "sigungu",
            "usage_type",
            "building_type",
            "power_mwh",
            "usage_ratio",
            "capacity_mw",
            "peak_mw",
            "reserve_mw",
            "reserve_rate",
            "reduction_need_score"
        ]
    ]


# =========================
# 12. 실행
# =========================
if __name__ == "__main__":

    # 1. 전력 데이터 전처리
    df = preprocess_kepco_folder(
        folder_path=INPUT_FOLDER,
        output_path=PREPROCESSED_PATH
    )

    print("전처리된 데이터 저장 완료")
    print(f"저장위치: {PREPROCESSED_PATH}")

    # 2. 전력사용율 계산
    df = add_usage_ratio(df)

    # 3. 수요 감축 필요도 계산
    idx_df = add_reduction_need(
        df=df,
        rate_path=SUPPLY_RATE_PATH
    )

    # 4. 기본 최종 데이터 저장
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

    # 5. 건축용도 매핑
    kepco_mapped = add_building_type(idx_df)

    # 6. 단전 우선순위 점수 계산
    kepco_mapped = add_reduction_score(kepco_mapped)

    # 7. 최종 매핑 데이터 저장
    kepco_mapped_final = select_mapping_columns(kepco_mapped)

    kepco_mapped_final.to_csv(
        MAPPING_FINAL_PATH,
        index=False,
        encoding="utf-8-sig"
    )

    print(f"kepco mapping 데이터 저장 완료: {MAPPING_FINAL_PATH}")
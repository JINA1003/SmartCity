"""
[전력 사용량 전처리]
출처: https://www.kepco.co.kr/home/customer/library/electricity-statistics/sales-volume/boardList.do
설명: 지역별 + 용도 + 달 전력 사용량
전처리 진행: 파일 필터링(각 연도별로 12월까지 저장된 파일만 가져오기) -> 파일별 결측, 공백, 컬럼 등 전처리

[생성 데이터 list]
- ../data/output/kepco_preprocessed.csv: 전처리만 진행(결측, 데이터타입, 컬럼 등 정리)
- ../data/output/kepco_final_data.csv: 전처리 + 지표 생성(수요 감축 필요도 등)
"""

import os
import re
import pandas as pd
import openpyxl


# =========================
# 1. 파일 필터링 함수
# =========================
def is_valid(file_name: str) -> bool:
    # 엑셀만 허용
    if not file_name.endswith((".xlsx", ".xls")):
        return False

    # -------------------------
    # 1) 2021 파일 처리
    # -------------------------
    # "2021" 또는 "21" 포함 파일 중
    # 오직 202112만 허용
    if ("2021" in file_name) or (file_name.startswith("21")):
        return "202112" in file_name

    # -------------------------
    # 2) 홈페이지 게시용 파일
    # -------------------------
    if "홈페이지" in file_name:
        m = re.search(r"(\d{6})", file_name)
        if not m:
            return False

        # YYYYMM에서 MM만 추출
        month = int(m.group(1)[4:])
        return month == 12
    
    # -------------------------
    # 3) 2004년 파일 제거
    # -------------------------
    if "2004" in file_name:
        return False

    # -------------------------
    # 3) 나머지 파일 허용
    # -------------------------
    return True


# =========================
# 2. 개별 파일 전처리
# =========================
def process_file(file_path: str, file_name: str) -> pd.DataFrame:

    df_raw = pd.read_excel(file_path, header=None)

    # "연도" 행 찾기
    header_row_idx = df_raw[df_raw.iloc[:, 0] == "연도"].index[0]

    # 컬럼 설정
    df_raw.columns = df_raw.iloc[header_row_idx]
    df = df_raw.iloc[header_row_idx + 1:].reset_index(drop=True)

    # 컬럼 정리
    df.columns = df.columns.astype(str).str.strip()

    # 월 컬럼
    months = ["1월","2월","3월","4월","5월","6월",
              "7월","8월","9월","10월","11월","12월"]

    # 숫자 변환
    df[months] = df[months].apply(pd.to_numeric, errors="coerce")

    # long format 변환
    df_long = df.melt(
        id_vars=["연도", "시도", "시군구", "계약종별"],
        value_vars=months,
        var_name="월",
        value_name="전력사용량"
    )

    # 서울시 필터
    df_long = df_long[df_long['시도'].str.startswith('서울')]

    # 계약종별 = 합계 or 총계 제거 & 공백 제거
    df_long["계약종별"] = (
        df_long["계약종별"]
        .astype(str)
        .str.replace(" ", "", regex=False)
        .str.strip()
        )

    df_long = df_long[~df_long["계약종별"].isin(["합계", "총계"])]
    df_long = df_long.rename(columns={"계약종별": "전력용도"})

    # 이상치 제거
    # 전력사용량 < 0 제거 (2007년 송파구 산업용)
    df_long = df_long[df_long['전력사용량'] >= 0]

    # 월 정리
    df_long["월"] = df_long["월"].str.replace("월", "", regex=False).astype(int)

    # 연도에서 '년' 제거 + 숫자로 변환
    df_long["연도"] = (
        df_long["연도"]
        .astype(str)
        .str.replace("년", "", regex=False)
        .str.strip()
        .astype(int)
        )
    
    if int(df_long["연도"].iloc[0]) >= 2014:
        df_long["전력사용량"] = df_long["전력사용량"] / 1000

    # 컬럼 이름 정리
    df_long =  df_long.rename(columns={"전력사용량":"전력사용량(MWh)"})

    return df_long


# =========================
# 3. 전체 폴더 처리 파이프라인
# =========================
def preprocess_kepco_folder(folder_path: str, output_path: str = None) -> pd.DataFrame:

    all_data = []

    files = [f for f in os.listdir(folder_path) if is_valid(f)]

    print(f"총 처리 파일 수: {len(files)}")

    for file in files:
        file_path = os.path.join(folder_path, file)

        try:
            df_long = process_file(file_path, file)
            all_data.append(df_long)
            print(f"저장 완료: {file}")


        except Exception as e:
            print(f"❌ 오류 발생 ({file}): {e}")

    # 합치기
    result = pd.concat(all_data, ignore_index=True)
    result = result.sort_values(
        by=["연도", "월", "시군구", "전력용도"],
        ascending=[True, True, True, True]
        ).reset_index(drop=True)

    print("전처리 완료")
    print("전체 데이터 크기:", result.shape)

    # =========================
    # 4. 저장 로직 추가
    # =========================
    if output_path is not None:

        # 폴더 자동 생성
        os.makedirs(os.path.dirname(output_path), exist_ok=True)

        if output_path.endswith(".csv"):
            result.to_csv(output_path, index=False, encoding="utf-8-sig")

        elif output_path.endswith(".xlsx"):
            result.to_excel(output_path, index=False)

        else:
            raise ValueError("지원하지 않는 파일 형식입니다 (csv, xlsx만 가능)")

        print(f"저장 완료: {output_path}")

    return result

df = preprocess_kepco_folder(
    folder_path="data/file/kepco_electricity_sales",
    output_path="data/output/kepco_preprocessed.csv"
)
print('전처리된 데이터 저장 완료')
print("저장위치: 'data/output/kepco_preprocessed.csv'")

# 연도-월별 전력사용량 합계 계산
df["연도_월별_시군구별_전력사용량합계"] = df.groupby(
    ["연도","월","시군구"]
)["전력사용량(MWh)"].transform("sum")

# 전력사용율 계산
df["전력사용율"] = (df["전력사용량(MWh)"] / df["연도_월별_시군구별_전력사용량합계"] * 100).round(3).fillna(0)

# ------------수요 감축 필요도 계산-------------
# 공급예비율 데이터 불러오기
rate = pd.read_csv('data/output/공급예비율_월별_20052026.csv')

# 공급예비율 + 전력 사용량 merge
idx_df = df.merge(
    rate,
    left_on=["연도","월"],
    right_on=["년","월"]
).copy()

idx_df = idx_df.drop(columns=["년"])

idx_df["공급위험도"] = (
    1 - idx_df["공급예비율(%)"] / 100
)

# 0~1 (상대적 피크)
idx_df["용도정규화사용률"] = (
    idx_df.groupby(['시군구','연도','월'])["전력사용율"]
            .transform(lambda x: x / x.max()).fillna(0)
)

# # 용도 가중치(데이터 기반으로?)
# # 실제로 공급을 많이 압박하는 용도가 자동으로 높은 가중치
# weights = risk_df.groupby("전력용도")["용도정규화사용률"].mean()
# weights = (weights / weights.max()).round(3)

# risk_df["용도가중치"] = risk_df["전력용도"].map(weights)

idx_df["수요감축필요도_1st"] = (
    idx_df["용도정규화사용률"]
    * idx_df["공급위험도"]
    # * risk_df["용도가중치"]
)

print('수요 감축 필요도 계산 완료')

idx_df.to_csv("data/output/kepco_final_data.csv", index=False, encoding="utf-8-sig")

print("최종 데이터 저장 완료")
print("저장위치: 'data/output/kepco_final_data.csv'")

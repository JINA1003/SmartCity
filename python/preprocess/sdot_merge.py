import pandas as pd
import openpyxl

df_district = pd.read_excel("../data/file/sdot_wether_data/서울시 도시데이터 센서(S-DoT) 환경정보 설치 위치정보.xlsx")
df_district["구"] = df_district["주소"].str.split(" ").str[1]


# df_district.sort_values("모델 시리얼(*)")
df_district.groupby("구").size()

df_gu = df_district.loc[:,["모델 시리얼(*)","구"]]
df_gu.head(2)
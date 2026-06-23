import pandas as pd

df = pd.read_csv("data/file/epsis_elec_supply_month.csv", encoding='cp949')

# 필요없는 컬러 제거
rate = df.drop(columns=['일', '설비용량(MW)','최소전력(MW)','최대전력기준일시','최소전력기준일시']).copy()

# 결측 처리
rate = rate.dropna(subset=['공급예비율(%)'])

rate.to_csv('data/output/epsis_supply_rate_final_20052026.csv', index=False)
print("저장 완료 !")

rate.head()
"""
[건물통합정보 + kepco_final_20052026.csv]
설명: (1) 건물 통합 정보 전처리 / (2) 건물 용도 매핑해서 건물별로 kepco_final_20052026 매칭<br>
필요 데이터: data/file/AL_D010_11_20260609 / data/output/kepco_final_20052026.csv
생성 데이터: (1) data/output/seoul_buildings.geojson / (2) data/output/kepco_mapping_final_20052026.csv"

[필요 데이터 출처: GIS건물통합정보]
https://www.vworld.kr/dtmk/dtmk_ntads_s002.do?svcCde=NA&dsId=18



"""
import geopandas as gpd
import pandas as pd
from shapely.validation import make_valid
from pathlib import Path

print('전처리 시작')

# =========================
# 0. 경로 설정
# =========================
building_path = "data/file/AL_D010_11_20260609.shp"
building_output_path = "data/output/seoul_buildings.geojson"


# =========================
# 1. 데이터 불러오기
# =========================
gdf_raw = gpd.read_file(building_path, encoding="cp949")

# =========================
# 2. 서울 건물 데이터 전처리
# =========================
# 서울 필터
gdf_seoul = gdf_raw[gdf_raw['A4'].str.startswith('서울')].copy()

# 좌표계 변환
gdf_seoul = gdf_seoul.to_crs('EPSG:4326')

# 결측 처리
gdf_seoul = gdf_seoul.dropna(subset=['A9']) # 건물용도

# 시군구 추출
gdf_seoul["sigungu"] = gdf_seoul["A4"].str.extract(r"([^ ]+구)")

# =========================
# 3. A16 높이 처리
# =========================
# 1. A16 실측 높이를 읽기, 없으면 0으로
gdf_seoul['height'] = gdf_seoul['A16'].fillna(0).astype(float)

# 2. 높이가 0이하인 건물 -> 층수 * 3m 로 대체
mask = gdf_seoul['height'] <= 0
gdf_seoul.loc[mask, 'height'] = gdf_seoul.loc[mask, 'A26'] * 3

# 3. 그래도 남은 null 이나 0이면 -> 최소 3m 보장
gdf_seoul['height'] = gdf_seoul['height'].fillna(3).clip(lower=3)

# =========================
# 4. geometry 유효성 처리
# =========================
# 1. 꼭짓점이 3개 미만 폴리곤 제거 (Unity 로 가져왔을때 건물로 표출 어려움)
from shapely.validation import make_valid
gdf_seoul['geometry'] = gdf_seoul['geometry'].apply(make_valid)

# 2. 꼭짓점이 부족한 geometry는 제거
gdf_seoul = gdf_seoul[gdf_seoul.geometry.apply(
    lambda g: all(
        len(list(p.exterior.coords[:-1])) >= 3
        for p in (g.geoms if g.geom_type == 'MultiPolygon' else [g])
    )
)]


# =========================
# 5. 문자열 정리
# =========================
gdf_seoul["sigungu"] = gdf_seoul["sigungu"].astype(str).str.strip()
gdf_seoul = gdf_seoul.rename(columns={'A9':'building_type'})

print(f'완료: {len(gdf_seoul)}개 건물')
print(f'height 0 개수: {(gdf_seoul["height"] <= 0).sum()}') #0이어야 정상

# 파일 저장
gdf_seoul.to_file(
    building_output_path,
    driver="GeoJSON",
    encoding="utf-8"
)

print(f'빌딩 데이터 저장 완료: data/output/seoul_buildings.geojson')




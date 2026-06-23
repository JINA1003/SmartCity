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

# =========================
# 0. 경로 설정
# =========================
building_path = "data/file/AL_D010_11_20260609.shp"
kepco_path = "data/output/kepco_final_20052026.csv"
building_output_path = "data/output/seoul_buildings.geojson"
kepco_mapping_output_path = "data/output/kepco_mapping_final_20052026.csv"

# =========================
# 1. 데이터 불러오기
# =========================
gdf_raw = gpd.read_file(building_path, encoding="cp949")
kepco_df = pd.read_csv(kepco_path)

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

# 건물용도 매핑 (전력용도 -> 건물타입)
mapping = {

    # ======================
    # 🏠 주택용
    # ======================
    "단독주택": "주택용",
    "공동주택": "주택용",
    "다가구주택": "주택용",

    # ======================
    # 🏢 일반용 (상업/서비스/공공)
    # ======================
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

    # ======================
    # 🎓 교육용
    # ======================
    "교육연구시설": "교육용",

    # ======================
    # 🏭 산업용
    # ======================
    "공장": "산업용",
    "창고시설": "산업용",
    "자동차관련시설": "산업용",
    "위험물저장및처리시설": "산업용",
    "동.식물 관련시설": "산업용",
    "발전시설": "산업용",
    "분뇨.쓰레기처리시설": "산업용",
    "묘지관련시설": "산업용",
    "가설건축물": "산업용",

    # ======================
    # 🌾 농사용 (거의 제한적)
    # ======================
    # 실제 데이터에 거의 없음, 동식물 관련시설 일부 포함 가능
}

gdf_seoul["usage_type"] = gdf_seoul["A9"].map(mapping)

# 매핑 안 된 건물 제거
gdf_seoul = gdf_seoul.dropna(subset=["usage_type"]).copy()

# =========================
# 5. 문자열 정리
# =========================
gdf_seoul["sigungu"] = gdf_seoul["sigungu"].astype(str).str.strip()
gdf_seoul["usage_type"] = gdf_seoul["usage_type"].astype(str).str.strip()

kepco_df["sigungu"] = kepco_df["sigungu"].astype(str).str.strip()
kepco_df["usage_type"] = kepco_df["usage_type"].astype(str).str.strip()

# key 생성
gdf_seoul['key'] = gdf_seoul['sigungu'] + "_" +gdf_seoul['usage_type']

print(f'완료: {len(gdf_seoul)}개 건물')
print(f'전력용도 null 개수: {gdf_seoul["usage_type"].isnull().sum()}') #null이면 mapping 안된 것
print(f'height 0 개수: {(gdf_seoul["height"] <= 0).sum()}') #0이어야 정상

# 파일 저장
gdf_seoul.to_file(
    building_output_path,
    driver="GeoJSON",
    encoding="utf-8"
)

print(f'빌딩 데이터 저장 완료: data/output/seoul_buildings.geojson')

# 건축용도 - 전력용도 매핑
mapping_df = pd.DataFrame(
    [(v, k) for k, v in mapping.items()],
    columns=["usage_type", "building_type"]
)

# 전력 데이터에 건축용도 컬럼 추가
kepco_mapped = kepco_df.merge(
    mapping_df,
    on="usage_type",
    how="left"
)

# =========================
# 1. building_type별 단전 가중치 설정
# 값이 클수록 단전 우선순위 높음
# =========================
weight_map = {
    # 국가 핵심시설 (단전 제외)
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
# 2. weight 컬럼 추가
# =========================
kepco_mapped["weight"] = kepco_mapped["building_type"].map(weight_map)

# 매핑 안 된 건축용도는 기본값 부여
kepco_mapped["weight"] = kepco_mapped["weight"].fillna(0)

# =========================
# 3. 최종 단전 우선순위 점수 계산
# =========================
kepco_mapped["reduction_need_score"] = (
    kepco_mapped["reduction_need_draft"] * kepco_mapped["weight"] * 100
)


# 필요 데이터만 필터
kepco_mapped_final = kepco_mapped[['year', 'month','sigungu', 'usage_type', 'building_type', 
                                   'power_mwh', 'usage_ratio', 'capacity_mw', 'peak_mw', 
                                   'reserve_mw', 'reserve_rate', 'reduction_need_score']]

# 파일 저장
kepco_mapped_final.to_csv(
    kepco_mapping_output_path,
    index=False,
    encoding="utf-8-sig"
)

print(f'kepco mapping 데이터 저장 완료: data/output/kepco_mapping_final_20052026.csv')


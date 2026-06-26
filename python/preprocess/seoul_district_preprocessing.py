"""
서울 구 경계 shp -> geojson 변환

[필요 파일]: data/file/shp/bnd_sigungu_11_2025_2Q.* 
=> 구글 드라이브 seoul_district_shp 폴더 파일들 다운 후 이 폴더에 저장

[출력 파일]: data/output/seoul_district.geojson
"""

import geopandas as gpd
from pathlib import Path

# =====================
# 경로 설정
# =====================
shp_path = "data/file/shp/bnd_sigungu_11_2025_2Q.shp"
geojson_path = "data/output/seoul_district.geojson"

# =====================
# shp 읽기
# =====================
gdf = gpd.read_file(shp_path)

# =====================
# 좌표계 변환
# =====================
gdf = gdf.to_crs("EPSG:4326")

# =====================
# 구 이름 공백 제거
# =====================
if "SIGUNGU_NM" in gdf.columns:
    gdf["SIGUNGU_NM"] = gdf["SIGUNGU_NM"].astype(str).str.strip()

# =====================
# centroid 계산
# =====================
# 정확한 중심 계산을 위해 투영좌표계로 변환 후 계산
gdf_proj = gdf.to_crs("EPSG:5179")

centroid_proj = gdf_proj.geometry.centroid

# 다시 EPSG:4326으로 변환
centroid_wgs84 = (
    gpd.GeoSeries(
        centroid_proj,
        crs="EPSG:5179"
    )
    .to_crs("EPSG:4326")
)

# GeoJSON properties에 저장될 컬럼
gdf["center_lon"] = centroid_wgs84.x
gdf["center_lat"] = centroid_wgs84.y

# =====================
# representative point도 추가
# polygon 내부에 반드시 위치하는 대표점
# =====================
rep_proj = gdf_proj.geometry.representative_point()

rep_wgs84 = (
    gpd.GeoSeries(
        rep_proj,
        crs="EPSG:5179"
    )
    .to_crs("EPSG:4326")
)

gdf["rep_lon"] = rep_wgs84.x
gdf["rep_lat"] = rep_wgs84.y

# =====================
# GeoJSON 저장
# =====================
gdf.to_file(
    geojson_path,
    driver="GeoJSON",
    encoding="utf-8"
)

print("좌표계:", gdf.crs)
print("GeoJSON 저장 완료:", geojson_path)
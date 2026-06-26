using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;

public class BuildingManager : MonoBehaviour
{
    [Header("설정")]
    public string geoJsonFileName = "seoul_buildings.geojson";
    public Material buildingMaterial;

    [Header("Cesium 설정")]
    public CesiumGeoreference cesiumGeoreference;

    private async void Start()
    {
        await LoadAndSpawnDistrictsAsync();
    }

    public async Task LoadAndSpawnDistrictsAsync()
    {
        DataParser parser = new DataParser();

        Debug.Log("[BuildingManager] GeoJSON 파싱 시작...");
        List<BuildingData> allBuildings = await parser.ParseGeoJson(geoJsonFileName);

        // 파싱된 전체 건물을 DistrictType(구)을 기준으로 그룹화(Group)합니다.
        var buildingsByDistrict = allBuildings.GroupBy(b => b.districtType);

        foreach (var districtGroup in buildingsByDistrict)
        {
            DistrictType currentDistrict = districtGroup.Key;
            List<BuildingObject> districtBuildingObjects = new List<BuildingObject>();

            int count = 0;
            // 해당 구에 속한 건물들을 하나씩 생성
            foreach (BuildingData bData in districtGroup)   
            {
                GameObject spawnedObj = SpawnBuilding(bData);
                count++;
                if (count % 50 == 0) await Task.Yield();
                if (spawnedObj != null)
                {
                    // BuildingObject 컴포넌트를 붙이고 데이터 초기화
                    BuildingObject bObj = spawnedObj.AddComponent<BuildingObject>();
                    bObj.data = bData;

                    districtBuildingObjects.Add(bObj);
                }
            }

            // 구별로 생성이 끝났다면 DistrictManager에게 리스트 전달
            if (DistrictManager.Instance != null)
            {
                DistrictManager.Instance.UpdateDistrictBuildings(currentDistrict, districtBuildingObjects);
            }
            else
            {
                Debug.LogWarning("[BuildingManager] DistrictManager 인스턴스를 찾을 수 없습니다.");
            }
        }

        Debug.Log("[BuildingManager] 모든 구 건물 스폰 및 등록 완료");
    }

    public GameObject SpawnBuilding(BuildingData data)
    {
        Mesh buildingMesh = MeshBuilder.BuildPolygonMesh(data.polygon, data.height);

        if (buildingMesh == null)
        {
            return null;
        }

        GameObject buildingObj = new GameObject($"Building_{data.id}");

        MeshFilter meshFilter = buildingObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = buildingObj.AddComponent<MeshRenderer>();

        meshFilter.mesh = buildingMesh;

        if (buildingMaterial != null)
        {
            meshRenderer.material = buildingMaterial;
        }


        // 1. 하이어라키 원점 이동: 떨림(Jittering) 방지를 위해 반드시 Georeference의 자식으로 둡니다.
        if (cesiumGeoreference != null)
        {
            buildingObj.transform.SetParent(cesiumGeoreference.transform, false);
        }
        else
        {
            buildingObj.transform.SetParent(this.transform, false);
            Debug.LogWarning("CesiumGeoreference가 할당되지 않았습니다!");
        }

        // 3. 지구상에 똑바로 세우기: CesiumGlobeAnchor 부착 및 위경도 데이터 입력
        CesiumGlobeAnchor anchor = buildingObj.AddComponent<CesiumGlobeAnchor>();

        // DataParser에서 평균 낸 중심 위도/경도를 삽입하여 지구 표면에 위치시킵니다.
        // 고도(Height)는 일단 0 또는 terrainAltitude로 둡니다.
        double altitude = data.terrainAltitude > 0 ? data.terrainAltitude : 0;
        anchor.longitudeLatitudeHeight = new double3(data.lon, data.lat, altitude);

        return buildingObj;
    }
}
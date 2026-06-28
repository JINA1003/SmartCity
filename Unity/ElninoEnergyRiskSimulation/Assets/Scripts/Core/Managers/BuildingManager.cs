using CesiumForUnity;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    [Header("설정")]
    public string geoJsonFileName = "seoul_buildings.geojson";
    public Material buildingMaterial;

    [Header("Cesium 설정")]
    public CesiumGeoreference cesiumGeoreference;
    public Cesium3DTileset terrainTileset;

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

                    StartCoroutine(PlaceBuilding(spawnedObj, bData.lon, bData.lat));
                }
            }

            // 구별로 생성이 끝났다면 DistrictManager에게 리스트 전달
            if (DistrictManager.Instance != null)
            {
                //DistrictManager.Instance.UpdateDistrictBuildings(currentDistrict, districtBuildingObjects);
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

        return buildingObj;
    }

    IEnumerator PlaceBuilding(GameObject obj, double lon, double lat)
    {
        yield return null;
        if (obj == null) yield break;

        // 1. 지구상에 똑바로 세우기 위한 앵커 추가 및 활성화 대기
        CesiumGlobeAnchor anchor = obj.AddComponent<CesiumGlobeAnchor>();
        yield return new WaitUntil(() => anchor != null && anchor.isActiveAndEnabled);

        double terrainHeight = 0;

        // 2. Cesium 3D Tileset 지형으로부터 해당 위경도의 최정밀 고도(Heigt) 샘플링 대기
        if (terrainTileset != null)
        {
            double3[] positions = new double3[] { new double3(lon, lat, 0) };
            System.Threading.Tasks.Task<CesiumSampleHeightResult> task
                = terrainTileset.SampleHeightMostDetailed(positions);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.Result.sampleSuccess[0])
                terrainHeight = task.Result.longitudeLatitudeHeightPositions[0].z;
        }

        // 3. 앵커 좌표에 정확한 해발고도 반영
        anchor.longitudeLatitudeHeight = new double3(lon, lat, terrainHeight);

        // 4. 컴포넌트 구조 체크 후 데이터 동기화
        BuildingObject bObj = obj.GetComponent<BuildingObject>();
        if (bObj != null && bObj.data != null)
        {
            // bObj.data.terrainAltitude = (float)terrainHeight;
        }

        yield return null;

        // 5. 고도 배치가 완전히 끝나면 숨겨두었던 메쉬를 화면에 렌더링
        if (obj != null)
            obj.GetComponent<MeshRenderer>().enabled = true;
    }
}
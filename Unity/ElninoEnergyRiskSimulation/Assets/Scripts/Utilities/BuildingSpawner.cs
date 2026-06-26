using UnityEngine;

public class BuildingSpawner : MonoBehaviour
{
    [Header("건물에 적용할 재질(Material)")]
    public Material buildingMaterial;

    /// <summary>
    /// BuildingData를 기반으로 유니티 씬에 건물 GameObject를 생성합니다.
    /// </summary>
    /// <param name="data">건물 정보가 담긴 BuildingData</param>
    public GameObject SpawnBuilding(BuildingData data)
    {
        // 1. MeshBuilder를 사용해 3D 메쉬 데이터 생성
        // data.polygon: 로컬 XZ 좌표 목록, data.height: 계산된 건물 높이
        Mesh buildingMesh = MeshBuilder.BuildPolygonMesh(data.polygon, data.height);

        // 2. 예외 처리 (매우 중요)
        // EarClipping 실패 시 null을 반환하므로 반드시 체크해야 합니다.
        if (buildingMesh == null)
        {
            Debug.LogWarning($"[BuildingSpawner] 건물 메쉬 생성 실패 (ID: {data.id})");
            return null;
        }

        // 3. 유니티 게임 오브젝트 생성
        // 오브젝트 이름에 건물 ID나 이름을 넣어두면 관리가 편합니다.
        GameObject buildingObj = new GameObject($"Building_{data.id}");

        // 4. 렌더링을 위한 컴포넌트 추가
        MeshFilter meshFilter = buildingObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = buildingObj.AddComponent<MeshRenderer>();

        // 5. 컴포넌트에 데이터 할당
        meshFilter.mesh = buildingMesh;

        // 머티리얼을 할당하지 않으면 유니티에서 기본적으로 분홍색(Magenta)으로 보입니다.
        if (buildingMaterial != null)
        {
            meshRenderer.material = buildingMaterial;
        }

        // 6. 건물의 위치(Position) 지정
        // polygon 데이터는 이미 로컬 XZ 좌표로 변환되어 있으므로 중심축 기준으로 높이만 조절합니다.
        // 지형 해발고도(terrainAltitude)를 적용하여 건물이 땅 위에 올라오도록 합니다.
        buildingObj.transform.position = new Vector3(0, data.terrainAltitude, 0);

        // (선택) 깔끔한 씬 관리를 위해 생성된 건물을 이 스크립트의 자식으로 설정
        buildingObj.transform.SetParent(this.transform);

        return buildingObj;
    }
}
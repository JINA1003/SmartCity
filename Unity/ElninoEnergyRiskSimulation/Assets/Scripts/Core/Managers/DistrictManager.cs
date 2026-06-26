using System.Collections.Generic;
using UnityEngine;

public class DistrictManager : MonoBehaviour
{
    public static DistrictManager Instance { get; private set; }

    public Dictionary<string, DistrictObject> districtObjects = new Dictionary<string, DistrictObject>();

    [SerializeField] private DataManager dataManager;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        if (dataManager != null)
        {
            dataManager.OnDistrictDataUpdated += HandleDistrictDataUpdated;
        }
        else
        {
            Debug.LogWarning("[PowerGridManager] dataManager가 존재하지 않습니다.");
        }
    }
    private void OnDisable()
    {
        if (dataManager != null)
        {
            dataManager.OnDistrictDataUpdated -= HandleDistrictDataUpdated;
        }
    }

    // DataManager에서 이벤트가 발생할 때마다 실행되는 콜백 함수
    private void HandleDistrictDataUpdated(DistrictData newData)
    {
        // 구 객체를 가져오거나 생성한 후 API 데이터를 갱신합니다.
        DistrictObject district = GetOrCreateDistrict(newData.districtName);
        district.data = newData;

        Debug.Log($"[DistrictManager] [{newData.districtName}] 전력 데이터 업데이트 완료.");

        // TODO: 이벤트 함수 만들어서 갱신됐다고 Invoke합니다.
    }

    // 해당 구(District) 객체가 존재하면 반환하고, 없으면 새로 생성하여 딕셔너리에 등록하는 헬퍼 메서드입니다.
    private DistrictObject GetOrCreateDistrict(string districtName)
    {
        if (!districtObjects.ContainsKey(districtName))
        {
            // 빈 껍데기 형태의 DistrictObject를 우선 생성하여 등록합니다.
            // (참고: DistrictObject가 MonoBehaviour이므로, 프로젝트 구조에 따라 
            //  Instantiate나 AddComponent 구조로 변경해야 할 수 있습니다.)
            districtObjects[districtName] = new DistrictObject
            {
                data = new DistrictData
                {
                    districtName = districtName,
                    // 이곳으로 이동해야 합니다.
                    buildingReductionScores = new Dictionary<BuildingType, float>()
                },
                buildings = new List<BuildingObject>(), // GameObject에서 BuildingObject 리스트로 변경
                IsShutDown = false
            };
        }
        return districtObjects[districtName];
    }

    // 건물 생성 로직에서 호출할 메서드
    public void UpdateDistrictBuildings(string districtName, List<BuildingObject> buildings)
    {
        // 구 객체를 가져오거나 생성한 후 건물 리스트를 연결합니다.
        DistrictObject district = GetOrCreateDistrict(districtName);
        district.buildings = buildings;

        Debug.Log($"[DistrictManager] [{districtName}] 건물 오브젝트 배치 완료. (총 {buildings.Count}개 건물)");
    }
}

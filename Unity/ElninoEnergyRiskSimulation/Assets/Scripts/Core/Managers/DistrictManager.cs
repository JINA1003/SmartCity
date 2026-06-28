using System;
using System.Collections.Generic;
using UnityEngine;

public class DistrictManager : MonoBehaviour
{
    public static DistrictManager Instance { get; private set; }

    public Dictionary<DistrictType, DistrictObject> districtObjects = new Dictionary<DistrictType, DistrictObject>();

    [SerializeField] private DataManager dataManager;

    public event Action<Dictionary<DistrictType, DistrictData>> OnAllDistrictsDataUpdated;

    public event Action<Dictionary<DistrictType, DistrictObject>> OnAllDistrictsObjectsUpdated;

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
            dataManager.OnAllDistrictsParsed += HandleAllDistrictsParsed;
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
            dataManager.OnAllDistrictsParsed -= HandleAllDistrictsParsed;
        }
    }

    // DataManager에서 이벤트가 발생할 때마다 실행되는 콜백 함수
    private void HandleDistrictDataUpdated(DistrictData newData)
    {
        // 구 객체를 가져오거나 생성한 후 API 데이터를 갱신합니다.
        if (districtObjects.TryGetValue(newData.districtType, out DistrictObject district))
        {
            // 이미 DistrictObject가 생성된 경우
            district.data = newData;
        }
        else
        {
            // 아직 생성 전이면 임시 데이터만 보관
            districtObjects[newData.districtType] = new DistrictObject
            {
                data = newData,
                buildings = new List<BuildingData>(),
                IsShutDown = false
            };
        }

        Debug.Log($"[DistrictManager] [{newData.districtType}] 전력 데이터 업데이트 완료.");

        // TODO: 이벤트 함수 만들어서 갱신됐다고 Invoke합니다.
    }

    private void HandleAllDistrictsParsed()
    {
        Debug.Log("[DistrictManager] 모든 구 데이터 세팅 완료. 구독자들에게 전달!");

        // 1. UI용 순수 데이터 딕셔너리 추출
        Dictionary<DistrictType, DistrictData> pureDataDict = new Dictionary<DistrictType, DistrictData>();
        foreach (var kvp in districtObjects)
        {
            pureDataDict[kvp.Key] = kvp.Value.data;
        }

        // 2. 이벤트 각각 발생!
        OnAllDistrictsDataUpdated?.Invoke(pureDataDict);
        OnAllDistrictsObjectsUpdated?.Invoke(districtObjects);
    }

    public void RegisterDistrictObject(DistrictObject districtObject)
    {
        if (districtObject?.data == null) return;

        DistrictType districtType = districtObject.data.districtType;

        if (districtObjects.TryGetValue(districtType, out DistrictObject existing))
        {
            // API 데이터가 먼저 들어와 있던 경우
            districtObject.data = existing.data;
            districtObject.buildings = existing.buildings;
        }

        districtObjects[districtType] = districtObject;

        Debug.Log($"[DistrictManager] [{districtType}] DistrictObject 등록 완료.");
    }
}

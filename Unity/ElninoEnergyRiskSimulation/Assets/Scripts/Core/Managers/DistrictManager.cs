using System.Collections.Generic;
using UnityEngine;

public class DistrictManager : MonoBehaviour
{
    public static DistrictManager Instance { get; private set; }

    public Dictionary<string, DistrictObject> districtObjects = new Dictionary<string, DistrictObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddDistrictObjects(string districtName, List<GameObject> buildings)
    {
        // 해당 districtName이 이미 존재하는지 확인하고, 존재하지 않으면 새로 생성
        if (!districtObjects.ContainsKey(districtName))
        {
            districtObjects[districtName] = new DistrictObject
            {
                data = new DistrictData
                {
                    districtName = districtName,
                    temperature = 0f, // 초기값 설정 필요
                    totalPowerUsage = 0.0, // 초기값 설정 필요
                    typePowerUsage = new Dictionary<string, float>() // 초기값 설정 필요
                },
                buildings = buildings,
                buildingReducationScores = new Dictionary<BuildingType, float>(), // 초기값 설정 필요
                IsShutDown = false
            };
        }
    }
}

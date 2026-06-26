using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

[System.Serializable]
public class ServerPredictResponse
{
    public PowerGridData powerGrid;
    public List<DistrictData> districts;
}

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    [Header("API 연동")]
    public ApiClient apiClient;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // ApiClient의 성공 이벤트에 파싱 메서드 연결
        if (apiClient != null)
        {
             //apiClient.OnPredictSuccess.AddListener(OnPredictDataParsed);
        }
    }

    /// <summary>
    /// UI에서 날짜와 ONI 값을 선택했을 때 호출하는 진입점
    /// </summary>
    public void LoadDataForDate(int year, int month, float oni)
    {
        apiClient.FetchPredict(year, month, oni);
    }

    /// <summary>
    /// API 통신 성공 시 JSON을 객체로 변환하여 각 매니저로 분배
    /// </summary>
    private void OnPredictDataParsed(string jsonResponse)
    {
        // Newtonsoft.Json을 사용하여 JSON 역직렬화
        ServerPredictResponse responseData = JsonConvert.DeserializeObject<ServerPredictResponse>(jsonResponse);

        if (responseData != null)
        {
            // 1. 전력망(PowerGrid) 데이터 갱신
            if (responseData.powerGrid != null)
            {
                // PowerGridManager.Instance.UpdateGridData(responseData.powerGrid);
            }

            // 2. 구별(District) 데이터 갱신
            if (responseData.districts != null)
            {
                foreach (var districtData in responseData.districts)
                {
                    // DistrictManager.Instance.UpdateDistrictData(districtData);
                }
            }
        }
    }
}

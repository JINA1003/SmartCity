using UnityEngine;

public class PowerGridManager : MonoBehaviour
{
    public static PowerGridManager Instance { get; private set; }

    PowerGridData data;

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
            dataManager.OnPowerDataUpdated += HandlePowerDataUpdated;
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
            dataManager.OnPowerDataUpdated -= HandlePowerDataUpdated;
        }
    }

    private void HandlePowerDataUpdated(PowerGridData newData)
    {
        this.data = newData;
        Debug.Log($"전체 전력망 데이터 갱신 완료 (예비율 등 UI 업데이트 시점)");
    }

    void AddData(PowerGridData data)
    {
        this.data = data;
    }
}

using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("패널 UI")]
    public InfoPanelUI infoPanel;
    public GuEnergyPanelUI guEnergyPanel;

    [Header("데이터")]
    [SerializeField] private DataManager dataManager;

    private readonly Dictionary<DistrictType, DistrictData> districtDataMap = new();

    // ================================
    // 테스트용
    // API 데이터가 Unity까지 들어오는지 확인하기 위해,
    // 첫 로딩 때 종로구 패널을 자동으로 한 번 띄웁니다.
    // 실제 사용 시에는 이 변수와 HandleDistrictDataUpdated 안의 테스트용 if 블록을 제거하거나 주석 처리하세요.
    // ================================
    private bool didShowInitialDistrictForTest = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (dataManager == null)
        {
            Debug.LogWarning("[UIManager] DataManager가 연결되지 않았습니다.");
            return;
        }

        dataManager.OnPowerDataUpdated += HandlePowerDataUpdated;
        dataManager.OnDistrictDataUpdated += HandleDistrictDataUpdated;
    }

    private void OnDisable()
    {
        if (dataManager == null) return;

        dataManager.OnPowerDataUpdated -= HandlePowerDataUpdated;
        dataManager.OnDistrictDataUpdated -= HandleDistrictDataUpdated;
    }

    private void ResolveReferences()
    {
        if (dataManager == null)
            dataManager = FindSceneObject<DataManager>();

        if (infoPanel == null)
            infoPanel = FindSceneObject<InfoPanelUI>();

        if (guEnergyPanel == null)
            guEnergyPanel = FindSceneObject<GuEnergyPanelUI>();
    }

    private static T FindSceneObject<T>() where T : Component
    {
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded)
                return component;
        }

        return null;
    }

    private void HandlePowerDataUpdated(PowerGridData data)
    {
        // ================================
        // 테스트용
        // 인포 패널 데이터가 API에서 들어왔는지 Console에서 확인합니다.
        // 실제 사용 시에는 Debug.Log만 제거해도 됩니다.
        // ================================
        Debug.Log($"[UIManager][TEST] 인포 데이터 수신: {data.year}/{data.month}, ONI={data.oni}, 상태={data.oniStatus}, 경보={data.riskLabel}");

        // ================================
        // 실제 사용
        // API에서 받은 연월, 경보 단계, ONI 상태, ONI 값을 인포 패널에 표시합니다.
        // ================================
        SetInfoPanel(
            $"{data.year}년 {data.month}월",
            data.riskLabel,
            data.oniStatus,
            data.oni.ToString("0.00")
        );
    }

    private void HandleDistrictDataUpdated(DistrictData data)
    {
        // ================================
        // 실제 사용
        // API에서 받은 구별 데이터를 Dictionary에 저장합니다.
        // 이후 사용자가 구를 클릭하면 ShowGuEnergyPanel(districtType)에서 꺼내 씁니다.
        // ================================
        districtDataMap[data.districtType] = data;

        // ================================
        // 테스트용
        // 구 데이터가 API에서 들어왔는지 Console에서 확인합니다.
        // 실제 사용 시에는 Debug.Log만 제거해도 됩니다.
        // ================================
        Debug.Log($"[UIManager][TEST] 구 데이터 수신: {data.districtType}, 총 사용량={data.totalPowerUsage}");

        // ================================
        // 테스트용
        // 시작 위치가 종로구라서, 종로구 데이터가 들어오면 패널을 자동 표시합니다.
        // 실제 사용 시에는 이 if 블록 전체를 제거하거나 주석 처리하세요.
        // ================================
        if (!didShowInitialDistrictForTest && data.districtType == DistrictType.JONGNO)
        {
            didShowInitialDistrictForTest = true;
            ShowGuEnergyPanel(DistrictType.JONGNO);
            Debug.Log("[UIManager][TEST] 종로구 패널 자동 표시");
        }
    }

    public void ShowGuEnergyPanel(DistrictType districtType)
    {
        // ================================
        // 실제 사용
        // 구 클릭 시 호출합니다.
        // 예: UIManager.Instance.ShowGuEnergyPanel(DistrictType.JONGNO);
        // ================================
        if (!districtDataMap.TryGetValue(districtType, out DistrictData data))
        {
            Debug.LogWarning($"[UIManager] {districtType} 데이터가 아직 없습니다.");
            return;
        }

        double seoulTotal = 0;
        foreach (DistrictData district in districtDataMap.Values)
            seoulTotal += district.totalPowerUsage;

        string usagePercentText = seoulTotal > 0
            ? $"{data.totalPowerUsage / seoulTotal * 100.0:0.0}%"
            : "-";

        ShowGuEnergyPanel(
            GetDistrictKoreanName(data.districtType),
            "-",
            "-",
            $"{data.totalPowerUsage:0.0} MWh",
            usagePercentText,
            GetUsageText(data, "주택용"),
            GetUsageText(data, "일반용"),
            GetUsageText(data, "교육용"),
            GetUsageText(data, "산업용"),
            GetUsageText(data, "농사용"),
            GetUsageText(data, "가로등"),
            GetUsageText(data, "심야")
        );
    }

    private string GetUsageText(DistrictData data, string usageName)
    {
        if (data.typePowerUsage != null &&
            data.typePowerUsage.TryGetValue(usageName, out float value))
        {
            return $"{value:0.0} MWh";
        }

        return "-";
    }

    public void SetInfoPanel(string dateText, string emergencyStage, string oniType, string oniValueText)
    {
        if (infoPanel != null)
            infoPanel.SetInfo(dateText, emergencyStage, oniType, oniValueText);
    }

    public void ShowGuEnergyPanel(
        string districtName,
        string usageChangeAmountText,
        string usageChangeRateText,
        string totalUsageText,
        string usagePercentText,
        string houseText,
        string generalText,
        string educationText,
        string industrialText,
        string agricultureText,
        string streetlightText,
        string midnightText
    )
    {
        if (guEnergyPanel != null)
        {
            guEnergyPanel.Show(
                districtName,
                usageChangeAmountText,
                usageChangeRateText,
                totalUsageText,
                usagePercentText,
                houseText,
                generalText,
                educationText,
                industrialText,
                agricultureText,
                streetlightText,
                midnightText
            );
        }
    }

    private string GetDistrictKoreanName(DistrictType districtType)
    {
        switch (districtType)
        {
            case DistrictType.DOBONG: return "도봉구";
            case DistrictType.DONGDAEMUN: return "동대문구";
            case DistrictType.DONGJAK: return "동작구";
            case DistrictType.EUNPYEONG: return "은평구";
            case DistrictType.GANGBUK: return "강북구";
            case DistrictType.GANGDONG: return "강동구";
            case DistrictType.GANGNAM: return "강남구";
            case DistrictType.GANGSEO: return "강서구";
            case DistrictType.GEUMCHEON: return "금천구";
            case DistrictType.GURO: return "구로구";
            case DistrictType.GWANAK: return "관악구";
            case DistrictType.GWANGJIN: return "광진구";
            case DistrictType.JONGNO: return "종로구";
            case DistrictType.JUNG: return "중구";
            case DistrictType.JUNGNANG: return "중랑구";
            case DistrictType.MAPO: return "마포구";
            case DistrictType.NOWON: return "노원구";
            case DistrictType.SEOCHO: return "서초구";
            case DistrictType.SEODAEMUN: return "서대문구";
            case DistrictType.SEONGBUK: return "성북구";
            case DistrictType.SEONGDONG: return "성동구";
            case DistrictType.SONGPA: return "송파구";
            case DistrictType.YANGCHEON: return "양천구";
            case DistrictType.YEONGDEUNGPO: return "영등포구";
            case DistrictType.YONGSAN: return "용산구";
            default: return districtType.ToString();
        }
    }
}

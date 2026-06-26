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
    private DistrictType selectedDistrictType;
    private bool hasSelectedDistrict;

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
        SetInfoPanel(
            $"{data.year}년 {data.month}월",
            data.riskLabel,
            data.oniStatus,
            data.oni.ToString("0.00")
        );
    }

    private void HandleDistrictDataUpdated(DistrictData data)
    {
        districtDataMap[data.districtType] = data;

        if (!hasSelectedDistrict)
        {
            SelectDistrict(data.districtType);
            return;
        }

        if (selectedDistrictType == data.districtType)
            ShowGuEnergyPanel(selectedDistrictType);
    }

    public void SelectDistrict(DistrictType districtType)
    {
        selectedDistrictType = districtType;
        hasSelectedDistrict = true;
        ShowGuEnergyPanel(districtType);
    }

    public void ShowGuEnergyPanel(DistrictType districtType)
    {
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

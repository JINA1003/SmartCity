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
    private const string DefaultDistrict = "종로구";
    private string currentDistrict = DefaultDistrict;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        ResolveInfoPanel();
        ResolveGuEnergyPanel();
    }

    private void Start()
    {
        ResolveInfoPanel();
        ResolveGuEnergyPanel();

        // 날짜 데이터가 들어오기 전에는 인포와 구 패널을 숨겨 둡니다.
        if (infoPanel != null)
            infoPanel.Hide();

        if (guEnergyPanel != null)
            guEnergyPanel.Hide();
    }

    private void OnEnable()
    {
        // 미니맵이 보내는 구 이름을 받아서, 구 패널만 갱신합니다.
        MinimapManager.OnDistrictSelected += HandleMinimapDistrictSelected;

        if (dataManager == null)
        {
            return;
        }

        dataManager.OnPowerDataUpdated += HandlePowerDataUpdated;
        dataManager.OnDistrictDataUpdated += HandleDistrictDataUpdated;
    }

    private void OnDisable()
    {
        MinimapManager.OnDistrictSelected -= HandleMinimapDistrictSelected;

        if (dataManager == null) return;

        dataManager.OnPowerDataUpdated -= HandlePowerDataUpdated;
        dataManager.OnDistrictDataUpdated -= HandleDistrictDataUpdated;
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
        // 날짜 선택으로 들어온 구별 데이터를 저장하고, 현재 선택된 구만 화면에 반영합니다.
        districtDataMap[data.districtType] = data;

        if (data.districtType == DataConverter.GetDistrictType(currentDistrict))
            BuildGuEnergyPanel();
    }

    private void HandleMinimapDistrictSelected(string districtName)
    {
        SetDistrict(districtName);
    }

    public void SetDistrict(string districtName)
    {
        currentDistrict = districtName;
        BuildGuEnergyPanel();
    }

    public void ShowGuEnergyPanel(DistrictType districtType)
    {
        currentDistrict = GetDistrictKoreanName(districtType);
        BuildGuEnergyPanel();
    }

    private void BuildGuEnergyPanel()
    {
        DistrictType districtType = DataConverter.GetDistrictType(currentDistrict);

        // 현재 날짜에 해당 구 데이터가 아직 없으면 마지막 표시 상태를 유지합니다.
        if (!districtDataMap.TryGetValue(districtType, out DistrictData data))
        {
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
        ResolveInfoPanel();

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
        ResolveGuEnergyPanel();

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

    private void ResolveInfoPanel()
    {
        if (infoPanel != null) return;

        // 인스펙터 연결이 비어 있어도 실행 중 인포 패널 컴포넌트를 찾습니다.
        infoPanel = FindFirstObjectByType<InfoPanelUI>(FindObjectsInactive.Include);
        if (infoPanel != null) return;

        GameObject panelObject = FindSceneObjectByName("Panel_Info");
        if (panelObject == null) return;

        infoPanel = panelObject.GetComponent<InfoPanelUI>();
        if (infoPanel == null)
            infoPanel = panelObject.AddComponent<InfoPanelUI>();
    }

    private void ResolveGuEnergyPanel()
    {
        if (guEnergyPanel != null) return;

        // 인스펙터 연결이 비어 있어도 실행 중 패널 컴포넌트를 찾습니다.
        guEnergyPanel = FindFirstObjectByType<GuEnergyPanelUI>(FindObjectsInactive.Include);
        if (guEnergyPanel != null) return;

        GameObject panelObject = FindSceneObjectByName("Panel_Gu_Energy");
        if (panelObject == null) return;

        guEnergyPanel = panelObject.GetComponent<GuEnergyPanelUI>();
        if (guEnergyPanel == null)
            guEnergyPanel = panelObject.AddComponent<GuEnergyPanelUI>();
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform target in transforms)
        {
            if (target.name == objectName)
                return target.gameObject;
        }

        return null;
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

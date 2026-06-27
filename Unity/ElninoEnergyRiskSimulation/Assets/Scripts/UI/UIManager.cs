using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("패널 UI")]
    public InfoPanelUI infoPanel;          // 상단 정보 패널
    public GuEnergyPanelUI guEnergyPanel;  // 구별 에너지 정보 패널

    [Header("데이터")]
    public DataManager dataManager;        // API 데이터를 받아오는 DataManager

    // 구별 데이터를 DistrictType 기준으로 저장합니다.
    private readonly Dictionary<DistrictType, DistrictData> districtDataMap =
        new Dictionary<DistrictType, DistrictData>();

    // 현재 선택된 구를 저장합니다.
    // 초기 화면에서는 무조건 종로구가 먼저 보이도록 기본값을 JONGNO로 고정합니다.
    private DistrictType selectedDistrictType = DistrictType.JONGNO;

    private void Awake()
    {
        // UIManager를 하나만 사용하기 위한 싱글톤 처리입니다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        // DataManager가 연결되지 않았으면 이벤트를 구독할 수 없습니다.
        if (dataManager == null)
        {
            Debug.LogWarning("[UIManager] dataManager가 연결되지 않았습니다.");
            return;
        }

        // 전체 전력 데이터가 갱신되면 상단 정보 패널을 갱신합니다.
        dataManager.OnPowerDataUpdated += OnPowerDataUpdated;

        // 구별 데이터가 갱신되면 구 에너지 패널을 갱신합니다.
        dataManager.OnDistrictDataUpdated += OnDistrictDataUpdated;
    }

    private void OnDisable()
    {
        // 오브젝트가 꺼질 때 이벤트 연결을 해제합니다.
        if (dataManager == null) return;

        dataManager.OnPowerDataUpdated -= OnPowerDataUpdated;
        dataManager.OnDistrictDataUpdated -= OnDistrictDataUpdated;
    }

    // 전체 전력망 데이터가 갱신되었을 때 호출됩니다.
    private void OnPowerDataUpdated(PowerGridData data)
    {
        if (infoPanel == null)
        {
            Debug.LogWarning("[UIManager] infoPanel이 연결되지 않았습니다.");
            return;
        }

        // 상단 정보 패널에 표시할 값을 준비합니다.
        string dateText = data.year + "년 " + data.month + "월";
        string emergencyStage = string.IsNullOrEmpty(data.riskLabel) ? "정상" : data.riskLabel;
        string oniType = string.IsNullOrEmpty(data.oniStatus) ? "-" : data.oniStatus;
        string oniValueText = data.oni.ToString("0.00");

        // 상단 정보 패널을 갱신합니다.
        infoPanel.SetInfoPanel(
            dateText,
            emergencyStage,
            oniType,
            oniValueText
        );
    }

    // 구별 데이터가 하나씩 갱신될 때 호출됩니다.
    private void OnDistrictDataUpdated(DistrictData data)
    {
        // DistrictType을 key로 사용해서 구별 데이터를 저장합니다.
        districtDataMap[data.districtType] = data;

        // 초기 selectedDistrictType이 JONGNO라서,
        // 종로구 데이터가 들어오는 순간 구 패널이 종로구 정보로 먼저 표시됩니다.
        // 강남구 등 다른 구가 먼저 들어와도 자동 선택하지 않습니다.
        if (selectedDistrictType == data.districtType)
        {
            SetGuEnergyPanel(selectedDistrictType);
        }
    }

    // 외부에서 DistrictType으로 구를 선택할 때 호출합니다.
    public void SelectDistrict(DistrictType districtType)
    {
        selectedDistrictType = districtType;
        SetGuEnergyPanel(districtType);
    }

    // 외부에서 한글 구 이름으로 구를 선택할 때 호출합니다.
    // 나중에 미니맵에서 "강남구", "종로구" 같은 이름을 넘기면 이 함수로 처리하면 됩니다.
    public void SelectDistrictByName(string districtName)
    {
        if (string.IsNullOrEmpty(districtName))
        {
            Debug.LogWarning("[UIManager] 선택된 구 이름이 비어 있습니다.");
            return;
        }

        DistrictType districtType = DataConverter.GetDistrictType(districtName.Trim());
        SelectDistrict(districtType);
    }

    // 선택한 구의 데이터를 찾아서 구 에너지 패널에 표시합니다.
    public void SetGuEnergyPanel(DistrictType districtType)
    {
        if (guEnergyPanel == null)
        {
            Debug.LogWarning("[UIManager] guEnergyPanel이 연결되지 않았습니다.");
            return;
        }

        if (!districtDataMap.TryGetValue(districtType, out DistrictData data))
        {
            Debug.LogWarning("[UIManager] " + districtType + " 데이터가 아직 없습니다.");
            return;
        }

        // 서울 전체 전력 사용량을 계산합니다.
        double seoulTotalUsage = GetSeoulTotalUsage();

        // 선택한 구가 서울 전체 사용량 중 몇 퍼센트인지 계산합니다.
        string usagePercentText = "-";
        if (seoulTotalUsage > 0)
        {
            usagePercentText = (data.totalPowerUsage / seoulTotalUsage * 100.0).ToString("0.0") + "%";
        }

        // DistrictType enum 값을 화면에 표시할 한글 구 이름으로 변환합니다.
        string districtName = GetDistrictName(data.districtType);

        // 구 에너지 패널에 표시할 모든 텍스트 값을 전달합니다.
        guEnergyPanel.SetGuEnergyPanel(
            districtName,
            "-",
            "-",
            data.totalPowerUsage.ToString("0.0") + " MWh",
            usagePercentText,
            GetUsageValue(data, "주택용"),
            GetUsageValue(data, "일반용"),
            GetUsageValue(data, "교육용"),
            GetUsageValue(data, "산업용"),
            GetUsageValue(data, "농사용"),
            GetUsageValue(data, "가로등"),
            GetUsageValue(data, "심야")
        );
    }

    // 현재 저장된 모든 구의 총 전력 사용량을 더합니다.
    private double GetSeoulTotalUsage()
    {
        double total = 0;

        foreach (DistrictData district in districtDataMap.Values)
        {
            total += district.totalPowerUsage;
        }

        return total;
    }

    // 용도별 전력 사용량을 화면에 표시할 문자열로 변환합니다.
    private string GetUsageValue(DistrictData data, string usageName)
    {
        if (data.typePowerUsage == null)
        {
            return "-";
        }

        if (data.typePowerUsage.TryGetValue(usageName, out float value))
        {
            return value.ToString("0.0") + " MWh";
        }

        return "-";
    }

    // DistrictType enum 값을 화면에 표시할 한글 구 이름으로 변환합니다.
    private string GetDistrictName(DistrictType districtType)
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
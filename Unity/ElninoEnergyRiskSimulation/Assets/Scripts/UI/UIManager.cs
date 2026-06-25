using UnityEngine;

public class UIManagers : MonoBehaviour
{
    public static UIManagers Instance { get; private set; }

    [Header("패널 UI")]
    public InfoPanelUI infoPanel;           // 인포패널
    public GuEnergyPanelUI guEnergyPanel;   // 구패널

    private void Awake()
    {
        // UIManagers 싱글톤 등록
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // private void Start()
    // {
    //     // 정보 패널 테스트용
    //     SetInfoPanel(
    //         "2023년 8월",
    //         "정상",
    //         "엘니뇨",
    //         "1.37"
    //     );

    //     // 구 패널 테스트용
    //     ShowGuEnergyPanel(
    //         "강남구",
    //         "508,904.9 MWh",
    //         "8.4%",
    //         "122,816.1 MWh",
    //         "342,672.4 MWh",
    //         "6,081.0 MWh",
    //         "22,251.8 MWh",
    //         "1,660.2 MWh",
    //         "2,650.7 MWh",
    //         "10,772.8 MWh"
    //     );
    // }

    public void SetInfoPanel(
        string dateText,
        string emergencyStage,
        string oniType,
        string oniValueText
    )
    {
        // 정보 패널 내용 갱신
        if (infoPanel != null)
        {
            infoPanel.SetInfo(
                dateText,
                emergencyStage,
                oniType,
                oniValueText
            );
        }
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
    // 구 에너지 패널 표시
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
}
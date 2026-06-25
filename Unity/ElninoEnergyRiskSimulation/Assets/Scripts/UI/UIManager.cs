using UnityEngine;

public class UIManagers : MonoBehaviour
{
    public static UIManagers Instance { get; private set; }

    [Header("패널 UI")]
    public InfoPanelUI infoPanel;
    public GuEnergyPanelUI guEnergyPanel;

    private void Awake()
    {
        // UIManagers 싱글톤 등록
        if (Instance != null && Instance != this)
        {
          Destroy(gameObject);
          return;
        }  Instance = this;
        
        
    }

    public void ShowsetInfoPanel(
        string dateText,
        string emergencyStage,
        Color32 emergencyColor,
        string oniType,
        string oniValueText
    )
    {
        // 정보 패널 표시
        if (infoPanel != null)
        {
        infoPanel.SetInfo(
            dateText,
            emergencyStage,
            emergencyColor,
            oniType,
            oniValueText
        );        }
    }

    public void ShowGuEnergyPanel(
        string districtName,
        string totalUsageText,
        string usagePercentText,
        string residentialText,
        string commercialText,
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
                totalUsageText,
                usagePercentText,
                residentialText,
                commercialText,
                educationText,
                industrialText,
                agricultureText,
                streetlightText,
                midnightText
            );
        }
    }
}
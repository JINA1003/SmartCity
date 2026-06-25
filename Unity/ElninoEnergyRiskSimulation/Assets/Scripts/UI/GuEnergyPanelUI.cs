using TMPro;
using UnityEngine;

public class GuEnergyPanelUI : MonoBehaviour
{
    [Header("구별 에너지 패널")]
    public GameObject Panel_Gu_Energy;

    public TMP_Text Text_District_Energy;              // 구 이름
    public TMP_Text Text_Usage_Change_Amount;          // 사용 변화량
    public TMP_Text Text_Usage_Change_Rate;            // 사용 변화율
    public TMP_Text Text_Total_Usage;                  // 전체 사용량
    public TMP_Text Text_Usage_Percent;                // 사용 비율

    public TMP_Text ResidentialMwh;     // 주택용
    public TMP_Text GeneralMwh;         // 일반용
    public TMP_Text EducationMwh;       // 교육용
    public TMP_Text IndustrialMwh;      // 산업용
    public TMP_Text AgricultureMwh;     // 농사용
    public TMP_Text StreetlightMwh;     // 가로등
    public TMP_Text MidnightMwh;        // 심야

    private string cachedPanelKey;  
    public void Show(
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
        // 구 패널 열기 - 미니맵에서 구 클릭 시 실행
        if (Panel_Gu_Energy != null)
        {
            Panel_Gu_Energy.SetActive(true);
        }

        // 기본 정보 표시
        Text_District_Energy.text = $"{districtName} 에너지";
        Text_Usage_Change_Amount.text = usageChangeAmountText;
        Text_Usage_Change_Rate.text = usageChangeRateText;
        Text_Total_Usage.text = totalUsageText;
        Text_Usage_Percent.text = usagePercentText;

        // 용도별 사용량 표시
        ResidentialMwh.text = houseText;
        GeneralMwh.text = generalText;
        EducationMwh.text = educationText;
        IndustrialMwh.text = industrialText;
        AgricultureMwh.text = agricultureText;
        StreetlightMwh.text = streetlightText;
        MidnightMwh.text = midnightText;
    }

    public void Hide()
    {
        // 구 패널 숨기기
        if (Panel_Gu_Energy != null)
        {
            Panel_Gu_Energy.SetActive(false);
        }
    }
}
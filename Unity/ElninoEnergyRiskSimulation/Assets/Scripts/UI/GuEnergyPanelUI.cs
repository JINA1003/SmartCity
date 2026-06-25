using TMPro;
using UnityEngine;

public class GuEnergyPanelUI : MonoBehaviour
{
    [Header("구별 에너지 패널")]
    public GameObject Panel_Gu_Energy;

    public TMP_Text Text_District_Energy;              // 구 이름
    public TMP_Text Text_Energy_Consumption_Value;     // 전체 사용량
    public TMP_Text Text_Energy_Consumption_Value2;    // 사용량 비율

    public TMP_Text ResidentialMwh;     // 주택
    public TMP_Text CommercialMwh;      // 상업
    public TMP_Text EducationMwh;       // 교육
    public TMP_Text IndustrialMwh;      // 산업
    public TMP_Text AgricultureMwh;     // 농업
    public TMP_Text StreetlightMwh;     // 가로등
    public TMP_Text MidnightMwh;        // 심야

    public void Show(
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
        // 구 패널 열기
        if (Panel_Gu_Energy != null)
        {
            Panel_Gu_Energy.SetActive(true);
        }

        // 기본 정보 표시
        Text_District_Energy.text = districtName;
        Text_Energy_Consumption_Value.text = totalUsageText;
        Text_Energy_Consumption_Value2.text = usagePercentText;

        // 용도별 사용량 표시
        ResidentialMwh.text = residentialText;
        CommercialMwh.text = commercialText;
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
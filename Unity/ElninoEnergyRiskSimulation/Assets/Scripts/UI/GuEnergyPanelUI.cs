using TMPro;
using UnityEngine;

public class GuEnergyPanelUI : MonoBehaviour
{
    [Header("구 에너지 패널")]
    public GameObject Panel_Gu_Energy;      // 구 에너지 패널 전체 오브젝트

    [Header("구 기본 정보")]
    public TMP_Text Text_District_Energy;       // 선택된 구 이름
    public TMP_Text Text_Usage_Change_Amount;   // 전력 사용 변화량
    public TMP_Text Text_Usage_Change_Rate;     // 전력 사용 변화율
    public TMP_Text Text_Total_Usage;           // 구 전체 전력 사용량
    public TMP_Text Text_Usage_Percent;         // 서울 전체 대비 사용 비율

    [Header("용도별 전력 사용량")]
    public TMP_Text ResidentialMwh;     // 주택용
    public TMP_Text GeneralMwh;         // 일반용
    public TMP_Text EducationMwh;       // 교육용
    public TMP_Text IndustrialMwh;      // 산업용
    public TMP_Text AgricultureMwh;     // 농사용
    public TMP_Text StreetlightMwh;     // 가로등
    public TMP_Text MidnightMwh;        // 심야

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
        if (Panel_Gu_Energy != null)
            Panel_Gu_Energy.SetActive(true);

        if (Text_District_Energy != null) Text_District_Energy.text = $"{districtName} 에너지";
        if (Text_Usage_Change_Amount != null) Text_Usage_Change_Amount.text = usageChangeAmountText;
        if (Text_Usage_Change_Rate != null) Text_Usage_Change_Rate.text = usageChangeRateText;
        if (Text_Total_Usage != null) Text_Total_Usage.text = totalUsageText;
        if (Text_Usage_Percent != null) Text_Usage_Percent.text = usagePercentText;

        if (ResidentialMwh != null) ResidentialMwh.text = houseText;
        if (GeneralMwh != null) GeneralMwh.text = generalText;
        if (EducationMwh != null) EducationMwh.text = educationText;
        if (IndustrialMwh != null) IndustrialMwh.text = industrialText;
        if (AgricultureMwh != null) AgricultureMwh.text = agricultureText;
        if (StreetlightMwh != null) StreetlightMwh.text = streetlightText;
        if (MidnightMwh != null) MidnightMwh.text = midnightText;
    }

    public void Hide()
    {
        if (Panel_Gu_Energy != null)
            Panel_Gu_Energy.SetActive(false);
    }
}
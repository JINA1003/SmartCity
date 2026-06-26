using TMPro;
using UnityEngine;

public class GuEnergyPanelUI : MonoBehaviour
{
    [Header("구별 에너지 패널")]
    public GameObject Panel_Gu_Energy;

    public TMP_Text Text_District_Energy;
    public TMP_Text Text_Usage_Change_Amount;
    public TMP_Text Text_Usage_Change_Rate;
    public TMP_Text Text_Total_Usage;
    public TMP_Text Text_Usage_Percent;

    public TMP_Text ResidentialMwh;
    public TMP_Text GeneralMwh;
    public TMP_Text EducationMwh;
    public TMP_Text IndustrialMwh;
    public TMP_Text AgricultureMwh;
    public TMP_Text StreetlightMwh;
    public TMP_Text MidnightMwh;

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
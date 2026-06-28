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

    private void Awake()
    {
        ResolveReferences();
    }

    // 선택된 구와 날짜에 맞는 전력 사용량을 구 패널에 표시합니다.
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
        ResolveReferences();

        if (Panel_Gu_Energy != null)
        {
            EnsureParentsActive(Panel_Gu_Energy.transform);
            Panel_Gu_Energy.SetActive(true);
        }

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

    // 날짜 데이터가 들어오기 전에는 구 패널을 숨깁니다.
    public void Hide()
    {
        ResolveReferences();

        if (Panel_Gu_Energy != null)
            Panel_Gu_Energy.SetActive(false);
    }

    // 인스펙터 연결이 비어 있어도 Panel_Gu_Energy와 값 텍스트를 찾아서 연결합니다.
    private void ResolveReferences()
    {
        if (Panel_Gu_Energy == null)
            Panel_Gu_Energy = FindPanelObject();

        if (Text_District_Energy == null) Text_District_Energy = FindText("Text_District_Energy");
        if (Text_Usage_Change_Amount == null) Text_Usage_Change_Amount = FindText("Text_Usage_Change_Amount");
        if (Text_Usage_Change_Rate == null) Text_Usage_Change_Rate = FindText("Text_Usage_Change_Rate");
        if (Text_Total_Usage == null) Text_Total_Usage = FindText("Text_Total_Usage");
        if (Text_Usage_Percent == null) Text_Usage_Percent = FindText("Text_Usage_Percent");

        if (ResidentialMwh == null) ResidentialMwh = FindText("Text_Residential_Value");
        if (GeneralMwh == null) GeneralMwh = FindText("Text_General_Value");
        if (EducationMwh == null) EducationMwh = FindText("Text_Education_Value");
        if (IndustrialMwh == null) IndustrialMwh = FindText("Text_Industrial_Value");
        if (AgricultureMwh == null) AgricultureMwh = FindText("Text_Agriculture_Value");
        if (StreetlightMwh == null) StreetlightMwh = FindText("Text_Streetlight_Value");
        if (MidnightMwh == null) MidnightMwh = FindText("Text_Midnight_Value");
    }

    // 스크립트가 패널 자식에 붙어 있어도 실제 패널 오브젝트를 찾아 켤 수 있게 합니다.
    private GameObject FindPanelObject()
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == "Panel_Gu_Energy")
                return current.gameObject;

            current = current.parent;
        }

        return gameObject;
    }

    // 라벨 텍스트가 아니라 값 텍스트만 찾아서 갱신합니다.
    private TMP_Text FindText(string objectName)
    {
        TMP_Text[] texts = Panel_Gu_Energy != null
            ? Panel_Gu_Energy.GetComponentsInChildren<TMP_Text>(true)
            : GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text.gameObject.name == objectName)
                return text;
        }

        return null;
    }

    private void EnsureParentsActive(Transform target)
    {
        Transform current = target.parent;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);

            current = current.parent;
        }
    }
}

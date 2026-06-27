using TMPro;
using UnityEngine;

public class GuEnergyPanelUI : MonoBehaviour
{
    [Header("구 에너지 패널")]
    public GameObject Panel_Gu_Energy; // 구 에너지 패널 전체 오브젝트

    [Header("구 기본 정보")]
    public TMP_Text Text_District_Energy;     // 선택된 구 이름
    public TMP_Text Text_Usage_Change_Amount; // 전력 사용 변화량
    public TMP_Text Text_Usage_Change_Rate;   // 전력 사용 변화율
    public TMP_Text Text_Total_Usage;         // 구 전체 전력 사용량
    public TMP_Text Text_Usage_Percent;       // 서울 전체 대비 사용 비율

    [Header("용도별 전력 사용량")]
    public TMP_Text Text_ResidentialMwh; // 주택용
    public TMP_Text Text_GeneralMwh;     // 일반용
    public TMP_Text Text_EducationMwh;   // 교육용
    public TMP_Text Text_IndustrialMwh;  // 산업용
    public TMP_Text Text_AgricultureMwh; // 농사용
    public TMP_Text Text_StreetlightMwh; // 가로등
    public TMP_Text Text_MidnightMwh;    // 심야

    // UIManager에서 받은 구 에너지 정보를 실제 텍스트 UI에 넣는 함수입니다.
    public void SetGuEnergyPanel(
        string districtName,              // 선택된 구 이름
        string usageChangeAmountText,     // 전력 사용 변화량
        string usageChangeRateText,       // 전력 사용 변화율
        string totalUsageText,            // 구 전체 전력 사용량
        string usagePercentText,          // 서울 전체 대비 사용 비율
        string residentialText,           // 주택용 전력 사용량
        string generalText,               // 일반용 전력 사용량
        string educationText,             // 교육용 전력 사용량
        string industrialText,            // 산업용 전력 사용량
        string agricultureText,           // 농사용 전력 사용량
        string streetlightText,           // 가로등 전력 사용량
        string midnightText               // 심야 전력 사용량
    )
    {
        // 패널을 켜서 화면에 보이게 합니다.
        OpenPanel();

        // 구 기본 정보 텍스트를 갱신합니다.
        SetText(Text_District_Energy, districtName + " 에너지");
        SetText(Text_Usage_Change_Amount, usageChangeAmountText);
        SetText(Text_Usage_Change_Rate, usageChangeRateText);
        SetText(Text_Total_Usage, totalUsageText);
        SetText(Text_Usage_Percent, usagePercentText);

        // 용도별 전력 사용량 텍스트를 갱신합니다.
        SetText(Text_ResidentialMwh, residentialText);
        SetText(Text_GeneralMwh, generalText);
        SetText(Text_EducationMwh, educationText);
        SetText(Text_IndustrialMwh, industrialText);
        SetText(Text_AgricultureMwh, agricultureText);
        SetText(Text_StreetlightMwh, streetlightText);
        SetText(Text_MidnightMwh, midnightText);
    }

    // TMP_Text가 연결되어 있을 때만 텍스트를 넣습니다.
    private void SetText(TMP_Text targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }

    // 구 에너지 패널을 화면에 보이게 합니다.
    public void OpenPanel()
    {
        if (Panel_Gu_Energy != null)
        {
            Panel_Gu_Energy.SetActive(true);
        }
    }

    // 구 에너지 패널을 화면에서 숨깁니다.
    public void ClosePanel()
    {
        if (Panel_Gu_Energy != null)
        {
            Panel_Gu_Energy.SetActive(false);
        }
    }
}
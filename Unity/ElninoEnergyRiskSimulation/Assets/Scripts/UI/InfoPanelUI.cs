using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoPanelUI : MonoBehaviour
{
    [Header("상단 정보 패널")]
    public TMP_Text Text_Date_Info;
    public TMP_Text Text_Emergency;
    public TMP_Text Text_Emergency_Value;
    public Image Img_Emergency_Dot;
    public TMP_Text Text_Info_ONIType;
    public TMP_Text Text_Info_ONINum;

    private string cachedDateText;
    private string cachedEmergencyStage;
    private string cachedOniType;
    private string cachedOniValueText;

    public void SetInfo(string dateText, string emergencyStage, string oniType, string oniValueText)
    {
        if (string.IsNullOrEmpty(emergencyStage))
            emergencyStage = "정상";

        if (cachedDateText == dateText &&
            cachedEmergencyStage == emergencyStage &&
            cachedOniType == oniType &&
            cachedOniValueText == oniValueText)
        {
            return;
        }

        cachedDateText = dateText;
        cachedEmergencyStage = emergencyStage;
        cachedOniType = oniType;
        cachedOniValueText = oniValueText;

        if (Text_Date_Info != null) Text_Date_Info.text = dateText;
        if (Text_Emergency_Value != null) Text_Emergency_Value.text = emergencyStage;
        if (Text_Info_ONIType != null) Text_Info_ONIType.text = oniType;
        if (Text_Info_ONINum != null) Text_Info_ONINum.text = oniValueText;

        if (Img_Emergency_Dot != null)
            Img_Emergency_Dot.color = GetStageColor(emergencyStage);
    }

    private Color32 GetStageColor(string emergencyStage)
    {
        switch (emergencyStage)
        {
            case "관심": return new Color32(91, 173, 255, 255);
            case "주의": return new Color32(255, 242, 0, 255);
            case "경계": return new Color32(255, 157, 0, 255);
            case "심각": return new Color32(255, 2, 2, 255);
            default: return new Color32(19, 204, 53, 255);
        }
    }
}
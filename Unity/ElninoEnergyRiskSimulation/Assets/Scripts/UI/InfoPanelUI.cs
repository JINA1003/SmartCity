using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoPanelUI : MonoBehaviour
{
    [Header("상단 정보 패널")]
    public TMP_Text Text_Date_Info;       // 날짜
    public TMP_Text Text_Emergency;       // 비상 단계 텍스트
    public TMP_Text Text_Emergency_Value; // 비상 단계 값
    public Image Img_Emergency_Dot;       // 단계 색상 원
    public TMP_Text Text_Info_ONIType;    // 엘니뇨 / 라니냐 / 중립
    public TMP_Text Text_Info_ONINum;     // ONI 수치

    public void SetInfoPanel(
        string dateText,
        string emergencyStage,
        string oniType,
        string oniValueText
    )
    {
        if (string.IsNullOrEmpty(emergencyStage)) // 단계가 없으면 정상으로 처리
            emergencyStage = "정상";

        Text_Date_Info.text = dateText;
        Text_Emergency_Value.text = emergencyStage;
        Text_Info_ONIType.text = oniType;
        Text_Info_ONINum.text = oniValueText;

        Img_Emergency_Dot.color = GetEmergencyColor(emergencyStage);
    }

    private Color32 GetEmergencyColor(string emergencyStage)
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
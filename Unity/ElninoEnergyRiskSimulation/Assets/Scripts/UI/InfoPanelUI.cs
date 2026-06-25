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

    public void SetInfo(
        string dateText,
        string emergencyStage,
        Color32 emergencyColor,
        string oniType,
        string oniValueText
    )
    {
        // 정보 텍스트 갱신
        Text_Date_Info.text = dateText;
        Text_Emergency_Value.text = emergencyStage;
        Text_Info_ONIType.text = oniType;
        Text_Info_ONINum.text = oniValueText;

        // 단계 색상 갱신
        if (Img_Emergency_Dot != null)
        {
            Img_Emergency_Dot.color = emergencyColor;
        }
    }
}
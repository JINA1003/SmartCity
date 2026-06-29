using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoPanelUI : MonoBehaviour
{
    [Header("상단 정보 패널")]
    public GameObject Panel_Info;

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

    private void Awake()
    {
        ResolveReferences();
    }

    // 날짜 데이터가 들어오면 인포 패널을 켜고 상단 정보를 갱신합니다.
    public void SetInfo(string dateText, string emergencyStage, string oniType, string oniValueText)
    {
        ResolveReferences();
        Show();

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

    // 게임 시작 직후에는 날짜 선택 전이므로 인포 패널을 숨깁니다.
    public void Hide()
    {
        ResolveReferences();

        if (Panel_Info != null)
            Panel_Info.SetActive(false);
    }

    private void Show()
    {
        if (Panel_Info != null)
            Panel_Info.SetActive(true);
    }

    // 인스펙터 연결이 비어 있어도 Panel_Info 오브젝트를 찾아서 사용합니다.
    private void ResolveReferences()
    {
        if (Panel_Info != null) return;

        Transform current = transform;
        while (current != null)
        {
            if (current.name == "Panel_Info")
            {
                Panel_Info = current.gameObject;
                return;
            }

            current = current.parent;
        }

        Panel_Info = gameObject;
    }

    // 경보 단계에 맞춰 상단 상태 점 색상을 정합니다.
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

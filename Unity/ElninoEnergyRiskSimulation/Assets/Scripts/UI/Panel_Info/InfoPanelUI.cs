using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoPanelUI : MonoBehaviour
{
    [Header("상단 정보 패널")]
    public GameObject Panel_Info;              // 켜고 끌 상단 인포 패널 루트 오브젝트

    public TMP_Text Text_Date_Info;            // 선택한 예측 연월 표시
    public TMP_Text Text_Temperature_Info;     // predicted.asos_temp, 서울 전체 평균 기온 표시
    public TMP_Text Text_Emergency;            // 경보 라벨 제목 텍스트
    public TMP_Text Text_Emergency_Value;      // 경보 단계 값(정상/관심/주의/경계/심각)
    public Image Img_Emergency_Dot;            // 경보 단계에 따라 색이 바뀌는 상태 점
    public TMP_Text Text_Info_ONIType;         // ONI 상태(엘니뇨/라니냐/중립)
    public TMP_Text Text_Info_ONINum;          // ONI 수치

    private string cachedDateText;             // 같은 값 반복 갱신 방지용 날짜 캐시
    private string cachedTemperatureText;      // 같은 값 반복 갱신 방지용 기온 캐시
    private string cachedEmergencyStage;       // 같은 값 반복 갱신 방지용 경보 단계 캐시
    private string cachedOniType;              // 같은 값 반복 갱신 방지용 ONI 상태 캐시
    private string cachedOniValueText;         // 같은 값 반복 갱신 방지용 ONI 수치 캐시

    private void Awake()
    {
        ResolveReferences();                   // 씬 시작 시 인스펙터 연결 누락을 보정
    }

    // 날짜 데이터가 들어오면 인포 패널을 켜고 상단 정보를 갱신합니다.
    public void SetInfo(string dateText, string temperatureText, string emergencyStage, string oniType, string oniValueText)
    {
        ResolveReferences();                   // 런타임 생성/프리팹 연결 누락에 대비
        Show();                                // 데이터가 들어왔으니 상단 패널 표시

        if (string.IsNullOrEmpty(emergencyStage))
            emergencyStage = "정상";           // API 값이 비어 있으면 안전 기본값 사용

        if (cachedDateText == dateText &&
            cachedTemperatureText == temperatureText &&
            cachedEmergencyStage == emergencyStage &&
            cachedOniType == oniType &&
            cachedOniValueText == oniValueText)
        {
            return;                            // 화면 값이 같으면 불필요한 텍스트 갱신 생략
        }

        cachedDateText = dateText;             // 최신 날짜 캐시
        cachedTemperatureText = temperatureText; // 최신 기온 캐시
        cachedEmergencyStage = emergencyStage; // 최신 경보 단계 캐시
        cachedOniType = oniType;               // 최신 ONI 상태 캐시
        cachedOniValueText = oniValueText;     // 최신 ONI 수치 캐시

        if (Text_Date_Info != null) Text_Date_Info.text = dateText;                       // 연월 표시
        if (Text_Temperature_Info != null) Text_Temperature_Info.text = temperatureText;   // 서울 전체 기온 표시
        if (Text_Emergency_Value != null) Text_Emergency_Value.text = emergencyStage;      // 경보 단계 표시
        if (Text_Info_ONIType != null) Text_Info_ONIType.text = oniType;                   // ONI 상태 표시
        if (Text_Info_ONINum != null) Text_Info_ONINum.text = oniValueText;                // ONI 수치 표시

        if (Img_Emergency_Dot != null)
            Img_Emergency_Dot.color = GetStageColor(emergencyStage); // 경보 단계에 맞는 점 색상 적용
    }

    // 게임 시작 직후에는 날짜 선택 전이므로 인포 패널을 숨깁니다.
    public void Hide()
    {
        ResolveReferences();                   // Panel_Info 연결 누락 보정

        if (Panel_Info != null)
            Panel_Info.SetActive(false);       // 상단 인포 패널 숨김
    }

    private void Show()
    {
        if (Panel_Info != null)
            Panel_Info.SetActive(true);        // 상단 인포 패널 표시
    }

    // 인스펙터 연결이 비어 있어도 Panel_Info 오브젝트를 찾아서 사용합니다.
    private void ResolveReferences()
    {
        if (Panel_Info == null)
            Panel_Info = FindPanelObject();    // 현재 오브젝트/부모에서 Panel_Info 루트 찾기

        if (Text_Date_Info == null) Text_Date_Info = FindText("Text_Date_Info");                       // 날짜 텍스트 자동 연결
        if (Text_Temperature_Info == null) Text_Temperature_Info = FindText("Text_Temperature_Info");   // 기온 텍스트 자동 연결
        if (Text_Emergency == null) Text_Emergency = FindText("Text_Emergency");                       // 경보 제목 텍스트 자동 연결
        if (Text_Emergency_Value == null) Text_Emergency_Value = FindText("Text_Emergency_Value");     // 경보 값 텍스트 자동 연결
        if (Text_Info_ONIType == null) Text_Info_ONIType = FindText("Text_Info_ONIType");              // ONI 상태 텍스트 자동 연결
        if (Text_Info_ONINum == null) Text_Info_ONINum = FindText("Text_Info_ONINum");                 // ONI 수치 텍스트 자동 연결
        if (Img_Emergency_Dot == null) Img_Emergency_Dot = FindImage("Img_Emergency_Dot");             // 경보 점 이미지 자동 연결
    }

    private GameObject FindPanelObject()
    {
        Transform current = transform;         // 현재 오브젝트부터 부모 방향으로 검색
        while (current != null)
        {
            if (current.name == "Panel_Info")
                return current.gameObject;     // Panel_Info를 찾으면 루트로 사용

            current = current.parent;          // 못 찾으면 한 단계 위 부모 검사
        }

        return gameObject;                     // 끝까지 못 찾으면 스크립트가 붙은 오브젝트 사용
    }

    private TMP_Text FindText(string objectName)
    {
        TMP_Text[] texts = Panel_Info != null
            ? Panel_Info.GetComponentsInChildren<TMP_Text>(true) // 비활성 자식까지 포함해서 패널 내부 검색
            : GetComponentsInChildren<TMP_Text>(true);           // 패널이 없으면 현재 오브젝트 기준 검색

        foreach (TMP_Text text in texts)
        {
            if (text.gameObject.name == objectName)
                return text;                    // 이름이 일치하는 TMP 텍스트 반환
        }

        return null;                            // 못 찾으면 인스펙터 수동 연결에 맡김
    }

    private Image FindImage(string objectName)
    {
        Image[] images = Panel_Info != null
            ? Panel_Info.GetComponentsInChildren<Image>(true) // 비활성 자식까지 포함해서 패널 내부 검색
            : GetComponentsInChildren<Image>(true);           // 패널이 없으면 현재 오브젝트 기준 검색

        foreach (Image image in images)
        {
            if (image.gameObject.name == objectName)
                return image;                   // 이름이 일치하는 Image 반환
        }

        return null;                            // 못 찾으면 인스펙터 수동 연결에 맡김
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

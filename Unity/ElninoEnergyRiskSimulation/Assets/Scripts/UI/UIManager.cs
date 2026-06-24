using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.InputSystem.HID.HID;
using Button = UnityEngine.UI.Button;

public class UIManagers : MonoBehaviour
{
    public static UIManagers Instance { get; private set; }

   

    // [Header("상단 정보 패널")]
    // public TMP_Text Text_Date_Info;                    // 현재 날짜
    // public TMP_Text Text_Emergency;              // 비상 단계 텍스트
    // public TMP_Text Text_ONIType;                // ONI 종류 (엘니뇨, 라니냐)

[Header("구별 에너지 패널")]
    public GameObject Panel_Gu_Energy;

    // 구 이름
    public TMP_Text Text_District_Energy;

    // 총 전력 사용량
    public TMP_Text Text_Energy_Consumption_Value;

    // 사용량 비율
    public TMP_Text Text_Energy_Consumption_Value2;

    // 용도별 전력 사용량
    public TMP_Text ResidentialMwh; //주택
    public TMP_Text CommercialMwh; //일반
    public TMP_Text EducationMwh; //교육
    public TMP_Text IndustrialMwh; //상업
    public TMP_Text AgricultureMwh; //농사
    public TMP_Text StreetlightMwh; //가로등
    public TMP_Text MidnightMwh;     //심야

    // [Header("수요 감축 필요도 패널")]
    // public TMP_Text Text_Residential;            // 주거용 감축 필요량
    // public TMP_Text Text_Commercial;             // 상업용 감축 필요량
    // public TMP_Text Text_Office;                 // 업무용 감축 필요량
    // public TMP_Text Text_Industrial;             // 산업용 감축 필요량
    // public TMP_Text Text_PublicFacility;         // 공공시설 감축 필요량

    // [Header("전력 감축 요청")]
    // public Button Btn_DemandResponse;            // 전력 감축 요청

    // [Header("상관관계 패널")]
    // public Image Img_CorrelationGraph;           // 상관관계 그래프
    // public TMP_Text Text_Supply;                 // 공급량
    // public TMP_Text Text_Consumption;            // 사용량
    // public TMP_Text Text_ReserveMargin;          // 예비율
    // public TMP_Text Text_Temperature;            // 기온


    // [Header("비상 전력 관리 패널")]
    // public TMP_Text Text_HydroStatus;            // 수력 발전소 상태
    // public TMP_Text Text_GasStatus;              // 가스 발전소 상태
    // public TMP_Text Text_ReserveRate;            // 현재 예비율
    // public Slider Slider_ReserveRate;            // 예비율 게이지

    [Header("기후 시나리오 제어 패널")]
    //public TMP_Dropdown Dropdown_Year;           // 연도 선택
    //public TMP_Dropdown Dropdown_Month;          // 월 선택

    public Slider Slider_ONI;              // ONI 조절 슬라이더
    public TMP_Text Text_ONI;              // ONI 타이틀
    public TMP_Text Text_ONI_Num;          // ONI 값 표시
    public TMP_Text Text_ONIType;          // 엘니뇨 / 라니냐 / 중립
    public RectTransform CenterFill;       // 중앙 기준 게이지

    // [Header("열지도 패널")]
    // public Image Img_Heatmap;                    // 열지도 이미지





    private void Start()
    {
        Slider_ONI.onValueChanged.AddListener(OnChangedONI);
        OnChangedONI(Slider_ONI.value);
    }

    private void OnChangedONI(float value)
    {
        // ONI 수치 표시
        Text_ONI_Num.text = value.ToString("F1");

        // 슬라이더 최대값 기준 비율 계산 (-2.5 ~ 2.5)
        float ratio = Mathf.Abs(value) / Slider_ONI.maxValue;

        // 게이지 최대 길이
        float maxWidth = 100f;

        // 게이지 길이 변경
        CenterFill.sizeDelta =
            new Vector2(ratio * maxWidth, CenterFill.sizeDelta.y);

        Image fillImage = CenterFill.GetComponent<Image>();

        // 라니냐
        if (value < 0)
        {
            CenterFill.pivot = new Vector2(1f, 0.5f);
            fillImage.color = new Color32(0, 123, 255, 255);
            Text_ONIType.text = "라니냐";
        }
        // 엘니뇨
        else if (value > 0)
        {
            CenterFill.pivot = new Vector2(0f, 0.5f);
            fillImage.color = new Color32(255, 2, 2, 255);
            Text_ONIType.text = "엘니뇨";
        }
        // 중립
        else
        {
            CenterFill.sizeDelta =
                new Vector2(0f, CenterFill.sizeDelta.y);

            fillImage.color = new Color32(150, 150, 150, 255);
            Text_ONIType.text = "중립";
        }
    }

    public float GetONI()
    {
        return Slider_ONI.value;
    }

    public void ShowGuEnergyPanel(DistrictData data, float usagePercent)
    {
        



    }





}
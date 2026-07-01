using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Dropdown yearDropdown;
    [SerializeField] private TMP_Dropdown monthDropdown;
    [SerializeField] private GameObject oniSliderPanel; // Panel_ONI_Scroll 전체
    [SerializeField] private Slider oniSlider;
    [SerializeField] private TMP_Text oniTypeText;  // 중립 / 라니냐 / 엘니뇨
    [SerializeField] private TMP_Text oniNumText;   // 0.0

    // 연월 드롭다운 변경 → /oni && /predict/oni_range 호출 트리거
    public event Action<string, string> OnDateSelected;

    // 슬라이더 드래그 중 실시간 ONI 값 변경 알림
    public event Action<float> OnOniValueChanged;

    // 슬라이더 버튼 뗄 때 → /predict 재호출 트리거 (ONI 값 전달)
    public event Action<float> OnOniSliderReleased;

    private void Awake()
    {
        BuildDropdownOptions();

        // 옵션 세팅 후 리스너 등록 — Awake에서 AddListener하면 BuildDropdownOptions의
        // value 변경이 onValueChanged를 트리거하지 않음 (리스너 없는 상태에서 설정)
        yearDropdown.onValueChanged.AddListener(_ => NotifyDateSelected());
        monthDropdown.onValueChanged.AddListener(_ => NotifyDateSelected());

        // 슬라이더: 드래그 중 ONI 타입/수치 실시간 표시 + 차트 수직선 이벤트
        oniSlider.onValueChanged.AddListener(v => { UpdateOniDisplay(v); OnOniValueChanged?.Invoke(v); });

        var trigger = oniSlider.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        entry.callback.AddListener(_ => OnOniSliderReleased?.Invoke(oniSlider.value));
        trigger.triggers.Add(entry);
    }

    private void Start()
    {
        // 초기 상태: 슬라이더 숨김, 드롭다운 선택 없음 (빈 placeholder 상태)
        SetSliderActive(false);

        // 자동 API 호출 없음 — 사용자가 직접 연월을 선택해야 호출됨
    }

    // DataManager에서 /oni 응답 후 호출 — 슬라이더 값 설정 및 활성화
    public void InitSlider(float oniValue, float min = -2.5f, float max = 2.5f)
    {
        oniSlider.minValue = min;
        oniSlider.maxValue = max;
        oniSlider.value    = oniValue;
        UpdateOniDisplay(oniValue);
        SetSliderActive(true);
    }

    private void UpdateOniDisplay(float value)
    {
        if (oniNumText  != null) oniNumText.text  = value.ToString("F1");
        if (oniTypeText != null) oniTypeText.text  = OniToType(value);
    }

    private static string OniToType(float v)
    {
        if (v <= -0.5f) return "라니냐";
        if (v >=  0.5f) return "엘니뇨";
        return "중립";
    }

    public string GetSelectedYear()  => yearDropdown.options[yearDropdown.value].text;
    public string GetSelectedMonth() => monthDropdown.options[monthDropdown.value].text;
    public float GetCurrentOni() => oniSlider != null ? oniSlider.value : 0f;

    private void NotifyDateSelected()
    {
        // placeholder(index=0) 상태면 API 호출 안 함
        if (yearDropdown.value == 0 || monthDropdown.value == 0) return;

        SetSliderActive(false);
        OnDateSelected?.Invoke(GetSelectedYear(), GetSelectedMonth());
    }

    private void SetSliderActive(bool active)
    {
        if (oniSliderPanel != null)
            oniSliderPanel.SetActive(active);
        else
            oniSlider.gameObject.SetActive(active);
        oniSlider.interactable = active;
    }

    private void BuildDropdownOptions()
    {
        yearDropdown.ClearOptions();
        var years = new List<string> { "연도 선택" };
        for (int y = 2005; y <= 2040; y++) years.Add(y.ToString());
        yearDropdown.AddOptions(years);
        yearDropdown.value = 0;

        monthDropdown.ClearOptions();
        var months = new List<string> { "월 선택" };
        for (int m = 1; m <= 12; m++) months.Add(m.ToString());
        monthDropdown.AddOptions(months);
        monthDropdown.value = 0;
    }
}

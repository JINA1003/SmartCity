using System;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField]
    private Dropdown yearDropdown;
    [SerializeField]
    private Dropdown monthDropdown;
    [SerializeField]
    private Button startButton;
    [SerializeField]
    private Slider OniSlider;

    // string(year), string(month)
    public event Action<string, string> OnStartButtonClick;
    private void Awake()
    {
        startButton.onClick.AddListener(HandleStartButtonClick);
    }

    private void Start()
    {
        // OniSlider.enabled = false;
    }
    private void HandleStartButtonClick()
    {
        //Debug.Log("[UIController] 시작(새로고침) 버튼이 클릭되었습니다.");
        //int yearIndex = yearDropdown.value;
        //int monthIndex = monthDropdown.value;
        //OnStartButtonClick?.Invoke(yearDropdown.options[yearIndex].text, monthDropdown.options[monthIndex].text);
        //OniSlider.enabled = true;
        OnStartButtonClick?.Invoke("2030", "8");
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BlackoutSimulationController : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private DataManager dataManager;

    [Header("Blackout Simulation 패널")]
    [SerializeField] private CanvasGroup blackoutSimulationCanvasGroup;
    [SerializeField] private Toggle simulationToggle;

    [Header("시뮬레이션 설정")]
    [SerializeField] private float stayDurationPerDistrict = 3f;

    // 비활성 상태일 때 패널 투명도
    private const float DisabledAlpha = 0.35f;

    public static event Action<string> OnBlackoutDistrictChanged;
    public static event Action<bool> OnBlackoutSimulationToggled;

    private List<string> _orderedDistricts = new();
    private Coroutine _simulationCoroutine;

    private void OnEnable()
    {
        dataManager.OnPowerDataUpdated    += HandlePowerDataUpdated;
        dataManager.OnBlackoutOrderParsed += HandleBlackoutOrderParsed;
        simulationToggle.onValueChanged.AddListener(HandleToggleChanged);
    }

    private void OnDisable()
    {
        dataManager.OnPowerDataUpdated    -= HandlePowerDataUpdated;
        dataManager.OnBlackoutOrderParsed -= HandleBlackoutOrderParsed;
        simulationToggle.onValueChanged.RemoveListener(HandleToggleChanged);
    }

    private void Start()
    {
        SetPanelEnabled(false);
    }

    private void HandlePowerDataUpdated(PowerGridData data)
    {
        bool isLevel4 = data.riskLevel == 4;
        SetPanelEnabled(isLevel4);

        if (!isLevel4 && simulationToggle.isOn)
            simulationToggle.isOn = false;
    }

    private void HandleBlackoutOrderParsed(List<string> orderedGuNames)
    {
        _orderedDistricts = orderedGuNames;
    }

    private void HandleToggleChanged(bool isOn)
    {
        OnBlackoutSimulationToggled?.Invoke(isOn);

        if (isOn)
        {
            if (_orderedDistricts.Count == 0)
            {
                Debug.LogWarning("[BlackoutSimulationController] 순회할 구 데이터가 없습니다.");
                simulationToggle.isOn = false;
                return;
            }

            _simulationCoroutine = StartCoroutine(RunSimulation());
        }
        else
        {
            if (_simulationCoroutine != null)
            {
                StopCoroutine(_simulationCoroutine);
                _simulationCoroutine = null;
            }
        }

        Debug.Log($"[BlackoutSimulationController] 시뮬레이션 {(isOn ? "시작" : "중단")}");
    }

    private IEnumerator RunSimulation()
    {
        foreach (string districtName in _orderedDistricts)
        {
            OnBlackoutDistrictChanged?.Invoke(districtName);
            Debug.Log($"[BlackoutSimulationController] 순회 중: {districtName}");

            // TODO: 건물 생성 후 여기에 해당 구의 건물 블랙아웃 처리 추가

            yield return new WaitForSeconds(stayDurationPerDistrict);
        }

        Debug.Log("[BlackoutSimulationController] 시뮬레이션 순회 완료");
        simulationToggle.isOn = false;
    }

    // 4단계: alpha=1 + 클릭 가능 / 그 외: alpha=0.35 + 클릭 차단
    private void SetPanelEnabled(bool enabled)
    {
        if (blackoutSimulationCanvasGroup == null) return;

        blackoutSimulationCanvasGroup.alpha          = enabled ? 1f : DisabledAlpha;
        blackoutSimulationCanvasGroup.interactable   = enabled;
        blackoutSimulationCanvasGroup.blocksRaycasts = enabled;
    }
}

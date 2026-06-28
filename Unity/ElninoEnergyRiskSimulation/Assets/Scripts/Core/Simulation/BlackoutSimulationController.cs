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
    [SerializeField] private GameObject blackoutSimulationPanel;
    [SerializeField] private Toggle simulationToggle;

    [Header("시뮬레이션 설정")]
    [SerializeField] private float stayDurationPerDistrict = 3f; // 구당 머무는 시간 (초)

    // 현재 순회 중인 구 이름 — MainCameraController가 구독해서 카메라 이동
    public static event Action<string> OnBlackoutDistrictChanged;

    // 시뮬레이션 ON/OFF — 외부에서 상태 감지용
    public static event Action<bool> OnBlackoutSimulationToggled;

    // districts_order 순서대로 저장된 구 이름 목록 (소비량 높은 순)
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
        SetPanelActive(false);
    }

    // /predict 결과로 riskLevel 수신 → 패널 활성/비활성
    private void HandlePowerDataUpdated(PowerGridData data)
    {
        bool isLevel4 = data.riskLevel == 4;
        SetPanelActive(isLevel4);

        // 4단계가 아닌데 시뮬레이션이 켜져 있으면 강제 종료
        if (!isLevel4 && simulationToggle.isOn)
            simulationToggle.isOn = false;
    }

    // DataManager에서 소비량 내림차순 + blackout_items 있는 구 이름 목록을 직접 수신
    private void HandleBlackoutOrderParsed(List<string> orderedGuNames)
    {
        _orderedDistricts = orderedGuNames;
    }

    // 토글 ON → 시뮬레이션 코루틴 시작 / OFF → 중단
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

    // 구 순서대로 카메라 이동 이벤트 발행
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

    private void SetPanelActive(bool active)
    {
        if (blackoutSimulationPanel != null)
            blackoutSimulationPanel.SetActive(active);
    }
}

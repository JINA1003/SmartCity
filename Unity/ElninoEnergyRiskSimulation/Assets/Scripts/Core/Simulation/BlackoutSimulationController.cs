using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackoutSimulationController : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private DataManager dataManager;

    public static event Action<string>           OnBlackoutDistrictChanged;
    public static event Action<bool>             OnBlackoutSimulationToggled;
    public static event Action<string, double>   OnDistrictBlackedOut;
    public static event Action                   OnSimulationCompleted;

    private List<string>               _orderedDistricts = new();
    private Dictionary<string, double> _guConsumption = new();
    private Coroutine                  _simulationCoroutine;
    private bool                       _isOn;
    private bool                       _waitingForFinish;

    public void RequestToggle(bool isOn)
    {
        if (_isOn == isOn) return;
        _isOn = isOn;
        OnBlackoutSimulationToggled?.Invoke(isOn);

        if (isOn)
        {
            if (_orderedDistricts.Count == 0)
            {
                Debug.LogWarning("[BlackoutSimulationController] 순회할 구 데이터가 없습니다.");
                _isOn = false;
                OnBlackoutSimulationToggled?.Invoke(false);
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

    private void OnEnable()
    {
        dataManager.OnPowerDataUpdated          += HandlePowerDataUpdated;
        dataManager.OnBlackoutSimulationParsed  += HandleBlackoutSimulationParsed;
    }

    private void OnDisable()
    {
        dataManager.OnPowerDataUpdated          -= HandlePowerDataUpdated;
        dataManager.OnBlackoutSimulationParsed  -= HandleBlackoutSimulationParsed;
    }

    // BlackoutLogger가 한 구의 로그를 모두 출력하면 호출
    public void NotifyDistrictFinished()
    {
        _waitingForFinish = false;
    }

    private void HandlePowerDataUpdated(PowerGridData data)
    {
        if (data.riskLevel < 4 && _isOn)
            RequestToggle(false);
    }

    private void HandleBlackoutSimulationParsed(
        List<string> orderedGuNames,
        Dictionary<string, double> guConsumption)
    {
        _orderedDistricts = orderedGuNames ?? new List<string>();
        _guConsumption = guConsumption ?? new Dictionary<string, double>();
    }

    private IEnumerator RunSimulation()
    {
        foreach (string gu in _orderedDistricts)
        {
            OnBlackoutDistrictChanged?.Invoke(gu);

            double consumption = _guConsumption.TryGetValue(gu, out double v) ? v : 0.0;
            _waitingForFinish = true;
            OnDistrictBlackedOut?.Invoke(gu, consumption);

            // BlackoutLogger가 NotifyDistrictFinished()를 호출할 때까지 대기
            yield return new WaitUntil(() => !_waitingForFinish);
        }

        Debug.Log("[BlackoutSimulationController] 시뮬레이션 완료");
        _simulationCoroutine = null;
        OnSimulationCompleted?.Invoke();
        RequestToggle(false);
    }
}

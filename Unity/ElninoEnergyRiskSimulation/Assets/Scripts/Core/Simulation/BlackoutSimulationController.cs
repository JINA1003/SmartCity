using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackoutSimulationController : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private DataManager dataManager;

    [Header("시뮬레이션 설정")]
    [SerializeField] private float stayDurationPerDistrict = 3f;

    public static event Action<string>           OnBlackoutDistrictChanged;
    public static event Action<bool>             OnBlackoutSimulationToggled;
    public static event Action<string, double>   OnDistrictBlackedOut;
    public static event Action                   OnSimulationCompleted;

    private List<string>               _orderedDistricts = new();
    private Dictionary<string, double> _guConsumption = new();
    private Coroutine                  _simulationCoroutine;
    private bool                       _isOn;

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
            OnDistrictBlackedOut?.Invoke(gu, consumption);

            Debug.Log($"[BlackoutSimulationController] 단전 중: {gu} ({consumption:F1} MWh)");

            yield return new WaitForSeconds(stayDurationPerDistrict);
        }

        Debug.Log("[BlackoutSimulationController] 시뮬레이션 완료");
        _simulationCoroutine = null;
        OnSimulationCompleted?.Invoke();
        RequestToggle(false);
    }
}

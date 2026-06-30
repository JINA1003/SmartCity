using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 순회단전 시뮬레이션의 단전 상태를 로그로 기록한다.
/// 구가 단전되면 0.5초 간격으로 건물유형을 순서대로 출력하고,
/// 모두 출력한 뒤 BlackoutSimulationController에 완료를 알려 다음 구로 넘어간다.
/// </summary>
public class BlackoutLogger : MonoBehaviour
{
    [SerializeField] private DataManager dataManager;
    [SerializeField] private BlackoutSimulationController simulationController;

    [Header("로그 설정")]
    [SerializeField] private float logIntervalSeconds = 0.5f;

    private Dictionary<string, List<BlackoutBuildingItem>> _itemsMap = new();
    private Dictionary<string, double> _consumptionMap = new();

    private Coroutine _logCoroutine;

    private void OnEnable()
    {
        dataManager.OnBlackoutSimulationParsed += HandleSimulationParsed;
        dataManager.OnBlackoutItemsParsed += HandleItemsParsed;

        simulationController.OnBlackoutSimulationToggled += HandleToggled;
        simulationController.OnDistrictBlackedOut += HandleDistrictBlackedOut;
        simulationController.OnSimulationCompleted += HandleSimulationCompleted;
    }

    private void OnDisable()
    {
        dataManager.OnBlackoutSimulationParsed -= HandleSimulationParsed;
        dataManager.OnBlackoutItemsParsed -= HandleItemsParsed;

        simulationController.OnBlackoutSimulationToggled -= HandleToggled;
        simulationController.OnDistrictBlackedOut -= HandleDistrictBlackedOut;
        simulationController.OnSimulationCompleted -= HandleSimulationCompleted;
    }

    // -----------------------------------------------------------------------

    private void HandleSimulationParsed(List<string> orderedGu, Dictionary<string, double> consumption)
    {
        _consumptionMap = consumption ?? new Dictionary<string, double>();

        Debug.Log($"[BlackoutLogger] 단전 순회 목록 수신 ({orderedGu.Count}개 구)");
        for (int i = 0; i < orderedGu.Count; i++)
        {
            string gu = orderedGu[i];
            double mwh = _consumptionMap.TryGetValue(gu, out double v) ? v : 0.0;
            Debug.Log($"[BlackoutLogger]   {i + 1}. {gu}  ({mwh:F1} MWh)");
        }
    }

    private void HandleItemsParsed(Dictionary<string, List<BlackoutBuildingItem>> itemsMap)
    {
        _itemsMap = itemsMap ?? new Dictionary<string, List<BlackoutBuildingItem>>();
    }

    private void HandleToggled(bool isOn)
    {
        if (isOn)
            Debug.Log("[BlackoutLogger] ===== 순회단전 시뮬레이션 시작 =====");
        else
        {
            // 중단 시 진행 중인 로그 코루틴 강제 종료
            if (_logCoroutine != null)
            {
                StopCoroutine(_logCoroutine);
                _logCoroutine = null;
            }
            Debug.Log("[BlackoutLogger] ===== 순회단전 시뮬레이션 중단 =====");
        }
    }

    private void HandleDistrictBlackedOut(string guName, double consumptionMwh)
    {
        if (_logCoroutine != null)
            StopCoroutine(_logCoroutine);

        _logCoroutine = StartCoroutine(LogDistrictBlackout(guName, consumptionMwh));
    }

    private IEnumerator LogDistrictBlackout(string guName, double consumptionMwh)
    {
        Debug.Log($"[BlackoutLogger] ▶ [{guName}] 단전 시작  (소비량: {consumptionMwh:F1} MWh)");

        if (_itemsMap.TryGetValue(guName, out var items) && items.Count > 0)
        {
            Debug.Log($"[BlackoutLogger]   단전 대상 건물유형 ({items.Count}개 용도)");

            foreach (var item in items)
            {
                yield return new WaitForSeconds(logIntervalSeconds);

                string label = string.IsNullOrEmpty(item.buildingType)
                    ? "알 수 없음"
                    : item.buildingType;
                Debug.Log($"[BlackoutLogger]   OFF  {label}  [수요감축 점수: {item.reductionNeedScore:F4}]");
            }
        }
        else
        {
            Debug.Log($"[BlackoutLogger]   단전 대상 건물유형 정보 없음");
        }

        Debug.Log($"[BlackoutLogger] ✔ [{guName}] 단전 완료 → 다음 구로 이동");
        _logCoroutine = null;

        // 모든 용도 로그 출력 완료 → 컨트롤러에 알려 다음 구로 진행
        simulationController.NotifyDistrictFinished();
    }

    private void HandleSimulationCompleted()
    {
        Debug.Log("[BlackoutLogger] ===== 순회단전 시뮬레이션 완료 =====");
    }
}

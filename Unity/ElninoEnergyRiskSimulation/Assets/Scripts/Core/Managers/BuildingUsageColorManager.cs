//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Runtime.InteropServices;
//using UnityEngine;
//using UnityEngine.Rendering;

///// <summary>
///// 수요감축 필요도(reduction_need_score)를 StructuredBuffer 기반으로 실시간 반영한다.
/////
///// [동작 원리]
///// 1. DataManager에서 구별/건물유형별 reduction_need_score를 수신
///// 2. CityManager의 CachedBuildingData 배열에서 각 건물의 reductionValue를 갱신
///// 3. CityManager.FlushBufferToGPU()로 GPU에 즉시 업로드
///// 4. 셰이더(SmartCity/BuildingUsage)가 StructuredBuffer에서 값을 읽어 히트맵 색상 적용
/////
///// ※ 기존 UV2.y 패칭 방식 완전 대체 — 메쉬 재생성 없이 매 프레임 색상 변경 가능
///// </summary>
//public class BuildingUsageColorManager : MonoBehaviour
//{
//    [Header("References")]
//    [SerializeField] private CityManager cityManager;
//    [SerializeField] private DistrictManager districtManager;
//    [SerializeField] private DataManager dataManager;

//    // buildingId → 배열 인덱스 룩업 (한 번만 빌드)
//    private Dictionary<int, int> _buildingIdToBufferIndex;

//    // 최신 구별 데이터 임시 저장
//    private Dictionary<DistrictType, DistrictData> _latestDistrictData;

//    // buildingId → BuildingType 룩업 (바이너리에서 한 번 빌드)
//    private readonly Dictionary<int, BuildingType> _buildingIdToType = new();

//    private bool _lookupReady;

//    private void OnEnable()
//    {
//        if (cityManager == null)
//            cityManager = FindFirstObjectByType<CityManager>();
//        if (districtManager == null)
//            districtManager = FindFirstObjectByType<DistrictManager>();
//        if (dataManager == null)
//            dataManager = FindFirstObjectByType<DataManager>();

//        if (districtManager != null)
//            districtManager.OnAllDistrictsDataUpdated += HandleAllDistrictsDataUpdated;

//        if (dataManager != null)
//        {
//            dataManager.OnDistrictDataUpdated += HandleDistrictDataUpdated;
//            dataManager.OnAllDistrictsParsed  += HandleAllDistrictsParsed;
//        }
//    }

//    private void OnDisable()
//    {
//        if (districtManager != null)
//            districtManager.OnAllDistrictsDataUpdated -= HandleAllDistrictsDataUpdated;

//        if (dataManager != null)
//        {
//            dataManager.OnDistrictDataUpdated -= HandleDistrictDataUpdated;
//            dataManager.OnAllDistrictsParsed  -= HandleAllDistrictsParsed;
//        }
//    }

//    private void Start()
//    {
//        BuildBuildingTypeLookupFromResources();
//        StartCoroutine(WaitForBufferAndBuildLookup());
//    }

//    /// <summary>
//    /// CityManager의 CachedBuildingData가 준비될 때까지 대기한 후
//    /// buildingId → 버퍼 인덱스 룩업 테이블을 구축한다.
//    /// </summary>
//    private IEnumerator WaitForBufferAndBuildLookup()
//    {
//        // CityManager가 모든 구역 메쉬 생성 + 버퍼 초기화를 마칠 때까지 대기
//        yield return new WaitUntil(() =>
//            cityManager != null &&
//            cityManager.CachedBuildingData != null &&
//            cityManager.CachedBuildingData.Length > 0);

//        BuildBufferIndexLookup();
//        _lookupReady = true;

//        Debug.Log($"[BuildingUsageColorManager] 버퍼 인덱스 룩업 완료 ({_buildingIdToBufferIndex.Count}개 건물)");

//        // 이미 수신된 데이터가 있으면 즉시 적용
//        if (_latestDistrictData != null && _latestDistrictData.Count > 0)
//            ApplyReductionScoresToBuffer(_latestDistrictData);
//    }

//    // ── 이벤트 핸들러 ──

//    private void HandleDistrictDataUpdated(DistrictData data)
//    {
//        _latestDistrictData ??= new Dictionary<DistrictType, DistrictData>();
//        _latestDistrictData[data.districtType] = data;
//    }

//    private void HandleAllDistrictsParsed()
//    {
//        if (_latestDistrictData != null && _latestDistrictData.Count > 0)
//            HandleAllDistrictsDataUpdated(_latestDistrictData);
//    }

//    private void HandleAllDistrictsDataUpdated(Dictionary<DistrictType, DistrictData> districts)
//    {
//        if (!_lookupReady)
//        {
//            // 아직 룩업이 준비되지 않았으면 데이터만 저장해두고 대기
//            _latestDistrictData = new Dictionary<DistrictType, DistrictData>(districts);
//            return;
//        }

//        ApplyReductionScoresToBuffer(districts);
//    }

//    // ── 핵심: StructuredBuffer에 reductionValue 기록 ──

//    /// <summary>
//    /// 모든 구의 건물 reductionValue를 갱신하고 GPU에 업로드한다.
//    /// </summary>
//    private void ApplyReductionScoresToBuffer(
//        IReadOnlyDictionary<DistrictType, DistrictData> districts)
//    {
//        NativeBuildingData[] buffer = cityManager.CachedBuildingData;
//        if (buffer == null || buffer.Length == 0) return;

//        // 전체 구에서 min/max 계산 (정규화용)
//        GetScoreRange(districts, out float minScore, out float maxScore);

//        int updatedCount = 0;

//        for (int i = 0; i < buffer.Length; i++)
//        {
//            ref NativeBuildingData building = ref buffer[i];

//            // 이 건물이 속한 구의 DistrictData 찾기
//            DistrictType dt = (DistrictType)building.districtType;
//            if (!districts.TryGetValue(dt, out DistrictData districtData))
//            {
//                building.reductionValue = 0f;
//                continue;
//            }

//            // 이 건물의 BuildingType에 해당하는 점수 찾기
//            BuildingType bt = (BuildingType)building.buildingType;
//            if (districtData.buildingReductionScores != null &&
//                districtData.buildingReductionScores.TryGetValue(bt, out float score))
//            {
//                // min-max 정규화 (0 ~ 1)
//                building.reductionValue = (maxScore > minScore)
//                    ? Mathf.Clamp01((score - minScore) / (maxScore - minScore))
//                    : 0f;
//                updatedCount++;
//            }
//            else
//            {
//                building.reductionValue = 0f;
//            }
//        }

//        // GPU에 반영
//        cityManager.MarkBufferDirty();
//        cityManager.FlushBufferToGPU();

//        Debug.Log($"[BuildingUsageColorManager] reductionValue 갱신 완료 ({updatedCount}개 건물, 범위 {minScore:F3}~{maxScore:F3})");
//    }

//    // ── 유틸리티 ──

//    /// <summary>
//    /// CachedBuildingData에서 buildingId → 배열 인덱스 매핑을 빌드한다.
//    /// (현재는 직접 순회 방식이라 미사용이지만, 개별 건물 제어가 필요할 때 활용)
//    /// </summary>
//    private void BuildBufferIndexLookup()
//    {
//        NativeBuildingData[] data = cityManager.CachedBuildingData;
//        _buildingIdToBufferIndex = new Dictionary<int, int>(data.Length);

//        for (int i = 0; i < data.Length; i++)
//        {
//            _buildingIdToBufferIndex[data[i].id] = i;
//        }
//    }

//    private void BuildBuildingTypeLookupFromResources()
//    {
//        _buildingIdToType.Clear();

//        foreach (DistrictType district in Enum.GetValues(typeof(DistrictType)))
//        {
//            if (district == DistrictType.None) continue;

//            TextAsset binFile = Resources.Load<TextAsset>($"Districts/District_{(int)district}");
//            if (binFile == null) continue;

//            ReadBuildingTypesFromBytes(binFile.bytes);
//        }
//    }

//    private void ReadBuildingTypesFromBytes(byte[] rawData)
//    {
//        int structSize = Marshal.SizeOf(typeof(NativeBuildingData));
//        if (structSize <= 0 || rawData.Length < structSize) return;

//        int buildingCount = rawData.Length / structSize;
//        GCHandle handle = GCHandle.Alloc(rawData, GCHandleType.Pinned);

//        try
//        {
//            IntPtr basePtr = handle.AddrOfPinnedObject();
//            for (int i = 0; i < buildingCount; i++)
//            {
//                IntPtr itemPtr = IntPtr.Add(basePtr, i * structSize);
//                NativeBuildingData building = Marshal.PtrToStructure<NativeBuildingData>(itemPtr);
//                _buildingIdToType[building.id] = (BuildingType)building.buildingType;
//            }
//        }
//        finally
//        {
//            if (handle.IsAllocated)
//                handle.Free();
//        }
//    }

//    private static void GetScoreRange(
//        IReadOnlyDictionary<DistrictType, DistrictData> districts,
//        out float minScore, out float maxScore)
//    {
//        minScore = float.MaxValue;
//        maxScore = float.MinValue;

//        foreach (DistrictData data in districts.Values)
//        {
//            if (data?.buildingReductionScores == null) continue;

//            foreach (float score in data.buildingReductionScores.Values)
//            {
//                minScore = Mathf.Min(minScore, score);
//                maxScore = Mathf.Max(maxScore, score);
//            }
//        }

//        if (minScore > maxScore)
//        {
//            minScore = 0f;
//            maxScore = 1f;
//        }
//    }
//}

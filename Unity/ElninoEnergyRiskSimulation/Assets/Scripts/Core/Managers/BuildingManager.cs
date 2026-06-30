using CesiumForUnity;
using Codice.CM.Client.Differences.Graphic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NativeBuildingData
{
    public double lon;
    public double lat;
    public float height;
    public float terrainAltitude;
    public float reductionValue;
    public int id;
    public int districtId;
    public int districtType;
    public int buildingType;
    public int isBlackout;
    public int polygonVertexCount;
    public int polygonStartIndex;
}

/// <summary>
/// 셰이더 전용 경량 구조체 (C++ BuildingRenderData와 동일 레이아웃).
/// GPU에는 이 8바이트만 전달된다.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BuildingRenderData
{
    public float reductionValue;   // 수요감축 필요도 (0~1)
    public int isBlackout;       // 정전 여부 (0 or 1)
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeVertex
{
    public float px, py, pz;
    public float nx, ny, nz;
    public float buildingId;
}

public class BuildingManager : MonoBehaviour
{
    [Header("매니저 연결")]
    [SerializeField] private DistrictManager districtManager;
    [SerializeField] private BlackoutSimulationController simulationController;

    public Cesium3DTileset terrainTileset;
    public CesiumGeoreference cesiumGeoreference;
    public Material buildingMaterial;
    private ComputeBuffer renderBuffer;

    private BuildingRenderData[] cachedRenderData;
    private bool bufferDirty;

    private Dictionary<int, (int start, int count)> districtRanges = new();

    public event Action<DistrictObject> OnDistrictObjectCreated;

    public BuildingRenderData[] CachedRenderData => cachedRenderData;

    public void MarkBufferDirty() => bufferDirty = true;

    private Dictionary<int, int[]> sortedDistrictIndices = new();

    [Header("정전 연출 설정")]
    [SerializeField] private int buildingsPerBatch = 100;      // 한 번에 꺼질 건물 수
    [SerializeField] private float secondsBetweenBatch = 0.05f; // 배치 간 딜레이
    private Coroutine blackoutCoroutine;

    public void FlushBufferToGPU()
    {
        if (!bufferDirty || cachedRenderData == null || renderBuffer == null)
            return;

        renderBuffer.SetData(cachedRenderData);
        bufferDirty = false;
    }

    /// <summary>
    /// 전체 건물의 NativeBuildingData를 읽어온다 (구/건물유형 매핑용).
    /// 렌더링과는 무관하며, DistrictManager가 reductionValue 계산 시 참조.
    /// </summary>
    public NativeBuildingData[] GetFullBuildingData()
    {
        int count = GetBuildingBufferCount();
        if (count == 0) return Array.Empty<NativeBuildingData>();

        NativeBuildingData[] data = new NativeBuildingData[count];
        IntPtr ptr = GetBuildingBufferPointer();
        int stride = Marshal.SizeOf(typeof(NativeBuildingData));

        for (int i = 0; i < count; i++)
        {
            IntPtr itemPtr = new IntPtr(ptr.ToInt64() + (i * stride));
            data[i] = Marshal.PtrToStructure<NativeBuildingData>(itemPtr);
        }

        return data;
    }

    // --- C++ DLL 함수 연결 ---
    [DllImport("SeoulBuildingProcessor")]
    private static extern void LoadDistrictData(System.IntPtr dataPointer, int byteLength, int districtId);

    [DllImport("SeoulBuildingProcessor")]
    private static extern void LoadPolygonData(IntPtr dataPointer, int elementCount);

    [DllImport("SeoulBuildingProcessor")]
    private static extern System.IntPtr GetBuildingBufferPointer();

    [DllImport("SeoulBuildingProcessor")]
    private static extern int GetBuildingBufferCount();

    [DllImport("SeoulBuildingProcessor")]
    private static extern void BuildDistrictMesh(int districtId, System.IntPtr terrainHeights, int terrainArrayLength, double centerLon, double centerLat);

    [DllImport("SeoulBuildingProcessor")]
    private static extern System.IntPtr GetChunkVertices();

    [DllImport("SeoulBuildingProcessor")]
    private static extern int GetChunkVertexCount();

    [DllImport("SeoulBuildingProcessor")]
    private static extern System.IntPtr GetChunkIndices();

    [DllImport("SeoulBuildingProcessor")]
    private static extern int GetChunkIndexCount();

    [DllImport("SeoulBuildingProcessor")]
    private static extern int GetDistrictBuildingCount(int districtId);

    [DllImport("SeoulBuildingProcessor")]
    private static extern void GetBuildingPositions(int districtId, [In, Out] double[] lons, [In, Out] double[] lats);

    [DllImport("SeoulBuildingProcessor")]
    private static extern void ClearAllNativeData();

    // ── 렌더링 버퍼 전용 DLL 함수 ──
    [DllImport("SeoulBuildingProcessor")]
    private static extern void BuildRenderingBuffer();

    [DllImport("SeoulBuildingProcessor")]
    private static extern void SetReductionValues([In] float[] values, int count);

    [DllImport("SeoulBuildingProcessor")]
    private static extern IntPtr GetRenderingBufferPointer();

    [DllImport("SeoulBuildingProcessor")]
    private static extern int GetRenderingBufferCount();

    [DllImport("SeoulBuildingProcessor")]
    private static extern bool GetDistrictRange(int districtId, out int startIndex, out int count);


    private void Start()
    {
        // [핵심] 유니티 에디터 메모리에 남아있는 이전 플레이의 C++ 찌꺼기 데이터 초기화
        ClearAllNativeData();

        Debug.Log("[CityManager] Start() - 구역별 건물 데이터 로드 및 메쉬 생성 시작...");
        // PloygonData 고속 로드
        LoadGlobalPolygonBinaryFast();

        // 서울시 25개 구별 건물 데이터(.bytes) 고속 로드
        foreach (DistrictType district in Enum.GetValues(typeof(DistrictType)))
        {
            if (district == DistrictType.None) continue;
            LoadDistrictBinaryFast((int)district);
        }

        // 모든 건물 데이터 로드 후 렌더링 버퍼 구축
        BuildRenderingBuffer();
        InitializeRenderBuffer();
        BuildingDistrictIndexMap();

        StartCoroutine(InitializeDistrict());
    }

    private void OnEnable()
    {
        simulationController.OnBlackoutSimulationToggled += HandleBlackoutSimulationStart;
        simulationController.OnBlackoutDistrictChanged += HandleDistrictBlackedOut;
    }

    private void OnDisable()
    {
        simulationController.OnBlackoutSimulationToggled -= HandleBlackoutSimulationStart;
        simulationController.OnBlackoutDistrictChanged -= HandleDistrictBlackedOut;
    }

    IEnumerator InitializeDistrict()
    {
        // Tileset이 준비될 때까지 대기
        while (terrainTileset == null || !terrainTileset.enabled)
        {
            yield return null;
        }

        yield return new WaitForSeconds(2.0f);
        Debug.Log("[CityManager] Tileset 준비 완료. 구역별 메쉬 생성 시작...");
        // 지연 후 호출
        foreach (DistrictType district in Enum.GetValues(typeof(DistrictType)))
        {
            if (district == DistrictType.None) continue;

            int districtId = (int)district;
            Debug.Log($"[CityManager] 구역 {districtId} 메쉬 생성 시작...");

            var spawnTask = SpawnDistrictChunkAsync(districtId);
            yield return new WaitUntil(() => spawnTask.IsCompleted);
            // break; // 한 번에 하나씩 처리
        }
        //var spawnTask = SpawnDistrictChunkAsync(11680);
        //yield return new WaitUntil(() => spawnTask.IsCompleted);
        Debug.Log("[CityManager] 모든 구역의 메쉬 생성이 완료되었습니다!");
    }

    #region DataLoad
    private void LoadDistrictBinaryFast(int districtId)
    {
        TextAsset binFile = Resources.Load<TextAsset>($"Districts/District_{districtId}");
        if (binFile == null)
        {
            Debug.Log($"[CityManager] 구역 {districtId} 바이너리 파일을 Resources에서 찾을 수 없음.");
            return;
        }

        byte[] rawData = binFile.bytes;
        GCHandle handle = GCHandle.Alloc(rawData, GCHandleType.Pinned);

        try
        {
            LoadDistrictData(handle.AddrOfPinnedObject(), rawData.Length, districtId);
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }
    }

    private void LoadGlobalPolygonBinaryFast()
    {
        TextAsset polyFile = Resources.Load<TextAsset>("Districts/PolygonData");
        if (polyFile == null) return;

        byte[] rawData = polyFile.bytes;
        GCHandle handle = GCHandle.Alloc(rawData, GCHandleType.Pinned);
        try
        {
            LoadPolygonData(handle.AddrOfPinnedObject(), rawData.Length / 4);
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }
    }
    #endregion

    #region MeshSpawn
    private async Task SpawnDistrictChunkAsync(int districtId)
    {
        List<double3> buildingPositions = GetBuildingPositionsFromCpp(districtId);
        float[] heights = new float[buildingPositions.Count];

        if (terrainTileset != null && buildingPositions.Count > 0)
        {
            var result = await terrainTileset.SampleHeightMostDetailed(buildingPositions.ToArray());
            for (int i = 0; i < buildingPositions.Count; i++)
            {
                heights[i] = result.sampleSuccess[i] ? (float)result.longitudeLatitudeHeightPositions[i].z : 0f;
            }
        }

        Vector2 centerCoord = DistrictCoordinates.GetCenter(districtId);

        GCHandle handle = GCHandle.Alloc(heights, GCHandleType.Pinned);
        try
        {
            BuildDistrictMesh(districtId, handle.AddrOfPinnedObject(), heights.Length, centerCoord.x, centerCoord.y);
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }

        ApplyChunkMeshToUnity(districtId);
    }

    private List<double3> GetBuildingPositionsFromCpp(int districtId)
    {
        int count = GetDistrictBuildingCount(districtId);

        double[] lons = new double[count];
        double[] lats = new double[count];

        GetBuildingPositions(districtId, lons, lats);

        List<double3> positions = new List<double3>();
        for (int i = 0; i < count; i++)
        {
            positions.Add(new double3(lons[i], lats[i], 0));
        }
        return positions;
    }

    private void ApplyChunkMeshToUnity(int districtId)
    {
        int vCount = GetChunkVertexCount();
        int iCount = GetChunkIndexCount();

        if (vCount == 0 || iCount == 0) return;

        System.IntPtr vPtr = GetChunkVertices();
        System.IntPtr iPtr = GetChunkIndices();

        float[] rawVertices = new float[vCount * 7];
        Marshal.Copy(vPtr, rawVertices, 0, vCount * 7);

        int[] indices = new int[iCount];
        Marshal.Copy(iPtr, indices, 0, iCount);

        Vector3[] unityVertices = new Vector3[vCount];
        Vector2[] unityUV2 = new Vector2[vCount];

        for (int i = 0; i < vCount; i++)
        {
            int offset = i * 7;
            unityVertices[i] = new Vector3(rawVertices[offset], rawVertices[offset + 1], rawVertices[offset + 2]);
            unityUV2[i] = new Vector2(rawVertices[offset + 6], 0); // 쉐이더 판별용 ID
        }

        Mesh chunkMesh = new Mesh();
        chunkMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        chunkMesh.vertices = unityVertices;
        chunkMesh.uv2 = unityUV2;
        chunkMesh.triangles = indices;

        chunkMesh.RecalculateNormals();
        chunkMesh.RecalculateBounds();

        GameObject chunkObj = new GameObject($"District_Chunk_{districtId}");
        if (cesiumGeoreference != null)
        {
            chunkObj.transform.SetParent(cesiumGeoreference.transform, false);
        }

        chunkObj.AddComponent<MeshFilter>().mesh = chunkMesh;
        MeshRenderer renderer = chunkObj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = buildingMaterial;
        renderer.renderingLayerMask = RenderingLayerMask.GetMask("BUILDING");

        CesiumGlobeAnchor anchor = chunkObj.AddComponent<CesiumGlobeAnchor>();
        Vector2 centerCoord = DistrictCoordinates.GetCenter(districtId);
        anchor.longitudeLatitudeHeight = new Unity.Mathematics.double3(centerCoord.x, centerCoord.y, 0);

        DistrictObject districtObject = chunkObj.AddComponent<DistrictObject>();
        districtObject.districtId = districtId;
        OnDistrictObjectCreated?.Invoke(districtObject);
    }
    #endregion

    #region RenderBuffer
    /// <summary>
    /// 렌더링 전용 ComputeBuffer 초기화.
    /// C++의 renderingBuffer로부터 데이터를 읽어 GPU에 업로드한다.
    /// </summary>
    private void InitializeRenderBuffer()
    {
        int count = GetRenderingBufferCount();
        if (count == 0) return;

        int stride = Marshal.SizeOf(typeof(BuildingRenderData));
        renderBuffer = new ComputeBuffer(count, stride);

        SyncRenderCacheFromNative();
        renderBuffer.SetData(cachedRenderData);
        buildingMaterial.SetBuffer("_BuildingRenderBuffer", renderBuffer);

        Debug.Log($"[CityManager] 렌더링 버퍼 초기화 완료 ({count}개, {stride}바이트/건물)");
    }

    /// <summary>
    /// C++ renderingBuffer → C# 캐시 배열로 복사.
    /// </summary>
    private void SyncRenderCacheFromNative()
    {
        int count = GetRenderingBufferCount();
        IntPtr ptr = GetRenderingBufferPointer();

        if (cachedRenderData == null || cachedRenderData.Length != count)
            cachedRenderData = new BuildingRenderData[count];

        int stride = Marshal.SizeOf(typeof(BuildingRenderData));
        for (int i = 0; i < count; i++)
        {
            IntPtr itemPtr = new IntPtr(ptr.ToInt64() + (i * stride));
            cachedRenderData[i] = Marshal.PtrToStructure<BuildingRenderData>(itemPtr);
        }
    }
    #endregion

    #region BlackoutHandling
    // 프레임마다 GetFullBuildingData()를 호출하지 않도록, 구별 건물 인덱스 매핑을 초기화 시점에 한 번만 수행
    private void BuildingDistrictIndexMap()
    {
        foreach (DistrictType district in Enum.GetValues(typeof(DistrictType)))
        {
            if (district == DistrictType.None) continue;
            int districtId = (int)district;

            if (GetDistrictRange(districtId, out int start, out int count))
            {
                districtRanges[districtId] = (start, count);
                BuildSortedIndices(districtId, start, count);
            }
        }
    }

    private void BuildSortedIndices(int districtId, int start, int count)
    {
        int[] indices = new int[count];
        for (int i = 0; i < count; i++)
            indices[i] = start + i;

        // reductionValue 내림차순 정렬 (물리적 배열은 그대로, 인덱스 순서만 정렬)
        Array.Sort(indices, (a, b) =>
            cachedRenderData[b].reductionValue.CompareTo(cachedRenderData[a].reductionValue));

        sortedDistrictIndices[districtId] = indices;
    }

    /// <summary>
    /// 모든 구역의 정렬된 건물 인덱스를 reductionValue 기준으로 다시 계산한다.
    /// BuildSortedIndices()는 Start() 시점(아직 API의 실제 reductionValue가 도착하기 전,
    /// 즉 전부 0인 상태)에 한 번 호출되므로 그대로 두면 정전이 항상 배열 순서대로 발생한다.
    /// DistrictManager가 실제 reductionValue를 buffer에 반영(ApplyReductionScoresToBuffer)한
    /// 직후 이 메서드를 호출해 정렬을 갱신해야 reductionValue가 높은 건물부터 정확히 꺼진다.
    /// </summary>
    public void RebuildSortedIndices()
    {
        if (districtRanges.Count == 0) return;

        foreach (var kvp in districtRanges)
        {
            BuildSortedIndices(kvp.Key, kvp.Value.start, kvp.Value.count);
        }
    }

    private void HandleBlackoutSimulationStart(bool obj)
    {
        if (!obj)
        {
            // 시뮬레이션 종료 시 모든 구역의 정전 상태를 초기화
            ResetAllBlackoutStates();
        }
    }

    private void HandleDistrictBlackedOut(DistrictType districtType)
    {
        Debug.Log("[BuildingManager] 구역 정전 연출 시작: " + DataConverter.GetDistrictName(districtType));
        int districtId = (int)districtType;
        if (!sortedDistrictIndices.TryGetValue(districtId, out var sortedIndices))
        {
            Debug.LogWarning($"[BuildingManager] '{DataConverter.GetDistrictName(districtType)}' 구의 정렬된 인덱스가 없습니다.");
            return;
        }

        if (blackoutCoroutine != null)
            StopCoroutine(blackoutCoroutine);

        blackoutCoroutine = StartCoroutine(BlackoutSequence(sortedIndices));
    }

    IEnumerator BlackoutSequence(int[] sortedIndices)
    {
        for (int i = 0; i < sortedIndices.Length; i += buildingsPerBatch)
        {
            int end = Mathf.Min(i + buildingsPerBatch, sortedIndices.Length);

            for (int j = i; j < end; j++)
            {
                cachedRenderData[sortedIndices[j]].isBlackout = 1;
            }

            MarkBufferDirty();
            FlushBufferToGPU();

            yield return new WaitForSeconds(secondsBetweenBatch);
        }

        blackoutCoroutine = null;
        Debug.Log("[BuildingManager] 구역 정전 연출 완료");

        // 시각적 정전 연출이 끝났으므로 컨트롤러에 알려 다음 구로 진행시킨다.
        // (기존에는 BlackoutLogger가 로그 출력을 다 마친 뒤 호출했지만,
        //  BlackoutLogger를 씬에서 제거하면서 이 책임을 BuildingManager로 옮김)
        simulationController.NotifyDistrictFinished();
    }

    private void ResetAllBlackoutStates()
    {
        // 진행 중인 정전 연출 코루틴이 있다면 중단
        if (blackoutCoroutine != null)
        {
            StopCoroutine(blackoutCoroutine);
            blackoutCoroutine = null;
        }

        if (cachedRenderData == null || cachedRenderData.Length == 0) return;

        for (int i = 0; i < cachedRenderData.Length; i++)
        {
            cachedRenderData[i].isBlackout = 0;
        }

        MarkBufferDirty();
        FlushBufferToGPU();

        Debug.Log("[BuildingManager] 모든 건물 정전 상태 초기화 완료");
    }
    #endregion

    void OnDestroy()
    {
        if (renderBuffer != null)
        {
            renderBuffer.Release();
        }
    }
}

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

    // ── 렌더링 버퍼 캐시 (C++ renderingBuffer의 C# 사본) ──
    private BuildingRenderData[] _cachedRenderData;
    private bool _bufferDirty;

    private Dictionary<int, List<int>> _districtBuildingIndices = new Dictionary<int, List<int>>();

    // DistirctObject 생성 시 이벤트(DistrictManager에 등록용)
    public event Action<DistrictObject> OnDistrictObjectCreated;

    /// <summary>
    /// GPU에 올라갈 BuildingRenderData 배열.
    /// DistrictManager 등 외부에서 reductionValue를 수정할 때 사용.
    /// 수정 후 MarkBufferDirty() → FlushBufferToGPU() 호출 필요.
    /// </summary>
    public BuildingRenderData[] CachedRenderData => _cachedRenderData;

    /// <summary>
    /// 캐시 데이터가 변경되었음을 표시.
    /// </summary>
    public void MarkBufferDirty() => _bufferDirty = true;

    /// <summary>
    /// 변경된 캐시를 GPU ComputeBuffer에 업로드한다.
    /// </summary>
    public void FlushBufferToGPU()
    {
        if (!_bufferDirty || _cachedRenderData == null || renderBuffer == null)
            return;

        renderBuffer.SetData(_cachedRenderData);
        _bufferDirty = false;
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
    private static extern void LoadDistrictData(System.IntPtr dataPointer, int byteLength);

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
        simulationController.OnDistrictBlackedOut += HandleDistrictBlackoutSequence;
    }

    private void OnDisable()
    {
        simulationController.OnBlackoutSimulationToggled += HandleBlackoutSimulationStart;
        simulationController.OnDistrictBlackedOut -= HandleDistrictBlackoutSequence;
    }

    private void HandleBlackoutSimulationStart(bool obj)
    {
        if (!obj)
        {
            // 시뮬레이션 종료 시 모든 구역의 정전 상태를 초기화
            ResetAllBlackoutStates();
        }
    }

    private void HandleDistrictBlackoutSequence(DistrictType districtType, double consumption)
    {
        
    }
    private void ResetAllBlackoutStates()
    {

    }
    private void ApplyBlackoutToDistrict(DistrictType districtType)
    {
        throw new NotImplementedException();
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
            break; // 한 번에 하나씩 처리
        }

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
            LoadDistrictData(handle.AddrOfPinnedObject(), rawData.Length);
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
        renderBuffer.SetData(_cachedRenderData);
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

        if (_cachedRenderData == null || _cachedRenderData.Length != count)
            _cachedRenderData = new BuildingRenderData[count];

        int stride = Marshal.SizeOf(typeof(BuildingRenderData));
        for (int i = 0; i < count; i++)
        {
            IntPtr itemPtr = new IntPtr(ptr.ToInt64() + (i * stride));
            _cachedRenderData[i] = Marshal.PtrToStructure<BuildingRenderData>(itemPtr);
        }
    }
    #endregion

    #region BlackoutHandling
    // 프레임마다 GetFullBuildingData()를 호출하지 않도록, 구별 건물 인덱스 매핑을 초기화 시점에 한 번만 수행
    private void BuildingDistrictIndexMap()
    {
        NativeBuildingData[] allBuildings = GetFullBuildingData();

        for (int i = 0; i < allBuildings.Length; i++)
        {
            int districtId = allBuildings[i].districtId;
            if (!_districtBuildingIndices.TryGetValue(districtId, out var list))
            {
                list = new List<int>();
                _districtBuildingIndices[districtId] = list;
            }
            list.Add(i);
        }

        Debug.Log($"[BuildingManager] 구별 건물 인덱스 매핑 완료 ({_districtBuildingIndices.Count}개 구)");
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

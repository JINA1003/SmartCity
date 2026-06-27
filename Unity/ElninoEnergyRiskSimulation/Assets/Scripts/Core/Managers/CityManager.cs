using CesiumForUnity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct NativeBuildingData
{
    public int id;
    public int districtId;
    public double lon;
    public double lat;
    public float height;
    public float demandPriority;
    public int isBlackout;
    public int padding;
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeVertex
{
    public float px, py, pz;
    public float nx, ny, nz;
    public float buildingId;
}

public class CityManager : MonoBehaviour
{
    public Cesium3DTileset terrainTileset;
    public CesiumGeoreference cesiumGeoreference;
    public Material blackoutMaterial;
    private ComputeBuffer blackoutBuffer;

    // --- C++ DLL 함수 연결 ---
    [DllImport("SeoulBuildingProcessor")]
    private static extern int LoadDistrictData(System.IntPtr dataPointer, int byteLength);

    [DllImport("SeoulBuildingProcessor")]
    private static extern void LoadPolygonData(IntPtr dataPointer, int elementCount);

    [DllImport("SeoulBuildingProcessor")]
    private static extern void TriggerBlackoutForDistrict(int targetDistrictId, int targetCount);

    [DllImport("SeoulBuildingProcessor")]
    private static extern System.IntPtr GetBuildingBufferPointer();

    [DllImport("SeoulBuildingProcessor")]
    private static extern int GetBuildingBufferCount();

    [DllImport("SeoulBuildingProcessor")]
    private static extern void BuildDistrictMesh(int districtId, System.IntPtr terrainHeights, int terrainArrayLength);

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

    private void Start()
    {
        Debug.Log("[CityManager] Start() - 구역별 건물 데이터 로드 및 메쉬 생성 시작...");
        // PloygonData 고속 로드
        LoadGlobalPolygonBinaryFast();

        // 서울시 25개 구별 건물 데이터(.bytes) 고속 로드
        foreach (DistrictType district in Enum.GetValues(typeof(DistrictType)))
        {
            if (district == DistrictType.None) continue;
            LoadDistrictBinaryFast((int)district);
        }

        StartCoroutine(InitializeDistrict());

        // 2. 렌더링용 Compute Buffer 초기화
        // InitializeComputeBuffer();
    }

    IEnumerator InitializeDistrict()
    {
        // Tileset이 준비될 때까지 대기
        while (terrainTileset == null || !terrainTileset.enabled)
        {
            yield return null;
        }

        Debug.Log("[CityManager] Tileset 준비 완료. 구역별 메쉬 생성 시작...");
        // 지연 후 호출
        yield return SpawnDistrictChunkAsync(11110);
    }

    // NativePlugin으로 데이터를 전달하기 위해 C# 배열을 핀(Pin) 고정하여 C++로 전달하는 방식으로 구현
    #region DataLoad
    // 구역별 바이너리(.bytes) 파일을 Resources에서 고속 로드하여 C++로 전달
    private void LoadDistrictBinaryFast(int districtId)
    {
        TextAsset binFile = Resources.Load<TextAsset>($"Districts/District_{districtId}");
        if (binFile == null)
        {
            Debug.Log($"[CityManager] 구역 {districtId} 바이너리 파일을 Resources에서 찾을 수 없음.");
            return;
        }

        byte[] rawData = binFile.bytes;
        // C# 배열을 핀(Pin) 고정하여 C++로 전달(배열이 이동해서 C++에서 잘못 읽는 문제 방지)
        GCHandle handle = GCHandle.Alloc(rawData, GCHandleType.Pinned);

        try
        {
            // C++ 구역별 건물 데이터 로드
            int result = LoadDistrictData(handle.AddrOfPinnedObject(), rawData.Length);
            Debug.Log(result);
        }
        finally
        {
            // 핀 고정 해제
            if (handle.IsAllocated) handle.Free();
        }
        
        // 전달 됐음
    }

    private void LoadGlobalPolygonBinaryFast()
    {
        TextAsset polyFile = Resources.Load<TextAsset>("Districts/PolygonData");
        if (polyFile == null) return;

        byte[] rawData = polyFile.bytes;
        GCHandle handle = GCHandle.Alloc(rawData, GCHandleType.Pinned);
        try
        {
            // 8바이트(double) 단위이므로 요소 개수는 길이 / 8
            LoadPolygonData(handle.AddrOfPinnedObject(), rawData.Length / 8);
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
        // 1. C++에서 해당 구역의 건물 위경도(lon, lat) 목록만 임시로 받아옴
        List<double3> buildingPositions = GetBuildingPositionsFromCpp(districtId);
        // 건물 위치 개수만큼 지형 고도 배열 생성
        float[] heights = new float[buildingPositions.Count];

        // 2. Cesium API를 호출하여 건물 위치들의 지형 고도를 한 번에 고속 측정
        if (terrainTileset != null && buildingPositions.Count > 0)
        {
            var result = await terrainTileset.SampleHeightMostDetailed(buildingPositions.ToArray());
            for (int i = 0; i < buildingPositions.Count; i++)
            {
                // 지형 측정이 실패하면 기본값 0, 성공하면 해당 고도
                heights[i] = result.sampleSuccess[i] ? (float)result.longitudeLatitudeHeightPositions[i].z : 0f;
            }
        }

        // 3. 측정된 지형 고도 배열을 핀(Pin) 고정하여 C++로 전달
        GCHandle handle = GCHandle.Alloc(heights, GCHandleType.Pinned);
        try
        {
            // C++ 공장 가동: 지형 높이를 반영하여 거대 메쉬 생성
            BuildDistrictMesh(districtId, handle.AddrOfPinnedObject(), heights.Length);
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }

        // 4. C++이 완성한 거대 메쉬 데이터를 유니티 Mesh로 가져오기
        ApplyChunkMeshToUnity(districtId);
    }

    // C++에서 해당 구역의 건물 위경도(lon, lat) 목록을 가져와 Cesium에서 처리 가능한 형식으로 변환
    private List<double3> GetBuildingPositionsFromCpp(int districtId)
    {
        int count = GetDistrictBuildingCount(districtId);
        double[] lons = new double[count];
        double[] lats = new double[count];

        // C++에서 데이터 가져오기 (포인터로 가져오기 떄문에 lons와 lats 배열이 채워짐)
        GetBuildingPositions(districtId, lons, lats);

        // Cesium에서 처리 가능한 형식으로 변환
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

        // C++ 메모리 복사
        NativeVertex[] vertices = new NativeVertex[vCount];
        int[] indices = new int[iCount];

        System.IntPtr vPtr = GetChunkVertices();
        System.IntPtr iPtr = GetChunkIndices();

        // 고속 복사 로직 (NativeArray 래핑 등 마샬링 최적화 가능)
        int vStride = Marshal.SizeOf(typeof(NativeVertex));
        for (int i = 0; i < vCount; i++)
        {
            vertices[i] = Marshal.PtrToStructure<NativeVertex>(vPtr + (i * vStride));
        }
        Marshal.Copy(iPtr, indices, 0, iCount);

        // Unity Mesh 생성 (60만 개가 아닌, 구역당 딱 1개)
        Vector3[] unityVertices = new Vector3[vCount];
        Vector2[] unityUV2 = new Vector2[vCount]; // UV2 채널에 buildingId 저장

        for (int i = 0; i < vCount; i++)
        {
            // Cesium 좌표계 등 축 방향에 따라 y, z 스왑이 필요할 수 있습니다.
            unityVertices[i] = new Vector3(vertices[i].px, vertices[i].pz, vertices[i].py);
            unityUV2[i] = new Vector2(vertices[i].buildingId, 0); // 쉐이더 판별용 ID
        }

        Mesh chunkMesh = new Mesh();
        chunkMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // 버텍스 65535개 이상 허용
        chunkMesh.vertices = unityVertices;
        chunkMesh.uv2 = unityUV2;
        chunkMesh.triangles = indices;
        chunkMesh.RecalculateNormals();
        chunkMesh.RecalculateBounds();

        // 씬에 배치
        GameObject chunkObj = new GameObject($"District_Chunk_{districtId}");

        if (cesiumGeoreference != null)
        {
            chunkObj.transform.SetParent(cesiumGeoreference.transform, false);
        }
        else
        {
            // 인스펙터 할당을 깜빡했을 때를 대비한 방어 코드
            CesiumGeoreference geo = FindFirstObjectByType<CesiumGeoreference>();
            if (geo != null)
            {
                cesiumGeoreference = geo;
                chunkObj.transform.SetParent(geo.transform, false);
            }
            else
            {
                Debug.LogError("[CityManager] 씬에 CesiumGeoreference가 존재하지 않습니다!");
            }
        }

        chunkObj.AddComponent<MeshFilter>().mesh = chunkMesh;

        MeshRenderer renderer = chunkObj.AddComponent<MeshRenderer>();
        renderer.material = blackoutMaterial;

        // --- [추가] CesiumGlobeAnchor 부착 및 좌표 맵핑 ---
        CesiumGlobeAnchor anchor = chunkObj.AddComponent<CesiumGlobeAnchor>();

        // 해당 구역(District)의 중심 위도, 경도를 알아야 합니다. (임시로 서울 시청 좌표 예시)
        Vector2 centerCoord = DistrictCoordinates.GetCenter(districtId);

        // 고도는 0으로 둡니다 (메쉬의 버텍스 Y값에 지형 고도(baseZ)가 이미 더해져 있으므로)
        anchor.longitudeLatitudeHeight = new Unity.Mathematics.double3(centerCoord.x, centerCoord.y, 0);

        // (선택) 이전 답변의 Compute Buffer 업데이트 로직 호출하여 블랙아웃 준비
    }
    #endregion
    private void InitializeComputeBuffer()
    {
        int count = GetBuildingBufferCount();
        if (count == 0) return;

        // C# 구조체 크기(40바이트 - double 2개 포함)만큼 버퍼 할당
        int stride = Marshal.SizeOf(typeof(NativeBuildingData));
        blackoutBuffer = new ComputeBuffer(count, stride);

        UpdateShaderBuffer();
    }

    private void UpdateShaderBuffer()
    {
        int count = GetBuildingBufferCount();
        System.IntPtr cppPointer = GetBuildingBufferPointer();

        // 1. C++ 배열 크기만큼 C# 임시 배열 생성
        NativeBuildingData[] tempArray = new NativeBuildingData[count];

        // 2. C++ 메모리를 C# 배열로 통째로 복사 (매우 빠름)
        // 만약 C# 10.0+ 및 unsafe 코드를 쓴다면 복사 없이 NativeArray로 래핑하여 더 빠르게 처리 가능합니다.
        int stride = Marshal.SizeOf(typeof(NativeBuildingData));
        for (int i = 0; i < count; i++)
        {
            System.IntPtr itemPtr = new System.IntPtr(cppPointer.ToInt64() + (i * stride));
            tempArray[i] = Marshal.PtrToStructure<NativeBuildingData>(itemPtr);
        }

        // 3. GPU로 쏘기
        blackoutBuffer.SetData(tempArray);
        blackoutMaterial.SetBuffer("_BuildingDataBuffer", blackoutBuffer);
    }

    // 외부에서 특정 구역의 정전을 지시할 때 호출 (파도타기 연출)
    public void StartBlackoutSimulation(int districtId)
    {
        StartCoroutine(SimulateBlackoutSequence(districtId));
    }

    private IEnumerator SimulateBlackoutSequence(int districtId)
    {
        int totalToTurnOff = 10000; // 꺼야할 총 개수 (예시)
        int perFrame = 300;         // 한 프레임당 300개씩 순차 정전

        while (totalToTurnOff > 0)
        {
            int turnOffThisFrame = Mathf.Min(totalToTurnOff, perFrame);

            // C++ 연산 (상태 업데이트)
            TriggerBlackoutForDistrict(districtId, turnOffThisFrame);

            // GPU 쉐이더 즉시 반영
            UpdateShaderBuffer();

            totalToTurnOff -= turnOffThisFrame;
            yield return null;
        }
    }

    void OnDestroy()
    {
        if (blackoutBuffer != null)
        {
            blackoutBuffer.Release();
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// District_Chunk 메쉬에 건물 용도별 전력 소비 색상을 적용하는 매니저.
/// BuildingUsage 셰이더(SmartCity/BuildingUsage)와 연동되며,
/// 팔레트·랭크맵 두 텍스처를 GPU에 업로드해 색상을 구동한다.
/// </summary>
public class BuildingUsageColorManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════
    // Inspector 필드
    // ═══════════════════════════════════════════════════════════════════════

    [Header("References")]
    [SerializeField] private DistrictManager districtManager;
    [SerializeField] private DataManager dataManager;
    // null이면 런타임에 SmartCity/BuildingUsage 셰이더로 자동 생성
    [SerializeField] private Material buildingUsageMaterial;

    [Header("Chunk Detection")]
    [SerializeField] private string districtChunkNamePrefix = "District_Chunk_";
    [SerializeField] private int expectedDistrictCount = 25;
    // 청크 수가 이 프레임 수 동안 변화 없으면 생성 완료로 판단
    [SerializeField] private int stableFrameCount = 60;

    // ═══════════════════════════════════════════════════════════════════════
    // 상태
    // ═══════════════════════════════════════════════════════════════════════

    private readonly Dictionary<int, MeshRenderer> districtRenderers = new Dictionary<int, MeshRenderer>();
    private MaterialPropertyBlock propertyBlock;
    private readonly Dictionary<int, int> buildingIdToUsageCategory = new Dictionary<int, int>();
    private Dictionary<DistrictType, DistrictData> latestDistrictData;

    private Texture2D paletteTexture;
    private Texture2D rankMapTexture;
    private Coroutine applyCoroutine;

    // ═══════════════════════════════════════════════════════════════════════
    // Unity 생명주기
    // ═══════════════════════════════════════════════════════════════════════

    private void OnEnable()
    {
        if (districtManager == null)
            districtManager = FindFirstObjectByType<DistrictManager>();
        if (dataManager == null)
            dataManager = FindFirstObjectByType<DataManager>();

        if (districtManager != null)
            districtManager.OnAllDistrictsDataUpdated += HandleAllDistrictsDataUpdated;

        if (dataManager != null)
        {
            dataManager.OnDistrictDataUpdated += HandleDistrictDataUpdated;
            dataManager.OnAllDistrictsParsed += HandleAllDistrictsParsed;
        }
    }

    private void OnDisable()
    {
        if (districtManager != null)
            districtManager.OnAllDistrictsDataUpdated -= HandleAllDistrictsDataUpdated;

        if (dataManager != null)
        {
            dataManager.OnDistrictDataUpdated -= HandleDistrictDataUpdated;
            dataManager.OnAllDistrictsParsed -= HandleAllDistrictsParsed;
        }
    }

    private void Start()
    {
        propertyBlock = new MaterialPropertyBlock();
        BuildBuildingUsageLookupFromResources();
        InitializeDefaultUsageTextures();
        applyCoroutine = StartCoroutine(WaitAndApplyUsageColors());
    }

    private void OnDestroy()
    {
        if (applyCoroutine != null)
            StopCoroutine(applyCoroutine);

        if (paletteTexture != null)
            Destroy(paletteTexture);

        if (rankMapTexture != null)
            Destroy(rankMapTexture);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 이벤트 핸들러
    // ═══════════════════════════════════════════════════════════════════════

    private void HandleDistrictDataUpdated(DistrictData data)
    {
        latestDistrictData ??= new Dictionary<DistrictType, DistrictData>();
        latestDistrictData[data.districtType] = data;
    }

    private void HandleAllDistrictsParsed()
    {
        if (latestDistrictData != null && latestDistrictData.Count > 0)
            HandleAllDistrictsDataUpdated(latestDistrictData);
    }

    private void HandleAllDistrictsDataUpdated(Dictionary<DistrictType, DistrictData> districts)
    {
        UpdateUsageColorTextures(districts);
        ApplyUsageColorsToKnownDistricts();
        Debug.Log("[BuildingUsageColorManager] 구·용도별 100칸 색상 맵 갱신 완료.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 초기화 — 청크 대기 및 UV2 패치
    // ═══════════════════════════════════════════════════════════════════════

    // CityManager가 District_Chunk를 비동기로 생성하므로,
    // 청크 수가 안정될 때까지 매 프레임 폴링한 뒤 일괄 처리한다.
    private IEnumerator WaitAndApplyUsageColors()
    {
        yield return WaitForDistrictChunks();

        CacheDistrictRenderers();
        PatchDistrictChunkUv2();
        ApplyUsageColorsToKnownDistricts();

        if (latestDistrictData != null && latestDistrictData.Count > 0)
            HandleAllDistrictsDataUpdated(latestDistrictData);
    }

    private IEnumerator WaitForDistrictChunks()
    {
        int lastCount = 0;
        int stableFrames = 0;

        while (stableFrames < stableFrameCount)
        {
            int count = CountDistrictChunks();
            if (count >= expectedDistrictCount || (count > 0 && count == lastCount))
                stableFrames++;
            else
                stableFrames = 0;

            lastCount = count;
            yield return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 청크 탐색
    // ═══════════════════════════════════════════════════════════════════════

    private int CountDistrictChunks()
    {
        int count = 0;
        foreach (Transform root in SceneRoots())
            count += CountChunksUnder(root);

        return count;
    }

    private int CountChunksUnder(Transform node)
    {
        int count = 0;
        if (node.name.StartsWith(districtChunkNamePrefix, StringComparison.Ordinal))
            count++;

        for (int i = 0; i < node.childCount; i++)
            count += CountChunksUnder(node.GetChild(i));

        return count;
    }

    private void CacheDistrictRenderers()
    {
        districtRenderers.Clear();

        foreach (Transform root in SceneRoots())
            CacheDistrictRenderersUnder(root);
    }

    private void CacheDistrictRenderersUnder(Transform node)
    {
        if (node.name.StartsWith(districtChunkNamePrefix, StringComparison.Ordinal))
        {
            string suffix = node.name.Substring(districtChunkNamePrefix.Length);
            if (int.TryParse(suffix, out int districtId))
            {
                MeshRenderer renderer = node.GetComponent<MeshRenderer>();
                if (renderer != null)
                    districtRenderers[districtId] = renderer;
            }
        }

        for (int i = 0; i < node.childCount; i++)
            CacheDistrictRenderersUnder(node.GetChild(i));
    }

    private static IEnumerable<Transform> SceneRoots()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.isLoaded) yield break;

        foreach (GameObject root in scene.GetRootGameObjects())
            yield return root.transform;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UV2 패치 — 건물 ID → 용도 카테고리 인덱스 기록
    // ═══════════════════════════════════════════════════════════════════════

    // CityManager가 UV2.x에 buildingId만 기록한 상태로 메쉬를 생성한다.
    // 여기서 UV2.y에 용도 카테고리(0~3)를 추가 기록해 셰이더가 읽을 수 있게 한다.
    private void PatchDistrictChunkUv2()
    {
        foreach (var kvp in districtRenderers)
        {
            MeshFilter meshFilter = kvp.Value.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            Mesh mesh = meshFilter.sharedMesh;
            Vector2[] uv2 = mesh.uv2;
            if (uv2 == null || uv2.Length == 0)
                continue;

            for (int i = 0; i < uv2.Length; i++)
            {
                int buildingId = Mathf.RoundToInt(uv2[i].x);
                if (buildingIdToUsageCategory.TryGetValue(buildingId, out int usageCategory))
                    uv2[i] = new Vector2(buildingId, usageCategory);
            }

            mesh.uv2 = uv2;

            // 버텍스 색 방식 대안 (현재 미사용)
            // API 갱신마다 CPU에서 전체 버텍스 색을 재계산해야 해서 GPU 텍스처 방식으로 대체됨.
            /*
            ApplyVertexColorsByUsageRank(mesh, uv2, kvp.Key);
            */
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 리소스 로드 — 바이너리 건물 데이터 → 용도 카테고리 룩업 테이블 구축
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildBuildingUsageLookupFromResources()
    {
        buildingIdToUsageCategory.Clear();

        foreach (DistrictType district in BuildingUsageMapper.GetOrderedDistricts())
        {
            TextAsset binFile = Resources.Load<TextAsset>($"Districts/District_{(int)district}");
            if (binFile == null)
                continue;

            ReadBuildingUsageFromBytes(binFile.bytes);
        }
    }

    private void ReadBuildingUsageFromBytes(byte[] rawData)
    {
        int structSize = Marshal.SizeOf(typeof(NativeBuildingData));
        if (structSize <= 0 || rawData.Length < structSize)
            return;

        int buildingCount = rawData.Length / structSize;
        GCHandle handle = GCHandle.Alloc(rawData, GCHandleType.Pinned);

        try
        {
            IntPtr basePtr = handle.AddrOfPinnedObject();
            for (int i = 0; i < buildingCount; i++)
            {
                IntPtr itemPtr = IntPtr.Add(basePtr, i * structSize);
                NativeBuildingData building = Marshal.PtrToStructure<NativeBuildingData>(itemPtr);
                int usageCategory = BuildingUsageMapper.GetUsageCategoryIndex((BuildingType)building.buildingType);
                buildingIdToUsageCategory[building.id] = usageCategory;
            }
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 텍스처 생성 및 GPU 업로드
    // ═══════════════════════════════════════════════════════════════════════

    // 데이터 수신 전 기본 상태: 팔레트 중간값(회색 계열)으로 초기화
    private void InitializeDefaultUsageTextures()
    {
        paletteTexture = BuildingUsageColormap.CreatePaletteTexture();
        int[] neutralRanks = new int[BuildingUsageColorService.CellCount];
        Array.Fill(neutralRanks, BuildingUsageColormap.PaletteSize / 2);
        rankMapTexture = BuildingUsageColorService.CreateRankMapTexture(neutralRanks);
    }

    private void UpdateUsageColorTextures(IReadOnlyDictionary<DistrictType, DistrictData> districts)
    {
        paletteTexture = BuildingUsageColormap.CreatePaletteTexture();

        // 방식 A: 25구 × 4용도 = 100칸을 사용량 순위로 줄 세워 팔레트 인덱스 부여 (현재 사용)
        int[] cellPaletteIndices = BuildingUsageColorService.ComputeCellPaletteIndicesRanked(districts);

        // 방식 B: MWh 값을 min~max 구간에 연속 매핑 (빈 구간 발생 가능)
        // int[] cellPaletteIndices = BuildingUsageColorService.ComputeCellPaletteIndicesContinuous(districts);

        rankMapTexture = BuildingUsageColorService.CreateRankMapTexture(cellPaletteIndices);
    }

    private void ApplyUsageColorsToKnownDistricts()
    {
        Material material = ResolveBuildingMaterial();
        if (material == null || paletteTexture == null || rankMapTexture == null)
            return;

        material.SetTexture("_ColorPalette", paletteTexture);
        material.SetTexture("_RankMap", rankMapTexture);

        foreach (var kvp in districtRenderers)
        {
            int districtIndex = BuildingUsageMapper.GetDistrictIndex((DistrictType)kvp.Key);
            MeshRenderer renderer = kvp.Value;

            if (renderer.sharedMaterial != material)
                renderer.sharedMaterial = material;

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat("_DistrictIndex", districtIndex);
            propertyBlock.SetTexture("_ColorPalette", paletteTexture);
            propertyBlock.SetTexture("_RankMap", rankMapTexture);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    // Inspector에서 Material이 연결되지 않으면 셰이더로 런타임 생성
    private Material ResolveBuildingMaterial()
    {
        if (buildingUsageMaterial != null)
            return buildingUsageMaterial;

        Shader shader = Shader.Find("SmartCity/BuildingUsage");
        if (shader == null)
            return null;

        buildingUsageMaterial = new Material(shader) { name = "M_BuildingUsage_Runtime" };
        return buildingUsageMaterial;
    }

    /*
    private void ApplyVertexColorsByUsageRank(Mesh mesh, Vector2[] uv2, int districtId, int[] rankPaletteIndices)
    {
        var colors = new Color[uv2.Length];
        for (int i = 0; i < uv2.Length; i++)
        {
            int usageCategory = Mathf.RoundToInt(uv2[i].y);
            int cellIndex = BuildingUsageMapper.GetDistrictIndex((DistrictType)districtId) * 4 + usageCategory;
            int paletteIndex = rankPaletteIndices[cellIndex];
            float t = paletteIndex / (float)(BuildingUsageColormap.PaletteSize - 1);
            colors[i] = BuildingUsageColormap.Evaluate(t);
        }

        mesh.colors = colors;
    }
    */
}

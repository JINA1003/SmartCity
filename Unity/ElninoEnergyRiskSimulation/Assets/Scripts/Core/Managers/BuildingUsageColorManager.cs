using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// District_Chunk 메쉬에 수요감축 필요도 기반 색상을 적용한다.
/// UV2.y에 정규화 점수(0~1)를 기록하고, _ColorPalette 텍스처를 머티리얼에 넘긴다.
/// 셰이더(HLSL) 쪽 구현은 별도 담당.
/// </summary>
public class BuildingUsageColorManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DistrictManager districtManager;
    [SerializeField] private DataManager dataManager;
    [SerializeField] private Material buildingUsageMaterial;

    [Header("Chunk Detection")]
    [SerializeField] private string districtChunkNamePrefix = "District_Chunk_";
    [SerializeField] private int expectedDistrictCount = 25;
    [SerializeField] private int stableFrameCount = 60;

    private readonly Dictionary<int, MeshRenderer> districtRenderers = new();
    private readonly Dictionary<int, BuildingType> buildingIdToType = new();
    private Dictionary<DistrictType, DistrictData> latestDistrictData;

    private MaterialPropertyBlock propertyBlock;
    private Texture2D paletteTexture;
    private Coroutine applyCoroutine;

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
        BuildBuildingTypeLookupFromResources();
        paletteTexture = ReductionColormap.CreatePaletteTexture();
        applyCoroutine = StartCoroutine(WaitAndApplyColors());
    }

    private void OnDestroy()
    {
        if (applyCoroutine != null)
            StopCoroutine(applyCoroutine);

        if (paletteTexture != null)
            Destroy(paletteTexture);
    }

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
        PatchDistrictChunkUv2(districts);
        ApplyColorsToKnownDistricts();
        Debug.Log("[BuildingUsageColorManager] 수요감축 필요도 색상 적용 완료.");
    }

    private IEnumerator WaitAndApplyColors()
    {
        yield return WaitForDistrictChunks();

        CacheDistrictRenderers();

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

    private void BuildBuildingTypeLookupFromResources()
    {
        buildingIdToType.Clear();

        foreach (DistrictType district in Enum.GetValues(typeof(DistrictType)))
        {
            if (district == DistrictType.None) continue;

            TextAsset binFile = Resources.Load<TextAsset>($"Districts/District_{(int)district}");
            if (binFile == null) continue;

            ReadBuildingTypesFromBytes(binFile.bytes);
        }
    }

    private void ReadBuildingTypesFromBytes(byte[] rawData)
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
                buildingIdToType[building.id] = (BuildingType)building.buildingType;
            }
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }

    private void PatchDistrictChunkUv2(IReadOnlyDictionary<DistrictType, DistrictData> districts)
    {
        GetScoreRange(districts, out float minScore, out float maxScore);

        foreach (var kvp in districtRenderers)
        {
            if (!districts.TryGetValue((DistrictType)kvp.Key, out DistrictData districtData))
                continue;

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
                float normalized = GetNormalizedScore(
                    buildingId, districtData, minScore, maxScore);

                uv2[i] = new Vector2(uv2[i].x, normalized);
            }

            mesh.uv2 = uv2;
        }
    }

    private static void GetScoreRange(
        IReadOnlyDictionary<DistrictType, DistrictData> districts,
        out float minScore, out float maxScore)
    {
        minScore = float.MaxValue;
        maxScore = float.MinValue;

        foreach (DistrictData data in districts.Values)
        {
            if (data?.buildingReductionScores == null) continue;

            foreach (float score in data.buildingReductionScores.Values)
            {
                minScore = Mathf.Min(minScore, score);
                maxScore = Mathf.Max(maxScore, score);
            }
        }

        if (minScore > maxScore)
        {
            minScore = 0f;
            maxScore = 1f;
        }
    }

    private float GetNormalizedScore(
        int buildingId, DistrictData districtData, float minScore, float maxScore)
    {
        if (!buildingIdToType.TryGetValue(buildingId, out BuildingType buildingType))
            return 0f;

        if (districtData.buildingReductionScores == null
            || !districtData.buildingReductionScores.TryGetValue(buildingType, out float score))
            return 0f;

        if (maxScore <= minScore)
            return 0f;

        return Mathf.Clamp01((score - minScore) / (maxScore - minScore));
    }

    private void ApplyColorsToKnownDistricts()
    {
        Material material = ResolveBuildingMaterial();
        if (material == null || paletteTexture == null)
            return;

        material.SetTexture("_ColorPalette", paletteTexture);

        foreach (MeshRenderer renderer in districtRenderers.Values)
        {
            if (renderer.sharedMaterial != material)
                renderer.sharedMaterial = material;

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetTexture("_ColorPalette", paletteTexture);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

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
}

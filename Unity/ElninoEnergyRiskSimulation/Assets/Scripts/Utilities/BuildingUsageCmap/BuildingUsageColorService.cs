using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 25구 × 4용도 = 100칸에 팔레트 인덱스를 계산하고 RankMap 텍스처를 생성한다.
/// 색상 매핑 방식은 Ranked(순위 기반)와 Continuous(연속 그라데이션) 두 가지를 제공한다.
/// </summary>
public static class BuildingUsageColorService
{
    // ═══════════════════════════════════════════════════════════════════════
    // 상수
    // ═══════════════════════════════════════════════════════════════════════

    public const int CellCount = BuildingUsageMapper.UsageCategoryCount * 25;

    // ═══════════════════════════════════════════════════════════════════════
    // 팔레트 인덱스 계산
    // ═══════════════════════════════════════════════════════════════════════

    // 방식 A: 서울 전체 100칸을 사용량 내림차순으로 줄 세운 뒤 0(최대)~99(최소) 팔레트 인덱스 부여.
    // 모든 칸이 팔레트 전 구간을 고르게 사용하므로 색 대비가 명확하다.
    public static int[] ComputeCellPaletteIndicesRanked(IReadOnlyDictionary<DistrictType, DistrictData> districts)
    {
        var cells = new List<UsageCell>(CellCount);

        foreach (DistrictType districtType in BuildingUsageMapper.GetOrderedDistricts())
        {
            int districtIndex = BuildingUsageMapper.GetDistrictIndex(districtType);
            districts.TryGetValue(districtType, out DistrictData districtData);

            for (int usageIndex = 0; usageIndex < BuildingUsageMapper.UsageCategoryCount; usageIndex++)
            {
                float mwh = districtData != null
                    ? BuildingUsageMapper.GetUsageMwh(districtData, usageIndex)
                    : 0f;

                cells.Add(new UsageCell
                {
                    CellIndex = districtIndex * BuildingUsageMapper.UsageCategoryCount + usageIndex,
                    ConsumptionMwh = mwh,
                });
            }
        }

        cells.Sort((a, b) => b.ConsumptionMwh.CompareTo(a.ConsumptionMwh));

        var paletteIndices = new int[CellCount];
        Array.Fill(paletteIndices, PaletteSize / 2);

        int assignedCount = Mathf.Min(cells.Count, PaletteSize);
        for (int rank = 0; rank < assignedCount; rank++)
        {
            // rank 0 = 최대 사용량 → 팔레트 99(빨강), rank 99 = 최소 → 팔레트 0(흰/베이지)
            int paletteIndex = PaletteSize - 1 - rank;
            paletteIndices[cells[rank].CellIndex] = paletteIndex;
        }

        return paletteIndices;
    }

    /*
     * 방식 B: MWh 값을 전체 100칸 min~max 구간에 연속 매핑.
     * 소비량 차이가 클 때 빈 팔레트 구간이 생길 수 있어 색 대비가 불균일해질 수 있다.
     *
    public static int[] ComputeCellPaletteIndicesContinuous(IReadOnlyDictionary<DistrictType, DistrictData> districts)
    {
        float[] consumptions = new float[CellCount];
        float min = float.MaxValue;
        float max = float.MinValue;

        foreach (DistrictType districtType in BuildingUsageMapper.GetOrderedDistricts())
        {
            int districtIndex = BuildingUsageMapper.GetDistrictIndex(districtType);
            districts.TryGetValue(districtType, out DistrictData districtData);

            for (int usageIndex = 0; usageIndex < BuildingUsageMapper.UsageCategoryCount; usageIndex++)
            {
                int cellIndex = districtIndex * BuildingUsageMapper.UsageCategoryCount + usageIndex;
                float mwh = districtData != null
                    ? BuildingUsageMapper.GetUsageMwh(districtData, usageIndex)
                    : 0f;

                consumptions[cellIndex] = mwh;
                min = Mathf.Min(min, mwh);
                max = Mathf.Max(max, mwh);
            }
        }

        var paletteIndices = new int[CellCount];
        float range = Mathf.Max(max - min, 0.0001f);

        for (int i = 0; i < CellCount; i++)
        {
            float normalized = (consumptions[i] - min) / range;
            paletteIndices[i] = Mathf.RoundToInt(normalized * (PaletteSize - 1));
        }

        return paletteIndices;
    }
    */

    // ═══════════════════════════════════════════════════════════════════════
    // 텍스처 생성
    // ═══════════════════════════════════════════════════════════════════════

    // 100칸 팔레트 인덱스를 R8 포맷 1D 텍스처(100x1)로 인코딩한다.
    // 셰이더는 cellIndex를 u좌표로 샘플링해 팔레트 인덱스(0~1 정규화)를 읽는다.
    public static Texture2D CreateRankMapTexture(int[] cellPaletteIndices)
    {
        var texture = new Texture2D(CellCount, 1, TextureFormat.R8, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
            name = "BuildingUsageRankMap",
        };

        for (int i = 0; i < CellCount; i++)
        {
            int paletteIndex = i < cellPaletteIndices.Length
                ? Mathf.Clamp(cellPaletteIndices[i], 0, PaletteSize - 1)
                : PaletteSize / 2;

            float normalized = paletteIndex / (float)(PaletteSize - 1);
            texture.SetPixel(i, 0, new Color(normalized, 0f, 0f, 1f));
        }

        texture.Apply();
        return texture;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 내부
    // ═══════════════════════════════════════════════════════════════════════

    private static int PaletteSize => BuildingUsageColormap.PaletteSize;

    private struct UsageCell
    {
        public int CellIndex;
        public float ConsumptionMwh;
    }
}

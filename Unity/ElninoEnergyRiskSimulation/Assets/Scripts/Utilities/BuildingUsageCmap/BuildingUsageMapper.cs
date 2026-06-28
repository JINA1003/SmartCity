using System.Collections.Generic;

/// <summary>
/// BuildingType 34종을 전력 4용도(주택/일반/교육/산업)로 분류하고,
/// 구 인덱스·셀 인덱스 조회 및 DistrictData에서 MWh 값 추출을 담당한다.
/// Python elecdemand_preprocessing.create_building_mapping()과 분류 기준이 동일하다.
/// </summary>
public static class BuildingUsageMapper
{
    // ═══════════════════════════════════════════════════════════════════════
    // 상수
    // ═══════════════════════════════════════════════════════════════════════

    public const int UsageCategoryCount = 4;

    // DistrictData.typePowerUsage 딕셔너리 키와 반드시 일치해야 한다
    public static readonly string[] UsageApiKeys =
    {
        "주택용",
        "일반용",
        "교육용",
        "산업용",
    };

    // ═══════════════════════════════════════════════════════════════════════
    // 건물 용도 분류
    // ═══════════════════════════════════════════════════════════════════════

    public static int GetUsageCategoryIndex(BuildingType buildingType)
    {
        return (int)GetUsageCategory(buildingType);
    }

    public static PowerUsageCategory GetUsageCategory(BuildingType buildingType)
    {
        switch (buildingType)
        {
            case BuildingType.single_house:
            case BuildingType.multi_house:
            case BuildingType.multi_family_house:
                return PowerUsageCategory.Housing;

            case BuildingType.edu_research:
                return PowerUsageCategory.Education;

            case BuildingType.factory:
            case BuildingType.warehouse:
            case BuildingType.vehicle_related:
            case BuildingType.hazardous_material:
            case BuildingType.animal_plant:
            case BuildingType.power_generation:
            case BuildingType.waste_treatment:
            case BuildingType.cemetery:
            case BuildingType.temporary_build:
                return PowerUsageCategory.Industrial;

            case BuildingType.neighborhood_1:
            case BuildingType.neighborhood_2:
            case BuildingType.office:
            case BuildingType.medical:
            case BuildingType.elder_child_care:
            case BuildingType.religious:
            case BuildingType.cultural_assembly:
            case BuildingType.retail:
            case BuildingType.sales_business:
            case BuildingType.entertainment:
            case BuildingType.tourist_rest:
            case BuildingType.accommodation:
            case BuildingType.sports:
            case BuildingType.neighborhood_all:
            case BuildingType.public_use:
            case BuildingType.funeral:
            case BuildingType.edu_welfare:
            case BuildingType.broadcasting_comm:
            case BuildingType.transport:
            case BuildingType.youth_training:
            case BuildingType.defense_prison:
                return PowerUsageCategory.General;

            default:
                return PowerUsageCategory.General;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MWh 조회
    // ═══════════════════════════════════════════════════════════════════════

    public static float GetUsageMwh(DistrictData data, PowerUsageCategory category)
    {
        return GetUsageMwh(data, (int)category);
    }

    public static float GetUsageMwh(DistrictData data, int categoryIndex)
    {
        if (data?.typePowerUsage == null) return 0f;
        if (categoryIndex < 0 || categoryIndex >= UsageApiKeys.Length) return 0f;

        return data.typePowerUsage.TryGetValue(UsageApiKeys[categoryIndex], out float value)
            ? value
            : 0f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 구 인덱스 / 셀 인덱스 조회
    // ═══════════════════════════════════════════════════════════════════════

    // 결과를 캐싱해 반복 호출 시 재정렬을 방지한다
    public static DistrictType[] GetOrderedDistricts()
    {
        if (_orderedDistricts != null) return _orderedDistricts;

        var districts = new List<DistrictType>();
        foreach (DistrictType district in System.Enum.GetValues(typeof(DistrictType)))
        {
            if (district == DistrictType.None) continue;
            districts.Add(district);
        }

        districts.Sort((a, b) => ((int)a).CompareTo((int)b));
        _orderedDistricts = districts.ToArray();
        return _orderedDistricts;
    }

    public static int GetDistrictIndex(DistrictType districtType)
    {
        var districts = GetOrderedDistricts();
        for (int i = 0; i < districts.Length; i++)
        {
            if (districts[i] == districtType) return i;
        }

        return 0;
    }

    public static int GetCellIndex(DistrictType districtType, PowerUsageCategory category)
    {
        return GetDistrictIndex(districtType) * UsageCategoryCount + (int)category;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 내부
    // ═══════════════════════════════════════════════════════════════════════

    private static DistrictType[] _orderedDistricts;
}

public enum PowerUsageCategory : int
{
    Housing   = 0,
    General   = 1,
    Education = 2,
    Industrial = 3,
}

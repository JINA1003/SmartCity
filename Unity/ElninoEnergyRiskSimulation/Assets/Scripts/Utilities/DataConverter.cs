using UnityEngine;

public static class DataConverter
{
    public static string GetDistrictName(DistrictType districtType)
    {
        return districtType switch
        {
            DistrictType.DOBONG => "도봉구",
            DistrictType.DONGDAEMUN => "동대문구",
            DistrictType.DONGJAK => "동작구",
            DistrictType.EUNPYEONG => "은평구",
            DistrictType.GANGBUK => "강북구",
            DistrictType.GANGDONG => "강동구",
            DistrictType.GANGNAM => "강남구",
            DistrictType.GANGSEO => "강서구",
            DistrictType.GEUMCHEON => "금천구",
            DistrictType.GURO => "구로구",
            DistrictType.GWANAK => "관악구",
            DistrictType.GWANGJIN => "광진구",
            DistrictType.JONGNO => "종로구",
            DistrictType.JUNG => "중구",
            DistrictType.JUNGNANG => "중랑구",
            DistrictType.MAPO => "마포구",
            DistrictType.NOWON => "노원구",
            DistrictType.SEOCHO => "서초구",
            DistrictType.SEODAEMUN => "서대문구",
            DistrictType.SEONGBUK => "성북구",
            DistrictType.SEONGDONG => "성동구",
            DistrictType.SONGPA => "송파구",
            DistrictType.YANGCHEON => "양천구",
            DistrictType.YEONGDEUNGPO => "영등포구",
            DistrictType.YONGSAN => "용산구",
            _ => "종로구"
        };
    }
    public static DistrictType GetDistrictType(string districtName)
    {
        // C#에서는 string이 null일 수 있으므로 안전한 검사가 필수입니다.
        if (string.IsNullOrWhiteSpace(districtName))
            return DistrictType.JONGNO;

        // switch 식을 사용하여 가독성 향상
        return districtName switch
        {
            var name when name.Contains("도봉") => DistrictType.DOBONG,
            var name when name.Contains("동대문") => DistrictType.DONGDAEMUN,
            var name when name.Contains("동작") => DistrictType.DONGJAK,
            var name when name.Contains("은평") => DistrictType.EUNPYEONG,
            var name when name.Contains("강북") => DistrictType.GANGBUK,
            var name when name.Contains("강동") => DistrictType.GANGDONG,
            var name when name.Contains("강남") => DistrictType.GANGNAM,
            var name when name.Contains("강서") => DistrictType.GANGSEO,
            var name when name.Contains("금천") => DistrictType.GEUMCHEON,
            var name when name.Contains("구로") => DistrictType.GURO,
            var name when name.Contains("관악") => DistrictType.GWANAK,
            var name when name.Contains("광진") => DistrictType.GWANGJIN,
            var name when name.Contains("종로") => DistrictType.JONGNO,
            var name when name.Contains("중구") => DistrictType.JUNG,
            var name when name.Contains("중랑") => DistrictType.JUNGNANG,
            var name when name.Contains("마포") => DistrictType.MAPO,
            var name when name.Contains("노원") => DistrictType.NOWON,
            var name when name.Contains("서초") => DistrictType.SEOCHO,
            var name when name.Contains("서대문") => DistrictType.SEODAEMUN,
            var name when name.Contains("성북") => DistrictType.SEONGBUK,
            var name when name.Contains("성동") => DistrictType.SEONGDONG,
            var name when name.Contains("송파") => DistrictType.SONGPA,
            var name when name.Contains("양천") => DistrictType.YANGCHEON,
            var name when name.Contains("영등포") => DistrictType.YEONGDEUNGPO,
            var name when name.Contains("용산") => DistrictType.YONGSAN,
            _ => DistrictType.JONGNO // 기본값
        };
    }

    public static string GetBuildingTypeName(BuildingType buildingType)
    {
        return buildingType switch
        {
            BuildingType.edu_research => "교육연구시설",
            BuildingType.factory => "공장",
            BuildingType.warehouse => "창고시설",
            BuildingType.vehicle_related => "자동차관련시설",
            BuildingType.hazardous_material => "위험물저장및처리시설",
            BuildingType.animal_plant => "동.식물 관련시설",
            BuildingType.power_generation => "발전시설",
            BuildingType.waste_treatment => "분뇨.쓰레기처리시설",
            BuildingType.cemetery => "묘지관련시설",
            BuildingType.temporary_build => "가설건축물",
            BuildingType.neighborhood_1 => "제1종근린생활시설",
            BuildingType.neighborhood_2 => "제2종근린생활시설",
            BuildingType.office => "업무시설",
            BuildingType.medical => "의료시설",
            BuildingType.elder_child_care => "노유자시설",
            BuildingType.religious => "종교시설",
            BuildingType.cultural_assembly => "문화및집회시설",
            BuildingType.retail => "판매시설",
            BuildingType.sales_business => "판매및영업시설",
            BuildingType.entertainment => "위락시설",
            BuildingType.tourist_rest => "관광휴게시설",
            BuildingType.accommodation => "숙박시설",
            BuildingType.sports => "운동시설",
            BuildingType.neighborhood_all => "근린생활시설",
            BuildingType.public_use => "공공용시설",
            BuildingType.funeral => "장례식장",
            BuildingType.edu_welfare => "교육연구및복지시설",
            BuildingType.broadcasting_comm => "방송통신시설",
            BuildingType.transport => "운수시설",
            BuildingType.youth_training => "수련시설",
            BuildingType.defense_prison => "교정및군사시설",
            BuildingType.single_house => "단독주택",
            BuildingType.multi_house => "공동주택",
            BuildingType.multi_family_house => "다가구주택",
            _ => "단독주택"
        };
    }

    public static BuildingType GetBuildingType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return BuildingType.single_house;

        return typeName switch
        {
            "교육연구시설" => BuildingType.edu_research,
            "공장" => BuildingType.factory,
            "창고시설" => BuildingType.warehouse,
            "자동차관련시설" => BuildingType.vehicle_related,
            "위험물저장및처리시설" => BuildingType.hazardous_material,
            "동.식물 관련시설" => BuildingType.animal_plant, // 데이터에 따라 "동식물관련시설"로 올 수도 있으니 주의!
            "발전시설" => BuildingType.power_generation,
            "분뇨.쓰레기처리시설" => BuildingType.waste_treatment, // "분뇨쓰레기처리시설" 
            "묘지관련시설" => BuildingType.cemetery,
            "가설건축물" => BuildingType.temporary_build,
            "제1종근린생활시설" => BuildingType.neighborhood_1,
            "제2종근린생활시설" => BuildingType.neighborhood_2,
            "업무시설" => BuildingType.office,
            "의료시설" => BuildingType.medical,
            "노유자시설" => BuildingType.elder_child_care,
            "종교시설" => BuildingType.religious,
            "문화및집회시설" => BuildingType.cultural_assembly,
            "판매시설" => BuildingType.retail,
            "판매및영업시설" => BuildingType.sales_business,
            "위락시설" => BuildingType.entertainment,
            "관광휴게시설" => BuildingType.tourist_rest,
            "숙박시설" => BuildingType.accommodation,
            "운동시설" => BuildingType.sports,
            "근린생활시설" => BuildingType.neighborhood_all,
            "공공용시설" => BuildingType.public_use,
            "장례식장" => BuildingType.funeral,
            "교육연구및복지시설" => BuildingType.edu_welfare,
            "방송통신시설" => BuildingType.broadcasting_comm,
            "운수시설" => BuildingType.transport,
            "수련시설" => BuildingType.youth_training,
            "교정및군사시설" => BuildingType.defense_prison,
            "단독주택" => BuildingType.single_house,
            "공동주택" => BuildingType.multi_house,
            "다가구주택" => BuildingType.multi_family_house,
            _ => BuildingType.single_house // 목록에 없는 예외 값이 들어올 경우의 기본값
        };
    }
}
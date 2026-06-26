using UnityEngine;

public static class DataConverter
{
    public static DistrictType GetDistrictType(string districtName)
    {
        if (string.IsNullOrEmpty(districtName)) return DistrictType.JONGNO; // 기본값

        if (districtName.Contains("도봉")) return DistrictType.DOBONG;
        if (districtName.Contains("동대문")) return DistrictType.DONGDAEMUN;
        if (districtName.Contains("동작")) return DistrictType.DONGJAK;
        if (districtName.Contains("은평")) return DistrictType.EUNPYEONG;
        if (districtName.Contains("강북")) return DistrictType.GANGBUK;
        if (districtName.Contains("강동")) return DistrictType.GANGDONG;
        if (districtName.Contains("강남")) return DistrictType.GANGNAM;
        if (districtName.Contains("강서")) return DistrictType.GANGSEO;
        if (districtName.Contains("금천")) return DistrictType.GEUMCHEON;
        if (districtName.Contains("구로")) return DistrictType.GURO;
        if (districtName.Contains("관악")) return DistrictType.GWANAK;
        if (districtName.Contains("광진")) return DistrictType.GWANGJIN;
        if (districtName.Contains("종로")) return DistrictType.JONGNO;
        if (districtName.Contains("중구")) return DistrictType.JUNG;
        if (districtName.Contains("중랑")) return DistrictType.JUNGNANG;
        if (districtName.Contains("마포")) return DistrictType.MAPO;
        if (districtName.Contains("노원")) return DistrictType.NOWON;
        if (districtName.Contains("서초")) return DistrictType.SEOCHO;
        if (districtName.Contains("서대문")) return DistrictType.SEODAEMUN;
        if (districtName.Contains("성북")) return DistrictType.SEONGBUK;
        if (districtName.Contains("성동")) return DistrictType.SEONGDONG;
        if (districtName.Contains("송파")) return DistrictType.SONGPA;
        if (districtName.Contains("양천")) return DistrictType.YANGCHEON;
        if (districtName.Contains("영등포")) return DistrictType.YEONGDEUNGPO;
        if (districtName.Contains("용산")) return DistrictType.YONGSAN;

        return DistrictType.JONGNO;
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
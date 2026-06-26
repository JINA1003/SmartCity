using UnityEngine;

public enum BuildingType
{
    edu_research,       // 교육연구시설
    factory,            // 공장
    warehouse,          // 창고시설
    vehicle_related,    // 자동차관련시설
    hazardous_material, // 위험물저장및처리시설
    animal_plant,       // 동.식물 관련시설
    power_generation,   // 발전시설
    waste_treatment,    // 분뇨.쓰레기처리시설
    cemetery,           // 묘지관련시설
    temporary_build,    // 가설건축물
    neighborhood_1,     // 제1종근린생활시설
    neighborhood_2,     // 제2종근린생활시설
    office,             // 업무시설
    medical,            // 의료시설
    elder_child_care,   // 노유자시설
    religious,          // 종교시설
    cultural_assembly,  // 문화및집회시설
    retail,             // 판매시설
    sales_business,     // 판매및영업시설
    entertainment,      // 위락시설
    tourist_rest,       // 관광휴게시설
    accommodation,      // 숙박시설
    sports,             // 운동시설
    neighborhood_all,   // 근린생활시설
    public_use,         // 공공용시설
    funeral,            // 장례식장
    edu_welfare,        // 교육연구및복지시설
    broadcasting_comm,  // 방송통신시설
    transport,          // 운수시설
    youth_training,     // 수련시설
    defense_prison,     // 교정및군사시설
    single_house,       // 단독주택
    multi_house,        // 공동주택
    multi_family_house, // 다가구주택
}

public enum DistrictType
{
    DOBONG,          // 도봉구
    DONGDAEMUN,      // 동대문구
    DONGJAK,         // 동작구
    EUNPYEONG,       // 은평구
    GANGBUK,         // 강북구
    GANGDONG,        // 강동구
    GANGNAM,         // 강남구
    GANGSEO,         // 강서구
    GEUMCHEON,       // 금천구
    GURO,            // 구로구
    GWANAK,          // 관악구
    GWANGJIN,        // 광진구
    JONGNO,          // 종로구
    JUNG,            // 중구
    JUNGNANG,        // 중랑구
    MAPO,            // 마포구
    NOWON,           // 노원
    SEOCHO,          // 서초구
    SEODAEMUN,       // 서대문구
    SEONGBUK,        // 성북구
    SEONGDONG,       // 성동구
    SONGPA,          // 송파구
    YANGCHEON,       // 양천구
    YEONGDEUNGPO,    // 영등포구
    YONGSAN          // 용산구
}

public static class EnumConverter
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
            "단독주택" => BuildingType.single_house,
            "공동주택" => BuildingType.multi_house,
            "업무시설" => BuildingType.office,
            "제1종근린생활시설" => BuildingType.neighborhood_1,
            "제2종근린생활시설" => BuildingType.neighborhood_2,
            "판매시설" => BuildingType.retail,
            _ => BuildingType.single_house // 매칭 안 될 경우 기본값
        };
    }
}
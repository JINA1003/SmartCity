using UnityEngine;

public enum BuildingType : int
{
    Unknown = 0,            // 알 수 없음
    edu_research = 1,       // 교육연구시설
    factory = 2,            // 공장
    warehouse = 3,          // 창고시설
    vehicle_related = 4,    // 자동차관련시설
    hazardous_material = 5, // 위험물저장및처리시설
    animal_plant = 6,       // 동.식물 관련시설
    power_generation = 7,   // 발전시설
    waste_treatment = 8,    // 분뇨.쓰레기처리시설
    cemetery = 9,           // 묘지관련시설
    temporary_build = 10,   // 가설건축물
    neighborhood_1 = 11,    // 제1종근린생활시설
    neighborhood_2 = 12,    // 제2종근린생활시설
    office = 13,            // 업무시설
    medical = 14,           // 의료시설
    elder_child_care = 15,  // 노유자시설
    religious = 16,         // 종교시설
    cultural_assembly = 17, // 문화및집회시설
    retail = 18,            // 판매시설
    sales_business = 19,    // 판매및영업시설
    entertainment = 20,     // 위락시설
    tourist_rest = 21,      // 관광휴게시설
    accommodation = 22,     // 숙박시설
    sports = 23,            // 운동시설
    neighborhood_all = 24,  // 근린생활시설
    public_use = 25,        // 공공용시설
    funeral = 26,           // 장례식장
    edu_welfare = 27,       // 교육연구및복지시설
    broadcasting_comm = 28, // 방송통신시설
    transport = 29,         // 운수시설
    youth_training = 30,    // 수련시설
    defense_prison = 31,    // 교정및군사시설
    single_house = 32,      // 단독주택
    multi_house = 33,       // 공동주택
    multi_family_house = 34 // 다가구주택
}

public enum DistrictType : int
{
    None = 0,

    JONGNO = 11110,        // 종로구
    JUNG = 11140,          // 중구
    YONGSAN = 11170,       // 용산구
    SEONGDONG = 11200,     // 성동구
    GWANGJIN = 11215,      // 광진구
    DONGDAEMUN = 11230,    // 동대문구
    JUNGNANG = 11260,      // 중랑구
    SEONGBUK = 11290,      // 성북구
    GANGBUK = 11305,       // 강북구
    DOBONG = 11320,        // 도봉구
    NOWON = 11350,         // 노원구
    EUNPYEONG = 11380,     // 은평구
    SEODAEMUN = 11410,     // 서대문구
    MAPO = 11440,          // 마포구
    YANGCHEON = 11470,     // 양천구
    GANGSEO = 11500,       // 강서구
    GURO = 11530,          // 구로구
    GEUMCHEON = 11545,     // 금천구
    YEONGDEUNGPO = 11560,  // 영등포구
    DONGJAK = 11590,       // 동작구
    GWANAK = 11620,        // 관악구
    SEOCHO = 11650,        // 서초구
    GANGNAM = 11680,       // 강남구
    SONGPA = 11710,        // 송파구
    GANGDONG = 11740       // 강동구
}
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    [Header("API 연동")]
    [SerializeField] private ApiClient apiClient;
    [SerializeField] private UIController uiController;

    public event Action<PowerGridData> OnPowerDataUpdated;
    public event Action<DistrictData> OnDistrictDataUpdated;
    public event Action<List<OniRangeData>> OniRangeDataUpdated;

    // 현재 선택된 연월 (슬라이더 재호출 시 사용)
    private int _currentYear;
    private int _currentMonth;

    private static readonly WaitForSeconds SliderDelay = new WaitForSeconds(0.5f);
    private Coroutine _sliderCoroutine;

    private void OnEnable()
    {
        if (uiController != null)
        {
            uiController.OnDateSelected       += HandleDateSelected;
            uiController.OnOniSliderReleased  += HandleSliderReleased;
        }
    }

    private void OnDisable()
    {
        if (uiController != null)
        {
            uiController.OnDateSelected       -= HandleDateSelected;
            uiController.OnOniSliderReleased  -= HandleSliderReleased;
        }
    }

    // 드롭다운 변경 → /oni && /predict/oni_range 병렬 호출, /oni 완료 후 /predict 순차 호출
    private void HandleDateSelected(string year, string month)
    {
        _currentYear  = SafeStringToInt(year);
        _currentMonth = SafeStringToInt(month);
        StartCoroutine(LoadOnDateSelected(_currentYear, _currentMonth));
    }

    // 슬라이더 버튼 뗄 때 → 0.5초 후 /predict 재호출
    private void HandleSliderReleased(float oniValue)
    {
        if (_sliderCoroutine != null) StopCoroutine(_sliderCoroutine);
        _sliderCoroutine = StartCoroutine(SliderDelayedPredict(oniValue));
    }

    private IEnumerator SliderDelayedPredict(float oniValue)
    {
        yield return SliderDelay;
        yield return StartCoroutine(LoadPredictOnly(_currentYear, _currentMonth, oniValue));
        _sliderCoroutine = null;
    }

    IEnumerator LoadOnDateSelected(int year, int month)
    {
        // /oni && /predict/oni_range 병렬 시작
        Debug.Log($"[DataManager] /oni && /predict/oni_range 병렬 호출 ({year}-{month})");

        float? fetchedOni = null;
        bool isOniLoaded  = false;
        bool isRangeLoaded = false;

        apiClient.FetchOni(year, month, (data) =>
        {
            if (data != null) fetchedOni = data["output"]["oni"].Value<float>();
            isOniLoaded = true;
        });

        apiClient.FetchOniRange(year, month, (rangeData) =>
        {
            if (rangeData != null)
            {
                ParseOniRangeData(rangeData);
                Debug.Log("[DataManager] OniRange 파싱 완료");
            }
            else
            {
                Debug.LogError("[DataManager] OniRange API 응답 Null");
            }
            isRangeLoaded = true;
        });

        // /oni 완료 대기
        yield return new WaitUntil(() => isOniLoaded);

        if (fetchedOni == null)
        {
            Debug.LogError("[DataManager] ONI값을 가져오지 못했습니다.");
            yield return new WaitUntil(() => isRangeLoaded);
            yield break;
        }

        // 슬라이더 초기화 (ONI 값으로)
        uiController.InitSlider(fetchedOni.Value);

        // /oni 완료 후 /predict 순차 호출
        yield return StartCoroutine(LoadPredictOnly(year, month, fetchedOni.Value));

        // oni_range도 완료될 때까지 대기 (이미 됐으면 즉시 통과)
        yield return new WaitUntil(() => isRangeLoaded);
    }

    IEnumerator LoadPredictOnly(int year, int month, float oniValue)
    {
        Debug.Log($"[DataManager] /predict 호출 ({year}-{month}, oni={oniValue})");

        JObject predictData   = null;
        bool isPredictLoaded  = false;

        apiClient.FetchPredict(year, month, oniValue, (data) =>
        {
            predictData     = data;
            isPredictLoaded = true;
        });
        yield return new WaitUntil(() => isPredictLoaded);

        if (predictData == null)
        {
            Debug.LogError("[DataManager] Predict API 응답 Null");
            yield break;
        }

        JObject predicted     = predictData["predicted"] as JObject;
        int currentAlertLevel = (predicted != null && predicted["alert_level"] != null)
                                ? predicted["alert_level"].Value<int>() : 0;

        JObject blackoutData  = null;
        if (currentAlertLevel == 4)
        {
            Debug.Log("[DataManager] /blackout_simulation 호출 (위험도 4단계)");
            bool isBlackoutLoaded = false;
            apiClient.FetchBlackoutSimulation(year, month, oniValue, (data) =>
            {
                blackoutData    = data;
                isBlackoutLoaded = true;
            });
            yield return new WaitUntil(() => isBlackoutLoaded);
        }

        ParseAndDispatchData(year.ToString(), month.ToString(), oniValue, predictData, blackoutData);
    }

    // 두 JSON 데이터를 병합하여 객체를 생성하고 이벤트를 호출하는 헬퍼 메서드
    private void ParseAndDispatchData(string year, string month, float oni, JObject predictData, JObject blackoutData)
    {
        JObject predicted = predictData["predicted"] as JObject;
        if (predicted == null) return;

        // --- [ 1. 전체 전력망 데이터 (PowerGridData) 파싱 ] ---
        PowerGridData powerGridData = new PowerGridData();
        powerGridData.year = year;
        powerGridData.month = month;
        powerGridData.oni = oni;
        powerGridData.temperature = predicted["asos_temp"].Value<float>();

        // blackout이 안 불렸을 수도 있으므로, predictData를 기준으로 위험도를 저장
        int currentAlertLevel = predicted["alert_level"] != null ? predicted["alert_level"].Value<int>() : 0;
        powerGridData.riskLevel = currentAlertLevel;
        powerGridData.riskLabel = predicted["alert_label"] != null ? predicted["alert_label"].Value<string>() : "정상"; // 기본값 부여

        if (predicted["oni_status"] != null)
            powerGridData.oniStatus = predicted["oni_status"].Value<string>();

        OnPowerDataUpdated?.Invoke(powerGridData);

        // --- [ 2. 블랙아웃 JSON 구(Gu)별 빠른 검색 Dictionary 만들기 (4단계일 때만) ] ---
        Dictionary<string, JArray> blackoutItemsDict = new Dictionary<string, JArray>();
        if (currentAlertLevel == 4 && blackoutData != null)
        {
            JArray districtsOrder = blackoutData["districts_order"] as JArray;
            if (districtsOrder != null)
            {
                foreach (JToken distToken in districtsOrder)
                {
                    string guName = distToken["gu"].Value<string>();
                    JArray bItems = distToken["blackout_items"] as JArray;
                    blackoutItemsDict[guName] = bItems;
                }
            }
        }

        // --- [ 3. 구역 데이터(DistrictData) 파싱 ] ---
        JArray regions = predicted["regions"] as JArray;
        if (regions != null)
        {
            foreach (JToken regionToken in regions)
            {
                JObject regionObject = regionToken as JObject;
                if (regionObject == null) continue;

                DistrictData districtData = new DistrictData();
                string guNameStr = regionObject["gu"].Value<string>();
                districtData.districtType = ConvertStringToDistrictType(guNameStr);
                districtData.temperature = regionObject["ta_gu"].Value<float>();
                districtData.totalPowerUsage = regionObject["total_consumption_mwh"].Value<double>();

                // (1) 용도별 사용량(Usage) 파싱
                districtData.typePowerUsage = new Dictionary<string, float>();
                JObject usageObject = regionObject["usage"] as JObject;
                if (usageObject != null)
                {
                    foreach (var kvp in usageObject)
                    {
                        districtData.typePowerUsage[kvp.Key] = kvp.Value["consumption_mwh"].Value<float>();
                    }
                }

                // (2) 건물 감축 필요도 파싱 (Blackout 데이터 결합)
                if (currentAlertLevel == 4 && blackoutItemsDict.ContainsKey(guNameStr))
                {
                    districtData.buildingReductionScores = new Dictionary<BuildingType, float>();
                    JArray bItems = blackoutItemsDict[guNameStr];

                    if (bItems != null)
                    {
                        foreach (JToken item in bItems)
                        {
                            string bTypeStr = item["building_type"].Value<string>();
                            float score = item["reduction_need_score"].Value<float>();

                            BuildingType enumType = ConvertStringToBuildingType(bTypeStr);
                            districtData.buildingReductionScores[enumType] = score;
                        }
                    }
                }
                else
                {
                    // 4단계가 아니거나 블랙아웃 대상 지역이 아니면 null 할당
                    districtData.buildingReductionScores = new Dictionary<BuildingType, float>();
                }

                // 완성된 구 데이터 이벤트로 전파
                OnDistrictDataUpdated?.Invoke(districtData);
            }
        }
    }

    // 차트용 ONI 범위 데이터를 파싱하는 메서드
    private void ParseOniRangeData(JObject rangeDataJson)
    {
        if (rangeDataJson == null) return;

        List<OniRangeData> rangeDataList = new List<OniRangeData>();

        // JSON 최상위에서 "oni_range" 배열을 가져옵니다.
        JArray jsonArray = rangeDataJson["oni_range"] as JArray;

        if (jsonArray != null)
        {
            foreach (JToken token in jsonArray)
            {
                JObject item = token as JObject;
                if (item == null) continue;

                OniRangeData data = new OniRangeData();

                // 1. 기본 예측 데이터 파싱 (JSON이 1차원 평면 구조로 되어 있음)
                data.oni = item["oni"] != null ? item["oni"].Value<float>() : 0f;
                data.seoulTemperature = item["asos_temp"] != null ? item["asos_temp"].Value<float>() : 0f;
                data.supplyPower = item["supply_mw"] != null ? item["supply_mw"].Value<float>() : 0f;
                data.reserveRate = item["reserve_rate"] != null ? item["reserve_rate"].Value<float>() : 0f;
                data.seoulTotalConsumption = item["seoul_total_consumption_mwh"] != null ? item["seoul_total_consumption_mwh"].Value<float>() : 0f;

                // 참고: 현재 올려주신 json에는 alert_level이 존재하지 않아 안전하게 처리합니다.
                data.alert_level = item["alert_level"].Value<int>();

                // 2. 구역별(regions) 데이터 파싱
                data.guTemperature = new Dictionary<string, float>();
                data.guConsumption = new Dictionary<string, double>();

                JArray regions = item["regions"] as JArray;
                if (regions != null)
                {
                    foreach (JToken regionToken in regions)
                    {
                        string guName = regionToken["gu"].Value<string>();
                        float temp = regionToken["ta_gu"] != null ? regionToken["ta_gu"].Value<float>() : 0f;

                        // 소비량은 double형이 안전합니다.
                        double consumption = regionToken["total_consumption_mwh"] != null ? regionToken["total_consumption_mwh"].Value<double>() : 0.0;

                        // 딕셔너리에 데이터 저장
                        data.guTemperature[guName] = temp;
                        data.guConsumption[guName] = consumption;
                    }
                }

                // 완성된 데이터를 리스트에 추가
                rangeDataList.Add(data);
            }
        }
        else
        {
            Debug.LogError("[DataManager] JSON 구조 내에 'oni_range' 배열이 존재하지 않습니다.");
        }

        // 파싱된 전체 범위 데이터 리스트를 이벤트로 방송 (차트 매니저 등에서 활용)
        OniRangeDataUpdated?.Invoke(rangeDataList);
    }

    private BuildingType ConvertStringToBuildingType(string koreanName)
    {
        switch (koreanName)
        {
            case "교육연구시설": return BuildingType.edu_research;
            case "공장": return BuildingType.factory;
            case "창고시설": return BuildingType.warehouse;
            case "자동차관련시설": return BuildingType.vehicle_related;
            case "위험물저장및처리시설": return BuildingType.hazardous_material;
            case "동.식물 관련시설": return BuildingType.animal_plant;
            case "발전시설": return BuildingType.power_generation;
            case "분뇨.쓰레기처리시설": return BuildingType.waste_treatment;
            case "묘지관련시설": return BuildingType.cemetery;
            case "가설건축물": return BuildingType.temporary_build;
            case "제1종근린생활시설": return BuildingType.neighborhood_1;
            case "제2종근린생활시설": return BuildingType.neighborhood_2;
            case "업무시설": return BuildingType.office;
            case "의료시설": return BuildingType.medical;
            case "노유자시설": return BuildingType.elder_child_care;
            case "종교시설": return BuildingType.religious;
            case "문화및집회시설": return BuildingType.cultural_assembly;
            case "판매시설": return BuildingType.retail;
            case "판매및영업시설": return BuildingType.sales_business;
            case "위락시설": return BuildingType.entertainment;
            case "관광휴게시설": return BuildingType.tourist_rest;
            case "숙박시설": return BuildingType.accommodation;
            case "운동시설": return BuildingType.sports;
            case "근린생활시설": return BuildingType.neighborhood_all;
            case "공공용시설": return BuildingType.public_use;
            case "장례식장": return BuildingType.funeral;
            case "교육연구및복지시설": return BuildingType.edu_welfare;
            case "방송통신시설": return BuildingType.broadcasting_comm;
            case "운수시설": return BuildingType.transport;
            case "수련시설": return BuildingType.youth_training;
            case "교정및군사시설": return BuildingType.defense_prison;
            case "단독주택": return BuildingType.single_house;
            case "공동주택": return BuildingType.multi_house;
            case "다가구주택": return BuildingType.multi_family_house;
            default:
                return BuildingType.neighborhood_all; // 기본값
        }
    }

    public DistrictType ConvertStringToDistrictType(string koreanName)
    {
        switch (koreanName)
        {
            case "도봉구": return DistrictType.DOBONG;
            case "동대문구": return DistrictType.DONGDAEMUN;
            case "동작구": return DistrictType.DONGJAK;
            case "은평구": return DistrictType.EUNPYEONG;
            case "강북구": return DistrictType.GANGBUK;
            case "강동구": return DistrictType.GANGDONG;
            case "강남구": return DistrictType.GANGNAM;
            case "강서구": return DistrictType.GANGSEO;
            case "금천구": return DistrictType.GEUMCHEON;
            case "구로구": return DistrictType.GURO;
            case "관악구": return DistrictType.GWANAK;
            case "광진구": return DistrictType.GWANGJIN;
            case "종로구": return DistrictType.JONGNO;
            case "중구": return DistrictType.JUNG;
            case "중랑구": return DistrictType.JUNGNANG;
            case "마포구": return DistrictType.MAPO;
            case "노원구": return DistrictType.NOWON;
            case "서초구": return DistrictType.SEOCHO;
            case "서대문구": return DistrictType.SEODAEMUN;
            case "성북구": return DistrictType.SEONGBUK;
            case "성동구": return DistrictType.SEONGDONG;
            case "송파구": return DistrictType.SONGPA;
            case "양천구": return DistrictType.YANGCHEON;
            case "영등포구": return DistrictType.YEONGDEUNGPO;
            case "용산구": return DistrictType.YONGSAN;
            default:
                Debug.LogWarning($"[DataManager] 알 수 없는 구 이름: {koreanName}");
                return DistrictType.GANGNAM; // 기본값 임시 할당
        }
    }

    public int SafeStringToInt(string input, int defaultValue = 0)
    {
        if (!string.IsNullOrEmpty(input) && int.TryParse(input.Trim(), out int result))
        {
            return result;
        }

        return defaultValue;
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json.Linq;
using TMPro;

public class MinimapManager : MonoBehaviour
{
    // 구 오브젝트
    [Header("UI")]
    [SerializeField] private RectTransform districtRoot;

    // 구 경계 geojson 파일
    [Header("GeoJSON")]
    [SerializeField] private string fileName = "seoul_district.geojson";

    // minimap 스타일 설정
    [Header("Map Style")]
    private Color districtColor = new Color(0.75f, 0.75f, 0.75f, 0.45f);
    private Color selectedOutlineColor = Color.red;
    private Color nonSelectedOutlineColor = Color.white;
    private float outlineWidth = 1.5f; // 라인두께 고정

    // cmap 스타일 설정
    [Header("CMap Style")]
    [SerializeField] private Color lowPowerColor = new Color(1f, 0.9f, 0.75f, 0.8f);
    [SerializeField] private Color highPowerColor = new Color(1f, 0.25f, 0.05f, 0.9f);

    [Header("Tooltip")]
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private TextMeshProUGUI tooltipText;

    [Header("DataManager")]
    [SerializeField] private DataManager dataManager;

    // 구 이름, 구 폴리곤 딕셔너리
    private Dictionary<string, List<MinimapPolygon>> districtPolygonMap =
        new Dictionary<string, List<MinimapPolygon>>();

    // 구 이름, 구 아웃라인 딕셔너리
    private Dictionary<string, List<MinimapOutline>> districtOutlineMap =
        new Dictionary<string, List<MinimapOutline>>();

    private readonly List<OniRangeData> oniRangeEntries = new List<OniRangeData>();

    private float currentOni = 0f;
    private bool hasCurrentOni = false;
    private bool isMapCreated = false;

    private string selectedDistrictName;

    // 구별 폴리곤 최대, 최소 좌표
    private double minLon = double.MaxValue;
    private double maxLon = double.MinValue;
    private double minLat = double.MaxValue;
    private double maxLat = double.MinValue;

    private float padding = 15f;
    private JArray features;

    private MainCameraController mainCameraController;

    // 구 선택 이벤트
    public static event Action<string> OnDistrictSelected;


    private void Awake()
    {
        // 카메라 설정
        mainCameraController = FindFirstObjectByType<MainCameraController>();

        if (mainCameraController == null)
        {
            Debug.LogError("[MinimapManager] MainCameraController를 찾을 수 없습니다.");
        }

        // dataManager 연결 안했으면 직접 찾아 연결
        if (dataManager == null)
        {
            dataManager = FindFirstObjectByType<DataManager>();
        }
    }

    // cmap에 필요한 데이터 이벤트 구독
    private void OnEnable()
    {
        if (dataManager != null)
        {
            dataManager.OniRangeDataUpdated += HandleOniRangeDataUpdated;
            dataManager.OnPowerDataUpdated += HandlePowerDataUpdated;
        }
        else
        {
            Debug.LogWarning("[MinimapManager] DataManager가 연결되지 않았습니다.");
        }
    }

    private void OnDisable()
    {
        if (dataManager != null)
        {
            dataManager.OniRangeDataUpdated -= HandleOniRangeDataUpdated;
            dataManager.OnPowerDataUpdated -= HandlePowerDataUpdated;
        }
    }
    
    private void Start()
    {
        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(false);

            CanvasGroup cg = tooltipRoot.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = tooltipRoot.gameObject.AddComponent<CanvasGroup>();
            }

            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        LoadGeoJson();
        CreateDistricts();

        isMapCreated = true;
        ApplyCurrentOniCMap();
    }

    // geojson 로드
    private void LoadGeoJson()
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path))
        {
            Debug.LogError("[MinimapManager] GeoJSON 파일을 찾을 수 없습니다: " + path);
            return;
        }

        string json = File.ReadAllText(path);
        JObject geoJson = JObject.Parse(json);
        features = (JArray)geoJson["features"];

        foreach (JObject feature in features)
        {
            JToken geometry = feature["geometry"];
            string type = geometry["type"]?.ToString();

            if (type == "Polygon")
            {
                ScanPolygonBounds((JArray)geometry["coordinates"]);
            }
            else if (type == "MultiPolygon")
            {
                foreach (JArray polygon in geometry["coordinates"])
                {
                    ScanPolygonBounds(polygon);
                }
            }
        }
    }

    // 구 경계 범위
    private void ScanPolygonBounds(JArray polygon)
    {
        JArray outerRing = (JArray)polygon[0];

        foreach (JArray coord in outerRing)
        {
            double lon = coord[0].Value<double>();
            double lat = coord[1].Value<double>();

            minLon = Math.Min(minLon, lon);
            maxLon = Math.Max(maxLon, lon);
            minLat = Math.Min(minLat, lat);
            maxLat = Math.Max(maxLat, lat);
        }
    }

    // 구 생성
    private void CreateDistricts()
    {
        if (features == null) return;

        foreach (JObject feature in features)
        {
            string districtName =
                feature["properties"]?["SIGUNGU_NM"]?.ToString() ?? "Unknown";

            JToken props = feature["properties"];

            bool hasRepPoint =
                props?["rep_lon"] != null &&
                props?["rep_lat"] != null;

            if (hasRepPoint && mainCameraController != null)
            {
                double repLon = props["rep_lon"].Value<double>();
                double repLat = props["rep_lat"].Value<double>();

                mainCameraController.RegisterDistrictPosition(
                    districtName,
                    repLon,
                    repLat
                );
            }

            JToken geometry = feature["geometry"];
            string type = geometry["type"]?.ToString();

            if (type == "Polygon")
            {
                CreatePolygonUI(districtName, (JArray)geometry["coordinates"]);
            }
            else if (type == "MultiPolygon")
            {
                foreach (JArray polygon in geometry["coordinates"])
                {
                    CreatePolygonUI(districtName, polygon);
                }
            }
        }
    }

    // 구 UI 범위에 맞게 나타내기
    private void CreatePolygonUI(string districtName, JArray polygon)
    {
        JArray outerRing = (JArray)polygon[0];

        GameObject obj = new GameObject("UI_District_" + districtName);
        obj.transform.SetParent(districtRoot, false);

        RectTransform parentRect = districtRoot.GetComponent<RectTransform>();

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = parentRect.rect.size;

        obj.AddComponent<CanvasRenderer>();

        MinimapPolygon polygonGraphic = obj.AddComponent<MinimapPolygon>();
        polygonGraphic.color = districtColor;
        polygonGraphic.districtName = districtName;
        polygonGraphic.minimapManager = this;

        List<Vector2> uiPoints = new List<Vector2>();

        foreach (JArray coord in outerRing)
        {
            double lon = coord[0].Value<double>();
            double lat = coord[1].Value<double>();

            uiPoints.Add(LonLatToUI(lon, lat));
        }

        if (
            uiPoints.Count > 1 &&
            Vector2.Distance(uiPoints[0], uiPoints[uiPoints.Count - 1]) < 0.01f
        )
        {
            uiPoints.RemoveAt(uiPoints.Count - 1);
        }

        polygonGraphic.points = uiPoints;
        polygonGraphic.SetVerticesDirty();

        if (!districtPolygonMap.ContainsKey(districtName))
        {
            districtPolygonMap[districtName] = new List<MinimapPolygon>();
        }

        districtPolygonMap[districtName].Add(polygonGraphic);

        GameObject outlineObj = new GameObject("Outline_" + districtName);
        outlineObj.transform.SetParent(obj.transform, false);

        RectTransform outlineRt = outlineObj.AddComponent<RectTransform>();
        outlineRt.anchorMin = Vector2.zero;
        outlineRt.anchorMax = Vector2.one;
        outlineRt.offsetMin = Vector2.zero;
        outlineRt.offsetMax = Vector2.zero;

        outlineObj.AddComponent<CanvasRenderer>();

        MinimapOutline outline = outlineObj.AddComponent<MinimapOutline>();
        outline.points = uiPoints;
        outline.color = nonSelectedOutlineColor;
        outline.lineWidth = outlineWidth;
        outline.raycastTarget = false;
        outline.SetVerticesDirty();

        polygonGraphic.outline = outline;

        if (!districtOutlineMap.ContainsKey(districtName))
        {
            districtOutlineMap[districtName] = new List<MinimapOutline>();
        }

        districtOutlineMap[districtName].Add(outline);
    }

    // 좌표 UI 범위에 맞게 재설정
    private Vector2 LonLatToUI(double lon, double lat)
    {
        float width = districtRoot.rect.width - padding * 2f;
        float height = districtRoot.rect.height - padding * 2f;

        float lonRange = (float)(maxLon - minLon);
        float latRange = (float)(maxLat - minLat);

        float scaleX = width / lonRange;
        float scaleY = height / latRange;
        float scale = Mathf.Min(scaleX, scaleY);

        float mapWidth = lonRange * scale;
        float mapHeight = latRange * scale;

        float x = (float)((lon - minLon) * scale);
        float y = (float)((lat - minLat) * scale);

        x -= mapWidth / 2f;
        y -= mapHeight / 2f;

        return new Vector2(x, y);
    }

    // 구 클릭
    public void SelectDistrict(MinimapPolygon polygon)
    {
        if (polygon == null) return;

        if (!string.IsNullOrEmpty(selectedDistrictName) &&
            districtOutlineMap.TryGetValue(selectedDistrictName, out List<MinimapOutline> previousOutlines))
        {
            foreach (MinimapOutline outline in previousOutlines)
            {
                if (outline != null)
                {
                    outline.SetOutlineColor(nonSelectedOutlineColor);
                }
            }
        }

        selectedDistrictName = polygon.districtName;

        if (districtOutlineMap.TryGetValue(selectedDistrictName, out List<MinimapOutline> selectedOutlines))
        {
            foreach (MinimapOutline outline in selectedOutlines)
            {
                if (outline != null)
                {
                    outline.SetOutlineColor(selectedOutlineColor);
                    outline.transform.parent.SetAsLastSibling();
                    outline.transform.SetAsLastSibling();
                }
            }
        }

        Debug.Log("[MinimapManager] 클릭한 구: " + polygon.districtName);

        OnDistrictSelected?.Invoke(polygon.districtName);
    }

    private void HandlePowerDataUpdated(PowerGridData data)
    {
        if (data == null) return;

        currentOni = data.oni;
        hasCurrentOni = true;

        Debug.Log($"[MinimapManager] 선택 연월 ONI 수신: {currentOni}");

        ApplyCurrentOniCMap();
    }

    private void HandleOniRangeDataUpdated(List<OniRangeData> data)
    {
        if (data == null || data.Count == 0)
        {
            Debug.LogWarning("[MinimapManager] OniRangeData가 비어 있습니다.");
            return;
        }

        oniRangeEntries.Clear();
        oniRangeEntries.AddRange(data);

        Debug.Log($"[MinimapManager] OniRangeData 수신 완료: {oniRangeEntries.Count}개");

        ApplyCurrentOniCMap();
    }

    private void ApplyCurrentOniCMap()
    {
        if (!isMapCreated) return;
        if (oniRangeEntries.Count == 0) return;

        float targetOni = hasCurrentOni ? currentOni : 0f;

        OniRangeData targetData = GetClosestOniData(targetOni);

        if (targetData == null || targetData.guConsumption == null)
        {
            Debug.LogWarning("[MinimapManager] guConsumption 데이터가 없습니다.");
            return;
        }

        Debug.Log($"[MinimapManager] cmap 적용 기준 ONI: {targetData.oni}");

        ApplyPowerUsageCMap(targetData.guConsumption);
    }

    private OniRangeData GetClosestOniData(float oniValue)
    {
        OniRangeData closest = null;
        float minDistance = float.MaxValue;

        foreach (OniRangeData data in oniRangeEntries)
        {
            float distance = Mathf.Abs(data.oni - oniValue);

            if (distance < minDistance)
            {
                minDistance = distance;
                closest = data;
            }
        }

        return closest;
    }

    private void ApplyPowerUsageCMap(Dictionary<string, double> guConsumption)
    {
        if (guConsumption == null || guConsumption.Count == 0) return;

        double minValue = double.MaxValue;
        double maxValue = double.MinValue;

        foreach (double value in guConsumption.Values)
        {
            minValue = Math.Min(minValue, value);
            maxValue = Math.Max(maxValue, value);
        }

        int appliedCount = 0;

        foreach (var kvp in guConsumption)
        {
            string districtName = kvp.Key.Trim();
            double powerUsage = kvp.Value;

            if (!districtPolygonMap.TryGetValue(districtName, out List<MinimapPolygon> polygons))
            {
                Debug.LogWarning("[MinimapManager] 미니맵에서 구를 찾지 못함: " + districtName);
                continue;
            }

            float t = 0f;

            if (maxValue > minValue)
            {
                t = (float)((powerUsage - minValue) / (maxValue - minValue));
            }

            Color cmapColor = Color.Lerp(lowPowerColor, highPowerColor, t);

            foreach (MinimapPolygon polygon in polygons)
            {
                if (polygon != null)
                {
                    polygon.color = cmapColor;
                    polygon.SetVerticesDirty();
                    appliedCount++;
                }
            }
        }

        Debug.Log($"[MinimapManager] 전력 사용량 cmap 적용 완료: {appliedCount}개 polygon");
    }

    // 구 이름 Tooltip 함수
    public void ShowDistrictTooltip(string districtName, Vector2 screenPosition)
    {
        if (tooltipRoot == null || tooltipText == null) return;

        tooltipText.text = districtName;
        tooltipRoot.gameObject.SetActive(true);

        tooltipRoot.position = screenPosition;
    }

    public void HideDistrictTooltip()
    {
        if (tooltipRoot == null) return;

        tooltipRoot.gameObject.SetActive(false);
    }

    public void MoveDistrictTooltip(Vector2 screenPosition)
    {
        if (tooltipRoot == null) return;

        if (!tooltipRoot.gameObject.activeSelf)
            return;

        tooltipRoot.position = screenPosition;
    }
}
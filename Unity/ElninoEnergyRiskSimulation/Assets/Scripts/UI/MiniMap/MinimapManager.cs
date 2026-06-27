using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json.Linq;
// using System.Runtime.CompilerServices;
using System;

public class MinimapManager : MonoBehaviour
{
    [Header("UI")]
    public RectTransform districtRoot;

    [Header("GeoJSON")]
    public string fileName = "seoul_district.geojson";

    [Header("Map Style")]
    // 시뮬레이션 시작전 미니맵 색
    public Color districtColor = Color.white;

    // TODO: 시뮬레이션 시작 후 cmap으로 미니맵 색 설정
    public Color cmapDistrictColor = new Color(1f, 0.45f, 0.2f, 0.65f); 

    // 구 아웃라인 색
    public Color selectedOutlineColor = Color.red;
    public Color nonSelectedOutlineColor = Color.gray;
    // 윤곽선 두께
    public float outlineWidth = 1.5f;

    // 구 클릭 이벤트
    public static event Action<string> OnDistrictSelected;

    private double minLon = double.MaxValue;
    private double maxLon = double.MinValue;
    private double minLat = double.MaxValue;
    private double maxLat = double.MinValue;

    private float padding = 15f;
    private JArray features;
    private MinimapPolygon selectedPolygon;
    private MainCameraController mainCameraController;

    private void Awake()
    {
        mainCameraController = FindFirstObjectByType<MainCameraController>();

        if (mainCameraController == null)
        {
            Debug.LogError("MainCameraController를 찾을 수 없습니다.");
        }
    }

    private void Start()
    {
        LoadGeoJson();
        CreateDistricts();
    }

    private void LoadGeoJson()
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path))
        {
            Debug.LogError("GeoJSON 파일을 찾을 수 없습니다: " + path);
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

    private void ScanPolygonBounds(JArray polygon)
    {
        JArray outerRing = (JArray)polygon[0];

        foreach (JArray coord in outerRing)
        {
            double lon = coord[0].Value<double>();
            double lat = coord[1].Value<double>();

            minLon = System.Math.Min(minLon, lon);
            maxLon = System.Math.Max(maxLon, lon);
            minLat = System.Math.Min(minLat, lat);
            maxLat = System.Math.Max(maxLat, lat);
        }
    }

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

        // ---------------------- outline 추가----------------------
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
    }

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

    // 구 클릭할 때(클릭 함수 in minimappolygon.cs) 실행 함수
    public void SelectDistrict(MinimapPolygon polygon)
    {
        if (polygon == null) return;

        if (selectedPolygon != null && selectedPolygon.outline != null)
        {
            selectedPolygon.outline.SetOutlineColor(nonSelectedOutlineColor);
        }

        selectedPolygon = polygon;

        if (selectedPolygon.outline != null)
        {
            selectedPolygon.outline.SetOutlineColor(selectedOutlineColor);
        }

        // 이벤트 발생
        OnDistrictSelected?.Invoke(polygon.districtName);
    }
}
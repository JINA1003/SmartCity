using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

//JObject -> { } 중괄호(딕셔너리 형태)
//JArray  -> [ ] 대괄호(배열 형태)
//JToken  -> 둘 다 포함하는 부모 타입

public class DataParser
{
    private const double LAT_SCALE = 111320.0;
    private const double MIN_POLYGON_SIZE_METERS = 2.0;
    private const float DEFAULT_HEIGHT = 3.0f;
    private const float METERS_PER_FLOOR = 3.0f;
    public async Task<List<BuildingData>> ParseGeoJson(string fileName)
    {
        List<BuildingData> result = new List<BuildingData>();

        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path))
        {
            Debug.LogError("[DataParser] 파일 경로에 없음: " + path);
            return result;
        }
        string json = File.ReadAllText(path);

        result = await Task.Run(() =>  //result에는 파싱이 완료된 List<BuildingData> 가 담김 
        {
            List<BuildingData> parsed = new List<BuildingData>();

            JObject root = JObject.Parse(json);
            JArray features = (JArray)root["features"];

            foreach (JToken feat in features)
            {
                try
                {
                    JToken props = feat["properties"];
                    JToken geom = feat["geometry"];

                    if (props == null || geom == null) continue;

                    if (geom["type"]?.ToString() != "Polygon") continue;

                    JArray ring = (JArray)geom["coordinates"][0];

                    if (ring == null || ring.Count < 3) continue;

                    List<double[]> rawCoords = new List<double[]>();

                    foreach (JArray point in ring)
                    {   // 주의: GeoJSON 은 경도(x), 위도(y) 순서다.
                        // point[0] 이 경도, point[1] 이 위도다.
                        double lon = (double)point[0]; // 경도
                        double lat = (double)point[1]; // 위도

                        // 경도, 위도를 배열로 묶어서 리스트에 추가한다.
                        double[] coord = new double[] { lon, lat };
                        rawCoords.Add(coord);
                    }

                    if (!IsValidPolygon(rawCoords)) continue;

                    BuildingData data = new BuildingData();
                    data.id = props["A0"]?.ToString();

                    string address = props["A4"]?.ToString();
                    string addressDetail = props["A5"]?.ToString();
                    data.name = (address + " " + addressDetail).Trim();

                    data.type = props["A9"]?.ToString();

                    //화재 시뮬레이션을 위한 코드
                    data.structureCode = props["A10"]?.ToString();





                    double a26 = 0;
                    if (props["A26"] != null && props["A26"].Type != JTokenType.Null)
                        a26 = (double)props["A26"];

                    double preHeight = 0;
                    if (props["height"] != null && props["height"].Type != JTokenType.Null)
                        preHeight = (double)props["height"];

                    if (preHeight > 0 && a26 > 0)
                    {
                        double meterPerFloor = preHeight / a26;

                        if (meterPerFloor < 2.0 || meterPerFloor > 10.0)
                        {
                            preHeight = 0;
                        }
                    }

                    //높이 계산
                    if (preHeight > 0)
                        data.height = (float)preHeight;
                    else if (a26 > 0)
                        data.height = (float)(a26 * METERS_PER_FLOOR);
                    else
                        data.height = DEFAULT_HEIGHT;

                    data.floors = (int)a26;

                    double lonSum = 0;
                    double latSum = 0;

                    foreach (double[] c in rawCoords)
                    {
                        lonSum += c[0]; // 경도 합산
                        latSum += c[1]; // 위도 합산
                    }

                    data.lon = lonSum / rawCoords.Count; // 경도 평균
                    data.lat = latSum / rawCoords.Count; // 위도 평균

                    data.polygon = ConvertToLocalPolygon(rawCoords, data.lon, data.lat);

                    if (data.polygon.Count > 1 &&
                        data.polygon[0] == data.polygon[data.polygon.Count - 1])
                        data.polygon.RemoveAt(data.polygon.Count - 1);

                    if (data.polygon.Count < 3) continue;

                    parsed.Add(data);
                }
                catch { continue; }
            }

            return parsed;
        });

        Debug.Log("[DataParser] GeoJSON 로드 완료: " + result.Count + "개");
        return result;
    }

    //!!!!!!추가된 부분!!!!!!!!!! 
    // 범용 JSON 파싱 — StreamingAssets 의 단순 배열 JSON 파일 처리
    // [ { ... }, { ... } ] 형태의 JSON 파일이면 무엇이든 파싱 가능
    //
    // 사용 예시:
    //   List<District>    = await parser.ParseJson<District>("seoul_districts.json");
    //   List<ShelterData> = await parser.ParseJson<ShelterData>("shelters.json");
    public async Task<List<T>> ParseJson<T>(string fileName)
    {
        List<T> result = new List<T>();

        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"[DataParser] 파일 없음: {path}");
            return result;
        }

        string json = File.ReadAllText(path);

        result = await Task.Run(() =>
            JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>()
        );

        Debug.Log($"[DataParser] {fileName} 로드 완료 — {result.Count}개");
        return result;
    }

    private List<Vector2> ConvertToLocalPolygon(List<double[]> rawCoords, double cLon, double cLat)
    {
        double lonScale = LAT_SCALE * Math.Cos(cLat * Math.PI / 180.0);

        List<Vector2> polygon = new List<Vector2>();
        foreach (double[] c in rawCoords)
        {
            // 중심점에서 얼마나 떨어져 있는지 미터로 계산한다.
            float x = (float)((c[0] - cLon) * lonScale);
            float z = (float)((c[1] - cLat) * LAT_SCALE);
            polygon.Add(new Vector2(x, z));
        }
        return polygon;
    }
    private bool IsValidPolygon(List<double[]> coords)
    {
        if (coords.Count < 3) return false;

        double cLat = 0;
        double minLon = double.MaxValue, maxLon = double.MinValue;
        double minLat = double.MaxValue, maxLat = double.MinValue;

        foreach (double[] c in coords)
        {
            cLat += c[1];
            if (c[0] < minLon) minLon = c[0];
            if (c[0] > maxLon) maxLon = c[0];
            if (c[1] < minLat) minLat = c[1];
            if (c[1] > maxLat) maxLat = c[1];
        }

        // 위도 평균값 계산 (경도 1도당 미터 거리 보정에 사용)
        cLat /= coords.Count;

        // 도 단위 차이를 미터로 변환하여 건물의 가로/세로 크기를 구한다.
        double lonScale = LAT_SCALE * Math.Cos(cLat * Math.PI / 180.0);
        double xRange = (maxLon - minLon) * lonScale;
        double zRange = (maxLat - minLat) * LAT_SCALE;

        // 가로 세로 중 긴 쪽이 2m 미만이면 실제로 존재할 수 없는 건물이므로 탈락
        return Math.Max(xRange, zRange) >= MIN_POLYGON_SIZE_METERS;
    }
}

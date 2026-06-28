using System.Collections.Generic;
using UnityEngine;

public static class DistrictCoordinates
{
    // 구역 ID(또는 Enum)에 따른 중심 좌표 (Lon 경도, Lat 위도)
    public static readonly Dictionary<int, Vector2> CenterCoords = new Dictionary<int, Vector2>()
    {
        { 11680, new Vector2(127.06278f, 37.49510f) }, // 강남구
        { 11740, new Vector2(127.14546f, 37.55274f) }, // 강동구
        { 11305, new Vector2(127.02015f, 37.63490f) }, // 강북구
        { 11500, new Vector2(126.81622f, 37.56227f) }, // 강서구
        { 11620, new Vector2(126.95235f, 37.47876f) }, // 관악구
        { 11215, new Vector2(127.08366f, 37.53913f) }, // 광진구
        { 11530, new Vector2(126.85020f, 37.49447f) }, // 구로구
        { 11545, new Vector2(126.89106f, 37.47486f) }, // 금천구
        { 11350, new Vector2(127.06718f, 37.66045f) }, // 노원구
        { 11320, new Vector2(127.03011f, 37.65066f) }, // 도봉구
        { 11230, new Vector2(127.05408f, 37.58189f) }, // 동대문구
        { 11590, new Vector2(126.95149f, 37.50056f) }, // 동작구
        { 11440, new Vector2(126.90926f, 37.55438f) }, // 마포구
        { 11410, new Vector2(126.93506f, 37.57809f) }, // 서대문구
        { 11650, new Vector2(127.01088f, 37.49447f) }, // 서초구
        { 11200, new Vector2(127.02461f, 37.54784f) }, // 성동구
        { 11290, new Vector2(127.01448f, 37.60267f) }, // 성북구
        { 11710, new Vector2(127.11113f, 37.50210f) }, // 송파구
        { 11470, new Vector2(126.87472f, 37.52056f) }, // 양천구
        { 11560, new Vector2(126.90308f, 37.52606f) }, // 영등포구
        { 11170, new Vector2(126.97750f, 37.53391f) }, // 용산구
        { 11380, new Vector2(126.92780f, 37.61846f) }, // 은평구
        { 11110, new Vector2(126.97928f, 37.57290f) }, // 종로구
        { 11140, new Vector2(126.99398f, 37.55986f) }, // 중구
        { 11260, new Vector2(127.09380f, 37.60199f) }  // 중랑구
    };

    // 만약 데이터에 없는 구역 ID가 들어올 경우를 대비한 서울 시청 기본 좌표
    public static readonly Vector2 DefaultCenter = new Vector2(126.9780f, 37.5665f);

    public static Vector2 GetCenter(int districtId)
    {
        if (CenterCoords.TryGetValue(districtId, out Vector2 coord))
        {
            return coord;
        }
        Debug.LogWarning($"[DistrictCoordinates] 매칭되는 좌표가 없습니다. ID: {districtId}. 기본 좌표 반환.");
        return DefaultCenter;
    }
}
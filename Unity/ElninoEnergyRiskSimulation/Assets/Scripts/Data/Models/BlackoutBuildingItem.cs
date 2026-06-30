/// <summary>
/// /blackout_simulation API의 blackout_items 항목 하나.
/// 단전 대상 건물유형과 수요감축 필요도 점수를 담는다.
/// </summary>
[System.Serializable]
public class BlackoutBuildingItem
{
    public string buildingType;        // API 한국어 키 (예: "공장", "업무시설")
    public float  reductionNeedScore;  // 수요감축 필요도 점수
}

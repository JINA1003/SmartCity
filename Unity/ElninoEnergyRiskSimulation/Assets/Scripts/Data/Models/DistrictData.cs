using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DistrictData
{
    public DistrictType districtType;
    public float temperature; //해당 평균기온
    public double totalPowerUsage; // total_consumption_mwh
    public Dictionary<string, float>typePowerUsage; //용도 별 사용량
    public Dictionary<BuildingType, float> buildingReductionScores; // 34개 건물 용도별 수요필요감축도
}

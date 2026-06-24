using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DistrictData
{
    public float districtNanme; // 구이름
    public float temperature; //해당 평균기온
    public double totalPowerUsage; // total_consumption_mwh
    public Dictionary<string, float>typePowerUsage; //용도 별 사용량
}

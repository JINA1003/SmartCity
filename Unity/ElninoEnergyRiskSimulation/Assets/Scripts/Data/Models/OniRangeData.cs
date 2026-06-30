using System.Collections.Generic;
using UnityEngine;

public class OniRangeData
{
    public float oni;
    public float seoulTemperature;
    public float supplyPower;
    public float reserveRate;
    public float seoulTotalConsumption;
    public int alert_level;
    public Dictionary<DistrictType, float> guTemperature;
    public Dictionary<DistrictType, double> guConsumption;
}

using System;
using System.Collections.Generic;

[Serializable]
public class PowerGridData
{
    public string year;
    public string month;
    public float  oni;
    public float  temperature;
    public int    riskLevel;
    public string riskLabel;
    public string oniStatus;

    // DataManager가 실제로 쓰는 필드명
    public float  seoulTemperature;
    public float  supplyPower;
    public float  reserveRate;
    public float  seoulTotalConsumption;
    public int    alert_level;

    public Dictionary<string, float>  guTemperature  = new();
    public Dictionary<string, double> guConsumption  = new();
}

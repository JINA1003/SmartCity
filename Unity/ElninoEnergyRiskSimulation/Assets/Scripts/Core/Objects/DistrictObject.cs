using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class DistrictObject : MonoBehaviour
{
    public DistrictData data;
    public List<BuildingData> buildings;
    public bool IsShutDown = false;

    public void Initialize(int districtId)
    {
        data.districtId = districtId;

    }
}

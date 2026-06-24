using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class DistrictObject : MonoBehaviour
{
    public DistrictData data;
    public List<GameObject> buildings;
    public Dictionary<BuildingType, float> buildingReducationScores;
    public bool IsShutDown = false;
}

using System.Collections.Generic;
using UnityEngine;

public class BuildingData
{
    public string id;
    public double lat;
    public double lon;
    public float height;
    public int floors;
    public string name;
    public List<Vector2> polygon;
    public float terrainAltitude = 0f;

    public DistrictType districtType;
    public BuildingType buildingType;
}
using System.Collections.Generic;
using UnityEngine;

public class BuildingData
{
    public int id;
    public int districtId;
    public DistrictType districtType;
    public BuildingType buildingType;

    public double lat;
    public double lon;

    public float height;

    public List<Vector2> polygon;

    public int floors;
    public string name;
}
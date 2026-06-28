using System.Collections.Generic;
using UnityEngine;

public class BuildingData
{
    public double lat;
    public double lon;
    public DistrictType districtType;
    public BuildingType buildingType;
    
    public float height;
    public float reuducationValue;

    public List<Vector2> polygon;
    public int id;
    public int districtId;

    public int floors;
    public string name;
}
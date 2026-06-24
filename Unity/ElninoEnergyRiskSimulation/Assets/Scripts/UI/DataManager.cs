using System.Collections.Generic;
using UnityEngine;

public class DistrictManager : MonoBehaviour
{
    public List<DistrictData> districts = new List<DistrictData>();

    // 구 이름으로 데이터 찾기
    public DistrictData GetDistrictData(string districtName)
    {
        return districts.Find(d => d.districtName == districtName);
    }
}
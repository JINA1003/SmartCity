using UnityEngine;

public class DistrictObject : MonoBehaviour
{
    public int districtId;
    public DistrictData data;
    public bool IsShutDown = false;

    private void Awake()
    {
        if (data == null)
        {
            data = new DistrictData();
        }
    }
}

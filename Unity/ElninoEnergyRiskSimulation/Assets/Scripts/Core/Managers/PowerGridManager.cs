using UnityEngine;

public class PowerGridManager : MonoBehaviour
{
    public static PowerGridManager Instance { get; private set; }

    PowerGridData data;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void AddData(PowerGridData data)
    {
        this.data = data;
    }
}

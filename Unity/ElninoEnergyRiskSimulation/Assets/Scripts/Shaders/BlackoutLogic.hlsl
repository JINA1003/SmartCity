
struct NativeBuildingData
{
    double lon;
    double lat;
    float height;
    float terrainAltitude;
    float reductionValue;
    int id;
    int districtId;
    int districtType;
    int buildingType;
    int isBlackout;
    int polygonVertexCount;
    int polygonStartIndex;
};

StructuredBuffer<NativeBuildingData> _BuildingDataBuffer;

void ApplyColorAndBlackout_float(float2 uv2, float3 originalColor, out float3 finalColor)
{
    uint dataIndex = (uint) uv2.x;
    NativeBuildingData data = _BuildingDataBuffer[dataIndex];

    if (data.isBlackout == 1)
    {
        finalColor = float3(0.02, 0.02, 0.02);
        return;
    }

    // API에서 받은 수치(0.0 ~ 1.0)를 기반으로 색상 보간 (Lerp)
    // 0.0(안전) = 파란색 -> 0.5(주의) = 노란색 -> 1.0(위험) = 붉은색
    
    float3 safeColor = float3(0.0, 0.5, 1.0); // Blue
    float3 warningColor = float3(1.0, 0.8, 0.0); // Yellow
    float3 dangerColor = float3(1.0, 0.1, 0.1); // Red

    float val = data.reductionValue;
    float3 heatColor;

    if (val < 0.5)
    {
        heatColor = lerp(safeColor, warningColor, val * 2.0); // 0~0.5 구간
    }
    else
    {
        heatColor = lerp(warningColor, dangerColor, (val - 0.5) * 2.0); // 0.5~1.0 구간
    }

    finalColor = heatColor;
}
using UnityEngine;

/// <summary>
/// 건물 용도별 전력 소비 시각화에 사용하는 100단계 색상 팔레트.
/// 낮은 소비(순위 하위) → 흰/베이지, 높은 소비(순위 상위) → 주황/빨강.
/// </summary>
public static class BuildingUsageColormap
{
    // ═══════════════════════════════════════════════════════════════════════
    // 상수
    // ═══════════════════════════════════════════════════════════════════════

    public const int PaletteSize = 100;

    // ═══════════════════════════════════════════════════════════════════════
    // 팔레트 색상 정의
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly Color LowColor         = new Color(1f,    1f,    1f,    1f);
    private static readonly Color BeigeColor       = new Color(0.96f, 0.94f, 0.86f, 1f);
    private static readonly Color LightOrangeColor = new Color(1f,    0.85f, 0.55f, 1f);
    private static readonly Color OrangeColor      = new Color(1f,    0.55f, 0.2f,  1f);
    private static readonly Color HighColor        = new Color(0.78f, 0.08f, 0.05f, 1f);

    // ═══════════════════════════════════════════════════════════════════════
    // 팔레트 샘플링
    // ═══════════════════════════════════════════════════════════════════════

    // t = 0(최소 소비) → 흰색, t = 1(최대 소비) → 짙은 빨강
    public static Color Evaluate(float t)
    {
        t = Mathf.Clamp01(t);

        if (t < 0.25f) return Color.Lerp(LowColor,         BeigeColor,       t / 0.25f);
        if (t < 0.5f)  return Color.Lerp(BeigeColor,       LightOrangeColor, (t - 0.25f) / 0.25f);
        if (t < 0.75f) return Color.Lerp(LightOrangeColor, OrangeColor,      (t - 0.5f)  / 0.25f);
        return             Color.Lerp(OrangeColor,          HighColor,        (t - 0.75f) / 0.25f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 텍스처 생성
    // ═══════════════════════════════════════════════════════════════════════

    // 셰이더가 팔레트 인덱스(정규화된 u좌표)로 색을 샘플링하는 RGBA32 1D 텍스처(100x1)
    public static Texture2D CreatePaletteTexture(int size = PaletteSize)
    {
        var texture = new Texture2D(size, 1, TextureFormat.RGBA32, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "BuildingUsagePalette",
        };

        for (int i = 0; i < size; i++)
        {
            float t = size <= 1 ? 0f : i / (float)(size - 1);
            texture.SetPixel(i, 0, Evaluate(t));
        }

        texture.Apply();
        return texture;
    }
}

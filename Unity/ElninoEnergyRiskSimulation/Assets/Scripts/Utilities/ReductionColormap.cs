using UnityEngine;

/// <summary>
/// 수요감축 필요도 시각화용 컬러맵. 낮음 → 초록, 높음 → 빨강.
/// HLSL 쪽에서는 _ColorPalette 텍스처를 t(0~1)로 샘플링하면 된다.
/// </summary>
public static class ReductionColormap
{
    public const int PaletteSize = 256;

    private static readonly Color LowColor  = new Color(0.15f, 0.72f, 0.28f, 1f); // 초록
    private static readonly Color MidColor  = new Color(0.98f, 0.88f, 0.12f, 1f); // 노랑
    private static readonly Color HighColor = new Color(0.85f, 0.10f, 0.08f, 1f); // 빨강

    public static Color Evaluate(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f
            ? Color.Lerp(LowColor, MidColor, t / 0.5f)
            : Color.Lerp(MidColor, HighColor, (t - 0.5f) / 0.5f);
    }

    public static Texture2D CreatePaletteTexture(int size = PaletteSize)
    {
        var texture = new Texture2D(size, 1, TextureFormat.RGBA32, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "ReductionColormap",
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

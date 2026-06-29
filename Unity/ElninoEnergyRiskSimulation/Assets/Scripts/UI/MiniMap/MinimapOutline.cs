using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapOutline : Graphic
{
    public List<Vector2> points = new List<Vector2>();

    public float lineWidth = 2f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (points == null || points.Count < 2)
            return;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Count];

            AddLine(vh, start, end);
        }
    }

    private void AddLine(VertexHelper vh, Vector2 start, Vector2 end)
    {
        Vector2 dir = (end - start).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x) * (lineWidth * 0.5f);

        // 선 끝을 살짝 연장해서 꼭짓점 틈 줄이기
        float extend = lineWidth * 0.5f;
        start -= dir * extend;
        end += dir * extend;

        int index = vh.currentVertCount;

        vh.AddVert(start - normal, color, Vector2.zero);
        vh.AddVert(start + normal, color, Vector2.zero);
        vh.AddVert(end + normal, color, Vector2.zero);
        vh.AddVert(end - normal, color, Vector2.zero);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    public void SetOutlineColor(Color newColor)
    {
        color = newColor;
        SetVerticesDirty();
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MinimapPolygon : Graphic, IPointerClickHandler
{
    public List<Vector2> points = new List<Vector2>();

    public string districtName;

    public MinimapManager minimapManager;

    public MinimapOutline outline;

    private  GuEnergyPanelUI guPanel;


    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (points == null || points.Count < 3)
            return;

        for (int i = 0; i < points.Count; i++)
        {
            vh.AddVert(points[i], color, Vector2.zero);
        }

        int[] triangles = new MinimapTrianguler(points.ToArray()).Triangulate();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            vh.AddTriangle(
                triangles[i],
                triangles[i + 1],
                triangles[i + 2]
            );
        }
    }

    public override bool Raycast(Vector2 sp, Camera eventCamera)
    {
        Vector2 localPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            sp,
            eventCamera,
            out localPoint
        );

        return IsPointInPolygon(localPoint, points);
    }

    private bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        bool inside = false;

        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            if (
                ((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                (point.x < (polygon[j].x - polygon[i].x) *
                    (point.y - polygon[i].y) /
                    (polygon[j].y - polygon[i].y) + polygon[i].x)
            )
            {
                inside = !inside;
            }
        }

        return inside;
    }

    // 특정 구 클릭시 실행되는 함수
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("클릭한 구: " + districtName);

        // TODO: 선택 구에 대한 info panel 뜨도록 해야함
        // guPanel.Show();
        // guPanel.Text_District_Energy.text = districtName;

        if (minimapManager != null)
        {
            // 여기서 클릭한 구에 대한 실행
            minimapManager.SelectDistrict(this);
        }
    }
}
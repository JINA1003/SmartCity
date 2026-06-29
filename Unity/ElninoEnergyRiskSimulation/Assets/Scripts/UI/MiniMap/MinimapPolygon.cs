using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// [인터페이스]
/// IPointerClickHandler: 클릭 감지
/// IPointerEnterHandler: 마우스가 올라왔을 때
/// IPointerExitHandler: 마우스가 벗어났을 때
/// IPointerMoveHandler: 마우스가 움직일 때
/// </summary>

public class MinimapPolygon : Graphic, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    // 폴리곤 point 리스트
    public List<Vector2> points = new List<Vector2>();

    public string districtName;

    // 툴팁 처리 위해
    public MinimapManager minimapManager;

    public MinimapOutline outline;

    // polygon 모양을 실제 UI Mesh로 그리는 함수
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        // 기존 Mesh 삭제
        vh.Clear();

        // 점이 3개 미만이면 폴리곤 그릴 수 없어 종료
        if (points == null || points.Count < 3)
            return;

        // 각 point를 ui mesh에 추가
        for (int i = 0; i < points.Count; i++)
        {
            vh.AddVert(points[i], color, Vector2.zero);
        }

        // 폴리곤을 여러 삼각형으로
        // : unity는 다각형이 아닌 삼각형 단위로 그리기 때문
        int[] triangles = new MinimapTrianguler(points.ToArray()).Triangulate();

        // 삼각형 인덱스를 3개씩 묶어 처리
        for (int i = 0; i < triangles.Length; i += 3)
        {
            vh.AddTriangle(
                triangles[i],
                triangles[i + 1],
                triangles[i + 2]
            );
        }
    }

    // 마우스가 폴리곤 안에 있는지 판단
    public override bool Raycast(Vector2 sp, Camera eventCamera)
    {
        // 마우스 위치
        Vector2 localPoint;

        // 화면 좌표(sp)를 UI 객체 기준의 로컬 좌표로 반환
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
        if (minimapManager != null)
        {
            // 여기서 클릭한 구에 대한 실행
            minimapManager.SelectDistrict(this);
        }
    }

    // 마우스가 구 위에 올라왔을 때
public void OnPointerEnter(PointerEventData eventData)
    {
        if (minimapManager != null)
        {
            minimapManager.ShowDistrictTooltip(
                districtName,
                eventData.position
            );
        }
    }

    // 마우스가 움직일 때 Tooltip도 따라 이동
    public void OnPointerMove(PointerEventData eventData)
    {
        if (minimapManager != null)
        {
            minimapManager.MoveDistrictTooltip(
                eventData.position
            );
        }
    }

    // 마우스가 구 밖으로 나갔을 때
    public void OnPointerExit(PointerEventData eventData)
    {
        if (minimapManager != null)
        {
            minimapManager.HideDistrictTooltip();
        }
    }
}
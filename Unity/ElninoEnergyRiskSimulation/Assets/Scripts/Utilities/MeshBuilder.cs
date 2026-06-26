using System.Collections.Generic;
using UnityEngine;

// 건물 폴리곤으로부터 3D 메시를 생성하는 정적 유틸리티 클래스.
// 상태를 가지지 않으므로 static class 로 구현한다.
// BuildingManager 의 SpawnBuilding 에서 호출한다.
public static class MeshBuilder
{
    // 폴리곤과 높이를 받아 건물 메시를 생성한다.
    // 실패 시 null 을 반환하며 호출부에서 반드시 null 체크가 필요하다.
    public static Mesh BuildPolygonMesh(List<Vector2> polygon, float height)
    {
        if (polygon == null || polygon.Count < 3)
        {
            Debug.LogWarning("[MeshBuilderV2] 정점 수 부족 (3개 미만)");
            return null;
        }

        polygon = RemoveDuplicateVertices(polygon);
        if (polygon.Count < 3)
        {
            Debug.LogWarning("[MeshBuilderV2] 중복 정점 제거 후 정점 수 부족");
            return null;
        }

        int n = polygon.Count;

        // PolyExtruder 와 동일한 방식으로 시계방향으로 통일
        if (!AreVerticesClockwise(polygon))
            polygon.Reverse();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // 1. 벽면 생성 (쿼드 스트립)
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;

            Vector3 b0 = new Vector3(polygon[i].x, 0, polygon[i].y);
            Vector3 b1 = new Vector3(polygon[next].x, 0, polygon[next].y);
            Vector3 t0 = new Vector3(polygon[i].x, height, polygon[i].y);
            Vector3 t1 = new Vector3(polygon[next].x, height, polygon[next].y);

            int idx = vertices.Count;
            vertices.Add(b0);
            vertices.Add(b1);
            vertices.Add(t0);
            vertices.Add(t1);

            triangles.Add(idx);
            triangles.Add(idx + 1);
            triangles.Add(idx + 2);
            triangles.Add(idx + 1);
            triangles.Add(idx + 3);
            triangles.Add(idx + 2);
        }

        // 2. 지붕 생성 (EarClipping 삼각화)
        List<int> roofTris = EarClipping(polygon);
        if (roofTris == null)
        {
            Debug.LogWarning("[MeshBuilderV2] 지붕 EarClipping 실패 - 자기교차 폴리곤으로 추정");
            return null;
        }

        int roofBase = vertices.Count;
        for (int i = 0; i < n; i++)
            vertices.Add(new Vector3(polygon[i].x, height, polygon[i].y));
        foreach (int t in roofTris)
            triangles.Add(roofBase + t);

        // 3. 바닥 생성 (지붕 역순)
        int floorBase = vertices.Count;
        for (int i = 0; i < n; i++)
            vertices.Add(new Vector3(polygon[i].x, 0, polygon[i].y));
        for (int i = 0; i < roofTris.Count; i += 3)
        {
            triangles.Add(floorBase + roofTris[i]);
            triangles.Add(floorBase + roofTris[i + 2]);
            triangles.Add(floorBase + roofTris[i + 1]);
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }


    // 연속된 중복 정점을 제거한다. (임계값: 0.01m)
    static List<Vector2> RemoveDuplicateVertices(List<Vector2> polygon, float threshold = 0.01f)
    {
        List<Vector2> result = new List<Vector2>();
        for (int i = 0; i < polygon.Count; i++)
        {
            int next = (i + 1) % polygon.Count;
            if (Vector2.Distance(polygon[i], polygon[next]) > threshold)
                result.Add(polygon[i]);
        }
        return result;
    }


    // 정점 순서가 시계방향인지 확인한다. (Shoelace formula - PolyExtruder 동일 방식)
    static bool AreVerticesClockwise(List<Vector2> vertices)
    {
        float edgesSum = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            int next = (i + 1) % vertices.Count;
            edgesSum += (vertices[next].x - vertices[i].x)
                      * (vertices[next].y + vertices[i].y);
        }
        return edgesSum >= 0f;
    }


    // EarClipping 방식으로 폴리곤을 삼각화한다.
    // 자기교차 등 처리 불가 폴리곤은 null 을 반환한다.
    static List<int> EarClipping(List<Vector2> polygon)
    {
        List<int> result = new List<int>();
        List<int> indices = new List<int>();
        for (int i = 0; i < polygon.Count; i++)
            indices.Add(i);

        // 복잡한 폴리곤에도 충분하도록 여유있게 설정
        int maxIter = polygon.Count * polygon.Count * 2;
        int iter = 0;

        while (indices.Count > 3 && iter < maxIter)
        {
            iter++;
            bool earFound = false;

            for (int i = 0; i < indices.Count; i++)
            {
                int prev = indices[(i - 1 + indices.Count) % indices.Count];
                int curr = indices[i];
                int next = indices[(i + 1) % indices.Count];

                Vector2 a = polygon[prev];
                Vector2 b = polygon[curr];
                Vector2 c = polygon[next];

                // double 정밀도 외적으로 오목 정점 판별
                double cross = ((double)(b.x - a.x)) * ((double)(c.y - a.y))
                             - ((double)(b.y - a.y)) * ((double)(c.x - a.x));

                if (cross >= 0) continue;

                bool hasPoint = false;
                foreach (int idx in indices)
                {
                    if (idx == prev || idx == curr || idx == next) continue;
                    if (PointInTriangle(polygon[idx], a, b, c))
                    {
                        hasPoint = true;
                        break;
                    }
                }

                if (!hasPoint)
                {
                    result.Add(prev);
                    result.Add(curr);
                    result.Add(next);
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            if (!earFound)
            {
                Debug.LogWarning("[MeshBuilderV2] EarClipping: ear 탐색 실패 (잔여 정점 "
                                 + indices.Count + "개) - 자기교차 폴리곤으로 추정");
                return null;
            }
        }

        if (indices.Count == 3)
        {
            result.Add(indices[0]);
            result.Add(indices[1]);
            result.Add(indices[2]);
        }
        else if (indices.Count != 0)
        {
            Debug.LogWarning("[MeshBuilderV2] EarClipping: maxIter 초과 (잔여 "
                             + indices.Count + "개)");
            return null;
        }

        return result;
    }


    // double 정밀도 외적 계산
    static double Cross(Vector2 o, Vector2 a, Vector2 b)
    {
        return ((double)(a.x - o.x)) * ((double)(b.y - o.y))
             - ((double)(a.y - o.y)) * ((double)(b.x - o.x));
    }


    // 점이 삼각형 안에 있는지 확인한다.
    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        double d1 = Cross(p, a, b);
        double d2 = Cross(p, b, c);
        double d3 = Cross(p, c, a);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }
}
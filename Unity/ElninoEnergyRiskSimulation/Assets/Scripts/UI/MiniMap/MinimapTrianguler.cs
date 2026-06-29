using UnityEngine;
using System.Collections.Generic;

public class MinimapTrianguler
{
    // 폴리곤 꼭짓점 list
    private List<Vector2> points;

    // 생성자
    // : 외부에서 받은 배열을 points에 저장
    public MinimapTrianguler(Vector2[] points)
    {
        this.points = new List<Vector2>(points);
    }

    // 폴리곤을 삼각형으로 나누고 사용량 인덱스 배열 반환
    public int[] Triangulate()
    {
        List<int> indices = new List<int>();

        int n = points.Count;

        // 삼각형 -> 최소 3개 점 필요
        if (n < 3)
            return indices.ToArray(); // 2개 이하 -> 빈 배열 반환

        int[] V = new int[n];

        // 폴리곤 점 시계 방향 확인
        if (Area() > 0)
        {
            for (int v = 0; v < n; v++)
                V[v] = v;
        }
        else
        {
            for (int v = 0; v < n; v++)
                V[v] = (n - 1) - v; // 음수면 순서 뒤집기
        }

        int nv = n; // 현재 남아 있는 꼭짓점 개수
        int count = 2 * nv;

        // 삼각형 자르기 반복
        // 3개 점 남으면 반복 끝
        for (int v = nv - 1; nv > 2;)
        {
            if ((count--) <= 0)
                break;

            // u, v, w -> 연속된 세 꼭짓점
            int u = v;

            if (nv <= u)
                u = 0;

            v = u + 1;

            if (nv <= v)
                v = 0;

            int w = v + 1;

            if (nv <= w)
                w = 0;

            if (Snip(u, v, w, nv, V))
            {
                int a = V[u];
                int b = V[v];
                int c = V[w];

                indices.Add(a);
                indices.Add(b);
                indices.Add(c);

                for (int s = v, t = v + 1; t < nv; s++, t++)
                    V[s] = V[t];

                nv--;
                count = 2 * nv;
            }
        }

        // 삼각형 인덱스 순서 뒤집기
        // 면이 반대로 보이는 문제 방지 (?)
        indices.Reverse();

        return indices.ToArray();
    }

    // 폴리곤 면적 계산
    private float Area()
    {
        int n = points.Count;
        float A = 0f;

        for (int p = n - 1, q = 0; q < n; p = q++)
        {
            Vector2 pval = points[p];
            Vector2 qval = points[q];

            // 다각형 면적 공식
            A += pval.x * qval.y - qval.x * pval.y;
        }

        return A * 0.5f;
    }

    // 세 점이 잘라낼 수 있는 삼각형인지 검사
    private bool Snip(int u, int v, int w, int n, int[] V)
    {
        Vector2 A = points[V[u]];
        Vector2 B = points[V[v]];
        Vector2 C = points[V[w]];

        // 삼각형 넓이 검사
        if (
            Mathf.Epsilon >
            (((B.x - A.x) * (C.y - A.y)) -
             ((B.y - A.y) * (C.x - A.x)))
        )
            return false;

        // 다른 점이 삼각형 안에 있는지 검사
        for (int p = 0; p < n; p++)
        {
            if ((p == u) || (p == v) || (p == w))
                continue;

            Vector2 P = points[V[p]];

            if (InsideTriangle(A, B, C, P))
                return false;
        }

        return true;
    }

    // 점 p가 세 점 안에 있는지 확인
    private bool InsideTriangle(Vector2 A, Vector2 B, Vector2 C, Vector2 P)
    {
        float ax = C.x - B.x;
        float ay = C.y - B.y;
        float bx = A.x - C.x;
        float by = A.y - C.y;
        float cx = B.x - A.x;
        float cy = B.y - A.y;

        float apx = P.x - A.x;
        float apy = P.y - A.y;
        float bpx = P.x - B.x;
        float bpy = P.y - B.y;
        float cpx = P.x - C.x;
        float cpy = P.y - C.y;

        float aCROSSbp = ax * bpy - ay * bpx;
        float cCROSSap = cx * apy - cy * apx;
        float bCROSScp = bx * cpy - by * cpx;

        return (
            (aCROSSbp >= 0.0f) &&
            (bCROSScp >= 0.0f) &&
            (cCROSSap >= 0.0f)
        );
    }
}
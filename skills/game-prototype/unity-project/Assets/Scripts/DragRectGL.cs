using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 첫사이클 T12 (2026-06-12) — 구역 드래그 중 미리보기 0 (벽 드래그는 고스트가
    /// 있는데 저장/경작은 release 에서야 일괄 Mark 라 깜깜이 드래그).  MarqueeSelector
    /// 의 GL 사각형 패턴을 공용 헬퍼로 추출 — designation 들의 OnRenderObject 에서
    /// 호출한다.  순수 시각(레이캐스트/물리 무영향).
    /// </summary>
    public static class DragRectGL
    {
        private static Material lineMat;

        private static void EnsureMat()
        {
            if (lineMat != null) return;
            var shader = Shader.Find("Hidden/Internal-Colored");
            lineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMat.SetInt("_ZWrite", 0);
        }

        /// <summary>월드 좌표 두 점의 사각형을 뷰포트 공간에 그린다 (마퀴와 동일 문법).</summary>
        public static void Draw(Camera cam, Vector3 worldA, Vector3 worldB, Color fill, Color border)
        {
            if (cam == null) return;
            EnsureMat();
            lineMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadOrtho();
            Vector3 a = cam.WorldToViewportPoint(worldA);
            Vector3 b = cam.WorldToViewportPoint(worldB);
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
            float y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
            GL.Begin(GL.QUADS);
            GL.Color(fill);
            GL.Vertex3(x0, y0, 0f); GL.Vertex3(x1, y0, 0f);
            GL.Vertex3(x1, y1, 0f); GL.Vertex3(x0, y1, 0f);
            GL.End();
            GL.Begin(GL.LINES);
            GL.Color(border);
            GL.Vertex3(x0, y0, 0f); GL.Vertex3(x1, y0, 0f);
            GL.Vertex3(x1, y0, 0f); GL.Vertex3(x1, y1, 0f);
            GL.Vertex3(x1, y1, 0f); GL.Vertex3(x0, y1, 0f);
            GL.Vertex3(x0, y1, 0f); GL.Vertex3(x0, y0, 0f);
            GL.End();
            GL.PopMatrix();
        }
    }
}

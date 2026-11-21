using System.Collections.Generic;
using UnityEngine;

namespace DeadCircuit
{
    /// <summary>One clipped UI mesh for the dependency paths; never intercepts pointer input.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TechnologyTreeLines : UnityEngine.UI.MaskableGraphic
    {
        public sealed class Path
        {
            public Vector2 from, to;
            public float laneY;
            public bool acrossEra;
            public Color color;
        }
        public readonly List<Path> paths = new List<Path>();

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            foreach (var path in paths)
            {
                if (path.acrossEra)
                {
                    Vector2 a = path.from + Vector2.right * 16;
                    Vector2 b = new Vector2(a.x, path.laneY);
                    Vector2 c = new Vector2(path.to.x - 16, path.laneY);
                    Vector2 d = new Vector2(c.x, path.to.y);
                    Segment(mesh, path.from, a, path.color);
                    Segment(mesh, a, b, path.color);
                    Segment(mesh, b, c, path.color);
                    Segment(mesh, c, d, path.color);
                    Segment(mesh, d, path.to, path.color);
                }
                else
                {
                    float x = (path.from.x + path.to.x) * .5f;
                    Vector2 a = new Vector2(x, path.from.y);
                    Vector2 b = new Vector2(x, path.to.y);
                    Segment(mesh, path.from, a, path.color);
                    Segment(mesh, a, b, path.color);
                    Segment(mesh, b, path.to, path.color);
                }
                Arrow(mesh, path.to, path.color);
            }
        }

        static void Segment(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, Color color)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < .01f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * 1.2f;
            int start = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }

        static void Arrow(UnityEngine.UI.VertexHelper mesh, Vector2 end, Color color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(end, color, Vector2.zero);
            mesh.AddVert(end + new Vector2(-7, 4), color, Vector2.zero);
            mesh.AddVert(end + new Vector2(-7, -4), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }
    }
}

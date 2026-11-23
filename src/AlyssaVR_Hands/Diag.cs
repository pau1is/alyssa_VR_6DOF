using System;
using System.Text;
using UnityEngine;

namespace AlyssaVRHands
{
    public static class Diag
    {
        public static string Dump(Transform root, int maxDepth)
        {
            StringBuilder sb = new StringBuilder();
            DumpRec(root, 0, maxDepth, sb);
            return sb.ToString();
        }

        private static void DumpRec(Transform t, int depth, int maxDepth, StringBuilder sb)
        {
            sb.Append(' ', depth * 2);
            sb.Append(t.name);
            sb.Append(t.gameObject.activeInHierarchy ? "" : " (inactive)");
            sb.Append("  lp=").Append(t.localPosition.ToString("F2"));
            if (t.localScale != Vector3.one) sb.Append(" ls=").Append(t.localScale.ToString("F3"));
            sb.Append(" le=").Append(t.localEulerAngles.ToString("F0"));
            sb.Append(" [");
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                sb.Append(c.GetType().Name);
                Behaviour b = c as Behaviour;
                if (b != null && !b.enabled) sb.Append("(off)");
                Light l = c as Light;
                if (l != null) sb.Append("{" + l.type + " i=" + l.intensity.ToString("F1") + " r=" + l.range.ToString("F1") + "}");
                ParticleSystem ps = c as ParticleSystem;
                if (ps != null) sb.Append("{scaling=" + ps.main.scalingMode + " sim=" + ps.main.simulationSpace
                    + " shapePos=" + ps.shape.position.ToString("F2") + " size=" + ps.main.startSizeMultiplier.ToString("F3") + "}");
                MeshFilter mf = c as MeshFilter;
                if (mf != null && mf.sharedMesh != null) sb.Append("{bounds c=" + mf.sharedMesh.bounds.center.ToString("F2")
                    + " s=" + mf.sharedMesh.bounds.size.ToString("F2") + "}");
                Renderer r = c as Renderer;
                if (r != null && !r.enabled) sb.Append("(hidden)");
                sb.Append(' ');
            }
            sb.Append("]\n");
            if (depth >= maxDepth) return;
            for (int i = 0; i < t.childCount; i++) DumpRec(t.GetChild(i), depth + 1, maxDepth, sb);
        }
    }
}

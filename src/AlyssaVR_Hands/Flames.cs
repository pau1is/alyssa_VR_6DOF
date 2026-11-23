using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlyssaVRHands
{
    // The game's match flame sits a bit beside the match head; from the flat camera that is invisible
    // (the offset is along the view), in VR it shows. Puts the flame emitters back onto the match's axis,
    // plus an optional manual offset (metres, controller space).
    internal static class Flames
    {
        private static readonly Dictionary<int, Vector3> orig = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, Vector3> fixedPos = new Dictionary<int, Vector3>();
        private static readonly HashSet<int> logged = new HashSet<int>();

        private static bool Wanted(Transform anchor)
        {
            foreach (string s in Plugin.CfgFlameFixFor.Value.Split(','))
                if (s.Trim().Length > 0 && s.Trim() == anchor.name) return true;
            return false;
        }

        public static void Apply(Transform anchor, Transform hand)
        {
            if (anchor == null || !Wanted(anchor)) return;
            Vector3 manual = Attach.ParseV(Plugin.CfgFlameOffset.Value, Vector3.zero);
            bool snap = Plugin.CfgFlameFix.Value;
            foreach (MeshFilter mf in anchor.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf.sharedMesh == null) continue;
                Transform stick = mf.transform;
                Bounds b = mf.sharedMesh.bounds;
                Vector3 axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? Vector3.right
                             : (b.size.y >= b.size.z ? Vector3.up : Vector3.forward);
                for (int i = 0; i < stick.childCount; i++)
                {
                    Transform c = stick.GetChild(i);
                    if (c.GetComponent<ParticleSystem>() == null && c.GetComponent<Light>() == null) continue;
                    int id = c.GetInstanceID();
                    if (!orig.ContainsKey(id))
                    {
                        Vector3 p = c.localPosition;
                        orig[id] = p;
                        Vector3 d = p - b.center;
                        Vector3 lateral = d - axis * Vector3.Dot(d, axis);
                        fixedPos[id] = p - lateral;
                        if (logged.Add(id))
                            Plugin.Log.LogInfo("Flame " + c.name + " on " + stick.name + ": moved " + lateral.ToString("F2")
                                + " (mesh units) onto the match axis");
                    }
                    Vector3 lp = snap ? fixedPos[id] : orig[id];
                    if (manual != Vector3.zero)
                        lp += stick.InverseTransformVector(hand.rotation * manual);
                    c.localPosition = lp;
                }
            }
        }

        public static void Restore(Transform root)
        {
            if (root == null) return;
            foreach (Transform c in root.GetComponentsInChildren<Transform>(true))
            {
                Vector3 p;
                if (orig.TryGetValue(c.GetInstanceID(), out p)) c.localPosition = p;
            }
        }
    }
}

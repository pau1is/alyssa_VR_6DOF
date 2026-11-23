using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AlyssaVRHands
{
    // Hides chosen pieces of the game's HUD (crosshair, "[F] light a match" hint ...).
    // Entries: a path ending ("Canvas_Matches/Text") or a name pattern with * ("*crosshair*", case-insensitive).
    internal static class HideUI
    {
        private static string raw;
        private static readonly List<string> paths = new List<string>();
        private static readonly List<string> patterns = new List<string>();
        private static readonly List<Graphic> hidden = new List<Graphic>();
        private static readonly Dictionary<Graphic, Vector3> origScale = new Dictionary<Graphic, Vector3>();
        private static float nextScan;
        private static readonly HashSet<int> dumpedHud = new HashSet<int>();

        public static void LateUpdate()
        {
            if (Plugin.CfgHideUI.Value != raw)
            {
                foreach (Graphic g in hidden)
                {
                    if (g == null) continue;
                    g.enabled = true;
                    Vector3 s;
                    if (origScale.TryGetValue(g, out s)) g.transform.localScale = s;
                }
                hidden.Clear();
                origScale.Clear();
                raw = Plugin.CfgHideUI.Value;
                paths.Clear();
                patterns.Clear();
                foreach (string s in raw.Split(','))
                {
                    string t = s.Trim();
                    if (t.Length == 0) continue;
                    if (t.IndexOf('*') >= 0) patterns.Add(t.Replace("*", "").ToLowerInvariant());
                    else paths.Add(t);
                }
                nextScan = 0f;
            }

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 2f;
                Scan();
            }
            for (int i = hidden.Count - 1; i >= 0; i--)
            {
                Graphic g = hidden[i];
                if (g == null) { hidden.RemoveAt(i); continue; }
                if (g.enabled) g.enabled = false;
                // the game switches some hints back on by itself - a zero size keeps them invisible anyway
                if (!origScale.ContainsKey(g)) origScale[g] = g.transform.localScale;
                if (g.transform.localScale != Vector3.zero) g.transform.localScale = Vector3.zero;
            }
        }

        private static void Scan()
        {
            foreach (Graphic g in Resources.FindObjectsOfTypeAll<Graphic>())
            {
                if (g == null || !g.gameObject.scene.IsValid() || hidden.Contains(g)) continue;
                Transform t = g.transform;
                // skip ALYSSA VR's own UI, but not the game's match / lighter that now hangs on the VR controller
                if (Plugin.IsModObject(t) && Plugin.Path(t).IndexOf("/ItemHandle/", StringComparison.Ordinal) < 0) continue;
                if (Matches(t))
                {
                    hidden.Add(g);
                    Plugin.Log.LogInfo("Hiding UI: " + Plugin.Path(t) + " (" + g.GetType().Name + ")");
                }
            }
            // Log the HUD layout once per HUD instance, so the right names can be put into HideUI.
            if (Plugin.CfgDumpHud.Value)
            {
                foreach (Canvas c in Resources.FindObjectsOfTypeAll<Canvas>())
                {
                    if (c == null || !c.gameObject.scene.IsValid() || !c.isRootCanvas) continue;
                    if (c.name != "Canvas_HUD") continue;
                    if (!dumpedHud.Add(c.GetInstanceID())) continue;
                    Plugin.Log.LogInfo("HUD layout " + Plugin.Path(c.transform) + ":\n" + Diag.Dump(c.transform, 6));
                }
            }
        }

        private static bool Matches(Transform t)
        {
            string n = t.name.ToLowerInvariant();
            foreach (string p in patterns) if (n.Contains(p)) return true;
            if (paths.Count == 0) return false;
            string full = null;
            foreach (string p in paths)
            {
                int slash = p.LastIndexOf('/');
                string last = slash >= 0 ? p.Substring(slash + 1) : p;
                if (t.name != last) continue;
                if (slash < 0) return true;
                if (full == null) full = Plugin.Path(t);
                if (full.EndsWith(p, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}

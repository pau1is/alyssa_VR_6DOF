using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlyssaVRHands
{
    // Objects held in the item hand (the game's Hands/ItemHandle = match / lighter).
    // They are re-parented onto the VR controller, so the head camera / GunSway no longer move them
    // (moving them every frame made the flame particles spawn at the face = double image).
    internal static class Attach
    {
        private class Held
        {
            public Transform T;
            public Transform OrigParent;
            public Vector3 OrigLocalPos;
            public Quaternion OrigLocalRot;
            public Vector3 OrigLocalScale;
            public Vector3 OrigLossy;
            public bool HiddenByUs;
            public Transform LastAnchor;
        }

        private static string anchorsRaw;
        private static string[] anchors = new string[0];

        private static Transform ActiveAnchor(Transform holder)
        {
            if (Plugin.CfgAnchors.Value != anchorsRaw)
            {
                anchorsRaw = Plugin.CfgAnchors.Value;
                List<string> l = new List<string>();
                foreach (string s in anchorsRaw.Split(',')) if (s.Trim().Length > 0) l.Add(s.Trim());
                anchors = l.ToArray();
            }
            foreach (string n in anchors)
            {
                Transform a = holder.Find(n);
                if (a != null && a.gameObject.activeInHierarchy) return a;
            }
            return null;
        }

        private static string namesRaw;
        private static string[] names = new string[0];
        private static readonly Dictionary<string, Held> held = new Dictionary<string, Held>();
        private static float nextFind;
        private static string offRaw, rotRaw;
        private static Vector3 off;
        private static Quaternion rot = Quaternion.identity;

        internal static Vector3 ParseV(string s, Vector3 def)
        {
            string[] p = s.Split(',');
            if (p.Length != 3) return def;
            float x, y, z;
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            if (float.TryParse(p[0].Trim(), System.Globalization.NumberStyles.Float, ci, out x)
                && float.TryParse(p[1].Trim(), System.Globalization.NumberStyles.Float, ci, out y)
                && float.TryParse(p[2].Trim(), System.Globalization.NumberStyles.Float, ci, out z))
                return new Vector3(x, y, z);
            return def;
        }

        public static void LateUpdate()
        {
            bool enabled = Plugin.CfgEnabled.Value;
            string want = enabled ? Plugin.CfgAttach.Value : "";
            if (want != namesRaw)
            {
                ReleaseAll();
                namesRaw = want;
                List<string> l = new List<string>();
                foreach (string s in namesRaw.Split(',')) if (s.Trim().Length > 0) l.Add(s.Trim());
                names = l.ToArray();
                nextFind = 0;
            }
            if (names.Length == 0) return;

            HandPose h = HandRig.Item;
            h.Refresh();
            Transform hand = h.AimT;
            if (hand == null) return;

            if (Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + 1f;
                foreach (string n in names)
                {
                    Held x;
                    if (held.TryGetValue(n, out x) && x.T != null) continue;
                    Transform t = FindByName(n);
                    if (t == null || t.parent == null) continue;
                    x = new Held();
                    x.T = t;
                    x.OrigParent = t.parent;
                    x.OrigLocalPos = t.localPosition;
                    x.OrigLocalRot = t.localRotation;
                    x.OrigLocalScale = t.localScale;
                    x.OrigLossy = t.lossyScale;
                    held[n] = x;
                    Plugin.Log.LogInfo("Holding in hand: " + Plugin.Path(t) + " (scale " + x.OrigLossy.ToString("F3") + ")");
                }
            }

            if (Plugin.CfgAttachOffset.Value != offRaw || Plugin.CfgAttachRotation.Value != rotRaw)
            {
                offRaw = Plugin.CfgAttachOffset.Value;
                rotRaw = Plugin.CfgAttachRotation.Value;
                off = ParseV(offRaw, Vector3.zero);
                rot = Quaternion.Euler(ParseV(rotRaw, new Vector3(71, 278, 271)));
            }
            float scale = Mathf.Clamp(Plugin.CfgItemScale.Value, 0.05f, 20f);

            List<string> remove = null;
            foreach (KeyValuePair<string, Held> kv in held)
            {
                Held x = kv.Value;
                if (x.T == null)
                {
                    if (remove == null) remove = new List<string>();
                    remove.Add(kv.Key);
                    continue;
                }
                if (x.OrigParent == null)
                {
                    // The player's rig was destroyed (level change) - don't keep its item alive on the controller.
                    UnityEngine.Object.Destroy(x.T.gameObject);
                    if (remove == null) remove = new List<string>();
                    remove.Add(kv.Key);
                    continue;
                }

                if (x.T.parent != hand)
                {
                    x.T.SetParent(hand, false);
                    Plugin.Log.LogInfo("Item attached to " + Plugin.Path(hand) + "; size " + Size(x.T));
                }
                x.T.localPosition = off;
                x.T.localRotation = rot;
                Vector3 hs = hand.lossyScale;
                x.T.localScale = new Vector3(
                    x.OrigLossy.x * scale / (Mathf.Abs(hs.x) > 1e-6f ? hs.x : 1f),
                    x.OrigLossy.y * scale / (Mathf.Abs(hs.y) > 1e-6f ? hs.y : 1f),
                    x.OrigLossy.z * scale / (Mathf.Abs(hs.z) > 1e-6f ? hs.z : 1f));

                // Put the item itself (match / lighter) into the hand, not the holder's pivot -
                // otherwise scaling the holder up pushes the item away from the controller.
                Transform anchor = ActiveAnchor(x.T);
                if (anchor != null)
                {
                    x.T.position += hand.TransformPoint(off) - anchor.position;
                    Flames.Apply(anchor, hand);
                    if (anchor != x.LastAnchor)
                    {
                        x.LastAnchor = anchor;
                        Plugin.Log.LogInfo("In hand: " + anchor.name + ", size " + Size(anchor) + " (ItemScale " + scale + ")");
                    }
                }
                else x.LastAnchor = null;

                // Show it only while the game shows its first-person hands.
                bool gameShows = x.OrigParent.gameObject.activeInHierarchy;
                if (!gameShows && x.T.gameObject.activeSelf)
                {
                    x.T.gameObject.SetActive(false);
                    x.HiddenByUs = true;
                }
                else if (gameShows && x.HiddenByUs)
                {
                    x.T.gameObject.SetActive(true);
                    x.HiddenByUs = false;
                }
            }
            if (remove != null) foreach (string k in remove) held.Remove(k);
        }

        public static void DumpAll()
        {
            foreach (KeyValuePair<string, Held> kv in held)
            {
                if (kv.Value.T == null) continue;
                Plugin.Log.LogInfo("Held " + kv.Key + " (lossy scale " + kv.Value.T.lossyScale.ToString("F3") + "):\n"
                    + Diag.Dump(kv.Value.T, 6));
            }
            if (held.Count == 0) Plugin.Log.LogInfo("Nothing held yet");
        }

        private static void ReleaseAll()
        {
            foreach (Held x in held.Values)
            {
                if (x.T == null || x.OrigParent == null) continue;
                Flames.Restore(x.T);
                x.T.SetParent(x.OrigParent, false);
                x.T.localPosition = x.OrigLocalPos;
                x.T.localRotation = x.OrigLocalRot;
                x.T.localScale = x.OrigLocalScale;
                if (x.HiddenByUs) x.T.gameObject.SetActive(true);
            }
            held.Clear();
        }

        private static string Size(Transform t)
        {
            Bounds b = new Bounds();
            bool any = false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(false))
            {
                if (!(r is MeshRenderer)) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? (b.size.ToString("F3") + " m") : "(no visible mesh)";
        }

        private static Transform FindByName(string n)
        {
            // Searches active and inactive objects in loaded scenes (name, or path ending like "Hands/ItemHandle").
            int slash = n.LastIndexOf('/');
            string last = slash >= 0 ? n.Substring(slash + 1) : n;
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || t.name != last) continue;
                if (!t.gameObject.scene.IsValid() || Plugin.IsModObject(t)) continue;
                if (slash < 0 || Plugin.Path(t).EndsWith(n, StringComparison.Ordinal)) return t;
            }
            return null;
        }
    }
}

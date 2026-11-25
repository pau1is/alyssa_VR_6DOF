using System;
using System.Collections.Generic;
using GameCreator.Runtime.Characters;
using UnityEngine;

namespace AlyssaVRHands
{
    // Keeps the head out of walls: right before the VR eyes render, a sphere is swept from the player's body
    // to the headset. If something is in the way, the whole VR rig (eyes, hands, held match, HUD panel) is moved
    // back for that frame so the eyes stay in front of the wall. Restored after rendering.
    internal static class HeadCollision
    {
        private static Character player;
        private static CharacterController cc;
        internal static CharacterController Controller { get { return player != null ? cc : null; } }
        private static float nextFind;
        private static Transform head;
        private static readonly List<Transform> roots = new List<Transform>();
        private static string rootsRaw;
        private static int rootsWanted = 1;

        private static int frameComputed = -1;
        private static Vector3 correction;
        private static bool shifted;
        private static int rendersThisFrame;
        private static float nextLog;
        private static bool loggedMatrixMode;

        private static readonly Dictionary<Camera, Matrix4x4> savedView = new Dictionary<Camera, Matrix4x4>();

        public static void Init()
        {
            Camera.onPreCull += OnPreCull;
            Camera.onPostRender += OnPostRender;
        }

        private static bool IsEye(Camera c)
        {
            return c != null && (c.name == "VR_LeftEye" || c.name == "VR_RightEye");
        }

        // Called from the plugin's Update: safety restore + find objects.
        public static void Update()
        {
            Restore();
            if (Time.unscaledTime < nextFind) return;
            nextFind = Time.unscaledTime + 2f;
            if (player == null || !player.isActiveAndEnabled)
            {
                player = null;
                cc = null;
                foreach (Character c in UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
                {
                    if (c != null && c.IsPlayer) { player = c; break; }
                }
                if (player != null)
                {
                    cc = player.GetComponent<CharacterController>();
                    Plugin.Log.LogInfo("Head collision: player " + player.name + (cc != null ? " (capsule h=" + cc.height.ToString("F2") + " r=" + cc.radius.ToString("F2") + ")" : " (no CharacterController)"));
                }
            }
            if (head == null)
            {
                GameObject r = GameObject.Find("VR_Root");
                if (r != null) head = r.transform.Find("VR_Holder/VR_Head");
            }
            // (re)find the VR objects to move - also while none were found yet (they appear after the game starts)
            if (Plugin.CfgShiftRoots.Value != rootsRaw || roots.Count < rootsWanted || roots.Exists(t => t == null))
            {
                rootsRaw = Plugin.CfgShiftRoots.Value;
                int before = roots.Count;
                roots.Clear();
                rootsWanted = 0;
                foreach (string s in rootsRaw.Split(','))
                {
                    if (s.Trim().Length == 0) continue;
                    rootsWanted++;
                    GameObject g = GameObject.Find(s.Trim());
                    if (g != null && g.transform.parent == null) roots.Add(g.transform);
                }
                if (roots.Count > 0 && roots.Count != before)
                {
                    string names = "";
                    foreach (Transform t in roots) names += t.name + " ";
                    Plugin.Log.LogInfo("Head collision: moves " + names.Trim());
                }
            }
        }

        private static void Compute()
        {
            correction = Vector3.zero;
            if (!Plugin.CfgEnabled.Value || !Plugin.CfgHeadCollision.Value) return;
            if (player == null || head == null || !head.gameObject.activeInHierarchy) return;
            if (head.localPosition.sqrMagnitude < 1e-6f) return; // headset not tracked

            Vector3 h = head.position;
            Transform pt = player.transform;
            Vector3 up = pt.up;
            Vector3 bottom, top;
            if (cc != null)
            {
                float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
                Vector3 c = pt.TransformPoint(cc.center);
                bottom = c - up * half;
                top = c + up * half;
            }
            else
            {
                bottom = pt.position + up * 0.3f;
                top = pt.position + up * 1.4f;
            }
            // the point on the body's axis closest to the head (inside the body = free space)
            Vector3 seg = top - bottom;
            float k = seg.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(h - bottom, seg) / seg.sqrMagnitude) : 0f;
            Vector3 s = bottom + seg * k;

            Vector3 d = h - s;
            float dist = d.magnitude;
            if (dist < 0.01f) return;
            Vector3 dir = d / dist;
            float radius = Mathf.Clamp(Plugin.CfgHeadRadius.Value, 0.02f, 0.4f);

            RaycastHit[] hits = Physics.SphereCastAll(s, radius, dir, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Collider bestCol = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.distance <= 0f) continue;   // 0 = already overlapping at the start
                Transform ht = hit.collider.transform;
                if (ht.IsChildOf(pt) || Plugin.IsModObject(ht)) continue;
                Rigidbody rb = hit.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic && rb.mass < 5f) continue;  // small loose props don't push the head
                if (hit.distance < best) { best = hit.distance; bestCol = hit.collider; }
            }
            if (bestCol == null) return;
            Vector3 target = s + dir * best;
            correction = target - h;
            if (Time.unscaledTime >= nextLog)
            {
                nextLog = Time.unscaledTime + 3f;
                Plugin.Log.LogInfo("Head collision: " + bestCol.name + ", pushed back " + correction.magnitude.ToString("F2") + " m");
            }
        }

        private static void OnPreCull(Camera cam)
        {
            if (!IsEye(cam)) return;
            try
            {
                if (frameComputed != Time.frameCount)
                {
                    Restore();
                    frameComputed = Time.frameCount;
                    rendersThisFrame = 0;
                    Compute();
                    if (correction != Vector3.zero)
                    {
                        foreach (Transform r in roots) if (r != null) r.position += correction;
                        shifted = true;
                    }
                }
                if (!shifted) return;

                // If the VR mod drives this eye with an explicit view matrix, moving transforms does nothing - move the matrix too.
                // (the getter returns the explicit matrix if one was set, otherwise one computed from the current - already moved - transform)
                Matrix4x4 fromTransform = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(cam.transform.position, cam.transform.rotation, Vector3.one).inverse;
                Matrix4x4 w = cam.worldToCameraMatrix;
                bool explicitView = !Approx(w, fromTransform)
                    && !Approx(w, Matrix4x4.Scale(new Vector3(1, 1, -1)) * cam.transform.worldToLocalMatrix);
                if (!loggedMatrixMode)
                {
                    loggedMatrixMode = true;
                    Plugin.Log.LogInfo("Head collision: eye view matrix is " + (explicitView ? "set by the VR mod (adjusting it)" : "from the transform"));
                }
                if (explicitView)
                {
                    savedView[cam] = w;
                    cam.worldToCameraMatrix = w * Matrix4x4.Translate(-correction);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Head collision: " + e.Message);
            }
        }

        private static void OnPostRender(Camera cam)
        {
            if (!IsEye(cam)) return;
            Matrix4x4 w;
            if (savedView.TryGetValue(cam, out w))
            {
                cam.worldToCameraMatrix = w;
                savedView.Remove(cam);
            }
            rendersThisFrame++;
            if (rendersThisFrame >= 2) Restore();
        }

        private static void Restore()
        {
            if (!shifted) return;
            foreach (Transform r in roots) if (r != null) r.position -= correction;
            shifted = false;
        }

        private static bool Approx(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i = 0; i < 16; i++) if (Mathf.Abs(a[i] - b[i]) > 1e-2f) return false;
            return true;
        }
    }
}

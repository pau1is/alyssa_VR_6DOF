using System;
using System.Reflection;
using UnityEngine;

namespace AlyssaVRHands
{
    // Room-scale walking: the character's body follows the headset.
    //
    // ALYSSA VR puts VR_Root on the game camera every frame and lets the headset move freely inside it. When you
    // walk in your room (or snap turn while not standing on the spot where you re-centred, which swings that offset
    // around the camera), your eyes drift away from the character's real body. The body then bumps into walls you
    // don't see in front of you - it looks like you are stuck - until you turn around.
    //
    // Fix: every frame the body is moved (CharacterController.Move, so walls still stop it) under the headset, and
    // VR_Holder is moved back by exactly the distance the body went, so the view itself doesn't jump.
    internal static class RoomScale
    {
        private static Transform root, holder, head;
        private static float nextFind;
        private static PropertyInfo headLookActive, menuOpen;
        private static bool reflected;
        private static float nextLog;
        private static float blockedSince = -1f;

        public static void LateUpdate()
        {
            if (!Plugin.CfgRoomScale.Value) return;
            if (!Find()) return;
            CharacterController cc = HeadCollision.Controller;
            if (cc == null || !cc.enabled || !cc.gameObject.activeInHierarchy) return;
            // the game itself moves the player while the character is switched off (ladders, scripted moves)
            if (HeadCollision.Player == null || !HeadCollision.Player.enabled) return;
            if (Time.timeScale <= 0f || !InPlay()) return;
            if (head.localPosition.sqrMagnitude < 1e-6f) return; // headset not tracked

            // where the headset is relative to the point ALYSSA VR anchors the rig at (the game camera)
            Vector3 local = root.InverseTransformPoint(head.position);
            local.y = 0f;
            Vector3 offset = root.TransformVector(local);
            offset.y = 0f;

            float dead = Mathf.Max(0f, Plugin.CfgRoomScaleDeadzone.Value);
            float dist = offset.magnitude;
            if (dist <= dead + 0.001f) { blockedSince = -1f; return; }
            Vector3 want = offset * ((dist - dead) / dist);
            float maxStep = Mathf.Max(0.5f, Plugin.CfgRoomScaleMaxSpeed.Value) * Mathf.Max(Time.unscaledDeltaTime, 1f / 90f);
            if (want.magnitude > maxStep) want = want.normalized * maxStep;

            Transform body = cc.transform;
            Vector3 before = body.position;
            cc.Move(want);
            Vector3 moved = body.position - before;
            moved.y = 0f;   // only the sideways part is taken out of the view (gravity / steps stay the game's)
            if (moved.sqrMagnitude > 1e-10f)
                holder.localPosition -= root.InverseTransformVector(moved);

            // log when the body can't get under the head for a while (a wall between them)
            if (dist - dead > 0.25f && moved.magnitude < want.magnitude * 0.5f)
            {
                if (blockedSince < 0f) blockedSince = Time.unscaledTime;
                if (Time.unscaledTime - blockedSince > 1f && Time.unscaledTime >= nextLog)
                {
                    nextLog = Time.unscaledTime + 5f;
                    Plugin.Log.LogInfo("Room scale: body blocked, head is " + dist.ToString("F2") + " m from it (step back or re-centre)");
                }
            }
            else blockedSince = -1f;
        }

        private static bool Find()
        {
            if (root != null && holder != null && head != null) return true;
            if (Time.unscaledTime < nextFind) return false;
            nextFind = Time.unscaledTime + 1f;
            GameObject r = GameObject.Find("VR_Root");
            if (r == null) return false;
            root = r.transform;
            holder = root.Find("VR_Holder");
            head = holder != null ? holder.Find("VR_Head") : null;
            if (holder != null && head != null) Plugin.Log.LogInfo("Room scale: the body follows the headset (" + Plugin.Path(holder) + ")");
            return holder != null && head != null;
        }

        // ALYSSA VR's own "the game camera follows the head now" state (play, tracked, no menu / cut-scene).
        private static bool InPlay()
        {
            if (!reflected)
            {
                reflected = true;
                try
                {
                    Type g = Type.GetType("AlyssaVR.Game, AlyssaVR");
                    if (g != null)
                    {
                        const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                        headLookActive = g.GetProperty("HeadLookActive", f);
                        menuOpen = g.GetProperty("MenuOpen", f);
                    }
                    if (headLookActive == null) Plugin.Log.LogWarning("Room scale: ALYSSA VR play state not found - using the cursor lock instead");
                }
                catch (Exception e) { Plugin.Log.LogWarning("Room scale: " + e.Message); }
            }
            try
            {
                if (headLookActive != null)
                {
                    if (!(bool)headLookActive.GetValue(null, null)) return false;
                    if (menuOpen != null && (bool)menuOpen.GetValue(null, null)) return false;
                    return true;
                }
            }
            catch (Exception) { headLookActive = null; }
            return Cursor.lockState == CursorLockMode.Locked;
        }
    }
}

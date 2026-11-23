using System;
using System.Collections.Generic;
using System.Reflection;
using GameCreator.Runtime.Characters;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AlyssaVRHands
{
    // Saved pose of a camera transform that was temporarily moved onto the controller.
    public class SwapState
    {
        public Transform T;
        public Vector3 Pos;
        public Quaternion Rot;
    }

    internal static class Patches
    {
        private static readonly Dictionary<Type, FieldInfo> maxDistField = new Dictionary<Type, FieldInfo>();
        private static FieldInfo modeInnerField;

        // last seen targets (for the log)
        internal static string GcTarget = "";
        internal static string IcTarget = "";
        internal static string PickTarget = "";

        public static string DescribeTargets()
        {
            string s = "";
            if (GcTarget.Length > 0) s += "GC2 " + GcTarget + "  ";
            if (IcTarget.Length > 0) s += "Interact " + IcTarget + "  ";
            if (PickTarget.Length > 0) s += "Pickup " + PickTarget;
            return s.Trim();
        }

        public static void Apply(Harmony h)
        {
            Patch(h, typeof(InteractionController), "CheckForInteractable", "IC_Prefix", null, "IC_Finalizer");
            Patch(h, typeof(InteractiveObject), "Update", "IO_Prefix", null, "Swap_Finalizer");
            Patch(h, typeof(InteractionMode), "CalculatePriority", null, "GC_Postfix", null);
            foreach (Type t in typeof(TInteractionMode).Assembly.GetTypes())
                if (typeof(TInteractionMode).IsAssignableFrom(t) && !t.IsAbstract && AccessTools.DeclaredMethod(t, "CalculatePriority") != null)
                    Patch(h, t, "CalculatePriority", null, "GC_Postfix", null);
            Patch(h, typeof(ObjectPickup), "Update", "OP_Update_Prefix", null, null);
            Patch(h, typeof(ObjectPickup), "FixedUpdate", "OP_FixedUpdate_Prefix", null, null);
            Patch(h, typeof(ObjectPickup), "TryPickupObject", "OP_TryPickup_Prefix", null, null);
            Patch(h, typeof(ObjectPickup), "CheckForInteractiveObject", "OP_CheckInteractive_Prefix", null, null);
            Patch(h, typeof(ObjectPickup), "CheckIfObjectIsVisible", "OP_Visible_Prefix", null, null);
            Patch(h, typeof(ObjectPickup), "ThrowObject", "MainCam_Prefix", null, "Swap_Finalizer");
            Patch(h, typeof(ObjectPickup), "DropObject", null, "OP_Drop_Postfix", null);
        }

        private static void Patch(Harmony h, Type t, string method, string prefix, string postfix, string finalizer)
        {
            try
            {
                MethodInfo orig = AccessTools.Method(t, method);
                if (orig == null) { Plugin.Log.LogWarning("Not found: " + t.Name + "." + method); return; }
                h.Patch(orig,
                    prefix == null ? null : new HarmonyMethod(typeof(Patches), prefix),
                    postfix == null ? null : new HarmonyMethod(typeof(Patches), postfix),
                    null,
                    finalizer == null ? null : new HarmonyMethod(typeof(Patches), finalizer),
                    null);
                Plugin.Log.LogInfo("Hooked " + t.Name + "." + method);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not hook " + t.Name + "." + method + ": " + e);
            }
        }

        // ----- camera pose swap -----

        private static SwapState SwapToHand(Transform t)
        {
            if (t == null || !HandRig.Valid) return null;
            SwapState s = new SwapState();
            s.T = t;
            s.Pos = t.position;
            s.Rot = t.rotation;
            t.SetPositionAndRotation(HandRig.Position, HandRig.Rotation);
            return s;
        }

        private static void Restore(SwapState s)
        {
            if (s != null && s.T != null) s.T.SetPositionAndRotation(s.Pos, s.Rot);
        }

        public static Exception Swap_Finalizer(Exception __exception, SwapState __state)
        {
            Restore(__state);
            return __exception;
        }

        public static void MainCam_Prefix(out SwapState __state)
        {
            __state = null;
            if (!Plugin.CfgPickup.Value) return;
            Camera c = Camera.main;
            if (c != null) __state = SwapToHand(c.transform);
        }

        // ----- InteractionController (the game's own interaction ray) -----

        public static void IC_Prefix(Camera ___cam, out SwapState __state)
        {
            __state = null;
            if (Plugin.CfgInteract.Value && ___cam != null) __state = SwapToHand(___cam.transform);
        }

        public static Exception IC_Finalizer(Exception __exception, SwapState __state, InteractableObject ___currentTarget)
        {
            Restore(__state);
            IcTarget = ___currentTarget != null ? ___currentTarget.name : "";
            return __exception;
        }

        public static void IO_Prefix(Camera ___mainCamera, out SwapState __state)
        {
            __state = null;
            if (Plugin.CfgInteract.Value && ___mainCamera != null) __state = SwapToHand(___mainCamera.transform);
        }

        // ----- Game Creator 2 interaction: priority = distance of the thing from the controller's line -----

        public static void GC_Postfix(object __instance, Character character, IInteractive interactive, ref float __result)
        {
            try
            {
                if (!Plugin.CfgInteract.Value || character == null || interactive == null) return;
                if (!character.IsPlayer || !HandRig.Valid) return;

                float maxDist = Plugin.CfgInteractRadius.Value;
                if (maxDist <= 0f) maxDist = GameMaxDistance(__instance);

                Vector3 dir = HandRig.Forward;
                Vector3 d = interactive.Position - HandRig.Position;
                if (Vector3.Dot(dir, d) < 0f) { __result = float.MaxValue; return; }
                float dist = Vector3.Cross(dir, d).magnitude;
                __result = dist < maxDist ? dist : float.MaxValue;
                if (__result < 9999f)
                {
                    Component c = interactive as Component;
                    GcTarget = c != null ? c.name : interactive.ToString();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("GC2 priority: " + e.Message);
            }
        }

        private static float GameMaxDistance(object mode)
        {
            try
            {
                if (modeInnerField == null) modeInnerField = AccessTools.Field(typeof(InteractionMode), "m_InteractionMode");
                object inner = mode is InteractionMode ? (modeInnerField != null ? modeInnerField.GetValue(mode) : null) : mode;
                if (inner != null)
                {
                    Type t = inner.GetType();
                    FieldInfo f;
                    if (!maxDistField.TryGetValue(t, out f))
                    {
                        f = AccessTools.Field(t, "m_MaxDistance");
                        maxDistField[t] = f;
                        Plugin.Log.LogInfo("GC2 interaction mode: " + t.Name
                            + (f != null ? " (max distance " + f.GetValue(inner) + ")" : ""));
                    }
                    if (f != null && f.FieldType == typeof(float))
                    {
                        float v = (float)f.GetValue(inner);
                        if (v > 0f && v < 100f) return Mathf.Max(v, 0.3f);
                    }
                }
            }
            catch (Exception) { }
            return 0.6f;
        }

        // ----- ObjectPickup: pick up / hold / throw with the controller -----

        private static readonly Dictionary<int, Vector3> holdLocalPos = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, Quaternion> holdLocalRot = new Dictionary<int, Quaternion>();
        private static Mouse vrMouse;
        private static float nextMouseFind;
        private static MethodInfo miDrop, miThrow;

        private static Mouse VrMouse()
        {
            if (vrMouse != null && vrMouse.added) return vrMouse;
            vrMouse = null;
            if (Time.unscaledTime < nextMouseFind) return null;
            nextMouseFind = Time.unscaledTime + 2f;
            foreach (InputDevice d in InputSystem.devices)
            {
                Mouse m = d as Mouse;
                if (m == null) continue;
                string n = (d.name ?? "") + " " + (d.displayName ?? "") + " " + (d.description.product ?? "");
                if (n.IndexOf("AlyssaVR", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    vrMouse = m;
                    Plugin.Log.LogInfo("VR mouse found: " + d.name);
                    break;
                }
            }
            return vrMouse;
        }

        private static void FollowHand(ObjectPickup op, Transform hold)
        {
            if (hold == null) return;
            int id = hold.GetInstanceID();
            if (!holdLocalPos.ContainsKey(id))
            {
                holdLocalPos[id] = hold.localPosition;
                holdLocalRot[id] = hold.localRotation;
            }
            if (Plugin.CfgPickup.Value && HandRig.Valid)
            {
                hold.SetPositionAndRotation(HandRig.Position + HandRig.Forward * Plugin.CfgHoldDistance.Value, HandRig.Rotation);
            }
            else
            {
                hold.localPosition = holdLocalPos[id];
                hold.localRotation = holdLocalRot[id];
            }
        }

        public static void OP_Update_Prefix(ObjectPickup __instance, Transform ___holdPosition,
            bool ___isHoldingObject, bool ___canPickup, Rigidbody ___pickedObject)
        {
            try
            {
                FollowHand(__instance, ___holdPosition);
                if (!Plugin.CfgPickup.Value || !HandRig.Valid) return;
                Mouse m = VrMouse();
                if (m == null) return;
                // VR right trigger (= left mouse of the mod's virtual mouse): pick up / drop.
                if (m.leftButton.wasPressedThisFrame)
                {
                    if (!___isHoldingObject && ___canPickup)
                        AccessTools.Method(typeof(ObjectPickup), "TryPickupObject").Invoke(__instance, null);
                    else if (___isHoldingObject)
                    {
                        if (miDrop == null) miDrop = AccessTools.Method(typeof(ObjectPickup), "DropObject");
                        miDrop.Invoke(__instance, null);
                    }
                }
                // VR left trigger (= right mouse): throw.
                if (m.rightButton.wasPressedThisFrame && ___pickedObject != null)
                {
                    if (miThrow == null) miThrow = AccessTools.Method(typeof(ObjectPickup), "ThrowObject");
                    miThrow.Invoke(__instance, null);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Pickup update: " + e);
            }
        }

        public static void OP_FixedUpdate_Prefix(ObjectPickup __instance, Transform ___holdPosition)
        {
            FollowHand(__instance, ___holdPosition);
        }

        private static bool HandRaycast(float distance, int mask, out RaycastHit hit, bool wantPickupTag)
        {
            Ray r = HandRig.Ray;
            if (Physics.Raycast(r, out hit, distance, mask) && (!wantPickupTag || hit.collider.CompareTag("Pickup")))
                return true;
            float rad = Plugin.CfgPickupAssist.Value;
            if (wantPickupTag && rad > 0f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(r, rad, distance, mask);
                float best = float.MaxValue;
                bool ok = false;
                foreach (RaycastHit x in hits)
                {
                    if (x.collider == null || !x.collider.CompareTag("Pickup")) continue;
                    if (x.distance > 0f && x.distance < best) { best = x.distance; hit = x; ok = true; }
                }
                if (ok) return true;
            }
            return false;
        }

        public static bool OP_TryPickup_Prefix(ObjectPickup __instance, float ___pickupDistance, LayerMask ___layerMask,
            ref Rigidbody ___pickedObject, Transform ___holdPosition, ref bool ___isHoldingObject, GameObject ___interactiveObject)
        {
            if (!Plugin.CfgPickup.Value || !HandRig.Valid) return true;
            RaycastHit hit;
            if (HandRaycast(___pickupDistance, ___layerMask.value, out hit, true))
            {
                Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
                if (rb == null) rb = hit.collider.attachedRigidbody;
                ___pickedObject = rb;
                if (rb != null)
                {
                    FollowHand(__instance, ___holdPosition);
                    rb.useGravity = false;
                    rb.linearDamping = 10f;
                    rb.constraints = RigidbodyConstraints.FreezeRotation;
                    rb.transform.SetParent(___holdPosition);
                    ___isHoldingObject = true;
                    if (___interactiveObject != null) ___interactiveObject.SetActive(true);
                    Plugin.Log.LogInfo("Picked up " + rb.name);
                }
            }
            return false;
        }

        public static bool OP_CheckInteractive_Prefix(float ___pickupDistance, LayerMask ___layerMask,
            ref Rigidbody ___lookedAtObject, GameObject ___interactiveObject)
        {
            if (!Plugin.CfgPickup.Value || !HandRig.Valid) return true;
            RaycastHit hit;
            bool on = false;
            if (HandRaycast(___pickupDistance, ___layerMask.value, out hit, true))
            {
                ___lookedAtObject = hit.collider.GetComponent<Rigidbody>();
                on = true;
            }
            else ___lookedAtObject = null;
            PickTarget = ___lookedAtObject != null ? ___lookedAtObject.name : "";
            if (___interactiveObject != null && ___interactiveObject.activeSelf != on) ___interactiveObject.SetActive(on);
            return false;
        }

        // The game drops the object when the screen-centre ray stops hitting it; with the hand that is meaningless.
        public static bool OP_Visible_Prefix()
        {
            return !(Plugin.CfgPickup.Value && HandRig.Valid);
        }

        public static void OP_Drop_Postfix()
        {
            PickTarget = "";
        }
    }
}

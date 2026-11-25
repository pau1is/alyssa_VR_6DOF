using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AlyssaVRHands
{
    // Ladders / climbable fences (LadderClimbGC2) read W / S with the OLD input (UnityEngine.Input.GetKey).
    // ALYSSA VR presses keys on a virtual keyboard of the NEW Input System, which the old input never sees - so the
    // climb starts (A = interact) but the left stick can't move you up or down. The calls to Input.GetKey in these
    // methods are swapped for one that also asks the Input System keyboards (the virtual one included).
    internal static class LegacyKeys
    {
        private static readonly Dictionary<KeyCode, Key> map = new Dictionary<KeyCode, Key>();
        private static readonly HashSet<KeyCode> unmapped = new HashSet<KeyCode>();

        public static void Apply(Harmony h)
        {
            Type ladder = AccessTools.TypeByName("LadderClimbGC2");
            if (ladder == null) { Plugin.Log.LogWarning("Not found: LadderClimbGC2"); return; }
            try
            {
                MethodInfo m = AccessTools.Method(ladder, "HandleClimbing");
                h.Patch(m, transpiler: new HarmonyMethod(typeof(LegacyKeys), "Transpiler"));
                Plugin.Log.LogInfo("Hooked LadderClimbGC2.HandleClimbing (left stick climbs ladders)");
                MethodInfo s = AccessTools.Method(ladder, "StartClimbing");
                if (s != null) h.Patch(s, postfix: new HarmonyMethod(typeof(LegacyKeys), "StartClimbing_Postfix"));
            }
            catch (Exception e) { Plugin.Log.LogError("Could not hook LadderClimbGC2: " + e); }
        }

        private static readonly MethodInfo legacyGetKey = AccessTools.Method(typeof(Input), "GetKey", new[] { typeof(KeyCode) });
        private static readonly MethodInfo ourGetKey = AccessTools.Method(typeof(LegacyKeys), "GetKey");

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            int n = 0;
            foreach (CodeInstruction c in code)
            {
                if (c.Calls(legacyGetKey)) { c.operand = ourGetKey; n++; }
                yield return c;
            }
            if (n == 0) Plugin.Log.LogWarning("LadderClimbGC2.HandleClimbing: no Input.GetKey calls found to replace");
        }

        private static void StartClimbing_Postfix(object __instance)
        {
            try
            {
                Traverse t = Traverse.Create(__instance);
                Plugin.Log.LogInfo("Climbing " + ((Component)__instance).name + " (up " + t.Field("climbUpKey").GetValue()
                    + ", down " + t.Field("climbDownKey").GetValue() + " = left stick up / down)");
            }
            catch (Exception) { }
        }

        public static bool GetKey(KeyCode k)
        {
            if (Input.GetKey(k)) return true;
            Key key;
            if (!ToKey(k, out key)) return false;
            try
            {
                foreach (InputDevice d in InputSystem.devices)
                {
                    Keyboard kb = d as Keyboard;
                    if (kb == null || !kb.added) continue;
                    KeyControl kc = kb[key];
                    if (kc != null && kc.isPressed) return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        private static bool ToKey(KeyCode k, out Key key)
        {
            if (map.TryGetValue(k, out key)) return true;
            if (unmapped.Contains(k)) return false;
            string name = k.ToString();
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) name = "Digit" + (k - KeyCode.Alpha0);
            else if (k == KeyCode.Return) name = "Enter";
            else if (k == KeyCode.LeftControl) name = "LeftCtrl";
            else if (k == KeyCode.RightControl) name = "RightCtrl";
            try
            {
                key = (Key)Enum.Parse(typeof(Key), name, true);
                map[k] = key;
                return true;
            }
            catch (Exception)
            {
                unmapped.Add(k);
                Plugin.Log.LogWarning("Key " + k + " has no Input System equivalent");
                return false;
            }
        }
    }
}

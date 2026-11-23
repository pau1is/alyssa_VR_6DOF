// ALYSSA VR Hands - companion plugin for ALYSSA VR (BepInEx 5).
// Makes the game aim with the right VR controller instead of the head:
//  * Game Creator 2 interaction target = what the controller points at
//  * the game's InteractionController / InteractiveObject rays come from the controller
//  * ObjectPickup: pick up / hold / throw with the controller (held object follows the hand)
//  * optional: attach named game objects (e.g. the match) to the hand
//  * diagnostics: logs lights / objects that appear near the camera so they can be attached
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AlyssaVRHands
{
    [BepInPlugin("alyssa.vr.hands", "ALYSSA VR Hands", "0.7.4")]
    public class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> CfgEnabled;
        internal static ConfigEntry<string> CfgHand;
        internal static ConfigEntry<bool> CfgInteract;
        internal static ConfigEntry<float> CfgInteractRadius;
        internal static ConfigEntry<bool> CfgPickup;
        internal static ConfigEntry<float> CfgHoldDistance;
        internal static ConfigEntry<float> CfgPickupAssist;
        internal static ConfigEntry<string> CfgAttach;
        internal static ConfigEntry<string> CfgAttachHand;
        internal static ConfigEntry<float> CfgItemScale;
        internal static ConfigEntry<string> CfgAnchors;
        internal static ConfigEntry<bool> CfgFlameFix;
        internal static ConfigEntry<string> CfgFlameFixFor;
        internal static ConfigEntry<string> CfgFlameOffset;
        internal static ConfigEntry<bool> CfgDumpHeld;
        internal static ConfigEntry<string> CfgHideUI;
        internal static ConfigEntry<bool> CfgDumpHud;
        internal static ConfigEntry<bool> CfgHeadCollision;
        internal static ConfigEntry<float> CfgHeadRadius;
        internal static ConfigEntry<string> CfgShiftRoots;
        internal static ConfigEntry<bool> CfgNoVsync;
        internal static ConfigEntry<bool> CfgNoHeadBob;
        internal static ConfigEntry<bool> CfgFrameStats;
        internal static ConfigEntry<bool> CfgFlatNoVolumetric;
        internal static ConfigEntry<string> CfgAttachOffset;
        internal static ConfigEntry<string> CfgAttachRotation;
        internal static ConfigEntry<bool> CfgDiagnostics;

        private float nextDiag;
        private string lightSignature = "";
        private float nextTargetLog;
        private string lastTarget = "";

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            CfgEnabled = Config.Bind("General", "Enabled", true,
                "Aim with the VR controller instead of the head.");
            CfgHand = Config.Bind("General", "AimHand", "Right",
                "Which controller aims: Right or Left.");
            CfgInteract = Config.Bind("Interact", "Enabled", true,
                "Doors / notes / items: the interaction target is what the controller points at.");
            CfgInteractRadius = Config.Bind("Interact", "MaxDistanceFromRay", 0f,
                "How far (metres) a usable thing may be from the controller's line. 0 = use the game's own value.");
            CfgPickup = Config.Bind("Pickup", "Enabled", true,
                "Physics objects: point + right trigger = pick up / drop, left trigger = throw. Held object follows the hand.");
            CfgHoldDistance = Config.Bind("Pickup", "HoldDistance", 0.35f,
                "Distance (metres) in front of the controller where a picked object is held.");
            CfgPickupAssist = Config.Bind("Pickup", "AimAssistRadius", 0.06f,
                "Thickness (metres) of the pickup ray, so small objects are easier to hit.");
            CfgAttach = Config.Bind("Hand", "AttachObjects", "Hands/ItemHandle",
                "Comma separated names (or path endings) of game objects to hold in the hand. Hands/ItemHandle = the match / lighter. Empty = off.");
            CfgAttachHand = Config.Bind("Hand", "ItemHand", "Left",
                "Which controller holds the match / lighter: Left or Right.");
            CfgAttachOffset = Config.Bind("Hand", "AttachOffset", "0,0,0",
                "Position offset (x,y,z metres, controller space) of held objects.");
            CfgAttachRotation = Config.Bind("Hand", "AttachRotation", "71,278,271",
                "Rotation offset (x,y,z degrees) of held objects (71,278,271 = like in the flat game).");
            CfgDiagnostics = Config.Bind("Debug", "Diagnostics", false,
                "Log lights / objects that appear near the camera (to find what to attach to the hand).");

            CfgItemScale = Config.Bind("Hand", "ItemScale", 3f,
                "Size multiplier for the held match / lighter (1 = the game's own size, which looks tiny in VR).");
            CfgAnchors = Config.Bind("Hand", "AnchorChildren", "Match,Lighter",
                "Children of the held object that are placed exactly at the controller (first active one).");
            CfgFlameFix = Config.Bind("Flame", "PutFlameOnMatchHead", true,
                "Move the match flame onto the match's axis (the game places it slightly beside the head).");
            CfgFlameFixFor = Config.Bind("Flame", "FixFor", "Match",
                "Held items whose flame is corrected (comma separated).");
            CfgFlameOffset = Config.Bind("Flame", "FlameOffset", "0,0,0",
                "Extra flame offset (x,y,z metres, controller space: x = right, y = up, z = forward).");
            CfgDumpHeld = Config.Bind("Debug", "DumpHeldNow", false,
                "Set to true while the game runs: logs the held item's parts once, then switches back to false.");
            CfgHideUI = Config.Bind("UI", "Hide", "Canvas_Matches/Text,*crosshair*,*reticle*",
                "HUD pieces to hide: path endings (Canvas_Matches/Text = the [F] match hint) or name patterns with * (case-insensitive). Empty = show all.");
            CfgDumpHud = Config.Bind("Debug", "LogHudLayout", true,
                "Write the game's HUD layout (Canvas_HUD) to the log once per level, to find names for [UI] Hide.");
            CfgHeadCollision = Config.Bind("Head", "StopAtWalls", true,
                "Keep the head out of walls / obstacles (the view is pushed back when you lean into geometry).");
            CfgHeadRadius = Config.Bind("Head", "HeadRadius", 0.12f,
                "Head size (metres) for the wall check. Bigger = stops further from walls.");
            CfgShiftRoots = Config.Bind("Head", "MovedObjects", "VR_Root,VR_UIPanel,VR_PointAim",
                "ALYSSA VR objects moved together with the view when the head is pushed back.");
            CfgNoVsync = Config.Bind("Smoothness", "DisableVSync", true,
                "Turn the desktop VSync off in VR (the monitor's refresh rate fights the headset's and makes the view judder).");
            CfgNoHeadBob = Config.Bind("Smoothness", "DisableHeadBobbing", true,
                "Turn off head bobbing through the game's HeadBobbingManager, where a level has one. (The Game Creator 'head_bobbing' setting used in most levels is not affected.)");
            CfgFrameStats = Config.Bind("Smoothness", "LogFrameStats", true,
                "Write fps / slow frame statistics to the log every 15 s.");

            HeadCollision.Init();
            CfgFlatNoVolumetric = Config.Bind("Performance", "FlatCameraNoVolumetricLight", true,
                "Skip the volumetric light pass on the flat camera while it draws no 3D (the VR eyes don't show it anyway).");

            FlatCam.Init();
            Harmony h = new Harmony("alyssa.vr.hands");
            Patches.Apply(h);
            try { cfgTime = System.IO.File.GetLastWriteTimeUtc(Config.ConfigFilePath); } catch (Exception) { }
            Logger.LogInfo("ALYSSA VR Hands 0.7.4 loaded (aim hand " + CfgHand.Value + ", items in " + CfgAttachHand.Value + ")");
        }

        // Re-read the config file when it is edited while the game runs.
        private DateTime cfgTime;
        private float nextCfgCheck;
        private void CheckConfigFile()
        {
            if (Time.unscaledTime < nextCfgCheck) return;
            nextCfgCheck = Time.unscaledTime + 1f;
            try
            {
                DateTime t = System.IO.File.GetLastWriteTimeUtc(Config.ConfigFilePath);
                if (t != cfgTime)
                {
                    cfgTime = t;
                    Config.Reload();
                    Logger.LogInfo("Config reloaded: items in " + CfgAttachHand.Value + ", scale " + CfgItemScale.Value
                        + ", offset " + CfgAttachOffset.Value + ", rotation " + CfgAttachRotation.Value);
                }
            }
            catch (Exception e) { Logger.LogWarning("Config reload: " + e.Message); }
        }

        private void Update()
        {
            CheckConfigFile();
            if (CfgDumpHeld.Value)
            {
                CfgDumpHeld.Value = false;
                try { Attach.DumpAll(); } catch (Exception e) { Logger.LogError("Dump: " + e); }
            }
            try { HeadCollision.Update(); } catch (Exception e) { Logger.LogError("Head: " + e.Message); }
            try { Comfort.Update(); } catch (Exception e) { Logger.LogError("Comfort: " + e.Message); }
            if (!CfgEnabled.Value) return;
            try
            {
                HandRig.Refresh();
                if (CfgDiagnostics.Value && Time.unscaledTime >= nextDiag)
                {
                    nextDiag = Time.unscaledTime + 1f;
                    DiagnoseLights();
                }
                if (Time.unscaledTime >= nextTargetLog)
                {
                    nextTargetLog = Time.unscaledTime + 0.25f;
                    LogTarget();
                }
            }
            catch (Exception e)
            {
                Logger.LogError("Update: " + e);
                nextDiag = Time.unscaledTime + 10f;
            }
        }

        private void LateUpdate()
        {
            try { Attach.LateUpdate(); }
            catch (Exception e) { Logger.LogError("Attach: " + e); }
            try { HideUI.LateUpdate(); }
            catch (Exception e) { Logger.LogError("HideUI: " + e); }
        }

        // ---------- diagnostics ----------

        private void LogTarget()
        {
            string t = Patches.DescribeTargets();
            if (t != lastTarget)
            {
                lastTarget = t;
                Logger.LogInfo("Hand target: " + (t.Length == 0 ? "(none)" : t));
            }
        }

        private void DiagnoseLights()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 p = cam.transform.position;
            List<Light> near = new List<Light>();
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || !l.isActiveAndEnabled) continue;
                if (l.type == LightType.Directional) continue;
                if (IsModObject(l.transform)) continue;
                if ((l.transform.position - p).sqrMagnitude > 2.5f * 2.5f) continue;
                near.Add(l);
            }
            near.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            string sig = "";
            foreach (Light l in near) sig += l.GetInstanceID() + ";";
            if (sig == lightSignature) return;
            lightSignature = sig;
            if (near.Count == 0) { Logger.LogInfo("Diag: no lights near the camera now"); return; }
            foreach (Light l in near)
            {
                Logger.LogInfo("Diag: light near camera: " + Path(l.transform) + "  {" + l.type + " i=" + l.intensity.ToString("F2")
                    + " r=" + l.range.ToString("F1") + " dist=" + Vector3.Distance(l.transform.position, p).ToString("F2") + "}");
                Transform top = l.transform;
                for (int i = 0; i < 3 && top.parent != null; i++) top = top.parent;
                Logger.LogInfo("Diag: around it:\n" + Diag.Dump(top, 5));
            }
        }

        internal static bool IsModObject(Transform t)
        {
            Transform r = t.root;
            return r.name.StartsWith("VR_", StringComparison.Ordinal) || r.name.StartsWith("AlyssaVR", StringComparison.Ordinal);
        }

        internal static string Path(Transform t)
        {
            string s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }

    // ---------- the aiming controller ----------
    internal static class HandRig
    {
        public static readonly HandPose Aim = new HandPose(() => Plugin.CfgHand.Value, "Aiming with");
        public static readonly HandPose Item = new HandPose(() => Plugin.CfgAttachHand.Value, "Items held in");

        public static void Refresh() { Aim.Refresh(); Item.Refresh(); }
        public static bool Valid { get { return Aim.Valid; } }
        public static Vector3 Position { get { return Aim.Position; } }
        public static Vector3 Forward { get { return Aim.Forward; } }
        public static Quaternion Rotation { get { return Aim.Rotation; } }
        public static Ray Ray { get { return new Ray(Aim.Position, Aim.Forward); } }
    }

    // One VR controller (VR_Root/VR_Holder/VR_<side>Hand/Aim, created by ALYSSA VR).
    internal class HandPose
    {
        private readonly Func<string> sideCfg;
        private readonly string what;
        private string side;
        private Transform aim;
        private Transform hand;
        private float nextFind;

        public HandPose(Func<string> sideCfg, string what)
        {
            this.sideCfg = sideCfg;
            this.what = what;
        }

        public void Refresh()
        {
            string s = sideCfg().Trim().StartsWith("L", StringComparison.OrdinalIgnoreCase) ? "Left" : "Right";
            if (s != side) { side = s; aim = null; hand = null; nextFind = 0; }
            if (aim != null && hand != null) return;
            if (Time.unscaledTime < nextFind) return;
            nextFind = Time.unscaledTime + 1f;
            GameObject root = GameObject.Find("VR_Root");
            if (root == null) return;
            Transform h = root.transform.Find("VR_Holder/VR_" + side + "Hand");
            if (h == null) return;
            hand = h;
            aim = h.Find("Aim");
            if (aim == null) aim = h;
            Plugin.Log.LogInfo(what + " " + Plugin.Path(aim));
        }

        // True when the controller is found and tracked (the mod leaves it at 0,0,0 when not tracked).
        public bool Valid
        {
            get
            {
                if (!Plugin.CfgEnabled.Value) return false;
                Refresh();
                return aim != null && hand != null && hand.gameObject.activeInHierarchy
                    && hand.localPosition.sqrMagnitude > 1e-6f;
            }
        }

        public Vector3 Position { get { return aim.position; } }
        public Vector3 Forward { get { return aim.forward; } }
        public Quaternion Rotation { get { return aim.rotation; } }
        public Transform AimT { get { return aim; } }
    }
}

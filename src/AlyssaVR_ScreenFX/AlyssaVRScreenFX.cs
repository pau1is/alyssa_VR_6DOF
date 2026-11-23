// ALYSSA VR Screen FX - companion plugin for ALYSSA VR.
// Copies the game's camera image effects (CameraFilterPack_* film grain / TV noise / posterize, Lutify LUT)
// from the flat game camera onto the VR eye cameras and keeps them in sync every frame.
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace AlyssaVRScreenFX
{
    [BepInPlugin("alyssa.vr.screenfx", "ALYSSA VR Screen FX", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> cfgEnabled;
        private ConfigEntry<float> cfgNoiseStrength;
        private ConfigEntry<string> cfgEffects;
        private ConfigEntry<string> cfgEyes;

        private float nextScan;
        private Camera source;
        private readonly List<Camera> eyes = new List<Camera>();
        // eye camera -> (source effect -> copy on the eye)
        private readonly Dictionary<Camera, Dictionary<Behaviour, Behaviour>> copies =
            new Dictionary<Camera, Dictionary<Behaviour, Behaviour>>();
        private static readonly Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();
        private readonly HashSet<string> logged = new HashSet<string>();

        private void Awake()
        {
            cfgEnabled = Config.Bind("General", "Enabled", true,
                "Show the game's screen effects (film grain / TV noise, posterize, LUT) in VR.");
            cfgNoiseStrength = Config.Bind("General", "NoiseStrength", 1f,
                "Multiplier for the grain / noise strength in VR (1 = same as the game, 0.5 = half, 0 = off).");
            cfgEffects = Config.Bind("Advanced", "EffectTypes", "CameraFilterPack_,Lutify",
                "Comma separated type names (or name prefixes) of camera effects to copy to the VR eyes.");
            cfgEyes = Config.Bind("Advanced", "EyeCameras", "VR_LeftEye,VR_RightEye",
                "Names of the VR eye camera objects created by ALYSSA VR.");
            Logger.LogInfo("ALYSSA VR Screen FX 1.0.0 loaded");
        }

        private void LateUpdate()
        {
            try
            {
                if (!cfgEnabled.Value)
                {
                    DisableAll();
                    return;
                }
                if (Time.unscaledTime >= nextScan)
                {
                    nextScan = Time.unscaledTime + 1f;
                    Scan();
                }
                Sync();
            }
            catch (Exception e)
            {
                Logger.LogError("Screen FX error: " + e);
                nextScan = Time.unscaledTime + 5f;
            }
        }

        private bool IsEffect(Behaviour b)
        {
            if (b == null) return false;
            string n = b.GetType().Name;
            foreach (string p in cfgEffects.Value.Split(','))
            {
                string t = p.Trim();
                if (t.Length > 0 && n.StartsWith(t, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private bool IsEye(Camera c)
        {
            if (c == null) return false;
            foreach (string p in cfgEyes.Value.Split(','))
                if (c.name == p.Trim()) return true;
            return false;
        }

        private void Scan()
        {
            eyes.RemoveAll(c => c == null);
            foreach (Camera c in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (c == null || !c.gameObject.scene.IsValid()) continue;
                if (IsEye(c) && !eyes.Contains(c))
                {
                    eyes.Add(c);
                    Logger.LogInfo("VR eye camera found: " + c.name);
                }
            }

            // Source: the game's main camera; if it has no effects, any non-eye camera that has some.
            Camera best = Camera.main;
            if (best != null && (IsEye(best) || !HasEffects(best))) best = null;
            if (best == null)
            {
                foreach (Camera c in Camera.allCameras)
                    if (!IsEye(c) && HasEffects(c)) { best = c; break; }
            }
            if (best != null && best != source)
            {
                source = best;
                Logger.LogInfo("Effects source camera: " + source.name);
            }
        }

        private bool HasEffects(Camera c)
        {
            foreach (Behaviour b in c.GetComponents<Behaviour>())
                if (IsEffect(b)) return true;
            return false;
        }

        private void Sync()
        {
            Behaviour[] srcEffects = new Behaviour[0];
            if (source != null)
            {
                List<Behaviour> l = new List<Behaviour>();
                foreach (Behaviour b in source.GetComponents<Behaviour>())
                    if (IsEffect(b)) l.Add(b);
                srcEffects = l.ToArray();
            }

            foreach (Camera eye in eyes)
            {
                if (eye == null) continue;
                Dictionary<Behaviour, Behaviour> map;
                if (!copies.TryGetValue(eye, out map))
                {
                    map = new Dictionary<Behaviour, Behaviour>();
                    copies[eye] = map;
                }

                // Remove copies whose source is gone (scene change, camera replaced).
                List<Behaviour> dead = null;
                foreach (KeyValuePair<Behaviour, Behaviour> kv in map)
                {
                    if (kv.Key == null || kv.Key.GetComponent<Camera>() != source || kv.Value == null)
                    {
                        if (kv.Value != null) Destroy(kv.Value);
                        if (dead == null) dead = new List<Behaviour>();
                        dead.Add(kv.Key);
                    }
                }
                if (dead != null) foreach (Behaviour k in dead) map.Remove(k);

                foreach (Behaviour src in srcEffects)
                {
                    Behaviour dst;
                    if (!map.TryGetValue(src, out dst) || dst == null)
                    {
                        dst = (Behaviour)eye.gameObject.AddComponent(src.GetType());
                        map[src] = dst;
                        string key = src.GetType().Name;
                        if (logged.Add(key)) Logger.LogInfo("Copying " + key + " to the VR eyes");
                    }
                    CopyFields(src, dst);
                    dst.enabled = src.enabled && src.gameObject.activeInHierarchy;
                }
            }
        }

        private void CopyFields(Behaviour src, Behaviour dst)
        {
            Type t = src.GetType();
            FieldInfo[] fields;
            if (!fieldCache.TryGetValue(t, out fields))
            {
                List<FieldInfo> l = new List<FieldInfo>();
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    // Materials / render textures are per-instance caches; each copy makes its own.
                    if (typeof(Material).IsAssignableFrom(f.FieldType)) continue;
                    if (typeof(RenderTexture).IsAssignableFrom(f.FieldType)) continue;
                    l.Add(f);
                }
                fields = l.ToArray();
                fieldCache[t] = fields;
            }
            bool noise = t.Name.IndexOf("Noise", StringComparison.OrdinalIgnoreCase) >= 0
                      || t.Name.IndexOf("Grain", StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (FieldInfo f in fields)
            {
                object v = f.GetValue(src);
                if (noise && f.Name == "Fade" && v is float)
                    v = Mathf.Clamp01((float)v * Mathf.Max(0f, cfgNoiseStrength.Value));
                f.SetValue(dst, v);
            }
        }

        private void DisableAll()
        {
            foreach (Dictionary<Behaviour, Behaviour> map in copies.Values)
                foreach (Behaviour b in map.Values)
                    if (b != null) b.enabled = false;
        }
    }
}

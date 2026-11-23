using System;
using UnityEngine;

namespace AlyssaVRHands
{
    // Performance: while playing, ALYSSA VR already stops the game's flat camera from drawing 3D (culling mask 0),
    // but the camera's volumetric light pass still runs every frame for nothing (the VR eyes don't use it).
    // That pass is skipped whenever the flat camera draws no 3D. The culling mask itself is never touched
    // (ALYSSA VR manages it; changing it confused its play / menu detection).
    internal static class FlatCam
    {
        private static Behaviour savedVol;

        public static int LastMask = -1;
        public static int Skipped;

        public static void Init()
        {
            Camera.onPreCull += OnPreCull;
            Camera.onPostRender += OnPostRender;
        }

        private static bool IsFlat(Camera c)
        {
            return c != null && c.name == "GameCamera";
        }

        private static void OnPreCull(Camera c)
        {
            if (!IsFlat(c)) return;
            try
            {
                LastMask = c.cullingMask;
                if (!Plugin.CfgEnabled.Value || !Plugin.CfgFlatNoVolumetric.Value) return;
                if ((c.cullingMask & ~(1 << 5)) != 0) return;   // it draws 3D (menus) - leave everything as is
                Behaviour v = c.GetComponent("VolumetricLightRenderer") as Behaviour;
                if (v != null && v.enabled)
                {
                    savedVol = v;
                    v.enabled = false;
                    Skipped++;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("FlatCam: " + e.Message);
            }
        }

        private static void OnPostRender(Camera c)
        {
            if (!IsFlat(c)) return;
            Restore();
        }

        public static void Restore()
        {
            if (savedVol != null) savedVol.enabled = true;
            savedVol = null;
        }
    }
}

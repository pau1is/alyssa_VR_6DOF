using System;
using UnityEngine;

namespace AlyssaVRHands
{
    // Smoothness: the desktop VSync (monitor 74 Hz) fights the headset (72 Hz) and makes the view judder now and then.
    // Turns VSync off while in VR, optionally turns off the game's head bobbing, and logs frame time statistics.
    internal static class Comfort
    {
        private static float nextCheck;
        private static bool loggedVsync;
        private static bool bobDone;

        private static float statStart = -1f;
        private static int frames;
        private static int slow;
        private static float maxDt;

        public static void Update()
        {
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 1f;
                if (Plugin.CfgNoVsync.Value && QualitySettings.vSyncCount != 0)
                {
                    if (!loggedVsync) Plugin.Log.LogInfo("VSync was " + QualitySettings.vSyncCount + " - turning it off (the headset sets the pace)");
                    loggedVsync = true;
                    QualitySettings.vSyncCount = 0;
                }
                if (Plugin.CfgNoHeadBob.Value && !bobDone)
                {
                    HeadBobbingManager m = HeadBobbingManager.Instance;
                    if (m != null)
                    {
                        bobDone = true;
                        if (m.HeadBobbingEnabled)
                        {
                            m.SetHeadBobbing(false);
                            Plugin.Log.LogInfo("Game head bobbing turned off");
                        }
                    }
                }
            }

            // frame statistics every 15 s
            float dt = Time.unscaledDeltaTime;
            if (statStart < 0f) statStart = Time.unscaledTime;
            frames++;
            if (dt > maxDt) maxDt = dt;
            if (dt > 1.5f / 72f) slow++;
            float span = Time.unscaledTime - statStart;
            if (span >= 15f)
            {
                if (Plugin.CfgFrameStats.Value)
                    Plugin.Log.LogInfo("Frames: " + (frames / span).ToString("F1") + " fps, slowest " + (maxDt * 1000f).ToString("F0")
                        + " ms, " + slow + " slow frames (> 21 ms) in " + span.ToString("F0") + " s, vSync " + QualitySettings.vSyncCount
                        + ", flat camera mask 0x" + FlatCam.LastMask.ToString("X")
                        + ", volumetric pass skipped " + FlatCam.Skipped + "x, " + SystemInfo.graphicsDeviceType);
                FlatCam.Skipped = 0;
                statStart = Time.unscaledTime;
                frames = 0;
                slow = 0;
                maxDt = 0f;
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        private void PlayLightCrossHaptic()
        {
            if (Time.unscaledTime - lastLightHapticTime < LightHapticCooldownSeconds)
            {
                return;
            }

            lastLightHapticTime = Time.unscaledTime;
            NekoHaptics.Play(HapticStrength.Light);
        }

        private enum HapticStrength
        {
            Light,
            Medium,
            Strong
        }

        private static class NekoHaptics
        {
            public static void Play(HapticStrength strength)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                PlayAndroid(strength);
#else
                PlayFallback();
#endif
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            private static void PlayAndroid(HapticStrength strength)
            {
                try
                {
                    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                    {
                        if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                        {
                            return;
                        }

                        long[] timings = BuildAndroidFadeTimings(strength, out int[] amplitudes, out long totalMilliseconds);
                        using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                        {
                            vibrator.Call("cancel");
                            if (version.GetStatic<int>("SDK_INT") >= 26)
                            {
                                using (AndroidJavaClass effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                                using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1))
                                {
                                    vibrator.Call("vibrate", effect);
                                }
                            }
                            else
                            {
                                vibrator.Call("vibrate", totalMilliseconds);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    PlayFallback();
                }
            }

            private static long[] BuildAndroidFadeTimings(HapticStrength strength, out int[] amplitudes, out long totalMilliseconds)
            {
                int steps;
                long stepMilliseconds;
                int peakAmplitude;

                switch (strength)
                {
                    case HapticStrength.Strong:
                        steps = 12;
                        stepMilliseconds = 18L;
                        peakAmplitude = 255;
                        break;
                    case HapticStrength.Medium:
                        steps = 10;
                        stepMilliseconds = 16L;
                        peakAmplitude = 255;
                        break;
                    default:
                        steps = 6;
                        stepMilliseconds = 14L;
                        peakAmplitude = 160;
                        break;
                }

                long[] timings = new long[steps + 1];
                amplitudes = new int[steps + 1];
                timings[0] = 0L;
                amplitudes[0] = 0;
                totalMilliseconds = steps * stepMilliseconds;

                for (int i = 1; i <= steps; i++)
                {
                    float fade = 1f - ((float)(i - 1) / Math.Max(1, steps - 1));
                    timings[i] = stepMilliseconds;
                    amplitudes[i] = Mathf.Clamp(Mathf.RoundToInt(peakAmplitude * fade), 0, 255);
                }

                return timings;
            }
#endif

            private static void PlayFallback()
            {
#if !UNITY_EDITOR
                Handheld.Vibrate();
#endif
            }
        }
    }
}

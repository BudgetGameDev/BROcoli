using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>Shared Windows defaults and persistent user preferences.</summary>
    public static class StreamlineSettings
    {
        public enum ReflexMode
        {
            Off,
            On,
            OnWithBoost,
        }

        private const string DlssKey = "Rendering.Streamline.Dlss";
        public static bool DlssEnabled
        {
            get => PlayerPrefs.GetInt(DlssKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(DlssKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        private const string FramesKey = "Rendering.Streamline.GeneratedFrames";
        private const string ReflexKey = "Rendering.Streamline.Reflex";
        public static event Action Changed;

        // Three generated frames plus one rendered frame = 4x. Native code clamps
        // this request to slDLSSGGetState().numFramesToGenerateMax, including 2x GPUs.
        public static int GeneratedFrames
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(FramesKey, 3), 0, 3);
            set
            {
                PlayerPrefs.SetInt(FramesKey, Mathf.Clamp(value, 0, 3));
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static ReflexMode Reflex
        {
            get => (ReflexMode)Mathf.Clamp(PlayerPrefs.GetInt(ReflexKey, 1), 0, 2);
            set
            {
                PlayerPrefs.SetInt(ReflexKey, Mathf.Clamp((int)value, 0, 2));
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static ReflexMode EffectiveReflex =>
            GeneratedFrames > 0 && Reflex == ReflexMode.Off ? ReflexMode.On : Reflex;

        public static void ResetDefaults()
        {
            PlayerPrefs.DeleteKey(DlssKey);
            PlayerPrefs.DeleteKey(FramesKey);
            PlayerPrefs.DeleteKey(ReflexKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static GlobalDynamicResolutionSettings ConfigureSuperResolution(
            GlobalDynamicResolutionSettings settings
        )
        {
            settings.enabled = true;
            settings.dynResType = DynamicResolutionType.Hardware;
            settings.minPercentage = 50;
            settings.maxPercentage = 100;
            settings.forceResolution = false;
            var priority = new List<string>(settings.advancedUpscalerNames ?? new List<string>());
            priority.RemoveAll(name =>
                name == "DLSS"
                || name == "Deep Learning Super Sampling 4"
                || name == StreamlineUpscaler.Name
            );
            priority.Insert(0, StreamlineUpscaler.Name);
            settings.advancedUpscalerNames = priority;
            return settings;
        }
    }
}

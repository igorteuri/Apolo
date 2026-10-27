/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The settings of the leveler kept between runs (Preferences), and the three ready-made strengths.
 The processing itself is in Ares.Playing (LevelCompressor); the values live in AudioLeveler.
*/
using System;
using Ares.Playing;

namespace Ares.Player
{
    internal static class LevelerSettings
    {
        public struct Preset
        {
            public string Pt, En;
            public float TargetDb, Strength, BoostDb, CutDb;
        }

        // the first is the program default
        public static readonly Preset[] Presets =
        {
            new Preset { Pt = "Médio (padrão)", En = "Medium (default)", TargetDb = -20f, Strength = 0.8f, BoostDb = 9f, CutDb = 12f },
            new Preset { Pt = "Suave", En = "Gentle", TargetDb = -20f, Strength = 0.5f, BoostDb = 6f, CutDb = 8f },
            new Preset { Pt = "Forte (volume bem parecido)", En = "Strong (very even volume)", TargetDb = -18f, Strength = 1f, BoostDb = 15f, CutDb = 18f }
        };

        public static void Load()
        {
            AudioLeveler.Enabled = Ares.CommonGUI.Preferences.GetBool("LevelerOn", true);
            AudioLeveler.TargetDb = ReadFloat("LevelerTarget", AudioLeveler.DefaultTargetDb);
            AudioLeveler.Strength = ReadFloat("LevelerStrength", AudioLeveler.DefaultStrength);
            AudioLeveler.MaxBoostDb = ReadFloat("LevelerBoost", AudioLeveler.DefaultMaxBoostDb);
            AudioLeveler.MaxCutDb = ReadFloat("LevelerCut", AudioLeveler.DefaultMaxCutDb);
        }

        public static void Save()
        {
            Ares.CommonGUI.Preferences.SetBool("LevelerOn", AudioLeveler.Enabled);
            Write("LevelerTarget", AudioLeveler.TargetDb);
            Write("LevelerStrength", AudioLeveler.Strength);
            Write("LevelerBoost", AudioLeveler.MaxBoostDb);
            Write("LevelerCut", AudioLeveler.MaxCutDb);
        }

        public static void Apply(Preset p)
        {
            AudioLeveler.TargetDb = p.TargetDb;
            AudioLeveler.Strength = p.Strength;
            AudioLeveler.MaxBoostDb = p.BoostDb;
            AudioLeveler.MaxCutDb = p.CutDb;
        }

        /// <summary>Index of the preset equal to the current values, or -1 (custom).</summary>
        public static int CurrentPreset()
        {
            for (int i = 0; i < Presets.Length; i++)
            {
                Preset p = Presets[i];
                if (Math.Abs(p.TargetDb - AudioLeveler.TargetDb) < 0.05f && Math.Abs(p.Strength - AudioLeveler.Strength) < 0.005f
                    && Math.Abs(p.BoostDb - AudioLeveler.MaxBoostDb) < 0.05f && Math.Abs(p.CutDb - AudioLeveler.MaxCutDb) < 0.05f)
                    return i;
            }
            return -1;
        }

        private static float ReadFloat(string key, float fallback)
        {
            float value;
            return float.TryParse(Ares.CommonGUI.Preferences.Get(key, ""), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static void Write(string key, float value)
        {
            Ares.CommonGUI.Preferences.Set(key, value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}

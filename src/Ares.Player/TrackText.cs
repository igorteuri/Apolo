/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Small text helpers of the track list, kept free of any window code so that they can be tested automatically.
*/
using System;

namespace Ares.Player
{
    internal static class TrackText
    {
        /// <summary>Seconds as m:ss (or h:mm:ss); empty when unknown.</summary>
        public static string FormatDuration(double seconds)
        {
            if (seconds <= 0)
                return "";
            int t = (int)Math.Round(seconds);
            if (t >= 3600)
                return (t / 3600) + ":" + ((t / 60) % 60).ToString("00") + ":" + (t % 60).ToString("00");
            return (t / 60) + ":" + (t % 60).ToString("00");
        }
    }
}

/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 From the position of a volume slider (0..100) to the gain of the sound. The ear hears loudness on a logarithmic
 scale, so a straight line makes the low end of the slider far too coarse: with background music the useful range
 sits in the first few percent. The square gives the low half of the slider much more room (50 % = -12 dB,
 20 % = -28 dB, 10 % = -40 dB) and leaves 0 % silent and 100 % at full level.
*/
using System;

namespace Ares.Playing.Audio
{
    public static class VolumeCurve
    {
        public static float ToGain(float percent)
        {
            float p = Math.Max(0f, Math.Min(100f, percent)) / 100f;
            return p * p;
        }
    }
}

/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The setting for repeated sound effects: when a sound effect repeats (a loop, "repeat forever", the repeat button of a
 playing sound), its end is blended into its beginning (a crossfade with itself), so the repetition has no sudden drop.
*/
namespace Ares.Playing
{
    public static class SoundRepeat
    {
        /// <summary>true = repeated sound effects crossfade with themselves (the choice is kept by the Player).</summary>
        public static volatile bool CrossfadeEnabled = true;

        /// <summary>Length of the crossfade in milliseconds (a sound shorter than three times this uses a third of its length).</summary>
        public static int CrossfadeMs = 2000;
    }
}

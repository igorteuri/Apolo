/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Music which does not play in the engine of Apolo but is still "the music" for the user (a YouTube playlist playing in a web
 tab): the controls of the program - pause, previous, next, stop, seek bar, volume, the title - act on it through this
 interface (see PlayingControl). The program sets PlayingControl.External; null = no such music.
*/
using System;
using Ares.Data;

namespace Ares.Players
{
    public interface IExternalMusic
    {
        /// <summary>An outside track is the current music (also while it is paused).</summary>
        bool Active { get; }
        /// <summary>The button which started it (shown as playing).</summary>
        IModeElement ModeElement { get; }
        /// <summary>The element of the list it belongs to (the "current playlist").</summary>
        int ListId { get; }
        /// <summary>A stand-in element for the track which plays now (its title is the title of the track), or -1.</summary>
        int TrackId { get; }
        bool IsPaused { get; }
        void TogglePause();
        void Next();
        void Previous();
        void Stop();
        void Seek(double positionSeconds);
        bool GetPosition(out double positionSeconds, out double lengthSeconds);
        /// <summary>The volume of the music, 0 to 100 (the music slider and the master slider together).</summary>
        void SetVolume(int percent);
    }
}

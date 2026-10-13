/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 One playing sound of the audio engine: decoder -> stereo -> (time stretch) -> resampler -> effects -> mixer.
*/
using System;

namespace Ares.Playing.Audio
{
    /// <summary>A value (volume, balance) which can change linearly over some time.</summary>
    internal sealed class Ramp
    {
        private float m_Current;
        private float m_Target;
        private long m_Left;
        private float m_Step;
        private bool m_Finished;

        public Ramp(float value)
        {
            m_Current = value;
            m_Target = value;
        }

        public float Current { get { return m_Current; } }
        public float Target { get { return m_Target; } }
        public bool Sliding { get { return m_Left > 0; } }

        public void Set(float value)
        {
            m_Current = value;
            m_Target = value;
            m_Left = 0;
            m_Finished = false;
        }

        public void Slide(float target, double frames)
        {
            if (frames < 2)
            {
                Set(target);
                return;
            }
            m_Target = target;
            m_Left = (long)frames;
            m_Step = (target - m_Current) / m_Left;
            m_Finished = false;
        }

        /// <summary>Moves on by some frames; returns the value afterwards.</summary>
        public float Advance(int frames)
        {
            if (m_Left <= 0)
                return m_Current;
            if (frames >= m_Left)
            {
                m_Current = m_Target;
                m_Left = 0;
                m_Finished = true;
            }
            else
            {
                m_Current += m_Step * frames;
                m_Left -= frames;
            }
            return m_Current;
        }

        /// <summary>True once after a slide has reached its target.</summary>
        public bool TakeFinished()
        {
            bool result = m_Finished;
            m_Finished = false;
            return result;
        }

        /// <summary>The value after <paramref name="frames"/> more frames, without moving.</summary>
        public float Peek(int frames)
        {
            if (m_Left <= 0)
                return m_Current;
            if (frames >= m_Left)
                return m_Target;
            return m_Current + m_Step * frames;
        }
    }

    internal enum ChannelState
    {
        Ready,
        Playing,
        Paused,
        Stopped
    }

    /// <summary>To which speakers a channel is sent: pairs (left, right) or single speakers (mono).</summary>
    internal sealed class SpeakerRoute
    {
        public int[] Targets;
        public bool Mono;

        public static readonly SpeakerRoute Fronts = new SpeakerRoute { Targets = new[] { 0, 1 }, Mono = false };
    }

    internal sealed class AudioChannel
    {
        private readonly AudioEngine m_Engine;
        private readonly object m_Lock = new object();
        private IAudioSource m_Source;
        private SourceStream m_Stereo;
        private StretchStream m_Stretch;
        private ResampleStream m_Resample;
        private IStereoStream m_Head;
        private SimpleReverb m_Reverb;
        private readonly int m_SrcRate;
        private readonly double m_Length;
        private double m_Speed = 1.0;      // seconds of the file per second of playing
        private double m_BasePos;          // file position (seconds) at m_Played == 0
        private double m_Played;           // seconds of the file which were played since m_BasePos
        private ChannelState m_State = ChannelState.Ready;
        private float m_Gain = 1f;
        private float[] m_Scratch = new float[0];
        private SpeakerRoute m_Route = SpeakerRoute.Fronts;
        private bool m_Fired;
        private bool m_StopAtSilence;
        private bool m_Loop;
        private double m_LoopStart;
        private double m_CueOut = -1;
        private double m_FadeAt = -1;

        public readonly Ramp Volume = new Ramp(1f);
        public readonly Ramp Pan = new Ramp(0f);

        public int Handle { get; private set; }

        /// <summary>Called (on the event thread) when the sound ended and is not repeated, or has been faded out and stopped.</summary>
        public Action Ended { get; set; }
        /// <summary>Called when a repeated sound jumps back to its start.</summary>
        public Action Looped { get; set; }
        /// <summary>Called when the position for the fade out has been reached.</summary>
        public Action FadeStarted { get; set; }

        public AudioChannel(AudioEngine engine, IAudioSource source, bool mono)
        {
            m_Engine = engine;
            m_Source = source;
            Handle = engine.NextHandle();
            m_SrcRate = Math.Max(1000, source.SampleRate);
            m_Length = source.Length;
            m_Stereo = new SourceStream(source, mono);
            m_Head = m_Stereo;
            m_Resample = new ResampleStream(m_Head, (double)m_SrcRate / AudioEngine.SampleRate);
            m_Head = m_Resample;
        }

        // ------------------------------------------------------------------ settings

        public double Length { get { return m_Length; } }

        public bool CanSeek
        {
            get { lock (m_Lock) { return m_Source != null && m_Source.CanSeek; } }
        }

        public double Position
        {
            get
            {
                lock (m_Lock)
                {
                    double pos = m_BasePos + m_Played;
                    if (m_Length > 0 && pos > m_Length)
                        pos = m_Length;
                    return pos;
                }
            }
        }

        public ChannelState State
        {
            get { lock (m_Lock) { return m_State; } }
        }

        public bool Loop
        {
            get { lock (m_Lock) { return m_Loop; } }
            set { lock (m_Lock) { m_Loop = value; } }
        }

        /// <summary>Where a repeated sound starts again (seconds).</summary>
        public double LoopStart
        {
            get { lock (m_Lock) { return m_LoopStart; } }
            set { lock (m_Lock) { m_LoopStart = Math.Max(0, value); } }
        }

        /// <summary>The sound ends at this position (seconds), negative if it plays until its end.</summary>
        public double CueOut
        {
            get { lock (m_Lock) { return m_CueOut; } }
            set { lock (m_Lock) { m_CueOut = value; } }
        }

        /// <summary>At this position (seconds) FadeStarted is called, negative for never.</summary>
        /// <summary>Length (ms) of the fade out which starts inside the mixer when FadeAt is reached (0 for none).</summary>
        public int FadeOutMs { get; set; }

        public double FadeAt
        {
            get { lock (m_Lock) { return m_FadeAt; } }
            set { lock (m_Lock) { m_FadeAt = value; m_Fired = false; } }
        }

        public SpeakerRoute Route
        {
            get { lock (m_Lock) { return m_Route; } }
            set { lock (m_Lock) { m_Route = value ?? SpeakerRoute.Fronts; } }
        }

        /// <summary>Linear gain of the "volume (dB)" effect.</summary>
        public float Gain
        {
            get { lock (m_Lock) { return m_Gain; } }
            set { lock (m_Lock) { m_Gain = value; } }
        }

        /// <summary>Tempo in percent (0 = unchanged), pitch in semitones.</summary>
        public void SetTempoPitch(double tempoPercent, double pitchSemitones)
        {
            lock (m_Lock)
            {
                double tempo = 1.0 + tempoPercent / 100.0;
                tempo = Math.Max(0.1, Math.Min(6.0, tempo));
                double pitch = Math.Pow(2.0, Math.Max(-48, Math.Min(48, pitchSemitones)) / 12.0);
                m_Speed = tempo;
                double stretch = Math.Max(0.2, Math.Min(6.0, tempo / pitch));
                m_Speed = stretch * pitch;
                if (Math.Abs(stretch - 1.0) > 0.002)
                {
                    if (m_Stretch == null)
                    {
                        m_Stretch = new StretchStream(m_Stereo, m_SrcRate);
                        m_Resample = new ResampleStream(m_Stretch, 1.0);
                        m_Head = m_Resample;
                    }
                    m_Stretch.Tempo = stretch;
                }
                else if (m_Stretch != null)
                {
                    m_Stretch.Tempo = 1.0;
                }
                m_Resample.Ratio = (double)m_SrcRate / AudioEngine.SampleRate * pitch;
            }
        }

        public void SetReverb(float level, int delayMs)
        {
            lock (m_Lock)
            {
                m_Reverb = new SimpleReverb(level, delayMs, AudioEngine.SampleRate);
            }
        }

        public void SetVolume(float volume)
        {
            lock (m_Lock) { if (!m_StopAtSilence) Volume.Set(volume); }
        }

        public void SlideVolume(float target, int milliseconds)
        {
            lock (m_Lock) { if (!m_StopAtSilence) Volume.Slide(target, milliseconds * 0.001 * AudioEngine.SampleRate); }
        }

        public void SetPan(float pan)
        {
            lock (m_Lock) { Pan.Set(Math.Max(-1f, Math.Min(1f, pan))); }
        }

        /// <summary>Slides the balance; the time is in seconds of the file.</summary>
        public void SlidePan(float target, double fileSeconds)
        {
            lock (m_Lock) { Pan.Slide(Math.Max(-1f, Math.Min(1f, target)), fileSeconds / m_Speed * AudioEngine.SampleRate); }
        }

        /// <summary>Fades out and stops the sound; Ended is called when it is silent.</summary>
        public void FadeOutAndStop(int milliseconds)
        {
            bool immediate = false;
            lock (m_Lock)
            {
                double frames = milliseconds * 0.001 * AudioEngine.SampleRate;
                if (frames < 2 || m_State == ChannelState.Stopped)
                {
                    immediate = m_State != ChannelState.Stopped;
                    if (immediate)
                        Finish();
                }
                else
                {
                    m_StopAtSilence = true;
                    Volume.Slide(0f, frames);
                }
            }
            if (immediate)
            {
                m_Engine.RemoveChannel(this);
                if (Ended != null)
                    m_Engine.Post(Ended);
            }
        }

        public bool Seek(double seconds)
        {
            lock (m_Lock)
            {
                if (m_Source == null || !m_Source.CanSeek)
                    return AudioErrors.Fail(AudioError.Position);
                try
                {
                    m_Source.Seek(seconds);
                }
                catch (Exception)
                {
                    return AudioErrors.Fail(AudioError.Position);
                }
                m_Head.Reset();
                m_BasePos = seconds;
                m_Played = 0;
                if (m_FadeAt >= 0 && seconds < m_FadeAt)
                    m_Fired = false;
                return true;
            }
        }

        // ------------------------------------------------------------------ control

        public bool Start()
        {
            lock (m_Lock)
            {
                if (m_State == ChannelState.Stopped)
                    return AudioErrors.Fail(AudioError.Ended);
                m_State = ChannelState.Playing;
            }
            m_Engine.AddChannel(this);
            return true;
        }

        public bool Pause()
        {
            lock (m_Lock)
            {
                if (m_State != ChannelState.Playing)
                    return false;
                m_State = ChannelState.Paused;
                return true;
            }
        }

        public bool Resume()
        {
            lock (m_Lock)
            {
                if (m_State != ChannelState.Paused)
                    return false;
                m_State = ChannelState.Playing;
                return true;
            }
        }

        /// <summary>Stops without calling anything.</summary>
        public void Stop()
        {
            lock (m_Lock)
            {
                Finish();
            }
            m_Engine.RemoveChannel(this);
        }

        // called with the lock held
        private void Finish()
        {
            m_State = ChannelState.Stopped;
            if (m_Source != null)
            {
                try { m_Source.Dispose(); } catch (Exception) { }
                m_Source = null;
            }
        }

        // ------------------------------------------------------------------ mixing (mixer thread)

        /// <summary>
        /// Adds <paramref name="frames"/> frames of this sound to the output (interleaved, <paramref name="outChannels"/> speakers).
        /// Returns false if the channel is finished and can be removed.
        /// </summary>
        public bool Mix(float[] output, int frames, int outChannels)
        {
            bool endedNow = false;
            bool loopedNow = false;
            bool fadeNow = false;
            bool alive = true;
            lock (m_Lock)
            {
                if (m_State == ChannelState.Stopped)
                    return false;
                if (m_State != ChannelState.Playing || m_Source == null)
                    return true;
                if (m_Scratch.Length < frames * 2)
                    m_Scratch = new float[frames * 2];
                int done = 0;
                int emptyRounds = 0;
                while (done < frames)
                {
                    int want = frames - done;
                    double posNow = m_BasePos + m_Played;
                    bool reachedEnd = false;
                    if (m_CueOut > 0)
                    {
                        double remaining = (m_CueOut - posNow) / m_Speed * AudioEngine.SampleRate;
                        if (remaining <= want)
                        {
                            want = Math.Max(0, (int)remaining);
                            reachedEnd = true;
                        }
                    }
                    int got = want > 0 ? ReadStage(m_Scratch, want) : 0;
                    if (got < want)
                        reachedEnd = true;
                    if (got > 0)
                    {
                        Process(m_Scratch, got);
                        RouteTo(m_Scratch, got, output, done, outChannels);
                        done += got;
                        m_Played += got * m_Speed / AudioEngine.SampleRate;
                        emptyRounds = 0;
                        if (m_FadeAt >= 0 && !m_Fired && m_BasePos + m_Played >= m_FadeAt)
                        {
                            m_Fired = true;
                            fadeNow = true;
                            if (FadeOutMs > 0 && !m_StopAtSilence)
                                Volume.Slide(0f, FadeOutMs * 0.001 * AudioEngine.SampleRate);
                        }
                        if (Volume.TakeFinished() && m_StopAtSilence)
                        {
                            Finish();
                            endedNow = true;
                            alive = false;
                            break;
                        }
                    }
                    if (!reachedEnd)
                        continue;
                    // the end of the sound
                    if (m_Loop && m_Source.CanSeek && ++emptyRounds < 3)
                    {
                        try
                        {
                            m_Source.Seek(m_LoopStart);
                            m_Head.Reset();
                            m_BasePos = m_LoopStart;
                            m_Played = 0;
                            m_Fired = false;
                            loopedNow = true;
                        }
                        catch (Exception)
                        {
                            Finish();
                            endedNow = true;
                            alive = false;
                            break;
                        }
                        continue;
                    }
                    Finish();
                    endedNow = true;
                    alive = false;
                    break;
                }
            }
            if (fadeNow && FadeStarted != null)
                m_Engine.Post(FadeStarted);
            if (loopedNow && Looped != null)
                m_Engine.Post(Looped);
            if (endedNow && Ended != null)
                m_Engine.Post(Ended);
            return alive;
        }

        private int ReadStage(float[] buffer, int frames)
        {
            try
            {
                return m_Head.Read(buffer, 0, frames);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // volume, balance, effects: in place on stereo frames
        private void Process(float[] buf, int frames)
        {
            if (m_Gain != 1f)
            {
                float g = m_Gain;
                for (int i = 0; i < frames * 2; i++)
                    buf[i] *= g;
            }
            if (m_Reverb != null)
                m_Reverb.Process(buf, frames);
            float v0 = Volume.Current;
            float v1 = Volume.Advance(frames);
            float p0 = Pan.Current;
            float p1 = Pan.Advance(frames);
            if (v0 == v1 && p0 == p1)
            {
                float l = v0 * (p0 > 0 ? 1f - p0 : 1f);
                float r = v0 * (p0 < 0 ? 1f + p0 : 1f);
                if (l != 1f || r != 1f)
                {
                    for (int f = 0; f < frames; f++)
                    {
                        buf[f * 2] *= l;
                        buf[f * 2 + 1] *= r;
                    }
                }
                return;
            }
            float inv = 1f / frames;
            for (int f = 0; f < frames; f++)
            {
                float t = (f + 1) * inv;
                float v = v0 + (v1 - v0) * t;
                float p = p0 + (p1 - p0) * t;
                buf[f * 2] *= v * (p > 0 ? 1f - p : 1f);
                buf[f * 2 + 1] *= v * (p < 0 ? 1f + p : 1f);
            }
        }

        private void RouteTo(float[] buf, int frames, float[] output, int outFrameOffset, int n)
        {
            SpeakerRoute route = m_Route;
            int[] targets = route.Targets;
            if (route.Mono)
            {
                for (int t = 0; t < targets.Length; t++)
                {
                    int target = targets[t];
                    if (target >= n)
                        continue;
                    for (int f = 0; f < frames; f++)
                        output[(outFrameOffset + f) * n + target] += (buf[f * 2] + buf[f * 2 + 1]) * 0.5f;
                }
            }
            else
            {
                for (int t = 0; t + 1 < targets.Length; t += 2)
                {
                    int a = targets[t];
                    int b = targets[t + 1];
                    for (int f = 0; f < frames; f++)
                    {
                        int o = (outFrameOffset + f) * n;
                        if (a < n)
                            output[o + a] += buf[f * 2];
                        if (b < n)
                            output[o + b] += buf[f * 2 + 1];
                    }
                }
            }
        }
    }
}

/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The sound output of Windows: WASAPI, through NAudio.
*/
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Ares.Playing.Audio
{
    [SupportedOSPlatform("windows")]
    internal sealed class WasapiBackend : AudioBackend
    {
        private sealed class WasapiSink : Sink
        {
            private readonly BufferedWaveProvider m_Buffer;
            private WasapiOut m_Out;

            public WasapiSink(string deviceId, int channels)
            {
                Channels = channels;
                DeviceId = deviceId;
                WaveFormat format = channels > 2
                    ? new WaveFormatExtensible(AudioEngine.SampleRate, 32, channels)
                    : WaveFormat.CreateIeeeFloatWaveFormat(AudioEngine.SampleRate, channels);
                m_Buffer = new BufferedWaveProvider(format);
                m_Buffer.BufferDuration = TimeSpan.FromMilliseconds(1500);
                m_Buffer.DiscardOnBufferOverflow = true;
                m_Buffer.ReadFully = true;
                MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
                try
                {
                    MMDevice device = enumerator.GetDevice(deviceId);
                    WasapiOut output = new WasapiOut(device, AudioClientShareMode.Shared, true, 60);
                    try
                    {
                        output.PlaybackStopped += OnStopped;
                        output.Init(m_Buffer);
                        output.Play();
                    }
                    catch (Exception)
                    {
                        output.PlaybackStopped -= OnStopped;
                        try { output.Dispose(); } catch (Exception) { }
                        throw;
                    }
                    m_Out = output;
                }
                finally
                {
                    enumerator.Dispose();
                }
            }

            private void OnStopped(object sender, StoppedEventArgs e)
            {
                if (e != null && e.Exception != null)
                    Failed = true;
            }

            public override int BufferedFrames
            {
                get { return m_Buffer.BufferedBytes / (4 * Channels); }
            }

            public override void Write(byte[] data, int count)
            {
                m_Buffer.AddSamples(data, 0, count);
            }

            public override void Dispose()
            {
                WasapiOut output = m_Out;
                m_Out = null;
                if (output != null)
                {
                    output.PlaybackStopped -= OnStopped;
                    try { output.Stop(); } catch (Exception) { }
                    try { output.Dispose(); } catch (Exception) { }
                }
            }
        }

        public override List<AudioDeviceInfo> ListCards()
        {
            List<AudioDeviceInfo> cards = new List<AudioDeviceInfo>();
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            try
            {
                foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    string name;
                    try { name = device.FriendlyName; }
                    catch (Exception) { name = device.ID; }
                    cards.Add(new AudioDeviceInfo { Name = name, Id = device.ID });
                }
            }
            finally
            {
                enumerator.Dispose();
            }
            return cards;
        }

        public override string DefaultDeviceId()
        {
            try
            {
                MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
                try
                {
                    MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    return device.ID;
                }
                finally
                {
                    enumerator.Dispose();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public override int MixChannels(string deviceId)
        {
            try
            {
                MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
                try
                {
                    MMDevice device = enumerator.GetDevice(deviceId);
                    int channels = device.AudioClient.MixFormat.Channels;
                    device.Dispose();
                    if (channels >= 8) return 8;
                    if (channels == 6) return 6;
                    return 2;
                }
                finally
                {
                    enumerator.Dispose();
                }
            }
            catch (Exception)
            {
                return 2;
            }
        }

        public override Sink Open(string deviceId, int channels)
        {
            return new WasapiSink(deviceId, channels);
        }
    }
}

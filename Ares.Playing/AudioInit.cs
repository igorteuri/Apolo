/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Starts the audio engine of Apolo (replaces the BASS library), and lists / switches the output devices.
*/
using System;
using System.Collections.Generic;
using System.Globalization;
using Ares.Playing.Audio;

namespace Ares.Playing
{
    [Serializable]
    public class AudioInitException : Exception
    {
        public AudioInitException(String message)
            : base(message)
        {
        }

        public AudioInitException(String message, Exception inner)
            : base(message, inner)
        {
        }

        protected AudioInitException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }

    public class AudioInit : IDisposable
    {
        private AudioEngine m_Engine;

        internal static string Text(string pt, string en, string de)
        {
            string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (lang == "pt")
                return pt;
            if (lang == "de")
                return de;
            return en;
        }

        /// <summary>The name of the "no sound on this PC" entry in the list of output devices.</summary>
        public static string NoLocalOutputName
        {
            get { return Text("Não ouvir pelo PC", "Don't listen on this PC", "Nicht an diesem PC hören"); }
        }

        public static event Action OutputChanged;

        private static void RaiseOutputChanged()
        {
            Action handler = OutputChanged;
            if (handler != null)
                handler();
        }

        public AudioInit(int deviceIndex, Action<String> warningHandler)
        {
            try
            {
                m_Engine = AudioEngine.Create(deviceIndex, warningHandler);
                m_Engine.OutputChanged += RaiseOutputChanged;
            }
            catch (AudioInitException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new AudioInitException(String.Format(StringResources.BassInitFail,
                                              ex.Message + "(" + ex.GetType().FullName + ")",
                                              ex.StackTrace));
            }
        }

        public class OutputDevice
        {
            public int Index { get; set; }
            public String Name { get; set; }
        }

        /// <summary>
        /// The output devices: -1 is the Windows default, 0 plays nothing on this PC
        /// (the sound is only sent to Discord, VDO.Ninja ...), 1 and more are the sound cards.
        /// </summary>
        public static List<OutputDevice> GetDevices()
        {
            var result = new List<OutputDevice>();
            result.Add(new OutputDevice() { Index = -1, Name = StringResources.Default });
            result.Add(new OutputDevice() { Index = 0, Name = NoLocalOutputName });
            foreach (AudioDeviceInfo card in AudioEngine.ListCards())
                result.Add(new OutputDevice() { Index = card.Index, Name = card.Name });
            return result;
        }

        /// <summary>Gets the output devices which can currently be used.</summary>
        public static List<OutputDevice> GetActiveDevices()
        {
            return GetDevices();
        }

        /// <summary>
        /// The device index of a saved choice: sound cards are saved with their name, because their
        /// index changes when devices are plugged in or removed.
        /// </summary>
        public static int ResolveIndex(int savedIndex, string savedName)
        {
            if (savedIndex <= 0)
                return savedIndex;
            if (String.IsNullOrEmpty(savedName))
                return -1; // saved by an older version: the numbers had another meaning
            foreach (OutputDevice device in GetDevices())
            {
                if (device.Index > 0 && String.Equals(device.Name, savedName, StringComparison.OrdinalIgnoreCase))
                    return device.Index;
            }
            return -1;
        }

        /// <summary>The name to save for a device index (empty for the special entries).</summary>
        public static string NameOf(int index)
        {
            if (index <= 0)
                return String.Empty;
            foreach (OutputDevice device in GetDevices())
            {
                if (device.Index == index)
                    return device.Name;
            }
            return String.Empty;
        }

        /// <summary>False if Apolo plays nothing on this PC ("don't listen on this PC").</summary>
        public static bool LocalOutputEnabled
        {
            get
            {
                AudioEngine engine = AudioEngine.Instance;
                return engine == null || engine.LocalOutput;
            }
        }

        /// <summary>
        /// Registers a listener for everything Apolo plays (48 kHz, stereo, 16 bit, blocks of 20 ms).
        /// The listener is called on its own thread.
        /// </summary>
        public static bool AddPcmListener(Action<byte[], int> listener)
        {
            AudioEngine engine = AudioEngine.Instance;
            if (engine == null)
                return false;
            engine.AddTap(listener);
            return true;
        }

        public static void RemovePcmListener(Action<byte[], int> listener)
        {
            AudioEngine engine = AudioEngine.Instance;
            if (engine != null)
                engine.RemoveTap(listener);
        }

        /// <summary>
        /// Switches to another output device without interrupting what is playing.
        /// If this isn't possible, nothing changes and an exception is thrown.
        /// </summary>
        public void SwitchDeviceKeepPlaying(int newDevice)
        {
            if (m_Engine == null)
                throw new AudioInitException(StringResources.BassDeviceInvalid);
            if (m_Engine.DeviceIndex == newDevice)
                return;
            m_Engine.SwitchDevice(newDevice);
        }

        public void SwitchDevice(int newDevice)
        {
            if (m_Engine == null)
                throw new AudioInitException(StringResources.BassDeviceInvalid);
            if (m_Engine.DeviceIndex == newDevice)
                return;
            try
            {
                m_Engine.SwitchDevice(newDevice);
            }
            catch (Exception ex)
            {
                try { m_Engine.SwitchDevice(-1); } catch (Exception) { }
                if (ex is AudioInitException)
                    throw;
                throw new AudioInitException(String.Format(StringResources.BassInitFail,
                                              ex.Message + "(" + ex.GetType().FullName + ")",
                                              ex.StackTrace));
            }
        }

        public void Dispose()
        {
            if (m_Engine != null)
            {
                m_Engine.OutputChanged -= RaiseOutputChanged;
                m_Engine.Dispose();
                m_Engine = null;
            }
        }
    }
}

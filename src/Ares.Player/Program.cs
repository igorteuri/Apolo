/*
 Copyright (c) 2015 [Joerg Ruedenauer]
 
 This file is part of Ares.

 Ares is free software; you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation; either version 2 of the License, or
 (at your option) any later version.

 Ares is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with Ares; if not, write to the Free Software
 Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Ares.Player
{
    static class Program
    {
		
#if MONO
#if !ANDROID
		[System.Runtime.InteropServices.DllImport ("X11")]
		extern public static int XInitThreads ();
#endif
#endif

		private static void PlatformInit ()
		{
#if MONO
#if !ANDROID
			try {
				XInitThreads ();
			} catch (Exception) {
				// silent failure
			}
#endif
#endif
		}


        // an error in an event handler must not close the program with the work of the session: it is written to
        // crash.log (next to the settings), the user is told, and the program goes on
        private static void InstallCrashHandlers()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ReportCrash(e.Exception, false);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash(e.ExceptionObject as Exception, e.IsTerminating);
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) => { Log(e.Exception); e.SetObserved(); };
        }

        private static readonly object s_LogLock = new object();
        private static DateTime s_LastDialog = DateTime.MinValue;

        private static void Log(Exception ex)
        {
            try
            {
                lock (s_LogLock)
                {
                    string file = System.IO.Path.Combine(Ares.Settings.AppFolders.Root, "crash.log");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
                    if (System.IO.File.Exists(file) && new System.IO.FileInfo(file).Length > 512 * 1024)
                        System.IO.File.Delete(file);
                    System.IO.File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + (ex != null ? ex.ToString() : "(unknown error)") + Environment.NewLine + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                // the log is the last resort: nothing more to do
            }
        }

        private static void ReportCrash(Exception ex, bool terminating)
        {
            Log(ex);
            // one message at a time (a broken repaint can raise the same error again and again)
            if ((DateTime.Now - s_LastDialog).TotalSeconds < 10)
                return;
            s_LastDialog = DateTime.Now;
            try
            {
                bool pt = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";
                string nl = Environment.NewLine + Environment.NewLine;
                string detail = ex != null ? ex.Message : "?";
                string text;
                if (terminating)
                    text = (pt ? "Erro grave: o Apolo precisa fechar. Seu projeto foi salvo na última alteração." : "Serious error: Apolo has to close. Your project was saved at the last change.")
                        + nl + detail;
                else
                    text = (pt ? "Ocorreu um erro inesperado, mas o Apolo continua aberto. Seu projeto é salvo a cada alteração." : "An unexpected error occurred, but Apolo stays open. Your project is saved after every change.")
                        + nl + detail + nl
                        + (pt ? "O registro completo está em crash.log, na pasta de dados do Apolo." : "The full record is in crash.log, in the Apolo data folder.");
                MessageBox.Show(text, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
			PlatformInit ();
#if !NET
            ServiceStack.Licensing.RegisterLicense(@"1001-e1JlZjoxMDAxLE5hbWU6VGVzdCBCdXNpbmVzcyxUeXBlOkJ1c2luZXNzLEhhc2g6UHVNTVRPclhvT2ZIbjQ5MG5LZE1mUTd5RUMzQnBucTFEbTE3TDczVEF4QUNMT1FhNXJMOWkzVjFGL2ZkVTE3Q2pDNENqTkQyUktRWmhvUVBhYTBiekJGUUZ3ZE5aZHFDYm9hL3lydGlwUHI5K1JsaTBYbzNsUC85cjVJNHE5QVhldDN6QkE4aTlvdldrdTgyTk1relY2eis2dFFqTThYN2lmc0JveHgycFdjPSxFeHBpcnk6MjAxMy0wMS0wMX0=");
#endif
            if (Environment.GetCommandLineArgs().Length > 1)
            {
                string arg = Environment.GetCommandLineArgs()[1];
                string paramName = "Language=";
                if (arg.StartsWith(paramName))
                {
                    arg = arg.Substring(paramName.Length);
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(arg);
                }
            }
            Ares.CommonGUI.LanguageManager.Apply();
            InstallCrashHandlers();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Ares.CommonGUI.ThemeManager.Install();
            SplashForm.ShowSplash();
            try
            {
                using (Ares.Playing.AudioInit bassInit = new Ares.Playing.AudioInit(-1, s => MessageBox.Show(s, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning)))
                {
                    Application.Run(new Player(bassInit));
                }
            }
#if !MONO
            catch (Ares.Ipc.ApplicationAlreadyStartedException)
            {
            }
#endif
            catch (Ares.Playing.AudioInitException ex)
            {
                SplashForm.CloseSplash();
                MessageBox.Show(ex.Message, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
            finally
            {
                SplashForm.CloseSplash();
            }
        }
    }
}

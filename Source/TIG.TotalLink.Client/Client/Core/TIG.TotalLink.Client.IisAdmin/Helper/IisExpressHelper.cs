using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace TIG.TotalLink.Client.IisAdmin.Helper
{
    public class IisExpressHelper
    {
        #region Win32 API

        internal class NativeMethods
        {
            public const uint WM_QUIT = 0x12;

            [DllImport("user32.dll", SetLastError = true)]
            internal static extern IntPtr GetTopWindow(IntPtr hWnd);

            [DllImport("user32.dll", SetLastError = true)]
            internal static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

            [DllImport("user32.dll", SetLastError = true)]
            internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint lpdwProcessId);

            [DllImport("user32.dll", SetLastError = true)]
            internal static extern bool PostMessage(HandleRef hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        }

        #endregion


        #region Private Fields

        private static string _iisExpressPath;

        #endregion


        #region Constructors

        static IisExpressHelper()
        {
            // Attempt to get the path to the IIS Express executable from the registry
            var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\IISExpress\8.0");
            if (key == null)
                return;

            _iisExpressPath = Path.Combine(((string)key.GetValue("InstallPath", @"C:\Program Files (x86)\IIS Express\")), "iisexpress.exe");
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Stops all IIS Express processes.
        /// </summary>
        public static void StopIIsExpress()
        {
            // Stop all IIS Express worker processes
            var processes = Process.GetProcessesByName("iisexpress");
            foreach (var process in processes)
            {
                SendStopMessageToProcess(process.Id);
            }

            // Wait for the IIS Express Tray application to close
            var repeats = 10;
            do
            {
                Thread.Sleep(1000);
                processes = Process.GetProcessesByName("iisexpresstray");
                repeats--;
            } while (processes.Length > 0 && repeats > 0);
        }

        /// <summary>
        /// Starts the specified site in IIS Express.
        /// </summary>
        /// <param name="name">The name of the site to start.</param>
        public static void StartIisExpressSite(string name)
        {
            var psi = new ProcessStartInfo(_iisExpressPath, string.Format("/site:{0}", name))
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Sends a stop message to a process.
        /// </summary>
        /// <param name="pid">The id of the process to stop.</param>
        private static void SendStopMessageToProcess(int pid)
        {
            try
            {
                for (var ptr = NativeMethods.GetTopWindow(IntPtr.Zero); ptr != IntPtr.Zero; ptr = NativeMethods.GetWindow(ptr, 2))
                {
                    uint num;
                    NativeMethods.GetWindowThreadProcessId(ptr, out num);
                    if (pid == num)
                    {
                        var hWnd = new HandleRef(null, ptr);
                        NativeMethods.PostMessage(hWnd, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                        return;
                    }
                }
            }
            catch (ArgumentException)
            {
            }
        }

        #endregion
    }
}

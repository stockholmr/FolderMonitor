using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Specialized;
using System.IO;
using System.Configuration;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Logging;

namespace FolderMonitor
{
    public class Program
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        static void Main(string[] args)
        {
            //IntPtr h = Process.GetCurrentProcess().MainWindowHandle;
            //ShowWindow(h, 0);

            string appPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

            FileLogger _logger = new FileLogger(LogLevel.Debug, appPath + "\\logs\\FolderMonitor.log");


            Monitor monitor = new Monitor();
            monitor.Load(appPath + "\\foldermonitor.ini");

            if(monitor.Count == 0)
            {
                Console.WriteLine("Nothing to do!");
                monitor.WriteExampleIni(appPath + "\\foldermonitor.example.ini");
                Environment.Exit(1);
            }


            ManualResetEvent _resetEvent = new ManualResetEvent(false);

            monitor.Start();

            Console.CancelKeyPress += delegate
            {
               monitor.Stop();
               _resetEvent.Set();
            };

            _resetEvent.WaitOne();
            Environment.Exit(0);
        }
    }
}

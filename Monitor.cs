using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics.Eventing.Reader;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Configuration;
using System.ComponentModel;

namespace FolderMonitor
{
    public class MonitorPath
    {
        private string _id;
        private string _path;
        private long   _timeout;
        private string _action;
        private string _args;
        private string _filter = "";
        private bool   _recursive = true;

        private FileSystemWatcher _watcher;
        private ManualResetEvent _resetEvent;
        private TimeSpan _lastRun;
        private Timer _timer;
        private Mutex _mutex;
        private bool _actionRunning = false;
        private bool _actionRequired = false;
        private bool _cancelled = false;

        public string ID { get { return _id; } set { _id = value; } }
        [Description("path: Path to monitor for changes")]
        public string Path { get { return _path; } set { _path = value; } }
        [Description("timeout: Length of time in miliseconds between running the action")]
        public long Timeout { get { return _timeout; } set { _timeout = value; } }
        [Description("action: Action to execute when changes to path occur")]
        public string Action { get { return _action; } set { _action = value; } }
        public string Args { get { return _args; } set { _args = value; } }
        [Description("recursive: Monitor all sub folders")]
        public bool Recursive { get { return _recursive; } set { _recursive = value; } }
        [Description("filter: Only monitor for changes to specified file type")]
        public string Filter { get { return _filter; } set { _filter = value; } }

        public bool IsCancelled { get { return _cancelled; } }

        public MonitorPath(string path, long timeOut, string action, string args) {
            _path = path;
            _timeout = timeOut;
            _action = action;
            _args = args;

            _resetEvent = new ManualResetEvent(false);
            _mutex = new Mutex(false);
        }

        public MonitorPath()
        {
            _resetEvent = new ManualResetEvent(false);
            _mutex = new Mutex(false);
        }

        public void Start(object obj)
        {
            _timer = new Timer(OnTimerEvent, null, 0, 1000);

            _watcher = new FileSystemWatcher(_path);
            _watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

            _watcher.Changed += OnEvent;
            _watcher.Created += OnEvent;
            _watcher.Deleted += OnEvent;
            _watcher.Renamed += OnEvent;
            _watcher.Error += OnError;
            _watcher.Filter = _filter;
            _watcher.IncludeSubdirectories = _recursive;
            _watcher.EnableRaisingEvents = true;

            _resetEvent.WaitOne();
        }

        public void Cancel()
        {
            _resetEvent.Set();
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnEvent;
            _watcher.Created -= OnEvent;
            _watcher.Deleted -= OnEvent;
            _watcher.Renamed -= OnEvent;
            _watcher.Error -= OnError;
            _watcher.Dispose();
            _watcher = null;
            _timer.Dispose();
            _cancelled = true;

            Console.WriteLine("Stopped Monitoring: " + _path);
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            Console.WriteLine(e.GetException().Message);
        }

        private void OnEvent(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine(e.ChangeType.ToString() + ": " + e.FullPath);
            _actionRequired = true;
        }

        private void OnTimerEvent(object obj)
        {
            RunAction();
        }


        private void RunAction()
        {
            if (_actionRequired && !_actionRunning)
            {
                _mutex.WaitOne();

                if (_lastRun.TotalMilliseconds == 0)
                {
                    _lastRun = DateTime.Now.TimeOfDay;
                }

                TimeSpan ts = DateTime.Now.TimeOfDay.Subtract(_lastRun);
                if (Math.Round(ts.TotalMilliseconds, 0) > _timeout)
                {
                    if (!_actionRunning)
                    {
                        _actionRunning = true;
                        _lastRun = DateTime.Now.TimeOfDay;

                        ProcessStartInfo info = new ProcessStartInfo();
                        info.UseShellExecute = true;
                        info.Verb = "open";
                        info.FileName = _action;
                        info.Arguments = _args;

                        try
                        {
                            var proc = Process.Start(info);
                            proc.WaitForExit();
                        } catch (Exception ex) {
                            if(ex.Message == "The system cannot find the file specified")
                            {
                                Console.WriteLine("File not found: " + _action);
                                Cancel();
                            }
                        }

                        _actionRunning = false;
                        _actionRequired = false;
                    }
                }

                _mutex.ReleaseMutex();
            }
        }

        public string ToIni()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[" + ID + "]");

            var properties = typeof(MonitorPath).GetProperties();
            foreach (var property in properties)
            {
                if (property.Name.ToLower() != "id" && property.Name.ToLower() != "iscancelled")
                {
                    string commentLine = "";
                    var attributes = property.GetCustomAttributes(false);
                    if(attributes.Length > 0)
                    {
                        commentLine = ";" + ((System.ComponentModel.DescriptionAttribute)attributes[0]).Description + "\n";
                    }

                    string line = String.Format("{0}=\"{1}\"", property.Name.ToLower(), property.GetValue(this).ToString());
                    
                    if (property.PropertyType == typeof(Int32) || 
                        property.PropertyType == typeof(Int64) ||
                        property.PropertyType == typeof(bool)
                    ) {
                        line = String.Format("{0}={1}", property.Name.ToLower(), property.GetValue(this).ToString());
                    }

                    sb.Append(commentLine);
                    sb.AppendLine(line);
                }
            }

            return sb.ToString();
        }
    }


    public class Monitor
    {
        private List<MonitorPath> _paths;
        private int _pathCount = -1;

        public Monitor() {
            _paths = new List<MonitorPath>();
        }

        public int Count
        {
            get { return _paths.Count; }
        }

        public void AddPath(string path, long timeOut, string action, string args)
        {
            _paths.Add(new MonitorPath(path, timeOut, action, args));
            _pathCount++;
        }

        public void WriteExampleIni(string settingsFile)
        {
            var example = new MonitorPath();
            example.ID = "ExamplePath";
            example.Path = "D:\\PathToMonitor";
            example.Action = "filetorun.exe";
            example.Args = "";
            example.Timeout = 60000;
            example.Filter = "*.*";
            example.Recursive = true;

            using (StreamWriter sw = new StreamWriter(settingsFile, false))
            {
                sw.Write(example.ToIni());
            }

        }

        public void Load(string settingsFile)
        {
            if (File.Exists(settingsFile))
            {
                using (StreamReader sr = new StreamReader(settingsFile))
                {
                    string commentDelimiter = ";";
                    string line = "";
                    while ((line = sr.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length == 0) continue;  // empty line
                        if (!String.IsNullOrEmpty(commentDelimiter) && line.StartsWith(commentDelimiter))
                            continue;  // comment

                        if (line.StartsWith("[") && line.Contains("]"))  // [section]
                        {
                            _pathCount++;
                            _paths.Add(new MonitorPath());
                            int index = line.IndexOf(']');
                            _paths[_pathCount].ID = line.Substring(1, index - 1).Trim();
                            continue;
                        }

                        if (line.Contains("="))  // key=value
                        {
                            int index = line.IndexOf('=');
                            string key = line.Substring(0, index).Trim();
                            string val = line.Substring(index + 1).Trim();

                            if (val.StartsWith("\"") && val.EndsWith("\""))  // strip quotes
                                val = val.Substring(1, val.Length - 2);

                            var properties = typeof(MonitorPath).GetProperties();

                            foreach (var property in properties)
                            {
                                if (property.Name.ToLower() == key.ToLower())
                                {
                                    if (property.PropertyType == typeof(string))
                                    {
                                        property.SetValue(_paths[_pathCount], val);
                                    }

                                    if (property.PropertyType == typeof(Int32))
                                    {
                                        Int32 intVal;
                                        bool state = int.TryParse(val, out intVal);
                                        if (state)
                                        {
                                            property.SetValue(_paths[_pathCount], intVal);
                                        }
                                    }

                                    if (property.PropertyType == typeof(Int64))
                                    {
                                        Int64 intVal;
                                        bool state = long.TryParse(val, out intVal);
                                        if (state)
                                        {
                                            property.SetValue(_paths[_pathCount], intVal);
                                        }
                                    }

                                    if (property.PropertyType == typeof(bool))
                                    {
                                        bool boolVal;
                                        bool state = Boolean.TryParse(val, out boolVal);
                                        if (state)
                                        {
                                            property.SetValue(_paths[_pathCount], boolVal);
                                        } else
                                        {
                                            if(val == "1") { property.SetValue(_paths[_pathCount], true); }
                                            if (val == "0") { property.SetValue(_paths[_pathCount], false); }
                                            if (val == "") { property.SetValue(_paths[_pathCount], false); }
                                            if (val == "null") { property.SetValue(_paths[_pathCount], false); }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void Save(string settingsFile)
        {
            using (StreamWriter sw = new StreamWriter(settingsFile, false))
            {
                foreach (var path in _paths)
                {
                    sw.WriteLine(path.ToIni());
                }
            }
        }

        public void Start()
        {
            foreach (var path in _paths)
            {
                Console.WriteLine("Monitoring: " + path.Path);
                ThreadPool.QueueUserWorkItem(new WaitCallback(path.Start));
            }
        }


        public void Stop()
        {
            foreach(var path in _paths)
            {
                if(!path.IsCancelled)
                {
                    path.Cancel();
                }
            }
        }

    }
}

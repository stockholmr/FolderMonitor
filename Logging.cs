using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Logging
{
    public enum LogLevel {
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }

    public interface ILogger
    {
        LogLevel Level { get; set; }
        void Log(LogLevel level, string message);
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Fatal(string message);
    }

    public class FileLogger : ILogger
    {
        private string _basePath;
        private string _fileName;
        private string _fileExtension;
        private int    _logfileIndex;
        private bool _logOpen;
        private Mutex _mutex;
        private StreamWriter _logStream;
        private long _maxFileSize = 26214400;
        private int _maxLogFiles = 10;
        private LogLevel _logLevel = LogLevel.Info;

        public FileLogger(LogLevel level, string filePath)
        {
            Init(level, filePath);
        }

        public FileLogger(LogLevel level, string filePath, long maxFileSize)
        {
            _maxFileSize = maxFileSize;
            Init(level, filePath);
        }

        public FileLogger(LogLevel level, string filePath, long maxFileSize, int maxLogFiles)
        {
            _maxFileSize = maxFileSize;
            _maxLogFiles = maxLogFiles;
            Init(level, filePath);
        }

        private void Init(LogLevel level, string filePath)
        {
            _logLevel = level;
            _basePath = Path.GetDirectoryName(filePath);
            _fileName = Path.GetFileNameWithoutExtension(filePath);
            _fileExtension = Path.GetExtension(filePath);
            _mutex = new Mutex(false);
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
            _logfileIndex = GetRotationLogIndex();

            OpenFile(GetFilePath());
        }

        public long MaxFileSize
        {
            get { return _maxFileSize; }
            set { _maxFileSize = value; }
        }

        public int MaxLogFiles
        {
            get { return _maxLogFiles; }
            set { _maxLogFiles = value; }
        }

        public LogLevel Level { 
            get { return _logLevel; }
            set { _logLevel = value; } 
        }

        private string GetFilePath()
        {
            return Path.Combine(_basePath, _fileName + _fileExtension);
        }

        private void RotateLog()
        {
            if(_logfileIndex == _maxLogFiles +1)
            {
                _logfileIndex = 1;
            }

            string newFilename = String.Format(
                "{0}_{1}{2}",
                _fileName,
                _logfileIndex,
                _fileExtension
            );
            string newFilePath = Path.Combine(_basePath, newFilename);
            CloseFile();
            if(File.Exists(newFilePath))
            {
                File.Delete(newFilePath);
            }
            File.Move(GetFilePath(), newFilePath);
            _logfileIndex++;
            OpenFile(GetFilePath());
        }

        private int GetRotationLogIndex()
        {
            int _lastIndex = 1;
            var files = Directory.EnumerateFiles(_basePath, "*.log");
            foreach(var file in files)
            {
                var start = file.IndexOf('_') +1;
                var end = file.LastIndexOf('.');
                if(start > 0 && end < file.Length)
                {
                    int index;
                    bool ok = int.TryParse(file.Substring(start, end - start), out index);
                    if(ok)
                    {
                        if(index > _lastIndex)
                        {
                            _lastIndex = index;
                        }
                    }
                }
            }
            return _lastIndex;
        }

        private void OpenFile(string filepath)
        {
            _logStream = new StreamWriter(
                File.Open(
                    filepath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite
                )
            );
            _logOpen = true;
        }

        private void CloseFile()
        {
            _logStream.Flush();
            _logStream.Close();
            _logOpen = false;
        }

        public void Dispose()
        {
            CloseFile();
        }

        public void Log(LogLevel level, string message)
        {
            if (_logOpen)
            {
                if (level >= _logLevel)
                {
                    _mutex.WaitOne();
                    long fileSize = _logStream.BaseStream.Length;
                    if (fileSize >= _maxFileSize)
                    {
                        RotateLog();
                    }

                    var timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                    _logStream.WriteLine(
                        String.Format(
                            "{0} {1} {2}",
                            timestamp,
                            level.ToString(),
                            message
                        )
                    );
                    _logStream.Flush();
                    _mutex.ReleaseMutex();
                }
            }
        }

        public void Debug(string message)
        {
            Log(LogLevel.Debug, message);
        }
        public void Info(string message)
        {
            Log(LogLevel.Info, message);
        }
        public void Warn(string message)
        {
            Log(LogLevel.Warn, message);
        }

        public void Error(string message)
        {
            Log(LogLevel.Error, message);
        }

        public void Fatal(string message)
        {
            Log(LogLevel.Fatal, message);
            Environment.Exit(-1);
        }
    }

    public class ConsoleLogger : ILogger
    {
        private LogLevel _logLevel = LogLevel.Info;
        public ConsoleLogger() {}

        public ConsoleLogger(LogLevel logLevel)
        {
            _logLevel = logLevel;
        }

        public LogLevel Level
        {
            get { return _logLevel; }
            set { _logLevel = value; }
        }

        public void Log(LogLevel level, string message) {
            if (level >= _logLevel)
            {
                var timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                Console.WriteLine(
                    String.Format(
                        "{0} {1} {2}",
                        timestamp,
                        level.ToString(),
                        message
                    )
                );
            }
        }

        public void Debug(string message)
        {
            Log(LogLevel.Debug, message);
        }
        public void Info(string message) {
            Log(LogLevel.Info, message);
        }
        public void Warn(string message) {
            Log(LogLevel.Warn, message);
        }
        public void Error(string message) { 
            Log(LogLevel.Error, message);
        }
        public void Fatal(string message) { 
            Log(LogLevel.Fatal, message);
            Environment.Exit(-1);
        }

    }

    public class MultiLogger : ILogger
    {
        private List<ILogger> _loggers;
        private LogLevel _logLevel = LogLevel.Info;

        public MultiLogger() {
            _loggers = new List<ILogger>();
        }

        public LogLevel Level
        {
            get { return _logLevel; }
            set { _logLevel = value; }
        }

        public void Add(ILogger logger)
        {
            _loggers.Add(logger);
        }

        public void Log(LogLevel level, string message)
        {
            foreach(var logger in _loggers)
            {
                logger.Log(level, message);
            }
        }

        public void Debug(string message)
        {
            Log(LogLevel.Debug, message);
        }
        public void Info(string message)
        {
            Log(LogLevel.Info, message);
        }
        public void Warn(string message)
        {
            Log(LogLevel.Warn, message);
        }
        public void Error(string message)
        {
            Log(LogLevel.Error, message);
        }
        public void Fatal(string message)
        {
            Log(LogLevel.Fatal, message);
            Environment.Exit(-1);
        }
    }

    public static class Default
    {
        private static ILogger _logger = new ConsoleLogger();

        public static ILogger Logger
        {
            get { return _logger; }
            set { _logger = value; }
        }

        public static LogLevel Level
        {
            get { return _logger.Level; }
            set { _logger.Level = value; }
        }

        public static void Log(LogLevel level, string message)
        {
            _logger.Log(level, message);
        }

        public static void Debug(string message)
        {
            Log(LogLevel.Debug, message);
        }
        public static void Info(string message)
        {
            Log(LogLevel.Info, message);
        }
        public static void Warn(string message)
        {
            Log(LogLevel.Warn, message);
        }
        public static void Error(string message)
        {
            Log(LogLevel.Error, message);
        }
        public static void Fatal(string message)
        {
            Log(LogLevel.Fatal, message);
            Environment.Exit(-1);
        }
    }
}

﻿using System;
using System.IO;
using System.Threading;

namespace WatcherExample.DirectoryWatcher
{
    public class ContainerWatcher
    {
        public event NewFileCreatedEvent NewFileCreated;
        public event WatcherWillStartAgainEvent WatcherWillStartAgain;
        private FileSystemWatcher watcher;
        private readonly string path;
        private readonly string filter;

        private readonly CreatingFileList creatingFileList;

        public ContainerWatcher(string path, string filter)
        {
            creatingFileList = new CreatingFileList();
            this.path = path;
            this.filter = filter;
        }
        public void StartWatching()
        {
            if(watcher != null)
            {
                throw new Exception("Watcher already started");
            }
            Start();
        }
        private void Start()
        {
            Console.WriteLine("Watcher Starting ...");
            watcher = new FileSystemWatcher();
            watcher.Path = path;
            watcher.Filter = filter;
            watcher.NotifyFilter = NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName;

            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;
            try
            {
                watcher.EnableRaisingEvents = true;
                Console.WriteLine("Watcher Started");
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error on Watcher Starting: " + ex.Message);
                Thread.Sleep(10);
                Start();
            }
        }
        private void Stop()
        {
            Console.WriteLine("Watcher Stopping ...");
            try
            {
                watcher.EnableRaisingEvents = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error on Watcher Stopping: " + ex.Message);
                if (watcher.EnableRaisingEvents)
                {
                    Thread.Sleep(10);
                    Stop();
                    return;
                }
            }
            try
            {
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error on Watcher Disposing: " + ex.Message);
            }
            Console.WriteLine("Watcher Stopped");
            watcher = null;
        }
        private void Restart()
        {
            Console.WriteLine("Watcher Restarting ...");
            Stop();
            creatingFileList.Clear();
            WatcherWillStartAgain?.Invoke();
            Start();
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            Console.WriteLine("Watcher Error = " + e.GetException().Message);
            Restart();
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                creatingFileList.AddFilePath(e.FullPath);
            }
            ProcessFileIfCreatedAndReady(e.FullPath.ToString());
        }

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            creatingFileList.ChangeFilePath(e.OldFullPath.ToString(), e.FullPath.ToString());
            ProcessFileIfCreatedAndReady(e.FullPath.ToString());
        }

        private object lockReadyVerification = "lockReadyVerification";
        private void ProcessFileIfCreatedAndReady(string filePath)
        {
            lock (lockReadyVerification)
            {
                if (creatingFileList.Contains(filePath))
                {
                    if (FileIsReady(filePath))
                    {
                        creatingFileList.RemoveFilePath(filePath);
                        NewFileCreated?.Invoke(filePath);
                    }
                    else
                    {
                        Thread.Sleep(5);
                        ProcessFileIfCreatedAndReady(filePath);
                    }
                }
            }
        }
        private bool FileIsReady(string filePath)
        {
            try
            {
                using (var file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
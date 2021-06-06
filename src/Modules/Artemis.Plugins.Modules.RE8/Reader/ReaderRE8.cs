﻿using Serilog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace SRTPluginProviderRE8
{
    public class ReaderRE8 : IDisposable
    {
        private Process process;
        private GameMemoryRE8Scanner gameMemoryScanner;
        private ILogger logger;
        private Stopwatch stopwatch;

        public ReaderRE8(ILogger logger)
        {
            this.logger = logger;
        }
        public bool GameRunning
        {
            get
            {
                if (gameMemoryScanner != null && !gameMemoryScanner.ProcessRunning)
                {
                    process = GetProcess();
                    if (process != null)
                        gameMemoryScanner.Initialize(process, logger); // Re-initialize and attempt to continue.
                }

                return gameMemoryScanner != null && gameMemoryScanner.ProcessRunning;
            }
        }

        public void Init()
        {
            gameMemoryScanner = new GameMemoryRE8Scanner(process);
            stopwatch = new Stopwatch();
            stopwatch.Start();
        }

        public object PullData()
        {
            if (stopwatch == null || gameMemoryScanner == null)
                return null;

            try
            {
                if (!GameRunning) // Not running? Bail out!
                    return null;

                if (stopwatch.ElapsedMilliseconds >= 2000L)
                {
                    gameMemoryScanner.UpdatePointers();
                    stopwatch.Restart();
                }

                return gameMemoryScanner.Refresh();
            }
            catch (Win32Exception ex)
            {
                // if ((ProcessMemory.Win32Error)ex.NativeErrorCode != ProcessMemory.Win32Error.ERROR_PARTIAL_COPY)
                // TODO
            }
            catch (Exception ex)
            {
               //TODO Log
            }
            return null;
        }

        private Process GetProcess() => Process.GetProcessesByName("re8")?.FirstOrDefault();

        public void Dispose()
        {
            stopwatch?.Stop();
            stopwatch = null;
            gameMemoryScanner?.Dispose();
            gameMemoryScanner = null;
        }
    }
}

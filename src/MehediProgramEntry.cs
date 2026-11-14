using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("MHS Download Manager 1.0.8 (Mehedi Hasan Shihab)")]
[assembly: AssemblyDescription("MHS Download Manager 1.0.8 (Mehedi Hasan Shihab) — Multi-Segment Download Accelerator & Browser Video Suite")]
[assembly: AssemblyCompany("Mehedi Hasan Shihab (sshihabb007)")]
[assembly: AssemblyProduct("MHS Download Manager 1.0.8 (Mehedi Hasan Shihab)")]
[assembly: AssemblyCopyright("Copyright © Mehedi Hasan Shihab — https://sshihabb007.github.io/")]
[assembly: AssemblyVersion("1.0.8.0")]
[assembly: AssemblyFileVersion("1.0.8.0")]
[assembly: AssemblyInformationalVersion("1.0.8")]

namespace ShihabMehediIDM
{
    public static class MehediProgramEntry
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);
        private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFFu;
        private const int STD_OUTPUT_HANDLE = -11;

        private static void EnsureConsoleIfNotRedirected()
        {
            try
            {
                IntPtr stdOut = GetStdHandle(STD_OUTPUT_HANDLE);
                if (stdOut == IntPtr.Zero || stdOut == new IntPtr(-1))
                {
                    AttachConsole(ATTACH_PARENT_PROCESS);
                }
            }
            catch { }
        }

        [STAThread]
        public static int Main(string[] shihabRawArgs)
        {
            ShihabCliParsedArguments mehediCli = ShihabCliParsedArguments.ParseMehediArgs(shihabRawArgs);

            // 1. Out-of-process crash watchdog mode (runs prior to Mutex or WinForms initialization)
            if (mehediCli.ShihabRunCrashWatchdog)
            {
                return ShihabCrashMonitor.RunOutOfProcessWatchdog(
                    mehediCli.ShihabCrashWatchdogPid,
                    mehediCli.ShihabCrashWatchdogDir);
            }

            ShihabStateStore shihabStore = new ShihabStateStore(mehediCli.MehediCustomConfigDir);
            MehediAppConfiguration mehediConfig = shihabStore.LoadShihabConfig();

            // 2. Stdio Native Messaging Host (Chrome/Edge/Firefox) — never touch parent console
            if (mehediCli.ShihabRunAsNativeHost)
            {
                return MehediBrowserBridgeServer.RunShihabNativeMessagingStdioLoop(mehediConfig.MehediLocalBridgePort);
            }

            // 3. Stdio Model Context Protocol (MCP) JSON-RPC Server — never call AttachConsole so stdio pipes stay clean
            if (mehediCli.ShihabRunAsMcpServer)
            {
                return ShihabMcpAndCliHost.RunMcpStdioServer(shihabStore);
            }

            // 4. Forget Sign-Ins Vault Purge
            if (mehediCli.ShihabForgetSignIns)
            {
                EnsureConsoleIfNotRedirected();
                int purged = ShihabSecretsVault.ForgetAllSignIns(shihabStore);
                try { Console.WriteLine("Purged " + purged + " saved credentials and API tokens from vault."); } catch { }
                return 0;
            }

            // 5. Headless CLI subcommands
            if (mehediCli.ShihabRunAsHeadlessCli)
            {
                EnsureConsoleIfNotRedirected();
                return ShihabMcpAndCliHost.RunCliCommand(shihabStore, mehediCli.ShihabRawArgs);
            }

            // 6. Enable Per-Monitor V2 DPI Awareness on Windows 10/11 (with fallback to System DPI Aware)
            try
            {
                if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                {
                    SetProcessDPIAware();
                }
            }
            catch
            {
                try { SetProcessDPIAware(); } catch { }
            }

            ShihabThemePalette.EnableShihabProcessDarkScrollbars();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Tidy up any old .old portable executable or stale update staging files
            ShihabAutoUpdater.TidyOldUpdateArtifacts(shihabStore.MehediBaseDirectory);

            bool shihabCreatedNewMutex = false;
            string mehediMutexName = "Local\\ShihabMehediIDM_Instance_" + mehediConfig.MehediLocalBridgePort;
            Mutex shihabSingleMutex = new Mutex(true, mehediMutexName, out shihabCreatedNewMutex);
            try
            {
                // When relaunching immediately after an Auto-Update, wait up to 20s for the previous process to release Mutex
                if (!shihabCreatedNewMutex && !string.IsNullOrWhiteSpace(mehediCli.ShihabUpdatedFromVersion))
                {
                    for (int retry = 0; retry < 40 && !shihabCreatedNewMutex; retry++)
                    {
                        shihabSingleMutex.Dispose();
                        Thread.Sleep(500);
                        shihabSingleMutex = new Mutex(true, mehediMutexName, out shihabCreatedNewMutex);
                    }
                }

                if (!shihabCreatedNewMutex)
                {
                    if (mehediCli.ShihabHasActionableCommand)
                    {
                        MehediBrowserBridgeServer.ForwardCliToRunningShihabInstance(mehediCli, mehediConfig.MehediLocalBridgePort);
                    }
                    else
                    {
                        try { MehediBrowserBridgeServer.PostLocalShihabJson(mehediConfig.MehediLocalBridgePort, "/shihab/show", "{}", mehediCli.MehediCustomConfigDir); } catch { }
                    }
                    return 0;
                }

                ShihabCrashMonitor.InitializeInMainProcess(shihabStore.MehediBaseDirectory, mehediConfig.ShihabCrashMonitorEnabled);
                ShihabDnsAndNetworkManager.EnsureNetworkWatcherStarted();

                // Ensure bridge authentication token file exists before starting bridge
                shihabStore.LoadOrCreateBridgeToken();

                List<ShihabDownloadItem> mehediExistingItems = shihabStore.LoadMehediDownloads();
                MehediDynamicSegmentEngine shihabEngine = new MehediDynamicSegmentEngine(shihabStore, mehediConfig, mehediExistingItems);

                // 7. Headless Daemon Mode (--headless): runs engine, local bridge, remote Web UI, scheduler & RSS poller without GUI window
                if (mehediCli.ShihabRunHeadlessDaemon)
                {
                    using (MehediBrowserBridgeServer mehediBridge = new MehediBrowserBridgeServer(shihabStore, mehediConfig, shihabEngine))
                    {
                        ApplicationContext headlessCtx = new ApplicationContext();
                        mehediBridge.OnShihabGracefulExitRequested += delegate { headlessCtx.ExitThread(); };
                        mehediBridge.StartShihabBridge();
                        shihabEngine.StartStartupQueues();

                        using (ShihabRemoteControlServer remoteServer = new ShihabRemoteControlServer(
                            shihabStore,
                            mehediConfig,
                            shihabEngine))
                        using (System.Windows.Forms.Timer tickTimer = new System.Windows.Forms.Timer())
                        {
                            if (mehediConfig.ShihabRemoteWebEnabled) remoteServer.Start();

                            tickTimer.Interval = 5000;
                            int rssTickCounter = 0;
                            int rssPollingFlag = 0;
                            int updTickCounter = 4319;
                            int updPollingFlag = 0;
                            tickTimer.Tick += delegate
                            {
                                shihabEngine.TickMehediQueuesAndScheduler();
                                updTickCounter++;
                                if (mehediConfig.ShihabAutoCheckUpdates &&
                                    updTickCounter >= 4320 &&
                                    Interlocked.CompareExchange(ref updPollingFlag, 1, 0) == 0)
                                {
                                    updTickCounter = 0;
                                    Task.Run(delegate
                                    {
                                        try
                                        {
                                            ShihabAutoUpdater.CheckForUpdates(mehediConfig, false);
                                        }
                                        catch { }
                                        finally
                                        {
                                            Interlocked.Exchange(ref updPollingFlag, 0);
                                        }
                                    });
                                }
                                rssTickCounter++;
                                if (rssTickCounter >= 12 && Interlocked.CompareExchange(ref rssPollingFlag, 1, 0) == 0)
                                {
                                    rssTickCounter = 0;
                                    Task.Run(delegate
                                    {
                                        try
                                        {
                                            bool dirty = ShihabRssFeedManager.PollDueFeedsInBackground(
                                                mehediConfig,
                                                delegate(ShihabRssFeedConfig sub, ShihabRssEntryItem entry)
                                                {
                                                    string fName = ShihabFormatHelpers.SanitizeShihabFileName(
                                                        string.IsNullOrWhiteSpace(entry.Title) ? ShihabFormatHelpers.ExtractMehediFileNameFromUrl(entry.DownloadUrl) : entry.Title);
                                                    ShihabCategoryRule cat = mehediConfig.ResolveMehediCategoryForFile(fName);
                                                    string saveDir = !string.IsNullOrWhiteSpace(sub.ShihabSaveDirectory) ? sub.ShihabSaveDirectory : cat.ShihabDefaultFolder;
                                                    if (string.IsNullOrWhiteSpace(saveDir))
                                                    {
                                                        saveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                                                    }
                                                    ShihabDownloadItem rssIt = new ShihabDownloadItem
                                                    {
                                                        MehediUrl = entry.DownloadUrl,
                                                        ShihabFileName = fName,
                                                        MehediSaveDirectory = saveDir,
                                                        ShihabCategoryName = !string.IsNullOrWhiteSpace(sub.MehediCategoryName) ? sub.MehediCategoryName : cat.ShihabName
                                                    };
                                                    if (entry.DownloadUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
                                                        entry.DownloadUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        rssIt.ShihabIsTorrent = true;
                                                        rssIt.ShihabTorrentMagnetOrPath = entry.DownloadUrl;
                                                    }
                                                    shihabEngine.AddShihabDownloadItem(rssIt, true);
                                                });
                                            if (dirty) shihabStore.SaveMehediConfig(mehediConfig);
                                        }
                                        finally
                                        {
                                            Interlocked.Exchange(ref rssPollingFlag, 0);
                                        }
                                    });
                                }
                            };
                            tickTimer.Start();

                            Application.Run(headlessCtx);

                            tickTimer.Stop();
                            shihabEngine.PersistShihabState();
                        }
                    }
                    return 0;
                }

                // 8. Standard Desktop GUI Mode
                using (MehediBrowserBridgeServer mehediBridge = new MehediBrowserBridgeServer(shihabStore, mehediConfig, shihabEngine))
                using (ShihabMainIdmWindow shihabWindow = new ShihabMainIdmWindow(
                    shihabStore,
                    mehediConfig,
                    shihabEngine,
                    mehediBridge,
                    mehediCli))
                {
                    Application.Run(shihabWindow);
                }
            }
            finally
            {
                if (shihabSingleMutex != null)
                {
                    try { if (shihabCreatedNewMutex) shihabSingleMutex.ReleaseMutex(); } catch { }
                    try { shihabSingleMutex.Dispose(); } catch { }
                }
            }
            return 0;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("MHS Download Manager 1.0.3 (Mehedi Hasan Shihab)")]
[assembly: AssemblyDescription("MHS Download Manager 1.0.3 (Mehedi Hasan Shihab) — Multi-Segment Download Accelerator & Browser Video Suite")]
[assembly: AssemblyCompany("Mehedi Hasan Shihab (sshihabb007)")]
[assembly: AssemblyProduct("MHS Download Manager 1.0.3 (Mehedi Hasan Shihab)")]
[assembly: AssemblyCopyright("Copyright © Mehedi Hasan Shihab — https://sshihabb007.github.io/")]
[assembly: AssemblyVersion("1.0.3.0")]
[assembly: AssemblyFileVersion("1.0.3.0")]
[assembly: AssemblyInformationalVersion("1.0.3")]

namespace ShihabMehediIDM
{
    public static class MehediProgramEntry
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        public static int Main(string[] shihabRawArgs)
        {
            ShihabCliParsedArguments mehediCli = ShihabCliParsedArguments.ParseMehediArgs(shihabRawArgs);
            ShihabStateStore shihabStore = new ShihabStateStore(mehediCli.MehediCustomConfigDir);
            MehediAppConfiguration mehediConfig = shihabStore.LoadShihabConfig();

            if (mehediCli.ShihabRunAsNativeHost)
            {
                return MehediBrowserBridgeServer.RunShihabNativeMessagingStdioLoop(mehediConfig.MehediLocalBridgePort);
            }

            try { SetProcessDPIAware(); } catch { }
            ShihabThemePalette.EnableShihabProcessDarkScrollbars();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool shihabCreatedNewMutex;
            string mehediMutexName = "Local\\ShihabMehediIDM_Instance_" + mehediConfig.MehediLocalBridgePort;
            using (Mutex shihabSingleMutex = new Mutex(true, mehediMutexName, out shihabCreatedNewMutex))
            {
                if (!shihabCreatedNewMutex)
                {
                    if (mehediCli.ShihabHasActionableCommand)
                    {
                        MehediBrowserBridgeServer.ForwardCliToRunningShihabInstance(mehediCli, mehediConfig.MehediLocalBridgePort);
                    }
                    return 0;
                }

                List<ShihabDownloadItem> mehediExistingItems = shihabStore.LoadMehediDownloads();
                MehediDynamicSegmentEngine shihabEngine = new MehediDynamicSegmentEngine(shihabStore, mehediConfig, mehediExistingItems);
                using (MehediBrowserBridgeServer mehediBridge = new MehediBrowserBridgeServer(mehediConfig, shihabEngine))
                {
                    ShihabMainIdmWindow shihabWindow = new ShihabMainIdmWindow(
                        shihabStore,
                        mehediConfig,
                        shihabEngine,
                        mehediBridge,
                        mehediCli);
                    Application.Run(shihabWindow);
                }
            }
            return 0;
        }
    }
}


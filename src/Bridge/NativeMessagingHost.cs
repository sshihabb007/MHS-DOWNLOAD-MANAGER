using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public partial class MehediBrowserBridgeServer
    {
        private static readonly Regex ChromiumExtIdRegex = new Regex(
            @"^[a-p]{32}$",
            RegexOptions.CultureInvariant);

        private static readonly string[] ChromiumNativeHostRegistryKeys = new string[]
        {
            @"Software\Google\Chrome\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Google\Chrome Beta\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Google\Chrome Dev\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Google\Chrome SxS\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Chromium\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Microsoft\Edge\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Microsoft\Edge Beta\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Microsoft\Edge Dev\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Vivaldi\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Opera Software\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Opera Software\Opera GX Stable\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Helium\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Thorium\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Arc\NativeMessagingHosts\com.shihab.mehedi.idm"
        };

        private static readonly string[] FirefoxNativeHostRegistryKeys = new string[]
        {
            @"Software\Mozilla\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Zen\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\LibreWolf\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Floorp\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Waterfox\NativeMessagingHosts\com.shihab.mehedi.idm"
        };

        public static int RunShihabNativeMessagingStdioLoop(int mehediBridgePort)
        {
            return RunShihabNativeMessagingStdioLoop(mehediBridgePort, null);
        }

        public static int RunShihabNativeMessagingStdioLoop(int mehediBridgePort, string customConfigDir)
        {
            Stream shihabStdin = Console.OpenStandardInput();
            Stream mehediStdout = Console.OpenStandardOutput();
            byte[] shihabLenBuf = new byte[4];

            while (true)
            {
                int mehediReadHeader = ReadExactShihabBytes(shihabStdin, shihabLenBuf, 4);
                if (mehediReadHeader == 0)
                {
                    break;
                }
                if (mehediReadHeader < 4)
                {
                    return 1;
                }
                int shihabMsgLen = BitConverter.ToInt32(shihabLenBuf, 0);
                if (shihabMsgLen <= 0 || shihabMsgLen > 16 * 1024 * 1024)
                {
                    break;
                }
                byte[] mehediMsgBytes = new byte[shihabMsgLen];
                int shihabGot = ReadExactShihabBytes(shihabStdin, mehediMsgBytes, shihabMsgLen);
                if (shihabGot < shihabMsgLen)
                {
                    break;
                }

                string mehediInputJson = Encoding.UTF8.GetString(mehediMsgBytes);
                string shihabReplyJson = ProcessSingleMehediNativeMessage(mehediInputJson, mehediBridgePort, customConfigDir);
                byte[] mehediReplyBytes = Encoding.UTF8.GetBytes(shihabReplyJson);
                byte[] shihabOutLen = BitConverter.GetBytes(mehediReplyBytes.Length);
                mehediStdout.Write(shihabOutLen, 0, 4);
                mehediStdout.Write(mehediReplyBytes, 0, mehediReplyBytes.Length);
                mehediStdout.Flush();
            }
            return 0;
        }

        private static string ProcessSingleMehediNativeMessage(string shihabInputJson, int mehediPort, string customConfigDir)
        {
            try
            {
                ShihabNativeEnvelopeDto mehediEnv = DeserializeMehediJson<ShihabNativeEnvelopeDto>(shihabInputJson);
                string shihabAct = (mehediEnv != null && mehediEnv.ShihabAction != null)
                    ? mehediEnv.ShihabAction.Trim().ToLowerInvariant()
                    : "ping";

                if (shihabAct == "get-bridge-token")
                {
                    ShihabStateStore tokenStore = new ShihabStateStore(customConfigDir);
                    string token = tokenStore.LoadOrCreateBridgeToken();
                    return string.Format(
                        "{{\"ok\":true,\"port\":{0},\"token\":\"{1}\"}}",
                        mehediPort,
                        ShihabJsonCodec.EscapeString(token));
                }

                // Passive ping NEVER auto-launches mdm.exe (prevents update races and 35s hangs).
                if (shihabAct == "ping")
                {
                    return GetLocalMehediPing(mehediPort, 600);
                }

                if (shihabAct == "yt-streams" && mehediEnv != null && !string.IsNullOrWhiteSpace(mehediEnv.MehediVideoId))
                {
                    return SerializeShihabJson(MehediYouTubeInnerTubeResolver.ResolveShihabYouTubeStreams(
                        mehediEnv.MehediVideoId,
                        mehediEnv.ShihabVisitorData,
                        false));
                }

                if (IsNamedMutexActive("Local\\ShihabMehediIDM_InstallerActive"))
                {
                    return SerializeShihabJson(BuildShihabErrorReply("MHS Download Manager is updating; please retry in a moment."));
                }

                EnsureShihabDesktopAppRunning(mehediPort);

                if (shihabAct == "capture" && mehediEnv != null && mehediEnv.MehediCapture != null)
                {
                    return PostLocalShihabJson(mehediPort, "/shihab/capture", SerializeShihabJson(mehediEnv.MehediCapture), customConfigDir);
                }
                if (shihabAct == "capture-batch" && mehediEnv != null && mehediEnv.ShihabBatch != null)
                {
                    return PostLocalShihabJson(mehediPort, "/shihab/capture-batch", SerializeShihabJson(mehediEnv.ShihabBatch), customConfigDir);
                }
                if (shihabAct == "show")
                {
                    return PostLocalShihabJson(mehediPort, "/shihab/show", "{}", customConfigDir);
                }
                if (shihabAct == "open-updater")
                {
                    return PostLocalShihabJson(mehediPort, "/shihab/open-updater", "{}", customConfigDir);
                }
                return GetLocalMehediPing(mehediPort, 800);
            }
            catch (Exception shihabEx)
            {
                return SerializeShihabJson(BuildShihabErrorReply("NativeHost error: " + shihabEx.Message));
            }
        }

        private static bool IsNamedMutexActive(string mutexName)
        {
            try
            {
                Mutex existing;
                if (Mutex.TryOpenExisting(mutexName, MutexRights.Synchronize, out existing))
                {
                    try { existing.Dispose(); } catch { }
                    return true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Exists and is owned by an elevated process (e.g., elevated Setup running in Program Files)
                return true;
            }
            catch { }
            return false;
        }

        private static void EnsureShihabDesktopAppRunning(int mehediPort)
        {
            try
            {
                GetLocalMehediPing(mehediPort, 500);
                return;
            }
            catch
            {
            }
            // Never launch mdm.exe while an in-place Setup update is running.
            if (IsNamedMutexActive("Local\\ShihabMehediIDM_InstallerActive"))
            {
                return;
            }
            try
            {
                string instanceMutexName = "Local\\ShihabMehediIDM_Instance_" + mehediPort;
                if (!IsNamedMutexActive(instanceMutexName))
                {
                    string shihabExePath = ShihabProcessAndPathSafety.CurrentExePath;
                    ProcessStartInfo mehediPsi = new ProcessStartInfo(shihabExePath, "/fromhost");
                    mehediPsi.WorkingDirectory = Path.GetDirectoryName(shihabExePath);
                    // UseShellExecute = true ensures zero handle inheritance (detaches Chrome stdio pipes and Job Object).
                    mehediPsi.UseShellExecute = true;
                    using (Process launched = Process.Start(mehediPsi)) { }
                }
                Stopwatch sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 3200)
                {
                    Thread.Sleep(200);
                    try
                    {
                        GetLocalMehediPing(mehediPort, 400);
                        return;
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static string RegisterShihabNativeMessagingHostForCurrentUser(string mehediExtensionId, string shihabDataDir)
        {
            string mehediCleanId = (mehediExtensionId ?? "").Trim();
            if (mehediCleanId.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
            {
                mehediCleanId = mehediCleanId.Substring("chrome-extension://".Length);
            }
            mehediCleanId = mehediCleanId.TrimEnd('/').ToLowerInvariant();
            if (!ChromiumExtIdRegex.IsMatch(mehediCleanId))
            {
                throw new ArgumentException("Please enter a valid Chromium Extension ID (32 lowercase letters a-p).");
            }

            Directory.CreateDirectory(shihabDataDir);
            string shihabExePath = ShihabProcessAndPathSafety.CurrentExePath;
            string mehediWrapperBat = Path.Combine(shihabDataDir, "shihab_native_host_launcher.cmd");
            string batchSafeExe = "\"" + (shihabExePath ?? "").Replace("\"", "").Replace("%", "%%") + "\"";
            File.WriteAllText(
                mehediWrapperBat,
                "@echo off\r\n" + batchSafeExe + " --shihab-native-messaging-host\r\n",
                Encoding.ASCII);
            ShihabSecretsVault.ApplyRestrictedUserFileAcl(mehediWrapperBat);

            string shihabManifestPath = Path.Combine(shihabDataDir, "com.shihab.mehedi.idm.json");
            HashSet<string> allowedIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            allowedIdSet.Add(mehediCleanId);
            try
            {
                string exeDir = Path.GetDirectoryName(shihabExePath);
                string bundledExtDir = !string.IsNullOrEmpty(exeDir) ? Path.Combine(exeDir, "extension") : "";
                if (!string.IsNullOrEmpty(bundledExtDir) && Directory.Exists(bundledExtDir))
                {
                    string bundledId = ShihabProcessAndPathSafety.ComputeChromiumUnpackedExtensionId(bundledExtDir);
                    if (ChromiumExtIdRegex.IsMatch(bundledId)) allowedIdSet.Add(bundledId);
                }
            }
            catch { }
            try
            {
                if (File.Exists(shihabManifestPath))
                {
                    string prevRaw = File.ReadAllText(shihabManifestPath, Encoding.UTF8);
                    Dictionary<string, object> prevObj = ShihabJsonCodec.ParseObjectOrNull(prevRaw);
                    if (prevObj != null)
                    {
                        List<string> prevOrigins = ShihabJsonCodec.GetStringArray(prevObj, "allowed_origins");
                        for (int oi = 0; oi < prevOrigins.Count; oi++)
                        {
                            string oe = (prevOrigins[oi] ?? "").Trim();
                            if (oe.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
                            {
                                string oid = oe.Substring("chrome-extension://".Length).TrimEnd('/').ToLowerInvariant();
                                if (ChromiumExtIdRegex.IsMatch(oid)) allowedIdSet.Add(oid);
                            }
                        }
                    }
                }
            }
            catch { }

            StringBuilder originsJson = new StringBuilder();
            int originIdx = 0;
            foreach (string oid in allowedIdSet)
            {
                if (originIdx > 0) originsJson.Append(",\r\n");
                originsJson.Append("    \"chrome-extension://").Append(oid).Append("/\"");
                originIdx++;
            }

            string mehediEscapedBat = ShihabJsonCodec.EscapeString(mehediWrapperBat);
            string shihabManifestJson =
                "{\r\n" +
                "  \"name\": \"com.shihab.mehedi.idm\",\r\n" +
                "  \"description\": \"MHS Download Manager Native Messaging Host (mdm.exe)\",\r\n" +
                "  \"path\": \"" + mehediEscapedBat + "\",\r\n" +
                "  \"type\": \"stdio\",\r\n" +
                "  \"allowed_origins\": [\r\n" +
                originsJson.ToString() + "\r\n" +
                "  ]\r\n" +
                "}\r\n";
            File.WriteAllText(shihabManifestPath, shihabManifestJson, new UTF8Encoding(false));
            ShihabSecretsVault.ApplyRestrictedUserFileAcl(shihabManifestPath);
            WriteNativeHostRegistryKeys(ChromiumNativeHostRegistryKeys, shihabManifestPath);

            string shihabFirefoxManifestPath = Path.Combine(shihabDataDir, "com.shihab.mehedi.idm.firefox.json");
            string shihabFirefoxManifestJson =
                "{\r\n" +
                "  \"name\": \"com.shihab.mehedi.idm\",\r\n" +
                "  \"description\": \"MHS Download Manager Native Messaging Host (mdm.exe)\",\r\n" +
                "  \"path\": \"" + mehediEscapedBat + "\",\r\n" +
                "  \"type\": \"stdio\",\r\n" +
                "  \"allowed_extensions\": [\r\n" +
                "    \"idm@shihab.mehedi\"\r\n" +
                "  ]\r\n" +
                "}\r\n";
            File.WriteAllText(shihabFirefoxManifestPath, shihabFirefoxManifestJson, new UTF8Encoding(false));
            ShihabSecretsVault.ApplyRestrictedUserFileAcl(shihabFirefoxManifestPath);
            WriteNativeHostRegistryKeys(FirefoxNativeHostRegistryKeys, shihabFirefoxManifestPath);

            return shihabManifestPath;
        }

        /// <summary>
        /// Idempotently ensures per-user HKCU Native Messaging Host registration exists for Portable builds
        /// and extra Chromium channels (Beta/Canary/Dev/Opera GX) when no valid HKLM registration already points
        /// to the running mdm.exe. Preserves all previously registered allowed_origins.
        /// </summary>
        public static void EnsureCurrentUserNativeMessagingHostRegisteredIfNeeded(string shihabDataDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(shihabDataDir)) return;
                string shihabExePath = ShihabProcessAndPathSafety.CurrentExePath;
                if (string.IsNullOrWhiteSpace(shihabExePath) || !File.Exists(shihabExePath)) return;
                string exeDir = Path.GetDirectoryName(shihabExePath);

                // If HKLM already has a valid manifest pointing to this install directory (standard Setup install),
                // ensure extra Chromium channels (Beta/Canary/Dev/Opera GX) in HKCU also know about it without shadowing HKLM.
                string hklmManifest = Path.Combine(exeDir, "com.shihab.mehedi.idm.json");
                if (File.Exists(hklmManifest))
                {
                    return;
                }

                string extId = "";
                string bundledExtDir = !string.IsNullOrEmpty(exeDir) ? Path.Combine(exeDir, "extension") : "";
                if (!string.IsNullOrEmpty(bundledExtDir) && Directory.Exists(bundledExtDir))
                {
                    extId = ShihabProcessAndPathSafety.ComputeChromiumUnpackedExtensionId(bundledExtDir);
                }

                string existingManifest = Path.Combine(shihabDataDir, "com.shihab.mehedi.idm.json");
                if (string.IsNullOrEmpty(extId) && File.Exists(existingManifest))
                {
                    try
                    {
                        string prevRaw = File.ReadAllText(existingManifest, Encoding.UTF8);
                        Dictionary<string, object> prevObj = ShihabJsonCodec.ParseObjectOrNull(prevRaw);
                        if (prevObj != null)
                        {
                            List<string> prevOrigins = ShihabJsonCodec.GetStringArray(prevObj, "allowed_origins");
                            for (int i = 0; i < prevOrigins.Count; i++)
                            {
                                string oe = (prevOrigins[i] ?? "").Trim();
                                if (oe.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
                                {
                                    string oid = oe.Substring("chrome-extension://".Length).TrimEnd('/').ToLowerInvariant();
                                    if (ChromiumExtIdRegex.IsMatch(oid))
                                    {
                                        extId = oid;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(extId) && ChromiumExtIdRegex.IsMatch(extId))
                {
                    string launcherPath = Path.Combine(shihabDataDir, "shihab_native_host_launcher.cmd");
                    bool needsWrite = !File.Exists(existingManifest) || !File.Exists(launcherPath);
                    if (!needsWrite)
                    {
                        try
                        {
                            string cmdText = File.ReadAllText(launcherPath, Encoding.ASCII);
                            if (cmdText.IndexOf(shihabExePath, StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                needsWrite = true;
                            }
                        }
                        catch { needsWrite = true; }
                    }
                    if (needsWrite)
                    {
                        RegisterShihabNativeMessagingHostForCurrentUser(extId, shihabDataDir);
                    }
                }
            }
            catch { }
        }

        private static void WriteNativeHostRegistryKeys(string[] regKeys, string manifestPath)
        {
            for (int i = 0; i < regKeys.Length; i++)
            {
                using (RegistryKey mehediKey = Registry.CurrentUser.CreateSubKey(regKeys[i]))
                {
                    if (mehediKey != null)
                    {
                        mehediKey.SetValue("", manifestPath, RegistryValueKind.String);
                    }
                }
            }
        }
    }
}


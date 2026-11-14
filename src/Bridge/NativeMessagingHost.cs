using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly string[] ChromiumNativeHostRegistryKeys = new string[]
        {
            @"Software\Google\Chrome\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Chromium\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Microsoft\Edge\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Vivaldi\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Opera Software\NativeMessagingHosts\com.shihab.mehedi.idm",
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

                EnsureShihabDesktopAppRunning(mehediPort);

                if (shihabAct == "yt-streams" && mehediEnv != null && !string.IsNullOrWhiteSpace(mehediEnv.MehediVideoId))
                {
                    return SerializeShihabJson(MehediYouTubeInnerTubeResolver.ResolveShihabYouTubeStreams(
                        mehediEnv.MehediVideoId,
                        mehediEnv.ShihabVisitorData,
                        false));
                }
                if (shihabAct == "capture" && mehediEnv != null && mehediEnv.MehediCapture != null)
                {
                    return PostLocalShihabJson(mehediPort, "/shihab/capture", SerializeShihabJson(mehediEnv.MehediCapture), customConfigDir);
                }
                if (shihabAct == "capture-batch" && mehediEnv != null && mehediEnv.ShihabBatch != null)
                {
                    return PostLocalShihabJson(mehediPort, "/shihab/capture-batch", SerializeShihabJson(mehediEnv.ShihabBatch), customConfigDir);
                }
                return GetLocalMehediPing(mehediPort);
            }
            catch (Exception shihabEx)
            {
                return SerializeShihabJson(BuildShihabErrorReply("NativeHost error: " + shihabEx.Message));
            }
        }

        private static void EnsureShihabDesktopAppRunning(int mehediPort)
        {
            try
            {
                GetLocalMehediPing(mehediPort);
                return;
            }
            catch
            {
            }
            try
            {
                string shihabExePath = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo mehediPsi = new ProcessStartInfo(shihabExePath);
                mehediPsi.UseShellExecute = false;
                Process.Start(mehediPsi);
                for (int shihabWait = 0; shihabWait < 15; shihabWait++)
                {
                    Thread.Sleep(200);
                    try
                    {
                        GetLocalMehediPing(mehediPort);
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
            string shihabExePath = Path.GetFullPath(Process.GetCurrentProcess().MainModule.FileName);
            string legacyWrapperCmd = Path.Combine(shihabDataDir, "shihab_native_host_launcher.cmd");
            try
            {
                if (File.Exists(legacyWrapperCmd))
                {
                    File.Delete(legacyWrapperCmd);
                }
            }
            catch { }

            string shihabManifestPath = Path.Combine(shihabDataDir, "com.shihab.mehedi.idm.json");
            string mehediEscapedExe = ShihabJsonCodec.EscapeString(shihabExePath);
            string shihabManifestJson =
                "{\r\n" +
                "  \"name\": \"com.shihab.mehedi.idm\",\r\n" +
                "  \"description\": \"MHS Download Manager Native Messaging Host (mdm.exe)\",\r\n" +
                "  \"path\": \"" + mehediEscapedExe + "\",\r\n" +
                "  \"type\": \"stdio\",\r\n" +
                "  \"allowed_origins\": [\r\n" +
                "    \"chrome-extension://" + mehediCleanId + "/\"\r\n" +
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
                "  \"path\": \"" + mehediEscapedExe + "\",\r\n" +
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


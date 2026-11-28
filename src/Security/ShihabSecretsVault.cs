using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public static class ShihabSecretsVault
    {
        private const string DpapiNativePrefix = "dpapi2:";
        private const string AesV3Prefix = "aes3:";
        private const string AesGcmLikePrefix = "aes2:";
        private const string LegacyDpapiPrefix = "dpapi:";
        private static readonly byte[] VaultEntropy = Encoding.UTF8.GetBytes("MHS.DownloadManager.ShihabSecretsVault.v2");
        private static readonly object FallbackKeyLock = new object();
        private static readonly SecurityIdentifier CurrentUserSid = ResolveCurrentUserSid();
        private static readonly SecurityIdentifier LocalSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        private static SecurityIdentifier ResolveCurrentUserSid()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    if (id != null && id.User != null)
                    {
                        return id.User;
                    }
                }
            }
            catch { }
            return null;
        }

        internal static string GenerateSecureRandomHexToken(int byteCount)
        {
            int count = byteCount > 0 ? byteCount : 32;
            byte[] raw = new byte[count];
            try
            {
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(raw);
                }
                const string digits = "0123456789abcdef";
                char[] hex = new char[count * 2];
                for (int i = 0; i < count; i++)
                {
                    hex[i * 2] = digits[(raw[i] >> 4) & 0x0F];
                    hex[i * 2 + 1] = digits[raw[i] & 0x0F];
                }
                return new string(hex);
            }
            finally
            {
                ZeroSensitiveBuffer(raw);
            }
        }

        internal static void ZeroSensitiveBuffer(byte[] buffer)
        {
            if (buffer != null && buffer.Length > 0)
            {
                Array.Clear(buffer, 0, buffer.Length);
            }
        }

        public static string ProtectSecret(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return "";
            }
            if (plainText.StartsWith(DpapiNativePrefix, StringComparison.Ordinal) ||
                plainText.StartsWith(AesV3Prefix, StringComparison.Ordinal))
            {
                return plainText;
            }
            if (plainText.StartsWith(AesGcmLikePrefix, StringComparison.Ordinal) ||
                plainText.StartsWith(LegacyDpapiPrefix, StringComparison.Ordinal))
            {
                string migrated = UnprotectSecret(plainText);
                if (!string.IsNullOrEmpty(migrated))
                {
                    plainText = migrated;
                }
                else
                {
                    return plainText;
                }
            }
            byte[] raw = null;
            try
            {
                raw = Encoding.UTF8.GetBytes(plainText);
                byte[] cipher = ProtectedData.Protect(raw, VaultEntropy, DataProtectionScope.CurrentUser);
                return DpapiNativePrefix + Convert.ToBase64String(cipher);
            }
            catch
            {
                byte[] fallbackRaw = null;
                try
                {
                    fallbackRaw = Encoding.UTF8.GetBytes(plainText);
                    return ProtectWithV3RandomIvAndHmac(fallbackRaw);
                }
                catch
                {
                    return "";
                }
                finally
                {
                    ZeroSensitiveBuffer(fallbackRaw);
                }
            }
            finally
            {
                ZeroSensitiveBuffer(raw);
            }
        }

        public static string UnprotectSecret(string storedValue)
        {
            if (string.IsNullOrEmpty(storedValue))
            {
                return "";
            }
            if (storedValue.StartsWith(DpapiNativePrefix, StringComparison.Ordinal))
            {
                byte[] plain = null;
                try
                {
                    string b64 = storedValue.Substring(DpapiNativePrefix.Length);
                    byte[] cipher = Convert.FromBase64String(b64);
                    plain = ProtectedData.Unprotect(cipher, VaultEntropy, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(plain);
                }
                catch
                {
                    return "";
                }
                finally
                {
                    ZeroSensitiveBuffer(plain);
                }
            }
            if (storedValue.StartsWith(AesV3Prefix, StringComparison.Ordinal))
            {
                return UnprotectV3RandomIvAndHmac(storedValue.Substring(AesV3Prefix.Length));
            }
            if (storedValue.StartsWith(AesGcmLikePrefix, StringComparison.Ordinal))
            {
                return UnprotectLegacyV2RandomIvAndHmac(storedValue.Substring(AesGcmLikePrefix.Length));
            }
            if (!storedValue.StartsWith(LegacyDpapiPrefix, StringComparison.Ordinal))
            {
                return storedValue;
            }
            byte[] legacyPlain = null;
            try
            {
                string b64 = storedValue.Substring(LegacyDpapiPrefix.Length);
                byte[] cipher = Convert.FromBase64String(b64);
                legacyPlain = TransformLegacySecret(cipher, false);
                return Encoding.UTF8.GetString(legacyPlain);
            }
            catch
            {
                return "";
            }
            finally
            {
                ZeroSensitiveBuffer(legacyPlain);
            }
        }

        private static string GetFallbackMasterKeyFilePath()
        {
            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ShihabMehediIDM");
            return Path.Combine(baseDir, "vault.key");
        }

        private static byte[] LoadOrCreateFallbackMasterKey(bool createIfMissing)
        {
            lock (FallbackKeyLock)
            {
                string keyPath = GetFallbackMasterKeyFilePath();
                try
                {
                    if (!ShihabProcessAndPathSafety.HasAnyReparsePointInPath(keyPath) && File.Exists(keyPath))
                    {
                        byte[] existing = File.ReadAllBytes(keyPath);
                        if (existing != null && existing.Length == 32)
                        {
                            return existing;
                        }
                        ZeroSensitiveBuffer(existing);
                    }
                }
                catch { }

                if (!createIfMissing)
                {
                    return null;
                }

                byte[] fresh = new byte[32];
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(fresh);
                }
                string tmpPath = keyPath + "." + Path.GetRandomFileName() + ".tmp";
                bool persisted = false;
                try
                {
                    string parent = Path.GetDirectoryName(keyPath);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }
                    if (!ShihabProcessAndPathSafety.HasAnyReparsePointInPath(keyPath))
                    {
                        using (FileStream fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            fs.Write(fresh, 0, fresh.Length);
                            fs.Flush(true);
                        }
                        ApplyRestrictedUserFileAcl(tmpPath);
                        ShihabProcessAndPathSafety.ReplaceFileAtomicallyWithRenameFallback(tmpPath, keyPath);
                        persisted = File.Exists(keyPath);
                    }
                }
                catch
                {
                    try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
                }
                if (!persisted)
                {
                    ZeroSensitiveBuffer(fresh);
                    return null;
                }
                return fresh;
            }
        }

        private static void DeriveV3DomainKeys(byte[] masterKey, out byte[] encKey, out byte[] macKey)
        {
            using (HMACSHA256 hmac = new HMACSHA256(masterKey))
            {
                encKey = hmac.ComputeHash(Encoding.UTF8.GetBytes("enc"));
                macKey = hmac.ComputeHash(Encoding.UTF8.GetBytes("mac"));
            }
        }

        // Legacy deterministic fallback key retained strictly for backward-compatible decryption of pre-existing aes2: blobs.
        private static byte[] DeriveLegacyV2FallbackMasterKey()
        {
            string seed = Environment.UserName + "|" + Environment.MachineName + "|" +
                          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "|MHS_VAULT_V3";
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
            }
        }

        private static string ProtectWithV3RandomIvAndHmac(byte[] plainBytes)
        {
            byte[] master = LoadOrCreateFallbackMasterKey(true);
            byte[] encKey = null;
            byte[] macKey = null;
            byte[] iv = new byte[16];
            try
            {
                if (master == null || master.Length != 32)
                {
                    return "";
                }
                DeriveV3DomainKeys(master, out encKey, out macKey);
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(iv);
                }

                byte[] cipherBytes;
                using (Aes aes = Aes.Create())
                {
                    aes.Key = encKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using (ICryptoTransform enc = aes.CreateEncryptor())
                    {
                        cipherBytes = enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    }
                }

                byte[] payload = new byte[16 + cipherBytes.Length];
                Buffer.BlockCopy(iv, 0, payload, 0, 16);
                Buffer.BlockCopy(cipherBytes, 0, payload, 16, cipherBytes.Length);

                byte[] mac;
                using (HMACSHA256 hmac = new HMACSHA256(macKey))
                {
                    mac = hmac.ComputeHash(payload);
                }

                byte[] envelope = new byte[32 + payload.Length];
                Buffer.BlockCopy(mac, 0, envelope, 0, 32);
                Buffer.BlockCopy(payload, 0, envelope, 32, payload.Length);
                return AesV3Prefix + Convert.ToBase64String(envelope);
            }
            finally
            {
                ZeroSensitiveBuffer(master);
                ZeroSensitiveBuffer(encKey);
                ZeroSensitiveBuffer(macKey);
                ZeroSensitiveBuffer(iv);
            }
        }

        private static string UnprotectV3RandomIvAndHmac(string b64)
        {
            byte[] master = null;
            byte[] encKey = null;
            byte[] macKey = null;
            byte[] plain = null;
            try
            {
                byte[] envelope = Convert.FromBase64String(b64);
                if (envelope.Length < 48)
                {
                    return "";
                }
                master = LoadOrCreateFallbackMasterKey(false);
                if (master == null || master.Length != 32)
                {
                    return "";
                }
                DeriveV3DomainKeys(master, out encKey, out macKey);

                byte[] expectedMac = new byte[32];
                Buffer.BlockCopy(envelope, 0, expectedMac, 0, 32);
                byte[] payload = new byte[envelope.Length - 32];
                Buffer.BlockCopy(envelope, 32, payload, 0, payload.Length);

                byte[] actualMac;
                using (HMACSHA256 hmac = new HMACSHA256(macKey))
                {
                    actualMac = hmac.ComputeHash(payload);
                }
                int diff = 0;
                for (int i = 0; i < 32; i++)
                {
                    diff |= (expectedMac[i] ^ actualMac[i]);
                }
                if (diff != 0)
                {
                    return "";
                }

                byte[] iv = new byte[16];
                Buffer.BlockCopy(payload, 0, iv, 0, 16);
                byte[] cipher = new byte[payload.Length - 16];
                Buffer.BlockCopy(payload, 16, cipher, 0, cipher.Length);
                using (Aes aes = Aes.Create())
                {
                    aes.Key = encKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using (ICryptoTransform dec = aes.CreateDecryptor())
                    {
                        plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
                        return Encoding.UTF8.GetString(plain);
                    }
                }
            }
            catch
            {
                return "";
            }
            finally
            {
                ZeroSensitiveBuffer(master);
                ZeroSensitiveBuffer(encKey);
                ZeroSensitiveBuffer(macKey);
                ZeroSensitiveBuffer(plain);
            }
        }

        private static string UnprotectLegacyV2RandomIvAndHmac(string b64)
        {
            byte[] master = null;
            byte[] plain = null;
            try
            {
                byte[] envelope = Convert.FromBase64String(b64);
                if (envelope.Length < 48)
                {
                    return "";
                }
                master = DeriveLegacyV2FallbackMasterKey();
                byte[] expectedMac = new byte[32];
                Buffer.BlockCopy(envelope, 0, expectedMac, 0, 32);
                byte[] payload = new byte[envelope.Length - 32];
                Buffer.BlockCopy(envelope, 32, payload, 0, payload.Length);

                byte[] actualMac;
                using (HMACSHA256 hmac = new HMACSHA256(master))
                {
                    actualMac = hmac.ComputeHash(payload);
                }
                int diff = 0;
                for (int i = 0; i < 32; i++)
                {
                    diff |= (expectedMac[i] ^ actualMac[i]);
                }
                if (diff != 0)
                {
                    return "";
                }

                byte[] iv = new byte[16];
                Buffer.BlockCopy(payload, 0, iv, 0, 16);
                byte[] cipher = new byte[payload.Length - 16];
                Buffer.BlockCopy(payload, 16, cipher, 0, cipher.Length);
                using (Aes aes = Aes.Create())
                {
                    aes.Key = master;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using (ICryptoTransform dec = aes.CreateDecryptor())
                    {
                        plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
                        return Encoding.UTF8.GetString(plain);
                    }
                }
            }
            catch
            {
                return "";
            }
            finally
            {
                ZeroSensitiveBuffer(master);
                ZeroSensitiveBuffer(plain);
            }
        }

        private static byte[] TransformLegacySecret(byte[] data, bool encrypt)
        {
            string seed = Environment.UserName + "|" + Environment.MachineName + "|" + Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            byte[] key = null;
            byte[] ivFull = null;
            byte[] iv = new byte[16];
            try
            {
                using (SHA256 sha = SHA256.Create())
                {
                    key = sha.ComputeHash(Encoding.UTF8.GetBytes(seed + "|MHS_VAULT_KEY"));
                    ivFull = sha.ComputeHash(VaultEntropy);
                    Buffer.BlockCopy(ivFull, 0, iv, 0, 16);
                    using (Aes aes = Aes.Create())
                    {
                        aes.Key = key;
                        aes.IV = iv;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        using (ICryptoTransform xform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor())
                        {
                            return xform.TransformFinalBlock(data, 0, data.Length);
                        }
                    }
                }
            }
            finally
            {
                ZeroSensitiveBuffer(key);
                ZeroSensitiveBuffer(ivFull);
                ZeroSensitiveBuffer(iv);
            }
        }

        public static string SealSecret(string plainText)
        {
            return ProtectSecret(plainText);
        }

        public static string UnsealSecret(string storedValue)
        {
            return UnprotectSecret(storedValue);
        }

        public static void SealConfigurationSecrets(MehediAppConfiguration cfg)
        {
            if (cfg == null) return;
            if (!string.IsNullOrEmpty(cfg.MehediProxyPassword))
            {
                cfg.MehediProxyPassword = ProtectSecret(cfg.MehediProxyPassword);
            }
            if (cfg.ShihabSiteLogins != null)
            {
                for (int i = 0; i < cfg.ShihabSiteLogins.Count; i++)
                {
                    if (cfg.ShihabSiteLogins[i] != null && !string.IsNullOrEmpty(cfg.ShihabSiteLogins[i].ShihabPassword))
                    {
                        cfg.ShihabSiteLogins[i].ShihabPassword = ProtectSecret(cfg.ShihabSiteLogins[i].ShihabPassword);
                    }
                }
            }
            if (cfg.ShihabDebridAccounts != null)
            {
                for (int i = 0; i < cfg.ShihabDebridAccounts.Count; i++)
                {
                    if (cfg.ShihabDebridAccounts[i] != null && !string.IsNullOrEmpty(cfg.ShihabDebridAccounts[i].MehediEncryptedApiToken))
                    {
                        cfg.ShihabDebridAccounts[i].MehediEncryptedApiToken = ProtectSecret(cfg.ShihabDebridAccounts[i].MehediEncryptedApiToken);
                    }
                }
            }
        }

        public static void UnsealConfigurationSecrets(MehediAppConfiguration cfg)
        {
            if (cfg == null) return;
            if (!string.IsNullOrEmpty(cfg.MehediProxyPassword))
            {
                cfg.MehediProxyPassword = UnprotectSecret(cfg.MehediProxyPassword);
            }
            if (cfg.ShihabSiteLogins != null)
            {
                for (int i = 0; i < cfg.ShihabSiteLogins.Count; i++)
                {
                    if (cfg.ShihabSiteLogins[i] != null && !string.IsNullOrEmpty(cfg.ShihabSiteLogins[i].ShihabPassword))
                    {
                        cfg.ShihabSiteLogins[i].ShihabPassword = UnprotectSecret(cfg.ShihabSiteLogins[i].ShihabPassword);
                    }
                }
            }
        }

        internal static void SealDownloadItemSecrets(ShihabDownloadItem item)
        {
            if (item == null) return;
            if (!string.IsNullOrEmpty(item.MehediAuthPassword))
            {
                item.MehediAuthPassword = ProtectSecret(item.MehediAuthPassword);
            }
            if (!string.IsNullOrEmpty(item.ShihabCookies))
            {
                item.ShihabCookies = ProtectSecret(item.ShihabCookies);
            }
            if (!string.IsNullOrEmpty(item.ShihabArchivePassword))
            {
                item.ShihabArchivePassword = ProtectSecret(item.ShihabArchivePassword);
            }
            if (!string.IsNullOrEmpty(item.ShihabPostBody))
            {
                item.ShihabPostBody = ProtectSecret(item.ShihabPostBody);
            }
        }

        internal static void UnsealDownloadItemSecrets(ShihabDownloadItem item)
        {
            if (item == null) return;
            if (!string.IsNullOrEmpty(item.MehediAuthPassword))
            {
                item.MehediAuthPassword = UnprotectSecret(item.MehediAuthPassword);
            }
            if (!string.IsNullOrEmpty(item.ShihabCookies))
            {
                item.ShihabCookies = UnprotectSecret(item.ShihabCookies);
            }
            if (!string.IsNullOrEmpty(item.ShihabArchivePassword))
            {
                item.ShihabArchivePassword = UnprotectSecret(item.ShihabArchivePassword);
            }
            if (!string.IsNullOrEmpty(item.ShihabPostBody))
            {
                item.ShihabPostBody = UnprotectSecret(item.ShihabPostBody);
            }
        }

        internal static void ApplyRestrictedUserFileAcl(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }
            try
            {
                SecurityIdentifier userSid = CurrentUserSid;
                if (userSid == null)
                {
                    return;
                }
                FileSecurity sec = new FileSecurity();
                sec.SetAccessRuleProtection(true, false);
                sec.AddAccessRule(new FileSystemAccessRule(
                    userSid,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));
                sec.AddAccessRule(new FileSystemAccessRule(
                    LocalSystemSid,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));
                File.SetAccessControl(filePath, sec);
            }
            catch
            {
            }
        }

        internal static void ApplyRestrictedUserDirectoryAcl(string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
            {
                return;
            }
            try
            {
                SecurityIdentifier userSid = CurrentUserSid;
                if (userSid == null)
                {
                    return;
                }
                DirectorySecurity sec = new DirectorySecurity();
                sec.SetAccessRuleProtection(true, false);
                sec.AddAccessRule(new FileSystemAccessRule(
                    userSid,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                sec.AddAccessRule(new FileSystemAccessRule(
                    LocalSystemSid,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                Directory.SetAccessControl(dirPath, sec);
            }
            catch
            {
            }
        }

        public static int ForgetAllSignIns(ShihabStateStore store)
        {
            if (store == null) return 0;
            int purged = 0;
            MehediAppConfiguration cfg = store.LoadShihabConfig();
            if (!string.IsNullOrEmpty(cfg.ShihabProxyUser) || !string.IsNullOrEmpty(cfg.MehediProxyPassword))
            {
                purged++;
            }
            cfg.ShihabProxyUser = "";
            cfg.MehediProxyPassword = "";
            if (cfg.ShihabSiteLogins != null)
            {
                purged += cfg.ShihabSiteLogins.Count;
                cfg.ShihabSiteLogins.Clear();
            }
            if (cfg.ShihabDebridAccounts != null)
            {
                for (int i = 0; i < cfg.ShihabDebridAccounts.Count; i++)
                {
                    if (!string.IsNullOrEmpty(cfg.ShihabDebridAccounts[i].MehediEncryptedApiToken)) purged++;
                    cfg.ShihabDebridAccounts[i].MehediEnabled = false;
                    cfg.ShihabDebridAccounts[i].MehediEncryptedApiToken = "";
                    cfg.ShihabDebridAccounts[i].ShihabAccountStatus = "Not signed in";
                }
            }
            if (cfg.ShihabPairedRemoteDevices != null)
            {
                purged += cfg.ShihabPairedRemoteDevices.Count;
                cfg.ShihabPairedRemoteDevices.Clear();
            }
            if (cfg.ShihabSavedArchivePasswords != null)
            {
                purged += cfg.ShihabSavedArchivePasswords.Count;
                cfg.ShihabSavedArchivePasswords.Clear();
            }
            cfg.ShihabLocalBridgeToken = GenerateSecureRandomHexToken(32);
            purged++;
            store.SaveMehediConfig(cfg);
            try
            {
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                bool changed = false;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!string.IsNullOrEmpty(items[i].MehediAuthPassword) ||
                        !string.IsNullOrEmpty(items[i].ShihabCookies) ||
                        !string.IsNullOrEmpty(items[i].ShihabArchivePassword) ||
                        !string.IsNullOrEmpty(items[i].ShihabPostBody))
                    {
                        items[i].ShihabAuthUser = "";
                        items[i].MehediAuthPassword = "";
                        items[i].ShihabCookies = "";
                        items[i].ShihabArchivePassword = "";
                        items[i].ShihabPostBody = "";
                        purged++;
                        changed = true;
                    }
                }
                if (changed) store.SaveShihabDownloads(items);
            }
            catch { }
            return purged;
        }
    }
}

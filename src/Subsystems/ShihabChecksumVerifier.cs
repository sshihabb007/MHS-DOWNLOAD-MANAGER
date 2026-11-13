using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ShihabMehediIDM
{
    public static class ShihabChecksumVerifier
    {
        private static readonly TimeSpan ChecksumRegexTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly Regex BsdChecksumRegex = new Regex(
            @"^(CRC32|MD5|SHA-?1|SHA-?256|SHA-?512)\s*\([^)]+\)\s*=\s*([0-9A-Fa-f]{8,128})$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            ChecksumRegexTimeout);

        private static readonly uint[] Crc32Table = BuildCrc32Table();

        private static uint[] BuildCrc32Table()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                {
                    if ((c & 1) != 0)
                        c = 0xEDB88320u ^ (c >> 1);
                    else
                        c >>= 1;
                }
                table[i] = c;
            }
            return table;
        }

        public static bool ParseExpectedChecksumSpec(string rawInput, out string algorithm, out string expectedHex)
        {
            algorithm = "SHA-256";
            expectedHex = "";
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return false;
            }
            string s = rawInput.Trim();

            try
            {
                Match bsdMatch = BsdChecksumRegex.Match(s);
                if (bsdMatch.Success)
                {
                    s = bsdMatch.Groups[1].Value + ":" + bsdMatch.Groups[2].Value;
                }
            }
            catch { }

            int colon = s.IndexOf(':');
            if (colon <= 0) colon = s.IndexOf('=');
            if (colon > 0)
            {
                string prefix = s.Substring(0, colon).Trim().ToUpperInvariant().Replace("-", "");
                string rest = s.Substring(colon + 1).Trim().Trim('"', '\'');
                int wsInRest = rest.IndexOfAny(new char[] { ' ', '\t' });
                if (wsInRest > 0) rest = rest.Substring(0, wsInRest);
                if (prefix == "CRC32" || prefix == "CRC") algorithm = "CRC32";
                else if (prefix == "MD5") algorithm = "MD5";
                else if (prefix == "SHA1") algorithm = "SHA-1";
                else if (prefix == "SHA256") algorithm = "SHA-256";
                else if (prefix == "SHA512") algorithm = "SHA-512";
                else return false;
                expectedHex = NormalizeHexOrBase64ToHex(rest, algorithm);
                return !string.IsNullOrEmpty(expectedHex);
            }

            string token = s.Trim('"', '\'');
            int wsIdx = token.IndexOfAny(new char[] { ' ', '\t' });
            if (wsIdx >= 8)
            {
                token = token.Substring(0, wsIdx);
            }

            string cleanHex = token.Replace(" ", "").ToLowerInvariant();
            for (int i = 0; i < cleanHex.Length; i++)
            {
                char c = cleanHex[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    return false;
                }
            }
            if (cleanHex.Length == 8) { algorithm = "CRC32"; expectedHex = cleanHex; return true; }
            if (cleanHex.Length == 32) { algorithm = "MD5"; expectedHex = cleanHex; return true; }
            if (cleanHex.Length == 40) { algorithm = "SHA-1"; expectedHex = cleanHex; return true; }
            if (cleanHex.Length == 64) { algorithm = "SHA-256"; expectedHex = cleanHex; return true; }
            if (cleanHex.Length == 128) { algorithm = "SHA-512"; expectedHex = cleanHex; return true; }
            return false;
        }

        private static string NormalizeHexOrBase64ToHex(string val, string algo)
        {
            if (string.IsNullOrWhiteSpace(val)) return "";
            string clean = val.Trim().Trim(':');
            bool isHex = true;
            for (int i = 0; i < clean.Length; i++)
            {
                char c = clean[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    isHex = false;
                    break;
                }
            }
            int expectedHexLen = 64;
            if (algo == "CRC32") expectedHexLen = 8;
            else if (algo == "MD5") expectedHexLen = 32;
            else if (algo == "SHA-1") expectedHexLen = 40;
            else if (algo == "SHA-512") expectedHexLen = 128;

            if (isHex && clean.Length == expectedHexLen)
            {
                return clean.ToLowerInvariant();
            }
            try
            {
                byte[] decoded = Convert.FromBase64String(clean);
                if (decoded.Length * 2 == expectedHexLen)
                {
                    StringBuilder sb = new StringBuilder(expectedHexLen);
                    for (int i = 0; i < decoded.Length; i++)
                    {
                        sb.Append(decoded[i].ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
            catch
            {
            }
            return "";
        }

        public static string ExtractServerDigestSpec(WebHeaderCollection headers, string etag)
        {
            return ExtractServerDigestSpec(headers, etag, 200);
        }

        public static string ExtractServerDigestSpec(WebHeaderCollection headers, string etag, int httpStatusCode)
        {
            if (headers != null)
            {
                string reprDigest = headers["Repr-Digest"] ?? headers["Digest"];
                if (!string.IsNullOrWhiteSpace(reprDigest) && httpStatusCode != 206)
                {
                    string[] parts = reprDigest.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    string[] priority = new string[] { "sha-512", "sha-256", "sha", "sha-1", "md5", "crc32" };
                    for (int p = 0; p < priority.Length; p++)
                    {
                        for (int i = 0; i < parts.Length; i++)
                        {
                            string token = parts[i].Trim();
                            int eq = token.IndexOf('=');
                            if (eq > 0)
                            {
                                string k = token.Substring(0, eq).Trim().ToLowerInvariant();
                                string v = token.Substring(eq + 1).Trim().Trim(':', '"', '\'');
                                if (k == priority[p])
                                {
                                    string algoName = "SHA-256";
                                    if (k == "sha-512") algoName = "SHA-512";
                                    else if (k == "sha-256") algoName = "SHA-256";
                                    else if (k == "sha" || k == "sha-1") algoName = "SHA-1";
                                    else if (k == "md5") algoName = "MD5";
                                    else if (k == "crc32") algoName = "CRC32";
                                    else continue;
                                    string hex = NormalizeHexOrBase64ToHex(v, algoName);
                                    if (!string.IsNullOrEmpty(hex))
                                    {
                                        return algoName.ToLowerInvariant().Replace("-", "") + ":" + hex;
                                    }
                                }
                            }
                        }
                    }
                }

                string contentMd5 = headers["Content-MD5"];
                if (!string.IsNullOrWhiteSpace(contentMd5) && httpStatusCode != 206)
                {
                    string hex = NormalizeHexOrBase64ToHex(contentMd5.Trim(), "MD5");
                    if (!string.IsNullOrEmpty(hex))
                    {
                        return "md5:" + hex;
                    }
                }

                string googHash = headers["x-goog-hash"];
                if (!string.IsNullOrWhiteSpace(googHash))
                {
                    string[] parts = googHash.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string t = parts[i].Trim();
                        if (t.StartsWith("md5=", StringComparison.OrdinalIgnoreCase))
                        {
                            string hex = NormalizeHexOrBase64ToHex(t.Substring(4).Trim(), "MD5");
                            if (!string.IsNullOrEmpty(hex)) return "md5:" + hex;
                        }
                    }
                }
            }

            if (httpStatusCode != 206 && !string.IsNullOrWhiteSpace(etag) && !etag.TrimStart().StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            {
                string cleanEtag = etag.Trim().Trim('"');
                if (cleanEtag.Length == 32 && cleanEtag.IndexOf('-') < 0)
                {
                    string algo;
                    string hex;
                    if (ParseExpectedChecksumSpec("md5:" + cleanEtag, out algo, out hex))
                    {
                        return "md5:" + hex;
                    }
                }
            }
            return "";
        }

        public static string ComputeStreamChecksumHex(Stream fs, string algorithm)
        {
            if (fs == null) return "";
            if (fs.CanSeek) fs.Seek(0, SeekOrigin.Begin);
            string algo = (algorithm ?? "SHA-256").Trim().ToUpperInvariant();
            if (algo == "CRC32" || algo == "CRC")
            {
                uint crc = 0xFFFFFFFFu;
                byte[] buf = new byte[65536];
                int read;
                while ((read = fs.Read(buf, 0, buf.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                    {
                        crc = Crc32Table[(crc ^ buf[i]) & 0xFF] ^ (crc >> 8);
                    }
                }
                crc ^= 0xFFFFFFFFu;
                return crc.ToString("x8");
            }

            HashAlgorithm hasher;
            if (algo == "MD5") hasher = MD5.Create();
            else if (algo == "SHA-1" || algo == "SHA1") hasher = SHA1.Create();
            else if (algo == "SHA-512" || algo == "SHA512") hasher = SHA512.Create();
            else hasher = SHA256.Create();

            using (hasher)
            {
                byte[] hash = hasher.ComputeHash(fs);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public static string ComputeFileChecksumHex(string filePath, string algorithm)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return "";
            }
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 131072))
            {
                return ComputeStreamChecksumHex(fs, algorithm);
            }
        }

        public static bool VerifyExpectedChecksum(
            string filePath,
            string expectedSpec,
            out string algorithm,
            out string computedHex,
            out string statusSummary)
        {
            algorithm = "SHA-256";
            computedHex = "";
            statusSummary = "";
            if (string.IsNullOrWhiteSpace(expectedSpec))
            {
                computedHex = ComputeFileChecksumHex(filePath, "SHA-256");
                statusSummary = "SHA-256 computed";
                return true;
            }
            string expHex;
            if (!ParseExpectedChecksumSpec(expectedSpec, out algorithm, out expHex))
            {
                computedHex = ComputeFileChecksumHex(filePath, "SHA-256");
                statusSummary = "Invalid checksum specification (rejected fail-closed): " + expectedSpec.Trim();
                return false;
            }
            computedHex = ComputeFileChecksumHex(filePath, algorithm);
            bool match = string.Equals(computedHex, expHex, StringComparison.OrdinalIgnoreCase);
            statusSummary = match
                ? string.Format("{0} Verified", algorithm)
                : string.Format("{0} MISMATCH (expected {1}, got {2})", algorithm, expHex, computedHex);
            return match;
        }

        public static bool VerifyExpectedChecksum(
            string filePath,
            string expectedSpec,
            out string computedFormatted,
            out string statusSummary)
        {
            return VerifyFileAgainstSpec(filePath, expectedSpec, out computedFormatted, out statusSummary);
        }

        public static bool VerifyFileAgainstSpec(
            string filePath,
            string expectedSpec,
            out string computedFormatted,
            out string statusSummary)
        {
            computedFormatted = "";
            statusSummary = "";
            if (string.IsNullOrWhiteSpace(expectedSpec))
            {
                string shaHex = ComputeFileChecksumHex(filePath, "SHA-256");
                computedFormatted = string.IsNullOrEmpty(shaHex) ? "" : ("sha256:" + shaHex);
                statusSummary = "SHA-256 computed";
                return true;
            }
            string algo;
            string expectedHex;
            if (!ParseExpectedChecksumSpec(expectedSpec, out algo, out expectedHex))
            {
                computedFormatted = "";
                statusSummary = "Invalid checksum specification (rejected fail-closed): " + expectedSpec.Trim();
                return false;
            }
            string actualHex = ComputeFileChecksumHex(filePath, algo);
            computedFormatted = algo.ToLowerInvariant().Replace("-", "") + ":" + actualHex;
            bool match = string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase);
            statusSummary = match
                ? string.Format("{0} Verified", algo)
                : string.Format("{0} MISMATCH (expected {1}, got {2})", algo, expectedHex, actualHex);
            return match;
        }
    }
}


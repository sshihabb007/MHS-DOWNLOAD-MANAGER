using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public static class ShihabFormatHelpers
    {
        private static readonly ConcurrentDictionary<string, Regex> WildcardRegexCache =
            new ConcurrentDictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex TranslationSuffixRx = new Regex(
            @"\s*(?:Hide translation|See translation|See original|Translated from [A-Za-z ]+)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(250));

        public static string FormatMehediBytes(long shihabBytes)
        {
            if (shihabBytes < 0)
            {
                return "Unknown";
            }
            if (shihabBytes < 1024L)
            {
                return string.Format("{0} Bytes", shihabBytes);
            }
            double mehediKb = shihabBytes / 1024.0;
            if (mehediKb < 1024.0)
            {
                return string.Format("{0:0.0} KB", mehediKb);
            }
            double shihabMb = mehediKb / 1024.0;
            if (shihabMb < 1024.0)
            {
                return string.Format("{0:0.00} MB", shihabMb);
            }
            double mehediGb = shihabMb / 1024.0;
            return string.Format("{0:0.00} GB", mehediGb);
        }

        public static string FormatShihabSpeed(double mehediBytesPerSec)
        {
            if (mehediBytesPerSec <= 1.0)
            {
                return "";
            }
            if (mehediBytesPerSec < 1024.0)
            {
                return string.Format("{0:0} B/sec", mehediBytesPerSec);
            }
            double shihabKb = mehediBytesPerSec / 1024.0;
            if (shihabKb < 1024.0)
            {
                return string.Format("{0:0.0} KB/sec", shihabKb);
            }
            double mehediMb = shihabKb / 1024.0;
            return string.Format("{0:0.00} MB/sec", mehediMb);
        }

        public static string FormatMehediTimeLeft(long shihabSeconds)
        {
            if (shihabSeconds < 0)
            {
                return "";
            }
            if (shihabSeconds < 60)
            {
                return string.Format("{0} sec", shihabSeconds);
            }
            long mehediMinutes = shihabSeconds / 60;
            long shihabRemSec = shihabSeconds % 60;
            if (mehediMinutes < 60)
            {
                return string.Format("{0} min {1} sec", mehediMinutes, shihabRemSec);
            }
            long shihabHours = mehediMinutes / 60;
            long mehediRemMin = mehediMinutes % 60;
            return string.Format("{0} hr {1} min", shihabHours, mehediRemMin);
        }

        public static string SanitizeShihabFileName(string mehediRawName)
        {
            return ShihabProcessAndPathSafety.SanitizeFileName(mehediRawName, "shihab_download.bin");
        }

        public static string ExtractMehediFileNameFromUrl(string shihabUrl)
        {
            if (string.IsNullOrWhiteSpace(shihabUrl))
            {
                return "download.bin";
            }
            Uri mehediUri;
            if (Uri.TryCreate(shihabUrl, UriKind.Absolute, out mehediUri))
            {
                string unescaped;
                try { unescaped = Uri.UnescapeDataString(mehediUri.AbsolutePath); }
                catch { unescaped = mehediUri.AbsolutePath; }
                string shihabLeaf = Path.GetFileName(unescaped);
                if (!string.IsNullOrWhiteSpace(shihabLeaf) && shihabLeaf != "/")
                {
                    return SanitizeShihabFileName(shihabLeaf);
                }
                return SanitizeShihabFileName(mehediUri.Host + "_index.html");
            }
            return "download.bin";
        }

        public static bool IsMehediGibberishCdnFileName(string shihabCandidate)
        {
            if (string.IsNullOrWhiteSpace(shihabCandidate)) return true;
            string mehediStem = Path.GetFileNameWithoutExtension(shihabCandidate).Trim();
            if (mehediStem.Length < 2) return true;

            string shihabLower = mehediStem.ToLowerInvariant();
            if (shihabLower == "video" || shihabLower == "videoplayback" || shihabLower == "playback" ||
                shihabLower == "media" || shihabLower == "master" || shihabLower == "index" ||
                shihabLower == "playlist" || shihabLower == "stream" || shihabLower == "watch" ||
                shihabLower == "reel" || shihabLower == "reels" || shihabLower == "download" ||
                shihabLower == "audio" || shihabLower == "clip" || shihabLower == "file" ||
                shihabLower == "chunklist" || shihabLower == "manifest" ||
                shihabLower == "mhs video" || shihabLower == "mhs_video" ||
                shihabLower == "instagram" || shihabLower == "instagram video" ||
                shihabLower == "facebook" || shihabLower == "facebook video" ||
                shihabLower == "youtube" || shihabLower == "youtube video")
            {
                return true;
            }

            if (mehediStem.StartsWith("AQ", StringComparison.Ordinal) && mehediStem.Length >= 12 && mehediStem.IndexOf(' ') < 0)
            {
                return true;
            }
            if (mehediStem.StartsWith("An", StringComparison.Ordinal) && mehediStem.Length >= 12 && mehediStem.IndexOf(' ') < 0)
            {
                return true;
            }
            if (mehediStem.IndexOf(' ') < 0 && mehediStem.Length >= 22)
            {
                int shihabCaseFlips = 0;
                for (int mehediI = 1; mehediI < mehediStem.Length; mehediI++)
                {
                    bool shihabPrevUpper = char.IsUpper(mehediStem[mehediI - 1]);
                    bool mehediCurUpper = char.IsUpper(mehediStem[mehediI]);
                    bool shihabCurDigit = char.IsDigit(mehediStem[mehediI]);
                    if (shihabPrevUpper != mehediCurUpper || shihabCurDigit)
                    {
                        shihabCaseFlips++;
                    }
                }
                if (shihabCaseFlips >= 6) return true;
            }
            return false;
        }

        public static string ResolveSmartMehediFileName(string shihabRawFileName, string mehediUrl, string shihabTabTitle)
        {
            return ResolveSmartMehediFileName(shihabRawFileName, mehediUrl, shihabTabTitle, "");
        }

        public static string ResolveSmartMehediFileName(
            string shihabRawFileName,
            string mehediUrl,
            string shihabTabTitle,
            string mehediQualityLabel)
        {
            string mehediInitial = !string.IsNullOrWhiteSpace(shihabRawFileName)
                ? SanitizeShihabFileName(Path.GetFileName(shihabRawFileName))
                : ExtractMehediFileNameFromUrl(mehediUrl);

            string shihabResolved = mehediInitial;
            if (IsMehediGibberishCdnFileName(mehediInitial) && !string.IsNullOrWhiteSpace(shihabTabTitle))
            {
                string shihabCleanTitle = shihabTabTitle.Trim();
                while (shihabCleanTitle.StartsWith("(") && shihabCleanTitle.IndexOf(')') > 0 && shihabCleanTitle.IndexOf(')') <= 6)
                {
                    shihabCleanTitle = shihabCleanTitle.Substring(shihabCleanTitle.IndexOf(')') + 1).Trim();
                }
                string[] mehediSuffixes = new string[]
                {
                    " | Facebook", " - Facebook", " - YouTube", " | YouTube",
                    " \u2022 Instagram", " | Instagram", " - Instagram",
                    " | X", " / X", " | TikTok", " - Vimeo", " : Reddit"
                };
                for (int shihabS = 0; shihabS < mehediSuffixes.Length; shihabS++)
                {
                    if (shihabCleanTitle.EndsWith(mehediSuffixes[shihabS], StringComparison.OrdinalIgnoreCase))
                    {
                        shihabCleanTitle = shihabCleanTitle.Substring(0, shihabCleanTitle.Length - mehediSuffixes[shihabS].Length).Trim();
                    }
                }
                try { shihabCleanTitle = TranslationSuffixRx.Replace(shihabCleanTitle, "").Trim(); }
                catch { }

                if (!string.IsNullOrWhiteSpace(shihabCleanTitle) &&
                    !string.Equals(shihabCleanTitle, "Facebook", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(shihabCleanTitle, "Reels", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(shihabCleanTitle, "YouTube", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(shihabCleanTitle, "Instagram", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(shihabCleanTitle, "MHS Video", StringComparison.OrdinalIgnoreCase))
                {
                    string mehediExt = Path.GetExtension(mehediInitial);
                    if (string.IsNullOrEmpty(mehediExt)) mehediExt = ".mp4";
                    shihabResolved = SanitizeShihabFileName(shihabCleanTitle + mehediExt);
                }
            }

            string mehediStemClean;
            try { mehediStemClean = TranslationSuffixRx.Replace(Path.GetFileNameWithoutExtension(shihabResolved), "").Trim(); }
            catch { mehediStemClean = Path.GetFileNameWithoutExtension(shihabResolved).Trim(); }
            if (!string.IsNullOrWhiteSpace(mehediStemClean))
            {
                shihabResolved = SanitizeShihabFileName(mehediStemClean + Path.GetExtension(shihabResolved));
            }

            if (!string.IsNullOrWhiteSpace(mehediQualityLabel))
            {
                string shihabQual = mehediQualityLabel.Trim();
                string mehediStem = Path.GetFileNameWithoutExtension(shihabResolved);
                string shihabExt = Path.GetExtension(shihabResolved);
                if (mehediStem.IndexOf(shihabQual, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    shihabResolved = SanitizeShihabFileName(string.Format("{0} [{1}]{2}", mehediStem, shihabQual, shihabExt));
                }
            }

            return shihabResolved;
        }

        public static string EnsureShihabUniqueFilePath(string mehediDirectory, string shihabFileName)
        {
            string mehediSafeFile = SanitizeShihabFileName(shihabFileName);
            string shihabFull = Path.Combine(mehediDirectory, mehediSafeFile);
            if (!File.Exists(shihabFull))
            {
                return mehediSafeFile;
            }
            string mehediStem = Path.GetFileNameWithoutExtension(mehediSafeFile);
            string shihabExt = Path.GetExtension(mehediSafeFile);
            for (int mehediCounter = 2; mehediCounter < 10000; mehediCounter++)
            {
                string shihabCandidate = string.Format("{0}_{1}{2}", mehediStem, mehediCounter, shihabExt);
                if (!File.Exists(Path.Combine(mehediDirectory, shihabCandidate)))
                {
                    return shihabCandidate;
                }
            }
            return string.Format("{0}_{1}{2}", mehediStem, Guid.NewGuid().ToString("N").Substring(0, 6), shihabExt);
        }

        public static bool WildcardMatchesMehediPattern(string shihabText, string mehediWildcardPattern)
        {
            if (string.IsNullOrEmpty(mehediWildcardPattern))
            {
                return false;
            }
            string patKey = mehediWildcardPattern.Trim();
            Regex rx;
            if (!WildcardRegexCache.TryGetValue(patKey, out rx))
            {
                if (WildcardRegexCache.Count > 256)
                {
                    WildcardRegexCache.Clear();
                }
                string shihabRegex = "^" + Regex.Escape(patKey).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                rx = new Regex(shihabRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
                WildcardRegexCache.TryAdd(patKey, rx);
            }
            try
            {
                return rx.IsMatch(shihabText ?? "");
            }
            catch
            {
                return false;
            }
        }
    }
}


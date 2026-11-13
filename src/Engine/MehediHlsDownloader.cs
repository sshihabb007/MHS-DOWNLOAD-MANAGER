using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public partial class MehediDynamicSegmentEngine
    {
        private const int MaxHlsPlaylistBytes = 16 * 1024 * 1024;
        private static readonly TimeSpan HlsRegexTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly Regex HlsUriAttrRegex = new Regex(@"URI=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, HlsRegexTimeout);
        private static readonly Regex HlsByteRangeAttrRegex = new Regex(@"BYTERANGE=""(\d+)(?:@(\d+))?""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, HlsRegexTimeout);
        private static readonly Regex HlsExtByteRangeRegex = new Regex(@"(\d+)(?:@(\d+))?", RegexOptions.Compiled | RegexOptions.CultureInvariant, HlsRegexTimeout);
        private static readonly Regex HlsGroupIdAttrRegex = new Regex(@"GROUP-ID=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, HlsRegexTimeout);
        private static readonly Regex HlsBandwidthAttrRegex = new Regex(@"BANDWIDTH=(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, HlsRegexTimeout);
        private static readonly Regex HlsAudioAttrRegex = new Regex(@"AUDIO=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, HlsRegexTimeout);

        private static bool IsSafeHlsSubresourceUri(Uri playlistBaseUri, Uri candidateUri)
        {
            if (candidateUri == null || !candidateUri.IsAbsoluteUri) return false;
            Uri validated;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(candidateUri.AbsoluteUri, false, out validated))
            {
                return false;
            }
            if (IsAlwaysBlockedMetadataOrInternalHost(validated))
            {
                return false;
            }
            if (playlistBaseUri != null && !IsSameHttpOrigin(playlistBaseUri, validated))
            {
                bool basePrivate = ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(playlistBaseUri.Host, true);
                if (!basePrivate && ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(validated.Host, true))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryParseHlsByteRangeValues(
            string lenText,
            bool hasOffset,
            string offsetText,
            long defaultOffset,
            out long start,
            out long end)
        {
            start = -1L;
            end = -1L;
            long len;
            if (!long.TryParse(lenText, NumberStyles.None, CultureInfo.InvariantCulture, out len) || len <= 0L)
            {
                return false;
            }
            long off = defaultOffset;
            if (hasOffset)
            {
                if (!long.TryParse(offsetText, NumberStyles.None, CultureInfo.InvariantCulture, out off) || off < 0L)
                {
                    return false;
                }
            }
            if (off > long.MaxValue - len)
            {
                return false;
            }
            start = off;
            end = (off + len) - 1L;
            return end >= start;
        }

        private void ExecuteMehediHlsStreamDownload(
            ShihabDownloadItem shihabItem,
            string mehediPlaylistUrl,
            string shihabTempDir,
            CancellationToken mehediToken,
            ActiveDownloadHandle handle)
        {
            string shihabManifestText = DownloadMehediText(mehediPlaylistUrl, shihabItem, mehediToken);
            if (mehediToken.IsCancellationRequested) return;
            if (shihabManifestText.IndexOf("#EXT-X-KEY", StringComparison.OrdinalIgnoreCase) >= 0 &&
                shihabManifestText.IndexOf("METHOD=NONE", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException("Protected/encrypted HLS stream cannot be downloaded (DRM/encryption measures are respected).");
            }

            string mehediActiveVideoPlaylistUrl = mehediPlaylistUrl;
            string shihabDiscoveredAudioPlaylistUrl = shihabItem.MehediAudioCompanionUrl ?? "";

            if (shihabManifestText.IndexOf("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string mehediMatchedAudioUri;
                string shihabBestVariant = SelectHighestBandwidthShihabVariant(shihabManifestText, mehediPlaylistUrl, out mehediMatchedAudioUri);
                if (!string.IsNullOrEmpty(shihabBestVariant))
                {
                    mehediActiveVideoPlaylistUrl = shihabBestVariant;
                }
                if (string.IsNullOrWhiteSpace(shihabDiscoveredAudioPlaylistUrl) && !string.IsNullOrEmpty(mehediMatchedAudioUri))
                {
                    shihabDiscoveredAudioPlaylistUrl = mehediMatchedAudioUri;
                }
            }

            bool mehediHasSeparateAudioHls = !string.IsNullOrWhiteSpace(shihabDiscoveredAudioPlaylistUrl) &&
                !string.Equals(shihabDiscoveredAudioPlaylistUrl, mehediActiveVideoPlaylistUrl, StringComparison.OrdinalIgnoreCase);

            string shihabVideoPartPath = Path.Combine(shihabTempDir, "hls_video_raw.part");
            DownloadMehediHlsMediaPlaylistToFile(mehediActiveVideoPlaylistUrl, shihabVideoPartPath, "HLS", shihabItem, true, mehediToken);
            if (mehediToken.IsCancellationRequested) return;

            ShihabMediaProbeInfo mehediVidProbe = MehediMediaMuxer.InspectShihabMediaFile(shihabVideoPartPath);
            if (mehediVidProbe.ShihabIsEncryptedDrm)
            {
                throw new InvalidOperationException("Protected/encrypted HLS fMP4 stream cannot be downloaded (DRM protections are respected).");
            }

            bool shihabIsFmp4Hls = mehediVidProbe.MehediContainerFormat == "mp4";
            if (shihabItem.ShihabFileName.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
                (shihabIsFmp4Hls && shihabItem.ShihabFileName.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)))
            {
                string mehediNewExt = shihabIsFmp4Hls ? ".mp4" : ".ts";
                shihabItem.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(
                    shihabItem.MehediSaveDirectory,
                    Path.GetFileNameWithoutExtension(shihabItem.ShihabFileName) + mehediNewExt);
            }

            string mehediTargetFull = shihabItem.GetShihabFullTargetPath();
            string targetDir = Path.GetDirectoryName(mehediTargetFull);
            if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);

            if (mehediHasSeparateAudioHls)
            {
                string shihabAudioPartPath = Path.Combine(shihabTempDir, "hls_audio_raw.part");
                DownloadMehediHlsMediaPlaylistToFile(shihabDiscoveredAudioPlaylistUrl, shihabAudioPartPath, "HLS Audio", shihabItem, false, mehediToken);
                if (mehediToken.IsCancellationRequested) return;

                ShihabMediaProbeInfo mehediAudProbe = MehediMediaMuxer.InspectShihabMediaFile(shihabAudioPartPath);
                if (shihabIsFmp4Hls && mehediAudProbe.MehediContainerFormat == "mp4")
                {
                    shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabRebuilding, "Merging HLS video + audio...");
                    NotifyShihabItemChanged(shihabItem);
                    mehediTargetFull = MehediMediaMuxer.MuxShihabVideoAndAudio(shihabVideoPartPath, shihabAudioPartPath, mehediTargetFull);
                    shihabItem.ShihabFileName = Path.GetFileName(mehediTargetFull);
                }
                else
                {
                    if (!TryAtomicMoveReplace(shihabVideoPartPath, mehediTargetFull))
                    {
                        throw new IOException("Unable to finalize HLS output: " + mehediTargetFull);
                    }
                }
            }
            else
            {
                if (!TryAtomicMoveReplace(shihabVideoPartPath, mehediTargetFull))
                {
                    throw new IOException("Unable to finalize HLS output: " + mehediTargetFull);
                }
            }

            ComputeShihabFileSha256AndSize(shihabItem, mehediTargetFull);
            if (_mehediConfig.ShihabWriteZoneIdentifier)
            {
                ShihabOsAndDiskSafety.WriteMarkOfTheWeb(mehediTargetFull, shihabItem.MehediUrl, shihabItem.MehediReferer);
            }

            try
            {
                if (Directory.Exists(shihabTempDir)) Directory.Delete(shihabTempDir, true);
            }
            catch { }

            CompleteActiveDownload(shihabItem, handle, "Complete");
        }

        private void DownloadMehediHlsMediaPlaylistToFile(
            string shihabMediaPlaylistUrl,
            string mehediOutputFilePath,
            string shihabLabelPrefix,
            ShihabDownloadItem mehediItem,
            bool shihabUpdateMainBytes,
            CancellationToken mehediToken)
        {
            string shihabManifestText = DownloadMehediText(shihabMediaPlaylistUrl, mehediItem, mehediToken);
            if (mehediToken.IsCancellationRequested) return;
            if (shihabManifestText.IndexOf("#EXT-X-KEY", StringComparison.OrdinalIgnoreCase) >= 0 &&
                shihabManifestText.IndexOf("METHOD=NONE", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException("Protected/encrypted HLS stream cannot be downloaded (DRM/encryption measures are respected).");
            }

            Uri mehediBaseUri;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(shihabMediaPlaylistUrl, false, out mehediBaseUri))
            {
                throw new SecurityException("Invalid HLS playlist URL.");
            }
            string[] shihabLines = shihabManifestText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> mehediSegmentUrls = new List<string>();
            List<long> shihabSegStarts = new List<long>();
            List<long> mehediSegEnds = new List<long>();

            long shihabNextRangeStart = -1L;
            long mehediNextRangeEnd = -1L;
            long shihabPrevByteRangeEnd = 0L;

            for (int shihabI = 0; shihabI < shihabLines.Length; shihabI++)
            {
                string mehediLine = shihabLines[shihabI].Trim();
                if (string.IsNullOrEmpty(mehediLine)) continue;

                if (mehediLine.StartsWith("#EXT-X-MAP:", StringComparison.OrdinalIgnoreCase))
                {
                    Match shihabMapUriMatch = HlsUriAttrRegex.Match(mehediLine);
                    if (shihabMapUriMatch.Success)
                    {
                        Uri mehediMapUri;
                        if (Uri.TryCreate(mehediBaseUri, shihabMapUriMatch.Groups[1].Value, out mehediMapUri) &&
                            IsSafeHlsSubresourceUri(mehediBaseUri, mehediMapUri))
                        {
                            long shihabMapStart = -1L, mehediMapEnd = -1L;
                            Match shihabMapRange = HlsByteRangeAttrRegex.Match(mehediLine);
                            if (shihabMapRange.Success)
                            {
                                TryParseHlsByteRangeValues(
                                    shihabMapRange.Groups[1].Value,
                                    shihabMapRange.Groups[2].Success,
                                    shihabMapRange.Groups[2].Value,
                                    0L,
                                    out shihabMapStart,
                                    out mehediMapEnd);
                            }
                            mehediSegmentUrls.Add(mehediMapUri.AbsoluteUri);
                            shihabSegStarts.Add(shihabMapStart);
                            mehediSegEnds.Add(mehediMapEnd);
                        }
                    }
                    continue;
                }

                if (mehediLine.StartsWith("#EXT-X-BYTERANGE:", StringComparison.OrdinalIgnoreCase))
                {
                    Match shihabBr = HlsExtByteRangeRegex.Match(mehediLine);
                    if (shihabBr.Success)
                    {
                        long pStart, pEnd;
                        if (TryParseHlsByteRangeValues(
                            shihabBr.Groups[1].Value,
                            shihabBr.Groups[2].Success,
                            shihabBr.Groups[2].Value,
                            shihabPrevByteRangeEnd,
                            out pStart,
                            out pEnd))
                        {
                            shihabNextRangeStart = pStart;
                            mehediNextRangeEnd = pEnd;
                            shihabPrevByteRangeEnd = (pEnd < long.MaxValue) ? (pEnd + 1L) : pEnd;
                        }
                    }
                    continue;
                }

                if (mehediLine.StartsWith("#")) continue;

                Uri shihabResolved;
                if (Uri.TryCreate(mehediBaseUri, mehediLine, out shihabResolved))
                {
                    if (!IsSafeHlsSubresourceUri(mehediBaseUri, shihabResolved))
                    {
                        throw new SecurityException("Blocked unsafe HLS segment URI scheme or internal target.");
                    }
                    mehediSegmentUrls.Add(shihabResolved.AbsoluteUri);
                    shihabSegStarts.Add(shihabNextRangeStart);
                    mehediSegEnds.Add(mehediNextRangeEnd);
                    shihabNextRangeStart = -1L;
                    mehediNextRangeEnd = -1L;
                }
            }

            if (mehediSegmentUrls.Count == 0)
            {
                throw new IOException("No media segments found in M3U8 playlist.");
            }

            mehediItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabDownloading, string.Format("{0} 0/{1}", shihabLabelPrefix, mehediSegmentUrls.Count));
            NotifyShihabItemChanged(mehediItem);

            string outDir = Path.GetDirectoryName(mehediOutputFilePath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
            string tempHlsOutPath = mehediOutputFilePath + ".assembling";
            long shihabAccumulatedBytes = 0;
            Stopwatch hlsThrottleWatch = Stopwatch.StartNew();
            long hlsWinStartMs = 0L;
            long hlsWinBytes = 0L;

            try
            {
                using (FileStream shihabOut = new FileStream(tempHlsOutPath, FileMode.Create, FileAccess.Write, FileShare.None, SegmentStreamBufferBytes))
                {
                    byte[] mehediBuf = new byte[65536];
                    for (int mehediIdx = 0; mehediIdx < mehediSegmentUrls.Count; mehediIdx++)
                    {
                        if (mehediToken.IsCancellationRequested) return;
                        if (_mehediConfig.IsQuotaExceeded())
                        {
                            throw new IOException("Bandwidth quota limit reached.");
                        }
                        HttpWebRequest shihabSegReq;
                        using (HttpWebResponse mehediSegResp = GetResponseWithSafeRedirects(
                            mehediSegmentUrls[mehediIdx],
                            "GET",
                            mehediItem,
                            shihabSegStarts[mehediIdx],
                            mehediSegEnds[mehediIdx],
                            null,
                            mehediToken,
                            out shihabSegReq))
                        using (Stream shihabSegStream = mehediSegResp.GetResponseStream())
                        {
                            int shihabRead;
                            while (!mehediToken.IsCancellationRequested && (shihabRead = shihabSegStream.Read(mehediBuf, 0, mehediBuf.Length)) > 0)
                            {
                                shihabOut.Write(mehediBuf, 0, shihabRead);
                                _mehediConfig.RecordQuotaBytes(shihabRead);
                                shihabAccumulatedBytes += shihabRead;
                                ApplyMehediSpeedLimiter(mehediItem, shihabRead, hlsThrottleWatch, ref hlsWinStartMs, ref hlsWinBytes, mehediToken);
                            }
                        }
                        if (mehediToken.IsCancellationRequested) return;
                        if (shihabUpdateMainBytes)
                        {
                            mehediItem.MehediDownloadedBytes = shihabAccumulatedBytes;
                            mehediItem.ShihabTotalBytes = (long)((shihabAccumulatedBytes / (double)(mehediIdx + 1)) * mehediSegmentUrls.Count);
                        }
                        mehediItem.ShihabStatusText = string.Format("{0} {1}/{2}", shihabLabelPrefix, mehediIdx + 1, mehediSegmentUrls.Count);
                        NotifyShihabItemChanged(mehediItem);
                    }
                    shihabOut.Flush(true);
                }

                if (!mehediToken.IsCancellationRequested && !TryAtomicMoveReplace(tempHlsOutPath, mehediOutputFilePath))
                {
                    throw new IOException("Unable to move assembled HLS media file to: " + mehediOutputFilePath);
                }
            }
            catch
            {
                try { if (File.Exists(tempHlsOutPath)) File.Delete(tempHlsOutPath); } catch { }
                throw;
            }
        }

        private string DownloadMehediText(string shihabUrl, ShihabDownloadItem mehediItem, CancellationToken mehediToken)
        {
            HttpWebRequest shihabReq;
            using (HttpWebResponse mehediResp = GetResponseWithSafeRedirects(shihabUrl, "GET", mehediItem, -1, -1, null, mehediToken, out shihabReq))
            using (Stream respStream = mehediResp.GetResponseStream())
            {
                if (respStream == null) return "";
                byte[] buf = new byte[16384];
                using (MemoryStream ms = new MemoryStream())
                {
                    int total = 0;
                    while (total < MaxHlsPlaylistBytes && !mehediToken.IsCancellationRequested)
                    {
                        int toRead = Math.Min(buf.Length, MaxHlsPlaylistBytes - total);
                        int r = respStream.Read(buf, 0, toRead);
                        if (r <= 0) break;
                        ms.Write(buf, 0, r);
                        total += r;
                    }
                    return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                }
            }
        }

        private static string SelectHighestBandwidthShihabVariant(
            string mehediMasterText,
            string shihabMasterUrl,
            out string mehediMatchedAudioPlaylistUrl)
        {
            mehediMatchedAudioPlaylistUrl = "";
            string[] mehediLines = mehediMasterText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            long shihabBestBw = -1;
            string mehediBestUri = "";
            string shihabBestAudioGroup = "";
            Uri shihabBase;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(shihabMasterUrl, false, out shihabBase))
            {
                return "";
            }

            Dictionary<string, string> mehediAudioGroupUris = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int shihabI = 0; shihabI < mehediLines.Length; shihabI++)
            {
                string mehediLine = mehediLines[shihabI].Trim();
                if (mehediLine.StartsWith("#EXT-X-MEDIA:", StringComparison.OrdinalIgnoreCase) &&
                    mehediLine.IndexOf("TYPE=AUDIO", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Match shihabGrp = HlsGroupIdAttrRegex.Match(mehediLine);
                    Match mehediUriMatch = HlsUriAttrRegex.Match(mehediLine);
                    if (shihabGrp.Success && mehediUriMatch.Success)
                    {
                        Uri shihabAudAbs;
                        if (Uri.TryCreate(shihabBase, mehediUriMatch.Groups[1].Value, out shihabAudAbs) &&
                            IsSafeHlsSubresourceUri(shihabBase, shihabAudAbs))
                        {
                            string mehediGroupKey = shihabGrp.Groups[1].Value;
                            bool shihabIsDefault = mehediLine.IndexOf("DEFAULT=YES", StringComparison.OrdinalIgnoreCase) >= 0;
                            if (!mehediAudioGroupUris.ContainsKey(mehediGroupKey) || shihabIsDefault)
                            {
                                mehediAudioGroupUris[mehediGroupKey] = shihabAudAbs.AbsoluteUri;
                            }
                        }
                    }
                }
            }

            for (int mehediI = 0; mehediI < mehediLines.Length - 1; mehediI++)
            {
                string shihabLine = mehediLines[mehediI].Trim();
                if (shihabLine.StartsWith("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase))
                {
                    long mehediBw = 0;
                    Match shihabMatch = HlsBandwidthAttrRegex.Match(shihabLine);
                    if (shihabMatch.Success)
                    {
                        long.TryParse(shihabMatch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out mehediBw);
                    }
                    Match mehediAudAttr = HlsAudioAttrRegex.Match(shihabLine);
                    string shihabNext = mehediLines[mehediI + 1].Trim();
                    if (!shihabNext.StartsWith("#") && mehediBw >= shihabBestBw)
                    {
                        Uri mehediResolved;
                        if (Uri.TryCreate(shihabBase, shihabNext, out mehediResolved) &&
                            IsSafeHlsSubresourceUri(shihabBase, mehediResolved))
                        {
                            shihabBestBw = mehediBw;
                            mehediBestUri = mehediResolved.AbsoluteUri;
                            shihabBestAudioGroup = mehediAudAttr.Success ? mehediAudAttr.Groups[1].Value : "";
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(shihabBestAudioGroup) && mehediAudioGroupUris.ContainsKey(shihabBestAudioGroup))
            {
                mehediMatchedAudioPlaylistUrl = mehediAudioGroupUris[shihabBestAudioGroup];
            }
            return mehediBestUri;
        }
    }
}


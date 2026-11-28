using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security;
using System.Security.Cryptography;
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
        private static readonly Regex HlsUriAttrRegex = new Regex(@"URI=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, HlsRegexTimeout);
        private static readonly Regex HlsByteRangeAttrRegex = new Regex(@"BYTERANGE=""(\d+)(?:@(\d+))?""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, HlsRegexTimeout);
        private static readonly Regex HlsExtByteRangeRegex = new Regex(@"(\d+)(?:@(\d+))?", RegexOptions.CultureInvariant, HlsRegexTimeout);
        private static readonly Regex HlsGroupIdAttrRegex = new Regex(@"GROUP-ID=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, HlsRegexTimeout);
        private static readonly Regex HlsBandwidthAttrRegex = new Regex(@"BANDWIDTH=(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, HlsRegexTimeout);
        private static readonly Regex HlsAudioAttrRegex = new Regex(@"AUDIO=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, HlsRegexTimeout);

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

        private static bool HasEncryptedHlsKeyTag(string shihabManifestText)
        {
            if (string.IsNullOrEmpty(shihabManifestText)) return false;
            string[] mehediLines = shihabManifestText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int shihabI = 0; shihabI < mehediLines.Length; shihabI++)
            {
                string mehediLine = mehediLines[shihabI].Trim();
                if (mehediLine.StartsWith("#EXT-X-KEY:", StringComparison.OrdinalIgnoreCase) ||
                    mehediLine.StartsWith("#EXT-X-SESSION-KEY:", StringComparison.OrdinalIgnoreCase))
                {
                    if (mehediLine.IndexOf("METHOD=NONE", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return true;
                    }
                }
            }
            return false;
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
            if (HasEncryptedHlsKeyTag(shihabManifestText))
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
            string inlineVideoSha256;
            long inlineVideoBytes;
            DownloadMehediHlsMediaPlaylistToFile(
                mehediActiveVideoPlaylistUrl,
                shihabVideoPartPath,
                "HLS",
                shihabItem,
                true,
                mehediToken,
                true,
                out inlineVideoSha256,
                out inlineVideoBytes);
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

            bool usedSeparateAudioMux = false;
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
                    usedSeparateAudioMux = true;
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

            if (!usedSeparateAudioMux && !string.IsNullOrEmpty(inlineVideoSha256))
            {
                shihabItem.MehediSha256Hex = inlineVideoSha256;
                shihabItem.MehediDownloadedBytes = inlineVideoBytes;
                shihabItem.ShihabTotalBytes = inlineVideoBytes;
            }
            else
            {
                ComputeShihabFileSha256AndSize(shihabItem, mehediTargetFull);
            }
            if (_mehediConfig.ShihabWriteZoneIdentifier)
            {
                ShihabOsAndDiskSafety.WriteMarkOfTheWeb(mehediTargetFull, shihabItem.MehediUrl, shihabItem.MehediReferer);
            }

            try
            {
                if (Directory.Exists(shihabTempDir)) ShihabProcessAndPathSafety.SafeDeleteDirectoryNoFollow(shihabTempDir);
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
            string ignoredSha;
            long ignoredBytes;
            DownloadMehediHlsMediaPlaylistToFile(
                shihabMediaPlaylistUrl,
                mehediOutputFilePath,
                shihabLabelPrefix,
                mehediItem,
                shihabUpdateMainBytes,
                mehediToken,
                false,
                out ignoredSha,
                out ignoredBytes);
        }

        private void DownloadMehediHlsMediaPlaylistToFile(
            string shihabMediaPlaylistUrl,
            string mehediOutputFilePath,
            string shihabLabelPrefix,
            ShihabDownloadItem mehediItem,
            bool shihabUpdateMainBytes,
            CancellationToken mehediToken,
            bool computeSha256Inline,
            out string computedSha256Hex,
            out long computedFileBytes)
        {
            computedSha256Hex = "";
            computedFileBytes = 0L;
            string shihabManifestText = DownloadMehediText(shihabMediaPlaylistUrl, mehediItem, mehediToken);
            if (mehediToken.IsCancellationRequested) return;
            if (HasEncryptedHlsKeyTag(shihabManifestText))
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
            int hlsConcurrency = Math.Min(8, Math.Max(1, mehediItem.MehediMaxConnections));
            if (hlsConcurrency > mehediSegmentUrls.Count) hlsConcurrency = mehediSegmentUrls.Count;

            long shihabAccumulatedBytes = 0;
            Stopwatch hlsThrottleWatch = Stopwatch.StartNew();
            long hlsWinStartMs = 0L;
            long hlsWinBytes = 0L;
            double hlsItemBucketBytes = 0.0;
            long hlsItemLastTickMs = 0L;
            object hlsThrottleLock = new object();

            if (hlsConcurrency <= 1 || mehediSegmentUrls.Count <= 1)
            {
                try
                {
                    object seqSegLock = GetItemSegmentLock(mehediItem);
                    using (SHA256 seqSha = computeSha256Inline ? SHA256.Create() : null)
                    using (FileStream shihabOut = new FileStream(tempHlsOutPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                    {
                        byte[] mehediBuf = new byte[65536];
                        for (int mehediIdx = 0; mehediIdx < mehediSegmentUrls.Count; mehediIdx++)
                        {
                            if (mehediToken.IsCancellationRequested) return;
                            if (_mehediConfig.IsQuotaExceeded())
                            {
                                throw new IOException("Bandwidth quota limit reached.");
                            }
                            using (CancelableHttpResponse mehediSegRespHandle = GetCancelableResponseWithSafeRedirects(
                                mehediSegmentUrls[mehediIdx],
                                "GET",
                                mehediItem,
                                shihabSegStarts[mehediIdx],
                                mehediSegEnds[mehediIdx],
                                null,
                                mehediToken))
                            using (Stream shihabSegStream = mehediSegRespHandle.Response.GetResponseStream())
                            {
                                int shihabRead;
                                while (!mehediToken.IsCancellationRequested && (shihabRead = shihabSegStream.Read(mehediBuf, 0, mehediBuf.Length)) > 0)
                                {
                                    shihabOut.Write(mehediBuf, 0, shihabRead);
                                    if (seqSha != null)
                                    {
                                        seqSha.TransformBlock(mehediBuf, 0, shihabRead, null, 0);
                                    }
                                    _mehediConfig.RecordQuotaBytes(shihabRead);
                                    shihabAccumulatedBytes += shihabRead;
                                    ApplyMehediSpeedLimiter(mehediItem, shihabRead, hlsThrottleWatch, ref hlsWinStartMs, ref hlsWinBytes, mehediToken, seqSegLock);
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
                        if (seqSha != null)
                        {
                            seqSha.TransformFinalBlock(new byte[0], 0, 0);
                            StringBuilder hexSb = new StringBuilder(64);
                            byte[] hashBytes = seqSha.Hash;
                            for (int h = 0; h < hashBytes.Length; h++)
                            {
                                hexSb.Append(hashBytes[h].ToString("x2"));
                            }
                            computedSha256Hex = hexSb.ToString();
                        }
                        computedFileBytes = shihabAccumulatedBytes;
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
                return;
            }

            // Bounded parallel HLS segment fetcher preserving exact media sequence order
            string segWorkDir = mehediOutputFilePath + "_hlssegs_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(segWorkDir);
            string[] segFiles = new string[mehediSegmentUrls.Count];
            int nextIndex = -1;
            int completedCount = 0;
            Exception firstError = null;
            object errLock = new object();

            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(mehediToken))
            {
                CancellationToken workerToken = linkedCts.Token;
                try
                {
                    System.Threading.Tasks.Task[] workers = new System.Threading.Tasks.Task[hlsConcurrency];
                    for (int w = 0; w < hlsConcurrency; w++)
                    {
                        workers[w] = System.Threading.Tasks.Task.Run(delegate
                        {
                            byte[] localBuf = new byte[65536];
                            while (!workerToken.IsCancellationRequested)
                            {
                                int segIdx = Interlocked.Increment(ref nextIndex);
                                if (segIdx >= mehediSegmentUrls.Count) break;
                                if (_mehediConfig.IsQuotaExceeded())
                                {
                                    lock (errLock)
                                    {
                                        if (firstError == null) firstError = new IOException("Bandwidth quota limit reached.");
                                    }
                                    try { linkedCts.Cancel(); } catch { }
                                    break;
                                }

                                string segPath = Path.Combine(segWorkDir, string.Format("seg_{0:D6}.bin", segIdx));
                                Exception segEx = null;
                                for (int attempt = 0; attempt < 3 && !workerToken.IsCancellationRequested; attempt++)
                                {
                                    segEx = null;
                                    try
                                    {
                                        using (CancelableHttpResponse segRespHandle = GetCancelableResponseWithSafeRedirects(
                                            mehediSegmentUrls[segIdx],
                                            "GET",
                                            mehediItem,
                                            shihabSegStarts[segIdx],
                                            mehediSegEnds[segIdx],
                                            null,
                                            workerToken))
                                        using (Stream segStream = segRespHandle.Response.GetResponseStream())
                                        using (FileStream segFs = new FileStream(segPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                                        {
                                            int r;
                                            while (!workerToken.IsCancellationRequested && (r = segStream.Read(localBuf, 0, localBuf.Length)) > 0)
                                            {
                                                segFs.Write(localBuf, 0, r);
                                                _mehediConfig.RecordQuotaBytes(r);
                                                long curAcc = Interlocked.Add(ref shihabAccumulatedBytes, r);
                                                if (shihabUpdateMainBytes) mehediItem.MehediDownloadedBytes = curAcc;

                                                int itemSleepMs = 0;
                                                if (mehediItem.ShihabUseItemSpeedLimit && mehediItem.MehediItemSpeedLimitKb > 0)
                                                {
                                                    lock (hlsThrottleLock)
                                                    {
                                                        long nowMs = hlsThrottleWatch.ElapsedMilliseconds;
                                                        long elapsedMs = Math.Max(0L, nowMs - hlsItemLastTickMs);
                                                        hlsItemLastTickMs = nowMs;
                                                        double rateBps = Math.Max(4096.0, mehediItem.MehediItemSpeedLimitKb * 1024.0);
                                                        double maxBurst = Math.Max(65536.0, rateBps * 0.25);
                                                        hlsItemBucketBytes = Math.Min(maxBurst, hlsItemBucketBytes + (rateBps * elapsedMs / 1000.0));
                                                        hlsItemBucketBytes -= r;
                                                        if (hlsItemBucketBytes < 0.0)
                                                        {
                                                            itemSleepMs = (int)Math.Min(250.0, (-hlsItemBucketBytes * 1000.0) / rateBps);
                                                        }
                                                    }
                                                }
                                                if (itemSleepMs > 5 && !workerToken.IsCancellationRequested)
                                                {
                                                    WaitSleepOrCancel(itemSleepMs, workerToken);
                                                }
                                                ApplyGlobalSpeedLimiterOnly(r, workerToken);
                                            }
                                            segFs.Flush();
                                        }
                                        break;
                                    }
                                    catch (Exception ex)
                                    {
                                        segEx = ex;
                                        try { if (File.Exists(segPath)) File.Delete(segPath); } catch { }
                                        if (workerToken.IsCancellationRequested || attempt >= 2) break;
                                        WaitSleepOrCancel(300 * (attempt + 1), workerToken);
                                    }
                                }

                                if (segEx != null)
                                {
                                    lock (errLock)
                                    {
                                        if (firstError == null) firstError = segEx;
                                    }
                                    try { linkedCts.Cancel(); } catch { }
                                    break;
                                }

                                segFiles[segIdx] = segPath;
                                int doneNow = Interlocked.Increment(ref completedCount);
                                long accNow = Interlocked.Read(ref shihabAccumulatedBytes);
                                if (shihabUpdateMainBytes)
                                {
                                    mehediItem.MehediDownloadedBytes = accNow;
                                    mehediItem.ShihabTotalBytes = (long)((accNow / (double)doneNow) * mehediSegmentUrls.Count);
                                }
                                double elapsedSec = Math.Max(0.2, hlsThrottleWatch.Elapsed.TotalSeconds);
                                mehediItem.ShihabCurrentSpeedBps = accNow / elapsedSec;
                                mehediItem.ShihabStatusText = string.Format("{0} {1}/{2} ({3}x)", shihabLabelPrefix, doneNow, mehediSegmentUrls.Count, hlsConcurrency);
                                NotifyShihabItemChanged(mehediItem);
                            }
                        });
                    }

                    System.Threading.Tasks.Task.WaitAll(workers);

                    if (mehediToken.IsCancellationRequested) return;
                    if (firstError != null) throw firstError;

                    mehediItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabRebuilding, string.Format("Assembling {0} segments...", mehediSegmentUrls.Count));
                    NotifyShihabItemChanged(mehediItem);

                    long assembledBytes = 0L;
                    using (SHA256 mergeSha = computeSha256Inline ? SHA256.Create() : null)
                    using (FileStream shihabOut = new FileStream(tempHlsOutPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                    {
                        byte[] copyBuf = new byte[65536];
                        for (int i = 0; i < segFiles.Length; i++)
                        {
                            if (mehediToken.IsCancellationRequested) return;
                            string segFilePath = segFiles[i];
                            if (string.IsNullOrEmpty(segFilePath) || !File.Exists(segFilePath))
                            {
                                throw new IOException("Missing HLS segment #" + i + " during assembly.");
                            }
                            using (FileStream inSeg = new FileStream(segFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
                            {
                                int r;
                                while ((r = inSeg.Read(copyBuf, 0, copyBuf.Length)) > 0)
                                {
                                    shihabOut.Write(copyBuf, 0, r);
                                    if (mergeSha != null)
                                    {
                                        mergeSha.TransformBlock(copyBuf, 0, r, null, 0);
                                    }
                                    assembledBytes += r;
                                }
                            }
                            try { File.Delete(segFilePath); } catch { }
                        }
                        shihabOut.Flush(true);
                        if (mergeSha != null)
                        {
                            mergeSha.TransformFinalBlock(new byte[0], 0, 0);
                            StringBuilder hexSb = new StringBuilder(64);
                            byte[] hashBytes = mergeSha.Hash;
                            for (int h = 0; h < hashBytes.Length; h++)
                            {
                                hexSb.Append(hashBytes[h].ToString("x2"));
                            }
                            computedSha256Hex = hexSb.ToString();
                        }
                        computedFileBytes = assembledBytes;
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
                finally
                {
                    try { if (Directory.Exists(segWorkDir)) ShihabProcessAndPathSafety.SafeDeleteDirectoryNoFollow(segWorkDir); } catch { }
                }
            }
        }

        private string DownloadMehediText(string shihabUrl, ShihabDownloadItem mehediItem, CancellationToken mehediToken)
        {
            using (CancelableHttpResponse mehediRespHandle = GetCancelableResponseWithSafeRedirects(shihabUrl, "GET", mehediItem, -1, -1, null, mehediToken))
            using (Stream respStream = mehediRespHandle.Response.GetResponseStream())
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
                    int shihabNextIdx = mehediI + 1;
                    bool mehediHitNextStreamInf = false;
                    while (shihabNextIdx < mehediLines.Length)
                    {
                        string mehediPeek = mehediLines[shihabNextIdx].Trim();
                        if (string.IsNullOrEmpty(mehediPeek))
                        {
                            shihabNextIdx++;
                            continue;
                        }
                        if (mehediPeek.StartsWith("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase))
                        {
                            mehediHitNextStreamInf = true;
                            break;
                        }
                        if (mehediPeek.StartsWith("#"))
                        {
                            shihabNextIdx++;
                            continue;
                        }
                        break;
                    }
                    if (mehediHitNextStreamInf)
                    {
                        mehediI = shihabNextIdx - 1;
                        continue;
                    }
                    if (shihabNextIdx >= mehediLines.Length)
                    {
                        break;
                    }
                    string shihabNext = mehediLines[shihabNextIdx].Trim();
                    mehediI = shihabNextIdx;
                    if (!string.IsNullOrEmpty(shihabNext) && !shihabNext.StartsWith("#") && mehediBw >= shihabBestBw)
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


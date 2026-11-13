using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShihabMehediIDM
{
    public partial class MehediDynamicSegmentEngine
    {
        private const int SegmentStreamBufferBytes = 262144;

        private void EnsureSparseSharedPartFile(string sharedSparsePartPath, long totalBytes)
        {
            string parentDir = Path.GetDirectoryName(sharedSparsePartPath);
            if (!string.IsNullOrEmpty(parentDir)) Directory.CreateDirectory(parentDir);

            bool newlyCreated = !File.Exists(sharedSparsePartPath);
            using (FileStream fs = new FileStream(sharedSparsePartPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, SegmentStreamBufferBytes))
            {
                if (newlyCreated && _mehediConfig.ShihabEnableSparseFiles)
                {
                    ShihabOsAndDiskSafety.TryMarkFileSparse(fs);
                }
                if (totalBytes > 0 && fs.Length < totalBytes)
                {
                    if (_mehediConfig.ShihabEnableSparseFiles)
                    {
                        ShihabOsAndDiskSafety.TryMarkFileSparse(fs);
                    }
                    fs.SetLength(totalBytes);
                }
            }
        }

        private void PrepareInitialShihabSegments(ShihabDownloadItem shihabItem, string mehediItemTempDir, string sharedSparsePartPath)
        {
            EnsureSparseSharedPartFile(sharedSparsePartPath, shihabItem.ShihabTotalBytes);
            object segLock = GetItemSegmentLock(shihabItem);

            lock (segLock)
            {
                if (shihabItem.MehediSegments == null)
                {
                    shihabItem.MehediSegments = new List<MehediSegmentChunk>();
                }

                if (shihabItem.MehediSegments.Count > 0)
                {
                    bool mehediValidExisting = true;
                    for (int shihabI = 0; shihabI < shihabItem.MehediSegments.Count; shihabI++)
                    {
                        MehediSegmentChunk mehediSeg = shihabItem.MehediSegments[shihabI];

                        if (!string.IsNullOrEmpty(mehediSeg.MehediPartFilePath) &&
                            !string.Equals(Path.GetFullPath(mehediSeg.MehediPartFilePath), Path.GetFullPath(sharedSparsePartPath), StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(mehediSeg.MehediPartFilePath))
                        {
                            try
                            {
                                using (FileStream oldSegFs = new FileStream(mehediSeg.MehediPartFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                using (FileStream dstFs = new FileStream(sharedSparsePartPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, SegmentStreamBufferBytes))
                                {
                                    dstFs.Seek(mehediSeg.MehediStartByte, SeekOrigin.Begin);
                                    byte[] migBuf = new byte[65536];
                                    long copied = 0;
                                    long maxAllowed = mehediSeg.ShihabEndByte >= 0 ? mehediSeg.GetShihabTotalSpan() : long.MaxValue;
                                    int r;
                                    while (copied < maxAllowed && (r = oldSegFs.Read(migBuf, 0, (int)Math.Min((long)migBuf.Length, maxAllowed - copied))) > 0)
                                    {
                                        dstFs.Write(migBuf, 0, r);
                                        copied += r;
                                    }
                                    mehediSeg.MehediDownloadedBytes = copied;
                                }
                                try { File.Delete(mehediSeg.MehediPartFilePath); } catch { }
                            }
                            catch { }
                            mehediSeg.MehediPartFilePath = sharedSparsePartPath;
                        }

                        if (File.Exists(sharedSparsePartPath))
                        {
                            mehediSeg.MehediPartFilePath = sharedSparsePartPath;
                            long diskLen = new FileInfo(sharedSparsePartPath).Length;
                            if (mehediSeg.ShihabEndByte >= 0)
                            {
                                long maxSpan = mehediSeg.GetShihabTotalSpan();
                                long maxAvailFromStart = Math.Max(0L, diskLen - mehediSeg.MehediStartByte);
                                mehediSeg.MehediDownloadedBytes = Math.Max(0L, Math.Min(mehediSeg.MehediDownloadedBytes, Math.Min(maxSpan, maxAvailFromStart)));
                            }
                            else if (!shihabItem.ShihabResumeSupported)
                            {
                                mehediSeg.MehediDownloadedBytes = 0;
                                if (shihabI > 0) mehediValidExisting = false;
                            }
                        }
                        else
                        {
                            mehediSeg.MehediDownloadedBytes = 0;
                            if (!shihabItem.ShihabResumeSupported && shihabI > 0)
                            {
                                mehediValidExisting = false;
                            }
                        }
                    }
                    if (mehediValidExisting && shihabItem.ShihabResumeSupported)
                    {
                        return;
                    }
                }

                lock (shihabItem.MehediSegments)
                {
                    shihabItem.MehediSegments.Clear();
                    string effectiveUrl = GetEffectiveHttpUrl(shihabItem, shihabItem.MehediUrl);
                    Uri targetUri;
                    string hostName = Uri.TryCreate(effectiveUrl, UriKind.Absolute, out targetUri) ? targetUri.Host : "";
                    int mehediDesiredConns = GetAdaptiveEffectiveConnections(hostName, shihabItem);
                    if (!shihabItem.ShihabResumeSupported || shihabItem.ShihabTotalBytes < 262144L)
                    {
                        mehediDesiredConns = 1;
                    }

                    if (mehediDesiredConns == 1)
                    {
                        shihabItem.MehediSegments.Add(new MehediSegmentChunk
                        {
                            ShihabSegmentIndex = 1,
                            MehediStartByte = 0,
                            ShihabEndByte = shihabItem.ShihabTotalBytes > 0 ? (shihabItem.ShihabTotalBytes - 1L) : -1L,
                            MehediDownloadedBytes = 0,
                            ShihabSegmentState = "Connecting...",
                            MehediPartFilePath = sharedSparsePartPath
                        });
                        return;
                    }

                    long shihabBaseSpan = (shihabItem.ShihabTotalBytes / mehediDesiredConns + 4095L) & ~4095L;
                    if (shihabBaseSpan <= 0 || shihabBaseSpan * (mehediDesiredConns - 1) >= shihabItem.ShihabTotalBytes)
                    {
                        shihabBaseSpan = shihabItem.ShihabTotalBytes / mehediDesiredConns;
                    }
                    long mehediCursor = 0;
                    for (int shihabIdx = 1; shihabIdx <= mehediDesiredConns; shihabIdx++)
                    {
                        long mehediEnd = (shihabIdx == mehediDesiredConns)
                            ? (shihabItem.ShihabTotalBytes - 1L)
                            : Math.Min(shihabItem.ShihabTotalBytes - 1L, mehediCursor + shihabBaseSpan - 1L);
                        shihabItem.MehediSegments.Add(new MehediSegmentChunk
                        {
                            ShihabSegmentIndex = shihabIdx,
                            MehediStartByte = mehediCursor,
                            ShihabEndByte = mehediEnd,
                            MehediDownloadedBytes = 0,
                            ShihabSegmentState = "Connecting...",
                            MehediPartFilePath = sharedSparsePartPath
                        });
                        mehediCursor = mehediEnd + 1L;
                    }
                }
            }
        }

        private void DownloadSingleMehediSegment(
            ShihabDownloadItem shihabItem,
            MehediSegmentChunk mehediChunk,
            string sharedSparsePartPath,
            SegmentWorkerTelemetry telemetry,
            object shihabSegmentLock,
            CancellationToken mehediToken)
        {
            FileStream shihabPartFs = null;
            try
            {
                lock (shihabSegmentLock)
                {
                    if (mehediChunk.ShihabEndByte >= 0 && mehediChunk.GetMehediRemainingBytes() <= 0)
                    {
                        mehediChunk.ShihabSegmentState = "Completed.";
                        mehediChunk.ShihabIsActiveWorker = false;
                        return;
                    }
                    if (!shihabItem.ShihabResumeSupported)
                    {
                        mehediChunk.MehediDownloadedBytes = 0;
                    }
                    mehediChunk.ShihabSegmentState = "Connecting...";
                }

                string targetUrl = GetEffectiveHttpUrl(shihabItem, shihabItem.MehediUrl);
                string ifRangeVal = "";
                if (!string.IsNullOrWhiteSpace(shihabItem.ShihabETag) &&
                    !shihabItem.ShihabETag.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
                {
                    ifRangeVal = shihabItem.ShihabETag;
                }
                else if (!string.IsNullOrWhiteSpace(shihabItem.ShihabServerLastModified))
                {
                    ifRangeVal = shihabItem.ShihabServerLastModified;
                }

                long shihabGvSliceCap = shihabItem.ShihabResumeSupported
                    ? GetShihabGoogleVideoBurstSliceBytes(targetUrl)
                    : -1L;
                byte[] mehediBuf = new byte[65536];
                Stopwatch shihabItemThrottleWatch = Stopwatch.StartNew();
                long mehediItemWindowStartMs = 0;
                long shihabItemWindowBytes = 0;
                long workerLastSpeedMs = 0;
                long workerBytesSinceSpeedTick = 0;
                int mehediTransientRetries = 0;
                bool overlapVerifiedForThisRun = false;

                while (!mehediToken.IsCancellationRequested)
                {
                    long shihabSubStart;
                    long mehediCurrentEnd;
                    long existingDiskBytes;
                    lock (shihabSegmentLock)
                    {
                        if (mehediChunk.ShihabEndByte >= 0 && mehediChunk.GetMehediRemainingBytes() <= 0)
                        {
                            break;
                        }
                        existingDiskBytes = mehediChunk.MehediDownloadedBytes;
                        shihabSubStart = mehediChunk.MehediStartByte + existingDiskBytes;
                        mehediCurrentEnd = mehediChunk.ShihabEndByte;
                    }

                    int overlapBytesToVerify = 0;
                    byte[] expectedOverlapBytes = null;
                    if (!overlapVerifiedForThisRun &&
                        _mehediConfig.ShihabVerifyResumeOverlap &&
                        shihabItem.ShihabResumeSupported &&
                        existingDiskBytes > 0L &&
                        File.Exists(sharedSparsePartPath))
                    {
                        int wantOverlap = (int)Math.Min(existingDiskBytes, 64L);
                        long checkOffset = shihabSubStart - wantOverlap;
                        try
                        {
                            using (FileStream checkFs = new FileStream(sharedSparsePartPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                if (checkFs.Length >= shihabSubStart)
                                {
                                    expectedOverlapBytes = new byte[wantOverlap];
                                    checkFs.Seek(checkOffset, SeekOrigin.Begin);
                                    int gotDisk = 0;
                                    while (gotDisk < wantOverlap)
                                    {
                                        int rd = checkFs.Read(expectedOverlapBytes, gotDisk, wantOverlap - gotDisk);
                                        if (rd <= 0) break;
                                        gotDisk += rd;
                                    }
                                    if (gotDisk == wantOverlap)
                                    {
                                        overlapBytesToVerify = wantOverlap;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            overlapBytesToVerify = 0;
                        }
                    }

                    long shihabReqStart = shihabItem.ShihabResumeSupported ? (shihabSubStart - overlapBytesToVerify) : -1L;
                    long mehediReqEnd = (shihabItem.ShihabResumeSupported && mehediCurrentEnd >= shihabSubStart) ? mehediCurrentEnd : -1L;
                    if (shihabGvSliceCap > 0 && shihabReqStart >= 0)
                    {
                        long shihabBurstEnd = shihabReqStart + shihabGvSliceCap - 1L;
                        mehediReqEnd = (mehediReqEnd >= shihabReqStart)
                            ? Math.Min(mehediReqEnd, shihabBurstEnd)
                            : shihabBurstEnd;
                    }

                    long mehediSubSliceReadTotal = 0;
                    bool shihabReachedDynamicBoundary = false;

                    try
                    {
                        HttpWebRequest shihabReq;
                        using (HttpWebResponse mehediResp = GetResponseWithSafeRedirects(
                            targetUrl,
                            "GET",
                            shihabItem,
                            shihabReqStart,
                            mehediReqEnd,
                            shihabReqStart > 0 ? ifRangeVal : null,
                            mehediToken,
                            out shihabReq))
                        using (Stream shihabNetStream = mehediResp.GetResponseStream())
                        {
                            if (shihabReqStart > 0 && mehediResp.StatusCode != HttpStatusCode.PartialContent)
                            {
                                int segCount;
                                lock (shihabSegmentLock)
                                {
                                    segCount = shihabItem.MehediSegments != null ? shihabItem.MehediSegments.Count : 0;
                                }
                                if (mehediChunk.MehediStartByte == 0 && segCount == 1)
                                {
                                    lock (shihabSegmentLock)
                                    {
                                        mehediChunk.MehediDownloadedBytes = 0;
                                        shihabItem.ShihabResumeSupported = false;
                                    }
                                    shihabSubStart = 0;
                                    overlapBytesToVerify = 0;
                                }
                                else
                                {
                                    throw new IOException("Server did not honor HTTP byte-range request (HTTP " + (int)mehediResp.StatusCode + ").");
                                }
                            }

                            if (overlapBytesToVerify > 0 && expectedOverlapBytes != null)
                            {
                                byte[] wireOverlap = new byte[overlapBytesToVerify];
                                int gotOverlap = 0;
                                while (gotOverlap < overlapBytesToVerify)
                                {
                                    int r = shihabNetStream.Read(wireOverlap, gotOverlap, overlapBytesToVerify - gotOverlap);
                                    if (r <= 0) break;
                                    gotOverlap += r;
                                }
                                if (gotOverlap != overlapBytesToVerify)
                                {
                                    throw new IOException("Resume overlap verification failed: server closed stream before sending overlap bytes.");
                                }
                                for (int ov = 0; ov < overlapBytesToVerify; ov++)
                                {
                                    if (wireOverlap[ov] != expectedOverlapBytes[ov])
                                    {
                                        throw new IOException("Resume overlap verification failed: remote file bytes changed on server.");
                                    }
                                }
                                overlapVerifiedForThisRun = true;
                            }

                            if (!shihabItem.ShihabResumeSupported && shihabItem.ShihabTotalBytes <= 0 && mehediResp.ContentLength > 0)
                            {
                                shihabItem.ShihabTotalBytes = mehediResp.ContentLength;
                            }

                            if (shihabPartFs == null)
                            {
                                EnsureSparseSharedPartFile(sharedSparsePartPath, shihabItem.ShihabTotalBytes);
                                shihabPartFs = new FileStream(sharedSparsePartPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite, SegmentStreamBufferBytes);
                            }
                            shihabPartFs.Seek(shihabSubStart, SeekOrigin.Begin);

                            lock (shihabSegmentLock)
                            {
                                mehediChunk.ShihabSegmentState = "Receiving data...";
                            }

                            while (!mehediToken.IsCancellationRequested)
                            {
                                int mehediRead = shihabNetStream.Read(mehediBuf, 0, mehediBuf.Length);
                                if (mehediRead <= 0)
                                {
                                    break;
                                }

                                if (telemetry != null)
                                {
                                    telemetry.FirstByteReceived = true;
                                }

                                mehediSubSliceReadTotal += mehediRead;
                                int shihabToWrite = mehediRead;
                                lock (shihabSegmentLock)
                                {
                                    if (mehediChunk.ShihabEndByte >= 0)
                                    {
                                        long shihabAllowed = (mehediChunk.ShihabEndByte - mehediChunk.MehediStartByte + 1L) - mehediChunk.MehediDownloadedBytes;
                                        if (shihabAllowed <= 0)
                                        {
                                            shihabToWrite = 0;
                                            shihabReachedDynamicBoundary = true;
                                        }
                                        else if (shihabToWrite > shihabAllowed)
                                        {
                                            shihabToWrite = (int)shihabAllowed;
                                            shihabReachedDynamicBoundary = true;
                                        }
                                    }
                                }

                                if (shihabToWrite > 0)
                                {
                                    // Commit bytes to disk BEFORE advancing downloaded progress counter
                                    shihabPartFs.Write(mehediBuf, 0, shihabToWrite);
                                    lock (shihabSegmentLock)
                                    {
                                        mehediChunk.MehediDownloadedBytes += shihabToWrite;
                                    }
                                    _mehediConfig.RecordQuotaBytes(shihabToWrite);

                                    if (telemetry != null)
                                    {
                                        workerBytesSinceSpeedTick += shihabToWrite;
                                        long wNowMs = shihabItemThrottleWatch.ElapsedMilliseconds;
                                        long wDelta = wNowMs - workerLastSpeedMs;
                                        if (wDelta >= 250L)
                                        {
                                            double inst = (workerBytesSinceSpeedTick * 1000.0) / Math.Max(1.0, (double)wDelta);
                                            telemetry.SmoothedBps = telemetry.SmoothedBps <= 1.0
                                                ? inst
                                                : (telemetry.SmoothedBps * 0.6) + (inst * 0.4);
                                            workerBytesSinceSpeedTick = 0;
                                            workerLastSpeedMs = wNowMs;
                                        }
                                    }

                                    ApplyMehediSpeedLimiter(
                                        shihabItem,
                                        shihabToWrite,
                                        shihabItemThrottleWatch,
                                        ref mehediItemWindowStartMs,
                                        ref shihabItemWindowBytes,
                                        mehediToken);
                                }

                                if (shihabReachedDynamicBoundary)
                                {
                                    break;
                                }
                            }
                        }

                        if (mehediSubSliceReadTotal > 0)
                        {
                            mehediTransientRetries = 0;
                        }
                    }
                    catch (Exception shihabSubEx)
                    {
                        if (mehediToken.IsCancellationRequested)
                        {
                            break;
                        }
                        string mehediErrMsg = shihabSubEx.Message ?? "";
                        bool mehediIsFatalMismatchOrAuth =
                            mehediErrMsg.IndexOf("overlap verification failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mehediErrMsg.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mehediErrMsg.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mehediErrMsg.IndexOf("Forbidden", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mehediErrMsg.IndexOf("Unauthorized", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mehediErrMsg.IndexOf("did not honor HTTP byte-range", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mehediErrMsg.IndexOf("Blocked", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (!shihabItem.ShihabResumeSupported || mehediIsFatalMismatchOrAuth || mehediTransientRetries >= 3)
                        {
                            throw;
                        }
                        mehediTransientRetries++;
                        WaitSleepOrCancel(200 * mehediTransientRetries, mehediToken);
                        continue;
                    }

                    if (!shihabItem.ShihabResumeSupported || shihabReachedDynamicBoundary || mehediSubSliceReadTotal <= 0)
                    {
                        break;
                    }
                    if (shihabGvSliceCap <= 0 && mehediCurrentEnd < 0)
                    {
                        break;
                    }
                    if (mehediCurrentEnd < 0 && mehediReqEnd >= shihabReqStart && mehediSubSliceReadTotal < (mehediReqEnd - shihabReqStart + 1L))
                    {
                        break;
                    }
                }

                if (shihabPartFs != null)
                {
                    shihabPartFs.Flush();
                }

                lock (shihabSegmentLock)
                {
                    if (telemetry == null || !telemetry.WasShedOrPreempted)
                    {
                        mehediChunk.ShihabIsActiveWorker = false;
                        if (!mehediToken.IsCancellationRequested &&
                            (mehediChunk.ShihabEndByte < 0 || mehediChunk.GetMehediRemainingBytes() == 0))
                        {
                            mehediChunk.ShihabSegmentState = "Completed.";
                        }
                    }
                }
            }
            catch (Exception shihabEx)
            {
                lock (shihabSegmentLock)
                {
                    if (telemetry == null || !telemetry.WasShedOrPreempted)
                    {
                        mehediChunk.ShihabIsActiveWorker = false;
                        mehediChunk.ShihabSegmentState = mehediToken.IsCancellationRequested
                            ? "Disconnect."
                            : ("Error: " + shihabEx.Message);
                    }
                }
            }
            finally
            {
                if (shihabPartFs != null)
                {
                    try { shihabPartFs.Dispose(); } catch { }
                }
            }
        }

        private void DownloadMehediGoogleVideoOrRelay(
            ShihabDownloadItem shihabItem,
            string mehediRawUrl,
            string shihabOutputFilePath,
            long mehediKnownTotalBytes,
            bool shihabIsPrimaryStream,
            CancellationToken mehediToken)
        {
            long shihabClen;
            string mehediCleanUrl = SanitizeMehediGoogleVideoUrl(mehediRawUrl, out shihabClen);
            long shihabEffectiveTotal = shihabClen > 0 ? shihabClen : mehediKnownTotalBytes;
            object segLock = GetItemSegmentLock(shihabItem);

            if (shihabIsPrimaryStream)
            {
                if (shihabEffectiveTotal > 0)
                {
                    shihabItem.ShihabTotalBytes = shihabEffectiveTotal;
                    shihabItem.ShihabResumeSupported = true;
                }
                lock (segLock)
                {
                    if (shihabItem.MehediSegments == null) shihabItem.MehediSegments = new List<MehediSegmentChunk>();
                    lock (shihabItem.MehediSegments)
                    {
                        shihabItem.MehediSegments.Clear();
                        shihabItem.MehediSegments.Add(new MehediSegmentChunk
                        {
                            ShihabSegmentIndex = 1,
                            MehediStartByte = 0,
                            ShihabEndByte = shihabEffectiveTotal > 0 ? (shihabEffectiveTotal - 1L) : -1L,
                            MehediDownloadedBytes = 0,
                            ShihabSegmentState = "Receiving data...",
                            MehediPartFilePath = shihabOutputFilePath,
                            ShihabIsActiveWorker = true
                        });
                    }
                }
                shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabDownloading, "Receiving data...");
                NotifyShihabItemChanged(shihabItem);
            }

            try
            {
                string outDir = Path.GetDirectoryName(shihabOutputFilePath);
                if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
                long shihabBurstCap = GetShihabGoogleVideoBurstSliceBytes(mehediCleanUrl);
                long shihabSliceBytes = shihabBurstCap > 0 ? shihabBurstCap : (2L * 1024L * 1024L);
                long mehediOffset = 0;
                Stopwatch shihabRateWatch = Stopwatch.StartNew();
                long mehediLastRateMs = 0;
                long shihabLastRateBytes = 0;

                using (FileStream mehediOutFs = new FileStream(shihabOutputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read, SegmentStreamBufferBytes))
                {
                    byte[] shihabReadBuf = new byte[65536];
                    Stopwatch mehediThrottleWatch = Stopwatch.StartNew();
                    long shihabWinStart = 0;
                    long mehediWinBytes = 0;

                    while (!mehediToken.IsCancellationRequested)
                    {
                        if (shihabEffectiveTotal > 0 && mehediOffset >= shihabEffectiveTotal)
                        {
                            break;
                        }
                        long shihabSliceEnd = shihabEffectiveTotal > 0
                            ? Math.Min(shihabEffectiveTotal - 1L, mehediOffset + shihabSliceBytes - 1L)
                            : (mehediOffset + shihabSliceBytes - 1L);
                        long mehediExpectedChunkSpan = (shihabSliceEnd - mehediOffset) + 1L;
                        string shihabChunkUrl = AppendShihabQueryRangeParam(mehediCleanUrl, mehediOffset, shihabSliceEnd);

                        long mehediGotInSlice = 0;
                        HttpWebRequest shihabReq;
                        using (HttpWebResponse mehediResp = GetResponseWithSafeRedirects(shihabChunkUrl, "GET", shihabItem, -1, -1, null, mehediToken, out shihabReq))
                        using (Stream shihabNet = mehediResp.GetResponseStream())
                        {
                            while (!mehediToken.IsCancellationRequested)
                            {
                                int mehediRead = shihabNet.Read(shihabReadBuf, 0, shihabReadBuf.Length);
                                if (mehediRead <= 0) break;
                                mehediOutFs.Write(shihabReadBuf, 0, mehediRead);
                                _mehediConfig.RecordQuotaBytes(mehediRead);
                                mehediOffset += mehediRead;
                                mehediGotInSlice += mehediRead;

                                ApplyMehediSpeedLimiter(shihabItem, mehediRead, mehediThrottleWatch, ref shihabWinStart, ref mehediWinBytes, mehediToken);

                                if (shihabIsPrimaryStream)
                                {
                                    shihabItem.MehediDownloadedBytes = mehediOffset;
                                    lock (segLock)
                                    {
                                        if (shihabItem.MehediSegments.Count > 0)
                                        {
                                            shihabItem.MehediSegments[0].MehediDownloadedBytes = mehediOffset;
                                        }
                                    }
                                }

                                long shihabNowMs = shihabRateWatch.ElapsedMilliseconds;
                                if (shihabNowMs - mehediLastRateMs >= 220)
                                {
                                    double mehediInstBps = ((mehediOffset - shihabLastRateBytes) * 1000.0) / Math.Max(1.0, (double)(shihabNowMs - mehediLastRateMs));
                                    shihabItem.ShihabCurrentSpeedBps = shihabItem.ShihabCurrentSpeedBps <= 1.0
                                        ? mehediInstBps
                                        : (shihabItem.ShihabCurrentSpeedBps * 0.65) + (mehediInstBps * 0.35);
                                    long shihabTargetDenom = shihabIsPrimaryStream ? shihabItem.ShihabTotalBytes : shihabEffectiveTotal;
                                    if (shihabTargetDenom > mehediOffset && shihabItem.ShihabCurrentSpeedBps > 10.0)
                                    {
                                        shihabItem.MehediEtaSeconds = (long)((shihabTargetDenom - mehediOffset) / shihabItem.ShihabCurrentSpeedBps);
                                    }
                                    shihabLastRateBytes = mehediOffset;
                                    mehediLastRateMs = shihabNowMs;
                                    if (shihabIsPrimaryStream)
                                    {
                                        shihabItem.ShihabStatusText = string.Format("{0:0.0}%", shihabItem.GetMehediPercent());
                                    }
                                    else
                                    {
                                        shihabItem.ShihabStatusText = shihabEffectiveTotal > 0
                                            ? string.Format("Audio track {0:0.0}% ({1} / {2})",
                                                Math.Min(100.0, (mehediOffset * 100.0) / shihabEffectiveTotal),
                                                ShihabFormatHelpers.FormatMehediBytes(mehediOffset),
                                                ShihabFormatHelpers.FormatMehediBytes(shihabEffectiveTotal))
                                            : ("Audio track " + ShihabFormatHelpers.FormatMehediBytes(mehediOffset));
                                    }
                                    NotifyShihabItemChanged(shihabItem);
                                }
                            }
                        }

                        if (mehediGotInSlice <= 0)
                        {
                            break;
                        }
                        if (shihabEffectiveTotal <= 0 && mehediGotInSlice < mehediExpectedChunkSpan)
                        {
                            break;
                        }
                    }
                    mehediOutFs.Flush();
                }

                if (mehediToken.IsCancellationRequested)
                {
                    return;
                }
                if (mehediOffset <= 0)
                {
                    throw new IOException("GoogleVideo stream returned 0 bytes.");
                }
                if (shihabIsPrimaryStream)
                {
                    shihabItem.MehediDownloadedBytes = mehediOffset;
                    if (shihabItem.ShihabTotalBytes <= 0) shihabItem.ShihabTotalBytes = mehediOffset;
                    lock (segLock)
                    {
                        if (shihabItem.MehediSegments.Count > 0)
                        {
                            shihabItem.MehediSegments[0].MehediDownloadedBytes = mehediOffset;
                            shihabItem.MehediSegments[0].ShihabSegmentState = "Completed.";
                            shihabItem.MehediSegments[0].ShihabIsActiveWorker = false;
                        }
                    }
                }
            }
            catch (Exception shihabDirectEx)
            {
                if (mehediToken.IsCancellationRequested)
                {
                    return;
                }
                EmitShihabLog("[GoogleVideoRelay] Direct HTTP failed (" + shihabDirectEx.Message + "), switching to Browser Bridge Relay...");
                DownloadViaShihabBrowserRelay(shihabItem, mehediCleanUrl, shihabOutputFilePath, shihabEffectiveTotal, shihabIsPrimaryStream, mehediToken);
            }
        }

        private void DownloadViaShihabBrowserRelay(
            ShihabDownloadItem shihabItem,
            string mehediTargetUrl,
            string shihabOutputFilePath,
            long mehediExpectedBytes,
            bool shihabIsPrimaryStream,
            CancellationToken mehediToken)
        {
            Stopwatch shihabWatch = Stopwatch.StartNew();
            long mehediLastMs = 0;
            long shihabLastBytes = 0;
            object segLock = GetItemSegmentLock(shihabItem);

            if (shihabIsPrimaryStream)
            {
                shihabItem.MehediDownloadedBytes = 0;
                lock (segLock)
                {
                    if (shihabItem.MehediSegments == null) shihabItem.MehediSegments = new List<MehediSegmentChunk>();
                    lock (shihabItem.MehediSegments)
                    {
                        shihabItem.MehediSegments.Clear();
                        shihabItem.MehediSegments.Add(new MehediSegmentChunk
                        {
                            ShihabSegmentIndex = 1,
                            MehediStartByte = 0,
                            ShihabEndByte = mehediExpectedBytes > 0 ? (mehediExpectedBytes - 1L) : -1L,
                            MehediDownloadedBytes = 0,
                            ShihabSegmentState = "Browser Bridge Relay...",
                            MehediPartFilePath = shihabOutputFilePath,
                            ShihabIsActiveWorker = true
                        });
                    }
                }
                shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabDownloading, "Receiving via Browser Bridge...");
                NotifyShihabItemChanged(shihabItem);
            }
            else
            {
                shihabItem.ShihabStatusText = "Receiving audio via Browser Bridge...";
                NotifyShihabItemChanged(shihabItem);
            }

            long shihabReceivedTotal = ShihabBrowserRelayHub.ExecuteMehediRelayDownload(
                mehediTargetUrl,
                shihabOutputFilePath,
                shihabItem.ShihabSourceTabId,
                shihabItem.MehediReferer,
                shihabItem.MehediOriginHeader,
                shihabItem.MehediOmitCookies,
                mehediExpectedBytes,
                delegate(long mehediGotBytes, long shihabRepTotal)
                {
                    if (shihabIsPrimaryStream)
                    {
                        if (shihabRepTotal > 0)
                        {
                            shihabItem.ShihabTotalBytes = shihabRepTotal;
                            shihabItem.ShihabResumeSupported = true;
                        }
                        shihabItem.MehediDownloadedBytes = mehediGotBytes;
                        lock (segLock)
                        {
                            if (shihabItem.MehediSegments.Count > 0)
                            {
                                shihabItem.MehediSegments[0].MehediDownloadedBytes = mehediGotBytes;
                                if (shihabRepTotal > 0) shihabItem.MehediSegments[0].ShihabEndByte = shihabRepTotal - 1L;
                            }
                        }
                        long mehediNowMs = shihabWatch.ElapsedMilliseconds;
                        if (mehediNowMs - mehediLastMs >= 200)
                        {
                            double shihabBps = ((mehediGotBytes - shihabLastBytes) * 1000.0) / Math.Max(1.0, (double)(mehediNowMs - mehediLastMs));
                            shihabItem.ShihabCurrentSpeedBps = shihabItem.ShihabCurrentSpeedBps <= 1.0
                                ? shihabBps
                                : (shihabItem.ShihabCurrentSpeedBps * 0.65) + (shihabBps * 0.35);
                            if (shihabItem.ShihabTotalBytes > mehediGotBytes && shihabItem.ShihabCurrentSpeedBps > 10.0)
                            {
                                shihabItem.MehediEtaSeconds = (long)((shihabItem.ShihabTotalBytes - mehediGotBytes) / shihabItem.ShihabCurrentSpeedBps);
                            }
                            shihabLastBytes = mehediGotBytes;
                            mehediLastMs = mehediNowMs;
                            shihabItem.ShihabStatusText = string.Format("{0:0.0}%", shihabItem.GetMehediPercent());
                            NotifyShihabItemChanged(shihabItem);
                        }
                    }
                    else
                    {
                        long mehediAudDenom = shihabRepTotal > 0 ? shihabRepTotal : mehediExpectedBytes;
                        shihabItem.ShihabStatusText = mehediAudDenom > 0
                            ? string.Format("Audio track {0:0}%", Math.Min(100.0, (mehediGotBytes * 100.0) / mehediAudDenom))
                            : ("Audio track " + ShihabFormatHelpers.FormatMehediBytes(mehediGotBytes));
                        NotifyShihabItemChanged(shihabItem);
                    }
                },
                mehediToken);

            if (shihabIsPrimaryStream)
            {
                shihabItem.MehediDownloadedBytes = shihabReceivedTotal;
                if (shihabItem.ShihabTotalBytes <= 0) shihabItem.ShihabTotalBytes = shihabReceivedTotal;
                lock (segLock)
                {
                    if (shihabItem.MehediSegments.Count > 0)
                    {
                        shihabItem.MehediSegments[0].MehediDownloadedBytes = shihabReceivedTotal;
                        shihabItem.MehediSegments[0].ShihabSegmentState = "Completed.";
                        shihabItem.MehediSegments[0].ShihabIsActiveWorker = false;
                    }
                }
            }
        }

        private void DownloadMehediAuxiliaryStream(
            ShihabDownloadItem shihabItem,
            string mehediAuxUrl,
            long shihabAuxKnownBytes,
            string mehediAuxOutputPath,
            CancellationToken shihabToken)
        {
            shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabDownloading, "Downloading audio track...");
            NotifyShihabItemChanged(shihabItem);

            if (mehediAuxUrl.IndexOf(".m3u8", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DownloadMehediHlsMediaPlaylistToFile(mehediAuxUrl, mehediAuxOutputPath, "HLS Audio", shihabItem, false, shihabToken);
                return;
            }

            if (IsShihabGoogleVideoUrl(mehediAuxUrl))
            {
                if (IsRealShihabGoogleVideoDomain(mehediAuxUrl) && IsPoisonedWebShihabGoogleVideoUrl(mehediAuxUrl))
                {
                    if (TryHealOrResolveShihabYouTubeItem(shihabItem, null, true) && !string.IsNullOrWhiteSpace(shihabItem.MehediAudioCompanionUrl))
                    {
                        mehediAuxUrl = shihabItem.MehediAudioCompanionUrl;
                        shihabAuxKnownBytes = shihabItem.ShihabAudioCompanionBytes;
                    }
                }

                long shihabAuxClen;
                mehediAuxUrl = SanitizeMehediGoogleVideoUrl(mehediAuxUrl, out shihabAuxClen);
                if (shihabAuxClen > 0 && shihabAuxKnownBytes <= 0)
                {
                    shihabAuxKnownBytes = shihabAuxClen;
                }

                if (!IsDirectMobileShihabGoogleVideoUrl(mehediAuxUrl))
                {
                    DownloadMehediGoogleVideoOrRelay(shihabItem, mehediAuxUrl, mehediAuxOutputPath, shihabAuxKnownBytes, false, shihabToken);
                    return;
                }
            }

            try
            {
                string auxDir = Path.GetDirectoryName(mehediAuxOutputPath);
                if (!string.IsNullOrEmpty(auxDir)) Directory.CreateDirectory(auxDir);
                bool mehediAuxSupportsRanges = IsRealShihabGoogleVideoDomain(mehediAuxUrl);
                if (shihabAuxKnownBytes <= 0 || !mehediAuxSupportsRanges)
                {
                    ShihabProbeMetadata mehediAuxProbe = ProbeMehediUrl(mehediAuxUrl, shihabItem);
                    if (shihabToken.IsCancellationRequested) return;
                    if (mehediAuxProbe.MehediSuccess)
                    {
                        if (shihabAuxKnownBytes <= 0 && mehediAuxProbe.ShihabContentLength > 0)
                        {
                            shihabAuxKnownBytes = mehediAuxProbe.ShihabContentLength;
                        }
                        if (mehediAuxProbe.MehediAcceptsRanges)
                        {
                            mehediAuxSupportsRanges = true;
                        }
                    }
                }

                if (mehediAuxSupportsRanges && shihabAuxKnownBytes > 262144L)
                {
                    long shihabAuxSliceCap = GetShihabGoogleVideoBurstSliceBytes(mehediAuxUrl);
                    if (shihabAuxSliceCap <= 0)
                    {
                        shihabAuxSliceCap = 1048576L;
                    }
                    DownloadFastParallelShihabRangeToFile(
                        shihabItem,
                        mehediAuxUrl,
                        mehediAuxOutputPath,
                        shihabAuxKnownBytes,
                        shihabAuxSliceCap,
                        false,
                        shihabToken);
                    return;
                }

                bool shihabUseExplicitAuxRange = IsRealShihabGoogleVideoDomain(mehediAuxUrl) && shihabAuxKnownBytes > 0;
                HttpWebRequest shihabReq;
                using (HttpWebResponse mehediResp = GetResponseWithSafeRedirects(
                    mehediAuxUrl,
                    "GET",
                    shihabItem,
                    shihabUseExplicitAuxRange ? 0L : -1L,
                    shihabUseExplicitAuxRange ? (shihabAuxKnownBytes - 1L) : -1L,
                    null,
                    shihabToken,
                    out shihabReq))
                using (Stream shihabNet = mehediResp.GetResponseStream())
                using (FileStream mehediFs = new FileStream(mehediAuxOutputPath, FileMode.Create, FileAccess.Write, FileShare.Read, SegmentStreamBufferBytes))
                {
                    long shihabTotal = mehediResp.ContentLength > 0 ? mehediResp.ContentLength : shihabAuxKnownBytes;
                    byte[] mehediBuf = new byte[65536];
                    long shihabGot = 0;
                    int mehediRead;
                    Stopwatch mehediRateWatch = Stopwatch.StartNew();
                    long shihabLastRateMs = 0;
                    long mehediLastRateBytes = 0;
                    long shihabWinStart = 0;
                    long mehediWinBytes = 0;

                    while (!shihabToken.IsCancellationRequested && (mehediRead = shihabNet.Read(mehediBuf, 0, mehediBuf.Length)) > 0)
                    {
                        ApplyMehediSpeedLimiter(shihabItem, mehediRead, mehediRateWatch, ref shihabWinStart, ref mehediWinBytes, shihabToken);
                        mehediFs.Write(mehediBuf, 0, mehediRead);
                        _mehediConfig.RecordQuotaBytes(mehediRead);
                        shihabGot += mehediRead;
                        long shihabNowMs = mehediRateWatch.ElapsedMilliseconds;
                        if (shihabNowMs - shihabLastRateMs >= 200)
                        {
                            double mehediInstBps = ((shihabGot - mehediLastRateBytes) * 1000.0) / Math.Max(1.0, (double)(shihabNowMs - shihabLastRateMs));
                            shihabItem.ShihabCurrentSpeedBps = shihabItem.ShihabCurrentSpeedBps <= 1.0
                                ? mehediInstBps
                                : (shihabItem.ShihabCurrentSpeedBps * 0.65) + (mehediInstBps * 0.35);
                            if (shihabTotal > shihabGot && shihabItem.ShihabCurrentSpeedBps > 10.0)
                            {
                                shihabItem.MehediEtaSeconds = (long)((shihabTotal - shihabGot) / shihabItem.ShihabCurrentSpeedBps);
                            }
                            mehediLastRateBytes = shihabGot;
                            shihabLastRateMs = shihabNowMs;
                            shihabItem.ShihabStatusText = shihabTotal > 0
                                ? string.Format("Audio track {0:0.0}% ({1} / {2})",
                                    Math.Min(100.0, (shihabGot * 100.0) / shihabTotal),
                                    ShihabFormatHelpers.FormatMehediBytes(shihabGot),
                                    ShihabFormatHelpers.FormatMehediBytes(shihabTotal))
                                : ("Audio track " + ShihabFormatHelpers.FormatMehediBytes(shihabGot));
                            NotifyShihabItemChanged(shihabItem);
                        }
                    }
                    mehediFs.Flush();
                    if (shihabGot <= 0 && !shihabToken.IsCancellationRequested)
                    {
                        throw new IOException("Audio stream returned 0 bytes.");
                    }
                }
            }
            catch (Exception mehediAuxEx)
            {
                if (shihabToken.IsCancellationRequested) return;
                if (IsRealShihabGoogleVideoDomain(mehediAuxUrl) &&
                    TryHealOrResolveShihabYouTubeItem(shihabItem, null, true) &&
                    !string.IsNullOrWhiteSpace(shihabItem.MehediAudioCompanionUrl) &&
                    !string.Equals(shihabItem.MehediAudioCompanionUrl, mehediAuxUrl, StringComparison.Ordinal))
                {
                    EmitShihabLog("[YouTubeAudioHeal] Refreshed companion audio URL for " + shihabItem.ShihabFileName);
                    DownloadDirectShihabStreamToFile(shihabItem, shihabItem.MehediAudioCompanionUrl, mehediAuxOutputPath, shihabItem.ShihabAudioCompanionBytes, false, shihabToken);
                    return;
                }
                EmitShihabLog("[AudioRelayFallback] Direct audio HTTP failed (" + mehediAuxEx.Message + "), switching to Browser Bridge Relay...");
                DownloadViaShihabBrowserRelay(shihabItem, mehediAuxUrl, mehediAuxOutputPath, shihabAuxKnownBytes, false, shihabToken);
            }
        }

        private void DownloadFastParallelShihabRangeToFile(
            ShihabDownloadItem shihabItem,
            string mehediTargetUrl,
            string shihabDestPath,
            long mehediTotalBytes,
            long shihabMaxSliceBytes,
            bool mehediIsPrimaryStream,
            CancellationToken shihabToken)
        {
            string destDir = Path.GetDirectoryName(shihabDestPath);
            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

            long mehediSliceSpan = Math.Max(65536L, shihabMaxSliceBytes);
            int shihabSliceCount = (int)((mehediTotalBytes + mehediSliceSpan - 1L) / mehediSliceSpan);
            int mehediConfiguredConns = shihabItem.MehediMaxConnections > 0
                ? shihabItem.MehediMaxConnections
                : _mehediConfig.ShihabDefaultMaxConnections;
            int shihabWorkerCount = Math.Min(shihabSliceCount, Math.Max(1, Math.Min(16, mehediConfiguredConns)));
            object segLock = GetItemSegmentLock(shihabItem);

            if (mehediIsPrimaryStream)
            {
                shihabItem.ShihabTotalBytes = mehediTotalBytes;
                shihabItem.ShihabResumeSupported = true;
                lock (segLock)
                {
                    if (shihabItem.MehediSegments == null) shihabItem.MehediSegments = new List<MehediSegmentChunk>();
                    lock (shihabItem.MehediSegments)
                    {
                        shihabItem.MehediSegments.Clear();
                        long shihabVisSpan = mehediTotalBytes / shihabWorkerCount;
                        long mehediVisCursor = 0;
                        for (int shihabV = 1; shihabV <= shihabWorkerCount; shihabV++)
                        {
                            long mehediVisEnd = (shihabV == shihabWorkerCount)
                                ? (mehediTotalBytes - 1L)
                                : (mehediVisCursor + shihabVisSpan - 1L);
                            shihabItem.MehediSegments.Add(new MehediSegmentChunk
                            {
                                ShihabSegmentIndex = shihabV,
                                MehediStartByte = mehediVisCursor,
                                ShihabEndByte = mehediVisEnd,
                                MehediDownloadedBytes = 0,
                                ShihabSegmentState = "Receiving data...",
                                MehediPartFilePath = shihabDestPath,
                                ShihabIsActiveWorker = true
                            });
                            mehediVisCursor = mehediVisEnd + 1L;
                        }
                    }
                }
                shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabDownloading, "Receiving data...");
                NotifyShihabItemChanged(shihabItem);
            }
            else
            {
                shihabItem.ShihabStatusText = string.Format("Audio track 0.0% (0 B / {0})", ShihabFormatHelpers.FormatMehediBytes(mehediTotalBytes));
                NotifyShihabItemChanged(shihabItem);
            }

            using (FileStream mehediInitFs = new FileStream(shihabDestPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, SegmentStreamBufferBytes))
            {
                mehediInitFs.SetLength(mehediTotalBytes);
            }

            long shihabSharedBytesDone = 0L;
            Stopwatch mehediRateWatch = Stopwatch.StartNew();
            long shihabLastTickMs = 0L;
            long mehediLastTickBytes = 0L;
            object shihabProgressLock = new object();
            int mehediNextSliceCursor = -1;
            Exception shihabFirstFatalError = null;
            object mehediErrorLock = new object();

            using (CancellationTokenSource mehediLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(shihabToken))
            {
                CancellationToken shihabWorkerToken = mehediLinkedCts.Token;
                Task[] shihabWorkers = new Task[shihabWorkerCount];

                for (int mehediW = 0; mehediW < shihabWorkerCount; mehediW++)
                {
                    int shihabWorkerIdx = mehediW;
                    shihabWorkers[mehediW] = Task.Run(delegate
                    {
                        try
                        {
                            using (FileStream mehediWorkerFs = new FileStream(shihabDestPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, SegmentStreamBufferBytes))
                            {
                                byte[] shihabWorkerBuf = new byte[65536];
                                Stopwatch mehediThrottleWatch = Stopwatch.StartNew();
                                long shihabWinStartMs = 0L;
                                long mehediWinBytes = 0L;

                                while (!shihabWorkerToken.IsCancellationRequested)
                                {
                                    int mehediSliceIdx = Interlocked.Increment(ref mehediNextSliceCursor);
                                    if (mehediSliceIdx >= shihabSliceCount)
                                    {
                                        break;
                                    }

                                    long shihabSliceStart = (long)mehediSliceIdx * mehediSliceSpan;
                                    long mehediSliceEnd = Math.Min(mehediTotalBytes - 1L, shihabSliceStart + mehediSliceSpan - 1L);
                                    long shihabExpectedSpan = (mehediSliceEnd - shihabSliceStart) + 1L;

                                    for (int mehediAttempt = 0; mehediAttempt < 3; mehediAttempt++)
                                    {
                                        long shihabGotInAttempt = 0L;
                                        try
                                        {
                                            mehediWorkerFs.Seek(shihabSliceStart, SeekOrigin.Begin);
                                            HttpWebRequest shihabReq;
                                            using (HttpWebResponse mehediResp = GetResponseWithSafeRedirects(mehediTargetUrl, "GET", shihabItem, shihabSliceStart, mehediSliceEnd, null, shihabWorkerToken, out shihabReq))
                                            using (Stream shihabNet = mehediResp.GetResponseStream())
                                            {
                                                if (shihabSliceStart > 0 && mehediResp.StatusCode != HttpStatusCode.PartialContent)
                                                {
                                                    throw new IOException("Server did not honor HTTP byte-range request (HTTP " + (int)mehediResp.StatusCode + ").");
                                                }

                                                while (!shihabWorkerToken.IsCancellationRequested)
                                                {
                                                    int mehediRead = shihabNet.Read(shihabWorkerBuf, 0, shihabWorkerBuf.Length);
                                                    if (mehediRead <= 0)
                                                    {
                                                        break;
                                                    }

                                                    int shihabWriteLen = (int)Math.Min((long)mehediRead, shihabExpectedSpan - shihabGotInAttempt);
                                                    if (shihabWriteLen <= 0)
                                                    {
                                                        break;
                                                    }

                                                    ApplyMehediSpeedLimiter(shihabItem, shihabWriteLen, mehediThrottleWatch, ref shihabWinStartMs, ref mehediWinBytes, shihabWorkerToken);
                                                    mehediWorkerFs.Write(shihabWorkerBuf, 0, shihabWriteLen);
                                                    _mehediConfig.RecordQuotaBytes(shihabWriteLen);
                                                    shihabGotInAttempt += shihabWriteLen;
                                                    long mehediCurTotal = Interlocked.Add(ref shihabSharedBytesDone, shihabWriteLen);

                                                    if (mehediIsPrimaryStream)
                                                    {
                                                        lock (segLock)
                                                        {
                                                            if (shihabWorkerIdx < shihabItem.MehediSegments.Count)
                                                            {
                                                                MehediSegmentChunk shihabVisChunk = shihabItem.MehediSegments[shihabWorkerIdx];
                                                                long mehediVisMax = shihabVisChunk.GetShihabTotalSpan();
                                                                shihabVisChunk.MehediDownloadedBytes = Math.Min(mehediVisMax, shihabVisChunk.MehediDownloadedBytes + shihabWriteLen);
                                                            }
                                                        }
                                                    }

                                                    long shihabNowMs = mehediRateWatch.ElapsedMilliseconds;
                                                    if (shihabNowMs - Interlocked.Read(ref shihabLastTickMs) >= 150)
                                                    {
                                                        if (Monitor.TryEnter(shihabProgressLock))
                                                        {
                                                            try
                                                            {
                                                                long mehediPrevTick = Interlocked.Read(ref shihabLastTickMs);
                                                                long shihabDeltaMs = shihabNowMs - mehediPrevTick;
                                                                if (shihabDeltaMs >= 150)
                                                                {
                                                                    double mehediInstBps = ((mehediCurTotal - mehediLastTickBytes) * 1000.0) / Math.Max(1.0, (double)shihabDeltaMs);
                                                                    shihabItem.ShihabCurrentSpeedBps = shihabItem.ShihabCurrentSpeedBps <= 1.0
                                                                        ? mehediInstBps
                                                                        : (shihabItem.ShihabCurrentSpeedBps * 0.6) + (mehediInstBps * 0.4);
                                                                    long shihabRemBytes = Math.Max(0L, mehediTotalBytes - mehediCurTotal);
                                                                    shihabItem.MehediEtaSeconds = shihabItem.ShihabCurrentSpeedBps > 10.0
                                                                        ? (long)(shihabRemBytes / shihabItem.ShihabCurrentSpeedBps)
                                                                        : 0L;
                                                                    mehediLastTickBytes = mehediCurTotal;
                                                                    Interlocked.Exchange(ref shihabLastTickMs, shihabNowMs);

                                                                    if (mehediIsPrimaryStream)
                                                                    {
                                                                        shihabItem.MehediDownloadedBytes = mehediCurTotal;
                                                                        shihabItem.ShihabStatusText = string.Format("{0:0.0}%", shihabItem.GetMehediPercent());
                                                                    }
                                                                    else
                                                                    {
                                                                        double mehediAudPct = Math.Min(100.0, (mehediCurTotal * 100.0) / mehediTotalBytes);
                                                                        shihabItem.ShihabStatusText = string.Format("Audio track {0:0.0}% ({1} / {2})",
                                                                            mehediAudPct,
                                                                            ShihabFormatHelpers.FormatMehediBytes(mehediCurTotal),
                                                                            ShihabFormatHelpers.FormatMehediBytes(mehediTotalBytes));
                                                                    }
                                                                    NotifyShihabItemChanged(shihabItem);
                                                                }
                                                            }
                                                            finally
                                                            {
                                                                Monitor.Exit(shihabProgressLock);
                                                            }
                                                        }
                                                    }

                                                    if (shihabGotInAttempt >= shihabExpectedSpan)
                                                    {
                                                        break;
                                                    }
                                                }

                                                if (!shihabWorkerToken.IsCancellationRequested && shihabGotInAttempt != shihabExpectedSpan)
                                                {
                                                    throw new IOException(string.Format("Incomplete parallel range slice ({0}/{1} bytes).", shihabGotInAttempt, shihabExpectedSpan));
                                                }
                                            }
                                            break;
                                        }
                                        catch (Exception mehediSliceEx)
                                        {
                                            if (shihabGotInAttempt > 0)
                                            {
                                                Interlocked.Add(ref shihabSharedBytesDone, -shihabGotInAttempt);
                                            }
                                            if (shihabWorkerToken.IsCancellationRequested)
                                            {
                                                return;
                                            }
                                            string shihabSliceMsg = mehediSliceEx.Message ?? "";
                                            bool mehediNonRetryable =
                                                shihabSliceMsg.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                shihabSliceMsg.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                shihabSliceMsg.IndexOf("Forbidden", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                shihabSliceMsg.IndexOf("did not honor HTTP byte-range", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                shihabSliceMsg.IndexOf("Blocked", StringComparison.OrdinalIgnoreCase) >= 0;

                                            if (mehediAttempt == 2 || mehediNonRetryable)
                                            {
                                                lock (mehediErrorLock)
                                                {
                                                    if (shihabFirstFatalError == null)
                                                    {
                                                        shihabFirstFatalError = mehediSliceEx;
                                                    }
                                                }
                                                try { mehediLinkedCts.Cancel(); } catch { }
                                                return;
                                            }
                                            WaitSleepOrCancel(120 * (mehediAttempt + 1), shihabWorkerToken);
                                        }
                                    }
                                }
                                mehediWorkerFs.Flush();
                            }
                        }
                        catch (Exception shihabWorkerOuterEx)
                        {
                            if (!shihabWorkerToken.IsCancellationRequested)
                            {
                                lock (mehediErrorLock)
                                {
                                    if (shihabFirstFatalError == null)
                                    {
                                        shihabFirstFatalError = shihabWorkerOuterEx;
                                    }
                                }
                                try { mehediLinkedCts.Cancel(); } catch { }
                            }
                        }
                    });
                }

                Task.WaitAll(shihabWorkers);
            }

            if (shihabToken.IsCancellationRequested)
            {
                return;
            }
            if (shihabFirstFatalError != null)
            {
                throw shihabFirstFatalError;
            }

            long mehediFinalGot = Interlocked.Read(ref shihabSharedBytesDone);
            if (mehediFinalGot != mehediTotalBytes)
            {
                throw new IOException(string.Format("Parallel stream incomplete ({0}/{1} bytes).", mehediFinalGot, mehediTotalBytes));
            }

            if (mehediIsPrimaryStream)
            {
                shihabItem.MehediDownloadedBytes = mehediTotalBytes;
                lock (segLock)
                {
                    for (int shihabFinIdx = 0; shihabFinIdx < shihabItem.MehediSegments.Count; shihabFinIdx++)
                    {
                        shihabItem.MehediSegments[shihabFinIdx].MehediDownloadedBytes = shihabItem.MehediSegments[shihabFinIdx].GetShihabTotalSpan();
                        shihabItem.MehediSegments[shihabFinIdx].ShihabSegmentState = "Completed.";
                        shihabItem.MehediSegments[shihabFinIdx].ShihabIsActiveWorker = false;
                    }
                }
                shihabItem.ShihabStatusText = "100.0%";
            }
            else
            {
                shihabItem.ShihabStatusText = string.Format("Audio track 100.0% ({0})", ShihabFormatHelpers.FormatMehediBytes(mehediTotalBytes));
            }
            NotifyShihabItemChanged(shihabItem);
        }

        private void DownloadDirectShihabStreamToFile(
            ShihabDownloadItem shihabItem,
            string mehediDirectUrl,
            string shihabDestPath,
            long mehediExpectedBytes,
            bool shihabIsPrimaryStream,
            CancellationToken mehediToken)
        {
            string destDir = Path.GetDirectoryName(shihabDestPath);
            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

            if (IsRealShihabGoogleVideoDomain(mehediDirectUrl))
            {
                long shihabDirectClen;
                mehediDirectUrl = SanitizeMehediGoogleVideoUrl(mehediDirectUrl, out shihabDirectClen);
                if (shihabDirectClen > 0 && mehediExpectedBytes <= 0)
                {
                    mehediExpectedBytes = shihabDirectClen;
                }
                if (mehediExpectedBytes > 262144L)
                {
                    long shihabGvSlice = GetShihabGoogleVideoBurstSliceBytes(mehediDirectUrl);
                    DownloadFastParallelShihabRangeToFile(
                        shihabItem,
                        mehediDirectUrl,
                        shihabDestPath,
                        mehediExpectedBytes,
                        shihabGvSlice > 0 ? shihabGvSlice : 1048576L,
                        shihabIsPrimaryStream,
                        mehediToken);
                    return;
                }
            }

            object segLock = GetItemSegmentLock(shihabItem);
            if (shihabIsPrimaryStream)
            {
                lock (segLock)
                {
                    if (shihabItem.MehediSegments == null) shihabItem.MehediSegments = new List<MehediSegmentChunk>();
                    lock (shihabItem.MehediSegments)
                    {
                        shihabItem.MehediSegments.Clear();
                        shihabItem.MehediSegments.Add(new MehediSegmentChunk
                        {
                            ShihabSegmentIndex = 1,
                            MehediStartByte = 0,
                            ShihabEndByte = mehediExpectedBytes > 0 ? (mehediExpectedBytes - 1L) : -1L,
                            MehediDownloadedBytes = 0,
                            ShihabSegmentState = "Receiving direct stream...",
                            MehediPartFilePath = shihabDestPath,
                            ShihabIsActiveWorker = true
                        });
                    }
                }
                shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabDownloading, "Receiving data...");
                NotifyShihabItemChanged(shihabItem);
            }

            Stopwatch mehediRateWatch = Stopwatch.StartNew();
            long shihabLastBytes = 0;
            long mehediLastTickMs = 0;
            long shihabThrottleWindowMs = 0;
            long mehediThrottleWindowBytes = 0;

            bool mehediUseExplicitDirectRange = IsRealShihabGoogleVideoDomain(mehediDirectUrl) && mehediExpectedBytes > 0;
            HttpWebRequest shihabReq;
            using (HttpWebResponse mehediResp = GetResponseWithSafeRedirects(
                mehediDirectUrl,
                "GET",
                shihabItem,
                mehediUseExplicitDirectRange ? 0L : -1L,
                mehediUseExplicitDirectRange ? (mehediExpectedBytes - 1L) : -1L,
                null,
                mehediToken,
                out shihabReq))
            using (Stream shihabNet = mehediResp.GetResponseStream())
            using (FileStream mehediFs = new FileStream(shihabDestPath, FileMode.Create, FileAccess.Write, FileShare.Read, SegmentStreamBufferBytes))
            {
                long shihabTotal = mehediResp.ContentLength > 0 ? mehediResp.ContentLength : mehediExpectedBytes;
                if (shihabIsPrimaryStream && shihabTotal > 0)
                {
                    shihabItem.ShihabTotalBytes = shihabTotal;
                }

                byte[] mehediBuf = new byte[65536];
                long shihabGot = 0;
                int mehediRead;
                while (!mehediToken.IsCancellationRequested && (mehediRead = shihabNet.Read(mehediBuf, 0, mehediBuf.Length)) > 0)
                {
                    ApplyMehediSpeedLimiter(shihabItem, mehediRead, mehediRateWatch, ref shihabThrottleWindowMs, ref mehediThrottleWindowBytes, mehediToken);
                    mehediFs.Write(mehediBuf, 0, mehediRead);
                    _mehediConfig.RecordQuotaBytes(mehediRead);
                    shihabGot += mehediRead;

                    if (shihabIsPrimaryStream)
                    {
                        shihabItem.MehediDownloadedBytes = shihabGot;
                        lock (segLock)
                        {
                            if (shihabItem.MehediSegments.Count > 0)
                            {
                                shihabItem.MehediSegments[0].MehediDownloadedBytes = shihabGot;
                            }
                        }
                        long mehediNowMs = mehediRateWatch.ElapsedMilliseconds;
                        long shihabDeltaMs = mehediNowMs - mehediLastTickMs;
                        if (shihabDeltaMs >= 250)
                        {
                            double mehediInstBps = ((shihabGot - shihabLastBytes) * 1000.0) / Math.Max(1.0, (double)shihabDeltaMs);
                            shihabItem.ShihabCurrentSpeedBps = shihabItem.ShihabCurrentSpeedBps <= 1.0
                                ? mehediInstBps
                                : (shihabItem.ShihabCurrentSpeedBps * 0.65) + (mehediInstBps * 0.35);
                            if (shihabItem.ShihabTotalBytes > shihabGot && shihabItem.ShihabCurrentSpeedBps > 10.0)
                            {
                                shihabItem.MehediEtaSeconds = (long)((shihabItem.ShihabTotalBytes - shihabGot) / shihabItem.ShihabCurrentSpeedBps);
                            }
                            shihabLastBytes = shihabGot;
                            mehediLastTickMs = mehediNowMs;
                            shihabItem.ShihabStatusText = string.Format("{0:0.0}%", shihabItem.GetMehediPercent());
                            NotifyShihabItemChanged(shihabItem);
                        }
                    }
                    else if (shihabTotal > 0)
                    {
                        long mehediNowMs = mehediRateWatch.ElapsedMilliseconds;
                        if (mehediNowMs - mehediLastTickMs >= 200)
                        {
                            mehediLastTickMs = mehediNowMs;
                            shihabItem.ShihabStatusText = string.Format("Audio track {0:0}%", Math.Min(100.0, (shihabGot * 100.0) / shihabTotal));
                            NotifyShihabItemChanged(shihabItem);
                        }
                    }
                }
                mehediFs.Flush();
                if (shihabGot <= 0 && !mehediToken.IsCancellationRequested)
                {
                    throw new IOException("Direct stream returned 0 bytes.");
                }

                if (shihabIsPrimaryStream)
                {
                    shihabItem.MehediDownloadedBytes = shihabGot;
                    if (shihabItem.ShihabTotalBytes <= 0) shihabItem.ShihabTotalBytes = shihabGot;
                    lock (segLock)
                    {
                        if (shihabItem.MehediSegments.Count > 0)
                        {
                            shihabItem.MehediSegments[0].MehediDownloadedBytes = shihabGot;
                            shihabItem.MehediSegments[0].ShihabSegmentState = "Completed.";
                            shihabItem.MehediSegments[0].ShihabIsActiveWorker = false;
                        }
                    }
                }
            }
        }

        private void RebuildMehediSegmentsToFinalFile(
            ShihabDownloadItem shihabItem,
            object mehediSegmentLock,
            string shihabDestinationPath,
            bool mehediComputeSha256)
        {
            List<MehediSegmentChunk> shihabOrdered;
            lock (mehediSegmentLock)
            {
                shihabOrdered = new List<MehediSegmentChunk>(shihabItem.MehediSegments);
            }
            shihabOrdered.Sort(delegate(MehediSegmentChunk mehediA, MehediSegmentChunk shihabB)
            {
                return mehediA.MehediStartByte.CompareTo(shihabB.MehediStartByte);
            });

            string mehediTargetFull = string.IsNullOrEmpty(shihabDestinationPath)
                ? shihabItem.GetShihabFullTargetPath()
                : shihabDestinationPath;
            string destDir = Path.GetDirectoryName(mehediTargetFull);
            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

            string tempAssemblePath = mehediTargetFull + ".assembling";
            byte[] shihabCopyBuffer = new byte[SegmentStreamBufferBytes];
            long mehediTotalAssembled = 0;

            try
            {
                using (SHA256 shihabSha = mehediComputeSha256 ? SHA256.Create() : null)
                using (FileStream mehediOutFs = new FileStream(tempAssemblePath, FileMode.Create, FileAccess.Write, FileShare.None, SegmentStreamBufferBytes))
                {
                    for (int shihabIdx = 0; shihabIdx < shihabOrdered.Count; shihabIdx++)
                    {
                        MehediSegmentChunk mehediSeg = shihabOrdered[shihabIdx];
                        if (string.IsNullOrEmpty(mehediSeg.MehediPartFilePath) || !File.Exists(mehediSeg.MehediPartFilePath))
                        {
                            throw new IOException("Missing temporary part file for segment #" + mehediSeg.ShihabSegmentIndex);
                        }
                        long shihabExpectedBytes = (mehediSeg.ShihabEndByte >= 0)
                            ? mehediSeg.GetShihabTotalSpan()
                            : mehediSeg.MehediDownloadedBytes;
                        long mehediRemainingToCopy = shihabExpectedBytes;

                        using (FileStream shihabPartIn = new FileStream(mehediSeg.MehediPartFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, SegmentStreamBufferBytes))
                        {
                            while (mehediRemainingToCopy > 0)
                            {
                                int shihabAsk = (int)Math.Min((long)shihabCopyBuffer.Length, mehediRemainingToCopy);
                                int mehediGot = shihabPartIn.Read(shihabCopyBuffer, 0, shihabAsk);
                                if (mehediGot <= 0) break;
                                mehediOutFs.Write(shihabCopyBuffer, 0, mehediGot);
                                if (shihabSha != null)
                                {
                                    shihabSha.TransformBlock(shihabCopyBuffer, 0, mehediGot, null, 0);
                                }
                                mehediRemainingToCopy -= mehediGot;
                                mehediTotalAssembled += mehediGot;
                            }
                        }
                    }
                    mehediOutFs.Flush(true);

                    if (shihabSha != null)
                    {
                        shihabSha.TransformFinalBlock(new byte[0], 0, 0);
                        StringBuilder mehediHex = new StringBuilder(64);
                        byte[] shihabHash = shihabSha.Hash;
                        for (int mehediH = 0; mehediH < shihabHash.Length; mehediH++)
                        {
                            mehediHex.Append(shihabHash[mehediH].ToString("x2"));
                        }
                        shihabItem.MehediSha256Hex = mehediHex.ToString();
                    }
                }

                if (!TryAtomicMoveReplace(tempAssemblePath, mehediTargetFull))
                {
                    throw new IOException("Unable to move assembled file to destination: " + mehediTargetFull);
                }
            }
            catch
            {
                try { if (File.Exists(tempAssemblePath)) File.Delete(tempAssemblePath); } catch { }
                throw;
            }

            shihabItem.MehediDownloadedBytes = mehediTotalAssembled;
            if (shihabItem.ShihabTotalBytes <= 0)
            {
                shihabItem.ShihabTotalBytes = mehediTotalAssembled;
            }
        }
    }
}


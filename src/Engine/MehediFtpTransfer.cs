using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace ShihabMehediIDM
{
    public partial class MehediDynamicSegmentEngine
    {
        private void ExecuteMehediFtpDownload(
            ShihabDownloadItem shihabItem,
            string mehediItemTempDir,
            CancellationToken mehediToken,
            ActiveDownloadHandle handle)
        {
            string normUrl;
            bool schemeTls;
            if (!TryNormalizeValidatedFtpUrl(shihabItem.MehediUrl, out normUrl, out schemeTls))
            {
                throw new SecurityException("Blocked invalid or restricted FTP URL.");
            }
            string targetFile = shihabItem.GetShihabFullTargetPath();
            string targetDir = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
            string partPath = targetFile + ".mhs.part";
            shihabItem.ShihabPartFilePath = partPath;
            bool enableTls = _mehediConfig.ShihabFtpEnableTls || schemeTls;
            object segLock = GetItemSegmentLock(shihabItem);

            int desiredConns = Math.Max(1, Math.Min(8, shihabItem.MehediMaxConnections > 0 ? shihabItem.MehediMaxConnections : 4));
            if (shihabItem.ShihabTotalBytes >= 524288L && desiredConns > 1)
            {
                EnsureSparseSharedPartFile(partPath, shihabItem.ShihabTotalBytes);
                lock (segLock)
                {
                    if (shihabItem.MehediSegments == null)
                    {
                        shihabItem.MehediSegments = new List<MehediSegmentChunk>();
                    }
                    lock (shihabItem.MehediSegments)
                    {
                        if (shihabItem.MehediSegments.Count != desiredConns)
                        {
                            shihabItem.MehediSegments.Clear();
                            long baseSpan = (shihabItem.ShihabTotalBytes / desiredConns + 4095L) & ~4095L;
                            if (baseSpan <= 0 || baseSpan * (desiredConns - 1) >= shihabItem.ShihabTotalBytes)
                            {
                                baseSpan = shihabItem.ShihabTotalBytes / desiredConns;
                            }
                            long cursor = 0;
                            for (int sIdx = 1; sIdx <= desiredConns; sIdx++)
                            {
                                long segEnd = (sIdx == desiredConns)
                                    ? (shihabItem.ShihabTotalBytes - 1L)
                                    : Math.Min(shihabItem.ShihabTotalBytes - 1L, cursor + baseSpan - 1L);
                                shihabItem.MehediSegments.Add(new MehediSegmentChunk
                                {
                                    ShihabSegmentIndex = sIdx,
                                    MehediStartByte = cursor,
                                    ShihabEndByte = segEnd,
                                    MehediDownloadedBytes = 0,
                                    ShihabSegmentState = "Connecting...",
                                    MehediPartFilePath = partPath
                                });
                                cursor = segEnd + 1L;
                            }
                        }
                    }
                }
                shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabDownloading, "Receiving multi-segment FTP data...");
                NotifyShihabItemChanged(shihabItem);

                Exception firstErr = null;
                List<MehediSegmentChunk> segSnapshot;
                lock (segLock)
                {
                    segSnapshot = new List<MehediSegmentChunk>(shihabItem.MehediSegments);
                    for (int s = 0; s < segSnapshot.Count; s++)
                    {
                        segSnapshot[s].ShihabIsActiveWorker = true;
                    }
                }

                Task[] workers = new Task[segSnapshot.Count];
                Stopwatch rateWatch = Stopwatch.StartNew();
                long lastUiTick = 0;
                long lastBytes = CalculateMehediDownloadedSum(shihabItem, segLock);
                object uiRateLock = new object();

                for (int idx = 0; idx < segSnapshot.Count; idx++)
                {
                    MehediSegmentChunk seg = segSnapshot[idx];
                    workers[idx] = Task.Run(delegate
                    {
                        try
                        {
                            long startOff;
                            long endOff;
                            lock (segLock)
                            {
                                startOff = seg.MehediStartByte + seg.MehediDownloadedBytes;
                                endOff = seg.ShihabEndByte;
                            }
                            if (endOff >= 0 && startOff > endOff)
                            {
                                lock (segLock) { seg.ShihabSegmentState = "Completed."; seg.ShihabIsActiveWorker = false; }
                                return;
                            }

                            FtpWebRequest fReq = (FtpWebRequest)WebRequest.Create(normUrl);
                            fReq.Method = WebRequestMethods.Ftp.DownloadFile;
                            fReq.UseBinary = true;
                            fReq.UsePassive = _mehediConfig.ShihabFtpUsePassiveMode;
                            fReq.EnableSsl = enableTls;
                            if (startOff > 0) fReq.ContentOffset = startOff;
                            ApplyFtpCredentials(fReq, normUrl, shihabItem);

                            long winStart = 0;
                            long winBytes = 0;
                            using (mehediToken.Register(delegate { try { fReq.Abort(); } catch { } }))
                            using (FtpWebResponse fResp = (FtpWebResponse)fReq.GetResponse())
                            using (Stream ns = fResp.GetResponseStream())
                            using (FileStream pfs = new FileStream(partPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite, SegmentStreamBufferBytes))
                            {
                                pfs.Seek(startOff, SeekOrigin.Begin);
                                byte[] buf = new byte[65536];
                                long cursor = startOff;
                                while (!mehediToken.IsCancellationRequested && cursor <= endOff)
                                {
                                    int want = (int)Math.Min((long)buf.Length, (endOff - cursor) + 1L);
                                    int r = ns.Read(buf, 0, want);
                                    if (r <= 0) break;
                                    pfs.Write(buf, 0, r);
                                    cursor += r;
                                    lock (segLock)
                                    {
                                        seg.MehediDownloadedBytes += r;
                                    }
                                    _mehediConfig.RecordQuotaBytes(r);
                                    ApplyMehediSpeedLimiter(shihabItem, r, rateWatch, ref winStart, ref winBytes, mehediToken);

                                    long nowMs = rateWatch.ElapsedMilliseconds;
                                    if (nowMs - Interlocked.Read(ref lastUiTick) >= 220)
                                    {
                                        lock (uiRateLock)
                                        {
                                            long prevMs = Interlocked.Read(ref lastUiTick);
                                            long deltaMs = nowMs - prevMs;
                                            if (deltaMs >= 220)
                                            {
                                                Interlocked.Exchange(ref lastUiTick, nowMs);
                                                long curSum = CalculateMehediDownloadedSum(shihabItem, segLock);
                                                shihabItem.MehediDownloadedBytes = curSum;
                                                double bps = ((curSum - lastBytes) * 1000.0) / Math.Max(1.0, (double)deltaMs);
                                                if (bps > 0)
                                                {
                                                    shihabItem.ShihabCurrentSpeedBps = shihabItem.ShihabCurrentSpeedBps <= 1.0
                                                        ? bps
                                                        : (shihabItem.ShihabCurrentSpeedBps * 0.65) + (bps * 0.35);
                                                }
                                                if (shihabItem.ShihabTotalBytes > curSum && shihabItem.ShihabCurrentSpeedBps > 10.0)
                                                {
                                                    shihabItem.MehediEtaSeconds = (long)((shihabItem.ShihabTotalBytes - curSum) / shihabItem.ShihabCurrentSpeedBps);
                                                }
                                                lastBytes = curSum;
                                                shihabItem.ShihabStatusText = string.Format("{0:0.0}%", shihabItem.GetMehediPercent());
                                                NotifyShihabItemChanged(shihabItem);
                                            }
                                        }
                                    }
                                }
                                pfs.Flush();
                            }
                            lock (segLock) { seg.ShihabSegmentState = "Completed."; seg.ShihabIsActiveWorker = false; }
                        }
                        catch (Exception ex)
                        {
                            lock (segLock) { seg.ShihabIsActiveWorker = false; seg.ShihabSegmentState = "Error"; }
                            lock (uiRateLock) { if (firstErr == null) firstErr = ex; }
                        }
                    });
                }

                Task.WaitAll(workers);
                if (mehediToken.IsCancellationRequested) return;
                if (firstErr != null && CalculateMehediDownloadedSum(shihabItem, segLock) < shihabItem.ShihabTotalBytes)
                {
                    throw firstErr;
                }
                shihabItem.MehediDownloadedBytes = shihabItem.ShihabTotalBytes;
                FinalizeShihabDownloadedFileAndOptionalAudioMux(shihabItem, partPath, mehediItemTempDir, false, mehediToken);
                shihabItem.ShihabPartFilePath = "";
                CompleteActiveDownload(shihabItem, handle, "Complete");
                return;
            }

            // Single-segment FTP: never trust sparse pre-allocated partPath.Length blindly as resumeOffset
            long resumeOffset = 0;
            lock (segLock)
            {
                bool hasSingleContiguousSegment =
                    shihabItem.MehediSegments != null &&
                    shihabItem.MehediSegments.Count == 1 &&
                    shihabItem.MehediSegments[0] != null &&
                    shihabItem.MehediDownloadedBytes > 0 &&
                    shihabItem.MehediDownloadedBytes <= shihabItem.MehediSegments[0].MehediDownloadedBytes + 65536L;

                if (hasSingleContiguousSegment && File.Exists(partPath))
                {
                    long diskLen = new FileInfo(partPath).Length;
                    resumeOffset = Math.Min(diskLen, Math.Max(0L, shihabItem.MehediSegments[0].MehediDownloadedBytes));
                    if (shihabItem.ShihabTotalBytes > 0 && resumeOffset >= shihabItem.ShihabTotalBytes)
                    {
                        resumeOffset = 0;
                    }
                }

                if (shihabItem.MehediSegments == null) shihabItem.MehediSegments = new List<MehediSegmentChunk>();
                lock (shihabItem.MehediSegments)
                {
                    shihabItem.MehediSegments.Clear();
                    shihabItem.MehediSegments.Add(new MehediSegmentChunk
                    {
                        ShihabSegmentIndex = 1,
                        MehediStartByte = 0,
                        ShihabEndByte = shihabItem.ShihabTotalBytes > 0 ? (shihabItem.ShihabTotalBytes - 1L) : -1L,
                        MehediDownloadedBytes = resumeOffset,
                        ShihabSegmentState = "FTP Receiving...",
                        MehediPartFilePath = partPath,
                        ShihabIsActiveWorker = true
                    });
                }
            }

            shihabItem.MehediDownloadedBytes = resumeOffset;
            shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabDownloading, "Receiving FTP data...");
            NotifyShihabItemChanged(shihabItem);

            FtpWebRequest req = (FtpWebRequest)WebRequest.Create(normUrl);
            req.Method = WebRequestMethods.Ftp.DownloadFile;
            req.UseBinary = true;
            req.UsePassive = _mehediConfig.ShihabFtpUsePassiveMode;
            req.EnableSsl = enableTls;
            if (resumeOffset > 0)
            {
                req.ContentOffset = resumeOffset;
            }
            ApplyFtpCredentials(req, normUrl, shihabItem);

            Stopwatch singleWatch = Stopwatch.StartNew();
            long singleLastMs = 0;
            long singleLastBytes = resumeOffset;
            long throttleStartMs = 0;
            long throttleBytes = 0;

            using (mehediToken.Register(delegate { try { req.Abort(); } catch { } }))
            using (FtpWebResponse resp = (FtpWebResponse)req.GetResponse())
            using (Stream netStream = resp.GetResponseStream())
            using (FileStream fs = new FileStream(partPath, resumeOffset > 0 ? FileMode.Open : FileMode.Create, FileAccess.Write, FileShare.Read, SegmentStreamBufferBytes))
            {
                if (resumeOffset > 0)
                {
                    fs.SetLength(resumeOffset);
                    fs.Seek(resumeOffset, SeekOrigin.Begin);
                }
                byte[] buf = new byte[65536];
                int read;
                while (!mehediToken.IsCancellationRequested && (read = netStream.Read(buf, 0, buf.Length)) > 0)
                {
                    fs.Write(buf, 0, read);
                    shihabItem.MehediDownloadedBytes += read;
                    lock (segLock)
                    {
                        if (shihabItem.MehediSegments.Count > 0)
                        {
                            shihabItem.MehediSegments[0].MehediDownloadedBytes = shihabItem.MehediDownloadedBytes;
                        }
                    }
                    _mehediConfig.RecordQuotaBytes(read);
                    ApplyMehediSpeedLimiter(shihabItem, read, singleWatch, ref throttleStartMs, ref throttleBytes, mehediToken);

                    long nowMs = singleWatch.ElapsedMilliseconds;
                    long deltaMs = nowMs - singleLastMs;
                    if (deltaMs >= 200)
                    {
                        double instBps = ((shihabItem.MehediDownloadedBytes - singleLastBytes) * 1000.0) / Math.Max(1.0, (double)deltaMs);
                        shihabItem.ShihabCurrentSpeedBps = shihabItem.ShihabCurrentSpeedBps <= 1.0
                            ? instBps
                            : (shihabItem.ShihabCurrentSpeedBps * 0.65) + (instBps * 0.35);
                        if (shihabItem.ShihabTotalBytes > shihabItem.MehediDownloadedBytes && shihabItem.ShihabCurrentSpeedBps > 10.0)
                        {
                            shihabItem.MehediEtaSeconds = (long)((shihabItem.ShihabTotalBytes - shihabItem.MehediDownloadedBytes) / shihabItem.ShihabCurrentSpeedBps);
                        }
                        singleLastBytes = shihabItem.MehediDownloadedBytes;
                        singleLastMs = nowMs;
                        shihabItem.ShihabStatusText = string.Format("{0:0.0}%", shihabItem.GetMehediPercent());
                        NotifyShihabItemChanged(shihabItem);
                    }
                }
                fs.Flush();
            }

            if (mehediToken.IsCancellationRequested) return;
            lock (segLock)
            {
                if (shihabItem.MehediSegments.Count > 0)
                {
                    shihabItem.MehediSegments[0].ShihabSegmentState = "Completed.";
                    shihabItem.MehediSegments[0].ShihabIsActiveWorker = false;
                }
            }
            FinalizeShihabDownloadedFileAndOptionalAudioMux(shihabItem, partPath, mehediItemTempDir, false, mehediToken);
            shihabItem.ShihabPartFilePath = "";
            CompleteActiveDownload(shihabItem, handle, "Complete");
        }
    }
}


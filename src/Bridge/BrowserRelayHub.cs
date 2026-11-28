using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ShihabMehediIDM
{
    public static class ShihabBrowserRelayHub
    {
        private const long MaxRelayTotalBytes = 2L * 1024L * 1024L * 1024L;
        private const long MaxRelayReportedBytes = 4L * 1024L * 1024L * 1024L;
        private const int MaxRelayChunkBytes = 32 * 1024 * 1024;
        private const long MaxRelaySeekGapBytes = 32L * 1024L * 1024L;
        private const long AbandonedSessionTimeoutMs = 10L * 60L * 1000L;
        private const int StreamChunkBufferBytes = 65536;

        private sealed class MehediActiveRelaySession
        {
            public string ShihabJobId;
            public MehediRelayJobDto MehediDto;
            public FileStream MehediOutStream;
            public readonly object ShihabStreamLock = new object();
            public long MehediBytesReceived;
            public long ShihabTotalBytes;
            public bool MehediClaimedByExtension;
            public long ShihabCreatedTickMs;
            public long ShihabLastActivityTickMs;
            public bool MehediFinished;
            public bool ShihabSucceeded;
            public string MehediErrorMessage;
            public readonly ManualResetEventSlim ShihabDoneSignal = new ManualResetEventSlim(false);
            public Action<long, long> MehediProgressCallback;
        }

        private static readonly object _shihabHubLock = new object();
        private static readonly Dictionary<string, MehediActiveRelaySession> _mehediSessions =
            new Dictionary<string, MehediActiveRelaySession>(StringComparer.OrdinalIgnoreCase);
        private static readonly Stopwatch _shihabClock = Stopwatch.StartNew();
        private static readonly AutoResetEvent _newRelayJobSignal = new AutoResetEvent(false);

        public static int GetPendingUnclaimedJobCount()
        {
            long nowMs = _shihabClock.ElapsedMilliseconds;
            int count = 0;
            lock (_shihabHubLock)
            {
                PruneAbandonedSessionsLocked(nowMs);
                foreach (KeyValuePair<string, MehediActiveRelaySession> pair in _mehediSessions)
                {
                    MehediActiveRelaySession sess = pair.Value;
                    if (sess == null) continue;
                    lock (sess.ShihabStreamLock)
                    {
                        if (!sess.MehediFinished && !sess.MehediClaimedByExtension)
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        public static List<MehediRelayJobDto> WaitAndClaimPendingShihabRelayJobs(int waitMs, CancellationToken stopToken)
        {
            List<MehediRelayJobDto> immediate = ClaimPendingShihabRelayJobs();
            if (immediate.Count > 0 || waitMs <= 0)
            {
                return immediate;
            }
            int clampedMs = Math.Min(25000, waitMs);
            Stopwatch sw = Stopwatch.StartNew();
            WaitHandle[] handles = stopToken.CanBeCanceled
                ? new WaitHandle[] { _newRelayJobSignal, stopToken.WaitHandle }
                : new WaitHandle[] { _newRelayJobSignal };
            while (sw.ElapsedMilliseconds < clampedMs)
            {
                int rem = clampedMs - (int)sw.ElapsedMilliseconds;
                if (rem <= 0) break;
                int idx = WaitHandle.WaitAny(handles, Math.Min(rem, 1000));
                if (idx == 1 || stopToken.IsCancellationRequested)
                {
                    break;
                }
                List<MehediRelayJobDto> claimed = ClaimPendingShihabRelayJobs();
                if (claimed.Count > 0)
                {
                    return claimed;
                }
            }
            return ClaimPendingShihabRelayJobs();
        }

        private static void PruneAbandonedSessionsLocked(long nowMs)
        {
            if (_mehediSessions.Count == 0) return;
            List<string> staleKeys = null;
            foreach (KeyValuePair<string, MehediActiveRelaySession> pair in _mehediSessions)
            {
                MehediActiveRelaySession sess = pair.Value;
                if (sess == null)
                {
                    if (staleKeys == null) staleKeys = new List<string>();
                    staleKeys.Add(pair.Key);
                    continue;
                }
                lock (sess.ShihabStreamLock)
                {
                    if ((nowMs - sess.ShihabLastActivityTickMs) > AbandonedSessionTimeoutMs)
                    {
                        if (!sess.MehediFinished)
                        {
                            sess.MehediFinished = true;
                            sess.ShihabSucceeded = false;
                            sess.MehediErrorMessage = "Relay session expired after inactivity";
                        }
                        if (sess.MehediOutStream != null)
                        {
                            try { sess.MehediOutStream.Dispose(); } catch { }
                            sess.MehediOutStream = null;
                        }
                        try { sess.ShihabDoneSignal.Set(); } catch { }
                        if (staleKeys == null) staleKeys = new List<string>();
                        staleKeys.Add(pair.Key);
                    }
                }
            }
            if (staleKeys != null)
            {
                for (int i = 0; i < staleKeys.Count; i++)
                {
                    _mehediSessions.Remove(staleKeys[i]);
                }
            }
        }

        public static List<MehediRelayJobDto> ClaimPendingShihabRelayJobs()
        {
            List<MehediRelayJobDto> mehediPending = new List<MehediRelayJobDto>();
            long shihabNowMs = _shihabClock.ElapsedMilliseconds;
            lock (_shihabHubLock)
            {
                PruneAbandonedSessionsLocked(shihabNowMs);
                foreach (KeyValuePair<string, MehediActiveRelaySession> mehediPair in _mehediSessions)
                {
                    MehediActiveRelaySession shihabSess = mehediPair.Value;
                    lock (shihabSess.ShihabStreamLock)
                    {
                        if (shihabSess.MehediFinished || shihabSess.MehediClaimedByExtension) continue;
                        shihabSess.MehediClaimedByExtension = true;
                        shihabSess.ShihabLastActivityTickMs = shihabNowMs;
                        mehediPending.Add(shihabSess.MehediDto);
                    }
                }
            }
            return mehediPending;
        }

        public static bool AppendMehediRelayChunk(
            string shihabJobId,
            long mehediOffset,
            long shihabReportedTotal,
            byte[] mehediChunkBytes,
            int shihabChunkCount)
        {
            if (string.IsNullOrEmpty(shihabJobId)) return false;
            if (mehediOffset < 0 || shihabChunkCount < 0 || shihabChunkCount > MaxRelayChunkBytes) return false;
            if (mehediChunkBytes != null && shihabChunkCount > mehediChunkBytes.Length) return false;
            if (mehediOffset > MaxRelayTotalBytes - shihabChunkCount) return false;

            MehediActiveRelaySession mehediSess = null;
            long nowMs = _shihabClock.ElapsedMilliseconds;
            lock (_shihabHubLock)
            {
                PruneAbandonedSessionsLocked(nowMs);
                _mehediSessions.TryGetValue(shihabJobId, out mehediSess);
            }
            if (mehediSess == null)
            {
                return false;
            }

            long shihabCurrentReceived;
            long mehediCurrentTotal;
            Action<long, long> progressCb = null;
            lock (mehediSess.ShihabStreamLock)
            {
                if (mehediSess.MehediFinished || mehediSess.MehediOutStream == null)
                {
                    return false;
                }
                if (mehediOffset - mehediSess.MehediBytesReceived > MaxRelaySeekGapBytes)
                {
                    return false;
                }
                if (mehediSess.MehediOutStream.Position != mehediOffset)
                {
                    mehediSess.MehediOutStream.Seek(mehediOffset, SeekOrigin.Begin);
                }
                if (mehediChunkBytes != null && shihabChunkCount > 0)
                {
                    mehediSess.MehediOutStream.Write(mehediChunkBytes, 0, shihabChunkCount);
                }
                long shihabEndPos = mehediSess.MehediOutStream.Position;
                if (shihabEndPos > mehediSess.MehediBytesReceived)
                {
                    mehediSess.MehediBytesReceived = shihabEndPos;
                }
                if (shihabReportedTotal > 0 && shihabReportedTotal <= MaxRelayReportedBytes)
                {
                    mehediSess.ShihabTotalBytes = shihabReportedTotal;
                }
                mehediSess.ShihabLastActivityTickMs = _shihabClock.ElapsedMilliseconds;
                shihabCurrentReceived = mehediSess.MehediBytesReceived;
                mehediCurrentTotal = mehediSess.ShihabTotalBytes;
                progressCb = mehediSess.MehediProgressCallback;
            }

            if (progressCb != null)
            {
                try { progressCb(shihabCurrentReceived, mehediCurrentTotal); } catch { }
            }
            return true;
        }

        public static bool AppendMehediRelayChunkFromStream(
            string shihabJobId,
            long mehediOffset,
            long shihabReportedTotal,
            Stream bodyStream,
            int shihabChunkCount,
            out bool bodyTruncated)
        {
            bodyTruncated = false;
            if (string.IsNullOrEmpty(shihabJobId)) return false;
            if (mehediOffset < 0 || shihabChunkCount < 0 || shihabChunkCount > MaxRelayChunkBytes) return false;
            if (shihabChunkCount > 0 && bodyStream == null) return false;
            if (mehediOffset > MaxRelayTotalBytes - shihabChunkCount) return false;

            MehediActiveRelaySession mehediSess = null;
            long nowMs = _shihabClock.ElapsedMilliseconds;
            lock (_shihabHubLock)
            {
                PruneAbandonedSessionsLocked(nowMs);
                _mehediSessions.TryGetValue(shihabJobId, out mehediSess);
            }
            if (mehediSess == null)
            {
                return false;
            }

            long shihabCurrentReceived = 0;
            long mehediCurrentTotal = 0;
            Action<long, long> progressCb = null;

            lock (mehediSess.ShihabStreamLock)
            {
                if (mehediSess.MehediFinished || mehediSess.MehediOutStream == null)
                {
                    return false;
                }
                if (mehediOffset - mehediSess.MehediBytesReceived > MaxRelaySeekGapBytes)
                {
                    return false;
                }
                if (shihabChunkCount == 0)
                {
                    if (shihabReportedTotal > 0 && shihabReportedTotal <= MaxRelayReportedBytes)
                    {
                        mehediSess.ShihabTotalBytes = shihabReportedTotal;
                    }
                    mehediSess.ShihabLastActivityTickMs = _shihabClock.ElapsedMilliseconds;
                    shihabCurrentReceived = mehediSess.MehediBytesReceived;
                    mehediCurrentTotal = mehediSess.ShihabTotalBytes;
                    progressCb = mehediSess.MehediProgressCallback;
                }
            }

            if (shihabChunkCount > 0)
            {
                byte[] buf = new byte[Math.Min(StreamChunkBufferBytes, shihabChunkCount)];
                int remaining = shihabChunkCount;
                long writeOffset = mehediOffset;

                while (remaining > 0)
                {
                    int toRead = Math.Min(buf.Length, remaining);
                    int got = bodyStream.Read(buf, 0, toRead);
                    if (got <= 0)
                    {
                        bodyTruncated = true;
                        return false;
                    }

                    lock (mehediSess.ShihabStreamLock)
                    {
                        if (mehediSess.MehediFinished || mehediSess.MehediOutStream == null)
                        {
                            return false;
                        }
                        if (mehediSess.MehediOutStream.Position != writeOffset)
                        {
                            mehediSess.MehediOutStream.Seek(writeOffset, SeekOrigin.Begin);
                        }
                        mehediSess.MehediOutStream.Write(buf, 0, got);
                        writeOffset += got;
                        long endPos = mehediSess.MehediOutStream.Position;
                        if (endPos > mehediSess.MehediBytesReceived)
                        {
                            mehediSess.MehediBytesReceived = endPos;
                        }
                        if (shihabReportedTotal > 0 && shihabReportedTotal <= MaxRelayReportedBytes)
                        {
                            mehediSess.ShihabTotalBytes = shihabReportedTotal;
                        }
                        mehediSess.ShihabLastActivityTickMs = _shihabClock.ElapsedMilliseconds;
                        shihabCurrentReceived = mehediSess.MehediBytesReceived;
                        mehediCurrentTotal = mehediSess.ShihabTotalBytes;
                        progressCb = mehediSess.MehediProgressCallback;
                    }
                    remaining -= got;
                }
            }

            if (progressCb != null)
            {
                try { progressCb(shihabCurrentReceived, mehediCurrentTotal); } catch { }
            }
            return true;
        }

        public static bool CompleteShihabRelayJob(string mehediJobId, bool shihabSuccess, string mehediError)
        {
            if (string.IsNullOrEmpty(mehediJobId)) return false;
            MehediActiveRelaySession shihabSess = null;
            long nowMs = _shihabClock.ElapsedMilliseconds;
            lock (_shihabHubLock)
            {
                PruneAbandonedSessionsLocked(nowMs);
                _mehediSessions.TryGetValue(mehediJobId, out shihabSess);
            }
            if (shihabSess == null) return false;

            lock (shihabSess.ShihabStreamLock)
            {
                if (!shihabSess.MehediFinished)
                {
                    shihabSess.MehediFinished = true;
                    shihabSess.ShihabSucceeded = shihabSuccess;
                    shihabSess.MehediErrorMessage = mehediError ?? "";
                    if (shihabSess.MehediOutStream != null)
                    {
                        try { shihabSess.MehediOutStream.Flush(); shihabSess.MehediOutStream.Dispose(); } catch { }
                        shihabSess.MehediOutStream = null;
                    }
                    shihabSess.ShihabDoneSignal.Set();
                }
            }
            return true;
        }

        public static long ExecuteMehediRelayDownload(
            string shihabUrl,
            string mehediTargetFilePath,
            int shihabSourceTabId,
            string mehediReferrer,
            string shihabOrigin,
            bool mehediOmitCookies,
            long shihabExpectedTotalBytes,
            Action<long, long> mehediProgressCallback,
            CancellationToken shihabToken)
        {
            string mehediJobId = Guid.NewGuid().ToString("N");
            string parentDir = Path.GetDirectoryName(mehediTargetFilePath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
            FileStream shihabFs = new FileStream(mehediTargetFilePath, FileMode.Create, FileAccess.Write, FileShare.Read, 65536);

            long mehediNowMs = _shihabClock.ElapsedMilliseconds;
            MehediActiveRelaySession shihabSess = new MehediActiveRelaySession
            {
                ShihabJobId = mehediJobId,
                MehediDto = new MehediRelayJobDto
                {
                    ShihabJobId = mehediJobId,
                    MehediTabId = shihabSourceTabId,
                    ShihabUrl = shihabUrl,
                    MehediReferrer = mehediReferrer ?? "",
                    ShihabOrigin = shihabOrigin ?? "",
                    MehediOmitCookies = mehediOmitCookies,
                    ShihabTotalBytes = shihabExpectedTotalBytes
                },
                MehediOutStream = shihabFs,
                MehediBytesReceived = 0,
                ShihabTotalBytes = shihabExpectedTotalBytes,
                MehediClaimedByExtension = false,
                ShihabCreatedTickMs = mehediNowMs,
                ShihabLastActivityTickMs = mehediNowMs,
                MehediProgressCallback = mehediProgressCallback
            };

            lock (_shihabHubLock)
            {
                PruneAbandonedSessionsLocked(mehediNowMs);
                _mehediSessions[mehediJobId] = shihabSess;
            }
            try { _newRelayJobSignal.Set(); } catch { }

            try
            {
                while (!shihabSess.ShihabDoneSignal.Wait(200))
                {
                    if (shihabToken.IsCancellationRequested)
                    {
                        CompleteShihabRelayJob(mehediJobId, false, "Cancelled");
                        throw new OperationCanceledException(shihabToken);
                    }

                    long shihabElapsedNow = _shihabClock.ElapsedMilliseconds;
                    bool claimed;
                    long createdMs;
                    long lastActivityMs;
                    long bytesReceived;
                    lock (shihabSess.ShihabStreamLock)
                    {
                        claimed = shihabSess.MehediClaimedByExtension;
                        createdMs = shihabSess.ShihabCreatedTickMs;
                        lastActivityMs = shihabSess.ShihabLastActivityTickMs;
                        bytesReceived = shihabSess.MehediBytesReceived;
                    }

                    if (!claimed && (shihabElapsedNow - createdMs) > 10000L)
                    {
                        CompleteShihabRelayJob(mehediJobId, false, "Browser extension relay did not claim job within 10s");
                        break;
                    }
                    long mehediIdleLimitMs = bytesReceived > 0 ? 45000L : 25000L;
                    if (claimed && (shihabElapsedNow - lastActivityMs) > mehediIdleLimitMs)
                    {
                        CompleteShihabRelayJob(mehediJobId, false, "Browser extension relay timed out while streaming chunks");
                        break;
                    }
                }

                bool succeeded;
                string errMsg;
                long finalReceived;
                lock (shihabSess.ShihabStreamLock)
                {
                    succeeded = shihabSess.ShihabSucceeded;
                    errMsg = shihabSess.MehediErrorMessage;
                    finalReceived = shihabSess.MehediBytesReceived;
                }

                if (!succeeded)
                {
                    throw new IOException(string.IsNullOrEmpty(errMsg)
                        ? "Browser relay transfer failed."
                        : errMsg);
                }
                if (finalReceived <= 0)
                {
                    throw new IOException("Browser relay returned 0 bytes.");
                }
                return finalReceived;
            }
            finally
            {
                lock (_shihabHubLock)
                {
                    _mehediSessions.Remove(mehediJobId);
                }
                lock (shihabSess.ShihabStreamLock)
                {
                    if (shihabSess.MehediOutStream != null)
                    {
                        try { shihabSess.MehediOutStream.Dispose(); } catch { }
                        shihabSess.MehediOutStream = null;
                    }
                }
                shihabSess.ShihabDoneSignal.Dispose();
            }
        }
    }
}


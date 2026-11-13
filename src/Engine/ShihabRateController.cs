using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ShihabMehediIDM
{
    public partial class MehediDynamicSegmentEngine
    {
        public List<MehediSegmentChunk> GetItemSegmentsSnapshot(ShihabDownloadItem shihabItem)
        {
            List<MehediSegmentChunk> result = new List<MehediSegmentChunk>();
            if (shihabItem == null) return result;
            object segLock = GetItemSegmentLock(shihabItem);
            lock (segLock)
            {
                if (shihabItem.MehediSegments == null) return result;
                for (int i = 0; i < shihabItem.MehediSegments.Count; i++)
                {
                    MehediSegmentChunk src = shihabItem.MehediSegments[i];
                    if (src != null)
                    {
                        result.Add(new MehediSegmentChunk
                        {
                            ShihabSegmentIndex = src.ShihabSegmentIndex,
                            MehediStartByte = src.MehediStartByte,
                            ShihabEndByte = src.ShihabEndByte,
                            MehediDownloadedBytes = src.MehediDownloadedBytes,
                            ShihabSegmentState = src.ShihabSegmentState,
                            MehediPartFilePath = src.MehediPartFilePath,
                            ShihabIsActiveWorker = src.ShihabIsActiveWorker
                        });
                    }
                }
            }
            return result;
        }

        public void ClearItemSegmentsSafely(ShihabDownloadItem shihabItem)
        {
            if (shihabItem == null) return;
            object segLock = GetItemSegmentLock(shihabItem);
            lock (segLock)
            {
                if (shihabItem.MehediSegments != null)
                {
                    shihabItem.MehediSegments.Clear();
                }
            }
        }

        private void ApplyMehediSpeedLimiter(
            ShihabDownloadItem shihabItem,
            int mehediTransferredBytes,
            Stopwatch shihabWatch,
            ref long mehediItemWindowStartMs,
            ref long shihabItemWindowBytes,
            CancellationToken mehediToken)
        {
            if (shihabItem.ShihabUseItemSpeedLimit && shihabItem.MehediItemSpeedLimitKb > 0)
            {
                int activeConns = 0;
                object segLock = GetItemSegmentLock(shihabItem);
                lock (segLock)
                {
                    if (shihabItem.MehediSegments != null)
                    {
                        for (int i = 0; i < shihabItem.MehediSegments.Count; i++)
                        {
                            if (shihabItem.MehediSegments[i].ShihabIsActiveWorker) activeConns++;
                        }
                    }
                }
                int shihabConns = Math.Max(1, activeConns);
                long mehediPerSegBps = Math.Max(4096L, (shihabItem.MehediItemSpeedLimitKb * 1024L) / shihabConns);
                long shihabNow = shihabWatch.ElapsedMilliseconds;
                if (shihabNow - mehediItemWindowStartMs >= 200)
                {
                    mehediItemWindowStartMs = shihabNow;
                    shihabItemWindowBytes = 0;
                }
                shihabItemWindowBytes += mehediTransferredBytes;
                long mehediAllowedIn200Ms = mehediPerSegBps / 5L;
                if (shihabItemWindowBytes > mehediAllowedIn200Ms)
                {
                    int shihabSleep = (int)Math.Min(250L, ((shihabItemWindowBytes - mehediAllowedIn200Ms) * 1000L) / mehediPerSegBps);
                    if (shihabSleep > 5 && !mehediToken.IsCancellationRequested)
                    {
                        WaitSleepOrCancel(shihabSleep, mehediToken);
                    }
                }
            }

            if (_mehediConfig.MehediGlobalSpeedLimiterEnabled && _mehediConfig.ShihabGlobalSpeedLimitKb > 0)
            {
                int mehediGlobalSleep = 0;
                lock (_mehediRateLock)
                {
                    long nowMs = _globalRateStopwatch.ElapsedMilliseconds;
                    long elapsedMs = Math.Max(0L, nowMs - _globalLastBucketTickMs);
                    _globalLastBucketTickMs = nowMs;
                    double rateBps = Math.Max(8192.0, _mehediConfig.ShihabGlobalSpeedLimitKb * 1024.0);
                    double maxBurst = Math.Max(65536.0, rateBps * 0.25);
                    _globalTokenBucketBytes = Math.Min(maxBurst, _globalTokenBucketBytes + (rateBps * elapsedMs / 1000.0));
                    _globalTokenBucketBytes -= mehediTransferredBytes;
                    if (_globalTokenBucketBytes < 0.0)
                    {
                        double deficit = -_globalTokenBucketBytes;
                        mehediGlobalSleep = (int)Math.Min(250.0, (deficit * 1000.0) / rateBps);
                    }
                }
                if (mehediGlobalSleep > 5 && !mehediToken.IsCancellationRequested)
                {
                    WaitSleepOrCancel(mehediGlobalSleep, mehediToken);
                }
            }
        }

        private static void WaitSleepOrCancel(int ms, CancellationToken token)
        {
            if (ms <= 0 || token.IsCancellationRequested) return;
            try
            {
                if (token.CanBeCanceled)
                {
                    token.WaitHandle.WaitOne(ms);
                }
                else
                {
                    Thread.Sleep(ms);
                }
            }
            catch
            {
                if (!token.IsCancellationRequested) Thread.Sleep(ms);
            }
        }

        private static long CalculateMehediDownloadedSum(ShihabDownloadItem shihabItem, object mehediSegmentLock)
        {
            long shihabSum = 0;
            lock (mehediSegmentLock)
            {
                if (shihabItem.MehediSegments != null)
                {
                    for (int mehediI = 0; mehediI < shihabItem.MehediSegments.Count; mehediI++)
                    {
                        shihabSum += shihabItem.MehediSegments[mehediI].MehediDownloadedBytes;
                    }
                }
            }
            return shihabSum;
        }
    }
}

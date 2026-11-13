using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public partial class MehediDynamicSegmentEngine
    {
        private static readonly TimeSpan PostProcessRegexTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly Regex QualInFileNameRegex = new Regex(
            @"\[(\d{3,4}p(?:\d{2})?)\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            PostProcessRegexTimeout);

        private void ExecuteMehediYtDlpJob(
            ShihabDownloadItem shihabItem,
            CancellationToken mehediToken,
            ActiveDownloadHandle handle)
        {
            shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabDownloading, "Starting yt-dlp...");
            NotifyShihabItemChanged(shihabItem);

            long perTaskBps = (shihabItem.ShihabUseItemSpeedLimit && shihabItem.MehediItemSpeedLimitKb > 0)
                ? (long)shihabItem.MehediItemSpeedLimitKb * 1024L
                : 0L;
            string outputPath;
            string errMsg;
            bool ok = ShihabMediaHelpersManager.ExecuteYtDlpDownload(
                shihabItem,
                _shihabStore.MehediBaseDirectory,
                _mehediConfig,
                perTaskBps,
                delegate
                {
                    NotifyShihabItemChanged(shihabItem);
                },
                mehediToken,
                out outputPath,
                out errMsg);

            if (mehediToken.IsCancellationRequested) return;
            if (!ok)
            {
                throw new IOException(string.IsNullOrWhiteSpace(errMsg) ? "yt-dlp job failed." : errMsg);
            }
            if (!string.IsNullOrWhiteSpace(outputPath) && File.Exists(outputPath))
            {
                shihabItem.ShihabFileName = Path.GetFileName(outputPath);
                shihabItem.MehediSaveDirectory = Path.GetDirectoryName(outputPath);
                ComputeShihabFileSha256AndSize(shihabItem, outputPath);
                if (_mehediConfig.ShihabWriteZoneIdentifier)
                {
                    ShihabOsAndDiskSafety.WriteMarkOfTheWeb(outputPath, shihabItem.MehediUrl, shihabItem.MehediReferer);
                }
            }

            CompleteActiveDownload(shihabItem, handle, "Complete");
        }

        private void ExecuteMehediGalleryDlJob(
            ShihabDownloadItem shihabItem,
            CancellationToken mehediToken,
            ActiveDownloadHandle handle)
        {
            shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabDownloading, "Downloading gallery...");
            NotifyShihabItemChanged(shihabItem);

            int downloadedCount;
            string errMsg;
            bool ok = ShihabMediaHelpersManager.ExecuteGalleryDlJob(
                shihabItem,
                _shihabStore.MehediBaseDirectory,
                _mehediConfig,
                delegate
                {
                    NotifyShihabItemChanged(shihabItem);
                },
                mehediToken,
                out downloadedCount,
                out errMsg);

            if (mehediToken.IsCancellationRequested) return;
            if (!ok)
            {
                throw new IOException(string.IsNullOrWhiteSpace(errMsg) ? "gallery-dl job failed." : errMsg);
            }
            if (_mehediConfig.ShihabWriteZoneIdentifier && Directory.Exists(shihabItem.MehediSaveDirectory))
            {
                ShihabOsAndDiskSafety.PropagateMarkOfTheWebToDirectory(shihabItem.MehediSaveDirectory, shihabItem.MehediUrl, shihabItem.MehediReferer);
            }

            CompleteActiveDownload(shihabItem, handle, string.Format("Complete ({0} files)", downloadedCount));
        }

        private void ExecuteMehediTorrentDownload(
            ShihabDownloadItem shihabItem,
            string mehediItemTempDir,
            CancellationToken mehediToken,
            ActiveDownloadHandle handle)
        {
            string sourceSpec = !string.IsNullOrWhiteSpace(shihabItem.ShihabTorrentMagnetOrPath)
                ? shihabItem.ShihabTorrentMagnetOrPath
                : shihabItem.MehediUrl;

            // 1. Optional Debrid Torrent/Magnet Instant Cloud Unlocking (Real-Debrid / AllDebrid / Premiumize)
            if (shihabItem.ShihabUseDebridForTorrent && !shihabItem.ShihabBypassDebrid)
            {
                string unlockedDirectUrl, unlockedName, providerUsed;
                if (ShihabDebridManager.TryUnlockTorrentOrMagnetWithDebrid(
                    sourceSpec,
                    _mehediConfig,
                    out unlockedDirectUrl,
                    out unlockedName,
                    out providerUsed) && !string.IsNullOrWhiteSpace(unlockedDirectUrl))
                {
                    EmitShihabLog(string.Format("[DebridTorrent] Unlocked torrent via {0} -> {1}", providerUsed, unlockedName));
                    if (!string.IsNullOrWhiteSpace(unlockedName))
                    {
                        shihabItem.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(
                            shihabItem.MehediSaveDirectory,
                            ShihabFormatHelpers.SanitizeShihabFileName(unlockedName));
                    }
                    string debridPartPath = shihabItem.GetShihabFullTargetPath() + ".mhs.part";
                    DownloadDirectShihabStreamToFile(shihabItem, unlockedDirectUrl, debridPartPath, shihabItem.ShihabTotalBytes, true, mehediToken);
                    if (mehediToken.IsCancellationRequested) return;
                    FinalizeShihabDownloadedFileAndOptionalAudioMux(shihabItem, debridPartPath, mehediItemTempDir, false, mehediToken);
                    if (mehediToken.IsCancellationRequested) return;
                    CompleteActiveDownload(shihabItem, handle, "Complete (" + providerUsed + ")");
                    return;
                }
            }

            // 2. Parse .torrent or Magnet URI and execute full P2P + BEP-9/10 + BEP-15 UDP/HTTP + BEP-19 WebSeed Session
            ShihabTorrentMetadata meta = sourceSpec.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)
                ? ShihabTorrentManager.ParseMagnetUri(sourceSpec)
                : (File.Exists(sourceSpec) ? ShihabTorrentManager.InspectTorrentFile(sourceSpec) : ShihabTorrentManager.ParseMagnetUri(sourceSpec));

            if (meta.TotalSizeBytes > 0 && shihabItem.ShihabTotalBytes <= 0)
            {
                shihabItem.ShihabTotalBytes = meta.TotalSizeBytes;
            }
            if (!string.IsNullOrWhiteSpace(meta.Name) &&
                (shihabItem.ShihabFileName == "download.bin" || shihabItem.ShihabFileName.StartsWith("magnet_", StringComparison.OrdinalIgnoreCase)))
            {
                shihabItem.ShihabFileName = ShihabFormatHelpers.SanitizeShihabFileName(meta.Name);
            }

            string p2pErr;
            bool p2pOk = ShihabTorrentManager.ExecuteP2pTorrentSession(
                shihabItem,
                meta,
                _mehediConfig,
                _shihabStore.MehediBaseDirectory,
                delegate { NotifyShihabItemChanged(shihabItem); },
                mehediToken,
                out p2pErr);

            if (mehediToken.IsCancellationRequested)
            {
                return;
            }
            if (!p2pOk)
            {
                throw new IOException(!string.IsNullOrWhiteSpace(p2pErr) ? p2pErr : "BitTorrent P2P session failed.");
            }

            string completedPath = shihabItem.GetShihabFullTargetPath();
            if (File.Exists(completedPath))
            {
                FinalizeShihabDownloadedFileAndOptionalAudioMux(shihabItem, completedPath, mehediItemTempDir, false, mehediToken);
                if (mehediToken.IsCancellationRequested) return;
            }

            CompleteActiveDownload(shihabItem, handle, shihabItem.ShihabTorrentKeepSeeding ? "Seeded & Complete" : "Complete");
        }

        private void FinalizeShihabDownloadedFileAndOptionalAudioMux(
            ShihabDownloadItem shihabItem,
            string mehediPrimaryAssembledPath,
            string shihabItemTempDir,
            bool mehediShaAlreadyComputedOnTarget,
            CancellationToken shihabToken)
        {
            ShihabMediaProbeInfo mehediPrimaryMedia = MehediMediaMuxer.InspectShihabMediaFile(mehediPrimaryAssembledPath);
            if (mehediPrimaryMedia.ShihabIsEncryptedDrm)
            {
                try { if (File.Exists(mehediPrimaryAssembledPath)) File.Delete(mehediPrimaryAssembledPath); } catch { }
                throw new InvalidOperationException("Protected/encrypted DRM stream cannot be downloaded (DRM protections are respected).");
            }

            string shihabFinalTargetPath = shihabItem.GetShihabFullTargetPath();
            string targetDir = Path.GetDirectoryName(shihabFinalTargetPath);
            if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);

            bool mehediNeedsAudioMux =
                !string.IsNullOrWhiteSpace(shihabItem.MehediAudioCompanionUrl) &&
                !(mehediPrimaryMedia.ShihabHasVideo && mehediPrimaryMedia.MehediHasAudio);

            if (mehediNeedsAudioMux)
            {
                string shihabAudioTempPath = Path.Combine(shihabItemTempDir, "companion_audio_raw.part");
                DownloadMehediAuxiliaryStream(
                    shihabItem,
                    shihabItem.MehediAudioCompanionUrl,
                    shihabItem.ShihabAudioCompanionBytes,
                    shihabAudioTempPath,
                    shihabToken);
                if (shihabToken.IsCancellationRequested) return;

                shihabItem.ShihabCurrentSpeedBps = 0;
                shihabItem.MehediEtaSeconds = 0;
                shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabRebuilding, "Merging video + audio...");
                NotifyShihabItemChanged(shihabItem);

                string mehediMuxedActualPath = MehediMediaMuxer.MuxShihabVideoAndAudio(
                    mehediPrimaryAssembledPath,
                    shihabAudioTempPath,
                    shihabFinalTargetPath);

                shihabFinalTargetPath = mehediMuxedActualPath;
                shihabItem.ShihabFileName = Path.GetFileName(mehediMuxedActualPath);
                mehediShaAlreadyComputedOnTarget = false;
            }
            else if (!string.Equals(Path.GetFullPath(mehediPrimaryAssembledPath), Path.GetFullPath(shihabFinalTargetPath), StringComparison.OrdinalIgnoreCase))
            {
                if (!TryAtomicMoveReplace(mehediPrimaryAssembledPath, shihabFinalTargetPath))
                {
                    throw new IOException("Unable to move assembled download to final target path: " + shihabFinalTargetPath);
                }
                mehediShaAlreadyComputedOnTarget = false;
            }

            if (!mehediShaAlreadyComputedOnTarget || string.IsNullOrEmpty(shihabItem.MehediSha256Hex))
            {
                ComputeShihabFileSha256AndSize(shihabItem, shihabFinalTargetPath);
            }

            // Multi-Algorithm Checksum Verification (CRC32, MD5, SHA-1, SHA-256, SHA-512)
            if (!string.IsNullOrWhiteSpace(shihabItem.ShihabExpectedChecksum))
            {
                shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabRebuilding, "Verifying checksum...");
                NotifyShihabItemChanged(shihabItem);
                string computedSpec;
                string statusSummary;
                bool verifiedOk = ShihabChecksumVerifier.VerifyFileAgainstSpec(
                    shihabFinalTargetPath,
                    shihabItem.ShihabExpectedChecksum,
                    out computedSpec,
                    out statusSummary);
                shihabItem.ShihabComputedChecksum = computedSpec;
                shihabItem.ShihabChecksumStatus = statusSummary;
                if (!verifiedOk)
                {
                    throw new InvalidOperationException("Checksum verification failed: " + statusSummary);
                }
                EmitShihabLog("[Checksum] " + shihabItem.ShihabFileName + " -> " + statusSummary);
            }
            else if (!string.IsNullOrEmpty(shihabItem.MehediSha256Hex))
            {
                shihabItem.ShihabComputedChecksum = "sha256:" + shihabItem.MehediSha256Hex;
                shihabItem.ShihabChecksumStatus = "SHA-256 Computed";
            }

            // Mark-of-the-Web (NTFS :Zone.Identifier ADS)
            if (_mehediConfig.ShihabWriteZoneIdentifier)
            {
                ShihabOsAndDiskSafety.WriteMarkOfTheWeb(shihabFinalTargetPath, shihabItem.MehediUrl, shihabItem.MehediReferer);
            }

            if ((_mehediConfig.ShihabSetServerFileTimestamp || _mehediConfig.ShihabSetServerCreationDate) &&
                !string.IsNullOrWhiteSpace(shihabItem.ShihabServerLastModified))
            {
                DateTime mehediServerDt;
                if (DateTime.TryParse(shihabItem.ShihabServerLastModified, out mehediServerDt))
                {
                    if (_mehediConfig.ShihabSetServerFileTimestamp)
                    {
                        try { File.SetLastWriteTime(shihabFinalTargetPath, mehediServerDt); } catch { }
                    }
                    if (_mehediConfig.ShihabSetServerCreationDate)
                    {
                        try { File.SetCreationTime(shihabFinalTargetPath, mehediServerDt); } catch { }
                    }
                }
            }

            // Hardened Post-Download Archive Auto-Extraction (waits for all split volumes + propagates MotW)
            if ((shihabItem.ShihabAutoExtract || _mehediConfig.ShihabAutoExtractArchives) &&
                ShihabSafeArchiveExtractor.IsSupportedArchiveFile(shihabFinalTargetPath))
            {
                string splitWaitReason;
                if (!ShihabSafeArchiveExtractor.AreAllSplitArchiveVolumesReady(shihabFinalTargetPath, out splitWaitReason))
                {
                    EmitShihabLog("[Extract] Deferred split archive extraction: " + splitWaitReason);
                }
                else
                {
                    shihabItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabRebuilding, "Extracting archive...");
                    NotifyShihabItemChanged(shihabItem);
                    string extractedDest;
                    string extractErr;
                    if (ShihabSafeArchiveExtractor.ExtractArchiveSafely(
                        shihabFinalTargetPath,
                        _mehediConfig.ShihabExtractSubfolderMode,
                        shihabItem.ShihabArchivePassword,
                        _mehediConfig.ShihabDeleteArchiveAfterExtract,
                        _mehediConfig.ShihabWriteZoneIdentifier ? shihabItem.MehediUrl : "",
                        _mehediConfig.ShihabWriteZoneIdentifier ? shihabItem.MehediReferer : "",
                        out extractedDest,
                        out extractErr))
                    {
                        EmitShihabLog("[Extract] Extracted " + shihabItem.ShihabFileName + " -> " + extractedDest);
                    }
                    else
                    {
                        EmitShihabLog("[ExtractWarning] " + extractErr);
                    }
                }
            }

            if (_mehediConfig.ShihabEnableVirusCheck && !string.IsNullOrWhiteSpace(_mehediConfig.MehediVirusScannerExe))
            {
                try
                {
                    string shihabScannerExe = _mehediConfig.MehediVirusScannerExe.Trim().Trim('"');
                    if (Path.IsPathRooted(shihabScannerExe))
                    {
                        shihabScannerExe = Path.GetFullPath(shihabScannerExe);
                        if (File.Exists(shihabScannerExe) && File.Exists(shihabFinalTargetPath))
                        {
                            string quotedTarget = ShihabProcessAndPathSafety.QuoteWindowsArgument(shihabFinalTargetPath);
                            string mehediArgTemplate = string.IsNullOrWhiteSpace(_mehediConfig.ShihabVirusScannerArgs)
                                ? "%1"
                                : _mehediConfig.ShihabVirusScannerArgs.Trim();
                            string shihabFormattedArgs;
                            if (mehediArgTemplate.IndexOf("\"%1\"", StringComparison.Ordinal) >= 0)
                            {
                                shihabFormattedArgs = mehediArgTemplate.Replace("\"%1\"", quotedTarget);
                            }
                            else if (mehediArgTemplate.IndexOf("%1", StringComparison.Ordinal) >= 0)
                            {
                                shihabFormattedArgs = mehediArgTemplate.Replace("%1", quotedTarget);
                            }
                            else
                            {
                                shihabFormattedArgs = mehediArgTemplate + " " + quotedTarget;
                            }

                            ProcessStartInfo mehediScanPsi = new ProcessStartInfo(shihabScannerExe, shihabFormattedArgs)
                            {
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            using (Process scanProc = Process.Start(mehediScanPsi))
                            {
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private static void ComputeShihabFileSha256AndSize(ShihabDownloadItem mehediItem, string shihabFilePath)
        {
            long mehediLen = 0;
            byte[] shihabBuf = new byte[262144];
            using (SHA256 mehediSha = SHA256.Create())
            using (FileStream shihabFs = new FileStream(shihabFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int mehediRead;
                while ((mehediRead = shihabFs.Read(shihabBuf, 0, shihabBuf.Length)) > 0)
                {
                    mehediSha.TransformBlock(shihabBuf, 0, mehediRead, null, 0);
                    mehediLen += mehediRead;
                }
                mehediSha.TransformFinalBlock(new byte[0], 0, 0);
                StringBuilder shihabHex = new StringBuilder(64);
                for (int mehediH = 0; mehediH < mehediSha.Hash.Length; mehediH++)
                {
                    shihabHex.Append(mehediSha.Hash[mehediH].ToString("x2"));
                }
                mehediItem.MehediSha256Hex = shihabHex.ToString();
            }
            mehediItem.MehediDownloadedBytes = mehediLen;
            mehediItem.ShihabTotalBytes = mehediLen;
        }

        private bool TryHealOrResolveShihabYouTubeItem(
            ShihabDownloadItem shihabItem,
            string mehediItemTempDir,
            bool shihabForceRefresh)
        {
            if (shihabItem == null || string.IsNullOrWhiteSpace(shihabItem.MehediUrl))
            {
                return false;
            }

            string mehediWatchVidId;
            bool shihabIsWatchUrl = MehediYouTubeInnerTubeResolver.TryExtractShihabYouTubeWatchVideoId(shihabItem.MehediUrl, out mehediWatchVidId);
            bool mehediNeedsPrimaryHeal =
                shihabForceRefresh ||
                shihabIsWatchUrl ||
                IsPoisonedWebShihabGoogleVideoUrl(shihabItem.MehediUrl) ||
                (IsRealShihabGoogleVideoDomain(shihabItem.MehediUrl) && !IsDirectMobileShihabGoogleVideoUrl(shihabItem.MehediUrl));
            bool shihabNeedsAudioHeal =
                !string.IsNullOrWhiteSpace(shihabItem.MehediAudioCompanionUrl) &&
                (shihabForceRefresh ||
                 IsPoisonedWebShihabGoogleVideoUrl(shihabItem.MehediAudioCompanionUrl) ||
                 (IsRealShihabGoogleVideoDomain(shihabItem.MehediAudioCompanionUrl) && !IsDirectMobileShihabGoogleVideoUrl(shihabItem.MehediAudioCompanionUrl)));

            if (!mehediNeedsPrimaryHeal && !shihabNeedsAudioHeal)
            {
                return false;
            }

            string shihabVideoId = MehediYouTubeInnerTubeResolver.ExtractShihabVideoIdFromItem(shihabItem);
            if (string.IsNullOrWhiteSpace(shihabVideoId))
            {
                return false;
            }

            ShihabYouTubeStreamsReplyDto mehediResolved = MehediYouTubeInnerTubeResolver.ResolveShihabYouTubeStreams(
                shihabVideoId,
                "",
                shihabForceRefresh);
            if (mehediResolved == null || !mehediResolved.ShihabOk || mehediResolved.MehediStreams == null || mehediResolved.MehediStreams.Count == 0)
            {
                return false;
            }

            string shihabExtLower = (Path.GetExtension(shihabItem.ShihabFileName ?? "") ?? "").ToLowerInvariant();
            bool mehediWantsAudioOnly =
                shihabExtLower == ".m4a" ||
                shihabExtLower == ".mp3" ||
                shihabExtLower == ".opus" ||
                shihabExtLower == ".ogg" ||
                (shihabItem.ShihabQualityLabel != null && shihabItem.ShihabQualityLabel.IndexOf("Audio", StringComparison.OrdinalIgnoreCase) >= 0);

            string shihabDesiredQual = shihabItem.ShihabQualityLabel ?? "";
            if (string.IsNullOrWhiteSpace(shihabDesiredQual) && !string.IsNullOrWhiteSpace(shihabItem.ShihabFileName))
            {
                try
                {
                    Match mehediQualMatch = QualInFileNameRegex.Match(shihabItem.ShihabFileName);
                    if (mehediQualMatch.Success)
                    {
                        shihabDesiredQual = mehediQualMatch.Groups[1].Value;
                    }
                }
                catch { }
            }
            if (string.IsNullOrWhiteSpace(shihabDesiredQual) && IsRealShihabGoogleVideoDomain(shihabItem.MehediUrl))
            {
                shihabDesiredQual = MehediYouTubeInnerTubeResolver.InferShihabResolutionFromItag(GetShihabUrlQueryParam(shihabItem.MehediUrl, "itag"));
            }

            MehediYouTubeResolvedStreamDto shihabChosenPrimary;
            MehediYouTubeResolvedStreamDto mehediChosenAudio;
            MehediYouTubeInnerTubeResolver.SelectBestShihabStreamPair(
                mehediResolved,
                shihabDesiredQual,
                mehediWantsAudioOnly,
                out shihabChosenPrimary,
                out mehediChosenAudio);

            if (shihabChosenPrimary == null || string.IsNullOrWhiteSpace(shihabChosenPrimary.ShihabUrl))
            {
                return false;
            }

            if (mehediNeedsPrimaryHeal)
            {
                bool shihabUrlChanged = !string.Equals(shihabItem.MehediUrl, shihabChosenPrimary.ShihabUrl, StringComparison.Ordinal);
                shihabItem.MehediUrl = shihabChosenPrimary.ShihabUrl;
                if (shihabChosenPrimary.ShihabSizeBytes > 0)
                {
                    shihabItem.ShihabTotalBytes = shihabChosenPrimary.ShihabSizeBytes;
                }
                shihabItem.MehediUserAgent = shihabChosenPrimary.MehediUserAgent ?? MehediYouTubeInnerTubeResolver.ShihabVisionOsUserAgent;
                shihabItem.MehediOmitCookies = true;
                shihabItem.MehediOriginHeader = "";
                if (string.IsNullOrWhiteSpace(shihabItem.MehediReferer) ||
                    string.Equals(shihabItem.MehediReferer.Trim(), "https://www.youtube.com/", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(shihabItem.MehediReferer.Trim(), "https://www.youtube.com", StringComparison.OrdinalIgnoreCase))
                {
                    shihabItem.MehediReferer = "https://www.youtube.com/watch?v=" + shihabVideoId;
                }

                if (shihabUrlChanged)
                {
                    object segLock = GetItemSegmentLock(shihabItem);
                    lock (segLock)
                    {
                        shihabItem.MehediDownloadedBytes = 0;
                        if (shihabItem.MehediSegments != null)
                        {
                            lock (shihabItem.MehediSegments)
                            {
                                shihabItem.MehediSegments.Clear();
                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(mehediItemTempDir) && Directory.Exists(mehediItemTempDir))
                    {
                        try
                        {
                            string[] mehediOldParts = Directory.GetFiles(mehediItemTempDir, "*.shihabpart");
                            for (int shihabP = 0; shihabP < mehediOldParts.Length; shihabP++)
                            {
                                try { File.Delete(mehediOldParts[shihabP]); } catch { }
                            }
                        }
                        catch { }
                    }
                }

                if (!string.IsNullOrWhiteSpace(mehediResolved.ShihabTitle) &&
                    (string.IsNullOrWhiteSpace(shihabItem.ShihabFileName) ||
                     shihabItem.ShihabFileName.StartsWith("videoplayback", StringComparison.OrdinalIgnoreCase) ||
                     shihabItem.ShihabFileName == "download.bin" ||
                     ShihabFormatHelpers.IsMehediGibberishCdnFileName(shihabItem.ShihabFileName)))
                {
                    string mehediExt = "." + (string.IsNullOrWhiteSpace(shihabChosenPrimary.ShihabExt) ? (mehediWantsAudioOnly ? "m4a" : "mp4") : shihabChosenPrimary.ShihabExt);
                    string shihabTag = mehediWantsAudioOnly ? "Audio" : (shihabChosenPrimary.ShihabResolution ?? "");
                    string mehediStem = ShihabFormatHelpers.SanitizeShihabFileName(mehediResolved.ShihabTitle);
                    string shihabCandidateName = !string.IsNullOrWhiteSpace(shihabTag)
                        ? string.Format("{0} [{1}]{2}", mehediStem, shihabTag, mehediExt)
                        : (mehediStem + mehediExt);
                    shihabItem.ShihabFileName = !string.IsNullOrWhiteSpace(shihabItem.MehediSaveDirectory)
                        ? ShihabFormatHelpers.EnsureShihabUniqueFilePath(shihabItem.MehediSaveDirectory, shihabCandidateName)
                        : ShihabFormatHelpers.SanitizeShihabFileName(shihabCandidateName);
                }
            }

            if (mehediWantsAudioOnly || (shihabChosenPrimary.ShihabHasVideo && shihabChosenPrimary.MehediHasAudio))
            {
                shihabItem.MehediAudioCompanionUrl = "";
                shihabItem.ShihabAudioCompanionBytes = -1L;
            }
            else if (mehediChosenAudio != null && !string.IsNullOrWhiteSpace(mehediChosenAudio.ShihabUrl))
            {
                shihabItem.MehediAudioCompanionUrl = mehediChosenAudio.ShihabUrl;
                shihabItem.ShihabAudioCompanionBytes = mehediChosenAudio.ShihabSizeBytes;
            }

            EmitShihabLog(string.Format("[YouTubeResolver] Resolved {0} ({1}, client={2}) -> direct multi-segment CDN stream",
                shihabVideoId,
                mehediWantsAudioOnly ? "Audio" : (shihabChosenPrimary.ShihabResolution ?? "Video"),
                mehediResolved.ShihabClientUsed ?? "ANDROID"));
            PersistShihabState();
            return true;
        }
    }
}


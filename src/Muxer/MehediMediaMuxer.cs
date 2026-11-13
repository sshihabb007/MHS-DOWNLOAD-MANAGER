using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShihabMehediIDM
{
    public static class MehediMediaMuxer
    {
        public static ShihabMediaProbeInfo InspectShihabMediaFile(string mehediFilePath)
        {
            ShihabMediaProbeInfo shihabInfo = new ShihabMediaProbeInfo();
            if (string.IsNullOrEmpty(mehediFilePath) || !File.Exists(mehediFilePath))
            {
                return shihabInfo;
            }

            using (FileStream mehediFs = new FileStream(mehediFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
            {
                if (mehediFs.Length < 12) return shihabInfo;
                byte[] shihabMagic = new byte[12];
                int mehediRead = MuxerBinaryIo.ReadExactCountFromStream(mehediFs, shihabMagic, 0, 12);
                if (mehediRead < 12) return shihabInfo;

                if (shihabMagic[0] == 0x1A && shihabMagic[1] == 0x45 && shihabMagic[2] == 0xDF && shihabMagic[3] == 0xA3)
                {
                    shihabInfo.MehediContainerFormat = "webm";
                    mehediFs.Position = 0;
                    ShihabParsedWebmStream mehediWebm = EbmlWebmMkvMuxer.ParseShihabWebmFile(mehediFs, true);
                    shihabInfo.ShihabHasVideo = mehediWebm.ShihabVideoTrackEntryPayload != null;
                    shihabInfo.MehediHasAudio = mehediWebm.MehediAudioTrackEntryPayload != null;
                    shihabInfo.MehediWidth = mehediWebm.MehediWidth;
                    shihabInfo.ShihabHeight = mehediWebm.ShihabHeight;
                    shihabInfo.MehediVideoCodec = mehediWebm.ShihabVideoCodecId;
                    shihabInfo.ShihabAudioCodec = mehediWebm.MehediAudioCodecId;
                    return shihabInfo;
                }

                string shihabFourCc = Encoding.ASCII.GetString(shihabMagic, 4, 4);
                if (shihabFourCc == "ftyp" || shihabFourCc == "moov" || shihabFourCc == "styp" || shihabFourCc == "sidx" || shihabFourCc == "free")
                {
                    shihabInfo.MehediContainerFormat = "mp4";
                    mehediFs.Position = 0;
                    bool mehediEncrypted;
                    List<ShihabParsedIsoTrack> shihabTracks = IsoBmffMuxer.ParseMehediIsoBmffTracks(mehediFs, true, out mehediEncrypted);
                    shihabInfo.ShihabIsEncryptedDrm = mehediEncrypted;
                    for (int mehediIdx = 0; mehediIdx < shihabTracks.Count; mehediIdx++)
                    {
                        ShihabParsedIsoTrack shihabTrk = shihabTracks[mehediIdx];
                        if (shihabTrk.ShihabHandlerType == "vide")
                        {
                            shihabInfo.ShihabHasVideo = true;
                            if (shihabTrk.MehediWidth > 0) shihabInfo.MehediWidth = shihabTrk.MehediWidth;
                            if (shihabTrk.ShihabHeight > 0) shihabInfo.ShihabHeight = shihabTrk.ShihabHeight;
                            shihabInfo.MehediVideoCodec = shihabTrk.MehediSampleEntryFourCc;
                        }
                        else if (shihabTrk.ShihabHandlerType == "soun")
                        {
                            shihabInfo.MehediHasAudio = true;
                            shihabInfo.ShihabAudioCodec = shihabTrk.MehediSampleEntryFourCc;
                        }
                    }
                }
            }
            return shihabInfo;
        }

        public static string MuxShihabVideoAndAudio(
            string shihabVideoFilePath,
            string mehediAudioFilePath,
            string shihabRequestedOutputPath)
        {
            if (string.IsNullOrEmpty(shihabRequestedOutputPath))
            {
                throw new ArgumentException("Requested mux output path is required.");
            }

            ShihabMediaProbeInfo mehediVidProbe = InspectShihabMediaFile(shihabVideoFilePath);
            ShihabMediaProbeInfo shihabAudProbe = InspectShihabMediaFile(mehediAudioFilePath);

            if (mehediVidProbe.ShihabIsEncryptedDrm || shihabAudProbe.ShihabIsEncryptedDrm)
            {
                throw new InvalidOperationException("Encrypted DRM media stream cannot be muxed (DRM protections are respected).");
            }

            if (mehediVidProbe.ShihabHasVideo && mehediVidProbe.MehediHasAudio)
            {
                MuxerBinaryIo.SafeCopyIfDistinctPaths(shihabVideoFilePath, shihabRequestedOutputPath);
                return shihabRequestedOutputPath;
            }

            string fullRequested = Path.GetFullPath(shihabRequestedOutputPath);
            string mehediOutDir = Path.GetDirectoryName(fullRequested) ?? "";
            string shihabOutStem = Path.GetFileNameWithoutExtension(fullRequested);

            if (mehediVidProbe.MehediContainerFormat == "mp4" && shihabAudProbe.MehediContainerFormat == "mp4")
            {
                string mehediFinalMp4 = Path.Combine(mehediOutDir, shihabOutStem + ".mp4");
                IsoBmffMuxer.MuxShihabIsoBmffToFastStartMp4(shihabVideoFilePath, mehediAudioFilePath, mehediFinalMp4);
                return mehediFinalMp4;
            }

            if (mehediVidProbe.MehediContainerFormat == "mp4" && shihabAudProbe.MehediContainerFormat == "webm")
            {
                string shihabFinalMp4 = Path.Combine(mehediOutDir, shihabOutStem + ".mp4");
                IsoBmffMuxer.MuxShihabMp4VideoWithWebmOpusToMp4(shihabVideoFilePath, mehediAudioFilePath, shihabFinalMp4);
                return shihabFinalMp4;
            }

            if (mehediVidProbe.MehediContainerFormat == "webm" && shihabAudProbe.MehediContainerFormat == "webm")
            {
                string mehediFinalWebm = Path.Combine(mehediOutDir, shihabOutStem + ".webm");
                EbmlWebmMkvMuxer.MuxMehediWebmVideoAndWebmAudio(shihabVideoFilePath, mehediAudioFilePath, mehediFinalWebm, "webm");
                return mehediFinalWebm;
            }

            if (mehediVidProbe.MehediContainerFormat == "webm" && shihabAudProbe.MehediContainerFormat == "mp4")
            {
                string shihabFinalMkv = Path.Combine(mehediOutDir, shihabOutStem + ".mkv");
                EbmlWebmMkvMuxer.MuxMehediWebmVideoWithMp4AacToMkv(shihabVideoFilePath, mehediAudioFilePath, shihabFinalMkv);
                return shihabFinalMkv;
            }

            MuxerBinaryIo.SafeCopyIfDistinctPaths(shihabVideoFilePath, shihabRequestedOutputPath);
            return shihabRequestedOutputPath;
        }
    }
}


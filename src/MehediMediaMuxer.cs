using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShihabMehediIDM
{
    public sealed class ShihabMediaProbeInfo
    {
        public string MehediContainerFormat { get; set; }
        public bool ShihabHasVideo { get; set; }
        public bool MehediHasAudio { get; set; }
        public bool ShihabIsEncryptedDrm { get; set; }
        public int MehediWidth { get; set; }
        public int ShihabHeight { get; set; }
        public string MehediVideoCodec { get; set; }
        public string ShihabAudioCodec { get; set; }

        public ShihabMediaProbeInfo()
        {
            MehediContainerFormat = "unknown";
            MehediVideoCodec = "";
            ShihabAudioCodec = "";
        }
    }

    internal sealed class MehediIsoSampleRecord
    {
        public long ShihabFileOffset;
        public uint MehediSize;
        public uint ShihabDuration;
        public int MehediCompositionOffset;
        public bool ShihabIsSync;
    }

    internal sealed class ShihabParsedIsoTrack
    {
        public uint MehediTrackId;
        public string ShihabHandlerType = "";
        public string MehediSampleEntryFourCc = "";
        public uint ShihabTimescale = 1000;
        public int MehediWidth;
        public int ShihabHeight;
        public byte[] MehediRawStsdBox;
        public byte[] ShihabAacAudioSpecificConfig;
        public uint MehediTrexDefaultDuration;
        public uint ShihabTrexDefaultSize;
        public uint MehediTrexDefaultFlags;
        public readonly List<MehediIsoSampleRecord> ShihabSamples = new List<MehediIsoSampleRecord>();
    }

    internal sealed class MehediEbmlFramePacket
    {
        public long ShihabTimestampMs;
        public int MehediOutputTrackNumber;
        public bool ShihabIsKeyframe;
        public byte[] MehediRawFramePayload;
    }

    internal sealed class ShihabParsedWebmStream
    {
        public long MehediTimecodeScaleNs = 1000000L;
        public byte[] ShihabVideoTrackEntryPayload;
        public int MehediOrigVideoTrackNum;
        public string ShihabVideoCodecId = "";
        public int MehediWidth;
        public int ShihabHeight;

        public byte[] MehediAudioTrackEntryPayload;
        public int ShihabOrigAudioTrackNum;
        public string MehediAudioCodecId = "";
        public byte[] ShihabAudioCodecPrivate;
        public int MehediAudioChannels = 2;
        public int ShihabAudioSampleRate = 48000;

        public readonly List<MehediEbmlFramePacket> ShihabVideoFrames = new List<MehediEbmlFramePacket>();
        public readonly List<MehediEbmlFramePacket> MehediAudioFrames = new List<MehediEbmlFramePacket>();
    }

    public static class MehediMediaMuxer
    {
        public static ShihabMediaProbeInfo InspectShihabMediaFile(string mehediFilePath)
        {
            ShihabMediaProbeInfo shihabInfo = new ShihabMediaProbeInfo();
            if (string.IsNullOrEmpty(mehediFilePath) || !File.Exists(mehediFilePath))
            {
                return shihabInfo;
            }

            using (FileStream mehediFs = new FileStream(mehediFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (mehediFs.Length < 12) return shihabInfo;
                byte[] shihabMagic = new byte[12];
                int mehediRead = mehediFs.Read(shihabMagic, 0, 12);
                if (mehediRead < 12) return shihabInfo;

                if (shihabMagic[0] == 0x1A && shihabMagic[1] == 0x45 && shihabMagic[2] == 0xDF && shihabMagic[3] == 0xA3)
                {
                    shihabInfo.MehediContainerFormat = "webm";
                    mehediFs.Position = 0;
                    ShihabParsedWebmStream mehediWebm = ParseShihabWebmFile(mehediFs, true);
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
                    List<ShihabParsedIsoTrack> shihabTracks = ParseMehediIsoBmffTracks(mehediFs, true, out mehediEncrypted);
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
            ShihabMediaProbeInfo mehediVidProbe = InspectShihabMediaFile(shihabVideoFilePath);
            ShihabMediaProbeInfo shihabAudProbe = InspectShihabMediaFile(mehediAudioFilePath);

            if (mehediVidProbe.ShihabIsEncryptedDrm || shihabAudProbe.ShihabIsEncryptedDrm)
            {
                throw new InvalidOperationException("Encrypted DRM media stream cannot be muxed (DRM protections are respected).");
            }

            // If the video file already contains both video and audio, simply move/copy cleanly
            if (mehediVidProbe.ShihabHasVideo && mehediVidProbe.MehediHasAudio)
            {
                if (File.Exists(shihabRequestedOutputPath)) File.Delete(shihabRequestedOutputPath);
                File.Copy(shihabVideoFilePath, shihabRequestedOutputPath, true);
                return shihabRequestedOutputPath;
            }

            string mehediOutDir = Path.GetDirectoryName(shihabRequestedOutputPath);
            string shihabOutStem = Path.GetFileNameWithoutExtension(shihabRequestedOutputPath);

            // Case 1: MP4/fMP4 Video + MP4/M4A/fMP4 Audio -> Standard Fast-Start Progressive .mp4
            if (mehediVidProbe.MehediContainerFormat == "mp4" && shihabAudProbe.MehediContainerFormat == "mp4")
            {
                string mehediFinalMp4 = Path.Combine(mehediOutDir, shihabOutStem + ".mp4");
                MuxShihabIsoBmffToFastStartMp4(shihabVideoFilePath, mehediAudioFilePath, mehediFinalMp4);
                return mehediFinalMp4;
            }

            // Case 2: MP4/fMP4 Video (H.264/AV1/VP9) + WebM Opus Audio (e.g. YouTube itag 137/399 + itag 251) -> Standard .mp4 with ISO-BMFF Opus/dOps
            if (mehediVidProbe.MehediContainerFormat == "mp4" && shihabAudProbe.MehediContainerFormat == "webm")
            {
                string shihabFinalMp4 = Path.Combine(mehediOutDir, shihabOutStem + ".mp4");
                MuxShihabMp4VideoWithWebmOpusToMp4(shihabVideoFilePath, mehediAudioFilePath, shihabFinalMp4);
                return shihabFinalMp4;
            }

            // Case 3: WebM Video + WebM Audio (e.g. YouTube VP9/AV1 WebM + Opus/Vorbis WebM) -> Standard .webm
            if (mehediVidProbe.MehediContainerFormat == "webm" && shihabAudProbe.MehediContainerFormat == "webm")
            {
                string mehediFinalWebm = Path.Combine(mehediOutDir, shihabOutStem + ".webm");
                MuxMehediWebmVideoAndWebmAudio(shihabVideoFilePath, mehediAudioFilePath, mehediFinalWebm, "webm");
                return mehediFinalWebm;
            }

            // Case 4: WebM Video + MP4/M4A AAC Audio -> Universal Matroska .mkv
            if (mehediVidProbe.MehediContainerFormat == "webm" && shihabAudProbe.MehediContainerFormat == "mp4")
            {
                string shihabFinalMkv = Path.Combine(mehediOutDir, shihabOutStem + ".mkv");
                MuxMehediWebmVideoWithMp4AacToMkv(shihabVideoFilePath, mehediAudioFilePath, shihabFinalMkv);
                return shihabFinalMkv;
            }

            // Fallback: preserve video stream directly if container unknown
            if (File.Exists(shihabRequestedOutputPath)) File.Delete(shihabRequestedOutputPath);
            File.Copy(shihabVideoFilePath, shihabRequestedOutputPath, true);
            return shihabRequestedOutputPath;
        }

        // =====================================================================
        // 1. ISO-BMFF (MP4 / fMP4 / M4A) PARSER & FAST-START PROGRESSIVE MUXER
        // =====================================================================

        private static void MuxShihabIsoBmffToFastStartMp4(
            string shihabVideoPath,
            string mehediAudioPath,
            string shihabOutputPath)
        {
            using (FileStream mehediVidFs = new FileStream(shihabVideoPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream shihabAudFs = new FileStream(mehediAudioPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool mehediEncV, shihabEncA;
                List<ShihabParsedIsoTrack> mehediVidTracks = ParseMehediIsoBmffTracks(mehediVidFs, false, out mehediEncV);
                List<ShihabParsedIsoTrack> shihabAudTracks = ParseMehediIsoBmffTracks(shihabAudFs, false, out shihabEncA);

                ShihabParsedIsoTrack mehediVideoTrack = FindTrackByShihabHandler(mehediVidTracks, "vide");
                ShihabParsedIsoTrack shihabAudioTrack = FindTrackByShihabHandler(shihabAudTracks, "soun");

                if (mehediVideoTrack == null || mehediVideoTrack.ShihabSamples.Count == 0)
                {
                    throw new InvalidDataException("No decodable video samples found in MP4 video stream.");
                }
                if (shihabAudioTrack == null || shihabAudioTrack.ShihabSamples.Count == 0)
                {
                    File.Copy(shihabVideoPath, shihabOutputPath, true);
                    return;
                }

                WriteFastStartDualTrackMp4(mehediVidFs, mehediVideoTrack, shihabAudFs, shihabAudioTrack, null, shihabOutputPath);
            }
        }

        private static void MuxShihabMp4VideoWithWebmOpusToMp4(
            string shihabVideoPath,
            string mehediWebmAudioPath,
            string shihabOutputPath)
        {
            using (FileStream mehediVidFs = new FileStream(shihabVideoPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream shihabWebmFs = new FileStream(mehediWebmAudioPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool mehediEncV;
                List<ShihabParsedIsoTrack> shihabVidTracks = ParseMehediIsoBmffTracks(mehediVidFs, false, out mehediEncV);
                ShihabParsedIsoTrack mehediVideoTrack = FindTrackByShihabHandler(shihabVidTracks, "vide");
                ShihabParsedWebmStream shihabWebm = ParseShihabWebmFile(shihabWebmFs, false);

                if (mehediVideoTrack == null || mehediVideoTrack.ShihabSamples.Count == 0)
                {
                    throw new InvalidDataException("No video samples found in MP4 stream.");
                }
                if (shihabWebm.MehediAudioFrames.Count == 0 ||
                    shihabWebm.MehediAudioCodecId.IndexOf("OPUS", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    File.Copy(shihabVideoPath, shihabOutputPath, true);
                    return;
                }

                // Synthesize an in-memory backing stream + ISO-BMFF Opus ('Opus' + 'dOps') track at 48000 Hz timescale
                MemoryStream mehediOpusMem = new MemoryStream();
                ShihabParsedIsoTrack shihabOpusTrack = new ShihabParsedIsoTrack
                {
                    MehediTrackId = 2,
                    ShihabHandlerType = "soun",
                    MehediSampleEntryFourCc = "Opus",
                    ShihabTimescale = 48000,
                    MehediRawStsdBox = BuildIsoBmffOpusStsdBox(
                        shihabWebm.ShihabAudioCodecPrivate,
                        shihabWebm.MehediAudioChannels,
                        shihabWebm.ShihabAudioSampleRate)
                };

                for (int mehediI = 0; mehediI < shihabWebm.MehediAudioFrames.Count; mehediI++)
                {
                    MehediEbmlFramePacket shihabPkt = shihabWebm.MehediAudioFrames[mehediI];
                    long mehediNextTsMs = (mehediI + 1 < shihabWebm.MehediAudioFrames.Count)
                        ? shihabWebm.MehediAudioFrames[mehediI + 1].ShihabTimestampMs
                        : (shihabPkt.ShihabTimestampMs + 20L);
                    long shihabDeltaMs = Math.Max(5L, Math.Min(120L, mehediNextTsMs - shihabPkt.ShihabTimestampMs));
                    uint mehediDurTicks = (uint)Math.Max(1L, (shihabDeltaMs * 48000L) / 1000L);

                    long shihabPos = mehediOpusMem.Position;
                    mehediOpusMem.Write(shihabPkt.MehediRawFramePayload, 0, shihabPkt.MehediRawFramePayload.Length);

                    shihabOpusTrack.ShihabSamples.Add(new MehediIsoSampleRecord
                    {
                        ShihabFileOffset = shihabPos,
                        MehediSize = (uint)shihabPkt.MehediRawFramePayload.Length,
                        ShihabDuration = mehediDurTicks,
                        MehediCompositionOffset = 0,
                        ShihabIsSync = true
                    });
                }

                mehediOpusMem.Position = 0;
                WriteFastStartDualTrackMp4(mehediVidFs, mehediVideoTrack, mehediOpusMem, shihabOpusTrack, null, shihabOutputPath);
            }
        }

        private static byte[] BuildIsoBmffOpusStsdBox(byte[] mehediOpusHead, int shihabChannels, int mehediSampleRate)
        {
            byte shihabOutCh = (byte)Math.Max(1, shihabChannels);
            ushort mehediPreSkip = 312;
            uint shihabInRate = (uint)(mehediSampleRate > 0 ? mehediSampleRate : 48000);
            short mehediGain = 0;
            byte shihabMapFamily = 0;
            byte[] mehediExtraMap = new byte[0];

            if (mehediOpusHead != null && mehediOpusHead.Length >= 19 &&
                Encoding.ASCII.GetString(mehediOpusHead, 0, 8) == "OpusHead")
            {
                shihabOutCh = mehediOpusHead[9];
                mehediPreSkip = (ushort)(mehediOpusHead[10] | (mehediOpusHead[11] << 8));
                shihabInRate = (uint)(mehediOpusHead[12] | (mehediOpusHead[13] << 8) | (mehediOpusHead[14] << 16) | (mehediOpusHead[15] << 24));
                mehediGain = (short)(mehediOpusHead[16] | (mehediOpusHead[17] << 8));
                shihabMapFamily = mehediOpusHead[18];
                if (shihabMapFamily != 0 && mehediOpusHead.Length > 19)
                {
                    mehediExtraMap = new byte[mehediOpusHead.Length - 19];
                    Buffer.BlockCopy(mehediOpusHead, 19, mehediExtraMap, 0, mehediExtraMap.Length);
                }
            }

            byte[] shihabDops;
            using (MemoryStream mehediDopsMs = new MemoryStream())
            {
                mehediDopsMs.WriteByte(0); // Version 0 in ISO-BMFF dOps
                mehediDopsMs.WriteByte(shihabOutCh);
                WriteBeU16(mehediDopsMs, mehediPreSkip);
                WriteBeU32(mehediDopsMs, shihabInRate);
                WriteBeU16(mehediDopsMs, (ushort)mehediGain);
                mehediDopsMs.WriteByte(shihabMapFamily);
                if (mehediExtraMap.Length > 0) mehediDopsMs.Write(mehediExtraMap, 0, mehediExtraMap.Length);
                shihabDops = WrapIsoBox("dOps", mehediDopsMs.ToArray());
            }

            byte[] mehediOpusEntry;
            using (MemoryStream shihabEntryMs = new MemoryStream())
            {
                shihabEntryMs.Write(new byte[6], 0, 6); // reserved
                WriteBeU16(shihabEntryMs, 1); // data_reference_index = 1
                shihabEntryMs.Write(new byte[8], 0, 8); // reserved
                WriteBeU16(shihabEntryMs, shihabOutCh);
                WriteBeU16(shihabEntryMs, 16); // samplesize = 16
                WriteBeU16(shihabEntryMs, 0);
                WriteBeU16(shihabEntryMs, 0);
                WriteBeU32(shihabEntryMs, 48000u << 16); // samplerate 48000.0 fixed-point
                shihabEntryMs.Write(shihabDops, 0, shihabDops.Length);
                mehediOpusEntry = WrapIsoBox("Opus", shihabEntryMs.ToArray());
            }

            using (MemoryStream shihabStsdPayload = new MemoryStream())
            {
                WriteBeU32(shihabStsdPayload, 0); // version + flags
                WriteBeU32(shihabStsdPayload, 1); // entry_count = 1
                shihabStsdPayload.Write(mehediOpusEntry, 0, mehediOpusEntry.Length);
                return WrapIsoBox("stsd", shihabStsdPayload.ToArray());
            }
        }

        private static void WriteFastStartDualTrackMp4(
            Stream shihabVidStream,
            ShihabParsedIsoTrack mehediVidTrack,
            Stream shihabAudStream,
            ShihabParsedIsoTrack mehediAudTrack,
            byte[] shihabCustomFtyp,
            string mehediOutputPath)
        {
            // Group samples into ~0.5-second chunks per track and interleave Video & Audio chunks in mdat
            List<List<MehediIsoSampleRecord>> shihabVidChunks = GroupSamplesIntoHalfSecondChunks(mehediVidTrack);
            List<List<MehediIsoSampleRecord>> mehediAudChunks = GroupSamplesIntoHalfSecondChunks(mehediAudTrack);

            long[] shihabVidChunkRelOffsets = new long[shihabVidChunks.Count];
            long[] mehediAudChunkRelOffsets = new long[mehediAudChunks.Count];

            long shihabMdatPayloadLen = 0;
            int mehediVi = 0;
            int shihabAi = 0;
            double mehediVidTimeSec = 0.0;
            double shihabAudTimeSec = 0.0;

            List<bool> mehediInterleaveOrderIsVideo = new List<bool>(shihabVidChunks.Count + mehediAudChunks.Count);

            while (mehediVi < shihabVidChunks.Count || shihabAi < mehediAudChunks.Count)
            {
                bool shihabPickVideo;
                if (mehediVi >= shihabVidChunks.Count) shihabPickVideo = false;
                else if (shihabAi >= mehediAudChunks.Count) shihabPickVideo = true;
                else shihabPickVideo = (mehediVidTimeSec <= shihabAudTimeSec);

                mehediInterleaveOrderIsVideo.Add(shihabPickVideo);

                if (shihabPickVideo)
                {
                    shihabVidChunkRelOffsets[mehediVi] = shihabMdatPayloadLen;
                    List<MehediIsoSampleRecord> mehediChunk = shihabVidChunks[mehediVi];
                    long shihabTicks = 0;
                    for (int mehediS = 0; mehediS < mehediChunk.Count; mehediS++)
                    {
                        shihabMdatPayloadLen += mehediChunk[mehediS].MehediSize;
                        shihabTicks += mehediChunk[mehediS].ShihabDuration;
                    }
                    mehediVidTimeSec += shihabTicks / (double)Math.Max(1u, mehediVidTrack.ShihabTimescale);
                    mehediVi++;
                }
                else
                {
                    mehediAudChunkRelOffsets[shihabAi] = shihabMdatPayloadLen;
                    List<MehediIsoSampleRecord> shihabChunk = mehediAudChunks[shihabAi];
                    long mehediTicks = 0;
                    for (int shihabS = 0; shihabS < shihabChunk.Count; shihabS++)
                    {
                        shihabMdatPayloadLen += shihabChunk[shihabS].MehediSize;
                        mehediTicks += shihabChunk[shihabS].ShihabDuration;
                    }
                    shihabAudTimeSec += mehediTicks / (double)Math.Max(1u, mehediAudTrack.ShihabTimescale);
                    shihabAi++;
                }
            }

            byte[] shihabFtyp = shihabCustomFtyp ?? BuildStandardMp4FtypBox();
            bool mehediUse64BitMdat = (shihabMdatPayloadLen + 16L) > uint.MaxValue;
            int shihabMdatHeaderSize = mehediUse64BitMdat ? 16 : 8;

            // Pass 1: build moov with dummy 0 offsets to compute exact moov byte length
            long[] mehediDummyVidOffsets = new long[shihabVidChunks.Count];
            long[] shihabDummyAudOffsets = new long[mehediAudChunks.Count];
            byte[] mehediPass1Moov = BuildCompleteMoovBox(
                mehediVidTrack, shihabVidChunks, mehediDummyVidOffsets,
                mehediAudTrack, mehediAudChunks, shihabDummyAudOffsets);

            long shihabMdatDataStart = shihabFtyp.Length + mehediPass1Moov.Length + shihabMdatHeaderSize;

            long[] mehediFinalVidOffsets = new long[shihabVidChunks.Count];
            for (int shihabI = 0; shihabI < shihabVidChunks.Count; shihabI++)
            {
                mehediFinalVidOffsets[shihabI] = shihabMdatDataStart + shihabVidChunkRelOffsets[shihabI];
            }

            long[] shihabFinalAudOffsets = new long[mehediAudChunks.Count];
            for (int mehediI = 0; mehediI < mehediAudChunks.Count; mehediI++)
            {
                shihabFinalAudOffsets[mehediI] = shihabMdatDataStart + mehediAudChunkRelOffsets[mehediI];
            }

            // Pass 2: build final moov with exact 64-bit co64 absolute file offsets
            byte[] shihabFinalMoov = BuildCompleteMoovBox(
                mehediVidTrack, shihabVidChunks, mehediFinalVidOffsets,
                mehediAudTrack, mehediAudChunks, shihabFinalAudOffsets);

            Directory.CreateDirectory(Path.GetDirectoryName(mehediOutputPath));
            byte[] mehediCopyBuf = new byte[262144];

            using (FileStream shihabOutFs = new FileStream(mehediOutputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                shihabOutFs.Write(shihabFtyp, 0, shihabFtyp.Length);
                shihabOutFs.Write(shihabFinalMoov, 0, shihabFinalMoov.Length);

                if (mehediUse64BitMdat)
                {
                    WriteBeU32(shihabOutFs, 1u);
                    WriteAscii(shihabOutFs, "mdat");
                    WriteBeU64(shihabOutFs, (ulong)(shihabMdatPayloadLen + 16L));
                }
                else
                {
                    WriteBeU32(shihabOutFs, (uint)(shihabMdatPayloadLen + 8L));
                    WriteAscii(shihabOutFs, "mdat");
                }

                int shihabWriteVi = 0;
                int mehediWriteAi = 0;
                for (int shihabStep = 0; shihabStep < mehediInterleaveOrderIsVideo.Count; shihabStep++)
                {
                    if (mehediInterleaveOrderIsVideo[shihabStep])
                    {
                        CopyChunkSamplesToOutput(shihabVidStream, shihabVidChunks[shihabWriteVi++], shihabOutFs, mehediCopyBuf);
                    }
                    else
                    {
                        CopyChunkSamplesToOutput(shihabAudStream, mehediAudChunks[mehediWriteAi++], shihabOutFs, mehediCopyBuf);
                    }
                }
                shihabOutFs.Flush();
            }
        }

        private static void CopyChunkSamplesToOutput(
            Stream shihabSource,
            List<MehediIsoSampleRecord> mehediSamples,
            Stream shihabDest,
            byte[] mehediBuf)
        {
            for (int shihabI = 0; shihabI < mehediSamples.Count; shihabI++)
            {
                MehediIsoSampleRecord mehediRec = mehediSamples[shihabI];
                shihabSource.Position = mehediRec.ShihabFileOffset;
                uint shihabRem = mehediRec.MehediSize;
                while (shihabRem > 0)
                {
                    int mehediAsk = (int)Math.Min((uint)mehediBuf.Length, shihabRem);
                    int shihabGot = shihabSource.Read(mehediBuf, 0, mehediAsk);
                    if (shihabGot <= 0) throw new EndOfStreamException("Unexpected EOF while muxing media sample.");
                    shihabDest.Write(mehediBuf, 0, shihabGot);
                    shihabRem -= (uint)shihabGot;
                }
            }
        }

        private static List<List<MehediIsoSampleRecord>> GroupSamplesIntoHalfSecondChunks(ShihabParsedIsoTrack shihabTrack)
        {
            List<List<MehediIsoSampleRecord>> mehediChunks = new List<List<MehediIsoSampleRecord>>();
            uint shihabHalfSecTicks = Math.Max(1u, shihabTrack.ShihabTimescale / 2u);
            List<MehediIsoSampleRecord> mehediCurrent = new List<MehediIsoSampleRecord>();
            ulong shihabAccumTicks = 0;

            for (int mehediI = 0; mehediI < shihabTrack.ShihabSamples.Count; mehediI++)
            {
                MehediIsoSampleRecord shihabSmp = shihabTrack.ShihabSamples[mehediI];
                mehediCurrent.Add(shihabSmp);
                shihabAccumTicks += shihabSmp.ShihabDuration;
                if (shihabAccumTicks >= shihabHalfSecTicks || mehediCurrent.Count >= 64)
                {
                    mehediChunks.Add(mehediCurrent);
                    mehediCurrent = new List<MehediIsoSampleRecord>();
                    shihabAccumTicks = 0;
                }
            }
            if (mehediCurrent.Count > 0)
            {
                mehediChunks.Add(mehediCurrent);
            }
            return mehediChunks;
        }

        private static byte[] BuildStandardMp4FtypBox()
        {
            using (MemoryStream shihabMs = new MemoryStream())
            {
                WriteAscii(shihabMs, "isom");
                WriteBeU32(shihabMs, 0x00000200u);
                WriteAscii(shihabMs, "isom");
                WriteAscii(shihabMs, "iso2");
                WriteAscii(shihabMs, "avc1");
                WriteAscii(shihabMs, "mp41");
                return WrapIsoBox("ftyp", shihabMs.ToArray());
            }
        }

        private static byte[] BuildCompleteMoovBox(
            ShihabParsedIsoTrack shihabVidTrack,
            List<List<MehediIsoSampleRecord>> mehediVidChunks,
            long[] shihabVidOffsets,
            ShihabParsedIsoTrack mehediAudTrack,
            List<List<MehediIsoSampleRecord>> shihabAudChunks,
            long[] mehediAudOffsets)
        {
            ulong shihabVidTicks = SumTrackTicks(shihabVidTrack);
            ulong mehediAudTicks = SumTrackTicks(mehediAudTrack);

            ulong shihabVidMs = (shihabVidTicks * 1000UL) / Math.Max(1UL, shihabVidTrack.ShihabTimescale);
            ulong mehediAudMs = (mehediAudTicks * 1000UL) / Math.Max(1UL, mehediAudTrack.ShihabTimescale);
            ulong shihabMovieMs = Math.Max(shihabVidMs, mehediAudMs);

            byte[] mehediMvhd = BuildMvhdBox(1000u, shihabMovieMs, 3u);
            byte[] shihabTrak1 = BuildTrakBox(1u, true, shihabVidTrack, shihabVidMs, shihabVidTicks, mehediVidChunks, shihabVidOffsets);
            byte[] mehediTrak2 = BuildTrakBox(2u, false, mehediAudTrack, mehediAudMs, mehediAudTicks, shihabAudChunks, mehediAudOffsets);

            using (MemoryStream shihabMoovMs = new MemoryStream())
            {
                shihabMoovMs.Write(mehediMvhd, 0, mehediMvhd.Length);
                shihabMoovMs.Write(shihabTrak1, 0, shihabTrak1.Length);
                shihabMoovMs.Write(mehediTrak2, 0, mehediTrak2.Length);
                return WrapIsoBox("moov", shihabMoovMs.ToArray());
            }
        }

        private static ulong SumTrackTicks(ShihabParsedIsoTrack mehediTrack)
        {
            ulong shihabTotal = 0;
            for (int mehediI = 0; mehediI < mehediTrack.ShihabSamples.Count; mehediI++)
            {
                uint shihabDur = mehediTrack.ShihabSamples[mehediI].ShihabDuration;
                if (shihabDur == 0)
                {
                    shihabDur = mehediTrack.ShihabHandlerType == "vide"
                        ? Math.Max(1u, mehediTrack.ShihabTimescale / 30u)
                        : 1024u;
                    mehediTrack.ShihabSamples[mehediI].ShihabDuration = shihabDur;
                }
                shihabTotal += shihabDur;
            }
            return shihabTotal;
        }

        private static byte[] BuildMvhdBox(uint shihabTimescale, ulong mehediDuration, uint shihabNextTrackId)
        {
            using (MemoryStream mehediMs = new MemoryStream())
            {
                bool shihabV1 = mehediDuration > uint.MaxValue;
                WriteBeU32(mehediMs, shihabV1 ? 0x01000000u : 0u);
                if (shihabV1)
                {
                    WriteBeU64(mehediMs, 0UL);
                    WriteBeU64(mehediMs, 0UL);
                    WriteBeU32(mehediMs, shihabTimescale);
                    WriteBeU64(mehediMs, mehediDuration);
                }
                else
                {
                    WriteBeU32(mehediMs, 0u);
                    WriteBeU32(mehediMs, 0u);
                    WriteBeU32(mehediMs, shihabTimescale);
                    WriteBeU32(mehediMs, (uint)mehediDuration);
                }
                WriteBeU32(mehediMs, 0x00010000u); // rate 1.0
                WriteBeU16(mehediMs, 0x0100);      // volume 1.0
                mehediMs.Write(new byte[10], 0, 10);
                WriteIdentityMatrix(mehediMs);
                mehediMs.Write(new byte[24], 0, 24);
                WriteBeU32(mehediMs, shihabNextTrackId);
                return WrapIsoBox("mvhd", mehediMs.ToArray());
            }
        }

        private static byte[] BuildTrakBox(
            uint shihabTrackId,
            bool mehediIsVideo,
            ShihabParsedIsoTrack shihabTrack,
            ulong mehediMovieDurMs,
            ulong shihabTrackTicks,
            List<List<MehediIsoSampleRecord>> mehediChunks,
            long[] shihabChunkOffsets)
        {
            byte[] mehediTkhd;
            using (MemoryStream shihabMs = new MemoryStream())
            {
                bool mehediV1 = mehediMovieDurMs > uint.MaxValue;
                WriteBeU32(shihabMs, (mehediV1 ? 0x01000000u : 0u) | 0x000003u); // track_enabled | track_in_movie
                if (mehediV1)
                {
                    WriteBeU64(shihabMs, 0UL);
                    WriteBeU64(shihabMs, 0UL);
                    WriteBeU32(shihabMs, shihabTrackId);
                    WriteBeU32(shihabMs, 0u);
                    WriteBeU64(shihabMs, mehediMovieDurMs);
                }
                else
                {
                    WriteBeU32(shihabMs, 0u);
                    WriteBeU32(shihabMs, 0u);
                    WriteBeU32(shihabMs, shihabTrackId);
                    WriteBeU32(shihabMs, 0u);
                    WriteBeU32(shihabMs, (uint)mehediMovieDurMs);
                }
                shihabMs.Write(new byte[8], 0, 8);
                WriteBeU16(shihabMs, 0); // layer
                WriteBeU16(shihabMs, 0); // alternate_group
                WriteBeU16(shihabMs, (ushort)(mehediIsVideo ? 0 : 0x0100));
                WriteBeU16(shihabMs, 0);
                WriteIdentityMatrix(shihabMs);
                uint mehediFixedW = mehediIsVideo ? ((uint)Math.Max(0, shihabTrack.MehediWidth) << 16) : 0u;
                uint shihabFixedH = mehediIsVideo ? ((uint)Math.Max(0, shihabTrack.ShihabHeight) << 16) : 0u;
                WriteBeU32(shihabMs, mehediFixedW);
                WriteBeU32(shihabMs, shihabFixedH);
                mehediTkhd = WrapIsoBox("tkhd", shihabMs.ToArray());
            }

            byte[] shihabMdhd;
            using (MemoryStream mehediMs = new MemoryStream())
            {
                bool shihabV1 = shihabTrackTicks > uint.MaxValue;
                WriteBeU32(mehediMs, shihabV1 ? 0x01000000u : 0u);
                if (shihabV1)
                {
                    WriteBeU64(mehediMs, 0UL);
                    WriteBeU64(mehediMs, 0UL);
                    WriteBeU32(mehediMs, Math.Max(1u, shihabTrack.ShihabTimescale));
                    WriteBeU64(mehediMs, shihabTrackTicks);
                }
                else
                {
                    WriteBeU32(mehediMs, 0u);
                    WriteBeU32(mehediMs, 0u);
                    WriteBeU32(mehediMs, Math.Max(1u, shihabTrack.ShihabTimescale));
                    WriteBeU32(mehediMs, (uint)shihabTrackTicks);
                }
                WriteBeU16(mehediMs, 0x55C4); // 'und'
                WriteBeU16(mehediMs, 0);
                shihabMdhd = WrapIsoBox("mdhd", mehediMs.ToArray());
            }

            byte[] mehediHdlr;
            using (MemoryStream shihabMs = new MemoryStream())
            {
                WriteBeU32(shihabMs, 0u);
                WriteBeU32(shihabMs, 0u);
                WriteAscii(shihabMs, mehediIsVideo ? "vide" : "soun");
                shihabMs.Write(new byte[12], 0, 12);
                byte[] mehediName = Encoding.ASCII.GetBytes(mehediIsVideo ? "VideoHandler\0" : "SoundHandler\0");
                shihabMs.Write(mehediName, 0, mehediName.Length);
                mehediHdlr = WrapIsoBox("hdlr", shihabMs.ToArray());
            }

            byte[] shihabMediaHeader;
            if (mehediIsVideo)
            {
                using (MemoryStream mehediMs = new MemoryStream())
                {
                    WriteBeU32(mehediMs, 1u); // version 0, flags 1
                    WriteBeU16(mehediMs, 0);
                    mehediMs.Write(new byte[6], 0, 6);
                    shihabMediaHeader = WrapIsoBox("vmhd", mehediMs.ToArray());
                }
            }
            else
            {
                using (MemoryStream mehediMs = new MemoryStream())
                {
                    WriteBeU32(mehediMs, 0u);
                    WriteBeU16(mehediMs, 0);
                    WriteBeU16(mehediMs, 0);
                    shihabMediaHeader = WrapIsoBox("smhd", mehediMs.ToArray());
                }
            }

            byte[] mehediDinf;
            using (MemoryStream shihabUrlMs = new MemoryStream())
            using (MemoryStream mehediDrefMs = new MemoryStream())
            {
                WriteBeU32(shihabUrlMs, 1u); // self-contained
                byte[] shihabUrlBox = WrapIsoBox("url ", shihabUrlMs.ToArray());
                WriteBeU32(mehediDrefMs, 0u);
                WriteBeU32(mehediDrefMs, 1u);
                mehediDrefMs.Write(shihabUrlBox, 0, shihabUrlBox.Length);
                mehediDinf = WrapIsoBox("dinf", WrapIsoBox("dref", mehediDrefMs.ToArray()));
            }

            byte[] shihabStbl = BuildStblBox(mehediIsVideo, shihabTrack, mehediChunks, shihabChunkOffsets);

            byte[] mehediMinf;
            using (MemoryStream shihabMs = new MemoryStream())
            {
                shihabMs.Write(shihabMediaHeader, 0, shihabMediaHeader.Length);
                shihabMs.Write(mehediDinf, 0, mehediDinf.Length);
                shihabMs.Write(shihabStbl, 0, shihabStbl.Length);
                mehediMinf = WrapIsoBox("minf", shihabMs.ToArray());
            }

            byte[] shihabMdia;
            using (MemoryStream mehediMs = new MemoryStream())
            {
                mehediMs.Write(shihabMdhd, 0, shihabMdhd.Length);
                mehediMs.Write(mehediHdlr, 0, mehediHdlr.Length);
                mehediMs.Write(mehediMinf, 0, mehediMinf.Length);
                shihabMdia = WrapIsoBox("mdia", mehediMs.ToArray());
            }

            using (MemoryStream shihabTrakMs = new MemoryStream())
            {
                shihabTrakMs.Write(mehediTkhd, 0, mehediTkhd.Length);
                shihabTrakMs.Write(shihabMdia, 0, shihabMdia.Length);
                return WrapIsoBox("trak", shihabTrakMs.ToArray());
            }
        }

        private static byte[] BuildStblBox(
            bool mehediIsVideo,
            ShihabParsedIsoTrack shihabTrack,
            List<List<MehediIsoSampleRecord>> mehediChunks,
            long[] shihabChunkOffsets)
        {
            List<MehediIsoSampleRecord> shihabSamples = shihabTrack.ShihabSamples;

            // 1. stts (run-length encoded durations)
            List<uint> mehediSttsCounts = new List<uint>();
            List<uint> shihabSttsDeltas = new List<uint>();
            for (int mehediI = 0; mehediI < shihabSamples.Count; mehediI++)
            {
                uint shihabDur = shihabSamples[mehediI].ShihabDuration;
                if (mehediSttsCounts.Count > 0 && shihabSttsDeltas[shihabSttsDeltas.Count - 1] == shihabDur)
                {
                    mehediSttsCounts[mehediSttsCounts.Count - 1]++;
                }
                else
                {
                    mehediSttsCounts.Add(1u);
                    shihabSttsDeltas.Add(shihabDur);
                }
            }
            byte[] mehediStts;
            using (MemoryStream shihabMs = new MemoryStream())
            {
                WriteBeU32(shihabMs, 0u);
                WriteBeU32(shihabMs, (uint)mehediSttsCounts.Count);
                for (int mehediI = 0; mehediI < mehediSttsCounts.Count; mehediI++)
                {
                    WriteBeU32(shihabMs, mehediSttsCounts[mehediI]);
                    WriteBeU32(shihabMs, shihabSttsDeltas[mehediI]);
                }
                mehediStts = WrapIsoBox("stts", shihabMs.ToArray());
            }

            // 2. ctts (optional composition offsets for B-frames in video)
            byte[] shihabCtts = null;
            if (mehediIsVideo)
            {
                bool mehediHasNonZeroCto = false;
                bool shihabHasNegativeCto = false;
                for (int mehediI = 0; mehediI < shihabSamples.Count; mehediI++)
                {
                    int shihabCto = shihabSamples[mehediI].MehediCompositionOffset;
                    if (shihabCto != 0) mehediHasNonZeroCto = true;
                    if (shihabCto < 0) shihabHasNegativeCto = true;
                }
                if (mehediHasNonZeroCto)
                {
                    List<uint> mehediCttsCounts = new List<uint>();
                    List<int> shihabCttsOffsets = new List<int>();
                    for (int mehediI = 0; mehediI < shihabSamples.Count; mehediI++)
                    {
                        int shihabCto = shihabSamples[mehediI].MehediCompositionOffset;
                        if (mehediCttsCounts.Count > 0 && shihabCttsOffsets[shihabCttsOffsets.Count - 1] == shihabCto)
                        {
                            mehediCttsCounts[mehediCttsCounts.Count - 1]++;
                        }
                        else
                        {
                            mehediCttsCounts.Add(1u);
                            shihabCttsOffsets.Add(shihabCto);
                        }
                    }
                    using (MemoryStream mehediMs = new MemoryStream())
                    {
                        WriteBeU32(mehediMs, shihabHasNegativeCto ? 0x01000000u : 0u);
                        WriteBeU32(mehediMs, (uint)mehediCttsCounts.Count);
                        for (int shihabI = 0; shihabI < mehediCttsCounts.Count; shihabI++)
                        {
                            WriteBeU32(mehediMs, mehediCttsCounts[shihabI]);
                            WriteBeU32(mehediMs, unchecked((uint)shihabCttsOffsets[shihabI]));
                        }
                        shihabCtts = WrapIsoBox("ctts", mehediMs.ToArray());
                    }
                }
            }

            // 3. stss (sync keyframes for video)
            byte[] mehediStss = null;
            if (mehediIsVideo)
            {
                List<uint> shihabSyncIndices = new List<uint>();
                for (int mehediI = 0; mehediI < shihabSamples.Count; mehediI++)
                {
                    if (shihabSamples[mehediI].ShihabIsSync)
                    {
                        shihabSyncIndices.Add((uint)(mehediI + 1));
                    }
                }
                if (shihabSyncIndices.Count > 0 && shihabSyncIndices.Count < shihabSamples.Count)
                {
                    using (MemoryStream mehediMs = new MemoryStream())
                    {
                        WriteBeU32(mehediMs, 0u);
                        WriteBeU32(mehediMs, (uint)shihabSyncIndices.Count);
                        for (int shihabI = 0; shihabI < shihabSyncIndices.Count; shihabI++)
                        {
                            WriteBeU32(mehediMs, shihabSyncIndices[shihabI]);
                        }
                        mehediStss = WrapIsoBox("stss", mehediMs.ToArray());
                    }
                }
            }

            // 4. stsc (sample-to-chunk compressed entries)
            List<uint> shihabFirstChunks = new List<uint>();
            List<uint> mehediSamplesPerChunk = new List<uint>();
            for (int shihabC = 0; shihabC < mehediChunks.Count; shihabC++)
            {
                uint mehediCnt = (uint)mehediChunks[shihabC].Count;
                if (mehediSamplesPerChunk.Count == 0 || mehediSamplesPerChunk[mehediSamplesPerChunk.Count - 1] != mehediCnt)
                {
                    shihabFirstChunks.Add((uint)(shihabC + 1));
                    mehediSamplesPerChunk.Add(mehediCnt);
                }
            }
            byte[] shihabStsc;
            using (MemoryStream mehediMs = new MemoryStream())
            {
                WriteBeU32(mehediMs, 0u);
                WriteBeU32(mehediMs, (uint)shihabFirstChunks.Count);
                for (int shihabI = 0; shihabI < shihabFirstChunks.Count; shihabI++)
                {
                    WriteBeU32(mehediMs, shihabFirstChunks[shihabI]);
                    WriteBeU32(mehediMs, mehediSamplesPerChunk[shihabI]);
                    WriteBeU32(mehediMs, 1u);
                }
                shihabStsc = WrapIsoBox("stsc", mehediMs.ToArray());
            }

            // 5. stsz (sample sizes)
            byte[] mehediStsz;
            using (MemoryStream shihabMs = new MemoryStream())
            {
                WriteBeU32(shihabMs, 0u);
                WriteBeU32(shihabMs, 0u);
                WriteBeU32(shihabMs, (uint)shihabSamples.Count);
                for (int mehediI = 0; mehediI < shihabSamples.Count; mehediI++)
                {
                    WriteBeU32(shihabMs, shihabSamples[mehediI].MehediSize);
                }
                mehediStsz = WrapIsoBox("stsz", shihabMs.ToArray());
            }

            // 6. co64 (64-bit chunk offsets — handles >4GB 4K files cleanly)
            byte[] shihabCo64;
            using (MemoryStream mehediMs = new MemoryStream())
            {
                WriteBeU32(mehediMs, 0u);
                WriteBeU32(mehediMs, (uint)shihabChunkOffsets.Length);
                for (int shihabI = 0; shihabI < shihabChunkOffsets.Length; shihabI++)
                {
                    WriteBeU64(mehediMs, (ulong)shihabChunkOffsets[shihabI]);
                }
                shihabCo64 = WrapIsoBox("co64", mehediMs.ToArray());
            }

            using (MemoryStream mehediStblMs = new MemoryStream())
            {
                mehediStblMs.Write(shihabTrack.MehediRawStsdBox, 0, shihabTrack.MehediRawStsdBox.Length);
                mehediStblMs.Write(mehediStts, 0, mehediStts.Length);
                if (shihabCtts != null) mehediStblMs.Write(shihabCtts, 0, shihabCtts.Length);
                if (mehediStss != null) mehediStblMs.Write(mehediStss, 0, mehediStss.Length);
                mehediStblMs.Write(shihabStsc, 0, shihabStsc.Length);
                mehediStblMs.Write(mehediStsz, 0, mehediStsz.Length);
                mehediStblMs.Write(shihabCo64, 0, shihabCo64.Length);
                return WrapIsoBox("stbl", mehediStblMs.ToArray());
            }
        }

        private static ShihabParsedIsoTrack FindTrackByShihabHandler(List<ShihabParsedIsoTrack> shihabTracks, string mehediHandler)
        {
            for (int shihabI = 0; shihabI < shihabTracks.Count; shihabI++)
            {
                if (shihabTracks[shihabI].ShihabHandlerType == mehediHandler)
                {
                    return shihabTracks[shihabI];
                }
            }
            return null;
        }

        private static List<ShihabParsedIsoTrack> ParseMehediIsoBmffTracks(
            Stream shihabStream,
            bool mehediMetadataOnly,
            out bool shihabIsEncrypted)
        {
            shihabIsEncrypted = false;
            List<ShihabParsedIsoTrack> mehediTracks = new List<ShihabParsedIsoTrack>();
            Dictionary<uint, ShihabParsedIsoTrack> shihabTrackById = new Dictionary<uint, ShihabParsedIsoTrack>();

            long mehediLen = shihabStream.Length;
            long shihabPos = 0;
            List<byte[]> shihabPendingMoofBytes = new List<byte[]>();
            List<long> mehediPendingMoofStarts = new List<long>();

            while (shihabPos + 8 <= mehediLen)
            {
                shihabStream.Position = shihabPos;
                long mehediBoxSize;
                string shihabType;
                int mehediHdrLen;
                if (!ReadIsoBoxHeader(shihabStream, mehediLen - shihabPos, out mehediBoxSize, out shihabType, out mehediHdrLen))
                {
                    break;
                }

                if (shihabType == "moov")
                {
                    byte[] mehediMoovPayload = ReadExactBytesFromStream(shihabStream, (int)(mehediBoxSize - mehediHdrLen));
                    ParseMoovBuffer(mehediMoovPayload, mehediMetadataOnly, mehediTracks, shihabTrackById, ref shihabIsEncrypted);
                    if (mehediMetadataOnly)
                    {
                        return mehediTracks;
                    }
                }
                else if (!mehediMetadataOnly && shihabType == "moof")
                {
                    byte[] shihabMoofPayload = ReadExactBytesFromStream(shihabStream, (int)(mehediBoxSize - mehediHdrLen));
                    shihabPendingMoofBytes.Add(shihabMoofPayload);
                    mehediPendingMoofStarts.Add(shihabPos);
                }
                else if (!mehediMetadataOnly && shihabType == "mdat" && shihabPendingMoofBytes.Count > 0)
                {
                    long shihabMdatPayloadStart = shihabPos + mehediHdrLen;
                    for (int mehediM = 0; mehediM < shihabPendingMoofBytes.Count; mehediM++)
                    {
                        ParseMoofFragment(
                            shihabPendingMoofBytes[mehediM],
                            mehediPendingMoofStarts[mehediM],
                            shihabMdatPayloadStart,
                            mehediTracks,
                            shihabTrackById);
                    }
                    shihabPendingMoofBytes.Clear();
                    mehediPendingMoofStarts.Clear();
                }

                if (mehediBoxSize <= 0) break;
                shihabPos += mehediBoxSize;
            }

            return mehediTracks;
        }

        private static void ParseMoovBuffer(
            byte[] shihabMoov,
            bool mehediMetadataOnly,
            List<ShihabParsedIsoTrack> shihabTracks,
            Dictionary<uint, ShihabParsedIsoTrack> mehediById,
            ref bool shihabIsEncrypted)
        {
            int mehediOff = 0;
            while (mehediOff + 8 <= shihabMoov.Length)
            {
                int shihabBoxLen;
                string mehediType;
                int shihabHdr;
                if (!ReadIsoBoxHeaderFromBuf(shihabMoov, mehediOff, shihabMoov.Length, out shihabBoxLen, out mehediType, out shihabHdr))
                {
                    break;
                }

                if (mehediType == "pssh")
                {
                    shihabIsEncrypted = true;
                }
                else if (mehediType == "trak")
                {
                    ShihabParsedIsoTrack shihabTrk = ParseSingleTrakFromBuf(
                        shihabMoov,
                        mehediOff + shihabHdr,
                        mehediOff + shihabBoxLen,
                        mehediMetadataOnly,
                        ref shihabIsEncrypted);
                    if (shihabTrk != null)
                    {
                        shihabTracks.Add(shihabTrk);
                        if (shihabTrk.MehediTrackId > 0)
                        {
                            mehediById[shihabTrk.MehediTrackId] = shihabTrk;
                        }
                    }
                }
                else if (mehediType == "mvex")
                {
                    ParseMvexFromBuf(shihabMoov, mehediOff + shihabHdr, mehediOff + shihabBoxLen, mehediById);
                }

                mehediOff += shihabBoxLen;
            }
        }

        private static ShihabParsedIsoTrack ParseSingleTrakFromBuf(
            byte[] shihabBuf,
            int mehediStart,
            int shihabEnd,
            bool mehediMetadataOnly,
            ref bool shihabIsEncrypted)
        {
            ShihabParsedIsoTrack mehediTrk = new ShihabParsedIsoTrack();
            int shihabOff = mehediStart;
            while (shihabOff + 8 <= shihabEnd)
            {
                int mehediLen;
                string shihabType;
                int mehediHdr;
                if (!ReadIsoBoxHeaderFromBuf(shihabBuf, shihabOff, shihabEnd, out mehediLen, out shihabType, out mehediHdr)) break;
                int shihabPayStart = shihabOff + mehediHdr;
                int mehediPayEnd = shihabOff + mehediLen;

                if (shihabType == "tkhd" && mehediPayEnd - shihabPayStart >= 84)
                {
                    byte shihabVer = shihabBuf[shihabPayStart];
                    if (shihabVer == 1 && mehediPayEnd - shihabPayStart >= 96)
                    {
                        mehediTrk.MehediTrackId = ReadBeU32(shihabBuf, shihabPayStart + 20);
                        mehediTrk.MehediWidth = (int)(ReadBeU32(shihabBuf, shihabPayStart + 88) >> 16);
                        mehediTrk.ShihabHeight = (int)(ReadBeU32(shihabBuf, shihabPayStart + 92) >> 16);
                    }
                    else
                    {
                        mehediTrk.MehediTrackId = ReadBeU32(shihabBuf, shihabPayStart + 12);
                        mehediTrk.MehediWidth = (int)(ReadBeU32(shihabBuf, shihabPayStart + 76) >> 16);
                        mehediTrk.ShihabHeight = (int)(ReadBeU32(shihabBuf, shihabPayStart + 80) >> 16);
                    }
                }
                else if (shihabType == "mdia")
                {
                    ParseMdiaFromBuf(shihabBuf, shihabPayStart, mehediPayEnd, mehediTrk, mehediMetadataOnly, ref shihabIsEncrypted);
                }

                shihabOff += mehediLen;
            }
            return mehediTrk;
        }

        private static void ParseMdiaFromBuf(
            byte[] shihabBuf,
            int mehediStart,
            int shihabEnd,
            ShihabParsedIsoTrack mehediTrk,
            bool shihabMetadataOnly,
            ref bool mehediIsEncrypted)
        {
            int shihabOff = mehediStart;
            while (shihabOff + 8 <= shihabEnd)
            {
                int mehediLen;
                string shihabType;
                int mehediHdr;
                if (!ReadIsoBoxHeaderFromBuf(shihabBuf, shihabOff, shihabEnd, out mehediLen, out shihabType, out mehediHdr)) break;
                int shihabPayStart = shihabOff + mehediHdr;
                int mehediPayEnd = shihabOff + mehediLen;

                if (shihabType == "mdhd" && mehediPayEnd - shihabPayStart >= 20)
                {
                    byte mehediVer = shihabBuf[shihabPayStart];
                    mehediTrk.ShihabTimescale = mehediVer == 1
                        ? ReadBeU32(shihabBuf, shihabPayStart + 20)
                        : ReadBeU32(shihabBuf, shihabPayStart + 12);
                }
                else if (shihabType == "hdlr" && mehediPayEnd - shihabPayStart >= 12)
                {
                    mehediTrk.ShihabHandlerType = Encoding.ASCII.GetString(shihabBuf, shihabPayStart + 8, 4);
                }
                else if (shihabType == "minf")
                {
                    ParseMinfFromBuf(shihabBuf, shihabPayStart, mehediPayEnd, mehediTrk, shihabMetadataOnly, ref mehediIsEncrypted);
                }

                shihabOff += mehediLen;
            }
        }

        private static void ParseMinfFromBuf(
            byte[] shihabBuf,
            int mehediStart,
            int shihabEnd,
            ShihabParsedIsoTrack mehediTrk,
            bool shihabMetadataOnly,
            ref bool mehediIsEncrypted)
        {
            int shihabOff = mehediStart;
            while (shihabOff + 8 <= shihabEnd)
            {
                int mehediLen;
                string shihabType;
                int mehediHdr;
                if (!ReadIsoBoxHeaderFromBuf(shihabBuf, shihabOff, shihabEnd, out mehediLen, out shihabType, out mehediHdr)) break;
                if (shihabType == "stbl")
                {
                    ParseStblFromBuf(shihabBuf, shihabOff + mehediHdr, shihabOff + mehediLen, mehediTrk, shihabMetadataOnly, ref mehediIsEncrypted);
                }
                shihabOff += mehediLen;
            }
        }

        private static void ParseStblFromBuf(
            byte[] shihabBuf,
            int mehediStart,
            int shihabEnd,
            ShihabParsedIsoTrack mehediTrk,
            bool shihabMetadataOnly,
            ref bool mehediIsEncrypted)
        {
            List<uint> shihabDurations = new List<uint>();
            List<int> mehediCtoList = new List<int>();
            HashSet<uint> shihabSyncSet = null;
            List<uint> mehediSizes = new List<uint>();
            List<ulong> shihabChunkOffsets = new List<ulong>();
            List<uint[]> mehediStscEntries = new List<uint[]>();

            int shihabOff = mehediStart;
            while (shihabOff + 8 <= shihabEnd)
            {
                int mehediLen;
                string shihabType;
                int mehediHdr;
                if (!ReadIsoBoxHeaderFromBuf(shihabBuf, shihabOff, shihabEnd, out mehediLen, out shihabType, out mehediHdr)) break;
                int shihabPay = shihabOff + mehediHdr;
                int mehediPayEnd = shihabOff + mehediLen;

                if (shihabType == "stsd")
                {
                    mehediTrk.MehediRawStsdBox = new byte[mehediLen];
                    Buffer.BlockCopy(shihabBuf, shihabOff, mehediTrk.MehediRawStsdBox, 0, mehediLen);

                    if (mehediPayEnd - shihabPay >= 16)
                    {
                        int shihabEntryOff = shihabPay + 8;
                        int mehediEntryLen;
                        string shihabEntryFourCc;
                        int mehediEntryHdr;
                        if (ReadIsoBoxHeaderFromBuf(shihabBuf, shihabEntryOff, mehediPayEnd, out mehediEntryLen, out shihabEntryFourCc, out mehediEntryHdr))
                        {
                            mehediTrk.MehediSampleEntryFourCc = shihabEntryFourCc;
                            if (shihabEntryFourCc == "encv" || shihabEntryFourCc == "enca")
                            {
                                mehediIsEncrypted = true;
                            }
                            if ((mehediTrk.MehediWidth <= 0 || mehediTrk.ShihabHeight <= 0) &&
                                shihabEntryOff + mehediEntryHdr + 32 <= mehediPayEnd &&
                                mehediTrk.ShihabHandlerType == "vide")
                            {
                                mehediTrk.MehediWidth = ReadBeU16(shihabBuf, shihabEntryOff + mehediEntryHdr + 24);
                                mehediTrk.ShihabHeight = ReadBeU16(shihabBuf, shihabEntryOff + mehediEntryHdr + 26);
                            }
                            if (shihabEntryFourCc == "mp4a")
                            {
                                mehediTrk.ShihabAacAudioSpecificConfig = TryExtractEsdsAudioSpecificConfig(
                                    shihabBuf,
                                    shihabEntryOff + mehediEntryHdr + 28,
                                    shihabEntryOff + mehediEntryLen);
                            }
                        }
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "stts" && mehediPayEnd - shihabPay >= 8)
                {
                    uint mehediCount = ReadBeU32(shihabBuf, shihabPay + 4);
                    int shihabCur = shihabPay + 8;
                    for (uint mehediI = 0; mehediI < mehediCount && shihabCur + 8 <= mehediPayEnd; mehediI++)
                    {
                        uint shihabSampleCount = ReadBeU32(shihabBuf, shihabCur);
                        uint mehediDelta = ReadBeU32(shihabBuf, shihabCur + 4);
                        for (uint shihabK = 0; shihabK < shihabSampleCount; shihabK++) shihabDurations.Add(mehediDelta);
                        shihabCur += 8;
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "ctts" && mehediPayEnd - shihabPay >= 8)
                {
                    uint shihabCount = ReadBeU32(shihabBuf, shihabPay + 4);
                    int mehediCur = shihabPay + 8;
                    for (uint shihabI = 0; shihabI < shihabCount && mehediCur + 8 <= mehediPayEnd; shihabI++)
                    {
                        uint mehediSampleCount = ReadBeU32(shihabBuf, mehediCur);
                        int shihabOffsetVal = unchecked((int)ReadBeU32(shihabBuf, mehediCur + 4));
                        for (uint mehediK = 0; mehediK < mehediSampleCount; mehediK++) mehediCtoList.Add(shihabOffsetVal);
                        mehediCur += 8;
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "stss" && mehediPayEnd - shihabPay >= 8)
                {
                    shihabSyncSet = new HashSet<uint>();
                    uint mehediCount = ReadBeU32(shihabBuf, shihabPay + 4);
                    int shihabCur = shihabPay + 8;
                    for (uint mehediI = 0; mehediI < mehediCount && shihabCur + 4 <= mehediPayEnd; mehediI++)
                    {
                        shihabSyncSet.Add(ReadBeU32(shihabBuf, shihabCur));
                        shihabCur += 4;
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "stsz" && mehediPayEnd - shihabPay >= 12)
                {
                    uint shihabUniformSize = ReadBeU32(shihabBuf, shihabPay + 4);
                    uint mehediCount = ReadBeU32(shihabBuf, shihabPay + 8);
                    int shihabCur = shihabPay + 12;
                    for (uint mehediI = 0; mehediI < mehediCount; mehediI++)
                    {
                        if (shihabUniformSize > 0) mehediSizes.Add(shihabUniformSize);
                        else if (shihabCur + 4 <= mehediPayEnd) { mehediSizes.Add(ReadBeU32(shihabBuf, shihabCur)); shihabCur += 4; }
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "stco" && mehediPayEnd - shihabPay >= 8)
                {
                    uint mehediCount = ReadBeU32(shihabBuf, shihabPay + 4);
                    int shihabCur = shihabPay + 8;
                    for (uint mehediI = 0; mehediI < mehediCount && shihabCur + 4 <= mehediPayEnd; mehediI++)
                    {
                        shihabChunkOffsets.Add(ReadBeU32(shihabBuf, shihabCur));
                        shihabCur += 4;
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "co64" && mehediPayEnd - shihabPay >= 8)
                {
                    uint mehediCount = ReadBeU32(shihabBuf, shihabPay + 4);
                    int shihabCur = shihabPay + 8;
                    for (uint mehediI = 0; mehediI < mehediCount && shihabCur + 8 <= mehediPayEnd; mehediI++)
                    {
                        shihabChunkOffsets.Add(ReadBeU64(shihabBuf, shihabCur));
                        shihabCur += 8;
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "stsc" && mehediPayEnd - shihabPay >= 8)
                {
                    uint shihabCount = ReadBeU32(shihabBuf, shihabPay + 4);
                    int mehediCur = shihabPay + 8;
                    for (uint shihabI = 0; shihabI < shihabCount && mehediCur + 12 <= mehediPayEnd; shihabI++)
                    {
                        mehediStscEntries.Add(new uint[]
                        {
                            ReadBeU32(shihabBuf, mehediCur),
                            ReadBeU32(shihabBuf, mehediCur + 4)
                        });
                        mehediCur += 12;
                    }
                }

                shihabOff += mehediLen;
            }

            // Reconstruct progressive stbl samples if non-fragmented MP4
            if (!shihabMetadataOnly && mehediSizes.Count > 0 && shihabChunkOffsets.Count > 0 && mehediStscEntries.Count > 0)
            {
                int shihabSampleIdx = 0;
                int mehediStscPtr = 0;
                for (int shihabChunkIdx = 0; shihabChunkIdx < shihabChunkOffsets.Count && shihabSampleIdx < mehediSizes.Count; shihabChunkIdx++)
                {
                    uint mehediChunkNum1Based = (uint)(shihabChunkIdx + 1);
                    while (mehediStscPtr + 1 < mehediStscEntries.Count && mehediChunkNum1Based >= mehediStscEntries[mehediStscPtr + 1][0])
                    {
                        mehediStscPtr++;
                    }
                    uint shihabSamplesInChunk = mehediStscEntries[mehediStscPtr][1];
                    long mehediByteCursor = (long)shihabChunkOffsets[shihabChunkIdx];

                    for (uint shihabS = 0; shihabS < shihabSamplesInChunk && shihabSampleIdx < mehediSizes.Count; shihabS++)
                    {
                        uint mehediSz = mehediSizes[shihabSampleIdx];
                        uint shihabDur = shihabSampleIdx < shihabDurations.Count ? shihabDurations[shihabSampleIdx] : 1024u;
                        int mehediCto = shihabSampleIdx < mehediCtoList.Count ? mehediCtoList[shihabSampleIdx] : 0;
                        bool shihabSync = shihabSyncSet == null || shihabSyncSet.Contains((uint)(shihabSampleIdx + 1));

                        mehediTrk.ShihabSamples.Add(new MehediIsoSampleRecord
                        {
                            ShihabFileOffset = mehediByteCursor,
                            MehediSize = mehediSz,
                            ShihabDuration = shihabDur,
                            MehediCompositionOffset = mehediCto,
                            ShihabIsSync = shihabSync
                        });

                        mehediByteCursor += mehediSz;
                        shihabSampleIdx++;
                    }
                }
            }
        }

        private static byte[] TryExtractEsdsAudioSpecificConfig(byte[] shihabBuf, int mehediStart, int shihabEnd)
        {
            int mehediOff = mehediStart;
            while (mehediOff + 8 <= shihabEnd)
            {
                int shihabLen;
                string mehediType;
                int shihabHdr;
                if (!ReadIsoBoxHeaderFromBuf(shihabBuf, mehediOff, shihabEnd, out shihabLen, out mehediType, out shihabHdr)) break;
                if (mehediType == "esds")
                {
                    int mehediPay = mehediOff + shihabHdr + 4;
                    int shihabPayEnd = mehediOff + shihabLen;
                    for (int mehediP = mehediPay; mehediP + 4 < shihabPayEnd; mehediP++)
                    {
                        if (shihabBuf[mehediP] == 0x05) // DecoderSpecificInfo tag
                        {
                            int shihabCursor = mehediP + 1;
                            int mehediDescLen = 0;
                            for (int shihabB = 0; shihabB < 4 && shihabCursor < shihabPayEnd; shihabB++)
                            {
                                byte mehediByte = shihabBuf[shihabCursor++];
                                mehediDescLen = (mehediDescLen << 7) | (mehediByte & 0x7F);
                                if ((mehediByte & 0x80) == 0) break;
                            }
                            if (mehediDescLen >= 2 && mehediDescLen <= 32 && shihabCursor + mehediDescLen <= shihabPayEnd)
                            {
                                byte[] shihabAsc = new byte[mehediDescLen];
                                Buffer.BlockCopy(shihabBuf, shihabCursor, shihabAsc, 0, mehediDescLen);
                                return shihabAsc;
                            }
                        }
                    }
                }
                mehediOff += shihabLen;
            }
            return new byte[] { 0x11, 0x90 }; // Standard AAC-LC 48kHz stereo fallback
        }

        private static void ParseMvexFromBuf(
            byte[] shihabBuf,
            int mehediStart,
            int shihabEnd,
            Dictionary<uint, ShihabParsedIsoTrack> mehediById)
        {
            int shihabOff = mehediStart;
            while (shihabOff + 8 <= shihabEnd)
            {
                int mehediLen;
                string shihabType;
                int mehediHdr;
                if (!ReadIsoBoxHeaderFromBuf(shihabBuf, shihabOff, shihabEnd, out mehediLen, out shihabType, out mehediHdr)) break;
                int shihabPay = shihabOff + mehediHdr;
                if (shihabType == "trex" && shihabOff + mehediLen - shihabPay >= 24)
                {
                    uint mehediTrackId = ReadBeU32(shihabBuf, shihabPay + 4);
                    ShihabParsedIsoTrack shihabTrack;
                    if (mehediById.TryGetValue(mehediTrackId, out shihabTrack))
                    {
                        shihabTrack.MehediTrexDefaultDuration = ReadBeU32(shihabBuf, shihabPay + 12);
                        shihabTrack.ShihabTrexDefaultSize = ReadBeU32(shihabBuf, shihabPay + 16);
                        shihabTrack.MehediTrexDefaultFlags = ReadBeU32(shihabBuf, shihabPay + 20);
                    }
                }
                shihabOff += mehediLen;
            }
        }

        private static void ParseMoofFragment(
            byte[] shihabMoof,
            long mehediMoofStartOffset,
            long shihabFollowingMdatDataStart,
            List<ShihabParsedIsoTrack> mehediAllTracks,
            Dictionary<uint, ShihabParsedIsoTrack> shihabById)
        {
            int mehediOff = 0;
            long shihabImplicitCursor = shihabFollowingMdatDataStart;

            while (mehediOff + 8 <= shihabMoof.Length)
            {
                int shihabLen;
                string mehediType;
                int shihabHdr;
                if (!ReadIsoBoxHeaderFromBuf(shihabMoof, mehediOff, shihabMoof.Length, out shihabLen, out mehediType, out shihabHdr)) break;
                if (mehediType == "traf")
                {
                    ParseTrafFragment(
                        shihabMoof,
                        mehediOff + shihabHdr,
                        mehediOff + shihabLen,
                        mehediMoofStartOffset,
                        ref shihabImplicitCursor,
                        mehediAllTracks,
                        shihabById);
                }
                mehediOff += shihabLen;
            }
        }

        private static void ParseTrafFragment(
            byte[] shihabBuf,
            int mehediStart,
            int shihabEnd,
            long mehediMoofStartOffset,
            ref long shihabImplicitDataCursor,
            List<ShihabParsedIsoTrack> mehediAllTracks,
            Dictionary<uint, ShihabParsedIsoTrack> shihabById)
        {
            ShihabParsedIsoTrack mehediTrack = mehediAllTracks.Count == 1 ? mehediAllTracks[0] : null;
            long shihabBaseDataOffset = mehediMoofStartOffset;
            bool mehediHasExplicitBase = false;
            uint shihabDefDur = 0;
            uint mehediDefSize = 0;
            uint shihabDefFlags = 0;

            int mehediOff = mehediStart;
            while (mehediOff + 8 <= shihabEnd)
            {
                int shihabLen;
                string mehediType;
                int shihabHdr;
                if (!ReadIsoBoxHeaderFromBuf(shihabBuf, mehediOff, shihabEnd, out shihabLen, out mehediType, out shihabHdr)) break;
                int shihabPay = mehediOff + shihabHdr;
                int mehediPayEnd = mehediOff + shihabLen;

                if (mehediType == "tfhd" && mehediPayEnd - shihabPay >= 8)
                {
                    uint shihabFlags = ReadBeU32(shihabBuf, shihabPay) & 0x00FFFFFFu;
                    uint mehediTrackId = ReadBeU32(shihabBuf, shihabPay + 4);
                    ShihabParsedIsoTrack shihabFound;
                    if (shihabById.TryGetValue(mehediTrackId, out shihabFound))
                    {
                        mehediTrack = shihabFound;
                    }
                    if (mehediTrack != null)
                    {
                        shihabDefDur = mehediTrack.MehediTrexDefaultDuration;
                        mehediDefSize = mehediTrack.ShihabTrexDefaultSize;
                        shihabDefFlags = mehediTrack.MehediTrexDefaultFlags;
                    }

                    int mehediCur = shihabPay + 8;
                    if ((shihabFlags & 0x000001u) != 0 && mehediCur + 8 <= mehediPayEnd)
                    {
                        shihabBaseDataOffset = (long)ReadBeU64(shihabBuf, mehediCur);
                        mehediHasExplicitBase = true;
                        mehediCur += 8;
                    }
                    if ((shihabFlags & 0x000002u) != 0) mehediCur += 4;
                    if ((shihabFlags & 0x000008u) != 0 && mehediCur + 4 <= mehediPayEnd)
                    {
                        shihabDefDur = ReadBeU32(shihabBuf, mehediCur);
                        mehediCur += 4;
                    }
                    if ((shihabFlags & 0x000010u) != 0 && mehediCur + 4 <= mehediPayEnd)
                    {
                        mehediDefSize = ReadBeU32(shihabBuf, mehediCur);
                        mehediCur += 4;
                    }
                    if ((shihabFlags & 0x000020u) != 0 && mehediCur + 4 <= mehediPayEnd)
                    {
                        shihabDefFlags = ReadBeU32(shihabBuf, mehediCur);
                        mehediCur += 4;
                    }
                }
                else if (mehediType == "trun" && mehediTrack != null && mehediPayEnd - shihabPay >= 8)
                {
                    uint shihabTrunHeader = ReadBeU32(shihabBuf, shihabPay);
                    uint mehediFlags = shihabTrunHeader & 0x00FFFFFFu;
                    uint shihabSampleCount = ReadBeU32(shihabBuf, shihabPay + 4);
                    int mehediCur = shihabPay + 8;

                    long shihabSampleByteCursor = shihabImplicitDataCursor;
                    if ((mehediFlags & 0x000001u) != 0 && mehediCur + 4 <= mehediPayEnd)
                    {
                        int mehediDataOffset = unchecked((int)ReadBeU32(shihabBuf, mehediCur));
                        mehediCur += 4;
                        shihabSampleByteCursor = (mehediHasExplicitBase ? shihabBaseDataOffset : mehediMoofStartOffset) + mehediDataOffset;
                    }

                    uint shihabFirstSampleFlags = shihabDefFlags;
                    bool mehediHasFirstFlags = false;
                    if ((mehediFlags & 0x000004u) != 0 && mehediCur + 4 <= mehediPayEnd)
                    {
                        shihabFirstSampleFlags = ReadBeU32(shihabBuf, mehediCur);
                        mehediHasFirstFlags = true;
                        mehediCur += 4;
                    }

                    for (uint shihabI = 0; shihabI < shihabSampleCount; shihabI++)
                    {
                        uint mehediDur = shihabDefDur;
                        uint shihabSz = mehediDefSize;
                        uint mehediSmpFlags = (shihabI == 0 && mehediHasFirstFlags) ? shihabFirstSampleFlags : shihabDefFlags;
                        int shihabCto = 0;

                        if ((mehediFlags & 0x000100u) != 0 && mehediCur + 4 <= mehediPayEnd)
                        {
                            mehediDur = ReadBeU32(shihabBuf, mehediCur);
                            mehediCur += 4;
                        }
                        if ((mehediFlags & 0x000200u) != 0 && mehediCur + 4 <= mehediPayEnd)
                        {
                            shihabSz = ReadBeU32(shihabBuf, mehediCur);
                            mehediCur += 4;
                        }
                        if ((mehediFlags & 0x000400u) != 0 && mehediCur + 4 <= mehediPayEnd)
                        {
                            mehediSmpFlags = ReadBeU32(shihabBuf, mehediCur);
                            mehediCur += 4;
                        }
                        if ((mehediFlags & 0x000800u) != 0 && mehediCur + 4 <= mehediPayEnd)
                        {
                            shihabCto = unchecked((int)ReadBeU32(shihabBuf, mehediCur));
                            mehediCur += 4;
                        }

                        bool mehediIsSync = ((mehediSmpFlags & 0x00010000u) == 0) && (((mehediSmpFlags >> 24) & 0x3u) != 1u);
                        if (shihabSz > 0)
                        {
                            mehediTrack.ShihabSamples.Add(new MehediIsoSampleRecord
                            {
                                ShihabFileOffset = shihabSampleByteCursor,
                                MehediSize = shihabSz,
                                ShihabDuration = mehediDur,
                                MehediCompositionOffset = shihabCto,
                                ShihabIsSync = mehediIsSync
                            });
                            shihabSampleByteCursor += shihabSz;
                        }
                    }
                    shihabImplicitDataCursor = shihabSampleByteCursor;
                }

                mehediOff += shihabLen;
            }
        }

        // =====================================================================
        // 2. WEBM / MATROSKA (EBML) PARSER & TWO-TRACK INTERLEAVED MUXER
        // =====================================================================

        private static void MuxMehediWebmVideoAndWebmAudio(
            string shihabVidPath,
            string mehediAudPath,
            string shihabOutputPath,
            string mehediDocType)
        {
            using (FileStream shihabVidFs = new FileStream(shihabVidPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream mehediAudFs = new FileStream(mehediAudPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ShihabParsedWebmStream shihabVidWebm = ParseShihabWebmFile(shihabVidFs, false);
                ShihabParsedWebmStream mehediAudWebm = ParseShihabWebmFile(mehediAudFs, false);

                if (shihabVidWebm.ShihabVideoTrackEntryPayload == null || shihabVidWebm.ShihabVideoFrames.Count == 0)
                {
                    File.Copy(shihabVidPath, shihabOutputPath, true);
                    return;
                }
                if (mehediAudWebm.MehediAudioTrackEntryPayload == null || mehediAudWebm.MehediAudioFrames.Count == 0)
                {
                    File.Copy(shihabVidPath, shihabOutputPath, true);
                    return;
                }

                byte[] shihabTrack1Entry = RewriteEbmlTrackEntryNumber(shihabVidWebm.ShihabVideoTrackEntryPayload, 1);
                byte[] mehediTrack2Entry = RewriteEbmlTrackEntryNumber(mehediAudWebm.MehediAudioTrackEntryPayload, 2);

                WriteInterleavedEbmlFile(
                    shihabTrack1Entry,
                    mehediTrack2Entry,
                    shihabVidWebm.ShihabVideoFrames,
                    mehediAudWebm.MehediAudioFrames,
                    mehediDocType,
                    shihabOutputPath);
            }
        }

        private static void MuxMehediWebmVideoWithMp4AacToMkv(
            string shihabWebmVidPath,
            string mehediMp4AudPath,
            string shihabOutputMkvPath)
        {
            using (FileStream shihabVidFs = new FileStream(shihabWebmVidPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream mehediAudFs = new FileStream(mehediMp4AudPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ShihabParsedWebmStream shihabVidWebm = ParseShihabWebmFile(shihabVidFs, false);
                bool mehediEncA;
                List<ShihabParsedIsoTrack> shihabAudTracks = ParseMehediIsoBmffTracks(mehediAudFs, false, out mehediEncA);
                ShihabParsedIsoTrack mehediAacTrack = FindTrackByShihabHandler(shihabAudTracks, "soun");

                if (shihabVidWebm.ShihabVideoTrackEntryPayload == null || shihabVidWebm.ShihabVideoFrames.Count == 0 ||
                    mehediAacTrack == null || mehediAacTrack.ShihabSamples.Count == 0)
                {
                    File.Copy(shihabWebmVidPath, shihabOutputMkvPath, true);
                    return;
                }

                byte[] shihabTrack1 = RewriteEbmlTrackEntryNumber(shihabVidWebm.ShihabVideoTrackEntryPayload, 1);
                byte[] mehediAsc = mehediAacTrack.ShihabAacAudioSpecificConfig ?? new byte[] { 0x11, 0x90 };
                byte[] shihabTrack2 = BuildMatroskaAacTrackEntry(2, mehediAacTrack.ShihabTimescale, mehediAsc);

                List<MehediEbmlFramePacket> mehediAacPackets = new List<MehediEbmlFramePacket>(mehediAacTrack.ShihabSamples.Count);
                ulong shihabAccumTicks = 0;
                uint mehediScale = Math.Max(1u, mehediAacTrack.ShihabTimescale);

                for (int shihabI = 0; shihabI < mehediAacTrack.ShihabSamples.Count; shihabI++)
                {
                    MehediIsoSampleRecord mehediSmp = mehediAacTrack.ShihabSamples[shihabI];
                    mehediAudFs.Position = mehediSmp.ShihabFileOffset;
                    byte[] shihabRawAac = ReadExactBytesFromStream(mehediAudFs, (int)mehediSmp.MehediSize);
                    long mehediPtsMs = (long)((shihabAccumTicks * 1000UL) / mehediScale);

                    mehediAacPackets.Add(new MehediEbmlFramePacket
                    {
                        ShihabTimestampMs = mehediPtsMs,
                        MehediOutputTrackNumber = 2,
                        ShihabIsKeyframe = true,
                        MehediRawFramePayload = shihabRawAac
                    });
                    shihabAccumTicks += mehediSmp.ShihabDuration > 0 ? mehediSmp.ShihabDuration : 1024u;
                }

                WriteInterleavedEbmlFile(
                    shihabTrack1,
                    shihabTrack2,
                    shihabVidWebm.ShihabVideoFrames,
                    mehediAacPackets,
                    "matroska",
                    shihabOutputMkvPath);
            }
        }

        private static byte[] BuildMatroskaAacTrackEntry(int shihabTrackNum, uint mehediSampleRate, byte[] shihabAsc)
        {
            using (MemoryStream mehediAudioSub = new MemoryStream())
            using (MemoryStream shihabEntry = new MemoryStream())
            {
                // SamplingFrequency (0xB5) float32 BE
                byte[] mehediFreqBytes = BitConverter.GetBytes((float)(mehediSampleRate > 0 ? mehediSampleRate : 44100u));
                if (BitConverter.IsLittleEndian) Array.Reverse(mehediFreqBytes);
                WriteEbmlElement(mehediAudioSub, 0xB5, mehediFreqBytes);
                WriteEbmlElement(mehediAudioSub, 0x9F, new byte[] { 2 }); // Channels = 2

                WriteEbmlElement(shihabEntry, 0xD7, new byte[] { (byte)shihabTrackNum }); // TrackNumber
                WriteEbmlElement(shihabEntry, 0x73C5, new byte[] { 0, 0, 0, (byte)shihabTrackNum }); // TrackUID
                WriteEbmlElement(shihabEntry, 0x83, new byte[] { 2 }); // TrackType = 2 (Audio)
                WriteEbmlElement(shihabEntry, 0x86, Encoding.ASCII.GetBytes("A_AAC")); // CodecID
                WriteEbmlElement(shihabEntry, 0x63A2, shihabAsc); // CodecPrivate
                WriteEbmlElement(shihabEntry, 0xE1, mehediAudioSub.ToArray()); // Audio settings
                return shihabEntry.ToArray();
            }
        }

        private static void WriteInterleavedEbmlFile(
            byte[] shihabVideoTrackEntryPayload,
            byte[] mehediAudioTrackEntryPayload,
            List<MehediEbmlFramePacket> shihabVideoPackets,
            List<MehediEbmlFramePacket> mehediAudioPackets,
            string shihabDocType,
            string mehediOutputPath)
        {
            List<MehediEbmlFramePacket> shihabMerged = new List<MehediEbmlFramePacket>(shihabVideoPackets.Count + mehediAudioPackets.Count);
            for (int mehediI = 0; mehediI < shihabVideoPackets.Count; mehediI++)
            {
                shihabVideoPackets[mehediI].MehediOutputTrackNumber = 1;
                shihabMerged.Add(shihabVideoPackets[mehediI]);
            }
            for (int shihabI = 0; shihabI < mehediAudioPackets.Count; shihabI++)
            {
                mehediAudioPackets[shihabI].MehediOutputTrackNumber = 2;
                shihabMerged.Add(mehediAudioPackets[shihabI]);
            }

            shihabMerged.Sort(delegate(MehediEbmlFramePacket mehediA, MehediEbmlFramePacket shihabB)
            {
                int mehediCmp = mehediA.ShihabTimestampMs.CompareTo(shihabB.ShihabTimestampMs);
                if (mehediCmp != 0) return mehediCmp;
                return mehediA.MehediOutputTrackNumber.CompareTo(shihabB.MehediOutputTrackNumber);
            });

            Directory.CreateDirectory(Path.GetDirectoryName(mehediOutputPath));
            using (FileStream shihabOut = new FileStream(mehediOutputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // EBML Header (0x1A45DFA3)
                using (MemoryStream mehediHdr = new MemoryStream())
                {
                    WriteEbmlElement(mehediHdr, 0x4286, new byte[] { 1 }); // EBMLVersion
                    WriteEbmlElement(mehediHdr, 0x42F7, new byte[] { 1 }); // EBMLReadVersion
                    WriteEbmlElement(mehediHdr, 0x42F2, new byte[] { 4 }); // EBMLMaxIDLength
                    WriteEbmlElement(mehediHdr, 0x42F3, new byte[] { 8 }); // EBMLMaxSizeLength
                    WriteEbmlElement(mehediHdr, 0x4282, Encoding.ASCII.GetBytes(shihabDocType)); // DocType
                    WriteEbmlElement(mehediHdr, 0x4287, new byte[] { 4 }); // DocTypeVersion
                    WriteEbmlElement(mehediHdr, 0x4285, new byte[] { 2 }); // DocTypeReadVersion
                    WriteEbmlElement(shihabOut, 0x1A45DFA3, mehediHdr.ToArray());
                }

                // Segment Header (0x18538067) with unknown/streaming 8-byte VINT size (0x01FFFFFFFFFFFFFF)
                WriteEbmlId(shihabOut, 0x18538067);
                byte[] mehediUnknownSize = new byte[] { 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
                shihabOut.Write(mehediUnknownSize, 0, 8);

                // Info (0x1549A966) — TimecodeScale = 1,000,000 ns (1 ms)
                using (MemoryStream shihabInfo = new MemoryStream())
                {
                    WriteEbmlElement(shihabInfo, 0x2AD7B1, new byte[] { 0x0F, 0x42, 0x40 }); // 1000000
                    WriteEbmlElement(shihabInfo, 0x4D80, Encoding.UTF8.GetBytes("MHS Download Manager 1.0.5"));
                    WriteEbmlElement(shihabInfo, 0x5741, Encoding.UTF8.GetBytes("mdm.exe"));
                    WriteEbmlElement(shihabOut, 0x1549A966, shihabInfo.ToArray());
                }

                // Tracks (0x1654AE6B)
                using (MemoryStream mehediTracksMs = new MemoryStream())
                {
                    WriteEbmlElement(mehediTracksMs, 0xAE, shihabVideoTrackEntryPayload);
                    WriteEbmlElement(mehediTracksMs, 0xAE, mehediAudioTrackEntryPayload);
                    WriteEbmlElement(shihabOut, 0x1654AE6B, mehediTracksMs.ToArray());
                }

                // Clusters (0x1F43B675), grouped ~2000ms per cluster
                int shihabIdx = 0;
                while (shihabIdx < shihabMerged.Count)
                {
                    long mehediClusterBaseMs = Math.Max(0L, shihabMerged[shihabIdx].ShihabTimestampMs);
                    using (MemoryStream shihabClusterMs = new MemoryStream())
                    {
                        WriteEbmlElement(shihabClusterMs, 0xE7, EncodeUnsignedBigEndian((ulong)mehediClusterBaseMs));
                        while (shihabIdx < shihabMerged.Count)
                        {
                            MehediEbmlFramePacket mehediPkt = shihabMerged[shihabIdx];
                            long shihabRelMs = mehediPkt.ShihabTimestampMs - mehediClusterBaseMs;
                            if (shihabRelMs > 2500L && shihabClusterMs.Length > 16)
                            {
                                break;
                            }
                            short mehediClampedRel = (short)Math.Max(-32000L, Math.Min(32767L, shihabRelMs));
                            byte[] shihabSimpleBlock = new byte[4 + mehediPkt.MehediRawFramePayload.Length];
                            shihabSimpleBlock[0] = (byte)(0x80 | (mehediPkt.MehediOutputTrackNumber & 0x7F));
                            shihabSimpleBlock[1] = (byte)((mehediClampedRel >> 8) & 0xFF);
                            shihabSimpleBlock[2] = (byte)(mehediClampedRel & 0xFF);
                            shihabSimpleBlock[3] = (byte)(mehediPkt.ShihabIsKeyframe ? 0x80 : 0x00);
                            Buffer.BlockCopy(mehediPkt.MehediRawFramePayload, 0, shihabSimpleBlock, 4, mehediPkt.MehediRawFramePayload.Length);

                            WriteEbmlElement(shihabClusterMs, 0xA3, shihabSimpleBlock);
                            shihabIdx++;
                        }
                        WriteEbmlElement(shihabOut, 0x1F43B675, shihabClusterMs.ToArray());
                    }
                }
                shihabOut.Flush();
            }
        }

        private static ShihabParsedWebmStream ParseShihabWebmFile(Stream shihabStream, bool mehediMetadataOnly)
        {
            ShihabParsedWebmStream mehediParsed = new ShihabParsedWebmStream();
            long shihabLen = shihabStream.Length;

            while (shihabStream.Position < shihabLen)
            {
                ulong mehediId;
                long shihabElemSize;
                if (!ReadEbmlHeader(shihabStream, out mehediId, out shihabElemSize)) break;
                long mehediContentStart = shihabStream.Position;

                if (mehediId == 0x18538067UL) // Segment — step inside children!
                {
                    long shihabSegEnd = shihabElemSize < 0 ? shihabLen : Math.Min(shihabLen, mehediContentStart + shihabElemSize);
                    while (shihabStream.Position < shihabSegEnd)
                    {
                        ulong mehediChildId;
                        long shihabChildSize;
                        if (!ReadEbmlHeader(shihabStream, out mehediChildId, out shihabChildSize)) break;
                        long mehediChildStart = shihabStream.Position;
                        long shihabChildEnd = shihabChildSize < 0 ? shihabSegEnd : Math.Min(shihabSegEnd, mehediChildStart + shihabChildSize);

                        if (mehediChildId == 0x1549A966UL && shihabChildSize > 0 && shihabChildSize < 1048576) // Info
                        {
                            byte[] mehediInfoBuf = ReadExactBytesFromStream(shihabStream, (int)shihabChildSize);
                            ParseEbmlInfo(mehediInfoBuf, mehediParsed);
                        }
                        else if (mehediChildId == 0x1654AE6BUL && shihabChildSize > 0 && shihabChildSize < 4194304) // Tracks
                        {
                            byte[] shihabTracksBuf = ReadExactBytesFromStream(shihabStream, (int)shihabChildSize);
                            ParseEbmlTracks(shihabTracksBuf, mehediParsed);
                            if (mehediMetadataOnly) return mehediParsed;
                        }
                        else if (!mehediMetadataOnly && mehediChildId == 0x1F43B675UL) // Cluster
                        {
                            ParseEbmlCluster(shihabStream, shihabChildEnd, mehediParsed);
                        }

                        shihabStream.Position = shihabChildEnd;
                    }
                    break;
                }

                if (shihabElemSize < 0) break;
                shihabStream.Position = Math.Min(shihabLen, mehediContentStart + shihabElemSize);
            }

            return mehediParsed;
        }

        private static void ParseEbmlInfo(byte[] shihabBuf, ShihabParsedWebmStream mehediParsed)
        {
            int shihabOff = 0;
            while (shihabOff < shihabBuf.Length)
            {
                ulong mehediId;
                long shihabSize;
                if (!ReadEbmlHeaderFromBuf(shihabBuf, ref shihabOff, shihabBuf.Length, out mehediId, out shihabSize) || shihabSize < 0) break;
                if (mehediId == 0x2AD7B1UL && shihabOff + shihabSize <= shihabBuf.Length)
                {
                    mehediParsed.MehediTimecodeScaleNs = (long)ReadEbmlUint(shihabBuf, shihabOff, (int)shihabSize);
                }
                shihabOff += (int)shihabSize;
            }
        }

        private static void ParseEbmlTracks(byte[] shihabBuf, ShihabParsedWebmStream mehediParsed)
        {
            int shihabOff = 0;
            while (shihabOff < shihabBuf.Length)
            {
                ulong mehediId;
                long shihabSize;
                if (!ReadEbmlHeaderFromBuf(shihabBuf, ref shihabOff, shihabBuf.Length, out mehediId, out shihabSize) || shihabSize < 0) break;
                if (mehediId == 0xAEUL && shihabOff + shihabSize <= shihabBuf.Length)
                {
                    byte[] mehediEntry = new byte[shihabSize];
                    Buffer.BlockCopy(shihabBuf, shihabOff, mehediEntry, 0, (int)shihabSize);
                    InspectSingleEbmlTrackEntry(mehediEntry, mehediParsed);
                }
                shihabOff += (int)shihabSize;
            }
        }

        private static void InspectSingleEbmlTrackEntry(byte[] shihabEntry, ShihabParsedWebmStream mehediParsed)
        {
            int shihabTrackNum = 0;
            int mehediTrackType = 0;
            string shihabCodecId = "";
            byte[] mehediCodecPriv = null;
            int shihabW = 0, mehediH = 0, shihabCh = 2, mehediRate = 48000;

            int shihabOff = 0;
            while (shihabOff < shihabEntry.Length)
            {
                ulong mehediId;
                long shihabSize;
                if (!ReadEbmlHeaderFromBuf(shihabEntry, ref shihabOff, shihabEntry.Length, out mehediId, out shihabSize) || shihabSize < 0) break;
                int mehediEnd = shihabOff + (int)shihabSize;
                if (mehediEnd > shihabEntry.Length) break;

                if (mehediId == 0xD7UL) shihabTrackNum = (int)ReadEbmlUint(shihabEntry, shihabOff, (int)shihabSize);
                else if (mehediId == 0x83UL) mehediTrackType = (int)ReadEbmlUint(shihabEntry, shihabOff, (int)shihabSize);
                else if (mehediId == 0x86UL) shihabCodecId = Encoding.ASCII.GetString(shihabEntry, shihabOff, (int)shihabSize).TrimEnd('\0');
                else if (mehediId == 0x63A2UL)
                {
                    mehediCodecPriv = new byte[shihabSize];
                    Buffer.BlockCopy(shihabEntry, shihabOff, mehediCodecPriv, 0, (int)shihabSize);
                }
                else if (mehediId == 0xE0UL) // Video sub-elements
                {
                    int shihabVOff = shihabOff;
                    while (shihabVOff < mehediEnd)
                    {
                        ulong mehediVId;
                        long shihabVSize;
                        if (!ReadEbmlHeaderFromBuf(shihabEntry, ref shihabVOff, mehediEnd, out mehediVId, out shihabVSize) || shihabVSize < 0) break;
                        if (mehediVId == 0xB0UL) shihabW = (int)ReadEbmlUint(shihabEntry, shihabVOff, (int)shihabVSize);
                        else if (mehediVId == 0xBAUL) mehediH = (int)ReadEbmlUint(shihabEntry, shihabVOff, (int)shihabVSize);
                        shihabVOff += (int)shihabVSize;
                    }
                }
                else if (mehediId == 0xE1UL) // Audio sub-elements
                {
                    int mehediAOff = shihabOff;
                    while (mehediAOff < mehediEnd)
                    {
                        ulong shihabAId;
                        long mehediASize;
                        if (!ReadEbmlHeaderFromBuf(shihabEntry, ref mehediAOff, mehediEnd, out shihabAId, out mehediASize) || mehediASize < 0) break;
                        if (shihabAId == 0x9FUL) shihabCh = (int)ReadEbmlUint(shihabEntry, mehediAOff, (int)mehediASize);
                        else if (shihabAId == 0xB5UL && (mehediASize == 4 || mehediASize == 8))
                        {
                            byte[] shihabF = new byte[mehediASize];
                            Buffer.BlockCopy(shihabEntry, mehediAOff, shihabF, 0, (int)mehediASize);
                            if (BitConverter.IsLittleEndian) Array.Reverse(shihabF);
                            mehediRate = mehediASize == 4 ? (int)BitConverter.ToSingle(shihabF, 0) : (int)BitConverter.ToDouble(shihabF, 0);
                        }
                        mehediAOff += (int)mehediASize;
                    }
                }

                shihabOff = mehediEnd;
            }

            if (mehediTrackType == 1 && mehediParsed.ShihabVideoTrackEntryPayload == null)
            {
                mehediParsed.ShihabVideoTrackEntryPayload = shihabEntry;
                mehediParsed.MehediOrigVideoTrackNum = shihabTrackNum;
                mehediParsed.ShihabVideoCodecId = shihabCodecId;
                mehediParsed.MehediWidth = shihabW;
                mehediParsed.ShihabHeight = mehediH;
            }
            else if (mehediTrackType == 2 && mehediParsed.MehediAudioTrackEntryPayload == null)
            {
                mehediParsed.MehediAudioTrackEntryPayload = shihabEntry;
                mehediParsed.ShihabOrigAudioTrackNum = shihabTrackNum;
                mehediParsed.MehediAudioCodecId = shihabCodecId;
                mehediParsed.ShihabAudioCodecPrivate = mehediCodecPriv;
                mehediParsed.MehediAudioChannels = shihabCh;
                mehediParsed.ShihabAudioSampleRate = mehediRate;
            }
        }

        private static byte[] RewriteEbmlTrackEntryNumber(byte[] shihabOrigEntry, int mehediNewTrackNum)
        {
            using (MemoryStream shihabOut = new MemoryStream())
            {
                int mehediOff = 0;
                while (mehediOff < shihabOrigEntry.Length)
                {
                    ulong shihabId;
                    long mehediSize;
                    if (!ReadEbmlHeaderFromBuf(shihabOrigEntry, ref mehediOff, shihabOrigEntry.Length, out shihabId, out mehediSize) || mehediSize < 0) break;
                    if (shihabId == 0xD7UL)
                    {
                        WriteEbmlElement(shihabOut, 0xD7, new byte[] { (byte)mehediNewTrackNum });
                    }
                    else if (shihabId == 0x73C5UL)
                    {
                        WriteEbmlElement(shihabOut, 0x73C5, new byte[] { 0, 0, 0, (byte)mehediNewTrackNum });
                    }
                    else if (mehediOff + mehediSize <= shihabOrigEntry.Length)
                    {
                        byte[] shihabSlice = new byte[mehediSize];
                        Buffer.BlockCopy(shihabOrigEntry, mehediOff, shihabSlice, 0, (int)mehediSize);
                        WriteEbmlElement(shihabOut, shihabId, shihabSlice);
                    }
                    mehediOff += (int)mehediSize;
                }
                return shihabOut.ToArray();
            }
        }

        private static void ParseEbmlCluster(Stream shihabStream, long mehediClusterEnd, ShihabParsedWebmStream shihabParsed)
        {
            long mehediClusterTimecode = 0;
            long shihabScaleNs = shihabParsed.MehediTimecodeScaleNs > 0 ? shihabParsed.MehediTimecodeScaleNs : 1000000L;

            while (shihabStream.Position < mehediClusterEnd)
            {
                ulong mehediId;
                long shihabSize;
                if (!ReadEbmlHeader(shihabStream, out mehediId, out shihabSize)) break;
                long mehediStart = shihabStream.Position;
                if (shihabSize < 0 || mehediStart + shihabSize > mehediClusterEnd)
                {
                    break;
                }

                if (mehediId == 0xE7UL && shihabSize <= 8) // Cluster Timecode
                {
                    byte[] shihabTcBuf = ReadExactBytesFromStream(shihabStream, (int)shihabSize);
                    mehediClusterTimecode = (long)ReadEbmlUint(shihabTcBuf, 0, shihabTcBuf.Length);
                }
                else if ((mehediId == 0xA3UL || mehediId == 0xA1UL) && shihabSize > 4 && shihabSize < 16777216) // SimpleBlock / Block
                {
                    byte[] mehediBlk = ReadExactBytesFromStream(shihabStream, (int)shihabSize);
                    int shihabOff = 0;
                    long mehediTrackNum;
                    if (ReadEbmlVintValue(mehediBlk, ref shihabOff, out mehediTrackNum) && shihabOff + 3 <= mehediBlk.Length)
                    {
                        short shihabRelTc = (short)((mehediBlk[shihabOff] << 8) | mehediBlk[shihabOff + 1]);
                        byte mehediFlags = mehediBlk[shihabOff + 2];
                        shihabOff += 3;

                        long shihabAbsMs = ((mehediClusterTimecode + shihabRelTc) * shihabScaleNs) / 1000000L;
                        byte[] mehediFrameData = new byte[mehediBlk.Length - shihabOff];
                        Buffer.BlockCopy(mehediBlk, shihabOff, mehediFrameData, 0, mehediFrameData.Length);

                        MehediEbmlFramePacket shihabPkt = new MehediEbmlFramePacket
                        {
                            ShihabTimestampMs = shihabAbsMs,
                            ShihabIsKeyframe = mehediId == 0xA3UL ? ((mehediFlags & 0x80) != 0) : true,
                            MehediRawFramePayload = mehediFrameData
                        };

                        if ((int)mehediTrackNum == shihabParsed.MehediOrigVideoTrackNum)
                        {
                            shihabParsed.ShihabVideoFrames.Add(shihabPkt);
                        }
                        else if ((int)mehediTrackNum == shihabParsed.ShihabOrigAudioTrackNum)
                        {
                            shihabParsed.MehediAudioFrames.Add(shihabPkt);
                        }
                    }
                }
                else if (mehediId == 0xA0UL && shihabSize > 4 && shihabSize < 16777216) // BlockGroup
                {
                    long shihabGroupEnd = mehediStart + shihabSize;
                    ParseEbmlCluster(shihabStream, shihabGroupEnd, shihabParsed);
                }

                shihabStream.Position = mehediStart + shihabSize;
            }
        }

        // =====================================================================
        // 3. LOW-LEVEL BIG-ENDIAN & EBML BINARY HELPERS
        // =====================================================================

        private static bool ReadIsoBoxHeader(Stream shihabStream, long mehediMaxRem, out long shihabSize, out string mehediType, out int shihabHdrLen)
        {
            shihabSize = 0;
            mehediType = "";
            shihabHdrLen = 8;
            byte[] mehediHdr = new byte[8];
            if (shihabStream.Read(mehediHdr, 0, 8) < 8) return false;
            uint shihabSize32 = ReadBeU32(mehediHdr, 0);
            mehediType = Encoding.ASCII.GetString(mehediHdr, 4, 4);

            if (shihabSize32 == 1)
            {
                byte[] mehediExt = new byte[8];
                if (shihabStream.Read(mehediExt, 0, 8) < 8) return false;
                shihabSize = (long)ReadBeU64(mehediExt, 0);
                shihabHdrLen = 16;
            }
            else if (shihabSize32 == 0)
            {
                shihabSize = mehediMaxRem;
            }
            else
            {
                shihabSize = shihabSize32;
            }
            return shihabSize >= shihabHdrLen && shihabSize <= mehediMaxRem;
        }

        private static bool ReadIsoBoxHeaderFromBuf(byte[] shihabBuf, int mehediOff, int shihabLimit, out int mehediBoxLen, out string shihabType, out int mehediHdrLen)
        {
            mehediBoxLen = 0;
            shihabType = "";
            mehediHdrLen = 8;
            if (mehediOff + 8 > shihabLimit) return false;
            uint shihabSize32 = ReadBeU32(shihabBuf, mehediOff);
            shihabType = Encoding.ASCII.GetString(shihabBuf, mehediOff + 4, 4);
            if (shihabSize32 == 1)
            {
                if (mehediOff + 16 > shihabLimit) return false;
                mehediBoxLen = (int)ReadBeU64(shihabBuf, mehediOff + 8);
                mehediHdrLen = 16;
            }
            else if (shihabSize32 == 0)
            {
                mehediBoxLen = shihabLimit - mehediOff;
            }
            else
            {
                mehediBoxLen = (int)shihabSize32;
            }
            return mehediBoxLen >= mehediHdrLen && (mehediOff + mehediBoxLen) <= shihabLimit;
        }

        private static byte[] WrapIsoBox(string shihabFourCc, byte[] mehediPayload)
        {
            byte[] shihabOut = new byte[8 + mehediPayload.Length];
            uint mehediTotal = (uint)shihabOut.Length;
            shihabOut[0] = (byte)((mehediTotal >> 24) & 0xFF);
            shihabOut[1] = (byte)((mehediTotal >> 16) & 0xFF);
            shihabOut[2] = (byte)((mehediTotal >> 8) & 0xFF);
            shihabOut[3] = (byte)(mehediTotal & 0xFF);
            byte[] shihabTag = Encoding.ASCII.GetBytes(shihabFourCc);
            Buffer.BlockCopy(shihabTag, 0, shihabOut, 4, 4);
            Buffer.BlockCopy(mehediPayload, 0, shihabOut, 8, mehediPayload.Length);
            return shihabOut;
        }

        private static void WriteIdentityMatrix(Stream shihabMs)
        {
            uint[] mehediMat = new uint[] { 0x00010000u, 0, 0, 0, 0x00010000u, 0, 0, 0, 0x40000000u };
            for (int shihabI = 0; shihabI < 9; shihabI++) WriteBeU32(shihabMs, mehediMat[shihabI]);
        }

        private static void WriteAscii(Stream shihabMs, string mehediStr)
        {
            byte[] shihabBytes = Encoding.ASCII.GetBytes(mehediStr);
            shihabMs.Write(shihabBytes, 0, shihabBytes.Length);
        }

        private static void WriteBeU16(Stream shihabMs, ushort mehediVal)
        {
            shihabMs.WriteByte((byte)((mehediVal >> 8) & 0xFF));
            shihabMs.WriteByte((byte)(mehediVal & 0xFF));
        }

        private static void WriteBeU32(Stream shihabMs, uint mehediVal)
        {
            shihabMs.WriteByte((byte)((mehediVal >> 24) & 0xFF));
            shihabMs.WriteByte((byte)((mehediVal >> 16) & 0xFF));
            shihabMs.WriteByte((byte)((mehediVal >> 8) & 0xFF));
            shihabMs.WriteByte((byte)(mehediVal & 0xFF));
        }

        private static void WriteBeU64(Stream shihabMs, ulong mehediVal)
        {
            WriteBeU32(shihabMs, (uint)(mehediVal >> 32));
            WriteBeU32(shihabMs, (uint)(mehediVal & 0xFFFFFFFFUL));
        }

        private static ushort ReadBeU16(byte[] shihabBuf, int mehediOff)
        {
            return (ushort)((shihabBuf[mehediOff] << 8) | shihabBuf[mehediOff + 1]);
        }

        private static uint ReadBeU32(byte[] shihabBuf, int mehediOff)
        {
            return ((uint)shihabBuf[mehediOff] << 24) |
                   ((uint)shihabBuf[mehediOff + 1] << 16) |
                   ((uint)shihabBuf[mehediOff + 2] << 8) |
                   shihabBuf[mehediOff + 3];
        }

        private static ulong ReadBeU64(byte[] shihabBuf, int mehediOff)
        {
            return ((ulong)ReadBeU32(shihabBuf, mehediOff) << 32) | ReadBeU32(shihabBuf, mehediOff + 4);
        }

        private static byte[] ReadExactBytesFromStream(Stream shihabStream, int mehediCount)
        {
            byte[] shihabBuf = new byte[mehediCount];
            int mehediOff = 0;
            while (mehediOff < mehediCount)
            {
                int shihabGot = shihabStream.Read(shihabBuf, mehediOff, mehediCount - mehediOff);
                if (shihabGot <= 0) break;
                mehediOff += shihabGot;
            }
            return shihabBuf;
        }

        private static bool ReadEbmlHeader(Stream shihabStream, out ulong mehediId, out long shihabSize)
        {
            mehediId = 0;
            shihabSize = 0;
            int mehediFirst = shihabStream.ReadByte();
            if (mehediFirst < 0) return false;

            int shihabIdLen = CountLeadingZeroBits((byte)mehediFirst) + 1;
            if (shihabIdLen > 4) return false;
            mehediId = (ulong)(byte)mehediFirst;
            for (int shihabI = 1; shihabI < shihabIdLen; shihabI++)
            {
                int mehediB = shihabStream.ReadByte();
                if (mehediB < 0) return false;
                mehediId = (mehediId << 8) | (ulong)(byte)mehediB;
            }

            int shihabSzFirst = shihabStream.ReadByte();
            if (shihabSzFirst < 0) return false;
            int mehediSzLen = CountLeadingZeroBits((byte)shihabSzFirst) + 1;
            if (mehediSzLen > 8) return false;
            ulong shihabMask = (1UL << (8 - mehediSzLen)) - 1UL;
            ulong mehediVal = (ulong)(byte)shihabSzFirst & shihabMask;
            bool shihabAllOnes = mehediVal == shihabMask;

            for (int mehediI = 1; mehediI < mehediSzLen; mehediI++)
            {
                int shihabB = shihabStream.ReadByte();
                if (shihabB < 0) return false;
                if (shihabB != 0xFF) shihabAllOnes = false;
                mehediVal = (mehediVal << 8) | (ulong)(byte)shihabB;
            }
            shihabSize = shihabAllOnes ? -1L : (long)mehediVal;
            return true;
        }

        private static bool ReadEbmlHeaderFromBuf(byte[] shihabBuf, ref int mehediOff, int shihabLimit, out ulong mehediId, out long shihabSize)
        {
            mehediId = 0;
            shihabSize = 0;
            if (mehediOff >= shihabLimit) return false;

            byte mehediFirst = shihabBuf[mehediOff++];
            int shihabIdLen = CountLeadingZeroBits(mehediFirst) + 1;
            if (shihabIdLen > 4 || mehediOff + shihabIdLen - 1 > shihabLimit) return false;
            mehediId = mehediFirst;
            for (int shihabI = 1; shihabI < shihabIdLen; shihabI++)
            {
                mehediId = (mehediId << 8) | shihabBuf[mehediOff++];
            }

            if (mehediOff >= shihabLimit) return false;
            byte shihabSzFirst = shihabBuf[mehediOff++];
            int mehediSzLen = CountLeadingZeroBits(shihabSzFirst) + 1;
            if (mehediSzLen > 8 || mehediOff + mehediSzLen - 1 > shihabLimit) return false;
            ulong shihabMask = (1UL << (8 - mehediSzLen)) - 1UL;
            ulong mehediVal = shihabSzFirst & shihabMask;
            bool shihabAllOnes = mehediVal == shihabMask;

            for (int mehediI = 1; mehediI < mehediSzLen; mehediI++)
            {
                byte shihabB = shihabBuf[mehediOff++];
                if (shihabB != 0xFF) shihabAllOnes = false;
                mehediVal = (mehediVal << 8) | shihabB;
            }
            shihabSize = shihabAllOnes ? -1L : (long)mehediVal;
            return true;
        }

        private static bool ReadEbmlVintValue(byte[] shihabBuf, ref int mehediOff, out long shihabValue)
        {
            shihabValue = 0;
            if (mehediOff >= shihabBuf.Length) return false;
            byte mehediFirst = shihabBuf[mehediOff++];
            int shihabLen = CountLeadingZeroBits(mehediFirst) + 1;
            if (shihabLen > 8 || mehediOff + shihabLen - 1 > shihabBuf.Length) return false;
            ulong mehediVal = (ulong)mehediFirst & ((1UL << (8 - shihabLen)) - 1UL);
            for (int shihabI = 1; shihabI < shihabLen; shihabI++)
            {
                mehediVal = (mehediVal << 8) | shihabBuf[mehediOff++];
            }
            shihabValue = (long)mehediVal;
            return true;
        }

        private static int CountLeadingZeroBits(byte shihabVal)
        {
            if (shihabVal == 0) return 8;
            int mehediCount = 0;
            byte shihabMask = 0x80;
            while ((shihabVal & shihabMask) == 0)
            {
                mehediCount++;
                shihabMask >>= 1;
            }
            return mehediCount;
        }

        private static ulong ReadEbmlUint(byte[] shihabBuf, int mehediOff, int shihabLen)
        {
            ulong mehediVal = 0;
            for (int shihabI = 0; shihabI < shihabLen; shihabI++)
            {
                mehediVal = (mehediVal << 8) | shihabBuf[mehediOff + shihabI];
            }
            return mehediVal;
        }

        private static byte[] EncodeUnsignedBigEndian(ulong shihabVal)
        {
            if (shihabVal <= 0xFFUL) return new byte[] { (byte)shihabVal };
            if (shihabVal <= 0xFFFFUL) return new byte[] { (byte)(shihabVal >> 8), (byte)shihabVal };
            if (shihabVal <= 0xFFFFFFUL) return new byte[] { (byte)(shihabVal >> 16), (byte)(shihabVal >> 8), (byte)shihabVal };
            return new byte[] { (byte)(shihabVal >> 24), (byte)(shihabVal >> 16), (byte)(shihabVal >> 8), (byte)shihabVal };
        }

        private static void WriteEbmlId(Stream shihabStream, ulong mehediId)
        {
            if (mehediId <= 0xFFUL) shihabStream.WriteByte((byte)mehediId);
            else if (mehediId <= 0xFFFFUL) { shihabStream.WriteByte((byte)(mehediId >> 8)); shihabStream.WriteByte((byte)mehediId); }
            else if (mehediId <= 0xFFFFFFUL) { shihabStream.WriteByte((byte)(mehediId >> 16)); shihabStream.WriteByte((byte)(mehediId >> 8)); shihabStream.WriteByte((byte)mehediId); }
            else { shihabStream.WriteByte((byte)(mehediId >> 24)); shihabStream.WriteByte((byte)(mehediId >> 16)); shihabStream.WriteByte((byte)(mehediId >> 8)); shihabStream.WriteByte((byte)mehediId); }
        }

        private static void WriteEbmlSize(Stream shihabStream, ulong mehediSize)
        {
            if (mehediSize < 127UL)
            {
                shihabStream.WriteByte((byte)(0x80UL | mehediSize));
                return;
            }
            if (mehediSize < 16383UL)
            {
                shihabStream.WriteByte((byte)(0x40UL | (mehediSize >> 8)));
                shihabStream.WriteByte((byte)(mehediSize & 0xFFUL));
                return;
            }
            if (mehediSize < 2097151UL)
            {
                shihabStream.WriteByte((byte)(0x20UL | (mehediSize >> 16)));
                shihabStream.WriteByte((byte)((mehediSize >> 8) & 0xFFUL));
                shihabStream.WriteByte((byte)(mehediSize & 0xFFUL));
                return;
            }
            if (mehediSize < 268435455UL)
            {
                shihabStream.WriteByte((byte)(0x10UL | (mehediSize >> 24)));
                shihabStream.WriteByte((byte)((mehediSize >> 16) & 0xFFUL));
                shihabStream.WriteByte((byte)((mehediSize >> 8) & 0xFFUL));
                shihabStream.WriteByte((byte)(mehediSize & 0xFFUL));
                return;
            }
            shihabStream.WriteByte(0x01);
            for (int shihabShift = 48; shihabShift >= 0; shihabShift -= 8)
            {
                shihabStream.WriteByte((byte)((mehediSize >> shihabShift) & 0xFFUL));
            }
        }

        private static void WriteEbmlElement(Stream shihabStream, ulong mehediId, byte[] shihabPayload)
        {
            WriteEbmlId(shihabStream, mehediId);
            WriteEbmlSize(shihabStream, (ulong)(shihabPayload != null ? shihabPayload.Length : 0));
            if (shihabPayload != null && shihabPayload.Length > 0)
            {
                shihabStream.Write(shihabPayload, 0, shihabPayload.Length);
            }
        }
    }
}

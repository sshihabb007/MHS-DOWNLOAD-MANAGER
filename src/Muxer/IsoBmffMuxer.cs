using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShihabMehediIDM
{
    internal static class IsoBmffMuxer
    {
        public static void MuxShihabIsoBmffToFastStartMp4(
            string shihabVideoPath,
            string mehediAudioPath,
            string shihabOutputPath)
        {
            bool copiedFallback = false;
            string tempOut = null;

            using (FileStream mehediVidFs = new FileStream(shihabVideoPath, FileMode.Open, FileAccess.Read, FileShare.Read, MuxerBinaryIo.MuxerFileStreamBufferBytes))
            using (FileStream shihabAudFs = new FileStream(mehediAudioPath, FileMode.Open, FileAccess.Read, FileShare.Read, MuxerBinaryIo.MuxerFileStreamBufferBytes))
            {
                bool mehediEncV;
                bool shihabEncA;
                List<ShihabParsedIsoTrack> mehediVidTracks = ParseMehediIsoBmffTracks(mehediVidFs, false, out mehediEncV);
                List<ShihabParsedIsoTrack> shihabAudTracks = ParseMehediIsoBmffTracks(shihabAudFs, false, out shihabEncA);

                if (mehediEncV || shihabEncA)
                {
                    throw new InvalidOperationException("Encrypted DRM media stream cannot be muxed (DRM protections are respected).");
                }

                ShihabParsedIsoTrack mehediVideoTrack = FindTrackByShihabHandler(mehediVidTracks, "vide");
                ShihabParsedIsoTrack shihabAudioTrack = FindTrackByShihabHandler(shihabAudTracks, "soun");

                if (mehediVideoTrack == null || mehediVideoTrack.ShihabSamples.Count == 0)
                {
                    throw new InvalidDataException("No decodable video samples found in MP4 video stream.");
                }
                if (shihabAudioTrack == null || shihabAudioTrack.ShihabSamples.Count == 0)
                {
                    copiedFallback = true;
                }
                else
                {
                    tempOut = MuxerBinaryIo.BuildUniqueTempPath(shihabOutputPath);
                    try
                    {
                        WriteFastStartDualTrackMp4(mehediVidFs, mehediVideoTrack, shihabAudFs, shihabAudioTrack, null, tempOut);
                    }
                    catch
                    {
                        MuxerBinaryIo.TryDeleteTempQuietly(tempOut);
                        throw;
                    }
                }
            }

            if (copiedFallback)
            {
                MuxerBinaryIo.SafeCopyIfDistinctPaths(shihabVideoPath, shihabOutputPath);
                return;
            }

            try
            {
                MuxerBinaryIo.PromoteTempFileAtomically(tempOut, shihabOutputPath);
            }
            finally
            {
                MuxerBinaryIo.TryDeleteTempQuietly(tempOut);
            }
        }

        public static void MuxShihabMp4VideoWithWebmOpusToMp4(
            string shihabVideoPath,
            string mehediWebmAudioPath,
            string shihabOutputPath)
        {
            bool copiedFallback = false;
            string tempOut = null;

            using (FileStream mehediVidFs = new FileStream(shihabVideoPath, FileMode.Open, FileAccess.Read, FileShare.Read, MuxerBinaryIo.MuxerFileStreamBufferBytes))
            using (FileStream shihabWebmFs = new FileStream(mehediWebmAudioPath, FileMode.Open, FileAccess.Read, FileShare.Read, MuxerBinaryIo.MuxerFileStreamBufferBytes))
            {
                bool mehediEncV;
                List<ShihabParsedIsoTrack> shihabVidTracks = ParseMehediIsoBmffTracks(mehediVidFs, false, out mehediEncV);
                if (mehediEncV)
                {
                    throw new InvalidOperationException("Encrypted DRM media stream cannot be muxed (DRM protections are respected).");
                }

                ShihabParsedIsoTrack mehediVideoTrack = FindTrackByShihabHandler(shihabVidTracks, "vide");
                ShihabParsedWebmStream shihabWebm = EbmlWebmMkvMuxer.ParseShihabWebmFile(shihabWebmFs, false);

                if (mehediVideoTrack == null || mehediVideoTrack.ShihabSamples.Count == 0)
                {
                    throw new InvalidDataException("No video samples found in MP4 stream.");
                }
                if (shihabWebm.MehediAudioFrames.Count == 0 ||
                    shihabWebm.MehediAudioCodecId.IndexOf("OPUS", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    copiedFallback = true;
                }
                else
                {
                    // Stream Opus payloads into a temporary backing file rather than holding a large audio track in RAM
                    string opusBackingPath = MuxerBinaryIo.BuildUniqueTempPath(shihabOutputPath + ".opus");
                    try
                    {
                        using (FileStream mehediOpusStream = new FileStream(
                            opusBackingPath,
                            FileMode.CreateNew,
                            FileAccess.ReadWrite,
                            FileShare.None,
                            MuxerBinaryIo.MuxerFileStreamBufferBytes))
                        {
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

                                long shihabPos = mehediOpusStream.Position;
                                mehediOpusStream.Write(shihabPkt.MehediRawFramePayload, 0, shihabPkt.MehediRawFramePayload.Length);

                                shihabOpusTrack.ShihabSamples.Add(new MehediIsoSampleRecord
                                {
                                    ShihabFileOffset = shihabPos,
                                    MehediSize = (uint)shihabPkt.MehediRawFramePayload.Length,
                                    ShihabDuration = mehediDurTicks,
                                    MehediCompositionOffset = 0,
                                    ShihabIsSync = true
                                });
                            }

                            mehediOpusStream.Flush();
                            mehediOpusStream.Position = 0;

                            tempOut = MuxerBinaryIo.BuildUniqueTempPath(shihabOutputPath);
                            WriteFastStartDualTrackMp4(mehediVidFs, mehediVideoTrack, mehediOpusStream, shihabOpusTrack, null, tempOut);
                        }
                    }
                    catch
                    {
                        MuxerBinaryIo.TryDeleteTempQuietly(tempOut);
                        throw;
                    }
                    finally
                    {
                        MuxerBinaryIo.TryDeleteTempQuietly(opusBackingPath);
                    }
                }
            }

            if (copiedFallback)
            {
                MuxerBinaryIo.SafeCopyIfDistinctPaths(shihabVideoPath, shihabOutputPath);
                return;
            }

            try
            {
                MuxerBinaryIo.PromoteTempFileAtomically(tempOut, shihabOutputPath);
            }
            finally
            {
                MuxerBinaryIo.TryDeleteTempQuietly(tempOut);
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
                mehediGain = unchecked((short)(mehediOpusHead[16] | (mehediOpusHead[17] << 8)));
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
                MuxerBinaryIo.WriteBeU16(mehediDopsMs, mehediPreSkip);
                MuxerBinaryIo.WriteBeU32(mehediDopsMs, shihabInRate);
                MuxerBinaryIo.WriteBeU16(mehediDopsMs, unchecked((ushort)mehediGain));
                mehediDopsMs.WriteByte(shihabMapFamily);
                if (mehediExtraMap.Length > 0)
                {
                    mehediDopsMs.Write(mehediExtraMap, 0, mehediExtraMap.Length);
                }
                shihabDops = MuxerBinaryIo.WrapIsoBox("dOps", mehediDopsMs.ToArray());
            }

            byte[] mehediOpusEntry;
            using (MemoryStream shihabEntryMs = new MemoryStream())
            {
                shihabEntryMs.Write(new byte[6], 0, 6); // reserved
                MuxerBinaryIo.WriteBeU16(shihabEntryMs, 1); // data_reference_index = 1
                shihabEntryMs.Write(new byte[8], 0, 8); // reserved
                MuxerBinaryIo.WriteBeU16(shihabEntryMs, shihabOutCh);
                MuxerBinaryIo.WriteBeU16(shihabEntryMs, 16); // samplesize = 16
                MuxerBinaryIo.WriteBeU16(shihabEntryMs, 0);
                MuxerBinaryIo.WriteBeU16(shihabEntryMs, 0);
                MuxerBinaryIo.WriteBeU32(shihabEntryMs, 48000u << 16); // samplerate 48000.0 fixed-point
                shihabEntryMs.Write(shihabDops, 0, shihabDops.Length);
                mehediOpusEntry = MuxerBinaryIo.WrapIsoBox("Opus", shihabEntryMs.ToArray());
            }

            using (MemoryStream shihabStsdPayload = new MemoryStream())
            {
                MuxerBinaryIo.WriteBeU32(shihabStsdPayload, 0); // version + flags
                MuxerBinaryIo.WriteBeU32(shihabStsdPayload, 1); // entry_count = 1
                shihabStsdPayload.Write(mehediOpusEntry, 0, mehediOpusEntry.Length);
                return MuxerBinaryIo.WrapIsoBox("stsd", shihabStsdPayload.ToArray());
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

            byte[] shihabFinalMoov = BuildCompleteMoovBox(
                mehediVidTrack, shihabVidChunks, mehediFinalVidOffsets,
                mehediAudTrack, mehediAudChunks, shihabFinalAudOffsets);

            string outDir = Path.GetDirectoryName(mehediOutputPath);
            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            byte[] mehediCopyBuf = new byte[MuxerBinaryIo.MuxerFileStreamBufferBytes];

            using (FileStream shihabOutFs = new FileStream(mehediOutputPath, FileMode.Create, FileAccess.Write, FileShare.None, MuxerBinaryIo.MuxerFileStreamBufferBytes))
            {
                shihabOutFs.Write(shihabFtyp, 0, shihabFtyp.Length);
                shihabOutFs.Write(shihabFinalMoov, 0, shihabFinalMoov.Length);

                if (mehediUse64BitMdat)
                {
                    MuxerBinaryIo.WriteBeU32(shihabOutFs, 1u);
                    MuxerBinaryIo.WriteAscii(shihabOutFs, "mdat");
                    MuxerBinaryIo.WriteBeU64(shihabOutFs, (ulong)(shihabMdatPayloadLen + 16L));
                }
                else
                {
                    MuxerBinaryIo.WriteBeU32(shihabOutFs, (uint)(shihabMdatPayloadLen + 8L));
                    MuxerBinaryIo.WriteAscii(shihabOutFs, "mdat");
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
            long sourceLength = shihabSource.Length;
            for (int shihabI = 0; shihabI < mehediSamples.Count; shihabI++)
            {
                MehediIsoSampleRecord mehediRec = mehediSamples[shihabI];
                if (mehediRec.ShihabFileOffset < 0 || mehediRec.ShihabFileOffset > sourceLength || mehediRec.MehediSize > (ulong)(sourceLength - mehediRec.ShihabFileOffset))
                {
                    throw new EndOfStreamException("Sample table offset exceeds source media stream length.");
                }
                shihabSource.Position = mehediRec.ShihabFileOffset;
                uint shihabRem = mehediRec.MehediSize;
                while (shihabRem > 0)
                {
                    int mehediAsk = (int)Math.Min((uint)mehediBuf.Length, shihabRem);
                    int shihabGot = shihabSource.Read(mehediBuf, 0, mehediAsk);
                    if (shihabGot <= 0)
                    {
                        throw new EndOfStreamException("Unexpected EOF while muxing media sample.");
                    }
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
                MuxerBinaryIo.WriteAscii(shihabMs, "isom");
                MuxerBinaryIo.WriteBeU32(shihabMs, 0x00000200u);
                MuxerBinaryIo.WriteAscii(shihabMs, "isom");
                MuxerBinaryIo.WriteAscii(shihabMs, "iso2");
                MuxerBinaryIo.WriteAscii(shihabMs, "avc1");
                MuxerBinaryIo.WriteAscii(shihabMs, "mp41");
                return MuxerBinaryIo.WrapIsoBox("ftyp", shihabMs.ToArray());
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
                return MuxerBinaryIo.WrapIsoBox("moov", shihabMoovMs.ToArray());
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
                MuxerBinaryIo.WriteBeU32(mehediMs, shihabV1 ? 0x01000000u : 0u);
                if (shihabV1)
                {
                    MuxerBinaryIo.WriteBeU64(mehediMs, 0UL);
                    MuxerBinaryIo.WriteBeU64(mehediMs, 0UL);
                    MuxerBinaryIo.WriteBeU32(mehediMs, shihabTimescale);
                    MuxerBinaryIo.WriteBeU64(mehediMs, mehediDuration);
                }
                else
                {
                    MuxerBinaryIo.WriteBeU32(mehediMs, 0u);
                    MuxerBinaryIo.WriteBeU32(mehediMs, 0u);
                    MuxerBinaryIo.WriteBeU32(mehediMs, shihabTimescale);
                    MuxerBinaryIo.WriteBeU32(mehediMs, (uint)mehediDuration);
                }
                MuxerBinaryIo.WriteBeU32(mehediMs, 0x00010000u); // rate 1.0
                MuxerBinaryIo.WriteBeU16(mehediMs, 0x0100);      // volume 1.0
                mehediMs.Write(new byte[10], 0, 10);
                MuxerBinaryIo.WriteIdentityMatrix(mehediMs);
                mehediMs.Write(new byte[24], 0, 24);
                MuxerBinaryIo.WriteBeU32(mehediMs, shihabNextTrackId);
                return MuxerBinaryIo.WrapIsoBox("mvhd", mehediMs.ToArray());
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
                MuxerBinaryIo.WriteBeU32(shihabMs, (mehediV1 ? 0x01000000u : 0u) | 0x000003u); // track_enabled | track_in_movie
                if (mehediV1)
                {
                    MuxerBinaryIo.WriteBeU64(shihabMs, 0UL);
                    MuxerBinaryIo.WriteBeU64(shihabMs, 0UL);
                    MuxerBinaryIo.WriteBeU32(shihabMs, shihabTrackId);
                    MuxerBinaryIo.WriteBeU32(shihabMs, 0u);
                    MuxerBinaryIo.WriteBeU64(shihabMs, mehediMovieDurMs);
                }
                else
                {
                    MuxerBinaryIo.WriteBeU32(shihabMs, 0u);
                    MuxerBinaryIo.WriteBeU32(shihabMs, 0u);
                    MuxerBinaryIo.WriteBeU32(shihabMs, shihabTrackId);
                    MuxerBinaryIo.WriteBeU32(shihabMs, 0u);
                    MuxerBinaryIo.WriteBeU32(shihabMs, (uint)mehediMovieDurMs);
                }
                shihabMs.Write(new byte[8], 0, 8);
                MuxerBinaryIo.WriteBeU16(shihabMs, 0); // layer
                MuxerBinaryIo.WriteBeU16(shihabMs, 0); // alternate_group
                MuxerBinaryIo.WriteBeU16(shihabMs, (ushort)(mehediIsVideo ? 0 : 0x0100));
                MuxerBinaryIo.WriteBeU16(shihabMs, 0);
                MuxerBinaryIo.WriteIdentityMatrix(shihabMs);
                uint mehediFixedW = mehediIsVideo ? ((uint)Math.Max(0, shihabTrack.MehediWidth) << 16) : 0u;
                uint shihabFixedH = mehediIsVideo ? ((uint)Math.Max(0, shihabTrack.ShihabHeight) << 16) : 0u;
                MuxerBinaryIo.WriteBeU32(shihabMs, mehediFixedW);
                MuxerBinaryIo.WriteBeU32(shihabMs, shihabFixedH);
                mehediTkhd = MuxerBinaryIo.WrapIsoBox("tkhd", shihabMs.ToArray());
            }

            byte[] shihabMdhd;
            using (MemoryStream mehediMs = new MemoryStream())
            {
                bool shihabV1 = shihabTrackTicks > uint.MaxValue;
                MuxerBinaryIo.WriteBeU32(mehediMs, shihabV1 ? 0x01000000u : 0u);
                if (shihabV1)
                {
                    MuxerBinaryIo.WriteBeU64(mehediMs, 0UL);
                    MuxerBinaryIo.WriteBeU64(mehediMs, 0UL);
                    MuxerBinaryIo.WriteBeU32(mehediMs, Math.Max(1u, shihabTrack.ShihabTimescale));
                    MuxerBinaryIo.WriteBeU64(mehediMs, shihabTrackTicks);
                }
                else
                {
                    MuxerBinaryIo.WriteBeU32(mehediMs, 0u);
                    MuxerBinaryIo.WriteBeU32(mehediMs, 0u);
                    MuxerBinaryIo.WriteBeU32(mehediMs, Math.Max(1u, shihabTrack.ShihabTimescale));
                    MuxerBinaryIo.WriteBeU32(mehediMs, (uint)shihabTrackTicks);
                }
                MuxerBinaryIo.WriteBeU16(mehediMs, 0x55C4); // 'und'
                MuxerBinaryIo.WriteBeU16(mehediMs, 0);
                shihabMdhd = MuxerBinaryIo.WrapIsoBox("mdhd", mehediMs.ToArray());
            }

            byte[] mehediHdlr;
            using (MemoryStream shihabMs = new MemoryStream())
            {
                MuxerBinaryIo.WriteBeU32(shihabMs, 0u);
                MuxerBinaryIo.WriteBeU32(shihabMs, 0u);
                MuxerBinaryIo.WriteAscii(shihabMs, mehediIsVideo ? "vide" : "soun");
                shihabMs.Write(new byte[12], 0, 12);
                byte[] mehediName = Encoding.ASCII.GetBytes(mehediIsVideo ? "VideoHandler\0" : "SoundHandler\0");
                shihabMs.Write(mehediName, 0, mehediName.Length);
                mehediHdlr = MuxerBinaryIo.WrapIsoBox("hdlr", shihabMs.ToArray());
            }

            byte[] shihabMediaHeader;
            if (mehediIsVideo)
            {
                using (MemoryStream mehediMs = new MemoryStream())
                {
                    MuxerBinaryIo.WriteBeU32(mehediMs, 1u);
                    MuxerBinaryIo.WriteBeU16(mehediMs, 0);
                    mehediMs.Write(new byte[6], 0, 6);
                    shihabMediaHeader = MuxerBinaryIo.WrapIsoBox("vmhd", mehediMs.ToArray());
                }
            }
            else
            {
                using (MemoryStream mehediMs = new MemoryStream())
                {
                    MuxerBinaryIo.WriteBeU32(mehediMs, 0u);
                    MuxerBinaryIo.WriteBeU16(mehediMs, 0);
                    MuxerBinaryIo.WriteBeU16(mehediMs, 0);
                    shihabMediaHeader = MuxerBinaryIo.WrapIsoBox("smhd", mehediMs.ToArray());
                }
            }

            byte[] mehediDinf;
            using (MemoryStream shihabUrlMs = new MemoryStream())
            using (MemoryStream mehediDrefMs = new MemoryStream())
            {
                MuxerBinaryIo.WriteBeU32(shihabUrlMs, 1u);
                byte[] shihabUrlBox = MuxerBinaryIo.WrapIsoBox("url ", shihabUrlMs.ToArray());
                MuxerBinaryIo.WriteBeU32(mehediDrefMs, 0u);
                MuxerBinaryIo.WriteBeU32(mehediDrefMs, 1u);
                mehediDrefMs.Write(shihabUrlBox, 0, shihabUrlBox.Length);
                mehediDinf = MuxerBinaryIo.WrapIsoBox("dinf", MuxerBinaryIo.WrapIsoBox("dref", mehediDrefMs.ToArray()));
            }

            byte[] shihabStbl = BuildStblBox(mehediIsVideo, shihabTrack, mehediChunks, shihabChunkOffsets);

            byte[] mehediMinf;
            using (MemoryStream shihabMs = new MemoryStream())
            {
                shihabMs.Write(shihabMediaHeader, 0, shihabMediaHeader.Length);
                shihabMs.Write(mehediDinf, 0, mehediDinf.Length);
                shihabMs.Write(shihabStbl, 0, shihabStbl.Length);
                mehediMinf = MuxerBinaryIo.WrapIsoBox("minf", shihabMs.ToArray());
            }

            byte[] shihabMdia;
            using (MemoryStream mehediMs = new MemoryStream())
            {
                mehediMs.Write(shihabMdhd, 0, shihabMdhd.Length);
                mehediMs.Write(mehediHdlr, 0, mehediHdlr.Length);
                mehediMs.Write(mehediMinf, 0, mehediMinf.Length);
                shihabMdia = MuxerBinaryIo.WrapIsoBox("mdia", mehediMs.ToArray());
            }

            using (MemoryStream shihabTrakMs = new MemoryStream())
            {
                shihabTrakMs.Write(mehediTkhd, 0, mehediTkhd.Length);
                shihabTrakMs.Write(shihabMdia, 0, shihabMdia.Length);
                return MuxerBinaryIo.WrapIsoBox("trak", shihabTrakMs.ToArray());
            }
        }

        private static byte[] BuildStblBox(
            bool mehediIsVideo,
            ShihabParsedIsoTrack shihabTrack,
            List<List<MehediIsoSampleRecord>> mehediChunks,
            long[] shihabChunkOffsets)
        {
            List<MehediIsoSampleRecord> shihabSamples = shihabTrack.ShihabSamples;

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
                MuxerBinaryIo.WriteBeU32(shihabMs, 0u);
                MuxerBinaryIo.WriteBeU32(shihabMs, (uint)mehediSttsCounts.Count);
                for (int mehediI = 0; mehediI < mehediSttsCounts.Count; mehediI++)
                {
                    MuxerBinaryIo.WriteBeU32(shihabMs, mehediSttsCounts[mehediI]);
                    MuxerBinaryIo.WriteBeU32(shihabMs, shihabSttsDeltas[mehediI]);
                }
                mehediStts = MuxerBinaryIo.WrapIsoBox("stts", shihabMs.ToArray());
            }

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
                        MuxerBinaryIo.WriteBeU32(mehediMs, shihabHasNegativeCto ? 0x01000000u : 0u);
                        MuxerBinaryIo.WriteBeU32(mehediMs, (uint)mehediCttsCounts.Count);
                        for (int shihabI = 0; shihabI < mehediCttsCounts.Count; shihabI++)
                        {
                            MuxerBinaryIo.WriteBeU32(mehediMs, mehediCttsCounts[shihabI]);
                            MuxerBinaryIo.WriteBeU32(mehediMs, unchecked((uint)shihabCttsOffsets[shihabI]));
                        }
                        shihabCtts = MuxerBinaryIo.WrapIsoBox("ctts", mehediMs.ToArray());
                    }
                }
            }

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
                        MuxerBinaryIo.WriteBeU32(mehediMs, 0u);
                        MuxerBinaryIo.WriteBeU32(mehediMs, (uint)shihabSyncIndices.Count);
                        for (int shihabI = 0; shihabI < shihabSyncIndices.Count; shihabI++)
                        {
                            MuxerBinaryIo.WriteBeU32(mehediMs, shihabSyncIndices[shihabI]);
                        }
                        mehediStss = MuxerBinaryIo.WrapIsoBox("stss", mehediMs.ToArray());
                    }
                }
            }

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
                MuxerBinaryIo.WriteBeU32(mehediMs, 0u);
                MuxerBinaryIo.WriteBeU32(mehediMs, (uint)shihabFirstChunks.Count);
                for (int shihabI = 0; shihabI < shihabFirstChunks.Count; shihabI++)
                {
                    MuxerBinaryIo.WriteBeU32(mehediMs, shihabFirstChunks[shihabI]);
                    MuxerBinaryIo.WriteBeU32(mehediMs, mehediSamplesPerChunk[shihabI]);
                    MuxerBinaryIo.WriteBeU32(mehediMs, 1u);
                }
                shihabStsc = MuxerBinaryIo.WrapIsoBox("stsc", mehediMs.ToArray());
            }

            byte[] mehediStsz;
            using (MemoryStream shihabMs = new MemoryStream())
            {
                MuxerBinaryIo.WriteBeU32(shihabMs, 0u);
                MuxerBinaryIo.WriteBeU32(shihabMs, 0u);
                MuxerBinaryIo.WriteBeU32(shihabMs, (uint)shihabSamples.Count);
                for (int mehediI = 0; mehediI < shihabSamples.Count; mehediI++)
                {
                    MuxerBinaryIo.WriteBeU32(shihabMs, shihabSamples[mehediI].MehediSize);
                }
                mehediStsz = MuxerBinaryIo.WrapIsoBox("stsz", shihabMs.ToArray());
            }

            byte[] shihabCo64;
            using (MemoryStream mehediMs = new MemoryStream())
            {
                MuxerBinaryIo.WriteBeU32(mehediMs, 0u);
                MuxerBinaryIo.WriteBeU32(mehediMs, (uint)shihabChunkOffsets.Length);
                for (int shihabI = 0; shihabI < shihabChunkOffsets.Length; shihabI++)
                {
                    MuxerBinaryIo.WriteBeU64(mehediMs, (ulong)shihabChunkOffsets[shihabI]);
                }
                shihabCo64 = MuxerBinaryIo.WrapIsoBox("co64", mehediMs.ToArray());
            }

            byte[] rawStsd = shihabTrack.MehediRawStsdBox ?? new byte[0];
            using (MemoryStream mehediStblMs = new MemoryStream())
            {
                if (rawStsd.Length > 0)
                {
                    mehediStblMs.Write(rawStsd, 0, rawStsd.Length);
                }
                mehediStblMs.Write(mehediStts, 0, mehediStts.Length);
                if (shihabCtts != null) mehediStblMs.Write(shihabCtts, 0, shihabCtts.Length);
                if (mehediStss != null) mehediStblMs.Write(mehediStss, 0, mehediStss.Length);
                mehediStblMs.Write(shihabStsc, 0, shihabStsc.Length);
                mehediStblMs.Write(mehediStsz, 0, mehediStsz.Length);
                mehediStblMs.Write(shihabCo64, 0, shihabCo64.Length);
                return MuxerBinaryIo.WrapIsoBox("stbl", mehediStblMs.ToArray());
            }
        }

        public static ShihabParsedIsoTrack FindTrackByShihabHandler(List<ShihabParsedIsoTrack> shihabTracks, string mehediHandler)
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

        public static List<ShihabParsedIsoTrack> ParseMehediIsoBmffTracks(
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
            long pendingMoofTotalBytes = 0;

            while (shihabPos + 8 <= mehediLen)
            {
                shihabStream.Position = shihabPos;
                long mehediBoxSize;
                string shihabType;
                int mehediHdrLen;
                if (!MuxerBinaryIo.ReadIsoBoxHeader(shihabStream, mehediLen - shihabPos, out mehediBoxSize, out shihabType, out mehediHdrLen))
                {
                    break;
                }

                long payloadSize = mehediBoxSize - mehediHdrLen;
                if (shihabType == "moov" && payloadSize >= 0 && payloadSize <= MuxerBinaryIo.MaxMetadataBufferBytes)
                {
                    byte[] mehediMoovPayload = MuxerBinaryIo.ReadExactBytesFromStream(shihabStream, (int)payloadSize);
                    ParseMoovBuffer(mehediMoovPayload, mehediMetadataOnly, mehediTracks, shihabTrackById, ref shihabIsEncrypted);
                    if (mehediMetadataOnly)
                    {
                        return mehediTracks;
                    }
                }
                else if (!mehediMetadataOnly && shihabType == "moof" &&
                         payloadSize >= 0 && payloadSize <= MuxerBinaryIo.MaxMetadataBufferBytes &&
                         pendingMoofTotalBytes + payloadSize <= MuxerBinaryIo.MaxPendingMoofTotalBytes)
                {
                    byte[] shihabMoofPayload = MuxerBinaryIo.ReadExactBytesFromStream(shihabStream, (int)payloadSize);
                    shihabPendingMoofBytes.Add(shihabMoofPayload);
                    mehediPendingMoofStarts.Add(shihabPos);
                    pendingMoofTotalBytes += payloadSize;
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
                            mehediLen,
                            mehediTracks,
                            shihabTrackById);
                    }
                    shihabPendingMoofBytes.Clear();
                    mehediPendingMoofStarts.Clear();
                    pendingMoofTotalBytes = 0;
                }

                if (mehediBoxSize <= 0)
                {
                    break;
                }
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
                if (!MuxerBinaryIo.ReadIsoBoxHeaderFromBuf(shihabMoov, mehediOff, shihabMoov.Length, out shihabBoxLen, out mehediType, out shihabHdr))
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
                if (!MuxerBinaryIo.ReadIsoBoxHeaderFromBuf(shihabBuf, shihabOff, shihabEnd, out mehediLen, out shihabType, out mehediHdr)) break;
                int shihabPayStart = shihabOff + mehediHdr;
                int mehediPayEnd = shihabOff + mehediLen;

                if (shihabType == "tkhd" && mehediPayEnd - shihabPayStart >= 84)
                {
                    byte shihabVer = shihabBuf[shihabPayStart];
                    if (shihabVer == 1 && mehediPayEnd - shihabPayStart >= 96)
                    {
                        mehediTrk.MehediTrackId = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPayStart + 20);
                        mehediTrk.MehediWidth = (int)(MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPayStart + 88) >> 16);
                        mehediTrk.ShihabHeight = (int)(MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPayStart + 92) >> 16);
                    }
                    else
                    {
                        mehediTrk.MehediTrackId = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPayStart + 12);
                        mehediTrk.MehediWidth = (int)(MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPayStart + 76) >> 16);
                        mehediTrk.ShihabHeight = (int)(MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPayStart + 80) >> 16);
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
                if (!MuxerBinaryIo.ReadIsoBoxHeaderFromBuf(shihabBuf, shihabOff, shihabEnd, out mehediLen, out shihabType, out mehediHdr)) break;
                int shihabPayStart = shihabOff + mehediHdr;
                int mehediPayEnd = shihabOff + mehediLen;

                if (shihabType == "mdhd" && mehediPayEnd - shihabPayStart >= 20)
                {
                    byte mehediVer = shihabBuf[shihabPayStart];
                    if (mehediVer == 1 && mehediPayEnd - shihabPayStart >= 24)
                    {
                        mehediTrk.ShihabTimescale = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPayStart + 20);
                    }
                    else
                    {
                        mehediTrk.ShihabTimescale = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPayStart + 12);
                    }
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
                if (!MuxerBinaryIo.ReadIsoBoxHeaderFromBuf(shihabBuf, shihabOff, shihabEnd, out mehediLen, out shihabType, out mehediHdr)) break;
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
                if (!MuxerBinaryIo.ReadIsoBoxHeaderFromBuf(shihabBuf, shihabOff, shihabEnd, out mehediLen, out shihabType, out mehediHdr)) break;
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
                        if (MuxerBinaryIo.ReadIsoBoxHeaderFromBuf(shihabBuf, shihabEntryOff, mehediPayEnd, out mehediEntryLen, out shihabEntryFourCc, out mehediEntryHdr))
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
                                mehediTrk.MehediWidth = MuxerBinaryIo.ReadBeU16(shihabBuf, shihabEntryOff + mehediEntryHdr + 24);
                                mehediTrk.ShihabHeight = MuxerBinaryIo.ReadBeU16(shihabBuf, shihabEntryOff + mehediEntryHdr + 26);
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
                    uint mehediCount = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 4);
                    int shihabCur = shihabPay + 8;
                    for (uint mehediI = 0; mehediI < mehediCount && shihabCur + 8 <= mehediPayEnd && shihabDurations.Count < MuxerBinaryIo.MaxMuxerSamplesPerTrack; mehediI++)
                    {
                        uint shihabSampleCount = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabCur);
                        uint mehediDelta = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabCur + 4);
                        int remSlots = MuxerBinaryIo.MaxMuxerSamplesPerTrack - shihabDurations.Count;
                        int appendCount = (int)Math.Min((ulong)shihabSampleCount, (ulong)remSlots);
                        for (int shihabK = 0; shihabK < appendCount; shihabK++)
                        {
                            shihabDurations.Add(mehediDelta);
                        }
                        shihabCur += 8;
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "ctts" && mehediPayEnd - shihabPay >= 8)
                {
                    uint shihabCount = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 4);
                    int mehediCur = shihabPay + 8;
                    for (uint shihabI = 0; shihabI < shihabCount && mehediCur + 8 <= mehediPayEnd && mehediCtoList.Count < MuxerBinaryIo.MaxMuxerSamplesPerTrack; shihabI++)
                    {
                        uint mehediSampleCount = MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur);
                        int shihabOffsetVal = unchecked((int)MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur + 4));
                        int remSlots = MuxerBinaryIo.MaxMuxerSamplesPerTrack - mehediCtoList.Count;
                        int appendCount = (int)Math.Min((ulong)mehediSampleCount, (ulong)remSlots);
                        for (int mehediK = 0; mehediK < appendCount; mehediK++)
                        {
                            mehediCtoList.Add(shihabOffsetVal);
                        }
                        mehediCur += 8;
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "stss" && mehediPayEnd - shihabPay >= 8)
                {
                    shihabSyncSet = new HashSet<uint>();
                    uint mehediCount = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 4);
                    int shihabCur = shihabPay + 8;
                    for (uint mehediI = 0; mehediI < mehediCount && shihabCur + 4 <= mehediPayEnd && shihabSyncSet.Count < MuxerBinaryIo.MaxMuxerSamplesPerTrack; mehediI++)
                    {
                        shihabSyncSet.Add(MuxerBinaryIo.ReadBeU32(shihabBuf, shihabCur));
                        shihabCur += 4;
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "stsz" && mehediPayEnd - shihabPay >= 12)
                {
                    uint shihabUniformSize = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 4);
                    uint mehediCount = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 8);
                    int shihabCur = shihabPay + 12;
                    uint cappedCount = (uint)Math.Min((ulong)mehediCount, (ulong)MuxerBinaryIo.MaxMuxerSamplesPerTrack);
                    for (uint mehediI = 0; mehediI < cappedCount; mehediI++)
                    {
                        if (shihabUniformSize > 0)
                        {
                            mehediSizes.Add(shihabUniformSize);
                        }
                        else if (shihabCur + 4 <= mehediPayEnd)
                        {
                            mehediSizes.Add(MuxerBinaryIo.ReadBeU32(shihabBuf, shihabCur));
                            shihabCur += 4;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "stco" && mehediPayEnd - shihabPay >= 8)
                {
                    uint mehediCount = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 4);
                    int shihabCur = shihabPay + 8;
                    for (uint mehediI = 0; mehediI < mehediCount && shihabCur + 4 <= mehediPayEnd && shihabChunkOffsets.Count < MuxerBinaryIo.MaxMuxerSamplesPerTrack; mehediI++)
                    {
                        shihabChunkOffsets.Add(MuxerBinaryIo.ReadBeU32(shihabBuf, shihabCur));
                        shihabCur += 4;
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "co64" && mehediPayEnd - shihabPay >= 8)
                {
                    uint mehediCount = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 4);
                    int shihabCur = shihabPay + 8;
                    for (uint mehediI = 0; mehediI < mehediCount && shihabCur + 8 <= mehediPayEnd && shihabChunkOffsets.Count < MuxerBinaryIo.MaxMuxerSamplesPerTrack; mehediI++)
                    {
                        shihabChunkOffsets.Add(MuxerBinaryIo.ReadBeU64(shihabBuf, shihabCur));
                        shihabCur += 8;
                    }
                }
                else if (!shihabMetadataOnly && shihabType == "stsc" && mehediPayEnd - shihabPay >= 8)
                {
                    uint shihabCount = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 4);
                    int mehediCur = shihabPay + 8;
                    for (uint shihabI = 0; shihabI < shihabCount && mehediCur + 12 <= mehediPayEnd && mehediStscEntries.Count < MuxerBinaryIo.MaxMuxerSamplesPerTrack; shihabI++)
                    {
                        uint firstChunk = MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur);
                        uint samplesPerChunk = MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur + 4);
                        mehediStscEntries.Add(new uint[] { firstChunk, samplesPerChunk });
                        mehediCur += 12;
                    }
                }

                shihabOff += mehediLen;
            }

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
                    ulong chunkOffsetU64 = shihabChunkOffsets[shihabChunkIdx];
                    if (chunkOffsetU64 > (ulong)long.MaxValue)
                    {
                        break;
                    }
                    long mehediByteCursor = (long)chunkOffsetU64;

                    for (uint shihabS = 0; shihabS < shihabSamplesInChunk && shihabSampleIdx < mehediSizes.Count && mehediTrk.ShihabSamples.Count < MuxerBinaryIo.MaxMuxerSamplesPerTrack; shihabS++)
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
            if (mehediStart < 0 || shihabEnd > shihabBuf.Length || mehediStart + 8 > shihabEnd)
            {
                return new byte[] { 0x11, 0x90 };
            }

            int mehediOff = mehediStart;
            while (mehediOff + 8 <= shihabEnd)
            {
                int shihabLen;
                string mehediType;
                int shihabHdr;
                if (!MuxerBinaryIo.ReadIsoBoxHeaderFromBuf(shihabBuf, mehediOff, shihabEnd, out shihabLen, out mehediType, out shihabHdr)) break;
                if (mehediType == "esds")
                {
                    int mehediPay = mehediOff + shihabHdr + 4;
                    int shihabPayEnd = mehediOff + shihabLen;
                    for (int mehediP = mehediPay; mehediP + 4 < shihabPayEnd; mehediP++)
                    {
                        if (shihabBuf[mehediP] == 0x05)
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
            return new byte[] { 0x11, 0x90 };
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
                if (!MuxerBinaryIo.ReadIsoBoxHeaderFromBuf(shihabBuf, shihabOff, shihabEnd, out mehediLen, out shihabType, out mehediHdr)) break;
                int shihabPay = shihabOff + mehediHdr;
                if (shihabType == "trex" && shihabOff + mehediLen - shihabPay >= 24)
                {
                    uint mehediTrackId = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 4);
                    ShihabParsedIsoTrack shihabTrack;
                    if (mehediById.TryGetValue(mehediTrackId, out shihabTrack))
                    {
                        shihabTrack.MehediTrexDefaultDuration = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 12);
                        shihabTrack.ShihabTrexDefaultSize = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 16);
                        shihabTrack.MehediTrexDefaultFlags = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 20);
                    }
                }
                shihabOff += mehediLen;
            }
        }

        private static void ParseMoofFragment(
            byte[] shihabMoof,
            long mehediMoofStartOffset,
            long shihabFollowingMdatDataStart,
            long streamLength,
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
                if (!MuxerBinaryIo.ReadIsoBoxHeaderFromBuf(shihabMoof, mehediOff, shihabMoof.Length, out shihabLen, out mehediType, out shihabHdr)) break;
                if (mehediType == "traf")
                {
                    ParseTrafFragment(
                        shihabMoof,
                        mehediOff + shihabHdr,
                        mehediOff + shihabLen,
                        mehediMoofStartOffset,
                        streamLength,
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
            long streamLength,
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
                if (!MuxerBinaryIo.ReadIsoBoxHeaderFromBuf(shihabBuf, mehediOff, shihabEnd, out shihabLen, out mehediType, out shihabHdr)) break;
                int shihabPay = mehediOff + shihabHdr;
                int mehediPayEnd = mehediOff + shihabLen;

                if (mehediType == "tfhd" && mehediPayEnd - shihabPay >= 8)
                {
                    uint shihabFlags = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay) & 0x00FFFFFFu;
                    uint mehediTrackId = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 4);
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
                        ulong base64 = MuxerBinaryIo.ReadBeU64(shihabBuf, mehediCur);
                        if (base64 <= (ulong)long.MaxValue)
                        {
                            shihabBaseDataOffset = (long)base64;
                            mehediHasExplicitBase = true;
                        }
                        mehediCur += 8;
                    }
                    if ((shihabFlags & 0x000002u) != 0) mehediCur += 4;
                    if ((shihabFlags & 0x000008u) != 0 && mehediCur + 4 <= mehediPayEnd)
                    {
                        shihabDefDur = MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur);
                        mehediCur += 4;
                    }
                    if ((shihabFlags & 0x000010u) != 0 && mehediCur + 4 <= mehediPayEnd)
                    {
                        mehediDefSize = MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur);
                        mehediCur += 4;
                    }
                    if ((shihabFlags & 0x000020u) != 0 && mehediCur + 4 <= mehediPayEnd)
                    {
                        shihabDefFlags = MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur);
                        mehediCur += 4;
                    }
                }
                else if (mehediType == "trun" && mehediTrack != null && mehediPayEnd - shihabPay >= 8)
                {
                    uint shihabTrunHeader = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay);
                    uint mehediFlags = shihabTrunHeader & 0x00FFFFFFu;
                    uint shihabSampleCount = MuxerBinaryIo.ReadBeU32(shihabBuf, shihabPay + 4);
                    int mehediCur = shihabPay + 8;

                    long shihabSampleByteCursor = shihabImplicitDataCursor;
                    if ((mehediFlags & 0x000001u) != 0 && mehediCur + 4 <= mehediPayEnd)
                    {
                        int mehediDataOffset = unchecked((int)MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur));
                        mehediCur += 4;
                        shihabSampleByteCursor = (mehediHasExplicitBase ? shihabBaseDataOffset : mehediMoofStartOffset) + mehediDataOffset;
                    }

                    uint shihabFirstSampleFlags = shihabDefFlags;
                    bool mehediHasFirstFlags = false;
                    if ((mehediFlags & 0x000004u) != 0 && mehediCur + 4 <= mehediPayEnd)
                    {
                        shihabFirstSampleFlags = MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur);
                        mehediHasFirstFlags = true;
                        mehediCur += 4;
                    }

                    int remainingSlots = MuxerBinaryIo.MaxMuxerSamplesPerTrack - mehediTrack.ShihabSamples.Count;
                    uint cappedSamples = (uint)Math.Min((ulong)shihabSampleCount, (ulong)Math.Max(0, remainingSlots));

                    for (uint shihabI = 0; shihabI < cappedSamples; shihabI++)
                    {
                        uint mehediDur = shihabDefDur;
                        uint shihabSz = mehediDefSize;
                        uint mehediSmpFlags = (shihabI == 0 && mehediHasFirstFlags) ? shihabFirstSampleFlags : shihabDefFlags;
                        int shihabCto = 0;

                        if ((mehediFlags & 0x000100u) != 0)
                        {
                            if (mehediCur + 4 > mehediPayEnd) break;
                            mehediDur = MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur);
                            mehediCur += 4;
                        }
                        if ((mehediFlags & 0x000200u) != 0)
                        {
                            if (mehediCur + 4 > mehediPayEnd) break;
                            shihabSz = MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur);
                            mehediCur += 4;
                        }
                        if ((mehediFlags & 0x000400u) != 0)
                        {
                            if (mehediCur + 4 > mehediPayEnd) break;
                            mehediSmpFlags = MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur);
                            mehediCur += 4;
                        }
                        if ((mehediFlags & 0x000800u) != 0)
                        {
                            if (mehediCur + 4 > mehediPayEnd) break;
                            shihabCto = unchecked((int)MuxerBinaryIo.ReadBeU32(shihabBuf, mehediCur));
                            mehediCur += 4;
                        }

                        bool mehediIsSync = ((mehediSmpFlags & 0x00010000u) == 0) && (((mehediSmpFlags >> 24) & 0x3u) != 1u);
                        if (shihabSz > 0)
                        {
                            if (shihabSampleByteCursor < 0 || shihabSampleByteCursor > streamLength || shihabSz > (ulong)(streamLength - shihabSampleByteCursor))
                            {
                                break;
                            }
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
    }
}


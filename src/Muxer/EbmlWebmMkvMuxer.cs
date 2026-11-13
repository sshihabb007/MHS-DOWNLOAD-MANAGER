using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShihabMehediIDM
{
    internal static class EbmlWebmMkvMuxer
    {
        public static void MuxMehediWebmVideoAndWebmAudio(
            string shihabVidPath,
            string mehediAudPath,
            string shihabOutputPath,
            string mehediDocType)
        {
            bool copiedFallback = false;
            string tempOut = null;

            using (FileStream shihabVidFs = new FileStream(shihabVidPath, FileMode.Open, FileAccess.Read, FileShare.Read, MuxerBinaryIo.MuxerFileStreamBufferBytes))
            using (FileStream mehediAudFs = new FileStream(mehediAudPath, FileMode.Open, FileAccess.Read, FileShare.Read, MuxerBinaryIo.MuxerFileStreamBufferBytes))
            {
                ShihabParsedWebmStream shihabVidWebm = ParseShihabWebmFile(shihabVidFs, false);
                ShihabParsedWebmStream mehediAudWebm = ParseShihabWebmFile(mehediAudFs, false);

                if (shihabVidWebm.ShihabVideoTrackEntryPayload == null || shihabVidWebm.ShihabVideoFrames.Count == 0 ||
                    mehediAudWebm.MehediAudioTrackEntryPayload == null || mehediAudWebm.MehediAudioFrames.Count == 0)
                {
                    copiedFallback = true;
                }
                else
                {
                    byte[] shihabTrack1Entry = RewriteEbmlTrackEntryNumber(shihabVidWebm.ShihabVideoTrackEntryPayload, 1);
                    byte[] mehediTrack2Entry = RewriteEbmlTrackEntryNumber(mehediAudWebm.MehediAudioTrackEntryPayload, 2);

                    tempOut = MuxerBinaryIo.BuildUniqueTempPath(shihabOutputPath);
                    try
                    {
                        WriteInterleavedEbmlFile(
                            shihabTrack1Entry,
                            mehediTrack2Entry,
                            shihabVidWebm.ShihabVideoFrames,
                            mehediAudWebm.MehediAudioFrames,
                            mehediDocType,
                            tempOut);
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
                MuxerBinaryIo.SafeCopyIfDistinctPaths(shihabVidPath, shihabOutputPath);
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

        public static void MuxMehediWebmVideoWithMp4AacToMkv(
            string shihabWebmVidPath,
            string mehediMp4AudPath,
            string shihabOutputMkvPath)
        {
            bool copiedFallback = false;
            string tempOut = null;

            using (FileStream shihabVidFs = new FileStream(shihabWebmVidPath, FileMode.Open, FileAccess.Read, FileShare.Read, MuxerBinaryIo.MuxerFileStreamBufferBytes))
            using (FileStream mehediAudFs = new FileStream(mehediMp4AudPath, FileMode.Open, FileAccess.Read, FileShare.Read, MuxerBinaryIo.MuxerFileStreamBufferBytes))
            {
                ShihabParsedWebmStream shihabVidWebm = ParseShihabWebmFile(shihabVidFs, false);
                bool mehediEncA;
                List<ShihabParsedIsoTrack> shihabAudTracks = IsoBmffMuxer.ParseMehediIsoBmffTracks(mehediAudFs, false, out mehediEncA);
                if (mehediEncA)
                {
                    throw new InvalidOperationException("Encrypted DRM media stream cannot be muxed (DRM protections are respected).");
                }

                ShihabParsedIsoTrack mehediAacTrack = IsoBmffMuxer.FindTrackByShihabHandler(shihabAudTracks, "soun");

                if (shihabVidWebm.ShihabVideoTrackEntryPayload == null || shihabVidWebm.ShihabVideoFrames.Count == 0 ||
                    mehediAacTrack == null || mehediAacTrack.ShihabSamples.Count == 0)
                {
                    copiedFallback = true;
                }
                else
                {
                    byte[] shihabTrack1 = RewriteEbmlTrackEntryNumber(shihabVidWebm.ShihabVideoTrackEntryPayload, 1);
                    byte[] mehediAsc = mehediAacTrack.ShihabAacAudioSpecificConfig ?? new byte[] { 0x11, 0x90 };
                    byte[] shihabTrack2 = BuildMatroskaAacTrackEntry(2, mehediAacTrack.ShihabTimescale, mehediAsc);

                    List<MehediEbmlFramePacket> mehediAacPackets = new List<MehediEbmlFramePacket>(mehediAacTrack.ShihabSamples.Count);
                    ulong shihabAccumTicks = 0;
                    uint mehediScale = Math.Max(1u, mehediAacTrack.ShihabTimescale);
                    long audStreamLen = mehediAudFs.Length;

                    for (int shihabI = 0; shihabI < mehediAacTrack.ShihabSamples.Count; shihabI++)
                    {
                        MehediIsoSampleRecord mehediSmp = mehediAacTrack.ShihabSamples[shihabI];
                        if (mehediSmp.ShihabFileOffset < 0 ||
                            mehediSmp.ShihabFileOffset > audStreamLen ||
                            mehediSmp.MehediSize > (ulong)(audStreamLen - mehediSmp.ShihabFileOffset) ||
                            mehediSmp.MehediSize > (uint)MuxerBinaryIo.MaxEbmlBlockBytes)
                        {
                            throw new EndOfStreamException("Audio sample offset or size exceeds stream bounds.");
                        }

                        mehediAudFs.Position = mehediSmp.ShihabFileOffset;
                        byte[] shihabRawAac = MuxerBinaryIo.ReadExactBytesFromStream(
                            mehediAudFs,
                            (int)mehediSmp.MehediSize,
                            MuxerBinaryIo.MaxEbmlBlockBytes);
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

                    tempOut = MuxerBinaryIo.BuildUniqueTempPath(shihabOutputMkvPath);
                    try
                    {
                        WriteInterleavedEbmlFile(
                            shihabTrack1,
                            shihabTrack2,
                            shihabVidWebm.ShihabVideoFrames,
                            mehediAacPackets,
                            "matroska",
                            tempOut);
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
                MuxerBinaryIo.SafeCopyIfDistinctPaths(shihabWebmVidPath, shihabOutputMkvPath);
                return;
            }

            try
            {
                MuxerBinaryIo.PromoteTempFileAtomically(tempOut, shihabOutputMkvPath);
            }
            finally
            {
                MuxerBinaryIo.TryDeleteTempQuietly(tempOut);
            }
        }

        private static byte[] BuildMatroskaAacTrackEntry(int shihabTrackNum, uint mehediSampleRate, byte[] shihabAsc)
        {
            using (MemoryStream mehediAudioSub = new MemoryStream())
            using (MemoryStream shihabEntry = new MemoryStream())
            {
                byte[] mehediFreqBytes = BitConverter.GetBytes((float)(mehediSampleRate > 0 ? mehediSampleRate : 44100u));
                if (BitConverter.IsLittleEndian) Array.Reverse(mehediFreqBytes);
                MuxerBinaryIo.WriteEbmlElement(mehediAudioSub, 0xB5, mehediFreqBytes);
                MuxerBinaryIo.WriteEbmlElement(mehediAudioSub, 0x9F, new byte[] { 2 });

                MuxerBinaryIo.WriteEbmlElement(shihabEntry, 0xD7, new byte[] { (byte)shihabTrackNum });
                MuxerBinaryIo.WriteEbmlElement(shihabEntry, 0x73C5, new byte[] { 0, 0, 0, (byte)shihabTrackNum });
                MuxerBinaryIo.WriteEbmlElement(shihabEntry, 0x83, new byte[] { 2 });
                MuxerBinaryIo.WriteEbmlElement(shihabEntry, 0x86, Encoding.ASCII.GetBytes("A_AAC"));
                MuxerBinaryIo.WriteEbmlElement(shihabEntry, 0x63A2, shihabAsc);
                MuxerBinaryIo.WriteEbmlElement(shihabEntry, 0xE1, mehediAudioSub.ToArray());
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

            string outDir = Path.GetDirectoryName(mehediOutputPath);
            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            using (FileStream shihabOut = new FileStream(mehediOutputPath, FileMode.Create, FileAccess.Write, FileShare.None, MuxerBinaryIo.MuxerFileStreamBufferBytes))
            {
                using (MemoryStream mehediHdr = new MemoryStream())
                {
                    MuxerBinaryIo.WriteEbmlElement(mehediHdr, 0x4286, new byte[] { 1 });
                    MuxerBinaryIo.WriteEbmlElement(mehediHdr, 0x42F7, new byte[] { 1 });
                    MuxerBinaryIo.WriteEbmlElement(mehediHdr, 0x42F2, new byte[] { 4 });
                    MuxerBinaryIo.WriteEbmlElement(mehediHdr, 0x42F3, new byte[] { 8 });
                    MuxerBinaryIo.WriteEbmlElement(mehediHdr, 0x4282, Encoding.ASCII.GetBytes(shihabDocType));
                    MuxerBinaryIo.WriteEbmlElement(mehediHdr, 0x4287, new byte[] { 4 });
                    MuxerBinaryIo.WriteEbmlElement(mehediHdr, 0x4285, new byte[] { 2 });
                    MuxerBinaryIo.WriteEbmlElement(shihabOut, 0x1A45DFA3, mehediHdr.ToArray());
                }

                MuxerBinaryIo.WriteEbmlId(shihabOut, 0x18538067);
                byte[] mehediUnknownSize = new byte[] { 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
                shihabOut.Write(mehediUnknownSize, 0, 8);

                using (MemoryStream shihabInfo = new MemoryStream())
                {
                    MuxerBinaryIo.WriteEbmlElement(shihabInfo, 0x2AD7B1, new byte[] { 0x0F, 0x42, 0x40 });
                    MuxerBinaryIo.WriteEbmlElement(shihabInfo, 0x4D80, Encoding.UTF8.GetBytes("MHS Download Manager 1.0.7"));
                    MuxerBinaryIo.WriteEbmlElement(shihabInfo, 0x5741, Encoding.UTF8.GetBytes("mdm.exe"));
                    MuxerBinaryIo.WriteEbmlElement(shihabOut, 0x1549A966, shihabInfo.ToArray());
                }

                using (MemoryStream mehediTracksMs = new MemoryStream())
                {
                    MuxerBinaryIo.WriteEbmlElement(mehediTracksMs, 0xAE, shihabVideoTrackEntryPayload);
                    MuxerBinaryIo.WriteEbmlElement(mehediTracksMs, 0xAE, mehediAudioTrackEntryPayload);
                    MuxerBinaryIo.WriteEbmlElement(shihabOut, 0x1654AE6B, mehediTracksMs.ToArray());
                }

                // Reuse single cluster MemoryStream and 4-byte SimpleBlock prefix to eliminate LOH allocations per packet
                byte[] simpleBlockHdr = new byte[4];
                using (MemoryStream shihabClusterMs = new MemoryStream(65536))
                {
                    int shihabIdx = 0;
                    while (shihabIdx < shihabMerged.Count)
                    {
                        shihabClusterMs.SetLength(0);
                        shihabClusterMs.Position = 0;
                        long mehediClusterBaseMs = Math.Max(0L, shihabMerged[shihabIdx].ShihabTimestampMs);
                        MuxerBinaryIo.WriteEbmlElement(shihabClusterMs, 0xE7, MuxerBinaryIo.EncodeUnsignedBigEndian((ulong)mehediClusterBaseMs));

                        while (shihabIdx < shihabMerged.Count)
                        {
                            MehediEbmlFramePacket mehediPkt = shihabMerged[shihabIdx];
                            long shihabRelMs = mehediPkt.ShihabTimestampMs - mehediClusterBaseMs;
                            if (shihabRelMs > 2500L && shihabClusterMs.Length > 16)
                            {
                                break;
                            }
                            short mehediClampedRel = (short)Math.Max(-32000L, Math.Min(32767L, shihabRelMs));
                            byte[] payload = mehediPkt.MehediRawFramePayload ?? new byte[0];

                            MuxerBinaryIo.WriteEbmlId(shihabClusterMs, 0xA3);
                            MuxerBinaryIo.WriteEbmlSize(shihabClusterMs, (ulong)(4 + payload.Length));
                            simpleBlockHdr[0] = (byte)(0x80 | (mehediPkt.MehediOutputTrackNumber & 0x7F));
                            simpleBlockHdr[1] = (byte)((mehediClampedRel >> 8) & 0xFF);
                            simpleBlockHdr[2] = (byte)(mehediClampedRel & 0xFF);
                            simpleBlockHdr[3] = (byte)(mehediPkt.ShihabIsKeyframe ? 0x80 : 0x00);
                            shihabClusterMs.Write(simpleBlockHdr, 0, 4);
                            if (payload.Length > 0)
                            {
                                shihabClusterMs.Write(payload, 0, payload.Length);
                            }

                            shihabIdx++;
                        }

                        int clusterByteLen = (int)shihabClusterMs.Length;
                        MuxerBinaryIo.WriteEbmlId(shihabOut, 0x1F43B675);
                        MuxerBinaryIo.WriteEbmlSize(shihabOut, (ulong)clusterByteLen);
                        shihabOut.Write(shihabClusterMs.GetBuffer(), 0, clusterByteLen);
                    }
                }
                shihabOut.Flush();
            }
        }

        public static ShihabParsedWebmStream ParseShihabWebmFile(Stream shihabStream, bool mehediMetadataOnly)
        {
            ShihabParsedWebmStream mehediParsed = new ShihabParsedWebmStream();
            long shihabLen = shihabStream.Length;

            while (shihabStream.Position < shihabLen)
            {
                long beforeHdr = shihabStream.Position;
                ulong mehediId;
                long shihabElemSize;
                if (!MuxerBinaryIo.ReadEbmlHeader(shihabStream, out mehediId, out shihabElemSize)) break;
                long mehediContentStart = shihabStream.Position;
                if (mehediContentStart <= beforeHdr) break;

                if (mehediId == 0x18538067UL)
                {
                    long shihabSegEnd = shihabElemSize < 0
                        ? shihabLen
                        : (shihabElemSize > shihabLen - mehediContentStart ? shihabLen : mehediContentStart + shihabElemSize);

                    while (shihabStream.Position < shihabSegEnd)
                    {
                        long beforeChild = shihabStream.Position;
                        ulong mehediChildId;
                        long shihabChildSize;
                        if (!MuxerBinaryIo.ReadEbmlHeader(shihabStream, out mehediChildId, out shihabChildSize)) break;
                        long mehediChildStart = shihabStream.Position;
                        if (mehediChildStart <= beforeChild) break;

                        long shihabChildEnd = shihabChildSize < 0
                            ? shihabSegEnd
                            : (shihabChildSize > shihabSegEnd - mehediChildStart ? shihabSegEnd : mehediChildStart + shihabChildSize);

                        if (mehediChildId == 0x1549A966UL && shihabChildSize > 0 && shihabChildSize <= MuxerBinaryIo.MaxWebmInfoBytes)
                        {
                            byte[] mehediInfoBuf = MuxerBinaryIo.ReadExactBytesFromStream(
                                shihabStream,
                                (int)shihabChildSize,
                                MuxerBinaryIo.MaxWebmInfoBytes);
                            ParseEbmlInfo(mehediInfoBuf, mehediParsed);
                        }
                        else if (mehediChildId == 0x1654AE6BUL && shihabChildSize > 0 && shihabChildSize <= MuxerBinaryIo.MaxWebmTracksBytes)
                        {
                            byte[] shihabTracksBuf = MuxerBinaryIo.ReadExactBytesFromStream(
                                shihabStream,
                                (int)shihabChildSize,
                                MuxerBinaryIo.MaxWebmTracksBytes);
                            ParseEbmlTracks(shihabTracksBuf, mehediParsed);
                            if (mehediMetadataOnly) return mehediParsed;
                        }
                        else if (!mehediMetadataOnly && mehediChildId == 0x1F43B675UL)
                        {
                            ParseEbmlCluster(shihabStream, shihabChildEnd, mehediParsed, 0);
                        }

                        shihabStream.Position = shihabChildEnd;
                    }
                    break;
                }

                if (shihabElemSize < 0 || shihabElemSize > shihabLen - mehediContentStart) break;
                shihabStream.Position = mehediContentStart + shihabElemSize;
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
                if (!MuxerBinaryIo.ReadEbmlHeaderFromBuf(shihabBuf, ref shihabOff, shihabBuf.Length, out mehediId, out shihabSize) ||
                    shihabSize < 0 ||
                    shihabSize > shihabBuf.Length - shihabOff)
                {
                    break;
                }
                if (mehediId == 0x2AD7B1UL)
                {
                    ulong scale = MuxerBinaryIo.ReadEbmlUint(shihabBuf, shihabOff, (int)shihabSize);
                    if (scale > 0 && scale <= (ulong)long.MaxValue)
                    {
                        mehediParsed.MehediTimecodeScaleNs = (long)scale;
                    }
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
                if (!MuxerBinaryIo.ReadEbmlHeaderFromBuf(shihabBuf, ref shihabOff, shihabBuf.Length, out mehediId, out shihabSize) ||
                    shihabSize < 0 ||
                    shihabSize > shihabBuf.Length - shihabOff)
                {
                    break;
                }
                if (mehediId == 0xAEUL)
                {
                    byte[] mehediEntry = new byte[(int)shihabSize];
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
                if (!MuxerBinaryIo.ReadEbmlHeaderFromBuf(shihabEntry, ref shihabOff, shihabEntry.Length, out mehediId, out shihabSize) ||
                    shihabSize < 0 ||
                    shihabSize > shihabEntry.Length - shihabOff)
                {
                    break;
                }
                int intSize = (int)shihabSize;
                int mehediEnd = shihabOff + intSize;

                if (mehediId == 0xD7UL) shihabTrackNum = (int)MuxerBinaryIo.ReadEbmlUint(shihabEntry, shihabOff, intSize);
                else if (mehediId == 0x83UL) mehediTrackType = (int)MuxerBinaryIo.ReadEbmlUint(shihabEntry, shihabOff, intSize);
                else if (mehediId == 0x86UL) shihabCodecId = Encoding.ASCII.GetString(shihabEntry, shihabOff, intSize).TrimEnd('\0');
                else if (mehediId == 0x63A2UL)
                {
                    mehediCodecPriv = new byte[intSize];
                    Buffer.BlockCopy(shihabEntry, shihabOff, mehediCodecPriv, 0, intSize);
                }
                else if (mehediId == 0xE0UL)
                {
                    int shihabVOff = shihabOff;
                    while (shihabVOff < mehediEnd)
                    {
                        ulong mehediVId;
                        long shihabVSize;
                        if (!MuxerBinaryIo.ReadEbmlHeaderFromBuf(shihabEntry, ref shihabVOff, mehediEnd, out mehediVId, out shihabVSize) ||
                            shihabVSize < 0 ||
                            shihabVSize > mehediEnd - shihabVOff)
                        {
                            break;
                        }
                        if (mehediVId == 0xB0UL) shihabW = (int)MuxerBinaryIo.ReadEbmlUint(shihabEntry, shihabVOff, (int)shihabVSize);
                        else if (mehediVId == 0xBAUL) mehediH = (int)MuxerBinaryIo.ReadEbmlUint(shihabEntry, shihabVOff, (int)shihabVSize);
                        shihabVOff += (int)shihabVSize;
                    }
                }
                else if (mehediId == 0xE1UL)
                {
                    int mehediAOff = shihabOff;
                    while (mehediAOff < mehediEnd)
                    {
                        ulong shihabAId;
                        long mehediASize;
                        if (!MuxerBinaryIo.ReadEbmlHeaderFromBuf(shihabEntry, ref mehediAOff, mehediEnd, out shihabAId, out mehediASize) ||
                            mehediASize < 0 ||
                            mehediASize > mehediEnd - mehediAOff)
                        {
                            break;
                        }
                        if (shihabAId == 0x9FUL) shihabCh = (int)MuxerBinaryIo.ReadEbmlUint(shihabEntry, mehediAOff, (int)mehediASize);
                        else if (shihabAId == 0xB5UL && (mehediASize == 4 || mehediASize == 8))
                        {
                            byte[] shihabF = new byte[(int)mehediASize];
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
                    if (!MuxerBinaryIo.ReadEbmlHeaderFromBuf(shihabOrigEntry, ref mehediOff, shihabOrigEntry.Length, out shihabId, out mehediSize) ||
                        mehediSize < 0 ||
                        mehediSize > shihabOrigEntry.Length - mehediOff)
                    {
                        break;
                    }
                    int intSize = (int)mehediSize;
                    if (shihabId == 0xD7UL)
                    {
                        MuxerBinaryIo.WriteEbmlElement(shihabOut, 0xD7, new byte[] { (byte)mehediNewTrackNum });
                    }
                    else if (shihabId == 0x73C5UL)
                    {
                        MuxerBinaryIo.WriteEbmlElement(shihabOut, 0x73C5, new byte[] { 0, 0, 0, (byte)mehediNewTrackNum });
                    }
                    else
                    {
                        byte[] shihabSlice = new byte[intSize];
                        Buffer.BlockCopy(shihabOrigEntry, mehediOff, shihabSlice, 0, intSize);
                        MuxerBinaryIo.WriteEbmlElement(shihabOut, shihabId, shihabSlice);
                    }
                    mehediOff += intSize;
                }
                return shihabOut.ToArray();
            }
        }

        private static void ParseEbmlCluster(Stream shihabStream, long mehediClusterEnd, ShihabParsedWebmStream shihabParsed, int depth)
        {
            if (depth >= MuxerBinaryIo.MaxEbmlRecursionDepth)
            {
                return;
            }

            long mehediClusterTimecode = 0;
            long shihabScaleNs = shihabParsed.MehediTimecodeScaleNs > 0 ? shihabParsed.MehediTimecodeScaleNs : 1000000L;

            while (shihabStream.Position < mehediClusterEnd)
            {
                long beforeHdr = shihabStream.Position;
                ulong mehediId;
                long shihabSize;
                if (!MuxerBinaryIo.ReadEbmlHeader(shihabStream, out mehediId, out shihabSize)) break;
                long mehediStart = shihabStream.Position;
                if (mehediStart <= beforeHdr || shihabSize < 0 || shihabSize > mehediClusterEnd - mehediStart)
                {
                    break;
                }

                if (mehediId == 0xE7UL && shihabSize <= 8)
                {
                    byte[] shihabTcBuf = MuxerBinaryIo.ReadExactBytesFromStream(shihabStream, (int)shihabSize, 8);
                    ulong tcVal = MuxerBinaryIo.ReadEbmlUint(shihabTcBuf, 0, shihabTcBuf.Length);
                    if (tcVal <= (ulong)long.MaxValue)
                    {
                        mehediClusterTimecode = (long)tcVal;
                    }
                }
                else if ((mehediId == 0xA3UL || mehediId == 0xA1UL) && shihabSize > 4 && shihabSize <= MuxerBinaryIo.MaxEbmlBlockBytes)
                {
                    int totalPacketCount = shihabParsed.ShihabVideoFrames.Count + shihabParsed.MehediAudioFrames.Count;
                    if (totalPacketCount >= MuxerBinaryIo.MaxMuxerSamplesPerTrack * 2)
                    {
                        break;
                    }

                    byte[] mehediBlk = MuxerBinaryIo.ReadExactBytesFromStream(
                        shihabStream,
                        (int)shihabSize,
                        MuxerBinaryIo.MaxEbmlBlockBytes);
                    int shihabOff = 0;
                    long mehediTrackNum;
                    if (MuxerBinaryIo.ReadEbmlVintValue(mehediBlk, ref shihabOff, out mehediTrackNum) && shihabOff + 3 <= mehediBlk.Length)
                    {
                        short shihabRelTc = unchecked((short)((mehediBlk[shihabOff] << 8) | mehediBlk[shihabOff + 1]));
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

                        if (mehediTrackNum == shihabParsed.MehediOrigVideoTrackNum)
                        {
                            shihabParsed.ShihabVideoFrames.Add(shihabPkt);
                        }
                        else if (mehediTrackNum == shihabParsed.ShihabOrigAudioTrackNum)
                        {
                            shihabParsed.MehediAudioFrames.Add(shihabPkt);
                        }
                    }
                }
                else if (mehediId == 0xA0UL && shihabSize > 4 && shihabSize <= MuxerBinaryIo.MaxEbmlBlockBytes)
                {
                    long shihabGroupEnd = mehediStart + shihabSize;
                    ParseEbmlCluster(shihabStream, shihabGroupEnd, shihabParsed, depth + 1);
                }

                shihabStream.Position = mehediStart + shihabSize;
            }
        }
    }
}

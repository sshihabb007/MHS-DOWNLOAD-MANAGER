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

    internal static class MuxerBinaryIo
    {
        public const int MuxerFileStreamBufferBytes = 262144;
        public const int MaxMetadataBufferBytes = 128 * 1024 * 1024;
        public const int MaxPendingMoofTotalBytes = 96 * 1024 * 1024;
        public const int MaxWebmInfoBytes = 1048576;
        public const int MaxWebmTracksBytes = 8388608;
        public const int MaxEbmlBlockBytes = 33554432;
        public const int MaxMuxerSamplesPerTrack = 5000000;
        public const int MaxEbmlRecursionDepth = 32;

        public static bool AreSameFilePath(string leftPath, string rightPath)
        {
            if (string.IsNullOrEmpty(leftPath) || string.IsNullOrEmpty(rightPath))
            {
                return false;
            }
            try
            {
                string fullLeft = Path.GetFullPath(leftPath);
                string fullRight = Path.GetFullPath(rightPath);
                return string.Equals(fullLeft, fullRight, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string BuildUniqueTempPath(string targetOutputPath)
        {
            string fullOut = Path.GetFullPath(targetOutputPath);
            string dir = Path.GetDirectoryName(fullOut);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return fullOut + "." + Guid.NewGuid().ToString("N") + ".mux.tmp";
        }

        public static void PromoteTempFileAtomically(string tempFilePath, string targetOutputPath)
        {
            string fullTarget = Path.GetFullPath(targetOutputPath);
            string targetDir = Path.GetDirectoryName(fullTarget);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            if (File.Exists(fullTarget))
            {
                File.Replace(tempFilePath, fullTarget, null, true);
            }
            else
            {
                File.Move(tempFilePath, fullTarget);
            }
        }

        public static void TryDeleteTempQuietly(string tempFilePath)
        {
            if (string.IsNullOrEmpty(tempFilePath))
            {
                return;
            }
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch
            {
            }
        }

        public static void SafeCopyIfDistinctPaths(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
            {
                throw new ArgumentException("Source and destination paths are required.");
            }
            if (AreSameFilePath(sourcePath, destinationPath))
            {
                return;
            }

            string tempPath = BuildUniqueTempPath(destinationPath);
            try
            {
                using (FileStream srcFs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, MuxerFileStreamBufferBytes))
                using (FileStream dstFs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, MuxerFileStreamBufferBytes))
                {
                    byte[] copyBuf = new byte[MuxerFileStreamBufferBytes];
                    int readCount;
                    while ((readCount = srcFs.Read(copyBuf, 0, copyBuf.Length)) > 0)
                    {
                        dstFs.Write(copyBuf, 0, readCount);
                    }
                    dstFs.Flush();
                }
                PromoteTempFileAtomically(tempPath, destinationPath);
            }
            finally
            {
                TryDeleteTempQuietly(tempPath);
            }
        }

        public static void ExecuteAtomicOutputWrite(string targetOutputPath, Action<string> writeToTempAction)
        {
            if (string.IsNullOrEmpty(targetOutputPath))
            {
                throw new ArgumentException("Output path is required.");
            }
            string tempPath = BuildUniqueTempPath(targetOutputPath);
            try
            {
                writeToTempAction(tempPath);
                PromoteTempFileAtomically(tempPath, targetOutputPath);
            }
            finally
            {
                TryDeleteTempQuietly(tempPath);
            }
        }

        public static bool ReadIsoBoxHeader(Stream shihabStream, long mehediMaxRem, out long shihabSize, out string mehediType, out int shihabHdrLen)
        {
            shihabSize = 0;
            mehediType = "";
            shihabHdrLen = 8;
            if (mehediMaxRem < 8)
            {
                return false;
            }

            byte[] mehediHdr = new byte[8];
            if (ReadExactCountFromStream(shihabStream, mehediHdr, 0, 8) < 8)
            {
                return false;
            }

            uint shihabSize32 = ReadBeU32(mehediHdr, 0);
            mehediType = Encoding.ASCII.GetString(mehediHdr, 4, 4);

            if (shihabSize32 == 1)
            {
                if (mehediMaxRem < 16)
                {
                    return false;
                }
                byte[] mehediExt = new byte[8];
                if (ReadExactCountFromStream(shihabStream, mehediExt, 0, 8) < 8)
                {
                    return false;
                }
                ulong size64 = ReadBeU64(mehediExt, 0);
                if (size64 < 16UL || size64 > (ulong)mehediMaxRem)
                {
                    return false;
                }
                shihabSize = (long)size64;
                shihabHdrLen = 16;
                return true;
            }

            if (shihabSize32 == 0)
            {
                shihabSize = mehediMaxRem;
                return shihabSize >= 8;
            }

            if ((ulong)shihabSize32 < 8UL || (ulong)shihabSize32 > (ulong)mehediMaxRem)
            {
                return false;
            }
            shihabSize = shihabSize32;
            return true;
        }

        public static bool ReadIsoBoxHeaderFromBuf(byte[] shihabBuf, int mehediOff, int shihabLimit, out int mehediBoxLen, out string shihabType, out int mehediHdrLen)
        {
            mehediBoxLen = 0;
            shihabType = "";
            mehediHdrLen = 8;
            if (shihabBuf == null || mehediOff < 0 || shihabLimit > shihabBuf.Length || shihabLimit - mehediOff < 8)
            {
                return false;
            }

            uint shihabSize32 = ReadBeU32(shihabBuf, mehediOff);
            shihabType = Encoding.ASCII.GetString(shihabBuf, mehediOff + 4, 4);
            int remaining = shihabLimit - mehediOff;

            if (shihabSize32 == 1)
            {
                if (remaining < 16)
                {
                    return false;
                }
                ulong size64 = ReadBeU64(shihabBuf, mehediOff + 8);
                if (size64 < 16UL || size64 > (ulong)remaining)
                {
                    return false;
                }
                mehediBoxLen = (int)size64;
                mehediHdrLen = 16;
                return true;
            }

            if (shihabSize32 == 0)
            {
                mehediBoxLen = remaining;
                return mehediBoxLen >= 8;
            }

            if ((ulong)shihabSize32 < 8UL || (ulong)shihabSize32 > (ulong)remaining)
            {
                return false;
            }
            mehediBoxLen = (int)shihabSize32;
            return true;
        }

        public static byte[] WrapIsoBox(string shihabFourCc, byte[] mehediPayload)
        {
            int payloadLen = mehediPayload != null ? mehediPayload.Length : 0;
            byte[] shihabOut = new byte[8 + payloadLen];
            uint mehediTotal = (uint)shihabOut.Length;
            shihabOut[0] = (byte)((mehediTotal >> 24) & 0xFF);
            shihabOut[1] = (byte)((mehediTotal >> 16) & 0xFF);
            shihabOut[2] = (byte)((mehediTotal >> 8) & 0xFF);
            shihabOut[3] = (byte)(mehediTotal & 0xFF);
            byte[] shihabTag = Encoding.ASCII.GetBytes(shihabFourCc);
            Buffer.BlockCopy(shihabTag, 0, shihabOut, 4, 4);
            if (payloadLen > 0)
            {
                Buffer.BlockCopy(mehediPayload, 0, shihabOut, 8, payloadLen);
            }
            return shihabOut;
        }

        public static void WriteIdentityMatrix(Stream shihabMs)
        {
            uint[] mehediMat = new uint[] { 0x00010000u, 0, 0, 0, 0x00010000u, 0, 0, 0, 0x40000000u };
            for (int shihabI = 0; shihabI < 9; shihabI++)
            {
                WriteBeU32(shihabMs, mehediMat[shihabI]);
            }
        }

        public static void WriteAscii(Stream shihabMs, string mehediStr)
        {
            byte[] shihabBytes = Encoding.ASCII.GetBytes(mehediStr);
            shihabMs.Write(shihabBytes, 0, shihabBytes.Length);
        }

        public static void WriteBeU16(Stream shihabMs, ushort mehediVal)
        {
            shihabMs.WriteByte((byte)((mehediVal >> 8) & 0xFF));
            shihabMs.WriteByte((byte)(mehediVal & 0xFF));
        }

        public static void WriteBeU32(Stream shihabMs, uint mehediVal)
        {
            shihabMs.WriteByte((byte)((mehediVal >> 24) & 0xFF));
            shihabMs.WriteByte((byte)((mehediVal >> 16) & 0xFF));
            shihabMs.WriteByte((byte)((mehediVal >> 8) & 0xFF));
            shihabMs.WriteByte((byte)(mehediVal & 0xFF));
        }

        public static void WriteBeU64(Stream shihabMs, ulong mehediVal)
        {
            WriteBeU32(shihabMs, (uint)(mehediVal >> 32));
            WriteBeU32(shihabMs, (uint)(mehediVal & 0xFFFFFFFFUL));
        }

        public static ushort ReadBeU16(byte[] shihabBuf, int mehediOff)
        {
            return (ushort)((shihabBuf[mehediOff] << 8) | shihabBuf[mehediOff + 1]);
        }

        public static uint ReadBeU32(byte[] shihabBuf, int mehediOff)
        {
            return ((uint)shihabBuf[mehediOff] << 24) |
                   ((uint)shihabBuf[mehediOff + 1] << 16) |
                   ((uint)shihabBuf[mehediOff + 2] << 8) |
                   shihabBuf[mehediOff + 3];
        }

        public static ulong ReadBeU64(byte[] shihabBuf, int mehediOff)
        {
            return ((ulong)ReadBeU32(shihabBuf, mehediOff) << 32) | ReadBeU32(shihabBuf, mehediOff + 4);
        }

        public static int ReadExactCountFromStream(Stream shihabStream, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int got = shihabStream.Read(buffer, offset + total, count - total);
                if (got <= 0)
                {
                    break;
                }
                total += got;
            }
            return total;
        }

        public static byte[] ReadExactBytesFromStream(Stream shihabStream, int mehediCount)
        {
            return ReadExactBytesFromStream(shihabStream, mehediCount, MaxMetadataBufferBytes);
        }

        public static byte[] ReadExactBytesFromStream(Stream shihabStream, int mehediCount, int maxAllowedBytes)
        {
            if (mehediCount < 0 || mehediCount > maxAllowedBytes)
            {
                throw new InvalidDataException("Media box or packet size exceeds safe bounds.");
            }
            if (mehediCount == 0)
            {
                return new byte[0];
            }
            if (shihabStream.CanSeek)
            {
                long remaining = shihabStream.Length - shihabStream.Position;
                if (mehediCount > remaining)
                {
                    throw new EndOfStreamException("Truncated media box exceeds remaining stream length.");
                }
            }

            byte[] shihabBuf = new byte[mehediCount];
            int mehediOff = 0;
            while (mehediOff < mehediCount)
            {
                int shihabGot = shihabStream.Read(shihabBuf, mehediOff, mehediCount - mehediOff);
                if (shihabGot <= 0)
                {
                    throw new EndOfStreamException("Unexpected EOF while reading media metadata.");
                }
                mehediOff += shihabGot;
            }
            return shihabBuf;
        }

        public static bool ReadEbmlHeader(Stream shihabStream, out ulong mehediId, out long shihabSize)
        {
            mehediId = 0;
            shihabSize = 0;
            int mehediFirst = shihabStream.ReadByte();
            if (mehediFirst < 0)
            {
                return false;
            }

            int shihabIdLen = CountLeadingZeroBits((byte)mehediFirst) + 1;
            if (shihabIdLen > 4)
            {
                return false;
            }
            mehediId = (ulong)(byte)mehediFirst;
            for (int shihabI = 1; shihabI < shihabIdLen; shihabI++)
            {
                int mehediB = shihabStream.ReadByte();
                if (mehediB < 0)
                {
                    return false;
                }
                mehediId = (mehediId << 8) | (ulong)(byte)mehediB;
            }

            int shihabSzFirst = shihabStream.ReadByte();
            if (shihabSzFirst < 0)
            {
                return false;
            }
            int mehediSzLen = CountLeadingZeroBits((byte)shihabSzFirst) + 1;
            if (mehediSzLen > 8)
            {
                return false;
            }
            ulong shihabMask = (1UL << (8 - mehediSzLen)) - 1UL;
            ulong mehediVal = (ulong)(byte)shihabSzFirst & shihabMask;
            bool shihabAllOnes = mehediVal == shihabMask;

            for (int mehediI = 1; mehediI < mehediSzLen; mehediI++)
            {
                int shihabB = shihabStream.ReadByte();
                if (shihabB < 0)
                {
                    return false;
                }
                if (shihabB != 0xFF)
                {
                    shihabAllOnes = false;
                }
                mehediVal = (mehediVal << 8) | (ulong)(byte)shihabB;
            }
            if (shihabAllOnes)
            {
                shihabSize = -1L;
                return true;
            }
            if (mehediVal > (ulong)long.MaxValue)
            {
                return false;
            }
            shihabSize = (long)mehediVal;
            return true;
        }

        public static bool ReadEbmlHeaderFromBuf(byte[] shihabBuf, ref int mehediOff, int shihabLimit, out ulong mehediId, out long shihabSize)
        {
            mehediId = 0;
            shihabSize = 0;
            if (shihabBuf == null || mehediOff < 0 || shihabLimit > shihabBuf.Length || mehediOff >= shihabLimit)
            {
                return false;
            }

            byte mehediFirst = shihabBuf[mehediOff++];
            int shihabIdLen = CountLeadingZeroBits(mehediFirst) + 1;
            if (shihabIdLen > 4 || shihabIdLen - 1 > shihabLimit - mehediOff)
            {
                return false;
            }
            mehediId = mehediFirst;
            for (int shihabI = 1; shihabI < shihabIdLen; shihabI++)
            {
                mehediId = (mehediId << 8) | shihabBuf[mehediOff++];
            }

            if (mehediOff >= shihabLimit)
            {
                return false;
            }
            byte shihabSzFirst = shihabBuf[mehediOff++];
            int mehediSzLen = CountLeadingZeroBits(shihabSzFirst) + 1;
            if (mehediSzLen > 8 || mehediSzLen - 1 > shihabLimit - mehediOff)
            {
                return false;
            }
            ulong shihabMask = (1UL << (8 - mehediSzLen)) - 1UL;
            ulong mehediVal = shihabSzFirst & shihabMask;
            bool shihabAllOnes = mehediVal == shihabMask;

            for (int mehediI = 1; mehediI < mehediSzLen; mehediI++)
            {
                byte shihabB = shihabBuf[mehediOff++];
                if (shihabB != 0xFF)
                {
                    shihabAllOnes = false;
                }
                mehediVal = (mehediVal << 8) | shihabB;
            }
            if (shihabAllOnes)
            {
                shihabSize = -1L;
                return true;
            }
            if (mehediVal > (ulong)long.MaxValue)
            {
                return false;
            }
            shihabSize = (long)mehediVal;
            return true;
        }

        public static bool ReadEbmlVintValue(byte[] shihabBuf, ref int mehediOff, out long shihabValue)
        {
            shihabValue = 0;
            if (shihabBuf == null || mehediOff < 0 || mehediOff >= shihabBuf.Length)
            {
                return false;
            }
            byte mehediFirst = shihabBuf[mehediOff++];
            int shihabLen = CountLeadingZeroBits(mehediFirst) + 1;
            if (shihabLen > 8 || shihabLen - 1 > shihabBuf.Length - mehediOff)
            {
                return false;
            }
            ulong mehediVal = (ulong)mehediFirst & ((1UL << (8 - shihabLen)) - 1UL);
            for (int shihabI = 1; shihabI < shihabLen; shihabI++)
            {
                mehediVal = (mehediVal << 8) | shihabBuf[mehediOff++];
            }
            if (mehediVal > (ulong)long.MaxValue)
            {
                return false;
            }
            shihabValue = (long)mehediVal;
            return true;
        }

        public static int CountLeadingZeroBits(byte shihabVal)
        {
            if (shihabVal == 0)
            {
                return 8;
            }
            int mehediCount = 0;
            byte shihabMask = 0x80;
            while ((shihabVal & shihabMask) == 0)
            {
                mehediCount++;
                shihabMask >>= 1;
            }
            return mehediCount;
        }

        public static ulong ReadEbmlUint(byte[] shihabBuf, int mehediOff, int shihabLen)
        {
            if (shihabBuf == null || mehediOff < 0 || shihabLen <= 0 || shihabLen > shihabBuf.Length - mehediOff)
            {
                return 0UL;
            }
            int clamped = Math.Min(shihabLen, 8);
            int start = mehediOff + (shihabLen - clamped);
            ulong mehediVal = 0;
            for (int shihabI = 0; shihabI < clamped; shihabI++)
            {
                mehediVal = (mehediVal << 8) | shihabBuf[start + shihabI];
            }
            return mehediVal;
        }

        public static byte[] EncodeUnsignedBigEndian(ulong shihabVal)
        {
            int len = 1;
            ulong probe = shihabVal >> 8;
            while (probe > 0)
            {
                len++;
                probe >>= 8;
            }
            byte[] outBytes = new byte[len];
            for (int i = len - 1; i >= 0; i--)
            {
                outBytes[i] = (byte)(shihabVal & 0xFFUL);
                shihabVal >>= 8;
            }
            return outBytes;
        }

        public static void WriteEbmlId(Stream shihabStream, ulong mehediId)
        {
            if (mehediId <= 0xFFUL)
            {
                shihabStream.WriteByte((byte)mehediId);
            }
            else if (mehediId <= 0xFFFFUL)
            {
                shihabStream.WriteByte((byte)(mehediId >> 8));
                shihabStream.WriteByte((byte)mehediId);
            }
            else if (mehediId <= 0xFFFFFFUL)
            {
                shihabStream.WriteByte((byte)(mehediId >> 16));
                shihabStream.WriteByte((byte)(mehediId >> 8));
                shihabStream.WriteByte((byte)mehediId);
            }
            else
            {
                shihabStream.WriteByte((byte)(mehediId >> 24));
                shihabStream.WriteByte((byte)(mehediId >> 16));
                shihabStream.WriteByte((byte)(mehediId >> 8));
                shihabStream.WriteByte((byte)mehediId);
            }
        }

        public static void WriteEbmlSize(Stream shihabStream, ulong mehediSize)
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

        public static void WriteEbmlElement(Stream shihabStream, ulong mehediId, byte[] shihabPayload)
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


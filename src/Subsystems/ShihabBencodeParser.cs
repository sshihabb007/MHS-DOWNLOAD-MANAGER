using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ShihabMehediIDM
{
    public class ShihabBencodeNode
    {
        public string Kind; // "int", "bytes", "list", "dict"
        public long IntValue;
        public byte[] BytesValue;
        public List<ShihabBencodeNode> ListValue;
        public Dictionary<string, ShihabBencodeNode> DictValue;
        public int RawStartOffset;
        public int RawEndOffset;

        public string GetUtf8String()
        {
            return BytesValue != null ? Encoding.UTF8.GetString(BytesValue) : "";
        }
    }

    public static class ShihabBencodeParser
    {
        public const int MaxBencodeDepth = 64;
        private const int MaxCollectionItems = 250000;

        public static ShihabBencodeNode Parse(byte[] data, ref int pos)
        {
            return ParseInternal(data, ref pos, 0);
        }

        private static ShihabBencodeNode ParseInternal(byte[] data, ref int pos, int depth)
        {
            if (depth > MaxBencodeDepth)
            {
                throw new InvalidDataException("Bencode nesting depth limit exceeded.");
            }
            if (data == null || pos < 0 || pos >= data.Length) return null;

            int startOffset = pos;
            byte c = data[pos];
            if (c == (byte)'i')
            {
                pos++;
                int end = pos;
                while (end < data.Length && data[end] != (byte)'e') end++;
                if (end >= data.Length) return null;
                string numStr = Encoding.ASCII.GetString(data, pos, end - pos);
                pos = end + 1;
                long v = 0;
                if (!long.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                {
                    return null;
                }
                return new ShihabBencodeNode
                {
                    Kind = "int",
                    IntValue = v,
                    RawStartOffset = startOffset,
                    RawEndOffset = pos
                };
            }
            if (c == (byte)'l')
            {
                pos++;
                List<ShihabBencodeNode> list = new List<ShihabBencodeNode>();
                int items = 0;
                while (pos < data.Length && data[pos] != (byte)'e')
                {
                    if (++items > MaxCollectionItems) throw new InvalidDataException("Bencode list exceeds item limit.");
                    ShihabBencodeNode child = ParseInternal(data, ref pos, depth + 1);
                    if (child == null) break;
                    list.Add(child);
                }
                if (pos < data.Length && data[pos] == (byte)'e') pos++;
                return new ShihabBencodeNode
                {
                    Kind = "list",
                    ListValue = list,
                    RawStartOffset = startOffset,
                    RawEndOffset = pos
                };
            }
            if (c == (byte)'d')
            {
                pos++;
                Dictionary<string, ShihabBencodeNode> dict = new Dictionary<string, ShihabBencodeNode>(StringComparer.Ordinal);
                int entries = 0;
                while (pos < data.Length && data[pos] != (byte)'e')
                {
                    if (++entries > MaxCollectionItems) throw new InvalidDataException("Bencode dictionary exceeds item limit.");
                    ShihabBencodeNode kNode = ParseInternal(data, ref pos, depth + 1);
                    if (kNode == null) break;
                    ShihabBencodeNode vNode = ParseInternal(data, ref pos, depth + 1);
                    dict[kNode.GetUtf8String()] = vNode;
                }
                if (pos < data.Length && data[pos] == (byte)'e') pos++;
                return new ShihabBencodeNode
                {
                    Kind = "dict",
                    DictValue = dict,
                    RawStartOffset = startOffset,
                    RawEndOffset = pos
                };
            }
            if (c >= (byte)'0' && c <= (byte)'9')
            {
                int colon = pos;
                while (colon < data.Length && data[colon] != (byte)':') colon++;
                if (colon >= data.Length) return null;
                string lenStr = Encoding.ASCII.GetString(data, pos, colon - pos);
                int len;
                if (!int.TryParse(lenStr, NumberStyles.None, CultureInfo.InvariantCulture, out len) || len < 0)
                {
                    return null;
                }
                pos = colon + 1;
                if (pos > data.Length) return null;
                int avail = data.Length - pos;
                if (len > avail) len = avail;
                byte[] bytes = new byte[len];
                if (len > 0) Buffer.BlockCopy(data, pos, bytes, 0, len);
                pos += len;
                return new ShihabBencodeNode
                {
                    Kind = "bytes",
                    BytesValue = bytes,
                    RawStartOffset = startOffset,
                    RawEndOffset = pos
                };
            }
            pos++;
            return null;
        }

        public static void WriteAscii(Stream s, string txt)
        {
            byte[] b = Encoding.ASCII.GetBytes(txt);
            s.Write(b, 0, b.Length);
        }

        public static void WriteBencodeString(Stream s, string str)
        {
            byte[] b = Encoding.UTF8.GetBytes(str ?? "");
            WriteAscii(s, b.Length.ToString(CultureInfo.InvariantCulture) + ":");
            s.Write(b, 0, b.Length);
        }

        public static void WriteBencodeKeyString(Stream s, string key, string val)
        {
            WriteBencodeString(s, key);
            WriteBencodeString(s, val);
        }

        public static void WriteBencodeInt(Stream s, long val)
        {
            WriteAscii(s, "i" + val.ToString(CultureInfo.InvariantCulture) + "e");
        }
    }
}


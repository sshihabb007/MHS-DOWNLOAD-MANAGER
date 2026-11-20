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
        private const int MaxTotalNodes = 500000;

        public static ShihabBencodeNode Parse(byte[] data, ref int pos)
        {
            int nodeCount = 0;
            return ParseInternal(data, ref pos, 0, ref nodeCount);
        }

        private static bool IsValidStrictBencodeInt(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s == "-0") return false;
            int idx = 0;
            if (s[0] == '-')
            {
                if (s.Length == 1) return false;
                idx = 1;
            }
            if (s.Length - idx > 1 && s[idx] == '0') return false;
            for (int i = idx; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9') return false;
            }
            return true;
        }

        private static bool IsValidStrictNonNegativeLength(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Length > 1 && s[0] == '0') return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9') return false;
            }
            return true;
        }

        private static ShihabBencodeNode ParseInternal(byte[] data, ref int pos, int depth, ref int nodeCount)
        {
            if (depth > MaxBencodeDepth)
            {
                throw new InvalidDataException("Bencode nesting depth limit exceeded.");
            }
            if (++nodeCount > MaxTotalNodes)
            {
                throw new InvalidDataException("Bencode total node limit exceeded.");
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
                if (!IsValidStrictBencodeInt(numStr)) return null;
                long v = 0;
                if (!long.TryParse(numStr, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v))
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
                    ShihabBencodeNode child = ParseInternal(data, ref pos, depth + 1, ref nodeCount);
                    if (child == null) return null;
                    list.Add(child);
                }
                if (pos >= data.Length || data[pos] != (byte)'e') return null;
                pos++;
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
                    ShihabBencodeNode kNode = ParseInternal(data, ref pos, depth + 1, ref nodeCount);
                    if (kNode == null || kNode.Kind != "bytes") return null;
                    ShihabBencodeNode vNode = ParseInternal(data, ref pos, depth + 1, ref nodeCount);
                    if (vNode == null) return null;
                    dict[kNode.GetUtf8String()] = vNode;
                }
                if (pos >= data.Length || data[pos] != (byte)'e') return null;
                pos++;
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
                if (!IsValidStrictNonNegativeLength(lenStr)) return null;
                int len;
                if (!int.TryParse(lenStr, NumberStyles.None, CultureInfo.InvariantCulture, out len) || len < 0)
                {
                    return null;
                }
                pos = colon + 1;
                if (pos > data.Length) return null;
                int avail = data.Length - pos;
                if (len > avail) return null; // Fail-closed: never silently truncate binary strings
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
            return null;
        }

        public static void WriteAscii(Stream s, string txt)
        {
            byte[] b = Encoding.ASCII.GetBytes(txt);
            s.Write(b, 0, b.Length);
        }

        public static void WriteBencodeBytes(Stream s, byte[] rawBytes)
        {
            byte[] b = rawBytes ?? new byte[0];
            WriteAscii(s, b.Length.ToString(CultureInfo.InvariantCulture) + ":");
            if (b.Length > 0) s.Write(b, 0, b.Length);
        }

        public static void WriteBencodeString(Stream s, string str)
        {
            byte[] b = Encoding.UTF8.GetBytes(str ?? "");
            WriteAscii(s, b.Length.ToString(CultureInfo.InvariantCulture) + ":");
            if (b.Length > 0) s.Write(b, 0, b.Length);
        }

        public static void WriteBencodeKeyString(Stream s, string key, string val)
        {
            WriteBencodeString(s, key);
            WriteBencodeString(s, val);
        }

        public static void WriteBencodeKeyBytes(Stream s, string key, byte[] val)
        {
            WriteBencodeString(s, key);
            WriteBencodeBytes(s, val);
        }

        public static void WriteBencodeInt(Stream s, long val)
        {
            WriteAscii(s, "i" + val.ToString(CultureInfo.InvariantCulture) + "e");
        }

        public static byte[] EncodeCanonical(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                WriteCanonicalValue(ms, obj);
                return ms.ToArray();
            }
        }

        private static void WriteCanonicalValue(Stream s, object obj)
        {
            if (obj == null)
            {
                WriteBencodeString(s, "");
                return;
            }
            if (obj is byte[])
            {
                WriteBencodeBytes(s, (byte[])obj);
                return;
            }
            if (obj is string)
            {
                WriteBencodeString(s, (string)obj);
                return;
            }
            if (obj is int)
            {
                WriteBencodeInt(s, (int)obj);
                return;
            }
            if (obj is long)
            {
                WriteBencodeInt(s, (long)obj);
                return;
            }
            if (obj is IList<object>)
            {
                IList<object> list = (IList<object>)obj;
                WriteAscii(s, "l");
                for (int i = 0; i < list.Count; i++) WriteCanonicalValue(s, list[i]);
                WriteAscii(s, "e");
                return;
            }
            if (obj is IDictionary<string, object>)
            {
                IDictionary<string, object> dict = (IDictionary<string, object>)obj;
                List<KeyValuePair<byte[], object>> sorted = new List<KeyValuePair<byte[], object>>(dict.Count);
                foreach (KeyValuePair<string, object> kv in dict)
                {
                    sorted.Add(new KeyValuePair<byte[], object>(Encoding.UTF8.GetBytes(kv.Key ?? ""), kv.Value));
                }
                sorted.Sort(delegate(KeyValuePair<byte[], object> a, KeyValuePair<byte[], object> b)
                {
                    int minLen = Math.Min(a.Key.Length, b.Key.Length);
                    for (int i = 0; i < minLen; i++)
                    {
                        int cmp = a.Key[i].CompareTo(b.Key[i]);
                        if (cmp != 0) return cmp;
                    }
                    return a.Key.Length.CompareTo(b.Key.Length);
                });
                WriteAscii(s, "d");
                for (int i = 0; i < sorted.Count; i++)
                {
                    WriteBencodeBytes(s, sorted[i].Key);
                    WriteCanonicalValue(s, sorted[i].Value);
                }
                WriteAscii(s, "e");
                return;
            }
            WriteBencodeString(s, Convert.ToString(obj, CultureInfo.InvariantCulture));
        }
    }
}

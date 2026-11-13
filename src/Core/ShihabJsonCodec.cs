using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShihabMehediIDM
{
    /// <summary>
    /// Strict C# 5 RFC-8259 JSON parser and serializer with recursion depth and token limits.
    /// Replaces ad-hoc regex extraction across Bridge, Native Host, MCP, Debrid, and Media Helpers.
    /// </summary>
    public static class ShihabJsonCodec
    {
        public static bool TryParse(string json, out object root, int maxDepth = 64, int maxValues = 200000)
        {
            root = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }
            if (maxDepth < 1) maxDepth = 1;
            if (maxValues < 16) maxValues = 16;

            Parser parser = new Parser(json, maxDepth, maxValues);
            try
            {
                root = parser.ParseRoot();
                return true;
            }
            catch
            {
                root = null;
                return false;
            }
        }

        public static Dictionary<string, object> ParseObjectOrNull(string json, int maxDepth = 64)
        {
            object root;
            if (!TryParse(json, out root, maxDepth))
            {
                return null;
            }
            return root as Dictionary<string, object>;
        }

        public static string GetString(IDictionary<string, object> obj, string key, string fallback = "")
        {
            if (obj == null || string.IsNullOrEmpty(key))
            {
                return fallback;
            }
            object val;
            if (!obj.TryGetValue(key, out val) || val == null)
            {
                return fallback;
            }
            string s = val as string;
            if (s != null)
            {
                return s;
            }
            if (val is bool)
            {
                return ((bool)val) ? "true" : "false";
            }
            if (val is long || val is int || val is double || val is decimal)
            {
                return Convert.ToString(val, CultureInfo.InvariantCulture);
            }
            return fallback;
        }

        public static long GetLong(IDictionary<string, object> obj, string key, long fallback = 0)
        {
            if (obj == null || string.IsNullOrEmpty(key))
            {
                return fallback;
            }
            object val;
            if (!obj.TryGetValue(key, out val) || val == null)
            {
                return fallback;
            }
            if (val is long) return (long)val;
            if (val is int) return (long)(int)val;
            if (val is double)
            {
                double d = (double)val;
                if (!double.IsNaN(d) && !double.IsInfinity(d) && d >= long.MinValue && d <= long.MaxValue)
                {
                    return (long)d;
                }
                return fallback;
            }
            string s = val as string;
            long parsed;
            if (s != null && long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }
            return fallback;
        }

        public static int GetInt(IDictionary<string, object> obj, string key, int fallback = 0)
        {
            long val = GetLong(obj, key, fallback);
            if (val < int.MinValue || val > int.MaxValue)
            {
                return fallback;
            }
            return (int)val;
        }

        public static bool GetBool(IDictionary<string, object> obj, string key, bool fallback = false)
        {
            if (obj == null || string.IsNullOrEmpty(key))
            {
                return fallback;
            }
            object val;
            if (!obj.TryGetValue(key, out val) || val == null)
            {
                return fallback;
            }
            if (val is bool)
            {
                return (bool)val;
            }
            if (val is long)
            {
                return (long)val != 0;
            }
            string s = val as string;
            if (s != null)
            {
                if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1") return true;
                if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase) || s == "0") return false;
            }
            return fallback;
        }

        public static Dictionary<string, object> GetObject(IDictionary<string, object> obj, string key)
        {
            if (obj == null || string.IsNullOrEmpty(key))
            {
                return null;
            }
            object val;
            if (!obj.TryGetValue(key, out val))
            {
                return null;
            }
            return val as Dictionary<string, object>;
        }

        public static List<object> GetArray(IDictionary<string, object> obj, string key)
        {
            if (obj == null || string.IsNullOrEmpty(key))
            {
                return null;
            }
            object val;
            if (!obj.TryGetValue(key, out val))
            {
                return null;
            }
            return val as List<object>;
        }

        public static List<string> GetStringArray(IDictionary<string, object> obj, string key)
        {
            List<string> results = new List<string>();
            List<object> raw = GetArray(obj, key);
            if (raw == null)
            {
                return results;
            }
            for (int i = 0; i < raw.Count; i++)
            {
                string s = raw[i] as string;
                if (s != null)
                {
                    results.Add(s);
                }
            }
            return results;
        }

        public static string SerializeJsonRpcId(IDictionary<string, object> obj)
        {
            if (obj == null)
            {
                return "null";
            }
            object val;
            if (!obj.TryGetValue("id", out val) || val == null)
            {
                return "null";
            }
            string s = val as string;
            if (s != null)
            {
                return "\"" + EscapeString(s) + "\"";
            }
            if (val is long)
            {
                return ((long)val).ToString(CultureInfo.InvariantCulture);
            }
            if (val is int)
            {
                return ((int)val).ToString(CultureInfo.InvariantCulture);
            }
            if (val is double)
            {
                double d = (double)val;
                return double.IsNaN(d) || double.IsInfinity(d)
                    ? "null"
                    : d.ToString("R", CultureInfo.InvariantCulture);
            }
            return "null";
        }

        public static string EscapeString(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder(text.Length + 8);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20 || ch == 0x2028 || ch == 0x2029)
                        {
                            sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)ch);
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        public static string Serialize(object value)
        {
            StringBuilder sb = new StringBuilder();
            WriteValue(sb, value, 0);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value, int depth)
        {
            if (depth > 64 || value == null)
            {
                sb.Append("null");
                return;
            }
            string s = value as string;
            if (s != null)
            {
                sb.Append('"').Append(EscapeString(s)).Append('"');
                return;
            }
            if (value is bool)
            {
                sb.Append(((bool)value) ? "true" : "false");
                return;
            }
            if (value is int || value is long || value is short || value is byte || value is uint || value is ulong)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            if (value is double)
            {
                double d = (double)value;
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    sb.Append("null");
                }
                else
                {
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                }
                return;
            }
            if (value is float)
            {
                float f = (float)value;
                if (float.IsNaN(f) || float.IsInfinity(f))
                {
                    sb.Append("null");
                }
                else
                {
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                }
                return;
            }
            if (value is decimal)
            {
                sb.Append(((decimal)value).ToString(CultureInfo.InvariantCulture));
                return;
            }

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry entry in dict)
                {
                    string key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(EscapeString(key)).Append("\":");
                    WriteValue(sb, entry.Value, depth + 1);
                }
                sb.Append('}');
                return;
            }

            IEnumerable seq = value as IEnumerable;
            if (seq != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (object item in seq)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteValue(sb, item, depth + 1);
                }
                sb.Append(']');
                return;
            }

            sb.Append('"').Append(EscapeString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)).Append('"');
        }

        private sealed class Parser
        {
            private readonly string _src;
            private readonly int _len;
            private readonly int _maxDepth;
            private readonly int _maxValues;
            private int _pos;
            private int _valueCount;

            internal Parser(string src, int maxDepth, int maxValues)
            {
                _src = src;
                _len = src.Length;
                _maxDepth = maxDepth;
                _maxValues = maxValues;
                _pos = 0;
                _valueCount = 0;
            }

            internal object ParseRoot()
            {
                SkipWhitespace();
                object val = ParseValue(0);
                SkipWhitespace();
                if (_pos != _len)
                {
                    throw new FormatException("Trailing non-whitespace characters in JSON payload.");
                }
                return val;
            }

            private object ParseValue(int depth)
            {
                if (depth > _maxDepth)
                {
                    throw new InvalidOperationException("JSON recursion limit exceeded.");
                }
                _valueCount++;
                if (_valueCount > _maxValues)
                {
                    throw new InvalidOperationException("JSON node budget exceeded.");
                }

                SkipWhitespace();
                if (_pos >= _len)
                {
                    throw new FormatException("Unexpected end of JSON input.");
                }

                char ch = _src[_pos];
                switch (ch)
                {
                    case '{': return ParseObject(depth + 1);
                    case '[': return ParseArray(depth + 1);
                    case '"': return ParseString();
                    case 't': ConsumeLiteral("true"); return true;
                    case 'f': ConsumeLiteral("false"); return false;
                    case 'n': ConsumeLiteral("null"); return null;
                    default:
                        if (ch == '-' || (ch >= '0' && ch <= '9'))
                        {
                            return ParseNumber();
                        }
                        throw new FormatException("Unexpected character in JSON input.");
                }
            }

            private Dictionary<string, object> ParseObject(int depth)
            {
                if (depth > _maxDepth)
                {
                    throw new InvalidOperationException("JSON recursion limit exceeded.");
                }
                _pos++; // '{'
                Dictionary<string, object> map = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (_pos < _len && _src[_pos] == '}')
                {
                    _pos++;
                    return map;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (_pos >= _len || _src[_pos] != '"')
                    {
                        throw new FormatException("Expected JSON property name string.");
                    }
                    string key = ParseString();
                    SkipWhitespace();
                    if (_pos >= _len || _src[_pos] != ':')
                    {
                        throw new FormatException("Expected ':' after JSON property name.");
                    }
                    _pos++;
                    object val = ParseValue(depth);
                    map[key] = val;

                    SkipWhitespace();
                    if (_pos >= _len)
                    {
                        throw new FormatException("Unterminated JSON object.");
                    }
                    char next = _src[_pos];
                    if (next == '}')
                    {
                        _pos++;
                        return map;
                    }
                    if (next != ',')
                    {
                        throw new FormatException("Expected ',' or '}' in JSON object.");
                    }
                    _pos++;
                }
            }

            private List<object> ParseArray(int depth)
            {
                if (depth > _maxDepth)
                {
                    throw new InvalidOperationException("JSON recursion limit exceeded.");
                }
                _pos++; // '['
                List<object> list = new List<object>();
                SkipWhitespace();
                if (_pos < _len && _src[_pos] == ']')
                {
                    _pos++;
                    return list;
                }

                while (true)
                {
                    list.Add(ParseValue(depth));
                    SkipWhitespace();
                    if (_pos >= _len)
                    {
                        throw new FormatException("Unterminated JSON array.");
                    }
                    char next = _src[_pos];
                    if (next == ']')
                    {
                        _pos++;
                        return list;
                    }
                    if (next != ',')
                    {
                        throw new FormatException("Expected ',' or ']' in JSON array.");
                    }
                    _pos++;
                }
            }

            private string ParseString()
            {
                _pos++; // opening '"'
                StringBuilder sb = new StringBuilder();
                while (_pos < _len)
                {
                    char ch = _src[_pos++];
                    if (ch == '"')
                    {
                        return sb.ToString();
                    }
                    if (ch < 0x20)
                    {
                        throw new FormatException("Unescaped control character in JSON string.");
                    }
                    if (ch != '\\')
                    {
                        sb.Append(ch);
                        continue;
                    }
                    if (_pos >= _len)
                    {
                        throw new FormatException("Unterminated escape sequence in JSON string.");
                    }
                    char esc = _src[_pos++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _len)
                            {
                                throw new FormatException("Incomplete \\uXXXX escape in JSON string.");
                            }
                            string hex = _src.Substring(_pos, 4);
                            int codePoint;
                            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codePoint))
                            {
                                throw new FormatException("Invalid \\uXXXX hex escape in JSON string.");
                            }
                            _pos += 4;
                            sb.Append((char)codePoint);
                            break;
                        default:
                            throw new FormatException("Invalid escape character in JSON string.");
                    }
                }
                throw new FormatException("Unterminated JSON string.");
            }

            private object ParseNumber()
            {
                int start = _pos;
                bool isFloat = false;
                if (_src[_pos] == '-')
                {
                    _pos++;
                }
                if (_pos >= _len || _src[_pos] < '0' || _src[_pos] > '9')
                {
                    throw new FormatException("Invalid JSON number.");
                }
                if (_src[_pos] == '0')
                {
                    _pos++;
                }
                else
                {
                    while (_pos < _len && _src[_pos] >= '0' && _src[_pos] <= '9')
                    {
                        _pos++;
                    }
                }
                if (_pos < _len && _src[_pos] == '.')
                {
                    isFloat = true;
                    _pos++;
                    if (_pos >= _len || _src[_pos] < '0' || _src[_pos] > '9')
                    {
                        throw new FormatException("Invalid JSON fraction.");
                    }
                    while (_pos < _len && _src[_pos] >= '0' && _src[_pos] <= '9')
                    {
                        _pos++;
                    }
                }
                if (_pos < _len && (_src[_pos] == 'e' || _src[_pos] == 'E'))
                {
                    isFloat = true;
                    _pos++;
                    if (_pos < _len && (_src[_pos] == '+' || _src[_pos] == '-'))
                    {
                        _pos++;
                    }
                    if (_pos >= _len || _src[_pos] < '0' || _src[_pos] > '9')
                    {
                        throw new FormatException("Invalid JSON exponent.");
                    }
                    while (_pos < _len && _src[_pos] >= '0' && _src[_pos] <= '9')
                    {
                        _pos++;
                    }
                }

                string raw = _src.Substring(start, _pos - start);
                if (!isFloat)
                {
                    long lVal;
                    if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out lVal))
                    {
                        return lVal;
                    }
                }
                double dVal;
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out dVal))
                {
                    throw new FormatException("JSON number out of range.");
                }
                return dVal;
            }

            private void ConsumeLiteral(string expected)
            {
                if (_pos + expected.Length > _len)
                {
                    throw new FormatException("Unexpected end of JSON literal.");
                }
                for (int i = 0; i < expected.Length; i++)
                {
                    if (_src[_pos + i] != expected[i])
                    {
                        throw new FormatException("Invalid JSON literal.");
                    }
                }
                _pos += expected.Length;
            }

            private void SkipWhitespace()
            {
                while (_pos < _len)
                {
                    char ch = _src[_pos];
                    if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n')
                    {
                        _pos++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
    }
}


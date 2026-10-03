// Tiny dependency-free JSON codec covering exactly what the bridge protocol
// needs: flat objects with string/number/boolean/null values. R.E.P.O. runs on
// Unity's Mono with netstandard2.1, where System.Text.Json is unavailable
// without extra packages, and we intentionally avoid adding any.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MinecraftInRepo.Net
{
    public static class JsonLite
    {
        // ------------------------------------------------------------------
        // Parsing
        // ------------------------------------------------------------------

        public static Dictionary<string, object> ParseObject(string json)
        {
            int i = 0;
            SkipWhitespace(json, ref i);
            Dictionary<string, object> result = ReadObject(json, ref i);
            return result;
        }

        public static double GetNumber(Dictionary<string, object> obj, string key, double fallback = 0.0)
        {
            object value;
            if (obj != null && obj.TryGetValue(key, out value))
            {
                if (value is double d)
                {
                    return d;
                }
                if (value is string s)
                {
                    double parsed;
                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                    {
                        return parsed;
                    }
                }
            }
            return fallback;
        }

        public static string GetString(Dictionary<string, object> obj, string key, string fallback = "")
        {
            object value;
            if (obj != null && obj.TryGetValue(key, out value) && value is string s)
            {
                return s;
            }
            return fallback;
        }

        private static Dictionary<string, object> ReadObject(string json, ref int i)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            Expect(json, ref i, '{');
            SkipWhitespace(json, ref i);
            if (Peek(json, i) == '}')
            {
                i++;
                return result;
            }
            while (true)
            {
                SkipWhitespace(json, ref i);
                string key = ReadString(json, ref i);
                SkipWhitespace(json, ref i);
                Expect(json, ref i, ':');
                SkipWhitespace(json, ref i);
                object value = ReadValue(json, ref i);
                result[key] = value;
                SkipWhitespace(json, ref i);
                char c = Peek(json, i);
                if (c == ',')
                {
                    i++;
                    continue;
                }
                Expect(json, ref i, '}');
                return result;
            }
        }

        private static object ReadValue(string json, ref int i)
        {
            char c = Peek(json, i);
            if (c == '"')
            {
                return ReadString(json, ref i);
            }
            if (c == 't')
            {
                ReadLiteral(json, ref i, "true");
                return true;
            }
            if (c == 'f')
            {
                ReadLiteral(json, ref i, "false");
                return false;
            }
            if (c == 'n')
            {
                ReadLiteral(json, ref i, "null");
                return null;
            }
            return ReadNumber(json, ref i);
        }

        private static string ReadString(string json, ref int i)
        {
            Expect(json, ref i, '"');
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                if (i >= json.Length)
                {
                    throw new FormatException("Unterminated string");
                }
                char c = json[i++];
                if (c == '"')
                {
                    return sb.ToString();
                }
                if (c == '\\')
                {
                    if (i >= json.Length)
                    {
                        throw new FormatException("Bad escape");
                    }
                    char e = json[i++];
                    switch (e)
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
                            if (i + 4 > json.Length)
                            {
                                throw new FormatException("Bad unicode escape");
                            }
                            string hex = json.Substring(i, 4);
                            i += 4;
                            sb.Append((char)ushort.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            break;
                        default:
                            throw new FormatException("Unknown escape: \\" + e);
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        private static double ReadNumber(string json, ref int i)
        {
            int start = i;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E' || (c >= '0' && c <= '9'))
                {
                    i++;
                }
                else
                {
                    break;
                }
            }
            if (start == i)
            {
                throw new FormatException("Expected number at position " + i);
            }
            return double.Parse(json.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void ReadLiteral(string json, ref int i, string literal)
        {
            if (i + literal.Length > json.Length || json.Substring(i, literal.Length) != literal)
            {
                throw new FormatException("Expected '" + literal + "' at position " + i);
            }
            i += literal.Length;
        }

        private static void SkipWhitespace(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
            {
                i++;
            }
        }

        private static char Peek(string json, int i)
        {
            if (i >= json.Length)
            {
                throw new FormatException("Unexpected end of JSON");
            }
            return json[i];
        }

        private static void Expect(string json, ref int i, char expected)
        {
            char c = Peek(json, i);
            if (c != expected)
            {
                throw new FormatException("Expected '" + expected + "' but found '" + c + "' at position " + i);
            }
            i++;
        }

        // ------------------------------------------------------------------
        // Writing
        // ------------------------------------------------------------------

        /// <summary>Writes a flat JSON object from (key, value) pairs. Values may be
        /// string, bool, float, double or int. Key order is preserved.</summary>
        public static string WriteObject(params object[] keysAndValues)
        {
            if (keysAndValues.Length % 2 != 0)
            {
                throw new ArgumentException("keysAndValues must come in pairs");
            }
            StringBuilder sb = new StringBuilder("{");
            for (int p = 0; p < keysAndValues.Length; p += 2)
            {
                if (p > 0)
                {
                    sb.Append(',');
                }
                WriteString(sb, (string)keysAndValues[p]);
                sb.Append(':');
                WriteValue(sb, keysAndValues[p + 1]);
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            if (value is string s)
            {
                WriteString(sb, s);
                return;
            }
            if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
                return;
            }
            if (value is int || value is long)
            {
                sb.Append(((IConvertible)value).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (value is float f)
            {
                sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value is double d)
            {
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            WriteString(sb, value.ToString());
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }
}

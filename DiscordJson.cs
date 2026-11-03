using System;
using System.Text;

namespace maimai_rpc
{
    /// <summary>
    /// Minimal, zero-dependency JSON builder and parser tailored specifically for the Discord IPC protocol.
    /// </summary>
    public static class DiscordJson
    {
        public static string CreateHandshake(string clientId)
        {
            return "{\"v\":1,\"client_id\":\"" + EscapeString(clientId) + "\"}";
        }

        public static string CreateSetActivity(int pid, DiscordActivity activity, string nonce)
        {
            var sb = new StringBuilder();
            sb.Append("{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":").Append(pid);

            if (activity == null)
            {
                sb.Append(",\"activity\":null}");
            }
            else
            {
                sb.Append(",\"activity\":{");
                bool first = true;

                if (!string.IsNullOrEmpty(activity.State))
                {
                    sb.Append("\"state\":\"").Append(EscapeString(activity.State)).Append("\"");
                    first = false;
                }

                if (!string.IsNullOrEmpty(activity.Details))
                {
                    if (!first) sb.Append(",");
                    sb.Append("\"details\":\"").Append(EscapeString(activity.Details)).Append("\"");
                    first = false;
                }

                if (activity.StartTimestamp.HasValue || activity.EndTimestamp.HasValue)
                {
                    if (!first) sb.Append(",");
                    sb.Append("\"timestamps\":{");
                    bool tsFirst = true;
                    if (activity.StartTimestamp.HasValue)
                    {
                        sb.Append("\"start\":").Append(activity.StartTimestamp.Value);
                        tsFirst = false;
                    }
                    if (activity.EndTimestamp.HasValue)
                    {
                        if (!tsFirst) sb.Append(",");
                        sb.Append("\"end\":").Append(activity.EndTimestamp.Value);
                    }
                    sb.Append("}");
                    first = false;
                }

                if (!string.IsNullOrEmpty(activity.LargeImageKey) || !string.IsNullOrEmpty(activity.SmallImageKey))
                {
                    if (!first) sb.Append(",");
                    sb.Append("\"assets\":{");
                    bool aFirst = true;

                    if (!string.IsNullOrEmpty(activity.LargeImageKey))
                    {
                        sb.Append("\"large_image\":\"").Append(EscapeString(activity.LargeImageKey)).Append("\"");
                        aFirst = false;
                    }
                    if (!string.IsNullOrEmpty(activity.LargeImageText))
                    {
                        if (!aFirst) sb.Append(",");
                        sb.Append("\"large_text\":\"").Append(EscapeString(activity.LargeImageText)).Append("\"");
                        aFirst = false;
                    }
                    if (!string.IsNullOrEmpty(activity.SmallImageKey))
                    {
                        if (!aFirst) sb.Append(",");
                        sb.Append("\"small_image\":\"").Append(EscapeString(activity.SmallImageKey)).Append("\"");
                        aFirst = false;
                    }
                    if (!string.IsNullOrEmpty(activity.SmallImageText))
                    {
                        if (!aFirst) sb.Append(",");
                        sb.Append("\"small_text\":\"").Append(EscapeString(activity.SmallImageText)).Append("\"");
                    }
                    sb.Append("}");
                    first = false;
                }

                if (!first) sb.Append(",");
                sb.Append("\"type\":").Append(activity.Type);

                sb.Append("}}");
            }

            sb.Append(",\"nonce\":\"").Append(EscapeString(nonce)).Append("\"}");
            return sb.ToString();
        }

        public static string EscapeString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            var sb = new StringBuilder(str.Length + 10);
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        public static string UnescapeString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            var sb = new StringBuilder(str.Length);
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '\\' && i + 1 < str.Length)
                {
                    i++;
                    char next = str[i];
                    switch (next)
                    {
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case '/': sb.Append('/'); break;
                        case 'u':
                            if (i + 4 < str.Length)
                            {
                                string hex = str.Substring(i + 1, 4);
                                int code;
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out code))
                                {
                                    sb.Append((char)code);
                                    i += 4;
                                    break;
                                }
                            }
                            sb.Append("\\u");
                            break;
                        default:
                            sb.Append(next);
                            break;
                    }
                }
                else
                {
                    sb.Append(str[i]);
                }
            }
            return sb.ToString();
        }

        public static string ExtractString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return null;

            string pattern = "\"" + key + "\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx == -1)
                return null;

            int colonIdx = json.IndexOf(':', idx + pattern.Length);
            if (colonIdx == -1)
                return null;

            int startQuote = -1;
            for (int i = colonIdx + 1; i < json.Length; i++)
            {
                if (char.IsWhiteSpace(json[i]))
                    continue;

                if (json[i] == '"')
                {
                    startQuote = i;
                    break;
                }
                else
                {
                    // Not a string (null, number, object, etc.)
                    return null;
                }
            }

            if (startQuote == -1)
                return null;

            var sb = new StringBuilder();
            bool escape = false;
            for (int i = startQuote + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (escape)
                {
                    sb.Append(c);
                    escape = false;
                }
                else if (c == '\\')
                {
                    sb.Append(c);
                    escape = true;
                }
                else if (c == '"')
                {
                    return UnescapeString(sb.ToString());
                }
                else
                {
                    sb.Append(c);
                }
            }

            return null;
        }

        public static int? ExtractInt(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return null;

            string pattern = "\"" + key + "\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx == -1)
                return null;

            int colonIdx = json.IndexOf(':', idx + pattern.Length);
            if (colonIdx == -1)
                return null;

            int startNum = -1;
            for (int i = colonIdx + 1; i < json.Length; i++)
            {
                if (char.IsWhiteSpace(json[i]))
                    continue;

                if (char.IsDigit(json[i]) || json[i] == '-')
                {
                    startNum = i;
                    break;
                }
                else
                {
                    return null;
                }
            }

            if (startNum == -1)
                return null;

            int endNum = startNum;
            while (endNum < json.Length && (char.IsDigit(json[endNum]) || json[endNum] == '-'))
            {
                endNum++;
            }

            string numStr = json.Substring(startNum, endNum - startNum);
            int val;
            if (int.TryParse(numStr, out val))
            {
                return val;
            }

            return null;
        }

        public static string ExtractNestedString(string json, string parentKey, string childKey)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(parentKey) || string.IsNullOrEmpty(childKey))
                return null;

            string pattern = "\"" + parentKey + "\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx == -1)
                return null;

            int braceIdx = json.IndexOf('{', idx + pattern.Length);
            if (braceIdx == -1)
                return null;

            // Find matching closing brace
            int depth = 1;
            int closingBrace = -1;
            bool insideQuote = false;
            bool escape = false;

            for (int i = braceIdx + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\')
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    insideQuote = !insideQuote;
                    continue;
                }

                if (!insideQuote)
                {
                    if (c == '{') depth++;
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            closingBrace = i;
                            break;
                        }
                    }
                }
            }

            if (closingBrace == -1)
                return null;

            string subObject = json.Substring(braceIdx, closingBrace - braceIdx + 1);
            return ExtractString(subObject, childKey);
        }
    }
}

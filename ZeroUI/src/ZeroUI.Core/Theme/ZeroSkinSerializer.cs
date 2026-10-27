using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZeroUI.Core.Theme
{
    /// <summary>
    /// Zero-dependency JSON serializer and deserializer for ZeroSkin definitions.
    /// Provides persistence and dynamic skin loading from external .zeroskin.json files.
    /// Fully compatible across .NET Standard 2.0, .NET Framework 4.6.2, and .NET 8/9.
    /// </summary>
    public static class ZeroSkinSerializer
    {
        public static string ToJson(ZeroSkin skin, bool indented = true)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));

            var sb = new StringBuilder(1024);
            string nl = indented ? "\r\n" : "";
            string sp = indented ? "  " : "";
            string sp2 = indented ? "    " : "";

            sb.Append("{" + nl);
            sb.Append($"{sp}\"Name\": \"{EscapeString(skin.Name)}\",{nl}");
            sb.Append($"{sp}\"DisplayName\": \"{EscapeString(skin.DisplayName)}\",{nl}");
            sb.Append($"{sp}\"IsDark\": {(skin.IsDark ? "true" : "false")},{nl}");
            sb.Append($"{sp}\"Tokens\": {{{nl}");

            var t = skin.Tokens;
            sb.Append($"{sp2}\"BgPrimary\": \"{EscapeString(t.BgPrimary)}\",{nl}");
            sb.Append($"{sp2}\"BgCard\": \"{EscapeString(t.BgCard)}\",{nl}");
            sb.Append($"{sp2}\"BgInput\": \"{EscapeString(t.BgInput)}\",{nl}");
            sb.Append($"{sp2}\"BgHover\": \"{EscapeString(t.BgHover)}\",{nl}");
            sb.Append($"{sp2}\"BgActive\": \"{EscapeString(t.BgActive)}\",{nl}");
            sb.Append($"{sp2}\"BgDisabled\": \"{EscapeString(t.BgDisabled)}\",{nl}");
            sb.Append($"{sp2}\"BorderDefault\": \"{EscapeString(t.BorderDefault)}\",{nl}");
            sb.Append($"{sp2}\"BorderSubtle\": \"{EscapeString(t.BorderSubtle)}\",{nl}");
            sb.Append($"{sp2}\"BorderFocus\": \"{EscapeString(t.BorderFocus)}\",{nl}");
            sb.Append($"{sp2}\"TextPrimary\": \"{EscapeString(t.TextPrimary)}\",{nl}");
            sb.Append($"{sp2}\"TextSecondary\": \"{EscapeString(t.TextSecondary)}\",{nl}");
            sb.Append($"{sp2}\"TextMuted\": \"{EscapeString(t.TextMuted)}\",{nl}");
            sb.Append($"{sp2}\"PrimaryAccent\": \"{EscapeString(t.PrimaryAccent)}\",{nl}");
            sb.Append($"{sp2}\"PrimaryAccentDark\": \"{EscapeString(t.PrimaryAccentDark)}\",{nl}");
            sb.Append($"{sp2}\"SecondaryAccent\": \"{EscapeString(t.SecondaryAccent)}\",{nl}");
            sb.Append($"{sp2}\"Success\": \"{EscapeString(t.Success)}\",{nl}");
            sb.Append($"{sp2}\"Warning\": \"{EscapeString(t.Warning)}\",{nl}");
            sb.Append($"{sp2}\"Danger\": \"{EscapeString(t.Danger)}\",{nl}");
            sb.Append($"{sp2}\"Info\": \"{EscapeString(t.Info)}\",{nl}");
            sb.Append($"{sp2}\"SelectionBackground\": \"{EscapeString(t.SelectionBackground)}\",{nl}");
            sb.Append($"{sp2}\"SelectionForeground\": \"{EscapeString(t.SelectionForeground)}\",{nl}");
            sb.Append($"{sp2}\"ScrollThumb\": \"{EscapeString(t.ScrollThumb)}\",{nl}");
            sb.Append($"{sp2}\"ScrollThumbHover\": \"{EscapeString(t.ScrollThumbHover)}\",{nl}");
            sb.Append($"{sp2}\"ScrollTrack\": \"{EscapeString(t.ScrollTrack)}\"{nl}");

            sb.Append($"{sp}}}{nl}");
            sb.Append("}");

            return sb.ToString();
        }

        public static ZeroSkin FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON content is empty.", nameof(json));

            var skin = new ZeroSkin();
            var tokens = new ZeroPaletteTokens();
            skin.Tokens = tokens;

            var pairs = ParseKeyValuePairs(json);

            if (pairs.TryGetValue("name", out var name)) skin.Name = name;
            if (pairs.TryGetValue("displayname", out var displayName)) skin.DisplayName = displayName;
            if (pairs.TryGetValue("isdark", out var isDarkStr))
            {
                skin.IsDark = bool.TryParse(isDarkStr, out var isDark) && isDark;
            }

            if (pairs.TryGetValue("bgprimary", out var bgPri)) tokens.BgPrimary = bgPri;
            if (pairs.TryGetValue("bgcard", out var bgCard)) tokens.BgCard = bgCard;
            if (pairs.TryGetValue("bginput", out var bgInp)) tokens.BgInput = bgInp;
            if (pairs.TryGetValue("bghover", out var bgHov)) tokens.BgHover = bgHov;
            if (pairs.TryGetValue("bgactive", out var bgAct)) tokens.BgActive = bgAct;
            if (pairs.TryGetValue("bgdisabled", out var bgDis)) tokens.BgDisabled = bgDis;

            if (pairs.TryGetValue("borderdefault", out var bDef)) tokens.BorderDefault = bDef;
            if (pairs.TryGetValue("bordersubtle", out var bSub)) tokens.BorderSubtle = bSub;
            if (pairs.TryGetValue("borderfocus", out var bFoc)) tokens.BorderFocus = bFoc;

            if (pairs.TryGetValue("textprimary", out var tPri)) tokens.TextPrimary = tPri;
            if (pairs.TryGetValue("textsecondary", out var tSec)) tokens.TextSecondary = tSec;
            if (pairs.TryGetValue("textmuted", out var tMut)) tokens.TextMuted = tMut;

            if (pairs.TryGetValue("primaryaccent", out var pAcc)) tokens.PrimaryAccent = pAcc;
            if (pairs.TryGetValue("primaryaccentdark", out var pAccDark)) tokens.PrimaryAccentDark = pAccDark;
            if (pairs.TryGetValue("secondaryaccent", out var sAcc)) tokens.SecondaryAccent = sAcc;

            if (pairs.TryGetValue("success", out var suc)) tokens.Success = suc;
            if (pairs.TryGetValue("warning", out var war)) tokens.Warning = war;
            if (pairs.TryGetValue("danger", out var dan)) tokens.Danger = dan;
            if (pairs.TryGetValue("info", out var inf)) tokens.Info = inf;

            if (pairs.TryGetValue("selectionbackground", out var selBg)) tokens.SelectionBackground = selBg;
            if (pairs.TryGetValue("selectionforeground", out var selFg)) tokens.SelectionForeground = selFg;

            if (pairs.TryGetValue("scrollthumb", out var scTh)) tokens.ScrollThumb = scTh;
            if (pairs.TryGetValue("scrollthumbhover", out var scThHov)) tokens.ScrollThumbHover = scThHov;
            if (pairs.TryGetValue("scrolltrack", out var scTr)) tokens.ScrollTrack = scTr;

            return skin;
        }

        public static void SaveToFile(ZeroSkin skin, string filePath)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = ToJson(skin, true);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        public static ZeroSkin LoadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException($"Skin file '{filePath}' was not found.", filePath);

            string json = File.ReadAllText(filePath, Encoding.UTF8);
            return FromJson(json);
        }

        private static string EscapeString(string? str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str!.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static Dictionary<string, string> ParseKeyValuePairs(string json)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            int len = json.Length;

            while (i < len)
            {
                // Find key start quote
                int keyStart = json.IndexOf('"', i);
                if (keyStart < 0) break;
                int keyEnd = json.IndexOf('"', keyStart + 1);
                if (keyEnd < 0) break;

                string key = json.Substring(keyStart + 1, keyEnd - keyStart - 1).Trim();

                // Find colon
                int colon = json.IndexOf(':', keyEnd + 1);
                if (colon < 0) break;

                // Move past whitespace
                int valStart = colon + 1;
                while (valStart < len && (char.IsWhiteSpace(json[valStart]) || json[valStart] == '\r' || json[valStart] == '\n'))
                    valStart++;

                if (valStart >= len) break;

                if (json[valStart] == '"')
                {
                    // String value
                    int valEnd = json.IndexOf('"', valStart + 1);
                    if (valEnd < 0) break;
                    string val = json.Substring(valStart + 1, valEnd - valStart - 1);
                    dict[key] = val;
                    i = valEnd + 1;
                }
                else if (json[valStart] == '{')
                {
                    // Nested object (e.g. Tokens) -> skip open brace and continue parsing inner pairs
                    i = valStart + 1;
                }
                else
                {
                    // Primitive (boolean, number, etc.)
                    int commaOrBrace = json.IndexOfAny(new[] { ',', '}', '\r', '\n' }, valStart);
                    if (commaOrBrace < 0) commaOrBrace = len;
                    string val = json.Substring(valStart, commaOrBrace - valStart).Trim();
                    dict[key] = val;
                    i = commaOrBrace + 1;
                }
            }

            return dict;
        }
    }
}

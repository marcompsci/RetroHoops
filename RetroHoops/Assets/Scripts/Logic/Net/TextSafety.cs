using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 compliance gate (App Review 1.2): text that arrives from another player's phone (LIVE opponents found
    /// by Game Center, two-phone friends, watchers) and text in shared kit / court / clutch codes is passed through here
    /// before it's shown. It's a small on-device word filter, not a moderation service: it hides the common slurs and
    /// swear words (English and Spanish, with number-for-letter tricks and stretched letters) behind a neutral fallback,
    /// and strips rich-text tags so another phone can't restyle or flood the screen. The simulation never reads names, so
    /// filtering on both phones keeps lockstep games identical.
    /// </summary>
    public static class TextSafety
    {
        /// <summary>Longest peer-supplied name kept (characters).</summary>
        public const int MaxName = 24;
        /// <summary>Longest peer-supplied message kept (a reject reason).</summary>
        public const int MaxMessage = 160;

        // The list is ROT13 so the source (and its text audits) don't spell the words out. Modes:
        //   ^ a word starts with it, = a whole word, * anywhere (letters only, spaces removed).
        private const string Encoded =
            "^shpx ^fuvg ^ovgpu ^phag ^qvpxurnq ^juber ^fyhg ^onfgneq ^nffubyr ^snttbg ^ergneq ^encvfg ^anmv ^jrgonpx ^genaal ^cbea " +
            "^wvmm ^gjng ^jnax ^ornare ^enturnq ^gbjryurnq ^chgn ^zvreqn ^pnoeba ^craqrwb ^znevpba ^wbqre ^phyreb ^puvatn ^avtt " +
            "=qvpx =pbpx =chffl =snt =encr =xvxr =fcvp =puvax =qlxr =phz =xxx =tbbx =xlf =chgb =iretn =cravf =intvan " +
            "*zbgureshpx *pbpxfhpx *avttre *avttn *uvgyre";

        private struct Entry { public char Mode; public string Raw, Collapsed; }

        private static readonly List<Entry> Entries = Decode();

        private static List<Entry> Decode()
        {
            var list = new List<Entry>();
            foreach (var token in Encoded.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var sb = new StringBuilder(token.Length - 1);
                for (int i = 1; i < token.Length; i++)
                {
                    char ch = token[i];
                    sb.Append(ch >= 'a' && ch <= 'z' ? (char)('a' + (ch - 'a' + 13) % 26) : ch);
                }
                string raw = sb.ToString();
                list.Add(new Entry { Mode = token[0], Raw = raw, Collapsed = Collapse(raw) });
            }
            return list;
        }

        /// <summary>Letters only, lower case, accents dropped, common number / symbol stand-ins read as letters.</summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char c0 in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c0) == UnicodeCategory.NonSpacingMark) continue;
                char c = char.ToLowerInvariant(c0);
                switch (c)
                {
                    case '0': c = 'o'; break;
                    case '1': case '!': case '|': c = 'i'; break;
                    case '3': c = 'e'; break;
                    case '4': case '@': c = 'a'; break;
                    case '5': case '$': c = 's'; break;
                    case '7': case '+': c = 't'; break;
                    case '8': c = 'b'; break;
                }
                if (c >= 'a' && c <= 'z') sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Runs of one letter squeezed to one ("fuuuun" → "fun").</summary>
        public static string Collapse(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) if (sb.Length == 0 || sb[sb.Length - 1] != c) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>False when the text contains a word on the list.</summary>
        public static bool IsClean(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            var words = new List<string>();
            var joined = new StringBuilder();
            foreach (var part in text.Split(new[] { ' ', '-', '_', '.', ',', '/', '\'', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string n = Normalize(part);
                if (n.Length == 0) continue;
                words.Add(n);
                joined.Append(n);
            }
            string all = joined.ToString(), allC = Collapse(all);
            foreach (var e in Entries)
            {
                if (e.Mode == '*')
                {
                    // Squeezed letters only for words without doubles (so "Nigeria" or "Niger" stay clean).
                    if (all.Contains(e.Raw) || (e.Raw == e.Collapsed && allC.Contains(e.Collapsed))) return false;
                    continue;
                }
                foreach (var w in words)
                {
                    string wc = Collapse(w);
                    if (e.Mode == '=')
                    {
                        if (w == e.Raw || (e.Collapsed.Length >= 4 && wc == e.Collapsed)) return false;
                    }
                    else if (w.StartsWith(e.Raw, StringComparison.Ordinal) || (e.Collapsed.Length >= 4 && wc.StartsWith(e.Collapsed, StringComparison.Ordinal)))
                        return false;
                }
            }
            return true;
        }

        /// <summary>Removes rich-text tags and control characters and trims to <paramref name="max"/> characters.</summary>
        public static string Plain(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(Math.Min(s.Length, max));
            foreach (char c in s)
            {
                if (c == '<' || c == '>' || char.IsControl(c)) continue;
                sb.Append(c);
                if (sb.Length >= max) break;
            }
            return sb.ToString().Trim();
        }

        /// <summary>A name from another phone or a shared code: plain, short, and the fallback if it isn't clean or is empty.</summary>
        public static string Name(string s, string fallback)
        {
            string p = Plain(s, MaxName);
            return p.Length == 0 || !IsClean(p) ? fallback : p;
        }

        /// <summary>Like <see cref="Name"/> but an empty field stays empty (only text that isn't clean is replaced).</summary>
        public static string Field(string s, string fallback)
        {
            string p = Plain(s, MaxName);
            return IsClean(p) ? p : fallback;
        }

        /// <summary>A message from another phone (a reject reason): plain and clean, or a neutral line.</summary>
        public static string Message(string s)
        {
            string p = Plain(s, MaxMessage);
            return p.Length == 0 || !IsClean(p) ? "The other player couldn't join this game." : p;
        }
    }
}

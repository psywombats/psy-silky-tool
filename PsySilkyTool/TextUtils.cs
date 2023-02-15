using System;
using System.IO;
using System.Text;

public static class TextUtils 
{
    private const int EncodingShiftJIS = 932;
    private static Encoding shiftJIS = Encoding.GetEncoding(EncodingShiftJIS);

    public static string Utf8FromJisFile(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        return Utf8FromJisBytes(bytes);
    }
    public static string Utf8FromJisBytes(byte[] bytes)
    {
        var jisString = shiftJIS.GetString(bytes);
        return jisString;
    }
    public static bool IsCharFullWidth(char c)
    {
        var byteCount = shiftJIS.GetByteCount(c.ToString());
        return byteCount == 2 && c != '─';
    }

    public static string Quote(string str)
    {
        return '"' + str + '"';
    }

    public static string CleanQuotes(string a)
    {
        if (a.StartsWith("『"))
        {
            a = a.Substring(1);
        }
        if (a.EndsWith("』"))
        {
            a = a.Substring(0, a.Length - 1);
        }
        a = a.Trim();
        return a;
    }

    public static string CleanNametags(string a)
    {
        if (a.StartsWith("【"))
        {
            a = a.Substring(1);
        }
        if (a.EndsWith("】"))
        {
            a = a.Substring(0, a.Length - 1);
        }
        a = a.Trim();
        return a;
    }

    public static bool IsLineMatch(string a, string b)
    {
        if (a.Equals(b))
        {
            // exact match
            return true;
        }

        var dist = LevenshteinDistance(a, b);
        if (dist <= (int)Math.Ceiling(b.Length / 10f) || dist <= 2)
        {
            // one or two characters changed
            return true;
        }

        if (a.Replace(" ", "").Equals(b.Replace(" ", "")))
        {
            return true;
        }

        // check if there's a newline between matches
        foreach (var subA in a.Split('\n'))
        {
            if (subA.Equals(b)) return true;
        }
        foreach (var subB in b.Split('\n'))
        {
            if (subB.Equals(a)) return true;
        }

        return false;
    }

    public static string GetBetterMatch(string @in, string target)
    {
        var d1 = LevenshteinDistance(target, @in);
        if (target.IndexOf('\n') > 0 && target.IndexOf('\n') <= 6)
        {
            var sub = target.Substring(target.IndexOf('\n') + 1);
            var d2 = LevenshteinDistance(sub, @in);
            if (d2 < d1)
            {
                return sub;
            }
        }
        if (@in.IndexOf('\n') > 0 && @in.IndexOf('\n') <= 6)
        {
            var sub = @in.Substring(@in.IndexOf('\n') + 1);
            var d2 = LevenshteinDistance(sub, target);
            if (d2 < d1)
            {
                return sub;
            }
        }
        return @in;
    }

    /// <summary>
    /// Compute the distance between two strings.
    /// </summary>
    /// <remarks>shamelessly stolen from https://www.csharpstar.com/csharp-string-distance-algorithm/ </remarks>
    public static int LevenshteinDistance(string a, string t)
    {
        if (a.Equals(t)) return 0;
        int n = a.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0)
        {
            return m;
        }
        if (m == 0)
        {
            return n;
        }

        // Step 2
        for (int i = 0; i <= n; d[i, 0] = i++) { }

        for (int j = 0; j <= m; d[0, j] = j++) { }

        // Step 3
        for (int i = 1; i <= n; i++)
        {
            //Step 4
            for (int j = 1; j <= m; j++)
            {
                // Step 5
                int cost = (t[j - 1] == a[i - 1]) ? 0 : 1;

                // Step 6
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        // Step 7
        return d[n, m];
    }

    /// <summary>
    /// Attempts to split a string with two sentences into two strings each with one sentence.
    /// </summary>
    /// <param name="combined">The combined two-sentence string</param>
    /// <param name="punctuation">The punctuation mark to try to split with (usually '.')</param>
    /// <param name="a">The first sentence</param>
    /// <param name="b">The second sentence</param>
    /// <param name="allowMulti">True to split even a 2+ sentence into 1+ and 1 sentences</param>
    /// <returns>True if the split succeeded</returns>
    public static bool TrySplit(string combined, char punctuation, out string a, out string b, bool allowMulti = false)
    {
        var seperator = $"{punctuation} ";
        var index1 = combined.IndexOf(seperator);
        var index2 = combined.LastIndexOf(seperator);
        if ((!allowMulti && index1 != index2) || index2 < 0)
        {
            a = null;
            b = null;
            return false;
        }

        a = combined.Substring(0, index2 + 1);
        b = combined.Substring(index2 + 2, combined.Length - (index2 + 2));
        return true;
    }

    public static bool IsLetter(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }

    public static string QuoteLiterals(string a) => a.Replace("\"", "\\\"");

    public static bool IsAnyFullWidth(string s)
    {
        foreach (var c in s)
        {
            if (IsCharFullWidth(c))
            {
                return true;
            }
        }
        return false;
    }

    // just jp/en
    public static bool IsPunctuation(string s)
    {
        foreach (var c in s)
        {
            switch (c)
            {
                case '.':
                case '?':
                case '!':
                case '─':
                case '\'':
                case '"':
                    continue;
                default:
                    return false;
            }
        }
        return true;
    }
}

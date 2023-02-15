using System;
using System.Collections.Generic;

public class LogicalLine
{
    /// <summary>All of the lines, seperated with :\n, that make up this line</summary>
    public List<string> JpSublines { get; private set; } = new List<string>();

    /// <summary>The translations for each subline</summary>
    public List<string> EngSublines { get; private set; } = new List<string>();

    /// <summary>If there are multiple sublines, is there code between them preventing merging?</summary>
    public bool IsSplitForced { get; set; }

    public string CombinedJpLine => string.Join("", JpSublines);
    public string CombinedEngLine { get; private set; }
    public string ReferenceLine { get; private set; }

    private Script script;

    public LogicalLine(Script script)
    {
        this.script = script;
    }

    /// <summary>
    /// Attempt to apply the translation for the entire group of sublines from a combined EN TL.
    /// </summary>
    /// <remarks>
    /// This can fail if we can't automatically logically split the EN TL.
    /// </remarks>
    /// <param name="tl">The translation</param>
    /// <param name="refLine">The JP reference line from the TL file</param>
    /// <returns>True if applying it succeeded</returns>
    public bool ApplyTL(TranslationLine tl)
    {
        CombinedEngLine = tl.TranslatedLine;
        ReferenceLine = tl.ReferenceLine;
        var succeeded = false;

        if (JpSublines.Count == 1 && tl.ReferenceLines.Count > 1 && SublineMatches(tl))
        {
            EngSublines.Add(tl.TranslatedLine);
            return true;
        }

        if (tl.TranslatedLines.Count == JpSublines.Count)
        {
            EngSublines.AddRange(tl.TranslatedLines);
            succeeded = true;
        }
        else if (JpSublines.Count == 1 || !IsSplitForced)
        {
            EngSublines.Add(CombinedEngLine);
            succeeded = true;
        }
        else if (JpSublines.Count == 2)
        {
            succeeded = TextUtils.TrySplit(CombinedEngLine, '.', out var tl1, out var tl2);
            if (!succeeded) { succeeded = TextUtils.TrySplit(CombinedEngLine, '?', out tl1, out tl2); }
            if (!succeeded) { succeeded = TextUtils.TrySplit(CombinedEngLine, '!', out tl1, out tl2); }

            if (succeeded)
            {
                EngSublines.Add(tl1);
                EngSublines.Add(tl2);
                script.Tool.Warn($"{script}: Automatically splitting TL to match split JP line:\n" +
                    $"{JpSublines[0]}\n{tl1}\n{JpSublines[1]}\n{tl2}\n");
            }
        }
        if (!succeeded)
        {
            script.Tool.Error($"{script}: Couldn't break down line:\n{CombinedJpLine}\n" + $"" +
                $"A split is forced into {JpSublines.Count} parts, but EN can't be autosplit: {CombinedEngLine}\n");
            EngSublines.Add(CombinedEngLine);
            while (EngSublines.Count < JpSublines.Count)
            {
                EngSublines.Add("");
            }
            return false;
        }

        if (!SublineMatches(tl))
        {
            var dist = TextUtils.LevenshteinDistance(CombinedJpLine, ReferenceLine);
            if (dist > 2 && dist > Math.Ceiling(CombinedJpLine.Length * .1f))
            {
                script.Tool.Warn($"{script}: Source/reference line mismatch:\n" 
                    + $"{CombinedJpLine}\n{ReferenceLine}\n{CombinedEngLine}\n");
            }
        }

        return succeeded;
    }

    /// <summary>
    /// Applies a 1:1 match between each jp subline and en tl'd lines
    /// </summary>
    /// <returns>True if application succeeded</returns>
    public bool ApplyTLs(params TranslationLine[] tls)
    {
        if (tls.Length != JpSublines.Count)
        {
            script.Tool.Error($"{script}: line count mismatch: {CombinedJpLine}\n");
            return false;
        }

        for (var i = 0; i < tls.Length; i += 1)
        {
            var tl = tls[i];
            EngSublines.Add(tl.TranslatedLine);
            if (TextUtils.LevenshteinDistance(tls[i].ReferenceLine, JpSublines[i]) > Math.Ceiling(JpSublines[i].Length * .1) + 1)
            {
                script.Tool.Warn($"{script}: Source/reference line mismatch:\n{JpSublines[i]}\n{tls[i].ReferenceLine}\n");
            }
        }

        return true;
    }

    /// <summary>
    /// Applies matches for a 3-part jp line that's been split into 2 en lines
    /// </summary>
    /// <returns>True if application succeeded</returns>
    public bool ApplyThreePartTL(TranslationLine line1, TranslationLine line2)
    {
        if (JpSublines.Count != 3)
        {
            script.Tool.Error($"{script}: 3 line count mismatch: {CombinedJpLine}\n");
            return false;
        }

        string tl1, tl2 = null;
        var tl3 = line2.TranslatedLine;
        var succeeded = TextUtils.TrySplit(line1.TranslatedLine, '.', out tl1, out tl2);
        if (!succeeded) { succeeded = TextUtils.TrySplit(line1.TranslatedLine, '?', out tl1, out tl2); }
        if (!succeeded) { succeeded = TextUtils.TrySplit(line1.TranslatedLine, '!', out tl1, out tl2); }
        if (!succeeded) { tl1 = line1.TranslatedLine; }
        if (!succeeded) succeeded = TextUtils.TrySplit(line2.TranslatedLine, '.', out tl2, out tl3);
        if (!succeeded) { succeeded = TextUtils.TrySplit(line2.TranslatedLine, '?', out tl2, out tl3); }
        if (!succeeded) { succeeded = TextUtils.TrySplit(line2.TranslatedLine, '!', out tl2, out tl3); }

        // try again with multi on
        if (!succeeded) { tl3 = line2.TranslatedLine; }
        if (!succeeded) { succeeded = TextUtils.TrySplit(line1.TranslatedLine, '.', out tl1, out tl2, allowMulti: true); }
        if (!succeeded) { succeeded = TextUtils.TrySplit(line1.TranslatedLine, '?', out tl1, out tl2, allowMulti: true); }
        if (!succeeded) { succeeded = TextUtils.TrySplit(line1.TranslatedLine, '!', out tl1, out tl2, allowMulti: true); }
        if (!succeeded) { tl1 = line1.TranslatedLine; }
        if (!succeeded) succeeded = TextUtils.TrySplit(line2.TranslatedLine, '.', out tl2, out tl3, allowMulti: true);
        if (!succeeded) { succeeded = TextUtils.TrySplit(line2.TranslatedLine, '?', out tl2, out tl3, allowMulti: true); }
        if (!succeeded) { succeeded = TextUtils.TrySplit(line2.TranslatedLine, '!', out tl2, out tl3, allowMulti: true); }

        if (!succeeded)
        {
            var error = script + ": Source has multiple sublines with code inbetween, but TL can't be split:\n";
            foreach (var subline in JpSublines)
            {
                error += subline + "\n";
            }
            foreach (var subline in line1.TranslatedLines)
            {
                error += subline + "\n";
            }
            foreach (var subline in line2.TranslatedLines)
            {
                error += subline + "\n";
            }
            script.Tool.Error(error);
            return false;
        }

        EngSublines.Add(tl1);
        EngSublines.Add(tl2);
        EngSublines.Add(tl3);
        return true;
    }

    public override string ToString()
    {
        return CombinedJpLine;
    }

    private bool SublineMatches(TranslationLine tl)
    {
        foreach (var subline in tl.ReferenceLines)
        {
            if (CombinedJpLine.Equals(subline)) { return true; }
        }
        return false;
    }
}

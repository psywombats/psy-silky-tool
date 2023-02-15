using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class Script 
{
    public PsySilkyTool Tool { get; private set; }

    private FileInfo engFile, srcFile;
    
    private List<TranslationLine> tls = new List<TranslationLine>();
    private List<LogicalLine> lines = new List<LogicalLine>();

    public Script(PsySilkyTool tool, FileInfo engFile)
    {
        Tool = tool;
        this.engFile = engFile;
    }

    public override string ToString()
    {
        return engFile.Name;
    }

    /// <returns>True if processing was successful</returns>
    public bool Process()
    {
        foreach (var file in Tool.sourceDir.EnumerateFiles())
        {
            if (String.Equals(engFile.Name, file.Name, StringComparison.OrdinalIgnoreCase))
            {
                srcFile = file;
                break;
            }
        }

        if (srcFile == null)
        {
            Tool.Error($"Couldn't find corresponding script file for {engFile.Name}");
            return false;
        }

        if (!ReadEngLines())
        {
            return false;
        }
        if (!ReadSrcLines())
        {
            return false;
        }

        VerifyLines();
        
        if (!WriteLines())
        {
            return false;
        }


        return true;
    }

    /// <returns>True if no completed successfully</returns>
    private bool ReadEngLines()
    {
        var splitLines = TextUtils.Utf8FromJisFile(engFile.FullName).Split('\n');

        // the TL files are sometimes UTF8
        if (!splitLines[0].EndsWith("\r"))
        {
            splitLines = File.ReadAllLines(engFile.FullName);
        }

        var tlLine = new TranslationLine();
        for (var lineNo = 0; lineNo <= splitLines.Length; lineNo += 1)
        {
            string line = null;

            if (lineNo == splitLines.Length)
            {
                // cleanup
                line = "\\";
            }
            else
            {
                line = splitLines[lineNo].Trim();
                line = line.Replace("\\r・", "");
                line = line.Replace("\\r", "");
                line = line.Replace("\n", "");
                line = line.Replace("<", "");
                line = line.Replace(">", "");
                if (line.EndsWith("\\")) line = line.Substring(0, line.Length - 1);
                line = TextUtils.CleanQuotes(line);
            }

            // whitespace or garbage
            var isGarbage = line.Length == 0 || line.StartsWith(";") || line.StartsWith(":") || line.StartsWith("●")
                || (line.StartsWith("\\") && !line.StartsWith("\\r"));
            if (isGarbage && line.Length > 2 && tlLine.ReferenceLines.Count > 0 && !TextUtils.IsAnyFullWidth(line))
            {
                // accidental ; in front of an eng line
                isGarbage = false;
            }

            // did the block end?
            var terminateBlock = isGarbage && line.Length > 0 && line[0] == '\\';
            if (terminateBlock && (tlLine.ReferenceLines.Count > 0 || tlLine.TranslatedLines.Count > 0))
            {
                if (tlLine.TranslatedLine.Length == 0)
                {
                    // sometimes there's an erroneous newline before the eng appears
                    if (line.Length == 0 && lineNo < splitLines.Length)
                    {
                        var nextLine = splitLines[lineNo + 1];
                        if (nextLine.Length > 1 && !TextUtils.IsAnyFullWidth(nextLine))
                        {
                            continue;
                        }
                    }

                    // maybe it's punctuation
                    var wasPunctuation = true;
                    foreach (var c in tlLine.ReferenceLine)
                    {
                        if (c != '…' && c != '。')
                        {
                            wasPunctuation = false;
                            break;
                        }
                    }
                    if (wasPunctuation)
                    {
                        tlLine.TranslatedLines.Add("...");
                        tls.Add(tlLine);
                        tlLine = new TranslationLine();
                        continue;
                    }

                    // have we seen exactly this line before?
                    var found = false;
                    foreach (var tl in tls)
                    {
                        if (tl.ReferenceLine.Equals(tlLine.ReferenceLine))
                        {
                            tlLine.TranslatedLines.AddRange(tl.TranslatedLines);
                            tlLine = new TranslationLine();
                            found = true;
                        }
                    }
                    if (found)
                    {
                        tls.Add(tlLine);
                        continue;
                    }

                    Tool.Error($"{this}: No TL found on line {lineNo}:\n{tlLine.ReferenceLine}\n");
                    tlLine.TranslatedLines.Add($"({engFile.Name}::{lineNo} TL missing)");
                }
                else if (tlLine.ReferenceLines.Count == 0)
                {
                    Tool.Warn($"{this}: No reference line found for TL on {lineNo}:\n{tlLine.TranslatedLine}\n");
                    tlLine.ReferenceLines.Add($"({engFile.Name}::{lineNo} ref missing)");
                }
                else
                {
                    tls.Add(tlLine);
                    tlLine = new TranslationLine();
                }
            }

            if (isGarbage)
            {
                continue;
            }

            // is this a jp line?
            if (TextUtils.IsAnyFullWidth(line) || (TextUtils.IsPunctuation(line) && tlLine.ReferenceLines.Count == 0))
            {
                if (tlLine.TranslatedLines.Count > 0)
                {
                    // forgot to terminate the block
                    tls.Add(tlLine);
                    tlLine = new TranslationLine();

                }
                var hretIndex = line.IndexOf(":hret ");
                if (hretIndex != -1)
                {
                    line = line.Substring(0, hretIndex) + line.Substring(hretIndex + 11, line.Length - (hretIndex + 11));
                }
                tlLine.ReferenceLines.Add(line);
                continue;
            }

            // assume it's an EN line
            if (line.StartsWith("\\")) line = line.Substring(1);
            tlLine.TranslatedLines.Add(line);
        }

        return true;
    }

    private bool ReadSrcLines()
    {
        var splitLines = TextUtils.Utf8FromJisFile(srcFile.FullName).Split('\n');

        var logicalLine = new LogicalLine(this);
        var uncrypting = false;
        var continuing = false;
        var returning = false;
        var splitForced = false;
        var wasUncrypting = false;
        for (var lineNo = 0; lineNo < splitLines.Length; lineNo += 1)
        {
            var line = splitLines[lineNo].Trim();

            // is this an opcode?
            // #1-STR_UNCRYPT
            if (line.StartsWith("#") && line.Length > 2)
            {
                var opcode = line.Substring(3);
                if (opcode == "STR_UNCRYPT")
                {
                    uncrypting = true;
                    if (!continuing && !returning && logicalLine.JpSublines.Count > 0)
                    {
                        // end block
                        lines.Add(logicalLine);
                        logicalLine = new LogicalLine(this);
                        logicalLine.IsSplitForced = splitForced;
                    }
                    continuing = false;
                    splitForced = false;
                    returning = false;
                }
                else if (opcode == "TO_NEW_STRING")
                {
                    // continuation of previous line
                    if (logicalLine.JpSublines.Count > 0)
                    {
                        continuing = true;
                    }
                }
                else if (opcode == "RETURN")
                {
                    // newline manually from script
                    if (wasUncrypting)
                    {
                        returning = true;
                    }
                }
                else if (opcode == "MESSAGE")
                {
                    // end block
                    if (logicalLine.JpSublines.Count > 0)
                    {
                        lines.Add(logicalLine);
                        logicalLine = new LogicalLine(this);
                    }
                    continuing = false;
                    splitForced = false;
                    returning = false;
                }
                else
                {
                    // other op
                    if (continuing)
                    {
                        // there is control code between two halves of a line
                        logicalLine.IsSplitForced = true;
                    }
                    returning = false;
                    wasUncrypting = false;
                }
            }

            // is this an arg (that we care about?)
            // ["『５班か。どういうメンバーがいるんだろう。緊張する』"]
            if (uncrypting && line.StartsWith("["))
            {
                var text = line.Substring(2, line.Length - 4);
                text = text.Replace("\\r", "");
                text = TextUtils.CleanQuotes(text);
                if (text != "・")
                {
                    logicalLine.JpSublines.Add(text);
                }
                uncrypting = false;
                wasUncrypting = true;
            }
        }


        // last block
        if (logicalLine.JpSublines.Count > 0)
        {
            // end block
            lines.Add(logicalLine);
            logicalLine = new LogicalLine(this);
            logicalLine.IsSplitForced = splitForced;
        }

        return true;
    }
    
    /// <returns>True if all lines have corresponding translations</returns>
    private bool VerifyLines()
    {
        var errorCount = 0;

        var refDict = new Dictionary<string, TranslationLine>();
        foreach (var tl in tls)
        {
            refDict[tl.ReferenceLine] = tl;
        }

        var tlIndex = -1;
        for (var i = 0; i < lines.Count; i += 1)
        {
            tlIndex += 1;
            var line = lines[i];

            // exact local match
            TranslationLine localTL = null;
            if (tlIndex < tls.Count)
            {
                localTL = tls[tlIndex];
                if (line.CombinedJpLine.Equals(localTL.ReferenceLine))
                {
                    errorCount += line.ApplyTL(localTL) ? 1 : 0;
                    continue;
                }
            }

            // exact match from elsewhere in the script (reordering)
            if (refDict.TryGetValue(line.CombinedJpLine, out var exactTL))
            {
                errorCount += line.ApplyTL(exactTL) ? 1 : 0;
                continue;
            }

            // do we have multiple EN lines to cover one logical JP line?
            if (tlIndex + 1 < tls.Count && line.JpSublines.Count == 2)
            {
                var nextTL = tls[tlIndex + 1];
                var dist1 = TextUtils.LevenshteinDistance(line.JpSublines[0], localTL.ReferenceLine);
                var dist2 = TextUtils.LevenshteinDistance(line.JpSublines[1], nextTL.ReferenceLine);
                var target = Math.Ceiling(line.CombinedJpLine.Length * .1) + 1;
                if (dist1 <= target && dist2 <= target)
                {
                    tlIndex += 1;
                    line.ApplyTLs(localTL, nextTL);
                    continue;
                }
            }

            // same check in triplicate
            if (tlIndex + 2 < tls.Count && line.JpSublines.Count == 3)
            {
                var tl2 = tls[tlIndex + 1];
                var tl3 = tls[tlIndex + 2];
                var dist1 = TextUtils.LevenshteinDistance(line.JpSublines[0], localTL.ReferenceLine);
                var dist2 = TextUtils.LevenshteinDistance(line.JpSublines[1], tl2.ReferenceLine);
                var dist3 = TextUtils.LevenshteinDistance(line.JpSublines[2], tl3.ReferenceLine);
                var target = Math.Ceiling(line.CombinedJpLine.Length * .1) + 1;
                if (dist1 <= target && dist2 <= target && dist3 < target)
                {
                    tlIndex += 2;
                    errorCount += line.ApplyTLs(localTL, tl2, tl3) ? 1 : 0;
                    continue;
                }
            }

            // do we need to cover a 3-part jp line?
            if (tlIndex + 1 < tls.Count && line.JpSublines.Count == 3)
            {
                var nextTL = tls[tlIndex + 1];
                tlIndex += 1;
                errorCount += line.ApplyThreePartTL(localTL, nextTL) ? 1 : 0;
                continue;
            }

            // try to find a better match somewhere else
            TranslationLine bestTL = null;
            var bestDist = Math.Ceiling(line.CombinedJpLine.Length * .1f) + 1;
            var tryLine = line.CombinedJpLine;
            foreach (var tl in tls)
            {
                var dist = TextUtils.LevenshteinDistance(tryLine, tl.ReferenceLine);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTL = tl;
                    if (dist == 0)
                    {
                        break;
                    }
                }
            }
            if (bestTL != null)
            {
                errorCount += line.ApplyTL(bestTL) ? 1 : 0;
                continue;
            }

            // was this line reordered + split? ugh
            if (line.JpSublines.Count > 0)
            {
                var foundTLs = new TranslationLine[line.JpSublines.Count];
                foreach (var tl in tls)
                {
                    for (var j = 0; j < line.JpSublines.Count; j += 1)
                    {
                        var subline = line.JpSublines[j];
                        if (subline.Equals(tl.ReferenceLine))
                        {
                            foundTLs[j] = tl;
                            break;
                        }
                    }
                }
                if (foundTLs.All(tl => tl != null))
                {
                    line.ApplyTLs(foundTLs);
                    continue;
                }
            }

            // see if the immediate neighbors are better
            bestDist = TextUtils.LevenshteinDistance(line.CombinedJpLine, localTL.ReferenceLine);
            for (var j = tlIndex - 1; j <= tlIndex + 1; j += 1)
            {
                if (j == -1 || j >= tls.Count) continue;
                foreach (var subline in tls[j].ReferenceLines)
                {
                    var dist = TextUtils.LevenshteinDistance(line.CombinedJpLine, subline);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        localTL = tls[j];
                    }
                }
            }

            // inexact local match
            if (localTL != null)
            {
                errorCount += line.ApplyTL(localTL) ? 1 : 0;
            }
            else
            {
                errorCount += 1;
                Tool.Error($"{this}: No TL match found for {line.CombinedJpLine}\n");
            }
        }

        return errorCount == 0;
    }

    private bool WriteLines()
    {
        var splitLines = TextUtils.Utf8FromJisFile(srcFile.FullName).Split('\n');
        var errorCount = 0;

        var outPath = $"{Tool.outDir.FullName}/{srcFile.Name}";
        if (File.Exists(outPath))
        {
            File.Delete(outPath);
        }
        var outWriter = new StreamWriter(File.OpenWrite(outPath));
        
        var squashedLineCount = 0;
        var logicalLineNo = 0;
        var logicalLineSubindex = 0;
        for (var lineNo = 0; lineNo < splitLines.Length - 1 && errorCount < 10; lineNo += 2)
        {
            var opLine = splitLines[lineNo].Trim();
            var dataLine = splitLines[lineNo + 1].Trim();
            var dataOutLine = dataLine;

            if (opLine.StartsWith("#2") || opLine.StartsWith("#3"))
            {
                // this is a label, not an op
                lineNo -= 1;
                outWriter.WriteLine(opLine);
                continue;
            }

            LogicalLine logicalLine = null;
            if (logicalLineNo < lines.Count)
            {
                logicalLine = lines[logicalLineNo];
            }
            
            // #1-STR_UNCRYPT
            var opcode = opLine.Substring(3);
            if (opcode == "TO_NEW_STRING")
            {
                if (squashedLineCount > 0)
                {
                    // this jp line has been compressed into the previous line
                    continue;
                }
            }
            if (opcode == "STR_UNCRYPT")
            {
                if (squashedLineCount > 0)
                {
                    // skip the weird dots for free
                    if (dataLine != "[\"・\"]")
                    {
                        squashedLineCount -= 1;
                    }
                    continue;
                }

                // ["『５班か。どういうメンバーがいるんだろう。緊張する』"]
                var text = dataLine.Substring(2, dataLine.Length - 4);
                text = text.Replace("\\r", "");
                text = TextUtils.CleanQuotes(text);
                if (text == "・")
                {
                    dataOutLine = "[\"\"]";
                }
                else
                {
                    if (logicalLine == null)
                    {
                        Tool.Error($"{this}: Exhaused logical lines before EOF");
                        return false;
                    }

                    var jpSubline = logicalLine.JpSublines[logicalLineSubindex];
                    if (jpSubline.Equals(text))
                    {
                        var engSubline = "";
                        if (logicalLineSubindex < logicalLine.EngSublines.Count)
                        {
                            engSubline = logicalLine.EngSublines[logicalLineSubindex];
                        }
                        else
                        {
                            Tool.Error($"{this}: no corresponding ENG line for {jpSubline}");
                        }

                        dataOutLine = $"[\"{TextUtils.QuoteLiterals(engSubline)}\"]";
                        if (logicalLineSubindex == logicalLine.JpSublines.Count - 1)
                        {
                            logicalLineSubindex = 0;
                            logicalLineNo += 1;
                        }
                        else if (logicalLineSubindex == logicalLine.EngSublines.Count - 1)
                        {
                            // we have one EN line for a multipart JP line
                            // we want to skip the next TO_NEW_STRING and STR_UNCRYPT ops
                            squashedLineCount = logicalLine.JpSublines.Count - logicalLine.EngSublines.Count;
                            logicalLineSubindex = 0;
                            logicalLineNo += 1;
                        }
                        else
                        {
                            logicalLineSubindex += 1;
                        }
                    }
                    else
                    {
                        Tool.Error($"{this}: Logical lines don't match .mes on line {lineNo}");
                        Tool.Log($"Have: {text}");
                        Tool.Log($"Expected: {jpSubline}\n");
                        errorCount += 1;
                    }
                }
            }
            else if (opcode == "PUSH_STR")
            {
                if (dataLine.StartsWith("[\"【"))
                {
                    // it's a name
                    var jpName = dataLine.Substring(3, dataLine.Length - 6);
                    if (Tool.NameTable.TryGetValue(jpName, out var engName))
                    {
                        dataOutLine = $"[\"{TextUtils.QuoteLiterals(engName)}\"]";
                    }
                    else if (!Tool.UnseenNames.Contains(jpName))
                    {
                        Tool.UnseenNames.Add(jpName);
                        Tool.Warn($"{this}: Unknown name {jpName} on line {lineNo}");
                    }
                }
                else if (TextUtils.IsAnyFullWidth(dataLine))
                {
                    // I don't know wtf this is but writing full width charas will blow it up
                    // TODO i guess
                    dataOutLine = $"[\"pushed str line {lineNo}\"]";
                }
            }

            var invalid = false;
            foreach (var c in opLine)
            {
                if (TextUtils.IsCharFullWidth(c))
                {
                    Tool.Error($"{this}: can't write line because it contains full-width characters:");
                    Tool.Log(opLine);
                    invalid = true;
                    break;
                }
            }
            foreach (var c in dataOutLine)
            {
                if (TextUtils.IsCharFullWidth(c))
                {
                    Tool.Error($"{this}: can't write line because it contains full-width characters:");
                    Tool.Log(dataOutLine);
                    invalid = true;
                    break;
                }
            }

            if (invalid)
            {
                errorCount += 1;
                continue;
            }
            outWriter.WriteLine(opLine);
            outWriter.WriteLine(dataOutLine);
        }

        outWriter.WriteLine("");
        outWriter.Close();

        return errorCount == 0;
    }
}

using System;
using System.Collections.Generic;
using System.IO;

public class PsySilkyTool
{
    private StreamWriter logStream;

    public DirectoryInfo sourceDir { get; private set; }
    public DirectoryInfo engDir { get; private set; }
    public DirectoryInfo outDir { get; private set; }

    public HashSet<string> UnseenNames { get; private set; } = new HashSet<string>();
    public Dictionary<string, string> NameTable { get; private set; } = new Dictionary<string, string>();

    static void Main(string[] args)
    {
        var tool = new PsySilkyTool(args);
        tool.Run();
        tool.Close();
    }

    private PsySilkyTool(string[] args)
    {
        logStream = new StreamWriter(File.Open("log.txt", FileMode.Create));

        if (args.Length != 3)
        {
            Log("Usage: psysilkytool [src dir] [eng dir] [out dir]");
            return;
        }

        sourceDir = new DirectoryInfo(args[0]);
        engDir = new DirectoryInfo(args[1]);
        outDir = new DirectoryInfo(args[2]);
    }

    public void Run()
    {
        if (sourceDir == null)
        {
            // initialization failed
            return;
        }

        if (!sourceDir.Exists)
        {
            Error($"Cannot find script source directory {sourceDir.FullName}");
            return;
        }
        if (!engDir.Exists)
        {
            Error($"Cannot find English script directory {engDir.FullName}");
            return;
        }
        outDir.Create();

        ReadNameTable();

        foreach (var engFile in engDir.EnumerateFiles())
        {
            ProcessFile(engFile);
        }

        Log("Run complete!");
    }

    public void Log(string line)
    {
        System.Diagnostics.Trace.WriteLine(line);
        logStream.WriteLine(line);
    }
    public void Error(string line) => Log($"[ERROR] {line}");
    public void Warn(string line) => Log($"[WARNING] {line}");

    public void Close()
    {
        logStream.Close();
    }

    private void ProcessFile(FileInfo engFile)
    {
        if (engFile.Extension != ".txt")
        {
            Log($"Skipping {engFile.Name}-- not a .txt");
            return;
        }
        if (engFile.Name == "nametable.txt")
        {
            return;
        }

        var script = new Script(this, engFile);
        script.Process();
    }

    private void ReadNameTable()
    {
        var nameTablePath = engDir.FullName + "/nametable.txt";
        if (!File.Exists(nameTablePath))
        {
            Warn($"Can't find name table: {nameTablePath}");
        }
        
        var lines = File.ReadAllLines(nameTablePath);
        foreach (var line in lines)
        {
            var parts = line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                NameTable[parts[0]] = parts[1];
                
            }
            else if (parts.Length == 1)
            {
                Warn($"Need a TL for name {parts[0]}");
                NameTable[parts[0]] = parts[0];
            }
            else
            {
                Error($"Malformatted name table line: {line}");
            }
        }
    }
}

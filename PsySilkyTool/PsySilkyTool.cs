using System;
using System.IO;

public class PsySilkyTool
{
    private StreamWriter logStream;

    public DirectoryInfo sourceDir { get; private set; }
    public DirectoryInfo engDir { get; private set; }
    public DirectoryInfo outDir { get; private set; }

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

        var script = new Script(this, engFile);
        script.Process();
    }
}

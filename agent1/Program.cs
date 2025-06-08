using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;

class Agent1
{
    static string pipeName = "agent1";
    static string directoryPath;
    static Dictionary<string, Dictionary<string, int>> fileWordCounts = new();

    static void Main(string[] args)
    {
        directoryPath = GetDirectoryPathFromUser();

        if (directoryPath == null)
        {
            Console.WriteLine("No valid directory provided. Exiting.");
            WaitToExit();
            return;
        }

        ProcessAffinity.SetAffinity(0);

        var readThread = new Thread(ReadFiles);
        var sendThread = new Thread(SendData);

        readThread.Start();
        sendThread.Start();

        readThread.Join();
        sendThread.Join();

        WaitToExit();
    }

    static string GetDirectoryPathFromUser()
    {
        while (true)
        {
            Console.Write("Enter path which contains .txt files: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Try again or type 'exit' to quit.");
                continue;
            }

            if (input.Trim().ToLower() == "exit")
                return null;

            if (!Directory.Exists(input))
            {
                Console.WriteLine($"Directory does not exist: {input}");
                continue;
            }

            var txtFiles = Directory.GetFiles(input, "*.txt");
            if (txtFiles.Length == 0)
            {
                Console.WriteLine("Try another path. Can not find .txt files found.");
                continue;
            }

            return input;
        }
    }

    static void ReadFiles()
    {
        var files = Directory.GetFiles(directoryPath, "*.txt");
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var words = text.Split(new char[] { ' ', '\r', '\n', '\t', ',', '.', ';', ':', '-', '!', '?', '\"', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
            var wordCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var word in words)
            {
                if (wordCount.ContainsKey(word))
                    wordCount[word]++;
                else
                    wordCount[word] = 1;
            }
            lock (fileWordCounts)
            {
                fileWordCounts[Path.GetFileName(file)] = wordCount;
            }
        }
    }

    static void SendData()
    {
        using (var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.Out))
        {
            Console.WriteLine($"Agent1 connecting to pipe '{pipeName}'...");
            pipeClient.Connect();

            using (var writer = new StreamWriter(pipeClient))
            {
                writer.AutoFlush = true;

                while (true)
                {
                    lock (fileWordCounts)
                    {
                        if (fileWordCounts.Count > 0)
                        {
                            foreach (var kvp in fileWordCounts)
                            {
                                var fileName = kvp.Key;
                                var wordCounts = kvp.Value;
                                foreach (var wc in wordCounts)
                                {
                                    writer.WriteLine($"{fileName}|{wc.Key}|{wc.Value}");
                                }
                            }
                            break;
                        }
                    }
                    Thread.Sleep(100);
                }
            }
        }
    }

    static void WaitToExit()
    {
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}

static class ProcessAffinity
{
    public static void SetAffinity(int core)
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var mask = 1 << core;
            process.ProcessorAffinity = (IntPtr)mask;
            Console.WriteLine($"Process affinity set to CPU core {core}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to set processor affinity: {e.Message}");
        }
    }
}

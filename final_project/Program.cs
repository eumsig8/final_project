using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;

class Master
{
    static string pipeName1;
    static string pipeName2;

    static ConcurrentDictionary<string, ConcurrentDictionary<string, int>> aggregatedData = new();

    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("No pipe names provided, using defaults: 'agent1' and 'agent2'");
            pipeName1 = "agent1";
            pipeName2 = "agent2";
        }
        else
        {
            pipeName1 = args[0];
            pipeName2 = args[1];
        }

        ProcessAffinity.SetAffinity(2);

        var thread1 = new Thread(() => ListenPipe(pipeName1));
        var thread2 = new Thread(() => ListenPipe(pipeName2));

        thread1.Start();
        thread2.Start();

        thread1.Join();
        thread2.Join();

        PrintAggregatedData();

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static void ListenPipe(string pipeName)
    {
        using (var server = new NamedPipeServerStream(pipeName, PipeDirection.In))
        {
            Console.WriteLine($"Waiting for connection on pipe '{pipeName}'...");
            server.WaitForConnection();
            Console.WriteLine($"Connected to pipe '{pipeName}'.");

            using (var reader = new StreamReader(server))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split('|');
                    if (parts.Length != 3)
                        continue;

                    var fileName = parts[0];
                    var word = parts[1];
                    if (!int.TryParse(parts[2], out int count))
                        continue;

                    var wordDict = aggregatedData.GetOrAdd(fileName, new ConcurrentDictionary<string, int>());
                    wordDict.AddOrUpdate(word, count, (k, old) => old + count);
                }
            }
        }
    }

    static void PrintAggregatedData()
    {
        foreach (var fileEntry in aggregatedData)
        {
            var fileName = fileEntry.Key;
            var wordDict = fileEntry.Value;

            foreach (var wordEntry in wordDict)
            {
                Console.WriteLine($"{fileName}:{wordEntry.Key}:{wordEntry.Value}");
            }
        }
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

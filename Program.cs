
using System.Diagnostics;

Sandbox.Run();

static class Sandbox
{
    const int ThreadCount = 8;
    const int Iterations = 1_000_000;
    const int Expected = ThreadCount * Iterations;
    const int Limit = 100_000;

    static int _counter;
    static volatile int _volatileCounter;
    static long _casRetries;
    static readonly object _sync = new();

    public static void Run()
    {
        Console.WriteLine($"Ядер: {Environment.ProcessorCount}. Потоков: {ThreadCount} x {Iterations:N0} итераций\n");
        Console.WriteLine("=== 1. Гонка: read-modify-write без защиты ===");
        
        Check("_counter++", Expected, () =>
        {
            _counter = 0;
            RunOnThreads(() => _counter++);
            return _counter;
        });

        Check("volatile _counter++  (не спасает!)", Expected, () =>
        {
            _volatileCounter = 0;
            RunOnThreads(() => _volatileCounter++);
            return _volatileCounter;
        });

    }

    static void Check(string name, int expected, Func<int> test)
    {
        var sw = Stopwatch.StartNew();
        int actual = test();
        sw.Stop();

        string verdict = actual == expected ? "OK" : "FAIL";
        Console.WriteLine(
            $"[{verdict}] {name, -40} {actual, 12:N0} / {expected:N0}" +
            $" (разница {actual - expected, +10:N0}) {sw.ElapsedMilliseconds, 5} мс"
        );
    }
    
    static void RunOnThreads(Action body)
    {
        using var start = new ManualResetEventSlim(false);
        var threads = new Thread[ThreadCount];

        for (int t = 0; t < ThreadCount; t++)
        {
            threads[t] = new Thread(() =>
            {
                start.Wait();
                for (int i = 0; i < Iterations; i++) body();
            });
            threads[t].Start();
        }

        start.Set();
        foreach (var th in threads) th.Join();
    }
}
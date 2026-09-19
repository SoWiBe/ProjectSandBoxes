
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
        
        // broken
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

        //fix this
        Console.WriteLine("Fixing...");
        Check("lock", Expected, () =>
        {
            _counter = 0;
            RunOnThreads(() => {
                lock (_sync)
                {
                    _counter++;
                } 
            });
            return _counter;
        });
        
        Check("Interlocked.Increment", Expected, () =>
        {
            _counter = 0;
            RunOnThreads(() => Interlocked.Increment(ref _counter));
            return _counter;
        });
        
        // custom increment
        Console.WriteLine("Custom realisation with CAS...");
        _casRetries = 0;
        Check("CasIncrement", Expected, () =>
        {
            _counter = 0;
            RunOnThreads(() => CasIncrement(ref _counter));
            return _counter;
        });
        
        // check - then - act: interlocked does not save multiple operation
        Console.WriteLine("Counter with limit");
        
        Check("if (x < limit) Interlocked.Increment", Limit, () =>
        {
            _counter = 0;
            RunOnThreads(() => NaiveIncrementBelow(ref _counter, Limit));
            return _counter;
        });
        
        Check("TryIncrementBelow (CAS)", Limit, () =>
        {
            _counter = 0;
            RunOnThreads(() => TryIncrementBelow(ref _counter, Limit));
            return _counter;
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

    static int CasIncrement(ref int location) // -> _counter
    {
        while (true)
        {
            int current = Volatile.Read(ref location);
            int next = current + 1;

            if (Interlocked.CompareExchange(ref location, next, current) == current)
                return next;
            
            Interlocked.Increment(ref _casRetries);
        }
    }
    
    // проверка и инкремент по отдельности атомарны, а вместе - нет
    // несколько потоков видят число, все проходят if, все инкремент -> перелет
    static void NaiveIncrementBelow(ref int location, int limit)
    {
        if (Volatile.Read(ref location) < limit)
            Interlocked.Increment(ref location);
    }

    static bool TryIncrementBelow(ref int location, int limit)
    {
        var current = Volatile.Read(ref location);
        while (current < limit)
        {
            var seen = Interlocked.CompareExchange(ref location, current + 1, current);
            if (seen == current) return true;
            current = seen;
        }

        return false;
    }
}
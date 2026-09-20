namespace InterlockedSandBox.Threads;

public static class MonitorSandBox
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    
    public static async Task Run()
    {
        var lockA = new object();
        var lockB = new object();

        var t1 = Task.Run(async () =>
        {
            await Gate.WaitAsync();
            try
            {
                Console.WriteLine("t1 взял A");
                Thread.Sleep(200);
                Console.WriteLine("t1 ждет B...");
                lock (lockB)
                {
                    Console.WriteLine("t1 взял B");
                }
            }
            finally
            {
                Gate.Release();
            }
        });

        var t2 = Task.Run(() =>
        {
            if (Monitor.TryEnter(lockB, TimeSpan.FromSeconds(1)))
            {
                try
                {
                    Console.WriteLine();
                    Thread.Sleep(100);
                }
                finally
                {
                    Monitor.Exit(lockB);
                }
            }
        });

        await Task.WhenAll(t1, t2);
    }

    public static async Task CrashMonitor()
    {
        var sync = new object();
        Monitor.Enter(sync);
        await SimpleTask();
        Monitor.Exit(sync);
    }

    private static async Task SimpleTask()
    {
        await Task.Delay(100);
    }
}
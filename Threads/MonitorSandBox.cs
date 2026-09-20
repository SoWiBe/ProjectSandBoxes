namespace InterlockedSandBox.Threads;

public static class MonitorSandBox
{
    private static async Task Run()
    { 
        var lockA = new object();
        var lockB = new object();

        var t1 = Task.Run(() =>
        {
            lock (lockA)
            {
                Console.WriteLine("t1 взял A");
                Thread.Sleep(200); // даем второму потоку взять B 
                Console.WriteLine("t1 ждет B...");
                lock (lockB)
                {
                    Console.WriteLine("t1 взял B"); // никогда не попадем
                }
            }
        });

        var t2 = Task.Run(() =>
        {
            lock (lockB)
            {
                Console.WriteLine();
                Thread.Sleep(100);
                Console.WriteLine();
                lock (lockA)
                {
                    Console.WriteLine();
                }
            }
        });

        await Task.WhenAll(t1, t2);
    }
}
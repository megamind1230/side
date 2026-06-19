using Serilog;
using Spectre.Console;

namespace DotaDraftAdvisor.Demo;

public class DeadlockSection
{
    private readonly object _allyPoolLock = new();
    private readonly object _enemyPoolLock = new();

    public void Run()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]═══ DEADLOCK DEMO ═══[/]");
        AnsiConsole.MarkupLine("Two threads acquire resources in different order:");
        AnsiConsole.MarkupLine("  [red]Thread 1:[/] lock AllyPool → lock EnemyPool");
        AnsiConsole.MarkupLine("  [red]Thread 2:[/] lock EnemyPool → lock AllyPool");
        AnsiConsole.WriteLine();

        BrokenPass();
        FixedPass();

        AnsiConsole.MarkupLine("[bold yellow]═══ END DEADLOCK DEMO ═══[/]");
        AnsiConsole.WriteLine();
    }

    private void BrokenPass()
    {
        AnsiConsole.MarkupLine("[red]--- Broken pass (opposite lock order) ---[/]");

        var barrier = new Barrier(2);
        bool t1Deadlocked = false;
        bool t2Deadlocked = false;

        var t1 = Task.Run(() =>
        {
            Log.Information("DeadlockDemo: Thread 1 locking AllyPool");
            lock (_allyPoolLock)
            {
                Log.Information("DeadlockDemo: Thread 1 acquired AllyPool, syncing...");
                barrier.SignalAndWait();

                Thread.Sleep(200);

                Log.Information("DeadlockDemo: Thread 1 trying EnemyPool...");
                if (Monitor.TryEnter(_enemyPoolLock, TimeSpan.FromSeconds(3)))
                {
                    Log.Information("DeadlockDemo: Thread 1 acquired EnemyPool (no deadlock)");
                    Thread.Sleep(100);
                    Monitor.Exit(_enemyPoolLock);
                }
                else
                {
                    t1Deadlocked = true;
                    Log.Warning("DeadlockDemo: Thread 1 deadlocked — holds AllyPool, waiting for EnemyPool");
                }
            }
        });

        var t2 = Task.Run(() =>
        {
            Log.Information("DeadlockDemo: Thread 2 locking EnemyPool");
            lock (_enemyPoolLock)
            {
                Log.Information("DeadlockDemo: Thread 2 acquired EnemyPool, syncing...");
                barrier.SignalAndWait();

                Thread.Sleep(200);

                Log.Information("DeadlockDemo: Thread 2 trying AllyPool...");
                if (Monitor.TryEnter(_allyPoolLock, TimeSpan.FromSeconds(3)))
                {
                    Log.Information("DeadlockDemo: Thread 2 acquired AllyPool (no deadlock)");
                    Thread.Sleep(100);
                    Monitor.Exit(_allyPoolLock);
                }
                else
                {
                    t2Deadlocked = true;
                    Log.Warning("DeadlockDemo: Thread 2 deadlocked — holds EnemyPool, waiting for AllyPool");
                }
            }
        });

        Task.WaitAll(t1, t2);

        AnsiConsole.MarkupLine($"  Expected: both threads complete in ~0.5s");
        AnsiConsole.MarkupLine($"  Actual:   ~3s (waited for timeout)");

        if (t1Deadlocked && t2Deadlocked)
        {
            AnsiConsole.MarkupLine("[red]  ❌ DEADLOCK DETECTED![/]");
            AnsiConsole.MarkupLine("[red]  Thread 1 holds AllyPool, waiting for EnemyPool[/]");
            AnsiConsole.MarkupLine("[red]  Thread 2 holds EnemyPool, waiting for AllyPool[/]");
            AnsiConsole.MarkupLine("[red]  Circular wait → neither thread can proceed[/]");
        }

        AnsiConsole.WriteLine();
    }

    private void FixedPass()
    {
        AnsiConsole.MarkupLine("[green]--- Fixed pass (same lock order: AllyPool → EnemyPool) ---[/]");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        Parallel.Invoke(
            () =>
            {
                lock (_allyPoolLock)
                {
                    Thread.Sleep(200);
                    Log.Information("DeadlockDemo: Thread 1 acquired both locks");
                    lock (_enemyPoolLock)
                    {
                        Thread.Sleep(100);
                    }
                }
            },
            () =>
            {
                lock (_allyPoolLock)
                {
                    Thread.Sleep(200);
                    Log.Information("DeadlockDemo: Thread 2 acquired both locks");
                    lock (_enemyPoolLock)
                    {
                        Thread.Sleep(100);
                    }
                }
            });

        sw.Stop();

        AnsiConsole.MarkupLine($"  Elapsed: [cyan]{sw.ElapsedMilliseconds}ms[/]");
        AnsiConsole.MarkupLine("[green]  ✅ Both threads completed successfully![/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[italic]Why? Both threads acquire locks in the same order[/]");
        AnsiConsole.MarkupLine("[italic](AllyPool → EnemyPool). When Thread 1 holds AllyPool,[/]");
        AnsiConsole.MarkupLine("[italic]Thread 2 waits at the AllyPool gate. No circular wait[/]");
        AnsiConsole.MarkupLine("[italic]is possible → deadlock eliminated.[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]Lock ordering principle:[/] Always acquire shared");
        AnsiConsole.MarkupLine("resources in a consistent order across all threads.");
        AnsiConsole.MarkupLine("If all threads acquire A before B, then while");
        AnsiConsole.MarkupLine("a thread holds A, no other thread can hold B and");
        AnsiConsole.MarkupLine("wait for A — the cycle is broken.");
        Console.WriteLine();
    }
}

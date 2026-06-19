using DotaDraftAdvisor.Demo;
using FluentAssertions;

namespace DotaDraftAdvisor.Tests;

public class DeadlockSectionTests
{
    [Fact]
    public void Run_completes_within_reasonable_time()
    {
        var sut = new DeadlockSection();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        sut.Run();

        sw.Stop();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Broken_pass_with_opposite_lock_order_times_out()
    {
        var lock1 = new object();
        var lock2 = new object();
        var barrier = new Barrier(2);
        bool t1TimedOut = false;
        bool t2TimedOut = false;

        var t1 = Task.Run(() =>
        {
            lock (lock1)
            {
                barrier.SignalAndWait();
                Thread.Sleep(200);
                t1TimedOut = !Monitor.TryEnter(lock2, TimeSpan.FromSeconds(3));
                if (!t1TimedOut) Monitor.Exit(lock2);
            }
        });

        var t2 = Task.Run(() =>
        {
            lock (lock2)
            {
                barrier.SignalAndWait();
                Thread.Sleep(200);
                t2TimedOut = !Monitor.TryEnter(lock1, TimeSpan.FromSeconds(3));
                if (!t2TimedOut) Monitor.Exit(lock1);
            }
        });

        await Task.WhenAll(t1, t2);

        t1TimedOut.Should().BeTrue("Thread 1 should deadlock waiting for lock2");
        t2TimedOut.Should().BeTrue("Thread 2 should deadlock waiting for lock1");
    }

    [Fact]
    public void Fixed_pass_with_same_lock_order_completes_quickly()
    {
        var lock1 = new object();
        var lock2 = new object();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Parallel.Invoke(
            () =>
            {
                lock (lock1)
                {
                    Thread.Sleep(200);
                    lock (lock2) { Thread.Sleep(50); }
                }
            },
            () =>
            {
                lock (lock1)
                {
                    Thread.Sleep(200);
                    lock (lock2) { Thread.Sleep(50); }
                }
            });

        sw.Stop();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }
}

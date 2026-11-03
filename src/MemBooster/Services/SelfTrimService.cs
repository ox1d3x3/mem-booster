using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace MemBooster.Services;

/// <summary>
/// Hands Mem-Booster's own idle memory back to Windows while it sits in the background.
///
/// Compacts the managed heap (so freed objects don't pin whole pages), returns the empty
/// GC regions to the OS, then trims the working set. Trimmed pages move to the standby
/// list and come back on demand, so returning to the window is still instant.
/// </summary>
public static class SelfTrimService
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(30);
    private static long _lastTrimTicks;

    public static SelfTrimResult TrimCurrentProcess()
    {
        // Rapid alt-tabbing shouldn't trigger a full collection every time.
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastTrimTicks);
        if (last != 0 && now - last < (long)MinimumInterval.TotalMilliseconds)
        {
            return SelfTrimResult.Skipped;
        }

        Interlocked.Exchange(ref _lastTrimTicks, now);

        var before = CurrentWorkingSet();

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        // (SIZE_T)-1 for both limits = remove as many pages as possible from the working set.
        SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1);

        return new SelfTrimResult(before, CurrentWorkingSet());
    }

    private static long CurrentWorkingSet()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint minimumWorkingSetSize, nint maximumWorkingSetSize);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
}

public readonly record struct SelfTrimResult(long BeforeBytes, long AfterBytes)
{
    public static SelfTrimResult Skipped => default;

    public long FreedBytes => Math.Max(0, BeforeBytes - AfterBytes);
}

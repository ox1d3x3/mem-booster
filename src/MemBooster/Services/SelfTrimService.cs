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
        var beforeDetail = DescribeCurrentProcess();

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        // (SIZE_T)-1 for both limits = remove as many pages as possible from the working set.
        SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1);

        return new SelfTrimResult(before, CurrentWorkingSet()) { BeforeDetail = beforeDetail };
    }

    /// <summary>
    /// One-line breakdown of Mem-Booster's own memory for logs and diagnostics. The total
    /// working set includes pages shared with other processes (Windows, WinUI and GPU
    /// driver DLLs); the private working set is what Task Manager's Memory column shows.
    /// </summary>
    public static string DescribeCurrentProcess()
    {
        try
        {
            var counters = new ProcessMemoryCountersEx2 { cb = (uint)Marshal.SizeOf<ProcessMemoryCountersEx2>() };
            var hasCounters = GetProcessMemoryInfo(GetCurrentProcess(), ref counters, counters.cb);
            if (!hasCounters)
            {
                // Older Windows builds only know the shorter struct without PrivateWorkingSetSize.
                counters = new ProcessMemoryCountersEx2 { cb = ProcessMemoryCountersExSize };
                hasCounters = GetProcessMemoryInfo(GetCurrentProcess(), ref counters, ProcessMemoryCountersExSize);
            }

            var gc = GC.GetGCMemoryInfo();
            var native = hasCounters
                ? $"workingSet={Mb(counters.WorkingSetSize)}; privateWorkingSet={(counters.PrivateWorkingSetSize > 0 ? Mb(counters.PrivateWorkingSetSize) : "n/a")}; privateCommit={Mb(counters.PrivateUsage)}"
                : $"workingSet={Mb((ulong)CurrentWorkingSet())}";
            return $"{native}; managedHeap={Mb((ulong)gc.HeapSizeBytes)}; gcCommitted={Mb((ulong)gc.TotalCommittedBytes)}; gcCount={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}";
        }
        catch (Exception ex)
        {
            return $"unavailable ({ex.Message})";
        }
    }

    private static string Mb(ulong bytes) => $"{bytes / 1048576d:0} MB";

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

    // PROCESS_MEMORY_COUNTERS_EX2. Everything up to PrivateUsage is the older _EX layout.
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCountersEx2
    {
        public uint cb;
        public uint PageFaultCount;
        public nuint PeakWorkingSetSize;
        public nuint WorkingSetSize;
        public nuint QuotaPeakPagedPoolUsage;
        public nuint QuotaPagedPoolUsage;
        public nuint QuotaPeakNonPagedPoolUsage;
        public nuint QuotaNonPagedPoolUsage;
        public nuint PagefileUsage;
        public nuint PeakPagefileUsage;
        public nuint PrivateUsage;
        public nuint PrivateWorkingSetSize;
        public ulong SharedCommitUsage;
    }

    private const uint ProcessMemoryCountersExSize = 80; // x64 size of PROCESS_MEMORY_COUNTERS_EX

    [DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCountersEx2 counters, uint size);
}

public readonly record struct SelfTrimResult(long BeforeBytes, long AfterBytes)
{
    public static SelfTrimResult Skipped => default;

    /// <summary>Breakdown of the process's memory just before the trim.</summary>
    public string? BeforeDetail { get; init; }

    public long FreedBytes => Math.Max(0, BeforeBytes - AfterBytes);
}

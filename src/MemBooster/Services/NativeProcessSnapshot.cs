using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MemBooster.Services;

/// <summary>
/// One-call snapshot of every running process: PID, parent PID, image name, working set
/// and start time.
///
/// Uses NtQuerySystemInformation(SystemProcessInformation), the same call .NET's
/// Process.GetProcesses() makes internally — but without building a Process object (and
/// a ProcessThread object for every thread) per process. On a typical desktop that's
/// thousands of short-lived objects avoided per scan. The native buffer is freed
/// immediately afterwards, so nothing stays resident between scans.
/// </summary>
public static class NativeProcessSnapshot
{
    private const int SystemProcessInformation = 5;
    private const uint StatusInfoLengthMismatch = 0xC0000004;

    // SYSTEM_PROCESS_INFORMATION field offsets on 64-bit Windows (same layout .NET uses).
    private const int OffsetNextEntry = 0;
    private const int OffsetCreateTime = 32;
    private const int OffsetImageNameLength = 56;
    private const int OffsetImageNameBuffer = 64;
    private const int OffsetProcessId = 80;
    private const int OffsetParentProcessId = 88;
    private const int OffsetWorkingSetSize = 144;

    // Start near the size the last call needed, so most scans make exactly one call.
    private static int _bufferSizeHint = 512 * 1024;

    public static IReadOnlyList<NativeProcessEntry> GetProcesses()
    {
        if (IntPtr.Size != 8)
        {
            return GetProcessesFallback(); // Offsets above are x64-only.
        }

        var size = Volatile.Read(ref _bufferSizeHint);
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                var status = NtQuerySystemInformation(SystemProcessInformation, buffer, size, out var needed);
                if (status == StatusInfoLengthMismatch)
                {
                    // Processes can start between calls; leave generous headroom.
                    size = Math.Max(size * 2, needed + (128 * 1024));
                    continue;
                }

                if (status != 0)
                {
                    return GetProcessesFallback();
                }

                Volatile.Write(ref _bufferSizeHint, Math.Max(256 * 1024, needed + (64 * 1024)));
                return Parse(buffer, needed);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return GetProcessesFallback();
    }

    private static List<NativeProcessEntry> Parse(IntPtr buffer, int length)
    {
        var results = new List<NativeProcessEntry>(384);
        var offset = 0;

        while (offset >= 0 && offset < length)
        {
            var entry = buffer + offset;
            var pid = (int)Marshal.ReadIntPtr(entry, OffsetProcessId);
            var parentPid = (int)Marshal.ReadIntPtr(entry, OffsetParentProcessId);
            var nameBytes = (ushort)Marshal.ReadInt16(entry, OffsetImageNameLength);
            var namePtr = Marshal.ReadIntPtr(entry, OffsetImageNameBuffer);
            var workingSet = (long)Marshal.ReadIntPtr(entry, OffsetWorkingSetSize);
            var createTime = Marshal.ReadInt64(entry, OffsetCreateTime);

            var name = nameBytes > 0 && namePtr != IntPtr.Zero
                ? CanonicalExeName(Marshal.PtrToStringUni(namePtr, nameBytes / 2))
                : string.Empty; // PID 0 (System Idle Process) has no name.

            results.Add(new NativeProcessEntry(pid, parentPid, name, workingSet, createTime));

            var next = Marshal.ReadInt32(entry, OffsetNextEntry);
            if (next <= 0)
            {
                break;
            }

            offset += next;
        }

        return results;
    }

    // Keeps names identical to earlier versions: "Foo.EXE" -> "Foo.exe".
    private static string CanonicalExeName(string name)
    {
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".exe", StringComparison.Ordinal)
            ? string.Concat(name.AsSpan(0, name.Length - 4), ".exe")
            : name;
    }

    // Slower, allocation-heavy path; only used if the native call ever fails.
    private static List<NativeProcessEntry> GetProcessesFallback()
    {
        var results = new List<NativeProcessEntry>(384);
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                long workingSet;
                try
                {
                    workingSet = process.WorkingSet64;
                }
                catch
                {
                    workingSet = 0;
                }

                // Parent PID / start time aren't available here; process trees fall back to roots only.
                // Raw name (no ".exe"): callers normalise, which handles special names like "System".
                results.Add(new NativeProcessEntry(process.Id, 0, process.ProcessName, workingSet, 0));
            }
            catch
            {
                // Exited mid-enumeration.
            }
            finally
            {
                process.Dispose();
            }
        }

        return results;
    }

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern uint NtQuerySystemInformation(int systemInformationClass, IntPtr systemInformation, int systemInformationLength, out int returnLength);
}

/// <param name="CreateTime">Process start time as a FILETIME tick count (0 when unknown).</param>
public readonly record struct NativeProcessEntry(int ProcessId, int ParentProcessId, string ExeName, long WorkingSetBytes, long CreateTime);

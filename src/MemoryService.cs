using System.Runtime.InteropServices;

namespace MemBooster.Services;

public sealed class MemoryService
{
    public MemoryInfo GetMemoryInfo()
    {
        var status = new MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
        };

        if (!GlobalMemoryStatusEx(ref status))
        {
            return new MemoryInfo(0, 0, 0, 0);
        }

        var used = status.ullTotalPhys - status.ullAvailPhys;
        return new MemoryInfo(status.ullTotalPhys, status.ullAvailPhys, used, status.dwMemoryLoad);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}

public sealed record MemoryInfo(ulong TotalBytes, ulong AvailableBytes, ulong UsedBytes, uint UsedPercent)
{
    public string Summary
    {
        get
        {
            if (TotalBytes == 0)
            {
                return "Memory unavailable";
            }

            return $"{FormatBytes(UsedBytes)} / {FormatBytes(TotalBytes)} used";
        }
    }

    private static string FormatBytes(ulong bytes)
    {
        var gb = bytes / 1024d / 1024d / 1024d;
        return $"{gb:0.0} GB";
    }
}

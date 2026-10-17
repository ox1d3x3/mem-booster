using System.Runtime.InteropServices;

namespace MemBooster.Services;

/// <summary>
/// Explorer-style Open/Save dialogs for XML profiles.
///
/// Uses the Win32 common dialog API (comdlg32 GetOpenFileNameW / GetSaveFileNameW)
/// through plain P/Invoke. Earlier versions used [ComImport] IFileDialog, which relies
/// on .NET "built-in COM interop" — a runtime feature that some build toolchains switch
/// off, making Load/Export XML fail with "Built-in COM has been disabled via a feature
/// switch". This API involves no .NET COM interop at all, so that switch cannot affect
/// it. On Windows 10/11 it still shows the modern Explorer dialog, and it works when
/// Mem-Booster is running as administrator (WinRT pickers do not).
///
/// The struct below is blittable (only IntPtr/int/short fields) and every string is
/// allocated and freed explicitly, so there is no hidden marshalling.
/// </summary>
public static class NativeFileDialogService
{
    private const string Title = "Mem-Booster XML profile";
    private const string FilterName = "Mem-Booster XML profile (*.xml)";
    private const string FilterSpec = "*.xml";
    private const int FileBufferChars = 4096;

    private const int OFN_OVERWRITEPROMPT = 0x00000002;
    private const int OFN_HIDEREADONLY = 0x00000004;
    private const int OFN_NOCHANGEDIR = 0x00000008;   // never change the app's working directory
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_EXPLORER = 0x00080000;
    private const int OFN_ENABLESIZING = 0x00800000;

    public static string? ShowOpenXmlProfileDialog(IntPtr ownerHandle)
    {
        return ShowDialog(
            ownerHandle,
            "Load " + Title,
            defaultFileName: null,
            OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR | OFN_ENABLESIZING,
            save: false);
    }

    public static string? ShowSaveXmlProfileDialog(IntPtr ownerHandle)
    {
        return ShowDialog(
            ownerHandle,
            "Export " + Title,
            defaultFileName: "gaming-boost-profile.xml",
            OFN_EXPLORER | OFN_OVERWRITEPROMPT | OFN_PATHMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR | OFN_ENABLESIZING,
            save: true);
    }

    private static string? ShowDialog(IntPtr ownerHandle, string title, string? defaultFileName, int flags, bool save)
    {
        // Filter must be double-null terminated: "name\0spec\0\0".
        // StringToHGlobalUni appends one terminator; the trailing \0 here supplies the other.
        var filterPtr = Marshal.StringToHGlobalUni(FilterName + "\0" + FilterSpec + "\0");
        var titlePtr = Marshal.StringToHGlobalUni(title);
        var defExtPtr = Marshal.StringToHGlobalUni("xml");
        var filePtr = Marshal.AllocHGlobal(FileBufferChars * sizeof(char));

        try
        {
            // Zero the whole buffer, then seed the default file name (save dialog only).
            var initial = new char[FileBufferChars];
            if (!string.IsNullOrEmpty(defaultFileName))
            {
                defaultFileName.CopyTo(0, initial, 0, Math.Min(defaultFileName.Length, FileBufferChars - 1));
            }

            Marshal.Copy(initial, 0, filePtr, FileBufferChars);

            var ofn = new OPENFILENAME
            {
                lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                hwndOwner = ownerHandle,
                lpstrFilter = filterPtr,
                nFilterIndex = 1,
                lpstrFile = filePtr,
                nMaxFile = FileBufferChars,
                lpstrTitle = titlePtr,
                lpstrDefExt = defExtPtr,
                Flags = flags
            };

            var accepted = save ? GetSaveFileNameW(ref ofn) : GetOpenFileNameW(ref ofn);
            if (!accepted)
            {
                var error = CommDlgExtendedError();
                if (error == 0)
                {
                    return null; // User cancelled.
                }

                throw new InvalidOperationException($"Windows file dialog error 0x{error:X4}.");
            }

            var path = Marshal.PtrToStringUni(filePtr);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        finally
        {
            Marshal.FreeHGlobal(filePtr);
            Marshal.FreeHGlobal(defExtPtr);
            Marshal.FreeHGlobal(titlePtr);
            Marshal.FreeHGlobal(filterPtr);
        }
    }

    [DllImport("comdlg32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileNameW(ref OPENFILENAME ofn);

    [DllImport("comdlg32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileNameW(ref OPENFILENAME ofn);

    [DllImport("comdlg32.dll", ExactSpelling = true)]
    private static extern uint CommDlgExtendedError();

    // OPENFILENAMEW. Field order and types match the Windows SDK definition
    // (including the trailing pvReserved/dwReserved/FlagsEx members).
    [StructLayout(LayoutKind.Sequential)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CrossFormat.Plugin;

/// <summary>Windows Explorer dialog on a dedicated STA thread; never blocks Dalamud's draw loop.</summary>
internal static class NativeFilePicker
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class OpenFileName
    {
        public int Size = Marshal.SizeOf<OpenFileName>();
        public nint Owner;
        public nint Instance;
        [MarshalAs(UnmanagedType.LPWStr)] public string Filter = "Scene and layout files (*.json)\0*.json\0All files (*.*)\0*.*\0\0";
        public nint CustomFilter;
        public int MaxCustomFilter;
        public int FilterIndex = 1;
        public nint File;
        public int MaxFile = 32768;
        public nint FileTitle;
        public int MaxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)] public string InitialDirectory = "";
        [MarshalAs(UnmanagedType.LPWStr)] public string Title = "";
        public uint Flags;
        public ushort FileOffset;
        public ushort FileExtension;
        [MarshalAs(UnmanagedType.LPWStr)] public string DefaultExtension = "json";
        public nint CustomData;
        public nint Hook;
        public nint TemplateName;
        public nint Reserved;
        public uint ReservedValue;
        public uint FlagsEx;
    }
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetOpenFileNameW([In, Out] OpenFileName options);
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSaveFileNameW([In, Out] OpenFileName options);
    [DllImport("comdlg32.dll")] private static extern uint CommDlgExtendedError();

    public static Task<string?> Show(string title, string directory, bool save = false, string fileName = "", bool image = false)
    {
        var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        nint owner = Process.GetCurrentProcess().MainWindowHandle;
        var thread = new Thread(() =>
        {
            nint buffer = Marshal.AllocHGlobal(32768 * 2);
            try
            {
                Marshal.Copy(new byte[32768 * 2], 0, buffer, 32768 * 2);
                var initialName = Encoding.Unicode.GetBytes(fileName[..Math.Min(fileName.Length, 32766)] + "\0");
                Marshal.Copy(initialName, 0, buffer, initialName.Length);
                var options = new OpenFileName
                {
                    Owner = owner, Title = title, InitialDirectory = directory, File = buffer,
                    // Explorer, resize, preserve process working directory, existing parent folder.
                    Flags = 0x00080000 | 0x00800000 | 0x00000008 | 0x00000800 | (save ? 0x00000002u : 0x00001000u),
                };
                if (image) { options.Filter = "Preview images (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0\0"; options.DefaultExtension = "png"; }
                bool accepted = save ? GetSaveFileNameW(options) : GetOpenFileNameW(options);
                if (accepted) result.TrySetResult(Marshal.PtrToStringUni(buffer));
                else
                {
                    uint error = CommDlgExtendedError();
                    if (error != 0) result.TrySetException(new IOException($"Windows file picker failed (0x{error:X})."));
                    else result.TrySetResult(null);
                }
            }
            catch (Exception e) { result.TrySetException(e); }
            finally { Marshal.FreeHGlobal(buffer); }
        }) { IsBackground = true, Name = "Sceneweaver file picker" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task;
    }
}

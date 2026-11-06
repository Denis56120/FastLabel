using System.Runtime.InteropServices;

/// <summary>
/// Utilitaire pour lancer un processus utilisateur depuis la Session 0 d'un Service Windows
/// </summary>
public static class ProcessLauncher
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr hToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(
        IntPtr hToken, string? lpApplicationName, string lpCommandLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles,
        uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXClass;
        public uint dwYClass;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    public static void StartProcessInUserSession(string exePath)
    {
        uint sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == 0xFFFFFFFF) return; // Pas de session active

        if (!WTSQueryUserToken(sessionId, out IntPtr hToken)) return;

        try
        {
            STARTUPINFO si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = "winsta0\\default"; // Force l'affichage sur le bureau interactif

            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

            string? workingDirectory = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;

            bool success = CreateProcessAsUser(
                hToken, null, exePath, IntPtr.Zero, IntPtr.Zero,
                false, 0, IntPtr.Zero, workingDirectory,
                ref si, out pi);

            if (success)
            {
                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
            }
        }
        finally
        {
            CloseHandle(hToken);
        }
    }
}
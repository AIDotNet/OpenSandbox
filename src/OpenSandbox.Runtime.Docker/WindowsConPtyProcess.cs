using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OpenSandbox.Runtime.Docker;

internal sealed class WindowsConPtyProcess : IDisposable
{
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const int ProcThreadAttributePseudoConsole = 0x00020016;
    private const uint Infinite = 0xFFFFFFFF;
    private const uint WaitObject0 = 0x00000000;

    private readonly SafeWaitHandle _processHandle;
    private readonly SafeWaitHandle _threadHandle;
    private readonly FileStream _outputStream;
    private readonly StreamWriter _inputWriter;
    private IntPtr _pseudoConsole;
    private bool _disposed;

    private WindowsConPtyProcess(IntPtr pseudoConsole, SafeWaitHandle processHandle, SafeWaitHandle threadHandle, FileStream outputStream, StreamWriter inputWriter)
    {
        _pseudoConsole = pseudoConsole;
        _processHandle = processHandle;
        _threadHandle = threadHandle;
        _outputStream = outputStream;
        _inputWriter = inputWriter;
    }

    public Stream OutputStream => _outputStream;
    public StreamWriter InputWriter => _inputWriter;

    public static WindowsConPtyProcess Start(string fileName, IReadOnlyList<string> arguments, short cols, short rows)
    {
        var securityAttributes = new SecurityAttributes
        {
            nLength = Marshal.SizeOf<SecurityAttributes>(),
            bInheritHandle = true,
        };

        IntPtr pseudoConsole = IntPtr.Zero;
        IntPtr pseudoConsoleInput = IntPtr.Zero;
        IntPtr inputWriterHandle = IntPtr.Zero;
        IntPtr outputReaderHandle = IntPtr.Zero;
        IntPtr pseudoConsoleOutput = IntPtr.Zero;
        IntPtr attributeList = IntPtr.Zero;
        IntPtr pseudoConsoleValue = IntPtr.Zero;
        SafeFileHandle? inputSafeHandle = null;
        SafeFileHandle? outputSafeHandle = null;

        try
        {
            if (!CreatePipe(out pseudoConsoleInput, out inputWriterHandle, ref securityAttributes, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe for terminal input failed.");
            }

            if (!CreatePipe(out outputReaderHandle, out pseudoConsoleOutput, ref securityAttributes, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe for terminal output failed.");
            }

            var createResult = CreatePseudoConsole(new Coord(cols, rows), pseudoConsoleInput, pseudoConsoleOutput, 0, out pseudoConsole);
            if (createResult != 0)
            {
                Marshal.ThrowExceptionForHR(createResult);
            }

            CloseHandle(pseudoConsoleInput);
            pseudoConsoleInput = IntPtr.Zero;
            CloseHandle(pseudoConsoleOutput);
            pseudoConsoleOutput = IntPtr.Zero;

            var attributeListSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeListSize);
            attributeList = Marshal.AllocHGlobal(attributeListSize);
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "InitializeProcThreadAttributeList failed.");
            }

            pseudoConsoleValue = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(pseudoConsoleValue, pseudoConsole);
            if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    (IntPtr)ProcThreadAttributePseudoConsole,
                    pseudoConsoleValue,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateProcThreadAttribute for pseudo console failed.");
            }

            var startupInfo = new StartupInfoEx();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<StartupInfoEx>();
            startupInfo.lpAttributeList = attributeList;

            var commandLine = BuildCommandLine(fileName, arguments);
            if (!CreateProcess(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT,
                    IntPtr.Zero,
                    null,
                    ref startupInfo,
                    out var processInformation))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess for ConPTY terminal failed.");
            }

            inputSafeHandle = new SafeFileHandle(inputWriterHandle, ownsHandle: true);
            outputSafeHandle = new SafeFileHandle(outputReaderHandle, ownsHandle: true);
            inputWriterHandle = IntPtr.Zero;
            outputReaderHandle = IntPtr.Zero;

            var inputStream = new FileStream(inputSafeHandle, FileAccess.Write, 4096, isAsync: true);
            var outputStream = new FileStream(outputSafeHandle, FileAccess.Read, 4096, isAsync: true);
            var inputWriter = new StreamWriter(inputStream, new UTF8Encoding(false))
            {
                AutoFlush = true,
            };

            return new WindowsConPtyProcess(
                pseudoConsole,
                new SafeWaitHandle(processInformation.hProcess, ownsHandle: true),
                new SafeWaitHandle(processInformation.hThread, ownsHandle: true),
                outputStream,
                inputWriter);
        }
        catch
        {
            inputSafeHandle?.Dispose();
            outputSafeHandle?.Dispose();

            if (inputWriterHandle != IntPtr.Zero)
            {
                CloseHandle(inputWriterHandle);
            }

            if (outputReaderHandle != IntPtr.Zero)
            {
                CloseHandle(outputReaderHandle);
            }

            if (pseudoConsoleInput != IntPtr.Zero)
            {
                CloseHandle(pseudoConsoleInput);
            }

            if (pseudoConsoleOutput != IntPtr.Zero)
            {
                CloseHandle(pseudoConsoleOutput);
            }

            if (pseudoConsole != IntPtr.Zero)
            {
                ClosePseudoConsole(pseudoConsole);
            }

            throw;
        }
        finally
        {
            if (attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (pseudoConsoleValue != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pseudoConsoleValue);
            }
        }
    }

    public void Resize(int cols, int rows)
    {
        if (_disposed || _pseudoConsole == IntPtr.Zero || cols <= 0 || rows <= 0)
        {
            return;
        }

        var resizeResult = ResizePseudoConsole(_pseudoConsole, new Coord((short)cols, (short)rows));
        if (resizeResult != 0)
        {
            Marshal.ThrowExceptionForHR(resizeResult);
        }
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            WaitForSingleObject(_processHandle.DangerousGetHandle(), Infinite);
        }, cancellationToken);
    }

    public void Terminate()
    {
        if (_disposed)
        {
            return;
        }

        if (WaitForSingleObject(_processHandle.DangerousGetHandle(), 0) == WaitObject0)
        {
            return;
        }

        TerminateProcess(_processHandle.DangerousGetHandle(), 1);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _inputWriter.Dispose();
        _outputStream.Dispose();
        _threadHandle.Dispose();
        _processHandle.Dispose();

        if (_pseudoConsole != IntPtr.Zero)
        {
            ClosePseudoConsole(_pseudoConsole);
            _pseudoConsole = IntPtr.Zero;
        }
    }

    private static string BuildCommandLine(string fileName, IReadOnlyList<string> arguments)
    {
        var parts = new List<string> { QuoteArgument(fileName) };
        parts.AddRange(arguments.Select(QuoteArgument));
        return string.Join(' ', parts);
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (!value.Any(ch => char.IsWhiteSpace(ch) || ch == '"'))
        {
            return value;
        }

        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord
    {
        public short X;
        public short Y;

        public Coord(short x, short y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SecurityAttributes lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(Coord size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ResizePseudoConsole(IntPtr hPC, Coord size);

    [DllImport("kernel32.dll")]
    private static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string? lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory, ref StartupInfoEx lpStartupInfo, out ProcessInformation lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);
}

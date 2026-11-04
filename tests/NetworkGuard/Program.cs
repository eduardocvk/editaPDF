using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
// Development-only debugger guard. No endpoint addresses or document data are collected.
// A network API breakpoint stops the test BEFORE the API executes.
static class Guard
{
    static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase) { "socket", "WSASocketW", "WSASocketA", "connect", "WSAConnect", "sendto", "WSASendTo", "ConnectEx", "DnsQuery_W", "DnsQuery_A", "DnsQuery_UTF8", "DnsQueryEx", "DnsQueryEx2", "DnsQueryRaw", "WinHttpConnect", "WinHttpSendRequest", "InternetConnectW", "InternetConnectA", "InternetOpenUrlW", "InternetOpenUrlA", "HttpSendRequestW", "HttpSendRequestA" };
    static readonly Dictionary<long, string> hooks = [];
    static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--canary-child")
        {
            using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            return 9;
        }
        if (args.Length == 0)
            throw new ArgumentException("Executable and arguments required");
        var exe = Path.GetFullPath(args[0]);
        var cmd = new StringBuilder('"' + exe + '"' + string.Concat(args.Skip(1).Select(a => " \"" + a.Replace("\"", "\\\"") + "\"")));
        var startup = new Startup { Size = Marshal.SizeOf<Startup>() };
        if (!CreateProcess(exe, cmd, IntPtr.Zero, IntPtr.Zero, false, 2, IntPtr.Zero, Path.GetDirectoryName(exe), ref startup, out var pi))
            throw new Win32Exception();
        var clock = Stopwatch.StartNew();
        bool exited = false;
        int result = 3;
        try
        {
            while (clock.Elapsed < TimeSpan.FromSeconds(120))
            {
                if (!WaitForDebugEvent(out var e, 1000))
                    continue;
                uint status = 0x00010002;
                if (e.Code == 3)
                {
                    Install(pi.Process, e.CreateBase, e.File);
                    if (e.File != IntPtr.Zero)
                        CloseHandle(e.File);
                }
                if (e.Code == 6)
                {
                    Install(pi.Process, e.LoadBase, e.File);
                    if (e.File != IntPtr.Zero)
                        CloseHandle(e.File);
                }
                if (e.Code == 1)
                {
                    if (e.ExceptionCode == 0x80000003)
                    {
                        if (hooks.TryGetValue(e.Address.ToInt64(), out var name))
                        {
                            Console.WriteLine("BLOCKED network API: " + name);
                            TerminateProcess(pi.Process, 2);
                            result = 2;
                        }
                    }
                    else
                        status = 0x80010001;
                }
                if (e.Code == 5)
                {
                    exited = true;
                    result = result == 2 ? 2 : (int)e.ExitCode;
                    ContinueDebugEvent(e.Pid, e.Tid, status);
                    break;
                }
                ContinueDebugEvent(e.Pid, e.Tid, status);
            }
            if (!exited)
            {
                TerminateProcess(pi.Process, 3);
                Console.WriteLine("FAIL timeout");
                result = 3;
            }
            Console.WriteLine($"Native network guard: {hooks.Count} API breakpoints; child exit {result}; " + (result == 0 ? "PASS no network API attempts in tested flows" : "FAIL"));
            return result;
        }
        finally { if (!exited) TerminateProcess(pi.Process, 3); CloseHandle(pi.Thread); CloseHandle(pi.Process); }
    }
    static void Install(IntPtr process, IntPtr module, IntPtr file)
    {
        if (file == IntPtr.Zero)
            return;
        var sb = new StringBuilder(32768);
        if (GetFinalPathNameByHandle(file, sb, (uint)sb.Capacity, 0) == 0)
            return;
        var path = sb.ToString();
        var name = Path.GetFileName(path);
        if (!new[] { "ws2_32.dll", "mswsock.dll", "dnsapi.dll", "winhttp.dll", "wininet.dll" }.Contains(name, StringComparer.OrdinalIgnoreCase))
            return;
        var data = File.ReadAllBytes(path);
        int pe = BitConverter.ToInt32(data, 0x3c), opt = pe + 24;
        int count = BitConverter.ToUInt16(data, pe + 6), optLen = BitConverter.ToUInt16(data, pe + 20);
        int dir = opt + (BitConverter.ToUInt16(data, opt) == 0x20b ? 112 : 96);
        uint export = BitConverter.ToUInt32(data, dir), exportSize = BitConverter.ToUInt32(data, dir + 4);
        if (export == 0)
            return;
        int Offset(uint rva)
        {
            for (int i = 0; i < count; i++)
            {
                int sec = opt + optLen + i * 40;
                uint start = BitConverter.ToUInt32(data, sec + 12), len = Math.Max(BitConverter.ToUInt32(data, sec + 8), BitConverter.ToUInt32(data, sec + 16));
                if (rva >= start && rva < start + len)
                    return checked((int)(rva - start + BitConverter.ToUInt32(data, sec + 20)));
            }
            throw new InvalidDataException("Invalid PE RVA");
        }
        int ex = Offset(export), n = BitConverter.ToInt32(data, ex + 24), functions = Offset(BitConverter.ToUInt32(data, ex + 28)), names = Offset(BitConverter.ToUInt32(data, ex + 32)), ordinals = Offset(BitConverter.ToUInt32(data, ex + 36));
        int installed = 0;
        for (int i = 0; i < n; i++)
        {
            int at = Offset(BitConverter.ToUInt32(data, names + i * 4)), end = Array.IndexOf(data, (byte)0, at);
            string function = Encoding.ASCII.GetString(data, at, end - at);
            if (!Names.Contains(function))
                continue;
            int ordinal = BitConverter.ToUInt16(data, ordinals + i * 2);
            uint rva = BitConverter.ToUInt32(data, functions + ordinal * 4);
            if (rva >= export && rva < export + exportSize)
                throw new InvalidOperationException("Forwarded network export requires review: " + function);
            var address = IntPtr.Add(module, checked((int)rva));
            if (hooks.ContainsKey(address.ToInt64()))
                continue;
            if (!VirtualProtectEx(process, address, 1, 0x40, out uint old))
                throw new Win32Exception();
            if (!WriteProcessMemory(process, address, [0xcc], 1, out var written) || written != 1)
                throw new Win32Exception();
            VirtualProtectEx(process, address, 1, old, out _);
            FlushInstructionCache(process, address, 1);
            hooks[address.ToInt64()] = name + "!" + function;
            installed++;
        }
        Console.WriteLine($"Guarded {name}: {installed} APIs");
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct Startup
    {
        public int Size; public IntPtr Reserved, Desktop, Title; public int X, Y, W, H, CharsX, CharsY, Fill, Flags; public short Show, Reserved2; public IntPtr ReservedPtr, StdIn, StdOut, StdErr;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct ProcessInfo
    {
        public IntPtr Process, Thread; public uint Pid, Tid;
    }
    [StructLayout(LayoutKind.Explicit, Size = 176)]
    struct DebugEvent
    {
        [FieldOffset(0)] public uint Code; [FieldOffset(4)] public uint Pid; [FieldOffset(8)] public uint Tid; [FieldOffset(16)] public IntPtr File; [FieldOffset(24)] public IntPtr LoadBase; [FieldOffset(40)] public IntPtr CreateBase; [FieldOffset(16)] public uint ExceptionCode; [FieldOffset(32)] public IntPtr Address; [FieldOffset(16)] public uint ExitCode;
    }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CreateProcess(string app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string? cwd, ref Startup si, out ProcessInfo pi);
    [DllImport("kernel32", SetLastError = true)] static extern bool WaitForDebugEvent(out DebugEvent e, uint ms);
    [DllImport("kernel32")] static extern bool ContinueDebugEvent(uint pid, uint tid, uint status);
    [DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32")] static extern bool TerminateProcess(IntPtr h, uint exit);
    [DllImport("kernel32", CharSet = CharSet.Unicode)] static extern uint GetFinalPathNameByHandle(IntPtr h, StringBuilder path, uint count, uint flags);
    [DllImport("kernel32", SetLastError = true)] static extern bool VirtualProtectEx(IntPtr p, IntPtr address, nuint size, uint protect, out uint old);
    [DllImport("kernel32", SetLastError = true)] static extern bool WriteProcessMemory(IntPtr p, IntPtr address, byte[] bytes, nuint size, out nuint written);
    [DllImport("kernel32")] static extern bool FlushInstructionCache(IntPtr p, IntPtr address, nuint size);
}

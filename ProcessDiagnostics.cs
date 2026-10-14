using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ChromeRemoteBarHide;

internal static class ProcessDiagnostics
{
    internal static string Describe(uint processId)
    {
        using var process = OpenProcess(0x1000, false, processId); // QUERY_LIMITED_INFORMATION
        if (process.IsInvalid)
            return $"security=unknown; OpenProcess error={Marshal.GetLastWin32Error()}";

        if (!OpenProcessToken(process, 0x0008, out var token)) // TOKEN_QUERY
        {
            var error = Marshal.GetLastWin32Error();
            token?.Dispose();
            return $"security=unknown; OpenProcessToken error={error}";
        }

        using (token)
            return $"integrity={ReadToken(token, 25, ReadIntegrity)}; uiAccess={ReadToken(token, 26, buffer => Marshal.ReadInt32(buffer).ToString())}";
    }

    private static string ReadToken(SafeAccessTokenHandle token, int informationClass, Func<IntPtr, string> read)
    {
        GetTokenInformation(token, informationClass, IntPtr.Zero, 0, out var length);
        var error = Marshal.GetLastWin32Error();
        if (length <= 0 || error != 122) // ERROR_INSUFFICIENT_BUFFER
            return $"unknown (GetTokenInformation error={error})";

        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            if (!GetTokenInformation(token, informationClass, buffer, length, out _))
                return $"unknown (GetTokenInformation error={Marshal.GetLastWin32Error()})";
            return read(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ReadIntegrity(IntPtr buffer)
    {
        // TOKEN_MANDATORY_LABEL starts with SID_AND_ATTRIBUTES, whose first field is the SID pointer.
        var sid = Marshal.ReadIntPtr(buffer);
        if (!IsValidSid(sid))
            return "unknown (invalid integrity SID)";
        var count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
        if (count == 0)
            return "unknown (empty integrity SID)";
        var rid = unchecked((uint)Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1))));
        return $"{IntegrityName(rid)} (0x{rid:X})";
    }

    internal static string IntegrityName(uint rid) => rid switch
    {
        < 0x1000 => "Untrusted",
        < 0x2000 => "Low",
        < 0x3000 => "Medium",
        < 0x4000 => "High",
        < 0x5000 => "System",
        _ => "Protected"
    };

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, IntPtr information, int length, out int returnLength);

    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsValidSid(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
}

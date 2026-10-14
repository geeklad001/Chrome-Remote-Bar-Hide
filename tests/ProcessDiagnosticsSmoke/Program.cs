using System.Diagnostics;
using ChromeRemoteBarHide;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
    checks++;
}

Check(ProcessDiagnostics.IntegrityName(0) == "Untrusted", "Untrusted integrity");
Check(ProcessDiagnostics.IntegrityName(0x1000) == "Low", "Low integrity");
Check(ProcessDiagnostics.IntegrityName(0x2000) == "Medium", "Medium integrity");
Check(ProcessDiagnostics.IntegrityName(0x2100) == "Medium", "Medium-plus integrity");
Check(ProcessDiagnostics.IntegrityName(0x3000) == "High", "High integrity");
Check(ProcessDiagnostics.IntegrityName(0x4000) == "System", "System integrity");
Check(ProcessDiagnostics.IntegrityName(0x5000) == "Protected", "Protected integrity");

var self = ProcessDiagnostics.Describe((uint)Environment.ProcessId);
Check(self.Contains("integrity=") && !self.Contains("unknown"), $"Cannot read own token: {self}");
Check(self.Contains("uiAccess=0") || self.Contains("uiAccess=1"), $"Cannot read UIAccess: {self}");
Check(ProcessDiagnostics.Describe(uint.MaxValue).Contains("security=unknown"), "Invalid process must report unknown");
Check(ProcessDiagnostics.Describe(0).Contains("OpenProcess error="), "Inaccessible process must preserve error");

using var process = Process.GetCurrentProcess();
var handlesBefore = process.HandleCount;
for (var i = 0; i < 100; i++)
    Check(ProcessDiagnostics.Describe((uint)Environment.ProcessId) == self, "Repeated token query changed");
process.Refresh();
Check(process.HandleCount < handlesBefore + 8, "Token or process handles leaked");
Console.WriteLine($"Passed {checks} checks; {self}");

using System.Runtime.InteropServices;

namespace LibraryDesk;

public static class NativeFees
{
    static NativeFees()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeFees).Assembly, (name, _, _) =>
            name == "libraryfees" ? NativeLibrary.Load(LibraryPath()) : 0);
    }

    private static string LibraryPath()
    {
        string? custom = Environment.GetEnvironmentVariable("LIBRARYDESK_NATIVE");
        if (!string.IsNullOrWhiteSpace(custom)) return Path.GetFullPath(custom);
        string platform = OperatingSystem.IsWindows() ? "win" : "linux";
        string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        string name = OperatingSystem.IsWindows() ? "libraryfees.dll" : "liblibraryfees.so";
        return Path.Combine(AppContext.BaseDirectory, "runtimes", platform + "-" + arch, name);
    }

    [DllImport("libraryfees", EntryPoint = "library_fee", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateFee(int days);

    public static Result<int> Quote(int days)
    {
        try
        {
            int fee = CalculateFee(days);
            return fee < 0 ? new Result<int>.Fail("Native fee rejected the day count.") : new Result<int>.Ok(fee);
        }
        catch (Exception e) when (e is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or ArgumentException)
        {
            return new Result<int>.Fail("Cannot load native fee function: " + e.Message);
        }
    }
}

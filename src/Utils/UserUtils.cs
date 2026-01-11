using System.Runtime.InteropServices;

public static class UserUtils
{
    [DllImport("libc")]
    public static extern uint getuid();
    [DllImport("libc")]
    public static extern uint geteuid();
    public static string GetUser()
    {
        return $"User={Environment.UserName}, UID={getuid()}, EUID={geteuid()}";
    }
}
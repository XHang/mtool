using System;
using System.IO;
using System.Runtime.InteropServices;

public static class SymlinkHelper
{
    // Win32 API
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateSymbolicLink(
        string lpSymlinkFileName,
        string lpTargetFileName,
        int dwFlags);

    private const int SYMBOLIC_LINK_FLAG_DIRECTORY = 0x1;
    private const int SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE = 0x2;

    public static bool CreateDirectorySymlink(string linkPath, string targetPath, out string error)
    {
        error = null;

        try
        {
            // 强制绝对路径
            linkPath = Path.GetFullPath(linkPath);
            targetPath = Path.GetFullPath(targetPath);

            // 删除旧链接
            if (Directory.Exists(linkPath))
                Directory.Delete(linkPath);

            Directory.CreateDirectory(Path.GetDirectoryName(linkPath));

            // 尝试创建符号链接（允许无管理员权限）
            bool result = CreateSymbolicLink(
                linkPath,
                targetPath,
                SYMBOLIC_LINK_FLAG_DIRECTORY | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE
            );

            if (!result)
            {
                int code = Marshal.GetLastWin32Error();
                error = $"CreateSymbolicLink failed, Win32 Error = {code}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

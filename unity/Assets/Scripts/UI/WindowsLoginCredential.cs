using System;
using System.Runtime.InteropServices;

namespace GuiQin
{
    // 账号与密码由当前 Windows 用户的凭据管理器保管，其他平台不显示记住选项。
    internal static class WindowsLoginCredential
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        const uint Generic = 1, LocalMachine = 2;
        const int NotFound = 1168;
        public static bool Supported => true;

        [StructLayout(LayoutKind.Sequential)]
        struct Credential
        {
            public uint Flags, Type;
            public IntPtr Target, Comment;
            public uint LastWrittenLow, LastWrittenHigh;
            public uint BlobSize;
            public IntPtr Blob;
            public uint Persist, AttributeCount;
            public IntPtr Attributes, Alias, UserName;
        }

        [DllImport("advapi32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CredReadW(string target, uint type, uint flags, out IntPtr data);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CredWriteW(ref Credential data, uint flags);

        [DllImport("advapi32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CredDeleteW(string target, uint type, uint flags);

        [DllImport("advapi32.dll", ExactSpelling = true)]
        static extern void CredFree(IntPtr data);

        static string Target(string endpoint) => "GuiQin/Login/" + endpoint;

        public static bool TryRead(string endpoint, out string account, out string password)
        {
            account = password = null;
            if (!CredReadW(Target(endpoint), Generic, 0, out var pointer)) return false;
            try
            {
                var data = Marshal.PtrToStructure<Credential>(pointer);
                if (data.Blob == IntPtr.Zero || data.BlobSize == 0 || data.BlobSize > 64 || data.BlobSize % 2 != 0) return false;
                account = Marshal.PtrToStringUni(data.UserName);
                password = Marshal.PtrToStringUni(data.Blob, (int)data.BlobSize / 2);
                return !string.IsNullOrEmpty(account) && !string.IsNullOrEmpty(password);
            }
            finally { CredFree(pointer); }
        }

        public static bool Save(string endpoint, string account, string password)
        {
            var data = new Credential { Type = Generic, Persist = LocalMachine, BlobSize = (uint)password.Length * 2 };
            try
            {
                data.Target = Marshal.StringToCoTaskMemUni(Target(endpoint));
                data.UserName = Marshal.StringToCoTaskMemUni(account);
                data.Blob = Marshal.StringToCoTaskMemUni(password);
                return CredWriteW(ref data, 0);
            }
            finally
            {
                Marshal.FreeCoTaskMem(data.Target);
                Marshal.FreeCoTaskMem(data.UserName);
                Marshal.ZeroFreeCoTaskMemUnicode(data.Blob);
            }
        }

        public static bool Clear(string endpoint) =>
            CredDeleteW(Target(endpoint), Generic, 0) || Marshal.GetLastWin32Error() == NotFound;
#else
        public static bool Supported => false;
        public static bool TryRead(string endpoint, out string account, out string password)
        {
            account = password = null;
            return false;
        }
        public static bool Save(string endpoint, string account, string password) => false;
        public static bool Clear(string endpoint) => false;
#endif
    }
}

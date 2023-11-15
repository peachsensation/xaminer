using System.Runtime.InteropServices;

namespace Xaminer.App.Interop
{
    internal static partial class ConsoleInterop
    {
        #region AllocConsole
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool AllocConsole();
        #endregion

        #region SetConsoleCtrlHandler
        internal delegate bool ConsoleCtrlDelegate(int sig);

        [LibraryImport("Kernel32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handler, [MarshalAs(UnmanagedType.Bool)] bool add);
        #endregion
    }
}

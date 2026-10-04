using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PulsoUsb {
    internal enum PinOutcome { Pinned, Declined, Manual }
    internal static class TaskbarIntegration {
        public const string AppId = "Pulso.UsbControl";
        [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
        static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
        public static void SetIdentity() { SetCurrentProcessExplicitAppUserModelID(AppId); }

        // Read the documented feature gate; do not unlock or change Windows policy.
        static bool NeedsRestrictedFeature() {
            using (var registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel\LimitedAccessFeatures\com.microsoft.windows.taskbar.pin")) {
                return key != null && Convert.ToInt32(key.GetValue("4096B239A7295B635C090E647E867B5707DA6AB6CB78340B01FE4E0C8F4953D4", 0)) != 0;
            }
        }
        static bool SupportsDesktop(Type managerType) {
            Type marker = Type.GetType("Windows.UI.Shell.ITaskbarManagerDesktopAppSupportStatics, Windows.UI, ContentType=WindowsRuntime", false);
            if (marker == null) return false;
            object factory = WindowsRuntimeMarshal.GetActivationFactory(managerType);
            IntPtr unknown = Marshal.GetIUnknownForObject(factory), supported = IntPtr.Zero;
            try { Guid id = marker.GUID; return Marshal.QueryInterface(unknown, ref id, out supported) >= 0; }
            finally { if (supported != IntPtr.Zero) Marshal.Release(supported); Marshal.Release(unknown); }
        }
        static async Task<bool> Result(object operation) {
            Type info = Type.GetType("Windows.Foundation.IAsyncInfo, Windows.Foundation, ContentType=WindowsRuntime", true);
            Type result = Type.GetType("Windows.Foundation.IAsyncOperation`1, Windows.Foundation, ContentType=WindowsRuntime", true).MakeGenericType(typeof(bool));
            DateTime deadline = DateTime.UtcNow.AddMinutes(2);
            try {
                while (Convert.ToInt32(info.GetProperty("Status").GetValue(operation, null)) == 0) {
                    if (DateTime.UtcNow >= deadline) { info.GetMethod("Cancel").Invoke(operation, null); return false; }
                    await Task.Delay(100);
                }
                if (Convert.ToInt32(info.GetProperty("Status").GetValue(operation, null)) != 1) return false;
                return (bool)result.GetMethod("GetResults").Invoke(operation, null);
            } finally {
                try { info.GetMethod("Close").Invoke(operation, null); } catch (TargetInvocationException) { }
            }
        }
        // Called exclusively by a button in the foreground app, never by Setup.
        public static async Task<PinOutcome> Request() {
            try {
                if (NeedsRestrictedFeature()) return PinOutcome.Manual;
                Type type = Type.GetType("Windows.UI.Shell.TaskbarManager, Windows.UI, ContentType=WindowsRuntime", false);
                if (type == null || !SupportsDesktop(type)) return PinOutcome.Manual;
                object manager = type.GetMethod("GetDefault").Invoke(null, null);
                if (!(bool)type.GetProperty("IsSupported").GetValue(manager, null)) return PinOutcome.Manual;
                if (await Result(type.GetMethod("IsCurrentAppPinnedAsync").Invoke(manager, null))) return PinOutcome.Pinned;
                if (!(bool)type.GetProperty("IsPinningAllowed").GetValue(manager, null)) return PinOutcome.Manual;
                return await Result(type.GetMethod("RequestPinCurrentAppAsync").Invoke(manager, null)) ? PinOutcome.Pinned : PinOutcome.Declined;
            } catch (Exception ex) {
                if (ex is TargetInvocationException || ex is COMException || ex is TypeLoadException || ex is NotSupportedException || ex is UnauthorizedAccessException || ex is IOException || ex is System.Security.SecurityException) return PinOutcome.Manual;
                throw;
            }
        }
    }
}

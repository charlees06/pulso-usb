using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Win32;

namespace PulsoUsb {
    internal static class DriverPackage {
        internal const string InfHash="48C3A0055070B9A66C239FFC3D068B39AAFE7A14554984268ACC6DA1492759E6";
        internal const string SysHash="2F82CDEB36BDAA42EA1933A9B11F3B8E1BDB28E6D3E3DA7E65B4631B3375412D";
        internal const string Revision="994259a8de31b35d2d44dc800368d9418dd3eb04";
        [StructLayout(LayoutKind.Sequential)] struct SystemInfo {public ushort Architecture,Reserved;public uint PageSize;public IntPtr Min,Max,Mask;public uint Count,Type,Granularity;public ushort Level,Revision;}
        [DllImport("kernel32.dll")] static extern void GetNativeSystemInfo(out SystemInfo info);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr SetupOpenInfFile(string file,string infClass,uint style,out uint errorLine);
        [DllImport("setupapi.dll")] static extern void SetupCloseInfFile(IntPtr inf);
        [DllImport("setupapi.dll")] static extern IntPtr SetupInitDefaultQueueCallback(IntPtr owner);
        [DllImport("setupapi.dll")] static extern void SetupTermDefaultQueueCallback(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate uint FileCallback(IntPtr context,uint notification,UIntPtr param1,UIntPtr param2);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode)] static extern uint SetupDefaultQueueCallback(IntPtr context,uint notification,UIntPtr param1,UIntPtr param2);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupInstallFromInfSection(IntPtr owner,IntPtr inf,string section,uint flags,IntPtr key,string source,uint copyFlags,FileCallback callback,IntPtr context,IntPtr deviceInfo,IntPtr device);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupInstallServicesFromInfSection(IntPtr inf,string section,uint flags);
        public static bool Supported(){SystemInfo info;GetNativeSystemInfo(out info);return info.Architecture==9 && Environment.OSVersion.Version.Major>=10;}
        public static bool RuntimeReady(){using(var registry=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32))using(var key=registry.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))return key!=null && Convert.ToInt32(key.GetValue("Release",0))>=528040;}
        public static bool Registered(){using(var registry=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))using(var key=registry.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\hidusbf"))return key!=null;}
        public static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
        internal static byte[] Resource(string name,string hash){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(stream==null)throw new IOException("Faltan archivos del componente incluido. Descarga de nuevo el paquete completo.");using(var memory=new MemoryStream()){stream.CopyTo(memory);var bytes=memory.ToArray();if(Hash(bytes)!=hash)throw new IOException("Los archivos del componente no superaron la verificación.");return bytes;}}}
        internal static void Extract(string folder){byte[] inf=Resource("HIDUSBF_AS.INF",InfHash),sys=Resource("HIDUSBF_NOPATCH.SYS",SysHash);Directory.CreateDirectory(Path.Combine(folder,"amd64_as"));File.WriteAllBytes(Path.Combine(folder,"HIDUSBF_AS.INF"),inf);File.WriteAllBytes(Path.Combine(folder,"amd64_as","hidusbf.sys"),sys);}
        internal static void Clean(string folder){string inf=Path.Combine(folder,"HIDUSBF_AS.INF"),sub=Path.Combine(folder,"amd64_as"),sys=Path.Combine(sub,"hidusbf.sys");if(File.Exists(inf))File.Delete(inf);if(File.Exists(sys))File.Delete(sys);if(Directory.Exists(sub))Directory.Delete(sub);if(Directory.Exists(folder))Directory.Delete(folder);}
        internal static bool ShouldInstall(bool supported,bool registered,bool fileExists){if(!supported)throw new InvalidOperationException("Este paquete requiere Windows 10 u 11 de 64 bits, con procesador Intel o AMD.");if(registered)return false;if(fileExists)throw new InvalidOperationException("Ya existe un archivo de HIDUSBF sin una instalación reconocida. Revisa la instalación oficial antes de continuar.");return true;}
        public static void Install(){
            if(!Changes.IsAdministrator())throw new InvalidOperationException("Windows necesita permisos de administrador para instalar el componente.");
            using(var mutex=new Mutex(false,@"Global\PulsoDriverInstallation")){bool acquired=false;try{try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}if(!acquired)throw new InvalidOperationException("Otra instalación de Pulso está en curso.");
                string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"drivers","hidusbf.sys");
                if(!ShouldInstall(Supported(),Registered(),File.Exists(target)))return;
                string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Pulso-Driver-"+Guid.NewGuid().ToString("N"));
                var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);foreach(var sid in new[]{WellKnownSidType.BuiltinAdministratorsSid,WellKnownSidType.LocalSystemSid})security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid,null),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
                Directory.CreateDirectory(folder,security);
                bool copied=false;
                try{Extract(folder);uint line;IntPtr inf=SetupOpenInfFile(Path.Combine(folder,"HIDUSBF_AS.INF"),null,2,out line);if(inf==new IntPtr(-1))throw new Win32Exception(Marshal.GetLastWin32Error());
                    try{IntPtr context=SetupInitDefaultQueueCallback(IntPtr.Zero);if(context==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());FileCallback callback=SetupDefaultQueueCallback;
                        try{if(!SetupInstallFromInfSection(IntPtr.Zero,inf,"DefaultInstall.nt",0x10,IntPtr.Zero,folder,0x8,callback,context,IntPtr.Zero,IntPtr.Zero))throw new Win32Exception(Marshal.GetLastWin32Error());}finally{SetupTermDefaultQueueCallback(context);GC.KeepAlive(callback);}
                        if(!File.Exists(target) || Hash(File.ReadAllBytes(target))!=SysHash)throw new IOException("Windows no confirmó los archivos del componente.");
                        copied=true;
                        if(!SetupInstallServicesFromInfSection(inf,"DefaultInstall.nt.services",0))throw new Win32Exception(Marshal.GetLastWin32Error());
                    }finally{SetupCloseInfFile(inf);}
                    if(!Registered())throw new IOException("Windows no confirmó la instalación del componente.");
                    try{using(var service=new ServiceController("hidusbf")){if(service.Status==ServiceControllerStatus.Stopped)service.Start();service.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(15));}}
                    catch(Exception ex){throw new InvalidOperationException("El componente se instaló, pero Windows no permitió iniciarlo. Reinicia el equipo y vuelve a intentarlo. Si continúa, consulta la guía oficial. "+ex.Message);}
                }catch{try{if(copied && !Registered() && File.Exists(target) && Hash(File.ReadAllBytes(target))==SysHash)File.Delete(target);}catch(IOException){}catch(UnauthorizedAccessException){}throw;}
                finally{try{Clean(folder);}catch(IOException){}catch(UnauthorizedAccessException){}}
            }finally{if(acquired)mutex.ReleaseMutex();}}
        }
        public static void Test(string report){Resource("HIDUSBF_AS.INF",InfHash);Resource("HIDUSBF_NOPATCH.SYS",SysHash);if(ShouldInstall(true,true,true) || !ShouldInstall(true,false,false))throw new Exception("Driver installation guard failed.");bool blocked=false;try{ShouldInstall(false,false,false);}catch(InvalidOperationException){blocked=true;}if(!blocked)throw new Exception("Unsupported architecture not blocked.");blocked=false;try{ShouldInstall(true,false,true);}catch(InvalidOperationException){blocked=true;}if(!blocked)throw new Exception("Existing file would be overwritten.");
            string folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)),"driver-package-test-"+Guid.NewGuid().ToString("N"));try{Extract(folder);if(Hash(File.ReadAllBytes(Path.Combine(folder,"amd64_as","hidusbf.sys")))!=SysHash)throw new Exception("Driver extraction differs.");}finally{Clean(folder);}
            File.AppendAllText(report,"Paquete HIDUSBF: archivos oficiales íntegros, extracción sin cambios, equipos no admitidos bloqueados e instalación existente conservada. Pruebas sin instalar servicios ni tocar archivos de Windows.\r\n");
        }
    }
}

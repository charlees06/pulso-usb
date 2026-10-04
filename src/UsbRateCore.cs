using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Serialization;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace PulsoUsb {
    internal sealed class UsbDevice {
        public string Id,Name,Service,DriverKey,Kind,Problem,Hardware;
        public uint DevInst,Parent,Port,Status,ProblemCode;
        public int Speed=-1;
        public int? Interval;
        public bool Filter,InheritedFilter,Input,Composite;
        public string[] Filters=new string[0];
        public readonly List<int> DescriptorIntervals=new List<int>();
        public string SpeedText {get {return Speed==0?"Low Speed":Speed==1?"Full Speed":Speed==2?"High Speed":Speed==3?"SuperSpeed":"No disponible";}}
        public string Requested {get {return !Filter?"Sin filtro":Interval.HasValue?Rates.Describe(Speed,Interval.Value):"Valor del dispositivo";}}
    }
    internal static class Rates {
        public static int[] Options(int speed) {return speed==2?new[]{125,250,500,1000,2000,4000,8000}:(speed==0 || speed==1)?new[]{125,250,500,1000}:new int[0];}
        public static int Encode(int speed,int hz) {
            if(!Options(speed).Contains(hz))throw new ArgumentException("Frecuencia no válida para esta velocidad USB.");
            if(speed<2)return 1000/hz;
            int interval=1;while(hz<8000){hz*=2;interval++;}return interval;
        }
        public static string Describe(int speed,int interval) {
            if(interval==0)return "Valor del dispositivo";
            if(interval<0 || interval>255)return "Valor no reconocido";
            if(speed<0 || speed>2)return "bInterval="+interval+" (velocidad desconocida)";
            if(speed==2) {if(interval>16)return "bInterval="+interval;return (8000.0/Math.Pow(2,interval-1)).ToString("0.##",CultureInfo.InvariantCulture)+" Hz solicitados";}
            return (1000.0/interval).ToString("0.##",CultureInfo.InvariantCulture)+" Hz solicitados";
        }
        public static bool HasFilter(IEnumerable<string> filters) {return filters.Any(x=>String.Equals(x,"hidusbf",StringComparison.OrdinalIgnoreCase));}
        public static string[] WithFilter(IEnumerable<string> filters,bool on) {
            var values=filters.Where(x=>!String.Equals(x,"hidusbf",StringComparison.OrdinalIgnoreCase)).ToList();if(on)values.Add("hidusbf");return values.ToArray();
        }
    }
    internal static class Native {
        internal static readonly IntPtr Invalid=new IntPtr(-1);
        [StructLayout(LayoutKind.Sequential)] internal struct DevInfo {public uint Size;public Guid Class;public uint DevInst;public UIntPtr Reserved;}
        [StructLayout(LayoutKind.Sequential)] struct Interface {public uint Size;public Guid Class;public uint Flags;public UIntPtr Reserved;}
        [StructLayout(LayoutKind.Sequential)] struct PropKey {public Guid Format;public uint Id;}
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern IntPtr SetupDiGetClassDevs(IntPtr cls,string enumerator,IntPtr parent,uint flags);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr SetupDiGetClassDevs(ref Guid cls,string enumerator,IntPtr parent,uint flags);
        [DllImport("setupapi.dll",SetLastError=true)] internal static extern bool SetupDiEnumDeviceInfo(IntPtr set,uint index,ref DevInfo info);
        [DllImport("setupapi.dll")] internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupDiGetDeviceInstanceId(IntPtr set,ref DevInfo info,StringBuilder value,uint length,out uint required);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupDiGetDeviceRegistryProperty(IntPtr set,ref DevInfo info,uint property,out uint type,byte[] data,uint length,out uint needed);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern bool SetupDiSetDeviceRegistryProperty(IntPtr set,ref DevInfo info,uint property,byte[] data,uint length);
        [DllImport("setupapi.dll",SetLastError=true)] internal static extern IntPtr SetupDiOpenDevRegKey(IntPtr set,ref DevInfo info,uint scope,uint profile,uint kind,uint access);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern bool SetupDiOpenDeviceInfo(IntPtr set,string id,IntPtr parent,uint flags,ref DevInfo info);
        [DllImport("setupapi.dll",SetLastError=true)] internal static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr cls,IntPtr parent);
        [DllImport("setupapi.dll",SetLastError=true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr info,ref Guid cls,uint index,ref Interface data);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref Interface data,IntPtr detail,uint length,out uint needed,ref DevInfo info);
        [DllImport("cfgmgr32.dll")] internal static extern uint CM_Get_Parent(out uint parent,uint child,uint flags);
        [DllImport("cfgmgr32.dll")] static extern uint CM_Get_DevNode_Status(out uint status,out uint problem,uint node,uint flags);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern uint CM_Get_DevNode_Property(uint node,ref PropKey key,out uint type,byte[] buffer,ref uint length,uint flags);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFile(string path,uint access,uint sharing,IntPtr security,uint disposition,uint flags,IntPtr template);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool DeviceIoControl(SafeFileHandle file,uint code,byte[] input,uint inputLength,byte[] output,uint outputLength,out uint returned,IntPtr overlapped);
        public static DevInfo NewInfo() {return new DevInfo {Size=(uint)Marshal.SizeOf(typeof(DevInfo))};}
        static byte[] GetRaw(IntPtr set,ref DevInfo info,uint property,bool strict) {
            var buffer=new byte[16384];uint type,needed;
            if(SetupDiGetDeviceRegistryProperty(set,ref info,property,out type,buffer,(uint)buffer.Length,out needed))return buffer.Take((int)needed).ToArray();
            int error=Marshal.GetLastWin32Error();if(strict && error!=13)throw new Win32Exception(error);return new byte[0];
        }
        public static string GetText(IntPtr set,ref DevInfo info,uint property) {return Encoding.Unicode.GetString(GetRaw(set,ref info,property,false)).TrimEnd('\0');}
        public static string[] ReadFilters(IntPtr set,ref DevInfo info) {return Encoding.Unicode.GetString(GetRaw(set,ref info,18,true)).Split(new[]{'\0'},StringSplitOptions.RemoveEmptyEntries);}
        public static string DeviceId(IntPtr set,ref DevInfo info) {var b=new StringBuilder(4096);uint need;if(!SetupDiGetDeviceInstanceId(set,ref info,b,(uint)b.Capacity,out need))throw new Win32Exception();return b.ToString();}
        static string BusName(uint node) {var key=new PropKey{Format=new Guid("540b947e-8b40-45bc-a8a2-6a0b894cbda2"),Id=4};var buffer=new byte[4096];uint size=(uint)buffer.Length,type;return CM_Get_DevNode_Property(node,ref key,out type,buffer,ref size,0)==0?Encoding.Unicode.GetString(buffer,0,(int)size).TrimEnd('\0'):"";}
        public static RegistryKey SoftwareKey(IntPtr set,ref DevInfo info,bool write) {
            IntPtr raw=SetupDiOpenDevRegKey(set,ref info,1,0,2,write?3u:1u);
            if(raw==Invalid)throw new Win32Exception(Marshal.GetLastWin32Error(),"No se pudo abrir la configuración del dispositivo.");
            return RegistryKey.FromHandle(new SafeRegistryHandle(raw,true),RegistryView.Registry64);
        }
        public static int? ReadInterval(RegistryKey key) {
            object value=key.GetValue("bInterval");if(value==null)return null;
            if(key.GetValueKind("bInterval")!=RegistryValueKind.DWord)throw new InvalidOperationException("bInterval tiene un formato que esta versión no modifica.");
            int i=(int)value;if(i<0 || i>255)throw new InvalidOperationException("bInterval está fuera del rango reconocido.");return i;
        }
        static Dictionary<uint,string> Hubs() {
            var found=new Dictionary<uint,string>();Guid cls=new Guid("f18a0e88-c30c-11d0-8815-00a0c906bed8");IntPtr set=SetupDiGetClassDevs(ref cls,null,IntPtr.Zero,18);if(set==Invalid)return found;
            try {for(uint i=0;;i++) {var face=new Interface{Size=(uint)Marshal.SizeOf(typeof(Interface))};if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref cls,i,ref face))break;var info=NewInfo();uint needed;SetupDiGetDeviceInterfaceDetail(set,ref face,IntPtr.Zero,0,out needed,ref info);if(needed<8 || needed>65536)continue;
                IntPtr detail=Marshal.AllocHGlobal((int)needed);try{Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);if(SetupDiGetDeviceInterfaceDetail(set,ref face,detail,needed,out needed,ref info))found[info.DevInst]=Marshal.PtrToStringUni(IntPtr.Add(detail,4));}finally{Marshal.FreeHGlobal(detail);}
            }}finally{SetupDiDestroyDeviceInfoList(set);}return found;
        }
        static void ReadConnection(UsbDevice device,Dictionary<uint,string> hubs) {
            if(device.Port==0 || !hubs.ContainsKey(device.Parent))return;
            // These are read-only hub queries; zero desired access works on standard Windows hubs.
            using(var file=CreateFile(hubs[device.Parent],0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
                if(file.IsInvalid)return;var data=new byte[4096];Array.Copy(BitConverter.GetBytes(device.Port),data,4);uint got;
                if(!DeviceIoControl(file,0x220448,data,(uint)data.Length,data,(uint)data.Length,out got,IntPtr.Zero) || got<35 || BitConverter.ToUInt32(data,31)!=1 || data[24]!=0)return;
                device.Speed=data[23];uint pipes=BitConverter.ToUInt32(data,27);
                for(int i=0;i<Math.Min(32,pipes);i++){int offset=35+i*11;if(offset+11>got)break;if((data[offset+2]&128)!=0 && (data[offset+3]&3)==3)device.DescriptorIntervals.Add(data[offset+6]);}
                var v2=new byte[16];Array.Copy(BitConverter.GetBytes(device.Port),v2,4);v2[4]=16;v2[8]=4;
                if(DeviceIoControl(file,0x22045c,v2,16,v2,16,out got,IntPtr.Zero) && got>=16 && (v2[12]&5)!=0)device.Speed=3;
            }
        }
        public static List<UsbDevice> Scan() {
            var all=new List<UsbDevice>();IntPtr set=SetupDiGetClassDevs(IntPtr.Zero,null,IntPtr.Zero,6);if(set==Invalid)throw new Win32Exception();
            try{for(uint i=0;;i++){var info=NewInfo();if(!SetupDiEnumDeviceInfo(set,i,ref info))break;string id=DeviceId(set,ref info);if(!id.StartsWith("USB\\",StringComparison.OrdinalIgnoreCase) && !id.StartsWith("HID\\",StringComparison.OrdinalIgnoreCase))continue;
                string hardware=GetText(set,ref info,1)+" "+GetText(set,ref info,2),cls=GetText(set,ref info,7),service=GetText(set,ref info,4);
                string kind="";if(cls.Equals("Mouse",StringComparison.OrdinalIgnoreCase))kind="Mouse";else if(cls.Equals("Keyboard",StringComparison.OrdinalIgnoreCase))kind="Teclado";
                if(hardware.IndexOf("HID_DEVICE_SYSTEM_GAME",StringComparison.OrdinalIgnoreCase)>=0 || Regex.IsMatch(service,"^(xusb|xboxgip)",RegexOptions.IgnoreCase))kind="Mando";
                uint parent;CM_Get_Parent(out parent,info.DevInst,0);var d=new UsbDevice{Id=id,DevInst=info.DevInst,Parent=parent,Name=BusName(info.DevInst),Kind=kind,Input=kind!="",Service=service,DriverKey=GetText(set,ref info,9)};
                if(String.IsNullOrWhiteSpace(d.Name))d.Name=GetText(set,ref info,12);if(String.IsNullOrWhiteSpace(d.Name))d.Name=GetText(set,ref info,0);
                CM_Get_DevNode_Status(out d.Status,out d.ProblemCode,d.DevInst,0);
                if(id.StartsWith("USB\\VID_",StringComparison.OrdinalIgnoreCase)) {
                    d.Hardware=id.Split('\\')[1];d.Composite=String.Equals(service,"usbccgp",StringComparison.OrdinalIgnoreCase);
                    byte[] address=GetRaw(set,ref info,28,false);if(address.Length>=4)d.Port=BitConverter.ToUInt32(address,0);
                    try{d.Filters=ReadFilters(set,ref info);d.Filter=Rates.HasFilter(d.Filters);using(var key=SoftwareKey(set,ref info,false))d.Interval=ReadInterval(key);}catch(Exception ex){d.Problem=ex.Message;}
                }
                all.Add(d);
            }}finally{SetupDiDestroyDeviceInfoList(set);}
            var index=all.ToDictionary(x=>x.DevInst,x=>x);
            foreach(var child in all.Where(x=>x.Input).ToArray()) {
                if(Regex.IsMatch(child.Id,@"^USB\\VID_[0-9A-F]{4}&PID_[0-9A-F]{4}\\",RegexOptions.IgnoreCase))continue;
                uint cursor=child.Parent;
                for(int step=0;step<12 && index.ContainsKey(cursor);step++){
                    var parent=index[cursor];if(parent.Service.StartsWith("USBHUB",StringComparison.OrdinalIgnoreCase))break;
                    parent.Input=true;if(parent.Kind=="")parent.Kind=child.Kind;else if(!parent.Kind.Split('/').Contains(child.Kind))parent.Kind+="/"+child.Kind;
                    if(Regex.IsMatch(parent.Id,@"^USB\\VID_[0-9A-F]{4}&PID_[0-9A-F]{4}\\",RegexOptions.IgnoreCase))break;
                    cursor=parent.Parent;
                }
            }
            var hubs=Hubs();var roots=all.Where(x=>Regex.IsMatch(x.Id,@"^USB\\VID_[0-9A-F]{4}&PID_[0-9A-F]{4}\\",RegexOptions.IgnoreCase)).ToList();
            foreach(var d in roots) {
                ReadConnection(d,hubs);
                // Do not layer a parent filter on top of a filter already attached to one of its interfaces.
                foreach(var sub in all.Where(x=>x.Filter && x.DevInst!=d.DevInst)) {uint node=sub.Parent;for(int step=0;step<12 && index.ContainsKey(node);step++){if(node==d.DevInst){d.InheritedFilter=true;break;}node=index[node].Parent;}}
                if(d.ProblemCode!=0)d.Problem="Windows informa un problema de dispositivo ("+d.ProblemCode+").";
            }
            return roots.OrderByDescending(x=>x.Input).ThenBy(x=>x.Name).ToList();
        }
    }
    internal sealed class DeviceHandle : IDisposable {
        public IntPtr Set;public Native.DevInfo Info;
        public DeviceHandle(string id) {Set=Native.SetupDiCreateDeviceInfoList(IntPtr.Zero,IntPtr.Zero);if(Set==Native.Invalid)throw new Win32Exception();Info=Native.NewInfo();if(!Native.SetupDiOpenDeviceInfo(Set,id,IntPtr.Zero,0,ref Info)){int code=Marshal.GetLastWin32Error();Dispose();throw new Win32Exception(code);}}
        public void Dispose(){if(Set!=IntPtr.Zero && Set!=Native.Invalid){Native.SetupDiDestroyDeviceInfoList(Set);Set=IntPtr.Zero;}}
    }
    public sealed class Backup {
        public string DeviceId,DriverKey,CreatedUtc;
        public bool HadFilter;
        public int? Interval;
    }
    internal interface IDeviceStore {
        string[] GetFilters();int? GetInterval();void SetFilters(string[] values);void SetInterval(int? value);
    }
    internal sealed class WindowsStore : IDeviceStore,IDisposable {
        readonly DeviceHandle handle;readonly RegistryKey key;
        public WindowsStore(string id){handle=new DeviceHandle(id);try{key=Native.SoftwareKey(handle.Set,ref handle.Info,true);}catch{handle.Dispose();throw;}}
        public string[] GetFilters(){return Native.ReadFilters(handle.Set,ref handle.Info);}
        public int? GetInterval(){return Native.ReadInterval(key);}
        public void SetInterval(int? interval){if(interval.HasValue)key.SetValue("bInterval",interval.Value,RegistryValueKind.DWord);else key.DeleteValue("bInterval",false);key.Flush();}
        public void SetFilters(string[] filters){byte[] bytes=filters.Length==0?null:Encoding.Unicode.GetBytes(String.Join("\0",filters)+"\0\0");if(!Native.SetupDiSetDeviceRegistryProperty(handle.Set,ref handle.Info,18,bytes,bytes==null?0:(uint)bytes.Length))throw new Win32Exception(Marshal.GetLastWin32Error());}
        public void Dispose(){key.Dispose();handle.Dispose();}
    }
    internal static class Changes {
        public static string BackupFolder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"copias");
        public static bool DriverInstalled(){try{using(var c=new ServiceController("hidusbf")){var s=c.Status;return true;}}catch{return false;}}
        public static string DriverStatus(){try{using(var c=new ServiceController("hidusbf"))return "HIDUSBF instalado · "+(c.Status==ServiceControllerStatus.Running?"activo":"detenido");}catch{return "HIDUSBF no instalado";}}
        public static string BackupPath(string id){using(var hash=SHA256.Create())return Path.Combine(BackupFolder,BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(id.ToUpperInvariant()))).Replace("-","")+".xml");}
        public static Backup LoadBackup(string id){string path=BackupPath(id);if(!File.Exists(path))return null;using(var stream=File.OpenRead(path)){var b=(Backup)new XmlSerializer(typeof(Backup)).Deserialize(stream);if(!String.Equals(id,b.DeviceId,StringComparison.OrdinalIgnoreCase) || (b.Interval.HasValue && (b.Interval<0 || b.Interval>255)))throw new InvalidOperationException("Copia no válida para este dispositivo.");return b;}}
        internal static void SaveBackup(UsbDevice d,IDeviceStore store){Directory.CreateDirectory(BackupFolder);string path=BackupPath(d.Id);if(File.Exists(path)){var old=LoadBackup(d.Id);if(old.DriverKey!=d.DriverKey)throw new InvalidOperationException("La instalación del dispositivo cambió desde la copia anterior. No se aplicó nada.");return;}
            var b=new Backup{DeviceId=d.Id,DriverKey=d.DriverKey,CreatedUtc=DateTime.UtcNow.ToString("o"),HadFilter=Rates.HasFilter(store.GetFilters()),Interval=store.GetInterval()};
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){new XmlSerializer(typeof(Backup)).Serialize(stream,b);stream.Flush(true);}File.Move(temporary,path);}finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        public static void Transact(IDeviceStore store,bool enable,int? interval) {
            string[] oldFilters=store.GetFilters();int? oldInterval=store.GetInterval();
            try{store.SetInterval(interval);store.SetFilters(Rates.WithFilter(oldFilters,enable));if(store.GetInterval()!=interval || !store.GetFilters().SequenceEqual(Rates.WithFilter(oldFilters,enable),StringComparer.OrdinalIgnoreCase))throw new IOException("Windows no confirmó los valores guardados.");}
            catch(Exception original){try{store.SetInterval(oldInterval);store.SetFilters(oldFilters);}catch(Exception rollback){throw new IOException("Falló el cambio y no se pudo completar la restauración. Conserva la copia y no reinicies el dispositivo. "+original.Message+" / "+rollback.Message);}throw new IOException("No se aplicó el cambio; se restauraron los valores anteriores. "+original.Message);}
        }
        public static void Apply(UsbDevice selected,bool enable,int hz,bool restore) {
            using(var mutex=new Mutex(false,@"Local\PulsoUsbDeviceChanges")) {
                bool acquired=false;try{try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
                    if(!acquired)throw new InvalidOperationException("Otra ventana de Pulso USB está aplicando un cambio.");
                    ApplyCore(selected,enable,hz,restore);
                }finally{if(acquired)mutex.ReleaseMutex();}
            }
        }
        static void ApplyCore(UsbDevice selected,bool enable,int hz,bool restore) {
            if(!IsAdministrator())throw new InvalidOperationException("Abre el programa como administrador para cambiar la frecuencia.");
            UsbDevice fresh=Native.Scan().SingleOrDefault(x=>String.Equals(x.Id,selected.Id,StringComparison.OrdinalIgnoreCase));
            if(fresh==null)throw new InvalidOperationException("El dispositivo ya no está conectado. Actualiza la lista.");
            bool recovery=restore && LoadBackup(fresh.Id)!=null;
            if(!fresh.Input && !(fresh.Filter && !enable) && !recovery)throw new InvalidOperationException("Solo se configuran dispositivos de entrada USB.");
            if(fresh.DriverKey!=selected.DriverKey)throw new InvalidOperationException("El dispositivo cambió. Actualiza la lista.");
            if(fresh.Interval!=selected.Interval || !fresh.Filters.SequenceEqual(selected.Filters,StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("La configuración cambió desde la última lectura. Actualiza la lista antes de modificarla.");
            if(fresh.InheritedFilter)throw new InvalidOperationException("Una interfaz de esta unidad ya tiene HIDUSBF. Adminístrala con Setup oficial para evitar filtros superpuestos.");
            bool desiredFilter=enable;int? interval=null;
            if(restore){var b=LoadBackup(fresh.Id);if(b==null || b.DriverKey!=fresh.DriverKey)throw new InvalidOperationException("No hay una copia compatible para este dispositivo.");desiredFilter=b.HadFilter;interval=b.Interval;}
            else if(enable){if(!String.IsNullOrEmpty(fresh.Problem))throw new InvalidOperationException(fresh.Problem);interval=Rates.Encode(fresh.Speed,hz);}
            if(desiredFilter){if(!DriverInstalled())throw new InvalidOperationException("Instala HIDUSBF desde su proyecto oficial antes de aplicar el filtro.");using(var service=new ServiceController("hidusbf")){if(service.Status==ServiceControllerStatus.Stopped)service.Start();service.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(10));}}
            using(var store=new WindowsStore(fresh.Id)){SaveBackup(fresh,store);Transact(store,desiredFilter,interval);}
        }
        public static bool IsAdministrator(){using(var identity=System.Security.Principal.WindowsIdentity.GetCurrent())return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);}
    }
    internal static class CoreTests {
        sealed class Fake : IDeviceStore {
            public string[] Filters=new[]{"otroFiltro","HiDuSbF"};public int? Interval=8;public bool FailNext;
            public string[] GetFilters(){return Filters.ToArray();}public int? GetInterval(){return Interval;}
            public void SetInterval(int? value){Interval=value;}
            public void SetFilters(string[] values){if(FailNext){FailNext=false;throw new IOException("Fallo simulado");}Filters=values;}
        }
        static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
        public static void Run(string path){
            Assert(Rates.Encode(1,125)==8 && Rates.Encode(1,1000)==1,"Codificación Full Speed incorrecta");
            Assert(Rates.Encode(2,125)==7 && Rates.Encode(2,1000)==4 && Rates.Encode(2,8000)==1,"Codificación High Speed incorrecta");
            bool rejected=false;try{Rates.Encode(-1,1000);}catch(ArgumentException){rejected=true;}Assert(rejected,"Se aceptó velocidad desconocida");
            rejected=false;try{Rates.Encode(1,8000);}catch(ArgumentException){rejected=true;}Assert(rejected,"Se aceptó 8000 Hz en Full Speed");
            var fake=new Fake();Changes.Transact(fake,true,1);Assert(fake.Interval==1 && fake.Filters.SequenceEqual(new[]{"otroFiltro","hidusbf"}),"No conserva los otros filtros");
            Changes.Transact(fake,false,null);Assert(fake.Interval==null && fake.Filters.SequenceEqual(new[]{"otroFiltro"}),"No desactiva de forma selectiva");
            fake=new Fake{FailNext=true};rejected=false;try{Changes.Transact(fake,true,1);}catch(IOException){rejected=true;}Assert(rejected && fake.Interval==8 && fake.Filters.SequenceEqual(new[]{"otroFiltro","HiDuSbF"}),"Falló el rollback");
            string savedFolder=Changes.BackupFolder;
            try {
                Changes.BackupFolder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)),"usb-test-backups",Guid.NewGuid().ToString("N"));
                var simulatedDevice=new UsbDevice{Id=@"USB\VID_0000&PID_0000\TEST",DriverKey="simulado"};fake=new Fake();Changes.SaveBackup(simulatedDevice,fake);
                var snapshot=Changes.LoadBackup(simulatedDevice.Id);Assert(snapshot.Interval==8 && snapshot.HadFilter,"La copia no conserva el estado inicial");
                Changes.Transact(fake,false,null);Changes.SaveBackup(simulatedDevice,fake);snapshot=Changes.LoadBackup(simulatedDevice.Id);Assert(snapshot.Interval==8 && snapshot.HadFilter,"Se sobrescribió la copia original");
                Changes.Transact(fake,snapshot.HadFilter,snapshot.Interval);Assert(fake.Interval==8 && Rates.HasFilter(fake.Filters) && fake.Filters.Contains("otroFiltro"),"No se restauró la copia");
            } finally {Changes.BackupFolder=savedFolder;}
            var devices=Native.Scan();var report=new StringBuilder("PRUEBAS CORRECTAS · No se modificó ningún dispositivo real.\r\n"+Changes.DriverStatus()+"\r\n");
            Assert(!devices.Any(x=>x.Input && x.Service.StartsWith("USBHUB",StringComparison.OrdinalIgnoreCase)),"Un hub se clasificó como dispositivo de entrada");
            foreach(var d in devices)report.AppendLine(d.Name+" | "+d.Kind+" | "+d.Hardware+" | "+d.SpeedText+" | "+d.Requested+" | bInterval="+d.Interval+" | endpoint="+String.Join(",",d.DescriptorIntervals)+" | "+d.Problem);
            File.WriteAllText(path,report.ToString(),new UTF8Encoding(true));
        }
    }
}

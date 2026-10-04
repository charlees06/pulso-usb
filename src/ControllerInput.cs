using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PulsoUsb {
    internal sealed class PadAxis {
        public ushort Page,Link,Usage,Bits; public byte Report; public int Min,Max;
        public string Name; public bool Hat,Valid; public long Raw;
        public double Unit {get{return Math.Max(0,Math.Min(1,(Raw-(double)Min)/Math.Max(1,(double)Max-Min)));}}
        public double Centered {get{return Unit*2-1;}}
    }
    internal sealed class PadButton {
        public ushort Page,Link,Usage; public byte Report; public bool Down,Seen;
        public string Name;
    }
    internal sealed class PadDevice : IDisposable {
        public string Id,Name; public IntPtr Handle,Preparsed; public int Slot=-1;
        public bool Sony,HasInput; public long Reports; public int ReportLength;
        public readonly List<PadAxis> Axes=new List<PadAxis>();
        public readonly List<PadButton> Buttons=new List<PadButton>();
        public override string ToString(){return L.T(Name);}
        public void ClearLive(){HasInput=false;foreach(var b in Buttons)b.Down=false;foreach(var a in Axes)a.Valid=false;}
        public void ResetHistory(){foreach(var b in Buttons)b.Seen=false;}
        public PadAxis Axis(ushort usage){return Axes.FirstOrDefault(x=>x.Page==1 && x.Usage==usage);}
        public bool Pressed(string label){return Buttons.Any(b=>b.Name==label && b.Down);}
        public void Dispose(){if(Preparsed!=IntPtr.Zero){Marshal.FreeHGlobal(Preparsed);Preparsed=IntPtr.Zero;}}
    }
    // Input only: no output reports, calibration, driver or device-setting writes.
    internal sealed class ControllerInput : IDisposable {
        const int Success=0x110000; const uint Error=0xFFFFFFFF;
        [StructLayout(LayoutKind.Sequential)] internal struct RawDevice {public IntPtr Handle;public uint Type;}
        [StructLayout(LayoutKind.Sequential)] struct Registration {public ushort Page,Usage;public uint Flags;public IntPtr Target;}
        [StructLayout(LayoutKind.Explicit,Size=64)] struct Caps {
            [FieldOffset(0)]public ushort Usage;[FieldOffset(2)]public ushort Page;[FieldOffset(4)]public ushort InputLength;
            [FieldOffset(46)]public ushort Buttons;[FieldOffset(48)]public ushort Values;
        }
        [StructLayout(LayoutKind.Explicit,Size=72)] struct ValueCap {
            [FieldOffset(0)]public ushort Page;[FieldOffset(2)]public byte Report;[FieldOffset(6)]public ushort Link;
            [FieldOffset(12)]public byte Range;[FieldOffset(15)]public byte Absolute;[FieldOffset(18)]public ushort Bits;
            [FieldOffset(40)]public int Min;[FieldOffset(44)]public int Max;
            [FieldOffset(56)]public ushort First;[FieldOffset(58)]public ushort Last;
        }
        [StructLayout(LayoutKind.Explicit,Size=72)] struct ButtonCap {
            [FieldOffset(0)]public ushort Page;[FieldOffset(2)]public byte Report;[FieldOffset(6)]public ushort Link;
            [FieldOffset(12)]public byte Range;[FieldOffset(56)]public ushort First;[FieldOffset(58)]public ushort Last;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct XboxState {
            public uint Packet;public ushort Buttons;public byte LeftTrigger,RightTrigger;public short LX,LY,RX,RY;
        }
        [DllImport("user32.dll",SetLastError=true)] static extern uint GetRawInputDeviceList([In,Out] RawDevice[] list,ref uint count,uint size);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern uint GetRawInputDeviceInfo(IntPtr device,uint command,IntPtr data,ref uint size);
        [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterRawInputDevices(Registration[] devices,uint count,uint size);
        [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr input,uint command,IntPtr data,ref uint size,uint headerSize);
        [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data,out Caps caps);
        [DllImport("hid.dll")] static extern int HidP_GetValueCaps(int type,[Out] ValueCap[] caps,ref ushort count,IntPtr data);
        [DllImport("hid.dll")] static extern int HidP_GetButtonCaps(int type,[Out] ButtonCap[] caps,ref ushort count,IntPtr data);
        [DllImport("hid.dll")] static extern int HidP_GetUsageValue(int type,ushort page,ushort link,ushort usage,out uint value,IntPtr data,byte[] report,uint length);
        [DllImport("hid.dll")] static extern int HidP_GetUsages(int type,ushort page,ushort link,[Out] ushort[] usages,ref uint count,IntPtr data,byte[] report,uint length);
        [DllImport("hid.dll",CharSet=CharSet.Unicode)] [return:MarshalAs(UnmanagedType.U1)] static extern bool HidD_GetProductString(SafeFileHandle device,StringBuilder text,uint bytes);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
        [DllImport("xinput1_4.dll")] static extern uint XInputGetState(uint index,out XboxState state);
        public readonly List<PadDevice> Devices=new List<PadDevice>();
        public string ErrorText; bool registered;
        public void Start(IntPtr window){if(registered)return;var registrations=new[]{new Registration{Page=1,Usage=4,Target=window},new Registration{Page=1,Usage=5,Target=window},new Registration{Page=1,Usage=8,Target=window}};registered=RegisterRawInputDevices(registrations,3,(uint)Marshal.SizeOf(typeof(Registration)));ErrorText=registered?null:"No se pudo iniciar la lectura del mando.";Refresh();}
        public void Stop(){if(registered){var registrations=new[]{new Registration{Page=1,Usage=4,Flags=1},new Registration{Page=1,Usage=5,Flags=1},new Registration{Page=1,Usage=8,Flags=1}};RegisterRawInputDevices(registrations,3,(uint)Marshal.SizeOf(typeof(Registration)));registered=false;}foreach(var d in Devices)d.ClearLive();}
        static string PathOf(IntPtr handle){uint length=0;GetRawInputDeviceInfo(handle,0x20000007,IntPtr.Zero,ref length);if(length==0 || length>32768)return null;IntPtr p=Marshal.AllocHGlobal((int)(length+1)*2);try{if(GetRawInputDeviceInfo(handle,0x20000007,p,ref length)==Error)return null;return Marshal.PtrToStringUni(p,(int)length).TrimEnd('\0');}finally{Marshal.FreeHGlobal(p);}}
        static PadDevice Open(RawDevice raw,string path){var device=new PadDevice{Handle=raw.Handle,Id=path,Name="Mando HID"};try{
            uint length=0;GetRawInputDeviceInfo(raw.Handle,0x20000005,IntPtr.Zero,ref length);if(length==0 || length>1048576)return null;
            device.Preparsed=Marshal.AllocHGlobal((int)length);if(GetRawInputDeviceInfo(raw.Handle,0x20000005,device.Preparsed,ref length)==Error)return null;
            Caps caps;if(HidP_GetCaps(device.Preparsed,out caps)!=Success || caps.Page!=1 || (caps.Usage!=4 && caps.Usage!=5 && caps.Usage!=8))return null;
            device.ReportLength=caps.InputLength;device.Sony=path.IndexOf("VID_054C",StringComparison.OrdinalIgnoreCase)>=0 && new[]{"PID_0DF2","PID_0CE6","PID_05C4","PID_09CC"}.Any(p=>path.IndexOf(p,StringComparison.OrdinalIgnoreCase)>=0);
            using(var handle=CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)){if(!handle.IsInvalid){var name=new StringBuilder(256);if(HidD_GetProductString(handle,name,512) && name.Length>0)device.Name=name.ToString();}}
            if(caps.Values>512 || caps.Buttons>512)return null;
            var values=new ValueCap[caps.Values];ushort count=caps.Values;
            if(count>0 && HidP_GetValueCaps(0,values,ref count,device.Preparsed)==Success)foreach(var cap in values.Take(count)){
                int last=cap.Range!=0?cap.Last:cap.First;if(cap.Absolute==0 || cap.Max<=cap.Min || cap.Bits==0 || cap.Bits>32 || last-cap.First>64)continue;
                for(int usage=cap.First;usage<=last;usage++)if((cap.Page==1 && usage>=0x30 && usage<=0x39) || (cap.Page==2 && (usage==0xC4 || usage==0xC5))){
                    if(device.Axes.Any(a=>a.Page==cap.Page && a.Link==cap.Link && a.Usage==usage && a.Report==cap.Report))continue;
                    device.Axes.Add(new PadAxis{Page=cap.Page,Link=cap.Link,Usage=(ushort)usage,Report=cap.Report,Min=cap.Min,Max=cap.Max,Bits=cap.Bits,Hat=cap.Page==1 && usage==0x39,Name=cap.Page==1?new[]{"X","Y","Z","RX","RY","RZ","Slider","Dial","Wheel","D-pad"}[usage-0x30]:usage==0xC4?"LT":"RT"});
                }
            }
            var buttons=new ButtonCap[caps.Buttons];count=caps.Buttons;
            if(count>0 && HidP_GetButtonCaps(0,buttons,ref count,device.Preparsed)==Success)foreach(var cap in buttons.Take(count)){
                int last=cap.Range!=0?cap.Last:cap.First;if(cap.Page!=9 || last-cap.First>255)continue;
                for(int usage=cap.First;usage<=last && device.Buttons.Count<256;usage++){
                    if(device.Buttons.Any(b=>b.Page==cap.Page && b.Link==cap.Link && b.Usage==usage && b.Report==cap.Report))continue;
                    string[] sony={"□","×","○","△","L1","R1","L2","R2","Create","Options","L3","R3","PS","Touch","Mute"};
                    device.Buttons.Add(new PadButton{Page=cap.Page,Link=cap.Link,Usage=(ushort)usage,Report=cap.Report,Name=device.Sony && usage>0 && usage<=sony.Length?sony[usage-1]:"B"+usage});
                }
            }
            if(device.Axes.Count==0 && device.Buttons.Count==0)return null;
            var result=device;device=null;return result;
        }finally{if(device!=null)device.Dispose();}}
        public bool Refresh(){var alive=new HashSet<string>(StringComparer.OrdinalIgnoreCase);bool changed=false;
            uint count=0;uint itemSize=(uint)Marshal.SizeOf(typeof(RawDevice));bool scanned=GetRawInputDeviceList(null,ref count,itemSize)!=Error && count<4096;
            if(scanned){var raw=new RawDevice[count];uint actual=GetRawInputDeviceList(raw,ref count,itemSize);scanned=actual!=Error;if(scanned)foreach(var item in raw.Take((int)actual).Where(d=>d.Type==2)){
                string path=PathOf(item.Handle);if(path==null || path.IndexOf("IG_",StringComparison.OrdinalIgnoreCase)>=0)continue;alive.Add(path);
                var old=Devices.FirstOrDefault(d=>String.Equals(d.Id,path,StringComparison.OrdinalIgnoreCase));if(old!=null && old.Handle==item.Handle)continue;
                if(old!=null){old.Dispose();Devices.Remove(old);changed=true;}var device=Open(item,path);if(device!=null){Devices.Add(device);changed=true;}
            }}
            for(uint slot=0;slot<4;slot++){XboxState state;if(XInputGetState(slot,out state)!=0)continue;string id="xinput:"+slot;alive.Add(id);var device=Devices.FirstOrDefault(d=>d.Id==id);if(device==null){device=CreateXbox((int)slot);Devices.Add(device);changed=true;}}
            foreach(var old in Devices.Where(d=>!alive.Contains(d.Id) && (scanned || d.Slot>=0)).ToArray()){old.Dispose();Devices.Remove(old);changed=true;}return changed;
        }
        internal static PadDevice CreateXbox(int slot){var d=new PadDevice{Id="xinput:"+slot,Name="Xbox / XInput · "+(slot+1),Slot=slot};
            string[] labels={"↑","↓","←","→","Menu","View","L3","R3","LB","RB","A","B","X","Y"};ushort[] bits={1,2,4,8,16,32,64,128,256,512,4096,8192,16384,32768};for(int i=0;i<bits.Length;i++)d.Buttons.Add(new PadButton{Name=labels[i],Usage=bits[i]});
            for(ushort i=0;i<6;i++)d.Axes.Add(new PadAxis{Page=1,Usage=(ushort)(0x30+i),Name=new[]{"X","Y","Z","RX","RY","RZ"}[i],Min=(i==2 || i==5)?0:-32768,Max=(i==2 || i==5)?255:32767,Bits=16});return d;
        }
        internal static void ApplyXbox(PadDevice d,XboxState state){foreach(var b in d.Buttons){b.Down=(state.Buttons & b.Usage)!=0;b.Seen|=b.Down;}long[] values={state.LX,InvertSigned(state.LY),state.LeftTrigger,state.RX,InvertSigned(state.RY),state.RightTrigger};for(int i=0;i<6;i++){d.Axes[i].Raw=values[i];d.Axes[i].Valid=true;}d.HasInput=true;d.Reports++;}
        internal static int InvertSigned(short value){return -1-(int)value;}
        public void PollXbox(){foreach(var d in Devices.Where(d=>d.Slot>=0)){XboxState state;if(XInputGetState((uint)d.Slot,out state)==0)ApplyXbox(d,state);else d.ClearLive();}}
        internal static long Decode(uint value,int bits,int minimum){if(minimum<0){if(bits==32)return unchecked((int)value);uint mask=(1u<<bits)-1;value &= mask;return (value & (1u<<(bits-1)))!=0?(long)value-(1L<<bits):value;}return value;}
        internal static int HatDirection(PadAxis a){if(a==null || !a.Valid || a.Raw<a.Min || a.Raw>a.Max)return -1;long span=(long)a.Max-a.Min+1;return span==8?(int)(a.Raw-a.Min):span==4?(int)(a.Raw-a.Min)*2:-1;}
        public void ProcessReport(PadDevice d,byte[] report){if(d.Preparsed==IntPtr.Zero || report.Length==0)return;bool valid=false;foreach(var a in d.Axes){if(a.Report!=report[0])continue;uint raw;if(HidP_GetUsageValue(0,a.Page,a.Link,a.Usage,out raw,d.Preparsed,report,(uint)report.Length)==Success){a.Raw=Decode(raw,a.Bits,a.Min);a.Valid=a.Hat || (a.Raw>=a.Min && a.Raw<=a.Max);valid|=a.Valid;}}
            foreach(var group in d.Buttons.Where(b=>b.Report==report[0]).GroupBy(b=>new{b.Page,b.Link})){var usages=new ushort[512];uint count=(uint)usages.Length;if(HidP_GetUsages(0,group.Key.Page,group.Key.Link,usages,ref count,d.Preparsed,report,(uint)report.Length)!=Success)continue;var down=new HashSet<ushort>(usages.Take((int)count));foreach(var button in group){button.Down=down.Contains(button.Usage);button.Seen|=button.Down;}valid=true;}
            if(valid){d.HasInput=true;d.Reports++;}
        }
        internal static bool PacketFits(uint total,uint header,uint size,uint count){return size>0 && count>0 && size<=65536 && count<=4096 && (ulong)header+8+(ulong)size*count<=total;}
        public void Input(IntPtr input){if(!registered)return;uint header=(uint)(8+IntPtr.Size*2),length=0;if(GetRawInputData(input,0x10000003,IntPtr.Zero,ref length,header)==Error || length<header+8 || length>1048576)return;IntPtr memory=Marshal.AllocHGlobal((int)length);try{
            uint actual=GetRawInputData(input,0x10000003,memory,ref length,header);if(actual==Error || actual<header+8 || Marshal.ReadInt32(memory)!=2)return;var device=Devices.FirstOrDefault(d=>d.Handle==Marshal.ReadIntPtr(memory,8));if(device==null)return;
            uint size=unchecked((uint)Marshal.ReadInt32(memory,(int)header)),count=unchecked((uint)Marshal.ReadInt32(memory,(int)header+4));if(!PacketFits(actual,header,size,count))return;
            var report=new byte[size];for(uint i=0;i<count;i++){Marshal.Copy(IntPtr.Add(memory,(int)(header+8+i*size)),report,0,(int)size);ProcessReport(device,report);}
        }finally{Marshal.FreeHGlobal(memory);}}
        public void Dispose(){Stop();foreach(var d in Devices)d.Dispose();Devices.Clear();}
    }
}

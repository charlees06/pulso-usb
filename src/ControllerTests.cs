using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Reflection;

namespace PulsoUsb {
    internal static class ControllerTests {
        [DllImport("hid.dll")] static extern int HidP_InitializeReportForID(int type,byte id,IntPtr data,[In,Out] byte[] report,uint length);
        [DllImport("hid.dll")] static extern int HidP_SetUsageValue(int type,ushort page,ushort link,ushort usage,uint value,IntPtr data,[In,Out] byte[] report,uint length);
        [DllImport("hid.dll")] static extern int HidP_SetUsages(int type,ushort page,ushort link,ushort[] usages,ref uint count,IntPtr data,[In,Out] byte[] report,uint length);
        static void Check(bool ok,string description){if(!ok)throw new Exception("Controller: "+description);}
        public static void Run(string report){
            Check(ControllerInput.Decode(255,8,-128)==-1,"8-bit sign extension");Check(ControllerInput.Decode(128,8,-128)==-128,"8-bit minimum");Check(ControllerInput.Decode(65535,16,-32768)==-1,"16-bit sign extension");Check(ControllerInput.Decode(0x80000000,32,Int32.MinValue)==Int32.MinValue,"32-bit minimum");Check(ControllerInput.Decode(255,8,0)==255,"unsigned trigger");
            var axis=new PadAxis{Min=0,Max=255,Raw=0};Check(axis.Centered==-1,"axis minimum");axis.Raw=255;Check(axis.Centered==1,"axis maximum");axis.Min=-32768;axis.Max=32767;axis.Raw=0;Check(Math.Abs(axis.Centered)<.0001,"axis center");
            var hat=new PadAxis{Min=1,Max=8,Raw=8,Valid=true};Check(ControllerInput.HatDirection(hat)==7,"one-based hat");hat.Raw=0;Check(ControllerInput.HatDirection(hat)==-1,"neutral hat");hat.Min=0;hat.Max=3;hat.Raw=3;Check(ControllerInput.HatDirection(hat)==6,"four-direction hat");
            Check(ControllerInput.PacketFits(96,24,64,1),"valid raw packet");Check(!ControllerInput.PacketFits(95,24,64,1),"truncated packet rejected");Check(!ControllerInput.PacketFits(100,24,0xFFFFFFFF,0xFFFFFFFF),"overflow rejected");Check(!ControllerInput.PacketFits(96,24,64,0),"empty packet rejected");
            using(var xbox=ControllerInput.CreateXbox(0)){ControllerInput.ApplyXbox(xbox,new ControllerInput.XboxState{Buttons=4096|256,LeftTrigger=255,LX=32767,LY=-32768});Check(xbox.Pressed("A") && xbox.Pressed("LB") && !xbox.Pressed("B"),"simultaneous buttons");Check(xbox.Axis(0x32).Unit==1 && xbox.Axis(0x35).Unit==0,"independent triggers");Check(xbox.Axis(0x31).Centered==1,"inverted Y endpoint");ControllerInput.ApplyXbox(xbox,new ControllerInput.XboxState());Check(!xbox.Pressed("A") && xbox.Buttons.First(b=>b.Name=="A").Seen,"release and coverage");xbox.ClearLive();Check(!xbox.HasInput && xbox.Axes.All(a=>!a.Valid) && xbox.Buttons.All(b=>!b.Down),"pause clears stale state");xbox.ResetHistory();Check(xbox.Buttons.All(b=>!b.Seen),"history reset");}
            // These setters fill local test byte arrays only; no report is sent to hardware.
            using(var input=new ControllerInput()){input.Refresh();foreach(var d in input.Devices){File.AppendAllText(report,"Mando detectado: "+d.Name+"; botones="+d.Buttons.Count+"; ejes="+d.Axes.Count+"\r\n");if(d.Preparsed==IntPtr.Zero)continue;
                    foreach(byte id in d.Axes.Select(a=>a.Report).Concat(d.Buttons.Select(b=>b.Report)).Distinct()){
                        var bytes=new byte[d.ReportLength];if(HidP_InitializeReportForID(0,id,d.Preparsed,bytes,(uint)bytes.Length)!=0x110000)continue;
                        var a=d.Axes.FirstOrDefault(x=>x.Report==id && !x.Hat);if(a!=null){Check(HidP_SetUsageValue(0,a.Page,a.Link,a.Usage,unchecked((uint)a.Max),d.Preparsed,bytes,(uint)bytes.Length)==0x110000,"build native test axis");}
                        var b=d.Buttons.FirstOrDefault(x=>x.Report==id);if(b!=null){uint n=1;Check(HidP_SetUsages(0,b.Page,b.Link,new[]{b.Usage},ref n,d.Preparsed,bytes,(uint)bytes.Length)==0x110000,"build native test button");}
                        input.ProcessReport(d,bytes);if(a!=null)Check(a.Valid && a.Raw==a.Max,"parse real descriptor axis");if(b!=null)Check(b.Down,"parse real descriptor button");
                        var release=new byte[d.ReportLength];Check(HidP_InitializeReportForID(0,id,d.Preparsed,release,(uint)release.Length)==0x110000,"build released report");input.ProcessReport(d,release);if(b!=null)Check(!b.Down && b.Seen,"native release preserves coverage");
                    }
                }}
            string original=L.Code;Window window;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PulsoV2.xaml"))window=(Window)XamlReader.Load(stream);
            try{var studio=new Studio(window,null);foreach(var language in L.Languages){typeof(Studio).GetMethod("ChangeLanguage",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(studio,new object[]{language.Code,false});typeof(Studio).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(studio,new object[]{"controller"});Check(((TextBlock)window.FindName("PageTitle")).Text==L.T("Probador de controles"),"tester title translated");Check(((StackPanel)window.FindName("ControllerPage")).Visibility==Visibility.Visible && ((Border)window.FindName("ReviewBar")).Visibility==Visibility.Collapsed,"tester navigation isolates configuration");}}finally{window.Close();L.Set(original);}
            File.AppendAllText(report,"Probador: rangos, botones simultáneos, gatillos independientes, cruceta, desconexión/pausa, desbordamientos y cinco idiomas correctos. Los informes HID de prueba solo se modificaron en memoria.\r\n");
        }
    }
}

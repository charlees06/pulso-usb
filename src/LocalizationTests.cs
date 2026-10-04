using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace PulsoUsb {
    internal static class LocalizationTests {
        static void Check(bool condition,string error){if(!condition)throw new Exception("Idiomas: "+error);}
        static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static object Get(object target,string field){return target.GetType().GetField(field,Private).GetValue(target);}
        static void Set(object target,string field,object value){target.GetType().GetField(field,Private).SetValue(target,value);}
        static void Call(object target,string method,params object[] args){target.GetType().GetMethod(method,Private).Invoke(target,args);}
        public static void Run(string report){string original=L.Code;L.ValidateCatalog();string xaml;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PulsoV2.xaml"))using(var reader=new StreamReader(stream))xaml=reader.ReadToEnd();Check(!xaml.Contains("LabPage") && !xaml.Contains("NavLab"),"no debe incluir el laboratorio");Check(Assembly.GetExecutingAssembly().GetType("PulsoUsb.RawMouseCapture")==null,"el motor de laboratorio no debe compilarse");
            Window window;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PulsoV2.xaml"))window=(Window)XamlReader.Load(stream);
            try{var app=new Studio(window,null);var device=new UsbDevice{Id=@"USB\VID_0000&PID_0000\LOCALE_TEST",Name="USB Keyboard",Hardware="VID_0000&PID_0000",Kind="Teclado",Input=true,Speed=1,Filter=false};Set(app,"all",new List<UsbDevice>{device});Call(app,"Select",device);Set(app,"manual",true);Set(app,"chosenHz",1000);
                foreach(var language in L.Languages){Call(app,"ChangeLanguage",language.Code,false);Check(ReferenceEquals(Get(app,"selected"),device),"el dispositivo cambió al traducir");Check((bool)Get(app,"manual") && (int)Get(app,"chosenHz")==1000,"se perdió la frecuencia pendiente");Check(((TextBlock)window.FindName("DeviceName")).Text==L.T("Teclado USB"),"nombre genérico sin traducir");Check(((Button)window.FindName("Refresh")).Content.ToString()==L.T("↻  Actualizar"),"botón de actualización sin traducir");Check(((TextBlock)window.FindName("PendingText")).Text.Contains("1000 Hz"),"resumen sin frecuencia");
                    foreach(Match resource in Regex.Matches(xaml,@"\{DynamicResource (loc_[A-F0-9]+)\}"))Check(window.Resources.Contains(resource.Groups[1].Value),"falta un texto de la interfaz");
                    Call(app,"Navigate","guide");Check(L.Has("Anclar a la barra de tareas") && ((Button)window.FindName("PinTaskbar")).Content.ToString()==L.T("Anclar a la barra de tareas"),"acceso a la barra sin traducir");Check(L.Has("Pulso está anclado a la barra de tareas.") && L.Has("Haz clic derecho en el icono de Pulso de la barra de tareas y elige Anclar a la barra de tareas."),"faltan mensajes de anclaje");
                    Call(app,"Navigate","guide");Check(((TextBlock)window.FindName("PageTitle")).Text==L.T("Todo bajo control"),"guía sin traducir");Call(app,"Navigate","recovery");Check(((TextBlock)window.FindName("PageTitle")).Text==L.T("Vuelve a tu punto de partida"),"restauración sin traducir");Call(app,"Navigate","devices");
                    Call(app,"ReviewDriverSetup");Check(((TextBlock)window.FindName("DialogTitle")).Text==L.T("Prepara los ajustes USB."),"preparación sin traducir");Check(((Border)window.FindName("DialogComparison")).Visibility==Visibility.Collapsed,"preparación mezclada con cambio de frecuencia");Call(app,"CloseDialog");Check((int)Get(app,"chosenHz")==1000,"preparación perdió la frecuencia");Call(app,"Review",device,"set",1000);Check(((TextBlock)window.FindName("DialogTo")).Text=="1000 Hz","frecuencia de revisión incorrecta");Check(((TextBlock)window.FindName("DialogTitle")).Text==L.T("Todo claro antes de aplicar."),"diálogo sin traducir");Call(app,"CloseDialog");
                    Check(L.Error("Windows informa un problema de dispositivo (43).")==L.F("Windows informa un problema de dispositivo ({0}).",43),"error parametrizado sin traducir");
                    Check(L.Error("No se aplicó el cambio; se restauraron los valores anteriores. Windows no confirmó los valores guardados.")==L.T("No se aplicó el cambio; se restauraron los valores anteriores. ")+L.T("Windows no confirmó los valores guardados."),"error compuesto sin traducir");
                    var draft=new Draft{Id=device.Id,Action="set",Hz=1000};var arguments=L.Initialize((draft.Arguments()+" --language "+language.Code).Split(' '));Check(Draft.Read(arguments).Hz==1000 && L.Code==language.Code,"no se conserva el idioma al solicitar administrador");
                }
            }finally{window.Close();L.Set(original);}
            string folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)),"language-test-"+Guid.NewGuid().ToString("N")),path=Path.Combine(folder,"language.txt");
            try{Check(L.ReadPreference(path)=="es","la primera apertura debe usar español");foreach(var language in L.Languages){L.SavePreference(path,language.Code);Check(L.ReadPreference(path)==language.Code,"la preferencia no persiste");}File.WriteAllText(path,"invalid");Check(L.ReadPreference(path)=="es","preferencia dañada sin recuperación");}
            finally{if(File.Exists(path))File.Delete(path);if(Directory.Exists(folder))Directory.Delete(folder);}
            File.AppendAllText(report,"Idiomas: entradas en español, inglés, portugués, chino y ucraniano; recursos y formatos completos.\r\nCambio de idioma conserva selección y frecuencia. Guía, revisión, recuperación, errores y preferencia: correctos.\r\nLaboratorio ausente del ejecutable. No se cambiaron ajustes USB ni el idioma guardado del usuario durante las pruebas.\r\n");
        }
    }
}

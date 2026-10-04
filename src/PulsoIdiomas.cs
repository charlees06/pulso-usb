using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PulsoUsb {
    internal sealed class Draft {
        public string Id,Action;
        public int Hz;
        public static Draft Read(string[] args) {
            if(args.Length!=4 || args[0]!="--review")return null;
            string id=Encoding.UTF8.GetString(Convert.FromBase64String(args[1]));int hz;
            if(id.Length>4096 || !Regex.IsMatch(id,@"^USB\\VID_[0-9A-F]{4}&PID_[0-9A-F]{4}\\",RegexOptions.IgnoreCase) || !new[]{"set","clear","restore"}.Contains(args[2]) || !Int32.TryParse(args[3],out hz) || hz<0 || hz>8000)throw new ArgumentException("No se pudo recuperar el cambio pendiente.");
            return new Draft{Id=id,Action=args[2],Hz=hz};
        }
        public string Arguments(){return "--review "+Convert.ToBase64String(Encoding.UTF8.GetBytes(Id))+" "+Action+" "+Hz.ToString(CultureInfo.InvariantCulture);}
    }
    internal static class App {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static int Main(string[] args) {
            try {
                args=L.Initialize(args);
                if(args.Length==1 && args[0]=="--install-driver"){try{DriverPackage.Install();return 0;}catch(Exception ex){MessageBox.Show(L.F("No se pudo preparar el componente. {0}",L.Error(ex.Message)),"Pulso");return 1;}}
                if(args.Length==2 && args[0]=="--self-test") {CoreTests.Run(args[1]);LocalizationTests.Run(args[1]);ControllerTests.Run(args[1]);DriverPackage.Test(args[1]);var d=new Draft{Id=@"USB\VID_0000&PID_0000\TEST",Action="set",Hz=1000};var restored=Draft.Read(d.Arguments().Split(' '));if(restored.Id!=d.Id || restored.Hz!=1000 || restored.Action!="set")throw new Exception("El cambio pendiente no se conserva al elevar permisos.");using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PulsoV2.xaml")){var testWindow=(Window)XamlReader.Load(stream);if(testWindow.FindName("ReviewButton")==null)throw new Exception("No se cargó la interfaz.");testWindow.Close();}File.AppendAllText(args[1],"Contexto de permisos y carga de interfaz: correctos.\r\n");return 0;}
                if(!DriverPackage.Supported()){MessageBox.Show(L.T("Este paquete requiere Windows 10 u 11 de 64 bits, con procesador Intel o AMD."),"Pulso");return 1;}
                if(!DriverPackage.RuntimeReady()){MessageBox.Show(L.T("Necesitas .NET Framework 4.8 o posterior para abrir esta versión de Pulso.")+"\nhttps://dotnet.microsoft.com/download/dotnet-framework/net48","Pulso");return 1;}
                TaskbarIntegration.SetIdentity();SetProcessDPIAware();var app=new Application();Window window;
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PulsoV2.xaml"))window=(Window)XamlReader.Load(stream);
                using(var icon=Assembly.GetExecutingAssembly().GetManifestResourceStream("Pulso.ico")){window.Icon=BitmapFrame.Create(icon,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);}
                new Studio(window,Draft.Read(args));app.Run(window);return 0;
            } catch(Exception ex){if(args.Length==2 && args[0]=="--self-test")File.WriteAllText(args[1],ex.ToString());else MessageBox.Show(L.F("No se pudo abrir Pulso. {0}",L.Error(ex.Message)),"Pulso");return 1;}
        }
    }
    internal static class Art {
        public static Brush Brush(string hex){return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);}
        public static ImageSource Device(string kind) {
            var group=new DrawingGroup();using(var d=group.Open()) {
                var outline=new Pen(Brush("#BE9AFF"),2.2);outline.StartLineCap=PenLineCap.Round;outline.EndLineCap=PenLineCap.Round;outline.LineJoin=PenLineJoin.Round;
                var fill=new LinearGradientBrush(Color.FromRgb(64,57,97),Color.FromRgb(25,35,52),45);var light=new Pen(Brush("#67E4EF"),2.8);light.StartLineCap=PenLineCap.Round;light.EndLineCap=PenLineCap.Round;
                if(kind=="Mouse") {
                    d.DrawGeometry(Brush("#121323"),null,Geometry.Parse("M81,37 C81,8 152,8 152,45 L152,109 C151,152 78,151 78,109 Z"));
                    d.DrawGeometry(fill,outline,Geometry.Parse("M74,45 C73,1 144,0 145,44 L145,100 C145,145 74,145 74,100 Z"));
                    d.DrawLine(outline,new Point(110,14),new Point(110,67));d.DrawGeometry(null,outline,Geometry.Parse("M76,64 Q110,80 144,64"));
                    d.DrawRoundedRectangle(Brush("#17212F"),light,new Rect(104,31,12,24),6,6);d.DrawGeometry(null,light,Geometry.Parse("M82,97 Q81,123 103,129"));
                    d.DrawLine(new Pen(Brush("#645880"),2),new Point(145,69),new Point(145,87));
                } else if(kind=="Mando") {
                    d.DrawGeometry(Brush("#0D1623"),null,Geometry.Parse("M52,48 C29,48 22,82 20,116 C18,148 39,149 61,121 L167,122 C188,148 211,145 205,115 C201,78 194,47 170,47 Z"));
                    d.DrawGeometry(fill,outline,Geometry.Parse("M49,40 C28,40 21,74 18,108 C15,136 35,140 57,112 L163,112 C183,137 207,137 201,107 C196,73 190,40 168,40 Z"));
                    d.DrawRoundedRectangle(Brush("#1B1C30"),new Pen(Brush("#7961A4"),1.6),new Rect(78,44,63,35),7,7);
                    d.DrawGeometry(Brush("#121827"),outline,Geometry.Parse("M47,58 L59,58 59,69 70,69 70,81 59,81 59,92 47,92 47,81 36,81 36,69 47,69 Z"));
                    foreach(var p in new[]{new Point(169,60),new Point(182,74),new Point(169,88),new Point(156,74)})d.DrawEllipse(Brush("#1D2438"),light,p,4.8,4.8);
                    d.DrawEllipse(Brush("#121929"),outline,new Point(82,98),12,12);d.DrawEllipse(Brush("#121929"),outline,new Point(138,98),12,12);
                    d.DrawLine(light,new Point(79,41),new Point(140,41));
                } else if(kind=="Teclado") {
                    d.DrawGeometry(Brush("#10202C"),null,Geometry.Parse("M28,47 L198,47 216,119 11,119 Z"));
                    d.DrawGeometry(fill,outline,Geometry.Parse("M25,36 L193,36 209,110 9,110 Z"));
                    for(int row=0;row<4;row++)for(int col=0;col<10;col++){double x=28+col*16-row*1.3,y=46+row*13.5;d.DrawRoundedRectangle(Brush(row==2 && col<4?"#5B447D":"#202538"),new Pen(Brush("#7C7298"),0.7),new Rect(x,y,11.5,8.5),1.6,1.6);}
                    d.DrawRoundedRectangle(Brush("#393455"),new Pen(Brush("#9380B4"),0.9),new Rect(73,100,66,4),1,1);d.DrawLine(light,new Point(17,113),new Point(201,113));
                } else {d.DrawRoundedRectangle(fill,outline,new Rect(78,28,64,96),12,12);d.DrawLine(light,new Point(91,47),new Point(129,47));d.DrawLine(light,new Point(91,60),new Point(116,60));}
            }
            group.Freeze();return new DrawingImage(group);
        }
    }
    internal sealed class Cadence : FrameworkElement {
        public int Rate;
        protected override void OnRender(DrawingContext d) {
            base.OnRender(d);double w=ActualWidth;if(w<2)return;
            d.DrawLine(new Pen(Art.Brush("#30374C"),1),new Point(3,21),new Point(w-3,21));
            if(Rate>0){int count=Math.Max(1,Math.Min(64,Rate/125));for(int i=0;i<count;i++){double x=4+(w-8)*(i+.5)/count;d.DrawLine(new Pen(Art.Brush(i%2==0?"#AC86FF":"#57E0ED"),count>32?1.3:2),new Point(x,12),new Point(x,30));}}
            var text=new FormattedText(L.T(Rate>0?"CONSULTAS SOLICITADAS EN 8 MS":"EL DISPOSITIVO CONTROLA EL INTERVALO"),L.Culture,FlowDirection.LeftToRight,new Typeface(L.Code=="zh"?"Microsoft YaHei UI":"Segoe UI"),8.5,Art.Brush("#717C97"),VisualTreeHelper.GetDpi(this).PixelsPerDip);text.MaxTextWidth=Math.Max(1,w);d.DrawText(text,new Point(0,39));
        }
    }
    internal sealed class Studio {
        const string Official="https://github.com/LordOfMice/hidusbf";
        readonly Window window;readonly Draft opening;readonly Cadence cadence=new Cadence();readonly DispatcherTimer deviceTimer=new DispatcherTimer();readonly ControllerView controller;
        List<UsbDevice> all=new List<UsbDevice>();UsbDevice selected,pendingDevice;string pendingAction;int pendingHz,chosenHz=1000;bool manual,busy,changing,building,driverReady,dialogFinished;bool openedDraft;string page="devices";
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        T Find<T>(string name) where T:class{return window.FindName(name) as T;}
        void Text(string name,string text){Find<TextBlock>(name).Text=L.T(text);}
        Button Button(string name){return Find<Button>(name);}
        bool OverlayOpen {get{return Find<Grid>("Overlay").Visibility==Visibility.Visible;}}
        public Studio(Window w,Draft draft) {
            window=w;opening=draft;Find<Grid>("CadenceHost").Children.Add(cadence);L.Apply(window);controller=new ControllerView(window,Find<StackPanel>("ControllerPage"));
            var language=Find<ComboBox>("LanguageSelector");language.ItemsSource=L.Languages;language.SelectedItem=L.Languages.First(x=>x.Code==L.Code);language.SelectionChanged+=delegate{var choice=language.SelectedItem as LanguageChoice;if(choice!=null && choice.Code!=L.Code && !busy && !changing)ChangeLanguage(choice.Code);};
            Button("Refresh").Click+=async delegate{if(page=="controller")controller.Refresh();else await Refresh(false);};Button("NavDevices").Click+=delegate{Navigate("devices");};Button("NavRecovery").Click+=delegate{Navigate("recovery");};Button("NavController").Click+=delegate{Navigate("controller");};Button("NavGuide").Click+=delegate{Navigate("guide");};Button("DriverLink").Click+=delegate{Open(Official);};
            Button("ModeOriginal").Click+=delegate{manual=false;UpdateDraft();};Button("ModeManual").Click+=delegate{manual=true;UpdateDraft();};
            Button("ReviewButton").Click+=delegate{if(!driverReady && manual){ReviewDriverSetup();return;}Review(selected,manual?"set":"clear",chosenHz);};
            Button("DialogClose").Click+=delegate{CloseDialog();};Button("DialogCancel").Click+=delegate{CloseDialog();};Button("DialogApply").Click+=async delegate{await Confirm();};
            TaskbarTexts();Button("PinTaskbar").Click+=async delegate{
                if(!window.IsActive)return;
                Button("PinTaskbar").IsEnabled=false;
                try{var result=await TaskbarIntegration.Request();Text("TaskbarStatus",result==PinOutcome.Pinned?"Pulso está anclado a la barra de tareas.":result==PinOutcome.Declined?"No se añadió el acceso. Puedes intentarlo cuando quieras.":"Haz clic derecho en el icono de Pulso de la barra de tareas y elige Anclar a la barra de tareas.");}
                finally{Button("PinTaskbar").IsEnabled=true;}
            };
            window.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape && OverlayOpen && !changing){CloseDialog();e.Handled=true;}};
            window.Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(changing){e.Cancel=true;Text("DialogNotice","Espera a que termine el cambio. La ventana se puede cerrar cuando finalice.");}};
            window.SourceInitialized+=delegate{var handle=new WindowInteropHelper(window).Handle;int dark=1;DwmSetWindowAttribute(handle,20,ref dark,4);HwndSource.FromHwnd(handle).AddHook(Hook);};
            deviceTimer.Interval=TimeSpan.FromMilliseconds(700);deviceTimer.Tick+=async delegate{deviceTimer.Stop();if(!busy && !changing && !OverlayOpen)await Refresh(true);};
            window.Closed+=delegate{deviceTimer.Stop();controller.Dispose();};window.Loaded+=async delegate{await Refresh(false);};
            Text("PermissionBadge",Changes.IsAdministrator()?"Permisos de administrador":"Permisos solo al aplicar");
            KeyboardNavigation.SetTabNavigation(Find<Grid>("Overlay"),KeyboardNavigationMode.Cycle);
        }
        IntPtr Hook(IntPtr hwnd,int message,IntPtr wparam,IntPtr lparam,ref bool handled){if(message==0xFF)controller.Input(lparam);if(message==0x219){deviceTimer.Stop();deviceTimer.Start();}return IntPtr.Zero;}
        static string Kind(UsbDevice d){return d.Kind.Contains("Mouse")?"Mouse":d.Kind.Contains("Mando")?"Mando":d.Kind.Contains("Teclado")?"Teclado":"USB";}
        static string Name(UsbDevice d){return Regex.Replace(d.Name,@"\s+(Gaming Mouse|Wireless Controller)$","",RegexOptions.IgnoreCase).Replace("CORSAIR","Corsair").Replace("IRONCLAW","Ironclaw").Replace("USB Keyboard",L.T("Teclado USB"));}
        static string RateLabel(UsbDevice d){if(!d.Filter || d.Interval==0)return L.T("Control del dispositivo");if(!d.Interval.HasValue || d.Speed<0 || d.Speed>2)return L.T("Personalizada");return L.Rate(d.Speed,d.Interval.Value,false);}
        void ChangeLanguage(string code,bool remember=true){bool wasManual=manual;int hz=chosenHz;L.Set(code);L.Apply(window);controller.Rebuild();Button("Refresh").Content=L.T("↻  Actualizar");BuildCards(all.Where(d=>d.Input || d.Filter || File.Exists(Changes.BackupPath(d.Id))).ToList(),selected);Select(selected);manual=wasManual;chosenHz=hz;UpdateDraft();Navigate(page);Text("DeviceCount",L.F("{0} conectados",Find<UniformGrid>("DeviceCards").Children.OfType<RadioButton>().Count()));Text("PermissionBadge",Changes.IsAdministrator()?"Permisos de administrador":"Permisos solo al aplicar");Text("Status","Los cambios se guardan solo cuando tú los aplicas.");try{if(remember)L.SavePreference(L.PreferencePath,code);}catch(Exception ex){Text("Status",L.F("El idioma cambió, pero no se pudo guardar la preferencia: {0}",L.Error(ex.Message)));}}
        void TaskbarTexts(){Text("TaskbarTitle","Pulso, siempre a mano");Text("TaskbarStatus","Añade Pulso a tu barra de tareas. Windows te pedirá confirmación si admite esta opción.");Button("PinTaskbar").Content=L.T("Anclar a la barra de tareas");}
        static int SavedHz(UsbDevice d){return !d.Interval.HasValue?0:Rates.Options(d.Speed).FirstOrDefault(hz=>Rates.Encode(d.Speed,hz)==d.Interval.Value);}
        void Fade(UIElement view){view.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.45,1,TimeSpan.FromMilliseconds(170)));}
        void Navigate(string destination) {
            if(destination=="guide")TaskbarTexts();
            page=destination;foreach(string section in new[]{"devices","controller","recovery","guide"}){string suffix=section=="devices"?"Device":section=="controller"?"Controller":section=="recovery"?"Recovery":"Guide";Find<StackPanel>(suffix+"Page").Visibility=section==destination?Visibility.Visible:Visibility.Collapsed;Button("Nav"+(section=="devices"?"Devices":suffix)).Tag=section==destination?"active":null;}
            controller.Activate(destination=="controller");
            Text("PageTitle",destination=="devices"?"Tus dispositivos":destination=="controller"?"Probador de controles":destination=="recovery"?"Vuelve a tu punto de partida":"Todo bajo control");Text("PageSubtitle",destination=="devices"?"Elige un periférico y encuentra tu configuración.":destination=="controller"?"Conecta, mueve y comprueba tu mando.":destination=="recovery"?"Recupera el ajuste original de cada periférico.":"Lo esencial para usar Pulso con confianza.");
            Find<Border>("ReviewBar").Visibility=destination=="devices" && selected!=null?Visibility.Visible:Visibility.Collapsed;
            if(destination=="recovery")BuildRecovery();Fade(Find<StackPanel>(destination=="devices"?"DevicePage":destination=="controller"?"ControllerPage":destination=="recovery"?"RecoveryPage":"GuidePage"));
        }
        async Task Refresh(bool quiet) {
            if(busy || changing)return;busy=true;Find<ComboBox>("LanguageSelector").IsEnabled=false;Button("Refresh").IsEnabled=false;Button("Refresh").Content=L.T("Buscando…");Button("ReviewButton").IsEnabled=false;
            string id=selected==null?null:selected.Id;bool oldManual=manual;int oldHz=chosenHz;if(!quiet)Text("Status","Leyendo tus dispositivos USB…");
            try {
                all=await Task.Run(()=>Native.Scan());driverReady=Changes.DriverInstalled();
                var list=all.Where(d=>d.Input || d.Filter || File.Exists(Changes.BackupPath(d.Id))).ToList();Text("DeviceCount",L.F("{0} conectados",list.Count));
                UsbDevice target=list.FirstOrDefault(d=>d.Id==id)??list.FirstOrDefault();
                if(opening!=null && !openedDraft)target=list.FirstOrDefault(d=>d.Id==opening.Id)??target;
                BuildCards(list,target);Select(target);
                if(target!=null && target.Id==id && (!oldManual || Rates.Options(target.Speed).Contains(oldHz))){manual=oldManual;chosenHz=oldHz;UpdateDraft();}
                Text("Status",list.Count==0?"Conecta un mouse, teclado o mando USB para empezar.":"Los cambios se guardan solo cuando tú los aplicas.");if(page=="recovery")BuildRecovery();
                if(opening!=null && !openedDraft){openedDraft=true;var found=list.FirstOrDefault(d=>d.Id==opening.Id);if(found!=null && (opening.Action!="set" || Rates.Options(found.Speed).Contains(opening.Hz))){Select(found);manual=opening.Action=="set";chosenHz=opening.Hz;UpdateDraft();Review(found,opening.Action,opening.Hz);}else Text("Status","El dispositivo o la frecuencia pendiente ya no están disponibles. Elige otra opción.");}
            } catch(Exception ex){Text("Status",L.F("No se pudo actualizar: {0}",L.Error(ex.Message)));}
            finally{busy=false;Find<ComboBox>("LanguageSelector").IsEnabled=true;Button("Refresh").IsEnabled=true;Button("Refresh").Content=L.T("↻  Actualizar");UpdateDraft();}
        }
        TextBlock Block(string text,double size,string color,bool bold){return new TextBlock{Text=L.T(text),FontSize=size,Foreground=Art.Brush(color),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap};}
        void BuildCards(List<UsbDevice> devices,UsbDevice active) {
            building=true;var host=Find<UniformGrid>("DeviceCards");host.Children.Clear();host.Columns=Math.Min(3,Math.Max(1,devices.Count));
            foreach(var device in devices) {
                var d=device;var radio=new RadioButton{GroupName="Devices",Style=(Style)window.FindResource("DeviceChoice"),IsChecked=active!=null && d.Id==active.Id,MinHeight=139};AutomationProperties.SetName(radio,Name(d)+", "+L.T(Kind(d))+", "+RateLabel(d));
                var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(56)});
                var text=new StackPanel();var top=Block(L.T(Kind(d)).ToUpper(L.Culture),9,"#9996B5",true);top.Margin=new Thickness(0,0,0,9);text.Children.Add(top);
                var title=Block(Name(d),14,"#E9E6F5",true);title.MinHeight=40;title.MaxHeight=44;text.Children.Add(title);var current=Block(RateLabel(d),11,d.Filter?"#6EDFE8":"#858CA7",false);current.Margin=new Thickness(0,7,0,0);text.Children.Add(current);body.Children.Add(text);
                var icon=new Image{Source=Art.Device(Kind(d)),Width=60,Height=57,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(5,0,0,0)};Grid.SetColumn(icon,1);body.Children.Add(icon);radio.Content=body;radio.Checked+=delegate{if(!building && !changing)Select(d);};host.Children.Add(radio);
            }
            if(devices.Count==0)host.Children.Add(new Border{Background=Art.Brush("#171A26"),CornerRadius=new CornerRadius(14),Padding=new Thickness(25),Child=Block("Tus periféricos aparecerán aquí al conectarlos.",16,"#A1ACC8",false)});
            building=false;
        }
        void Select(UsbDevice device) {
            selected=device;Find<Border>("Editor").Visibility=device==null?Visibility.Collapsed:Visibility.Visible;Find<Border>("ReviewBar").Visibility=device==null || page!="devices"?Visibility.Collapsed:Visibility.Visible;Find<Expander>("Advanced").Visibility=device==null?Visibility.Collapsed:Visibility.Visible;
            if(device==null)return;var d=device;manual=d.Filter;chosenHz=SavedHz(d);if(chosenHz==0)chosenHz=Rates.Options(d.Speed).Contains(1000)?1000:Rates.Options(d.Speed).FirstOrDefault();
            Text("DeviceName",Name(d));Text("DeviceKind",L.F("{0}  ·  Conexión USB",L.T(Kind(d))));Text("CurrentRate",RateLabel(d));Text("CurrentNote",d.Filter?"Frecuencia guardada · sin medición":"Sin ajuste de Pulso");Find<Image>("HeroIcon").Source=Art.Device(Kind(d));
            Text("AdvancedText",String.Join("\n",new[]{L.F("Nombre completo: {0}",d.Name),L.F("Identificador: {0}",d.Hardware),L.F("Velocidad USB: {0} · Funciones: {1}",L.T(d.SpeedText),String.Join("/",d.Kind.Split('/').Select(L.T))),L.F("HIDUSBF: {0} · bInterval guardado: {1}",L.T(d.Filter?"filtro activado":"sin filtro"),d.Interval.HasValue?d.Interval.ToString():L.T("ninguno")),L.T("Este ajuste se aplica a todas las funciones de la unidad USB.")} )+(String.IsNullOrEmpty(d.Problem)?"":"\n"+L.F("Estado: {0}",L.Error(d.Problem))));
            var chips=Find<WrapPanel>("RateChips");chips.Children.Clear();foreach(int value in Rates.Options(d.Speed)){int hz=value;var chip=new Button{Content=value+" Hz",Style=(Style)window.FindResource("RateChoice")};chip.Click+=delegate{chosenHz=hz;manual=true;UpdateDraft();};chips.Children.Add(chip);}
            UpdateDraft();
        }
        string BlockReason() {
            if(selected==null)return "Selecciona un dispositivo.";
            if(selected.InheritedFilter)return "Una interfaz tiene su propio filtro. Gestiona ese caso con HIDUSBF oficial.";
            if(!manual)return "";
            if(!selected.Input)return "Este dispositivo está disponible solo para consulta o recuperación.";
            if(!String.IsNullOrEmpty(selected.Problem))return L.Error(selected.Problem);
            if(selected.Speed<0)return "No se pudo leer la velocidad USB. Actualiza la lista antes de configurar.";
            if(selected.Speed>2)return "La configuración SuperSpeed todavía no está validada en esta versión.";
            if(!Rates.Options(selected.Speed).Contains(chosenHz))return "Elige una frecuencia disponible.";
            return "";
        }
        void UpdateDraft() {
            if(selected==null)return;Button("ModeOriginal").Tag=!manual?"selected":null;Button("ModeManual").Tag=manual?"selected":null;
            foreach(Button chip in Find<WrapPanel>("RateChips").Children){int hz=Int32.Parse(chip.Content.ToString().Split(' ')[0]);chip.Tag=manual && hz==chosenHz?"selected":null;chip.Opacity=manual?1:.65;chip.IsEnabled=!changing;}
            Text("IntervalNumber",manual && chosenHz>0?(1000.0/chosenHz).ToString("0.###"):"Auto");Find<TextBlock>("IntervalUnit").Visibility=manual?Visibility.Visible:Visibility.Collapsed;cadence.Rate=manual?chosenHz:0;cadence.InvalidateVisual();
            Text("RateHelp",!manual?"El dispositivo gestiona el intervalo. No se fuerza una frecuencia.":selected.Speed==0?"En este periférico USB de baja velocidad, superar 125 Hz puede no funcionar con el controlador actual.":"Intervalo solicitado, no latencia medida. El hardware puede limitar la frecuencia efectiva.");
            string reason=BlockReason();bool changed=manual?(!selected.Filter || !selected.Interval.HasValue || (reason=="" && selected.Interval.Value!=Rates.Encode(selected.Speed,chosenHz))):selected.Filter;
            Text("PendingTitle",reason!=""?"Necesita una revisión":changed?"Tienes un cambio preparado":"Tu configuración está al día");
            Text("PendingText",reason!=""?reason:changed?L.F("{0}  →  {1}. La copia original se conserva.",RateLabel(selected),manual?chosenHz+" Hz":L.T("Control del dispositivo")):L.T("Puedes explorar las frecuencias sin aplicar ningún cambio."));
            var review=Button("ReviewButton");review.Content=L.T(!driverReady && manual?"Preparar ajustes USB":changed?"Revisar cambio  →":"Sin cambios");review.IsEnabled=!busy && !changing && reason=="" && (changed || (!driverReady && manual));
        }
        void BuildRecovery() {
            var host=Find<StackPanel>("RecoveryCards");host.Children.Clear();int count=0;
            foreach(var d in all.Where(x=>File.Exists(Changes.BackupPath(x.Id)))) {
                try{var saved=Changes.LoadBackup(d.Id);if(saved==null)continue;count++;var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                    var info=new StackPanel();info.Children.Add(Block(Name(d),17,"#EDE9F7",true));string original=!saved.HadFilter?L.T("Control del dispositivo"):saved.Interval.HasValue?L.Rate(d.Speed,saved.Interval.Value,true):L.T("Valor del dispositivo");var detail=Block(L.F("Copia original: {0}",original),12,"#A79DBB",false);detail.Margin=new Thickness(0,8,20,0);info.Children.Add(detail);body.Children.Add(info);
                    var button=new Button{Content=L.T("Revisar restauración"),Style=(Style)window.FindResource("ButtonBase"),VerticalAlignment=VerticalAlignment.Center};var captured=d;button.Click+=delegate{Review(captured,"restore",0);};Grid.SetColumn(button,1);body.Children.Add(button);host.Children.Add(new Border{Background=Art.Brush("#181C29"),CornerRadius=new CornerRadius(13),Padding=new Thickness(21),Margin=new Thickness(0,0,0,12),Child=body});
                }catch(Exception ex){host.Children.Add(Block(L.F("Una copia no se pudo leer: {0}",L.Error(ex.Message)),12,"#D6B58A",false));}
            }
            if(count==0)host.Children.Add(new Border{Background=Art.Brush("#191C29"),Padding=new Thickness(27),CornerRadius=new CornerRadius(14),Child=Block("Tu primera copia aparecerá aquí cuando apliques un cambio. Si ya tienes una, conecta ese periférico.",16,"#A4AFC9",false)});
        }
        void Review(UsbDevice target,string action,int hz) {
            if(target==null || changing)return;pendingDevice=target;pendingAction=action;pendingHz=hz;dialogFinished=false;
            Find<Border>("DialogComparison").Visibility=Visibility.Visible;
            string desired=action=="set"?hz+" Hz":L.T("Control del dispositivo");
            try{if(action=="restore"){var saved=Changes.LoadBackup(target.Id);if(saved==null)throw new IOException("No hay una copia original para este dispositivo.");desired=saved.HadFilter && saved.Interval.HasValue?L.Rate(target.Speed,saved.Interval.Value,true):L.T("Control del dispositivo");}}
            catch(Exception ex){Text("Status",L.Error(ex.Message));return;}
            Text("DialogEyebrow",action=="restore"?"RECUPERACIÓN  /  COPIA ORIGINAL":"03  /  REVISA EL CAMBIO");Text("DialogTitle",action=="restore"?"Recupera tu configuración.":"Todo claro antes de aplicar.");Text("DialogDevice",Name(target));Text("DialogFrom",RateLabel(target));Text("DialogTo",desired);
            Text("DialogBody","El cambio afecta a la unidad USB completa, incluidas sus funciones adicionales. Después de aplicarlo, desconecta y conecta el periférico para que Windows vuelva a cargar su configuración.");
            Text("DialogNotice",Changes.IsAdministrator()?"Tu copia original se conserva. La frecuencia solicitada no garantiza la tasa efectiva del dispositivo.":"Windows solicitará permisos de administrador. Tu dispositivo y frecuencia elegidos se conservarán; después podrás aplicar el cambio.");
            Button("DialogApply").Content=L.T(Changes.IsAdministrator()?"Aplicar configuración":"Continuar como administrador");Button("DialogApply").IsEnabled=true;Button("DialogClose").IsEnabled=true;Button("DialogCancel").IsEnabled=true;Button("DialogCancel").Visibility=Visibility.Visible;
            Find<Grid>("Workspace").IsEnabled=false;Find<Border>("Sidebar").IsEnabled=false;Find<Grid>("Overlay").Visibility=Visibility.Visible;Fade(Find<Grid>("Overlay"));Button("DialogCancel").Focus();
        }
        void CloseDialog(){if(changing)return;Find<Grid>("Overlay").Visibility=Visibility.Collapsed;Find<Grid>("Workspace").IsEnabled=true;Find<Border>("Sidebar").IsEnabled=true;if(page=="devices")Button("ReviewButton").Focus();else Button("NavRecovery").Focus();}
        void ReviewDriverSetup(){
            if(changing)return;pendingDevice=selected;pendingAction="driver";dialogFinished=false;
            Find<Border>("DialogComparison").Visibility=Visibility.Collapsed;
            Text("DialogEyebrow","PREPARACIÓN INICIAL");Text("DialogTitle","Prepara los ajustes USB.");Text("DialogDevice","Componente incluido con Pulso");
            Text("DialogBody","Para ajustar las frecuencias, Pulso necesita instalar el controlador oficial HIDUSBF incluido en este paquete. Windows solicitará permisos de administrador.");
            Text("DialogNotice",DriverPackage.Supported()?"La instalación no cambia las frecuencias de tus dispositivos. Después podrás revisar y aplicar tu ajuste. Se conserva cualquier instalación existente.":"Este paquete requiere Windows 10 u 11 de 64 bits, con procesador Intel o AMD.");
            Button("DialogApply").Content=L.T("Instalar componente");Button("DialogApply").IsEnabled=DriverPackage.Supported();Button("DialogClose").IsEnabled=true;Button("DialogCancel").IsEnabled=true;Button("DialogCancel").Visibility=Visibility.Visible;
            Find<Grid>("Workspace").IsEnabled=false;Find<Border>("Sidebar").IsEnabled=false;Find<Grid>("Overlay").Visibility=Visibility.Visible;Button("DialogCancel").Focus();
        }
        async Task InstallDriver(){
            changing=true;Button("DialogApply").IsEnabled=false;Button("DialogClose").IsEnabled=false;Button("DialogCancel").IsEnabled=false;Button("DialogApply").Content=L.T("Instalando…");Text("DialogNotice","Espera a que Windows complete la instalación.");
            try{var info=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--install-driver --language "+L.Code){UseShellExecute=true,Verb="runas",WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory};using(var process=Process.Start(info)){await Task.Run(()=>process.WaitForExit());if(process.ExitCode!=0)throw new InvalidOperationException("No se completó la instalación. Puedes seguir usando el probador de controles.");}
                if(!Changes.DriverInstalled())throw new InvalidOperationException("Windows no confirmó la instalación del componente.");
                Text("DialogEyebrow","PREPARACIÓN COMPLETA");Text("DialogTitle","Listo para configurar.");Text("DialogBody","El componente está instalado. Ahora puedes revisar y aplicar la frecuencia que elegiste.");Text("DialogNotice","Tus frecuencias USB siguen como estaban.");
            }catch(System.ComponentModel.Win32Exception ex){Text("DialogNotice",ex.NativeErrorCode==1223?"No se concedieron permisos. Tu selección sigue aquí y no se ha aplicado ningún cambio.":L.Error(ex.Message));}
            catch(Exception ex){Text("DialogNotice",L.Error(ex.Message));}
            finally{changing=false;dialogFinished=true;Button("DialogApply").Content=L.T("Entendido");Button("DialogApply").IsEnabled=true;Button("DialogClose").IsEnabled=true;Button("DialogCancel").Visibility=Visibility.Collapsed;}
            await Refresh(true);
        }
        async Task Confirm() {
            if(dialogFinished){CloseDialog();return;}if(changing)return;if(pendingAction=="driver"){await InstallDriver();return;}if(pendingDevice==null)return;
            if(!Changes.IsAdministrator()) {
                try{var draft=new Draft{Id=pendingDevice.Id,Action=pendingAction,Hz=pendingHz};Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,draft.Arguments()+" --language "+L.Code){UseShellExecute=true,Verb="runas",WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory});window.Close();}
                catch(System.ComponentModel.Win32Exception ex){Text("DialogNotice",ex.NativeErrorCode==1223?"No se concedieron permisos. Tu selección sigue aquí y no se ha aplicado ningún cambio.":L.Error(ex.Message));}return;
            }
            changing=true;Button("DialogApply").IsEnabled=false;Button("DialogClose").IsEnabled=false;Button("DialogCancel").IsEnabled=false;Button("DialogApply").Content=L.T("Aplicando…");Text("DialogNotice","Guardando la copia y verificando la configuración…");
            try{var target=pendingDevice;string action=pendingAction;int hz=pendingHz;await Task.Run(()=>Changes.Apply(target,action=="set",hz,action=="restore"));Text("DialogEyebrow","CAMBIO GUARDADO");Text("DialogTitle","Ahora, vuelve a conectarlo.");Text("DialogBody",L.F("Desconecta y conecta de nuevo {0}. Pulso actualizará la lista al detectar la conexión. También puedes pulsar Actualizar.",Name(target)));Text("DialogNotice","Se verificaron los valores guardados en Windows. La frecuencia efectiva aún no se ha medido.");Text("Status",L.F("Configuración guardada para {0}. Reconecta el periférico.",Name(target)));}
            catch(Exception ex){Text("DialogEyebrow","NO SE COMPLETÓ EL CAMBIO");Text("DialogTitle","Revisa este detalle.");Text("DialogNotice",L.Error(ex.Message));Text("Status","El cambio no se completó. Consulta el detalle en pantalla.");}
            finally{changing=false;dialogFinished=true;Button("DialogApply").Content=L.T("Entendido");Button("DialogApply").IsEnabled=true;Button("DialogClose").IsEnabled=true;Button("DialogCancel").Visibility=Visibility.Collapsed;}
            await Refresh(true);
        }
        void Open(string destination){try{Process.Start(new ProcessStartInfo(destination){UseShellExecute=true});}catch(Exception ex){Text("Status",L.F("No se pudo abrir: {0}",L.Error(ex.Message)));}}
    }
}

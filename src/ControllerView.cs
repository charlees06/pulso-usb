using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PulsoUsb {
    internal static class PadPaint {
        static readonly Dictionary<string,Brush> brushes=new Dictionary<string,Brush>();
        public static Brush Brush(string color){Brush brush;if(!brushes.TryGetValue(color,out brush)){brush=Art.Brush(color);brush.Freeze();brushes.Add(color,brush);}return brush;}
    }
    internal sealed class StickScope : FrameworkElement {
        public PadAxis X,Y; readonly Queue<Point> trail=new Queue<Point>();Point? previous;
        public void Reset(){trail.Clear();previous=null;InvalidateVisual();}
        public void Sample(){if(X!=null && Y!=null && X.Valid && Y.Valid){var p=new Point(X.Centered,Y.Centered);if(!previous.HasValue || (p-previous.Value).Length>.025){trail.Enqueue(p);if(trail.Count>110)trail.Dequeue();previous=p;}}InvalidateVisual();}
        protected override void OnRender(DrawingContext d){double r=Math.Min(ActualWidth/2-12,ActualHeight/2-10);if(r<1)return;Point c=new Point(ActualWidth/2,ActualHeight/2);
            d.DrawEllipse(PadPaint.Brush("#10131E"),new Pen(PadPaint.Brush("#3D3459"),1.5),c,r,r);d.DrawEllipse(null,new Pen(PadPaint.Brush("#282E43"),1),c,r*.5,r*.5);
            d.DrawLine(new Pen(PadPaint.Brush("#30374B"),1),new Point(c.X-r,c.Y),new Point(c.X+r,c.Y));d.DrawLine(new Pen(PadPaint.Brush("#30374B"),1),new Point(c.X,c.Y-r),new Point(c.X,c.Y+r));
            Point? last=null;foreach(var p in trail){var q=new Point(c.X+p.X*r,c.Y+p.Y*r);if(last.HasValue)d.DrawLine(new Pen(PadPaint.Brush("#6560A4"),1.5),last.Value,q);last=q;}
            if(X!=null && Y!=null && X.Valid && Y.Valid){var p=new Point(c.X+X.Centered*r,c.Y+Y.Centered*r);d.DrawEllipse(PadPaint.Brush("#255A6976"),null,p,13,13);d.DrawEllipse(PadPaint.Brush("#57E0ED"),new Pen(PadPaint.Brush("#BAFCFF"),1),p,5,5);}
        }
    }
    internal sealed class PadPicture : FrameworkElement {
        public PadDevice Device;
        void Label(DrawingContext d,string label,double x,double y,bool pressed,double radius){d.DrawEllipse(PadPaint.Brush(pressed?"#A576F2":"#171B2C"),new Pen(PadPaint.Brush(pressed?"#D7BAFF":"#6B588E"),1.5),new Point(x,y),radius,radius);var t=new FormattedText(label,L.Culture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),label.Length>3?8:12,PadPaint.Brush(pressed?"#FFFFFF":"#C8BADF"),VisualTreeHelper.GetDpi(this).PixelsPerDip);d.DrawText(t,new Point(x-t.Width/2,y-t.Height/2));}
        protected override void OnRender(DrawingContext d){if(ActualWidth<1)return;double scale=Math.Min(ActualWidth/480,ActualHeight/230);d.PushTransform(new TranslateTransform((ActualWidth-480*scale)/2,(ActualHeight-230*scale)/2));d.PushTransform(new ScaleTransform(scale,scale));
            d.DrawEllipse(PadPaint.Brush("#191C30"),null,new Point(240,157),200,63);
            d.DrawGeometry(new LinearGradientBrush(Color.FromRgb(57,43,86),Color.FromRgb(25,31,48),90),new Pen(PadPaint.Brush("#9270C4"),2),Geometry.Parse("M131,46 C87,36 76,60 58,105 L36,178 C28,214 58,224 84,198 L127,159 L353,159 L396,198 C422,224 452,214 444,178 L422,105 C404,60 393,36 349,46 Z"));
            d.DrawRoundedRectangle(PadPaint.Brush("#151A2C"),new Pen(PadPaint.Brush("#67518F"),1),new Rect(186,52,108,58),9,9);d.DrawLine(new Pen(PadPaint.Brush("#57E0ED"),2),new Point(196,49),new Point(284,49));
            bool sony=Device!=null && Device.Sony,known=Device!=null && (sony || Device.Slot>=0);Func<string,bool> down=s=>Device!=null && Device.HasInput && Device.Pressed(s);
            string[] face=sony?new[]{"△","○","×","□"}:Device!=null && Device.Slot>=0?new[]{"Y","B","A","X"}:new[]{"B4","B2","B1","B3"};
            Label(d,face[0],355,77,down(face[0]),13);Label(d,face[1],380,102,down(face[1]),13);Label(d,face[2],355,127,down(face[2]),13);Label(d,face[3],330,102,down(face[3]),13);
            int hat=Device==null?-1:ControllerInput.HatDirection(Device.Axes.FirstOrDefault(a=>a.Hat));string[] arrows={"↑","→","↓","←"};Point[] points={new Point(127,83),new Point(146,102),new Point(127,121),new Point(108,102)};
            for(int i=0;i<4;i++){bool active=hat>=0 && ((hat+1)/2%4==i || hat/2==i);active|=down(arrows[i]);Label(d,arrows[i],points[i].X,points[i].Y,active,11);}
            Label(d,sony?"Create":"View",168,78,known && down(sony?"Create":"View"),10);Label(d,sony?"Options":"Menu",312,78,known && down(sony?"Options":"Menu"),10);
            Label(d,"L3",189,146,down("L3"),25);Label(d,"R3",291,146,down("R3"),25);
            foreach(bool left in new[]{true,false}){string name=sony?(left?"L1":"R1"):(left?"LB":"RB");d.DrawRoundedRectangle(PadPaint.Brush(down(name)?"#A576F2":"#302941"),new Pen(PadPaint.Brush("#806397"),1),new Rect(left?102:316,30,64,12),5,5);}
            d.Pop();d.Pop();
        }
    }
    internal sealed class ControllerView : IDisposable {
        readonly Window window;readonly StackPanel host;readonly ControllerInput input=new ControllerInput();
        readonly DispatcherTimer timer=new DispatcherTimer(DispatcherPriority.Background);DateTime nextScan;
        bool active,building,wasFocused;PadDevice selected;
        ComboBox picker;TextBlock status,deviceInfo,coverage,leftValue,rightValue,leftTitle,rightTitle,triggerLeftText,triggerRightText,hint;
        Border liveCard;PadPicture picture;StickScope leftScope,rightScope;ProgressBar triggerLeft,triggerRight;
        readonly Dictionary<PadButton,Border> buttonViews=new Dictionary<PadButton,Border>();readonly Dictionary<PadAxis,TextBlock> axisViews=new Dictionary<PadAxis,TextBlock>();
        StackPanel detailHost;
        public ControllerView(Window window,StackPanel host){this.window=window;this.host=host;timer.Interval=TimeSpan.FromMilliseconds(16);timer.Tick+=delegate{Tick();};Rebuild();}
        TextBlock Text(string text,double size,string color){return new TextBlock{Text=L.T(text),FontSize=size,Foreground=PadPaint.Brush(color),TextWrapping=TextWrapping.Wrap};}
        Border Card(UIElement child,double padding){return new Border{Background=PadPaint.Brush("#161923"),BorderBrush=PadPaint.Brush("#2C3144"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(15),Padding=new Thickness(padding),Child=child};}
        Grid Columns(params double[] widths){var grid=new Grid();foreach(double width in widths)grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(width,GridUnitType.Star)});return grid;}
        void Add(Grid grid,UIElement item,int column){Grid.SetColumn(item,column);grid.Children.Add(item);}
        public void Activate(bool value){if(active==value)return;active=value;if(value){IntPtr hwnd=new WindowInteropHelper(window).Handle;if(hwnd!=IntPtr.Zero){input.Start(hwnd);SyncPicker();timer.Start();nextScan=DateTime.UtcNow.AddSeconds(1);Tick();}}else{timer.Stop();input.Stop();Render();}}
        public void Refresh(){if(!active)return;try{IntPtr hwnd=new WindowInteropHelper(window).Handle;if(hwnd==IntPtr.Zero)return;input.Start(hwnd);input.Refresh();SyncPicker();timer.Start();Tick();}catch(Exception){status.Text=L.T("No se pudo leer el mando. Pulsa Actualizar para reintentar.");}}
        public void Input(IntPtr pointer){if(active && window.IsActive)input.Input(pointer);}
        public void Rebuild(){host.Children.Clear();buttonViews.Clear();axisViews.Clear();
            var top=Columns(1.65,1);var selection=new StackPanel();selection.Children.Add(Text("ELIGE TU MANDO",10,"#AC86FF"));picker=new ComboBox{Style=(Style)window.FindResource("LanguageBox"),Margin=new Thickness(0,8,22,0),MinWidth=180};AutomationProperties.SetName(picker,L.T("Elige tu mando"));picker.SelectionChanged+=delegate{if(!building){selected=picker.SelectedItem as PadDevice;BuildDetails();Render();}};selection.Children.Add(picker);Add(top,selection,0);
            status=Text("Conecta un mando para empezar.",12,"#57E0ED");liveCard=Card(status,16);liveCard.VerticalAlignment=VerticalAlignment.Bottom;Add(top,liveCard,1);top.Margin=new Thickness(0,6,0,18);host.Children.Add(top);
            var hero=Columns(1.25,1);picture=new PadPicture{Height=200};AutomationProperties.SetName(picture,L.T("Vista del mando"));Add(hero,picture,0);
            var instructions=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(16,0,0,0)};var title=Text("Cada movimiento cuenta.",24,"#F1E9FF");title.FontWeight=FontWeights.SemiBold;instructions.Children.Add(title);var intro=Text("Pulsa los botones y mueve las palancas. Verás la respuesta al instante.",13,"#ADB1C8");intro.Margin=new Thickness(0,12,0,14);instructions.Children.Add(intro);deviceInfo=Text("",11,"#7EDCE6");instructions.Children.Add(deviceInfo);Add(hero,instructions,1);var heroCard=Card(hero,22);heroCard.Margin=new Thickness(0,0,0,16);host.Children.Add(heroCard);
            var metrics=Columns(1,1,1);leftScope=new StickScope{Height=123};rightScope=new StickScope{Height=123};
            leftTitle=Text("Palanca izquierda",12,"#BBA2E8");rightTitle=Text("Palanca derecha",12,"#BBA2E8");leftValue=Text("—",11,"#87E6EC");rightValue=Text("—",11,"#87E6EC");
            for(int i=0;i<2;i++){var panel=new StackPanel();panel.Children.Add(i==0?leftTitle:rightTitle);panel.Children.Add(i==0?leftScope:rightScope);var value=i==0?leftValue:rightValue;value.HorizontalAlignment=HorizontalAlignment.Center;panel.Children.Add(value);var card=Card(panel,17);card.Margin=new Thickness(0,0,12,0);Add(metrics,card,i);}
            var triggers=new StackPanel();triggers.Children.Add(Text("Gatillos",12,"#BBA2E8"));triggerLeftText=Text("L2 / LT   —",12,"#99E9EE");triggerRightText=Text("R2 / RT   —",12,"#CDB1FF");triggerLeft=new ProgressBar{Minimum=0,Maximum=100,Height=9,Foreground=PadPaint.Brush("#57E0ED"),Background=PadPaint.Brush("#272F41"),BorderThickness=new Thickness(0)};triggerRight=new ProgressBar{Minimum=0,Maximum=100,Height=9,Foreground=PadPaint.Brush("#B78BFF"),Background=PadPaint.Brush("#272F41"),BorderThickness=new Thickness(0)};
            triggerLeftText.Margin=new Thickness(0,24,0,9);triggerRightText.Margin=new Thickness(0,20,0,9);triggers.Children.Add(triggerLeftText);triggers.Children.Add(triggerLeft);triggers.Children.Add(triggerRightText);triggers.Children.Add(triggerRight);Add(metrics,Card(triggers,17),2);metrics.Margin=new Thickness(0,0,0,16);host.Children.Add(metrics);
            detailHost=new StackPanel();host.Children.Add(detailHost);hint=Text("Lectura en vivo al mantener Pulso activo. El probador no mide la latencia ni cambia la configuración.",11,"#828CA8");hint.Margin=new Thickness(3,16,3,16);host.Children.Add(hint);SyncPicker();Render();
        }
        void SyncPicker(){building=true;try{string id=selected==null?null:selected.Id;selected=input.Devices.FirstOrDefault(d=>d.Id==id)??input.Devices.FirstOrDefault();picker.ItemsSource=null;picker.ItemsSource=input.Devices.ToArray();picker.SelectedItem=selected;picker.IsEnabled=input.Devices.Count>0;}finally{building=false;}BuildDetails();}
        void BuildDetails(){detailHost.Children.Clear();buttonViews.Clear();axisViews.Clear();leftScope.Reset();rightScope.Reset();var panel=new StackPanel();
            var row=Columns(1,1);coverage=Text("Botones",14,"#E7DDFB");coverage.VerticalAlignment=VerticalAlignment.Center;Add(row,coverage,0);var reset=new Button{Content=L.T("Reiniciar recorrido"),Style=(Style)window.FindResource("Quiet"),FontSize=11,Padding=new Thickness(10,6,10,6),HorizontalAlignment=HorizontalAlignment.Right};reset.Click+=delegate{if(selected!=null)selected.ResetHistory();leftScope.Reset();rightScope.Reset();Render();};Add(row,reset,1);panel.Children.Add(row);
            var buttons=new WrapPanel{Margin=new Thickness(0,12,0,0)};panel.Children.Add(buttons);
            if(selected!=null)foreach(var b in selected.Buttons){var name=Text(b.Name,12,"#CBC5E3");name.HorizontalAlignment=HorizontalAlignment.Center;var tile=new Border{Child=name,MinWidth=43,Padding=new Thickness(10,10,10,10),CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1),Margin=new Thickness(0,0,8,8)};AutomationProperties.SetName(tile,b.Name);buttonViews[b]=tile;buttons.Children.Add(tile);}
            var legend=Text("Morado: pulsado · Turquesa: probado",11,"#8F99B4");legend.Margin=new Thickness(0,4,0,0);panel.Children.Add(legend);detailHost.Children.Add(Card(panel,19));
            if(selected!=null){var axisWrap=new WrapPanel();foreach(var a in selected.Axes){var t=Text("",11,"#8EABC6");t.Margin=new Thickness(0,0,20,9);axisViews[a]=t;axisWrap.Children.Add(t);}var expander=new Expander{Header=L.T("Valores del mando"),Content=axisWrap,Foreground=PadPaint.Brush("#959FBA"),FontSize=11,Margin=new Thickness(4,14,0,0)};detailHost.Children.Add(expander);}
        }
        void Tick(){if(!active)return;try{if(DateTime.UtcNow>=nextScan){nextScan=DateTime.UtcNow.AddSeconds(1);if(input.Refresh())SyncPicker();}
            bool focused=window.IsActive;if(!focused && wasFocused)foreach(var d in input.Devices)d.ClearLive();wasFocused=focused;if(focused)input.PollXbox();Render();
        }catch(Exception){timer.Stop();input.Stop();status.Text=L.T("No se pudo leer el mando. Pulsa Actualizar para reintentar.");}}
        static string Values(PadAxis x,PadAxis y){return x==null || y==null || !x.Valid || !y.Valid?"—":String.Format(L.Culture,"X {0:+0.0;-0.0;0.0}%   Y {1:+0.0;-0.0;0.0}%",x.Centered*100,y.Centered*100);}
        void Render(){if(status==null)return;var d=selected;bool known=d!=null && (d.Sony || d.Slot>=0);
            status.Text=L.T(input.ErrorText??(d==null?"Conecta un mando para empezar.":!active || !window.IsActive?"En pausa · vuelve a Pulso":d.HasInput?"●  Recibiendo señales":"Mueve una palanca para empezar."));
            deviceInfo.Text=d==null?L.T("Los mandos compatibles aparecerán aquí."):L.F("{0} botones · {1} ejes",d.Buttons.Count,d.Axes.Count(a=>!a.Hat));picture.Device=d;picture.InvalidateVisual();
            PadAxis lx=d==null?null:d.Axis(0x30),ly=d==null?null:d.Axis(0x31),rx=null,ry=null,lt=null,rt=null;
            if(d!=null){if(d.Sony){rx=d.Axis(0x32);ry=d.Axis(0x35);lt=d.Axis(0x33);rt=d.Axis(0x34);}else{rx=d.Axis(0x33);ry=d.Axis(0x34);if(d.Slot>=0){lt=d.Axis(0x32);rt=d.Axis(0x35);}else{if(rx==null || ry==null){rx=d.Axis(0x32);ry=d.Axis(0x35);}lt=d.Axes.FirstOrDefault(a=>a.Page==2 && a.Usage==0xC4);rt=d.Axes.FirstOrDefault(a=>a.Page==2 && a.Usage==0xC5);}}}
            leftScope.X=lx;leftScope.Y=ly;rightScope.X=rx;rightScope.Y=ry;leftScope.Sample();rightScope.Sample();leftTitle.Text=L.T(known?"Palanca izquierda":"Ejes X / Y");rightTitle.Text=known?L.T("Palanca derecha"):L.F("Ejes {0} / {1}",rx==null?"—":rx.Name,ry==null?"—":ry.Name);leftValue.Text=Values(lx,ly);rightValue.Text=Values(rx,ry);
            triggerLeft.Value=lt!=null && lt.Valid?lt.Unit*100:0;triggerRight.Value=rt!=null && rt.Valid?rt.Unit*100:0;triggerLeftText.Text="L2 / LT   "+(lt!=null && lt.Valid?triggerLeft.Value.ToString("0",L.Culture)+"%":"—");triggerRightText.Text="R2 / RT   "+(rt!=null && rt.Valid?triggerRight.Value.ToString("0",L.Culture)+"%":"—");
            coverage.Text=d==null?L.T("Botones"):L.F("Botones · {0} de {1} probados",d.Buttons.Count(b=>b.Seen),d.Buttons.Count);
            foreach(var pair in buttonViews){var b=pair.Key;pair.Value.Background=PadPaint.Brush(b.Down?"#7742BA":b.Seen?"#193E46":"#202335");pair.Value.BorderBrush=PadPaint.Brush(b.Down?"#BD90FF":b.Seen?"#51AAB7":"#343A50");}
            foreach(var pair in axisViews){var a=pair.Key;pair.Value.Text=a.Name+": "+(a.Valid?a.Raw.ToString(L.Culture):"—");}
            hint.Text=L.T(known?"Lectura en vivo al mantener Pulso activo. El probador no mide la latencia ni cambia la configuración.":"En mandos genéricos, los botones aparecen numerados y los ejes con su nombre. Los extras dependen de lo que Windows exponga.");
        }
        public void Dispose(){timer.Stop();input.Dispose();}
    }
}

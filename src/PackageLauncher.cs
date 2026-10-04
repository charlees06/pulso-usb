using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using Microsoft.Win32;

// Uses only .NET 4.0 APIs so it can check prerequisites on Windows 10 before WPF starts.
internal static class PackageLauncher {
    const string RuntimeHash="0A3A390C47E639D0F7FC65B21195FEE6B7F65B066F80F70C60FAB191D14B7E40";
    static readonly string[] Codes={"es","en","pt","zh","uk"};static int language;
    [StructLayout(LayoutKind.Sequential)] struct SystemInfo {public ushort Architecture,Reserved;public uint PageSize;public IntPtr Min,Max,Mask;public uint Count,Type,Granularity;public ushort Level,Revision;}
    [DllImport("kernel32.dll")] static extern void GetNativeSystemInfo(out SystemInfo info);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    static string T(string es,string en,string pt,string zh,string uk){return new[]{es,en,pt,zh,uk}[language];}
    static bool Ready(){using(var registry=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32))using(var key=registry.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))return key!=null && Convert.ToInt32(key.GetValue("Release",0))>=528040;}
    internal static string HashFile(string path){using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    internal static bool Supported(ushort architecture,int major){return architecture==9 && major>=10;}
    [STAThread] static int Main(string[] args){try{
        string code=CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;string pref=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Pulso","language.txt");if(File.Exists(pref))code=File.ReadAllText(pref).Trim();language=Array.IndexOf(Codes,code);if(language<0)language=1;
        string root=AppDomain.CurrentDomain.BaseDirectory,app=Path.Combine(root,"Aplicacion","Pulso.exe"),runtime=Path.Combine(root,"Requisitos","NDP48-x86-x64-AllOS-ENU.exe");
        if(args.Length==2 && args[0]=="--self-test"){
            if(!File.Exists(app) || HashFile(runtime)!=RuntimeHash)throw new IOException("Incomplete distribution or invalid runtime hash.");
            if(!Supported(9,10) || Supported(12,10) || Supported(0,10) || Supported(9,6))throw new Exception("Platform guard failed.");
            File.WriteAllText(args[1],"Paquete completo: aplicación presente, instalador .NET oficial íntegro, comprobaciones x64/ARM64/x86/Windows correctas. No se ejecutaron instaladores.\r\n");return 0;
        }
        if(args.Length!=0)return 1;
        SystemInfo info;GetNativeSystemInfo(out info);if(!Supported(info.Architecture,Environment.OSVersion.Version.Major)){MessageBox.Show(T("Este paquete requiere Windows 10 u 11 de 64 bits, con procesador Intel o AMD.","This package requires 64-bit Windows 10 or 11 on an Intel or AMD processor.","Este pacote requer Windows 10 ou 11 de 64 bits, com processador Intel ou AMD.","此软件包需要搭载 Intel 或 AMD 处理器的 64 位 Windows 10 或 11。","Цей пакет потребує 64-бітної Windows 10 або 11 на процесорі Intel чи AMD."),"Pulso");return 1;}
        if(!File.Exists(app))throw new IOException(T("Extrae todo el ZIP antes de abrir Pulso. Conserva juntas las carpetas Aplicacion y Requisitos.","Extract the entire ZIP before opening Pulso. Keep the Aplicacion and Requisitos folders together.","Extraia todo o ZIP antes de abrir o Pulso. Mantenha juntas as pastas Aplicacion e Requisitos.","打开 Pulso 前，请解压整个 ZIP，并将 Aplicacion 和 Requisitos 文件夹保留在一起。","Перш ніж відкривати Pulso, розпакуйте весь ZIP. Зберігайте папки Aplicacion і Requisitos разом."));
        if(!Ready()){
            var answer=MessageBox.Show(T("Falta .NET Framework 4.8, necesario para abrir Pulso. El instalador oficial de Microsoft está incluido. ¿Quieres abrirlo ahora? Windows puede solicitar permisos o un reinicio.",".NET Framework 4.8 is required to open Pulso. The official Microsoft installer is included. Open it now? Windows may request permission or a restart.","O .NET Framework 4.8 é necessário para abrir o Pulso. O instalador oficial da Microsoft está incluído. Deseja abri-lo agora? O Windows pode pedir permissão ou reinicialização.","打开 Pulso 需要 .NET Framework 4.8。软件包已包含 Microsoft 官方安装程序。现在打开吗？Windows 可能请求权限或要求重启。","Для запуску Pulso потрібен .NET Framework 4.8. Офіційний інсталятор Microsoft є в пакеті. Відкрити його зараз? Windows може попросити дозвіл або перезавантаження."),"Pulso",MessageBoxButtons.YesNo,MessageBoxIcon.Information);
            if(answer!=DialogResult.Yes)return 0;
            if(!File.Exists(runtime) || HashFile(runtime)!=RuntimeHash)throw new IOException(T("El instalador incluido no superó la verificación. Descarga de nuevo el paquete completo.","The bundled installer failed verification. Download the complete package again.","O instalador incluído não passou na verificação. Baixe o pacote completo novamente.","内附安装程序未通过验证。请重新下载完整软件包。","Інсталятор не пройшов перевірку. Завантажте повний пакет ще раз."));
            using(var process=Process.Start(new ProcessStartInfo(runtime){UseShellExecute=true,Verb="runas",WorkingDirectory=Path.GetDirectoryName(runtime)})){process.WaitForExit();
                if(process.ExitCode==3010 || process.ExitCode==1641){MessageBox.Show(T("Reinicia Windows y vuelve a abrir Pulso.","Restart Windows, then open Pulso again.","Reinicie o Windows e abra o Pulso novamente.","请重启 Windows，然后再次打开 Pulso。","Перезавантажте Windows і знову відкрийте Pulso."),"Pulso");return 0;}
                if(process.ExitCode!=0 || !Ready())throw new IOException(T("No se completó la preparación de .NET. Finaliza el instalador de Microsoft y vuelve a abrir Pulso.",".NET setup did not finish. Complete the Microsoft installer, then open Pulso again.","A preparação do .NET não foi concluída. Finalize o instalador da Microsoft e abra o Pulso novamente.",".NET 设置未完成。请完成 Microsoft 安装程序，然后再次打开 Pulso。","Налаштування .NET не завершено. Завершіть роботу інсталятора Microsoft і знову відкрийте Pulso."));
            }
        }
        SetCurrentProcessExplicitAppUserModelID("Pulso.UsbControl");Process.Start(new ProcessStartInfo(app){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(app)});return 0;
    }catch(Win32Exception ex){if(ex.NativeErrorCode==1223)return 0;MessageBox.Show(ex.Message,"Pulso");return 1;}catch(Exception ex){if(args.Length==2 && args[0]=="--self-test")File.WriteAllText(args[1],ex.ToString());else MessageBox.Show(ex.Message,"Pulso");return 1;}}
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace PulsoUsb {
    internal sealed class LanguageChoice {
        public string Code,Name;
        public override string ToString(){return Name;}
    }
    internal static class L {
        internal static readonly LanguageChoice[] Languages={new LanguageChoice{Code="es",Name="Español"},new LanguageChoice{Code="en",Name="English"},new LanguageChoice{Code="pt",Name="Português"},new LanguageChoice{Code="zh",Name="简体中文"},new LanguageChoice{Code="uk",Name="Українська"}};
        static readonly string[] Cultures={"es-ES","en-US","pt-BR","zh-CN","uk-UA"};
        static readonly Dictionary<string,string[]> Catalog=LoadCatalog();
        static int index;
        public static string Code {get{return Languages[index].Code;}}
        public static CultureInfo Culture {get{return CultureInfo.GetCultureInfo(Cultures[index]);}}
        public static string PreferencePath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Pulso","language.txt");}}
        static Dictionary<string,string[]> LoadCatalog(){var rows=new Dictionary<string,string[]>(StringComparer.Ordinal);using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Idiomas.txt"))using(var reader=new StreamReader(stream,Encoding.UTF8)){string line;while((line=reader.ReadLine())!=null){if(line.Length==0 || line.StartsWith("#"))continue;string[] cells=line.Split('|');if(cells.Length!=5 || cells.Any(String.IsNullOrWhiteSpace))throw new InvalidDataException("Invalid language entry: "+line);rows.Add(cells[0],cells);}}return rows;}
        public static string T(string source){if(source==null)return "";string[] values;return Catalog.TryGetValue(source,out values)?values[index]:source;}
        public static string F(string source,params object[] values){return String.Format(Culture,T(source),values);}
        public static string Key(string source){using(var sha=SHA256.Create())return "loc_"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-","").Substring(0,16);}
        public static bool Has(string source){return Catalog.ContainsKey(source);}
        public static void Set(string code){int next=Array.FindIndex(Languages,x=>x.Code==code);if(next<0)throw new ArgumentException("Unknown language.");index=next;Thread.CurrentThread.CurrentCulture=Culture;Thread.CurrentThread.CurrentUICulture=Culture;CultureInfo.DefaultThreadCurrentCulture=Culture;CultureInfo.DefaultThreadCurrentUICulture=Culture;}
        public static string ReadPreference(string path){try{string code=File.ReadAllText(path,Encoding.UTF8).Trim();return Languages.Any(x=>x.Code==code)?code:"es";}catch{return "es";}}
        public static void SavePreference(string path,string code){if(!Languages.Any(x=>x.Code==code))throw new ArgumentException("Unknown language.");string folder=Path.GetDirectoryName(path);Directory.CreateDirectory(folder);string temp=Path.Combine(folder,"language-"+Guid.NewGuid().ToString("N")+".tmp");try{File.WriteAllText(temp,code,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
        public static string[] Initialize(string[] args){string code=ReadPreference(PreferencePath);if(args.Length>=2 && args[args.Length-2]=="--language"){code=args[args.Length-1];args=args.Take(args.Length-2).ToArray();}Set(code);return args;}
        public static void Apply(Window window){foreach(var row in Catalog)window.Resources[Key(row.Key)]=row.Value[index];window.Language=XmlLanguage.GetLanguage(Cultures[index]);window.FontFamily=new FontFamily(index==3?"Microsoft YaHei UI, Microsoft YaHei, Segoe UI":"Segoe UI, Microsoft YaHei UI");window.Title="Pulso · "+T("USB CONTROL");}
        public static string Rate(int speed,int interval,bool requested){if(interval==0)return T("Valor del dispositivo");if(interval<0 || interval>255)return T("Valor no reconocido");if(speed<0 || speed>2)return F("bInterval={0} (velocidad desconocida)",interval);if(speed==2 && interval>16)return "bInterval="+interval;double hz=speed==2?8000.0/Math.Pow(2,interval-1):1000.0/interval;return requested?F("{0} Hz solicitados",hz.ToString("0.##",Culture)):hz.ToString("0.##",Culture)+" Hz";}
        public static string Error(string message){if(String.IsNullOrEmpty(message))return "";if(Has(message))return T(message);var device=Regex.Match(message,@"^Windows informa un problema de dispositivo \((\d+)\)\.$");if(device.Success)return F("Windows informa un problema de dispositivo ({0}).",device.Groups[1].Value);
            foreach(var prefix in Catalog.Keys.Where(x=>x.EndsWith(" ")).OrderByDescending(x=>x.Length)){if(message.StartsWith(prefix,StringComparison.Ordinal))return T(prefix)+String.Join(" / ",message.Substring(prefix.Length).Split(new[]{" / "},StringSplitOptions.None).Select(Error));}return message;
        }
        public static void ValidateCatalog(){var keys=new HashSet<string>();foreach(var row in Catalog){if(!keys.Add(Key(row.Key)))throw new Exception("Duplicate resource key.");var placeholders=Regex.Matches(row.Key,@"\{\d+\}").Cast<Match>().Select(x=>x.Value).OrderBy(x=>x).ToArray();foreach(var value in row.Value){var translated=Regex.Matches(value,@"\{\d+\}").Cast<Match>().Select(x=>x.Value).OrderBy(x=>x).ToArray();if(!placeholders.SequenceEqual(translated))throw new Exception("Translation placeholders differ: "+row.Key);String.Format(CultureInfo.InvariantCulture,value,Enumerable.Repeat<object>("test",10).ToArray());}}}
    }
}

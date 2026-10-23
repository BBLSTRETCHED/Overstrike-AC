// Confirmed configuration edits for suit mods. GPL-3.0; see LICENSE.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Overstrike.ArkhamCity;
public class SuitRegistration {
    [JsonPropertyName("config")] public string Config {get;set;}="";
    [JsonPropertyName("lines")] public List<string> Lines {get;set;}=new();
    [JsonPropertyName("backup")] public string Backup {get;set;}="";
}
public sealed partial class CityStore {
    record AutoSuitConfig(string Game,string Config);
    public string? ResolveAutoSuitConfig() {
        var path=Path.Combine(Home,"auto-suit-config.json");
        if(File.Exists(path)) {
            var saved=JsonSerializer.Deserialize<AutoSuitConfig>(File.ReadAllText(path));
            if(saved!=null&&Path.GetFullPath(saved.Game).Equals(Path.GetFullPath(Game()),StringComparison.OrdinalIgnoreCase)) {
                var target=ConfigPath(saved.Config);
                if(!Parse(ReadConfig(target).Text,saved.Config).Any())throw new IOException("The saved suit config no longer contains suit entries. Use Settings to choose the active suit config again.");
                return saved.Config;
            }
        }
        var candidates=SuitConfigCandidates();return candidates.Count==1?candidates[0]:null;
    }
    public void SetAutoSuitConfig(string config) {
        var path=ConfigPath(config);
        if(!Parse(ReadConfig(path).Text,config).Any())throw new IOException("Choose a config containing existing PlayableCharactersV2 entries. Required suit-menu dependencies may be missing.");
        var file=Path.Combine(Home,"auto-suit-config.json");var temp=file+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(new AutoSuitConfig(Game(),config)));File.Move(temp,file,true);
    }
    public void InstallAutomatic(CityMod mod) {
        bool suits=Parse(mod.Suitcode,mod.Name).Any();
        string? config=suits?(mod.Registration?.Config??ResolveAutoSuitConfig()??throw new IOException("Suit config is ambiguous or missing. Choose the active config once in Settings; no file payload was installed.")):null;
        if(suits&&mod.Registration==null)PlanSuitRegistration(mod,config!);
        bool wasActive=mod.Active;
        try {Enable(mod);if(suits)RegisterSuits(mod,config!);}
        catch(Exception error) {
            if(!wasActive&&mod.Active) {
                try{Disable(mod);}catch(Exception rollback){throw new IOException("Automatic install failed: "+error.Message+". Restoration also failed: "+rollback.Message+". Review backups before retrying.",error);}
            }
            throw;
        }
    }
    record ConfigContent(byte[] Bytes,string Text,Encoding Encoding,byte[] Bom);
    static ConfigContent ReadConfig(string path) {
        var bytes=File.ReadAllBytes(path);Encoding encoding;int skip=0;
        if(bytes.Take(2).SequenceEqual(new byte[]{255,254})){encoding=new UnicodeEncoding(false,false,true);skip=2;}
        else if(bytes.Take(2).SequenceEqual(new byte[]{254,255})){encoding=new UnicodeEncoding(true,false,true);skip=2;}
        else if(bytes.Take(3).SequenceEqual(new byte[]{239,187,191})){encoding=new UTF8Encoding(false,true);skip=3;}
        else {
            int sample=Math.Min(bytes.Length,1000);
            if(sample>=4&&Enumerable.Range(0,sample/2).Count(i=>bytes[i*2+1]==0)>sample/5)encoding=new UnicodeEncoding(false,false,true);
            else if(sample>=4&&Enumerable.Range(0,sample/2).Count(i=>bytes[i*2]==0)>sample/5)encoding=new UnicodeEncoding(true,false,true);
            else {encoding=new UTF8Encoding(false,true);try{encoding.GetString(bytes);}catch(DecoderFallbackException){encoding=Encoding.Latin1;}}
        }
        return new(bytes,encoding.GetString(bytes,skip,bytes.Length-skip),encoding,bytes.Take(skip).ToArray());
    }
    string ConfigPath(string rel) {
        if(!rel.Replace('\\','/').StartsWith("BmGame/Config/",StringComparison.OrdinalIgnoreCase)||!new[]{".ini",".cfg",".txt"}.Contains(Path.GetExtension(rel).ToLowerInvariant()))throw new IOException("Choose an existing config file beneath the game's BmGame/Config folder.");
        var path=Safe(Game(),rel);var configRoot=Path.GetFullPath(Path.Combine(Game(),"BmGame","Config"))+Path.DirectorySeparatorChar;if(!path.StartsWith(configRoot,StringComparison.OrdinalIgnoreCase))throw new IOException("Config target escaped BmGame/Config.");if(!File.Exists(path))throw new IOException("Config file not found: "+rel);if(new FileInfo(path).Length>8*1024*1024)throw new IOException("Config exceeds the 8 MiB safety limit.");return path;
    }
    public List<string> SuitConfigCandidates() {
        var root=Game();var folder=Path.Combine(root,"BmGame","Config");var result=new List<string>();
        if(!Directory.Exists(folder))return result;
        var options=new EnumerationOptions {RecurseSubdirectories=true,IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint};
        foreach(var p in Directory.EnumerateFiles(folder,"*",options)) {
            if(!new[]{".ini",".cfg",".txt"}.Contains(Path.GetExtension(p).ToLowerInvariant())||new FileInfo(p).Length>8*1024*1024)continue;
            try{if(Parse(ReadConfig(Safe(root,Path.GetRelativePath(root,p))).Text,p).Any())result.Add(Path.GetRelativePath(root,p));}catch(IOException){}
        }
        return result;
    }
    string BackupConfig(CityMod mod,string path) {
        var folder=Path.Combine(Home,"config-backups",mod.Id);Directory.CreateDirectory(folder);
        var backup=Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+"-"+Path.GetFileName(path));File.Copy(path,backup);return backup;
    }
    static void WriteConfig(string path,ConfigContent before,string text) {
        if(!File.ReadAllBytes(path).SequenceEqual(before.Bytes))throw new IOException("Config changed during the operation. Retry after closing other editors.");
        var temp=path+".overstrikeac-"+Guid.NewGuid().ToString("N")+".tmp";
        try {File.WriteAllBytes(temp,before.Bom.Concat(before.Encoding.GetBytes(text)).ToArray());File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public List<string> PlanSuitRegistration(CityMod mod,string config) {
        var target=ConfigPath(config);var before=ReadConfig(target);
        if(!Parse(before.Text,config).Any())throw new IOException("This file has no existing PlayableCharactersV2 entries. Confirm the correct active suit config instead of editing an arbitrary file.");
        var all=new List<CitySuit>();foreach(var candidate in SuitConfigCandidates())all.AddRange(Parse(ReadConfig(ConfigPath(candidate)).Text,candidate));
        var used=all.Select(s=>int.TryParse(s.Id,out int id)?id:-1).Where(i=>i>=0).ToHashSet();
        var existing=Parse(before.Text,config).Select(s=>s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);long next=Math.Max(100L,used.Count==0?100L:(long)used.Max()+1);
        var lines=new List<string>();
        foreach(var raw in mod.Suitcode.Split('\n')) {
            string line=raw.Trim();var entry=Parse(line,mod.Name).FirstOrDefault();if(entry==null)continue;
            if(!Regex.IsMatch(entry.Name,@"^[A-Za-z0-9_.-]+$")||!int.TryParse(entry.BaseId,out var baseId)||baseId<0||baseId>255)throw new IOException("Suitcode has unsupported Name or BaseId. Review it manually.");
            if(existing.Contains(entry.Name))continue;
            if(next>int.MaxValue)throw new IOException("No safe suit ID range available.");
            if(!Regex.IsMatch(line,@"\bId\s*=",RegexOptions.IgnoreCase))throw new IOException("Suitcode entry has no Id field.");
            line=Regex.Replace(line,@"\bId\s*=\s*[^,)]*","Id="+next,RegexOptions.IgnoreCase);
            lines.Add(line);existing.Add(entry.Name);next++;
        }
        return lines;
    }
    public int RegisterSuits(CityMod mod,string config) {
        if(!mod.Active)throw new IOException("Install the mod's file payload first, then register its suits.");
        if(mod.Registration!=null) {
            ValidateRegistrationRemoval(mod);
            var current=ReadConfig(ConfigPath(mod.Registration.Config)).Text.Split('\n').Select(l=>l.Trim()).ToHashSet();
            if(mod.Registration.Lines.All(current.Contains))return 0;
            // An unmodified but missing tracked line can be safely repaired. Keep
            // external edits protected, remove only remaining owned lines, then
            // re-plan IDs against current config instead of reusing stale IDs.
            string oldConfig=mod.Registration.Config;
            RemoveSuitRegistration(mod);Save();
            return RegisterSuits(mod,oldConfig);
        }
        var path=ConfigPath(config);var before=ReadConfig(path);var lines=PlanSuitRegistration(mod,config);if(lines.Count==0){mod.Registration=new SuitRegistration {Config=config};Save();SetAutoSuitConfig(config);return 0;}
        string newline=before.Text.Contains("\r\n")?"\r\n":before.Text.Contains('\n')?"\n":"\r\n";
        var anchor=Regex.Matches(before.Text,@"(?m)^[ \t]*\+?PlayableCharactersV2[ \t]*=[^\r\n]*(?:\r\n|\n|\r|$)").Cast<Match>().Last();
        int insert=anchor.Index+anchor.Length;
        // Insert after existing entries, not at EOF: the next INI section must stay intact.
        string text=before.Text[..insert]+(anchor.Value.EndsWith('\n')||anchor.Value.EndsWith('\r')?"":newline)+string.Join(newline,lines)+newline+before.Text[insert..];
        var registration=new SuitRegistration {Config=config,Lines=lines,Backup=BackupConfig(mod,path)};
        mod.Registration=registration;Save();
        try{WriteConfig(path,before,text);}catch{mod.Registration=null;Save();throw;}
        SetAutoSuitConfig(config);return lines.Count;
    }
    public void ValidateRegistrationRemoval(CityMod mod) {
        if(mod.Registration==null||mod.Registration.Lines.Count==0)return;
        var current=ReadConfig(ConfigPath(mod.Registration.Config)).Text;
        foreach(var line in mod.Registration.Lines) {
            var owned=Parse(line,mod.Name).Single();
            foreach(var actual in current.Split('\n').Where(l=>Parse(l,"").Any(e=>e.Name.Equals(owned.Name,StringComparison.OrdinalIgnoreCase))))
                if(actual.Trim()!=line.Trim())throw new IOException("Tracked suit entry changed externally: "+owned.Name+". Resolve it manually; no config lines will be guessed or deleted.");
        }
    }
    void RemoveSuitRegistration(CityMod mod) {
        if(mod.Registration==null)return;if(mod.Registration.Lines.Count==0){mod.Registration=null;return;}ValidateRegistrationRemoval(mod);
        var path=ConfigPath(mod.Registration.Config);var before=ReadConfig(path);var owned=mod.Registration.Lines.Select(l=>l.Trim()).ToHashSet();
        var text=string.Concat(Regex.Matches(before.Text,@"[^\r\n]*(?:\r\n|\r|\n|$)").Cast<Match>().Where(m=>m.Length>0&&!owned.Contains(m.Value.Trim())).Select(m=>m.Value));
        if(text!=before.Text){BackupConfig(mod,path);WriteConfig(path,before,text);}
        mod.Registration=null;
    }
}

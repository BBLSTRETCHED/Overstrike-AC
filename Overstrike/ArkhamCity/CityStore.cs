// Arkham City fork additions. GPL-3.0; see LICENSE.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Overstrike.ArkhamCity;
public class CityState {
    [JsonPropertyName("game")] public string Game { get; set; } = "";
    [JsonPropertyName("mods")] public List<CityMod> Mods { get; set; } = new();
    [JsonExtensionData] public Dictionary<string,JsonElement>? Extra { get; set; }
}
public class CityMod {
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("files")] public List<string> Files { get; set; } = new();
    [JsonPropertyName("active")] public bool Active { get; set; }
    [JsonPropertyName("records")] public List<CityRecord> Records { get; set; } = new();
    [JsonPropertyName("suitcode")] public string Suitcode { get; set; } = "";
    [JsonPropertyName("registration")] public SuitRegistration? Registration {get;set;}
}
public class CityRecord {
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("existed")] public bool Existed { get; set; }
    [JsonPropertyName("hash")] public string Hash { get; set; } = "";
}
public record CitySuit(string Name, string Id, string BaseId, string Source, string Status);
public static class CitySelection {
    public static int ToggleVisible(IEnumerable<CityMod> visible,IDictionary<string,bool> desired) {
        var mods=visible.ToList();if(mods.Count==0)return 0;
        bool target=!mods.All(m=>desired.TryGetValue(m.Id,out var value)?value:m.Active);
        foreach(var m in mods)desired[m.Id]=target;
        return mods.Count;
    }
}
public sealed partial class CityStore {
    public static string Home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArkhamCityManager");
    public CityState State { get; }
    public List<CitySuit> Suits { get; private set; } = new();
    public List<string> Candidates { get; private set; } = new();
    public CityStore() {
        Directory.CreateDirectory(Home);
        var db=Path.Combine(Home,"state.json");
        State=File.Exists(db) ? JsonSerializer.Deserialize<CityState>(File.ReadAllText(db)) ?? throw new IOException("Invalid state file") : new();
    }
    public void Save() {
        string db=Path.Combine(Home,"state.json"), temp=db+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(State,new JsonSerializerOptions { WriteIndented=true }));
        File.Move(temp,db,true);
    }
    public string Game() {
        if (string.IsNullOrWhiteSpace(State.Game) || !Directory.Exists(Path.Combine(State.Game,"BmGame","CookedPCConsole"))) throw new IOException("Choose the GOTY root containing BmGame/CookedPCConsole.");
        return State.Game;
    }
    public static string Safe(string root,string rel) {
        var full=Path.GetFullPath(Path.Combine(root,rel));
        string basePath=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if (!full.StartsWith(basePath,StringComparison.OrdinalIgnoreCase)) throw new IOException("Unsafe path: "+rel);
        // Never follow a junction/symlink into another location while writing or scanning.
        for (var current=new FileInfo(full) as FileSystemInfo; current!=null; current=current is DirectoryInfo d ? d.Parent : ((FileInfo)current).Directory) {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint)!=0) throw new IOException("Linked paths are not supported: "+full);
            if (string.Equals(current.FullName,Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase)) break;
        }
        return full;
    }
    public static string Hash(string path) { using var f=File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant(); }
    public CityMod Import(string path) {
        var m=new CityMod {Id=Guid.NewGuid().ToString("N"),Name=Path.GetFileNameWithoutExtension(path)};
        string folder=Path.Combine(Home,"library",m.Id); Directory.CreateDirectory(folder);
        try {
            using var zip=ZipFile.OpenRead(path);
            if (zip.Entries.Sum(e=>e.Length)>8L*1024*1024*1024) throw new IOException("Archive exceeds 8 GiB safety limit.");
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var e in zip.Entries) {
                if (string.IsNullOrEmpty(e.Name)) continue;
                var parts=e.FullName.Replace('\\','/').Split('/');
                if (e.FullName.StartsWith('/') || parts.Any(p=>p==".." || p.Contains(':')) || ((e.ExternalAttributes>>16)&0xf000)==0xa000) throw new IOException("Unsafe archive entry.");
                if (parts.Any(p=>p.Equals("screenshots",StringComparison.OrdinalIgnoreCase)||p.Equals("voice lines",StringComparison.OrdinalIgnoreCase))) continue;
                int index=Array.FindIndex(parts,p=>p.Equals("CookedPCConsole",StringComparison.OrdinalIgnoreCase)||p.Equals("Localization",StringComparison.OrdinalIgnoreCase));
                if (index<0) {
                    if (Path.GetFileNameWithoutExtension(e.Name).Contains("suitcode",StringComparison.OrdinalIgnoreCase) && e.Length<100000) { using var r=new StreamReader(e.Open()); m.Suitcode+=r.ReadToEnd()+"\n"; }
                    continue;
                }
                var rel="BmGame/"+string.Join('/',parts.Skip(index));
                if (!seen.Add(rel)) throw new IOException("Duplicate archive destination: "+rel);
                if (new[]{".exe",".dll",".bat",".cmd",".ps1"}.Contains(Path.GetExtension(rel).ToLowerInvariant())) throw new IOException("Executable payloads are unsupported.");
                var target=Safe(folder,rel); Directory.CreateDirectory(Path.GetDirectoryName(target)!); e.ExtractToFile(target); m.Files.Add(rel);
            }
            if (m.Files.Count==0) throw new IOException("No CookedPCConsole or Localization files found. Extract RAR/7z and re-zip one variant first.");
            State.Mods.Add(m); Save(); return m;
        } catch { State.Mods.Remove(m); Directory.Delete(folder,true); throw; }
    }
    public void Enable(CityMod m) {
        if(m.Active) return;
        var root=Game(); var occupied=State.Mods.Where(x=>x.Active).SelectMany(x=>x.Records).Select(r=>r.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(m.Files.Any(occupied.Contains)) throw new IOException("File conflict: disable the other managed mod first.");
        string backup=Path.Combine(Home,"backups",m.Id); m.Records.Clear(); m.Active=true; Save();
        try {
            foreach(var rel in m.Files) {
                var target=Safe(root,rel); var source=Safe(Path.Combine(Home,"library",m.Id),rel); var old=Safe(backup,rel); bool existed=File.Exists(target);
                if(existed) {Directory.CreateDirectory(Path.GetDirectoryName(old)!); File.Copy(target,old,true);}
                m.Records.Add(new CityRecord {Path=rel,Existed=existed,Hash=Hash(source)}); Save();
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source,target,true);
            }
        } catch { Disable(m,true); throw; }
    }
    public void Disable(CityMod m,bool recovery=false) {
        if(!m.Active) return;
        var root=Game(); var backup=Path.Combine(Home,"backups",m.Id);
        ValidateRegistrationRemoval(m);
        if(!recovery && m.Records.Any(r=>File.Exists(Safe(root,r.Path))&&!Hash(Safe(root,r.Path)).Equals(r.Hash,StringComparison.OrdinalIgnoreCase))) throw new IOException("Managed files changed externally. Refusing to overwrite. Restore manually from "+backup);
        foreach(var r in m.Records.Where(r=>r.Existed)) {
            var original=Safe(backup,r.Path);
            if(!File.Exists(original))throw new IOException("Original backup missing for "+r.Path+". No files restored.");
            using var readable=File.OpenRead(original);readable.ReadByte();
        }
        foreach(var r in m.Records.AsEnumerable().Reverse()) {
            var target=Safe(root,r.Path);
            if(r.Existed) {Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(Safe(backup,r.Path),target,true);} else if(File.Exists(target)) File.Delete(target);
        }
        RemoveSuitRegistration(m);m.Active=false; m.Records.Clear(); Save();
    }
    public int CleanManaged() {
        var root=Game();var active=State.Mods.Where(m=>m.Active).ToList();
        // Preflight ALL managed records before the first write. Unmanaged files
        // are never enumerated for deletion by this operation.
        foreach(var m in active)ValidateRegistrationRemoval(m);
        foreach(var m in active)foreach(var r in m.Records) {
            var target=Safe(root,r.Path);
            if(File.Exists(target)&&!Hash(target).Equals(r.Hash,StringComparison.OrdinalIgnoreCase))throw new IOException("Nothing restored: managed file changed externally: "+r.Path+". Resolve it manually before trying again.");
            if(r.Existed) {
                var backup=Safe(Path.Combine(Home,"backups",m.Id),r.Path);
                if(!File.Exists(backup))throw new IOException("Nothing restored: original backup missing for "+r.Path);
                using var readable=File.OpenRead(backup);readable.ReadByte();
            }
        }
        foreach(var m in active)Disable(m);
        return active.Count;
    }
    public static IEnumerable<CitySuit> Parse(string text,string source,string status="Registered · read-only") {
        foreach(var line in text.Split('\n')) {
            var match=Regex.Match(line.Trim(),@"^\+?PlayableCharactersV2\s*=\s*\((.*)\)",RegexOptions.IgnoreCase);
            if(!match.Success) continue;
            var fields=Regex.Matches(match.Groups[1].Value,"(\\w+)\\s*=\\s*(\"[^\"\\r\\n]*\"|[^,)]*)").Cast<Match>().GroupBy(m=>m.Groups[1].Value,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Last().Groups[2].Value.Trim().Trim('"'),StringComparer.OrdinalIgnoreCase);
            if(fields.TryGetValue("Name",out var name)) yield return new(name,fields.GetValueOrDefault("Id","unknown"),fields.GetValueOrDefault("BaseId","unknown"),source,status);
        }
    }
    public void Scan() {
        var root=Game(); var suits=new List<CitySuit>(); var candidates=new List<string>();
        var options=new EnumerationOptions {RecurseSubdirectories=true,IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint};
        var config=Path.Combine(root,"BmGame","Config");
        if(Directory.Exists(config)) foreach(var path in Directory.EnumerateFiles(config,"*",options)) {
            if(!new[]{".ini",".txt",".cfg"}.Contains(Path.GetExtension(path).ToLowerInvariant()) || new FileInfo(path).Length>8*1024*1024) continue;
            try { using var reader=new StreamReader(Safe(root,Path.GetRelativePath(root,path)),Encoding.UTF8,true); suits.AddRange(Parse(reader.ReadToEnd(),Path.GetRelativePath(root,path))); } catch(IOException) { }
        }
        var owned=State.Mods.Where(m=>m.Active).SelectMany(m=>m.Files).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var path in Directory.EnumerateFiles(Path.Combine(root,"BmGame","CookedPCConsole"),"*",options)) {
            var rel=Path.GetRelativePath(root,path).Replace('\\','/');
            if(new[]{".upk",".u",".xxx"}.Contains(Path.GetExtension(path).ToLowerInvariant())&&!owned.Contains(rel)) candidates.Add(rel);
        }
        Suits=suits.Distinct().ToList(); Candidates=candidates; ScanFiles(root);
    }
    public IEnumerable<CitySuit> AllSuits() {
        var result=new List<CitySuit>(Suits); var names=result.Select(s=>s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var m in State.Mods.Where(m=>m.Active)) {
            var entries=Parse(m.Suitcode,m.Name,"Managed payload · registration unverified").ToList();
            if(entries.Count==0) entries.Add(new(m.Name,"unknown","unknown",m.Name,"Managed payload · registration unverified"));
            foreach(var e in entries) if(names.Add(e.Name)) result.Add(e);
        }
        return result;
    }
}

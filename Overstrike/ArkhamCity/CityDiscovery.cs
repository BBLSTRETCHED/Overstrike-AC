// Read-only discovery additions. GPL-3.0; see LICENSE.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Overstrike.ArkhamCity;
public record FileStamp(long Size,string Hash);
public record ArchiveReference(string Name,string ArchiveName,Dictionary<string,FileStamp> Files);
public record CleanBaseline(string Source,string CreatedUtc,Dictionary<string,FileStamp> Files);
public record CityFinding(string Name,string Category,string Status,string Source,string Evidence,bool Unverified=false);
public sealed partial class CityStore {
    public List<CityFinding> Findings {get;private set;}=new();
    public List<string> ScanWarnings {get;private set;}=new();
    static string Normalize(string p)=>p.Replace('\\','/');
    static EnumerationOptions Options()=>new() {RecurseSubdirectories=true,IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint};
    public static string Category(string rel)=>Normalize(rel).Contains("/ShaderFixes/",StringComparison.OrdinalIgnoreCase)?"SHADER":Path.GetExtension(rel).ToLowerInvariant() switch {
        ".upk" or ".u" or ".xxx"=>"PACKAGE",
        ".tfc" or ".dds" or ".png"=>"TEXTURE",
        ".hlsl" or ".hlsli" or ".cso" or ".fx" or ".bin"=>"SHADER",
        ".ini" or ".cfg"=>"CONFIG",
        ".dll" or ".asi" or ".exe"=>"BINARY",
        ".cs" or ".lua" or ".py" or ".bat" or ".cmd" or ".ps1"=>"SCRIPT",
        ".wem" or ".ogg" or ".wav" or ".mp3" or ".bnk"=>"AUDIO",
        ".int" or ".fra" or ".deu" or ".esn"=>"LOCALE",
        _=>"FILE"
    };
    public void CaptureBaseline(string cleanRoot) {
        if(!Directory.Exists(Path.Combine(cleanRoot,"BmGame","CookedPCConsole")))throw new IOException("Not a GOTY game root.");
        if(Path.GetFullPath(cleanRoot).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(Game()).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))throw new IOException("Choose a SEPARATE known-clean installation, not the selected modded folder.");
        var files=new Dictionary<string,FileStamp>(StringComparer.OrdinalIgnoreCase);
        foreach(var path in Directory.EnumerateFiles(cleanRoot,"*",Options())) {
            var rel=Normalize(Path.GetRelativePath(cleanRoot,path));Safe(cleanRoot,rel);files.Add(rel,new(new FileInfo(path).Length,Hash(path)));
        }
        File.WriteAllText(Path.Combine(Home,"clean-baseline.json"),JsonSerializer.Serialize(new CleanBaseline(cleanRoot,DateTime.UtcNow.ToString("O"),files)));
    }
    public int AddReference(string archive,string name,string loosePrefix,bool remastered=false) {
        var files=new Dictionary<string,FileStamp>(StringComparer.OrdinalIgnoreCase);
        using var zip=ZipFile.OpenRead(archive);
        if(zip.Entries.Sum(e=>e.Length)>8L*1024*1024*1024)throw new IOException("Reference ZIP exceeds 8 GiB safety limit.");
        foreach(var e in zip.Entries) {
            if(string.IsNullOrEmpty(e.Name))continue;
            var raw=Normalize(e.FullName);var parts=raw.Split('/');
            if(raw.StartsWith('/')||parts.Any(p=>p==".."||p.Contains(':'))||((e.ExternalAttributes>>16)&0xf000)==0xa000)throw new IOException("Unsafe reference archive path.");
            if(parts.Any(p=>new[]{"screenshots","voice lines","__MACOSX"}.Contains(p,StringComparer.OrdinalIgnoreCase)))continue;
            int index=Array.FindIndex(parts,p=>new[]{"BmGame","Binaries","Engine"}.Contains(p,StringComparer.OrdinalIgnoreCase));
            int contentIndex=Array.FindIndex(parts,p=>new[]{"CookedPCConsole","Localization","Config"}.Contains(p,StringComparer.OrdinalIgnoreCase));
            string rel;
            if(remastered && e.Name.Equals("BmInput.ini",StringComparison.OrdinalIgnoreCase))rel="@UserConfig/BmInput.ini";
            else if(remastered && parts.Any(p=>p.Equals("ShaderFixes",StringComparison.OrdinalIgnoreCase)))rel="Binaries/Win32/"+string.Join('/',parts.Skip(Array.FindIndex(parts,p=>p.Equals("ShaderFixes",StringComparison.OrdinalIgnoreCase))));
            else if(remastered && new[]{"d3d11.dll","d3dcompiler_46.dll","d3dx.ini","BmInput.dll"}.Contains(e.Name,StringComparer.OrdinalIgnoreCase))rel="Binaries/Win32/"+e.Name;
            else if(index>=0)rel=string.Join('/',parts.Skip(index));
            else if(contentIndex>=0)rel="BmGame/"+string.Join('/',parts.Skip(contentIndex));
            else if(!raw.Contains('/')&& !new[]{".txt",".md",".pdf",".html"}.Contains(Path.GetExtension(raw).ToLowerInvariant()))rel=(loosePrefix.Trim('/')+"/"+raw).TrimStart('/');
            else continue;
            Safe(Home,rel);
            using var stream=e.Open();var hash=Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if(!files.TryAdd(rel,new(e.Length,hash)))throw new IOException("Multiple variants map to "+rel+". Re-zip one variant only.");
        }
        if(files.Count==0)throw new IOException("No directly mappable files. Installer/patch archives need their original installer and cannot be fingerprinted as installed payloads.");
        var refs=ReadReferences();refs.RemoveAll(r=>r.Name.Equals(name,StringComparison.OrdinalIgnoreCase));refs.Add(new(name,Path.GetFileName(archive),files));
        File.WriteAllText(Path.Combine(Home,"archive-references.json"),JsonSerializer.Serialize(refs));return files.Count;
    }
    List<ArchiveReference> ReadReferences() {
        var p=Path.Combine(Home,"archive-references.json");return File.Exists(p)?JsonSerializer.Deserialize<List<ArchiveReference>>(File.ReadAllText(p))??new():new();
    }
    public void SelectUserConfig(string folder) {
        if(!File.Exists(Safe(folder,"BmInput.ini")))throw new IOException("Select the Config folder containing BmInput.ini.");
        File.WriteAllText(Path.Combine(Home,"user-config-folder.json"),JsonSerializer.Serialize(Path.GetFullPath(folder)));
    }
    public void ClearUserConfig() {var p=Path.Combine(Home,"user-config-folder.json");if(File.Exists(p))File.Delete(p);}
    void ScanFiles(string root) {
        Findings=new();ScanWarnings=new();
        var files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var path in Directory.EnumerateFiles(root,"*",Options())) {var rel=Normalize(Path.GetRelativePath(root,path));files[rel]=Safe(root,rel);}
        var selection=Path.Combine(Home,"user-config-folder.json");
        if(File.Exists(selection)) {
            var folder=JsonSerializer.Deserialize<string>(File.ReadAllText(selection));
            if(folder!=null&&Directory.Exists(folder))foreach(var path in Directory.EnumerateFiles(folder,"*",new EnumerationOptions {IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint})) {
                if(new[]{".ini",".cfg"}.Contains(Path.GetExtension(path).ToLowerInvariant()))files["@UserConfig/"+Path.GetFileName(path)]=Safe(folder,Path.GetFileName(path));
            }
            else ScanWarnings.Add("Selected user config folder is unavailable.");
        }
        var hashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        string GetHash(string rel) {
            if(!hashes.TryGetValue(rel,out var hash)){hash=Hash(files[rel]);hashes[rel]=hash;}return hash;
        }
        var owned=State.Mods.Where(m=>m.Active).SelectMany(m=>m.Files).Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var identified=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var reference in ReadReferences()) {
            int exact=0,missing=0,different=0,unreadable=0;
            foreach(var entry in reference.Files) {
                if(!files.TryGetValue(entry.Key,out var path)){missing++;continue;}
                try {
                    if(new FileInfo(path).Length==entry.Value.Size&&GetHash(entry.Key)==entry.Value.Hash){exact++;identified.Add(entry.Key);}else different++;
                }catch(IOException){unreadable++;}
            }
            string state=exact==reference.Files.Count?"Archive files match":exact>0?"Partial match · review":different>0?"Paths present · differs":"No matching payload";
            Findings.Add(new(reference.Name,"MOD REF",state,reference.ArchiveName,$"SHA-256 comparison: {exact}/{reference.Files.Count} exact, {different} different, {missing} missing, {unreadable} unreadable. A full match confirms these bytes, not ownership or runtime compatibility. Partial matches do not prove the mod is installed. @UserConfig entries are compared only after you explicitly select that folder in Settings."));
        }
        string binary="Binaries/Win32/";
        int shaders=files.Keys.Count(p=>p.StartsWith(binary+"ShaderFixes/",StringComparison.OrdinalIgnoreCase));
        if(shaders>0||files.ContainsKey(binary+"d3dx.ini"))Findings.Add(new("Shader injection components","SHADER","Components present",binary,$"Found {shaders} ShaderFixes file(s); d3d11.dll: {files.ContainsKey(binary+"d3d11.dll")}; d3dx.ini: {files.ContainsKey(binary+"d3dx.ini")}. These generic components do NOT identify Arkham City Remastered, prove correct installation, or verify that the game loads them. Add its ZIP fingerprint for byte matching."));
        CleanBaseline? baseline=null;var baselinePath=Path.Combine(Home,"clean-baseline.json");
        if(File.Exists(baselinePath))baseline=JsonSerializer.Deserialize<CleanBaseline>(File.ReadAllText(baselinePath));
        var clean=baseline==null?null:new Dictionary<string,FileStamp>(baseline.Files,StringComparer.OrdinalIgnoreCase);
        foreach(var pair in files) {
            if(owned.Contains(pair.Key))continue;
            if(pair.Key.StartsWith("@UserConfig/",StringComparison.OrdinalIgnoreCase)) {
                if(!identified.Contains(pair.Key))Findings.Add(new(Path.GetFileName(pair.Key),"USER CFG","Unverified user config",pair.Value,"Explicitly selected external Config folder. No clean user-config baseline exists; exact archive fingerprints are needed to identify its contents.",true));
                continue;
            }
            string category=Category(pair.Key),state="Unverified file";bool unknown=baseline==null;
            try {
                if(clean!=null&&clean.TryGetValue(pair.Key,out var stamp)) {
                    if(new FileInfo(pair.Value).Length==stamp.Size&&GetHash(pair.Key)==stamp.Hash)continue;
                    state="Changed vs clean";
                } else if(clean!=null) state="Added vs clean";
                else if(identified.Contains(pair.Key))continue;
                Findings.Add(new(Path.GetFileName(pair.Key),category,state,pair.Key,baseline==null?"No clean baseline. This may be an original game file, unrelated file, or mod. No mod identity is inferred.":"Compared against user-supplied clean snapshot from "+baseline.Source+". Differences can also come from game versions, settings or tools, not just mods.",unknown));
            }catch(IOException e){ScanWarnings.Add(pair.Key+": "+e.Message);}
        }
        if(clean!=null)foreach(var rel in clean.Keys.Where(p=>!files.ContainsKey(p)))Findings.Add(new(Path.GetFileName(rel),Category(rel),"Missing vs clean",rel,"Absent relative to the user-supplied baseline; not evidence of a specific mod."));
        if(baseline==null)ScanWarnings.Add("No clean baseline: unverified files are hidden by default. Enable Show unverified files to inspect them, or add a separate clean installation baseline.");
        ScanWarnings.Add("Scope: selected game root plus the explicitly selected user Config folder, if any. No other external folders, TexMod sessions or running process/memory inspection. Reparse points and inaccessible directories are skipped.");
    }
}

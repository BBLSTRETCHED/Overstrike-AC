// Arkham City fork additions. GPL-3.0; see LICENSE.txt.
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;

namespace Overstrike.ArkhamCity;
public class CityRow : INotifyPropertyChanged {
    public CityMod? Mod {get;init;}
    public CitySuit? Suit {get;init;}
    public CityFinding? Finding {get;init;}
    public bool Managed => Mod!=null;
    public string Name => Mod?.Name ?? Finding?.Name ?? Suit!.Name;
    public string Kind => Managed ? "ZIP" : Finding?.Category ?? "SUIT";
    public string Source => Managed ? string.Join(", ",Mod!.Files.Select(p=>Path.GetDirectoryName(p)).Distinct().Take(2)) : Finding?.Source ?? Suit!.Source;
    bool check;
    public bool Checked {get=>check;set {check=value;PropertyChanged?.Invoke(this,new(nameof(Checked)));PropertyChanged?.Invoke(this,new(nameof(Status)));}}
    public string Status => Mod==null ? Finding?.Status ?? "Detected · read-only" : Checked!=Mod.Active ? (Checked?"Pending enable":"Pending disable") : Mod.Active?(CityStore.Parse(Mod.Suitcode,Mod.Name).Any()&&Mod.Registration==null?"Files only · register suits":"Installed"):"Not installed";
    public event PropertyChangedEventHandler? PropertyChanged;
}
public class CityWindow : Window {
    readonly CityStore store=new();
    readonly CityUi interaction;
    string startupWarning="";
    readonly List<CityRow> rows=new();
    readonly DataGrid list=new();
    readonly TextBox search=new(), suitSearch=new();
    readonly TextBlock status=new(), gamePath=new(), counts=new(), details=new();
    readonly WrapPanel cards=new();
    readonly ComboBox sort=new(), filter=new();
    Button? checkAll,applyAction,revertAction;
    readonly TextBlock managementHint=new();
    CitySuit? selectedSuit;
    readonly Dictionary<string,string> portraits=new();
    bool showUnverified=false, busy=false;
    static Brush Color(string c)=>new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(c));
    static readonly Brush Bg=Color("#141416"), Panel=Color("#242124"), Accent=Color("#9E3541"), Ink=Color("#F0ECE9");
    public CityWindow(CityUi? ui=null) {
        interaction=ui??new CityUi();
        Title="Overstrike AC"; Icon=new BitmapImage(new Uri("pack://application:,,,/Overstrike;component/ArkhamCity/OverstrikeAC.ico")); Width=1280; Height=860; MinWidth=980; MinHeight=650;
        Background=Bg; Foreground=Ink; FontFamily=new FontFamily("Segoe UI"); FontSize=14;
        Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("/Overstrike;component/ArkhamCity/CityTheme.xaml",UriKind.Relative)});
        var root=new DockPanel(); Content=root;
        var banner=new DockPanel {Height=105,Background=Brushes.Black}; DockPanel.SetDock(banner,Dock.Top); root.Children.Add(banner);
        var logoPath=Path.Combine(AppContext.BaseDirectory,"Support","logo.png");
        if(File.Exists(logoPath)) banner.Children.Add(new Image {Source=new BitmapImage(new Uri(logoPath)),Width=210,Margin=new Thickness(14,0,0,0),HorizontalAlignment=HorizontalAlignment.Left});
        else banner.Children.Add(Text("BATMAN  /  ARKHAM CITY",24));
        banner.Children.Add(new TextBlock {Text="OVERSTRIKE AC  /  GOTY",Foreground=Ink,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(25)});
        var profile=new DockPanel {Background=Panel,LastChildFill=true}; DockPanel.SetDock(profile,Dock.Top); root.Children.Add(profile);
        profile.Children.Add(Text("Batman Arkham City GOTY  /  PC",14));
        var browse=Button("Browse…",Choose); DockPanel.SetDock(browse,Dock.Right);profile.Children.Add(browse);
        gamePath.Margin=new Thickness(15,12,10,12); gamePath.Foreground=Color("#C4B9BE");profile.Children.Add(gamePath);
        DockPanel.SetDock(status,Dock.Bottom); status.Margin=new Thickness(16,10,16,10);status.Foreground=Color("#D3C5CD");status.FontSize=13;status.TextWrapping=TextWrapping.Wrap; root.Children.Add(status);
        var tabs=new TabControl {Background=Bg,Foreground=Ink,BorderBrush=Color("#553238")}; root.Children.Add(tabs);
        var mods=new DockPanel {Margin=new Thickness(10)}; tabs.Items.Add(new TabItem {Header="MODS",Content=mods});
        var tools=new DockPanel(); DockPanel.SetDock(tools,Dock.Top);mods.Children.Add(tools);
        managementHint.Margin=new Thickness(5,7,5,12);managementHint.Foreground=Color("#D8C5CC");managementHint.TextWrapping=TextWrapping.Wrap;DockPanel.SetDock(managementHint,Dock.Top);mods.Children.Add(managementHint);
        search.Width=240; search.Margin=new Thickness(4);search.Padding=new Thickness(6);search.Background=Panel;search.Foreground=Ink;search.ToolTip="Search names and paths";search.TextChanged+=(_,_)=>FilterRows();tools.Children.Add(search);
        checkAll=Button("Check managed",ToggleVisible);tools.Children.Add(checkAll);
        counts.VerticalAlignment=VerticalAlignment.Center; counts.Margin=new Thickness(8);counts.FontSize=13;counts.Foreground=Color("#C4B9BE");counts.MaxWidth=220;counts.TextTrimming=TextTrimming.CharacterEllipsis;tools.Children.Add(counts);
        var import=Button("+ Install ZIP",Import);DockPanel.SetDock(import,Dock.Right);tools.Children.Add(import);
        sort.ItemsSource=new[]{"Name ↑","Name ↓"};sort.SelectedIndex=0;sort.Width=120;sort.Margin=new Thickness(4);sort.SelectionChanged+=(_,_)=>FilterRows();DockPanel.SetDock(sort,Dock.Right);tools.Children.Add(sort);
        var bottom=new WrapPanel();DockPanel.SetDock(bottom,Dock.Bottom);mods.Children.Add(bottom);
        bottom.Children.Add(Button("Scan game folder",Scan)); bottom.Children.Add(Button("Remove",Remove)); bottom.Children.Add(Button("Restore managed…",CleanInstallation));bottom.Children.Add(Button("Repair suits…",RegisterSelected));
        foreach(var item in new[]{("Launch game",(Action)Launch),("Apply",(Action)Apply),("Revert",(Action)(()=>RefreshRows(false)))}) {var b=Button(item.Item1,item.Item2);if(item.Item1=="Apply")applyAction=b;if(item.Item1=="Revert")revertAction=b;if(item.Item1=="Apply"){b.Background=Color("#A52F43");b.BorderBrush=Color("#F07591");b.FontWeight=FontWeights.SemiBold;}DockPanel.SetDock(b,Dock.Right);bottom.Children.Add(b);}
        list.AutoGenerateColumns=false;list.CanUserAddRows=false;list.CanUserDeleteRows=false;list.CanUserSortColumns=false;list.HeadersVisibility=DataGridHeadersVisibility.Column;list.RowHeight=32;list.GridLinesVisibility=DataGridGridLinesVisibility.None;list.Background=Bg;list.Foreground=Ink;list.RowBackground=Bg;list.AlternatingRowBackground=Color("#181719");list.BorderBrush=Color("#4C353B");list.SelectionMode=DataGridSelectionMode.Single;
        var checkTemplate=(DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Grid><CheckBox x:Name='Toggle' IsChecked='{Binding Checked, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}' HorizontalAlignment='Center' VerticalAlignment='Center' ToolTip='Stage managed ZIP installation/removal; click Apply to commit.'/><TextBlock x:Name='ReadOnly' Text='◆' Visibility='Collapsed' HorizontalAlignment='Center' VerticalAlignment='Center' ToolTip='Detected only: no original-file backups. This row cannot be enabled or disabled.'/></Grid><DataTemplate.Triggers><DataTrigger Binding='{Binding Managed}' Value='False'><Setter TargetName='Toggle' Property='Visibility' Value='Collapsed'/><Setter TargetName='ReadOnly' Property='Visibility' Value='Visible'/></DataTrigger></DataTemplate.Triggers></DataTemplate>");
        list.Columns.Add(new DataGridTemplateColumn {Header="",CellTemplate=checkTemplate,Width=32});
        AddColumn("Kind",82);AddColumn("Name",new DataGridLength(1,DataGridLengthUnitType.Star));AddColumn("Status",190);AddColumn("Source",new DataGridLength(1,DataGridLengthUnitType.Star));
        list.MouseDoubleClick+=(_,e)=>{for(var node=e.OriginalSource as DependencyObject;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))if(node is CheckBox)return;if(list.SelectedItem is CityRow r) {if(r.Managed)r.Checked=!r.Checked;else if(r.Finding!=null) {ShowText(r.Name,r.Status+"\n\n"+r.Source+"\n\n"+r.Finding.Evidence+"\n\nRead-only discovery. No automatic disable or removal.");} else {SelectSuit(r.Suit!);tabs.SelectedIndex=1;}}};
        mods.Children.Add(list);
        var suitPage=new DockPanel {Margin=new Thickness(12)};tabs.Items.Add(new TabItem {Header="SUITS MENU",Content=suitPage});
        var suitTools=new WrapPanel();DockPanel.SetDock(suitTools,Dock.Top);suitPage.Children.Add(suitTools);
        suitTools.Children.Add(Text("SUITS",14));suitSearch.Width=220;suitSearch.Margin=new Thickness(8);suitSearch.TextChanged+=(_,_)=>RenderSuits();suitTools.Children.Add(suitSearch);
        filter.ItemsSource=new[]{"ALL","BATMAN","ROBIN","NIGHTWING","CATWOMAN","OTHER"};filter.SelectedIndex=0;filter.Width=140;filter.Margin=new Thickness(8);filter.SelectionChanged+=(_,_)=>RenderSuits();suitTools.Children.Add(filter);
        var side=new StackPanel {Width=275,Background=Panel};var sideScroll=new ScrollViewer {Content=side,Width=290,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};DockPanel.SetDock(sideScroll,Dock.Right);suitPage.Children.Add(sideScroll);
        details.Text="Select a suit. Detected registrations are read-only.";details.TextWrapping=TextWrapping.Wrap;details.Margin=new Thickness(15);side.Children.Add(details);
        side.Children.Add(Button("Assign portrait…",Portrait));side.Children.Add(Button("Scan game folder",Scan));side.Children.Add(Button("Repair selected ZIP’s suits…",RegisterSelected));side.Children.Add(Button("View scan report",Report));
        side.Children.Add(new TextBlock {Text="Included suitcode is registered automatically. Suit-menu reordering is not supported. Missing portraits use placeholders.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(15),Foreground=Ink});
        suitPage.Children.Add(new ScrollViewer {Content=cards,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        tabs.Items.Add(new TabItem {Header="SETTINGS",Content=BuildSettings()});
        LoadPortraits();RefreshRows(false);gamePath.Text=store.State.Game;status.Text="Ready. Existing Python library is read from the same application-data folder. Do not run both managers at once. "+startupWarning;
        Loaded+=(_,_)=>{if(!string.IsNullOrWhiteSpace(store.State.Game))Run(Scan);};
    }
    protected override void OnClosing(CancelEventArgs e) {
        if(busy) {e.Cancel=true;status.Text="Please wait for the current operation to finish before closing Overstrike AC.";}
        base.OnClosing(e);
    }
    FrameworkElement BuildSettings() {
        var page=new StackPanel {Margin=new Thickness(26,20,26,28),MaxWidth=1120,HorizontalAlignment=HorizontalAlignment.Stretch};
        page.Children.Add(new TextBlock {Text="Settings & maintenance",FontSize=28,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,6)});
        page.Children.Add(new TextBlock {Text="Choose how the manager discovers files and identifies installed mods.",Foreground=Color("#C9BDC3"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,20)});
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});page.Children.Add(grid);
        StackPanel Section(string title,string description,int row,int col) {
            var content=new StackPanel();content.Children.Add(new TextBlock {Text=title,FontSize=19,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)});
            content.Children.Add(new TextBlock {Text=description,TextWrapping=TextWrapping.Wrap,LineHeight=22,Foreground=Color("#C9BDC3"),Margin=new Thickness(0,0,0,14)});
            var border=new Border {Background=Color("#211D22"),BorderBrush=Color("#53434D"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(22),Margin=new Thickness(0,0,16,16),Child=content};
            Grid.SetRow(border,row);Grid.SetColumn(border,col);grid.Children.Add(border);return content;
        }
        var identify=Section("Identify mods","Match installed file contents against a ZIP you provide. Comparisons are read-only and do not install the mod.",0,0);
        identify.Children.Add(Button("Add mod ZIP fingerprint",AddReference));identify.Children.Add(Button("Add Remastered fingerprint",()=>Reference(true)));
        var documents=Section("Documents configuration","Optionally include INI/CFG files in the folder you choose. No other Documents folders are searched or modified.",0,1);
        documents.Children.Add(Button("Choose Config folder…",UserConfig));documents.Children.Add(Button("Stop scanning this folder",()=>{store.ClearUserConfig();Scan();}));
        var discovery=Section("Clean-game comparison","Compare against a separate, unmodified installation of the same game build. Differences are not automatically proof of a mod.",1,0);
        discovery.Children.Add(Button("Choose clean installation…",Baseline));
        var check=new CheckBox {Content=new TextBlock {Text="Show unverified files in Mods\nIncludes original game files; not confirmed mods.",TextWrapping=TextWrapping.Wrap},Margin=new Thickness(0,10,0,4)};
        check.Checked+=(_,_)=>{showUnverified=true;RefreshRows(true);};check.Unchecked+=(_,_)=>{showUnverified=false;RefreshRows(true);};discovery.Children.Add(check);
        var game=Section("Game & library","Managed installation supports CookedPCConsole and Localization ZIP payloads. Close the game before applying changes.",1,1);
        game.Children.Add(Button("Change game location…",Choose));game.Children.Add(Button("Choose automatic suit config…",SetupAutoSuitConfig));game.Children.Add(Button("Repair selected ZIP’s suits…",RegisterSelected));game.Children.Add(Button("View selected mod’s suitcode",Suitcode));
        var clean=Section("Restore managed mods","Restore the original backups for enabled ZIP mods installed by Overstrike AC. This does NOT produce a fully pristine game: manual mods, saves and Documents config are left untouched. Only suit entries added by Overstrike AC are removed.",2,0);
        Grid.SetColumnSpan((FrameworkElement)clean.Parent,2);
        clean.Children.Add(Button("Restore managed mods…",CleanInstallation));
        page.Children.Add(new TextBlock {Text="Storage & safety",FontSize=19,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,8)});
        page.Children.Add(new TextBlock {Text="Library and backups: "+CityStore.Home,Foreground=Color("#C9BDC3"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)});
        page.Children.Add(new TextBlock {Text="Detected files remain read-only without original backups. Suit registration is automatic when the ZIP includes usable suitcode. Resetting detection does not delete game files or managed backups.",TextWrapping=TextWrapping.Wrap,Foreground=Color("#C9BDC3"),LineHeight=22});
        page.Children.Add(Button("Reset detection references & baseline…",ClearDetection));
        return new ScrollViewer {Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    }
    TextBlock Text(string text,int size)=>new() {Text=text,FontSize=size,Margin=new Thickness(12),Foreground=Ink,VerticalAlignment=VerticalAlignment.Center};
    Button Button(string title,Action action) {var b=new Button {Content=title};AutomationProperties.SetAutomationId(b,"action-"+System.Text.RegularExpressions.Regex.Replace(title.ToLowerInvariant(),"[^a-z0-9]+","-").Trim('-'));AutomationProperties.SetName(b,title);b.Click+=(_,_)=>{interaction.ButtonInvoked(AutomationProperties.GetAutomationId(b),title);Run(action);};return b;}
    void AddColumn(string property,DataGridLength width)=>list.Columns.Add(new DataGridTextColumn {Header=property=="Source"?"SOURCE / PATH":property.ToUpperInvariant(),Binding=new Binding(property),Width=width,IsReadOnly=true});
    void Run(Action action) {try {action();}catch(OperationCanceledException) {status.Text="Cancelled. No new installation was completed.";RefreshRows(true);}catch(Exception e) {interaction.Message(this,e.Message,"Action stopped");RefreshRows(true);}}
    void Choose() {
        if(store.State.Mods.Any(m=>m.Active))throw new IOException("Disable managed mods before changing game folders.");
        // .NET 7's built-in file dialog can select the executable without extra shell dependencies.
        var dialog=new OpenFileDialog {Title="Select Binaries/Win32/BatmanAC.exe",Filter="Arkham City executable|BatmanAC.exe"};if(interaction.PickFile(dialog,this)!=true)return;
        var root=Directory.GetParent(dialog.FileName)?.Parent?.Parent?.FullName ?? "";var old=store.State.Game;store.State.Game=root;
        try {store.Game();}catch {store.State.Game=old;throw;}
        store.Save();gamePath.Text=root;Scan();
    }
    void Import() {
        store.Game();EnsureGameClosed();
        var d=new OpenFileDialog {Filter="Mod ZIP archives|*.zip",Title="Overstrike AC — Import and install ZIP"};if(interaction.PickFile(d,this)!=true)return;
        var mod=store.Import(d.FileName);RefreshRows(true);list.SelectedItem=rows.FirstOrDefault(r=>r.Mod==mod);
        EnsureAutoConfig(mod);store.InstallAutomatic(mod);RefreshRows(false);list.SelectedItem=rows.FirstOrDefault(r=>r.Mod==mod);
        Work("Refreshing installed mod discovery…",()=>store.Scan(),CityStore.Parse(mod.Suitcode,mod.Name).Any()?"Installed "+mod.Name+" and registered its suits automatically. Restart the game to test.":"Installed "+mod.Name+". No suitcode was included in the archive.",false);
    }
    void RefreshRows(bool keepPending) {
        var pending=keepPending?rows.Where(r=>r.Managed).ToDictionary(r=>r.Mod!.Id,r=>r.Checked):new Dictionary<string,bool>(); rows.Clear();
        foreach(var m in store.State.Mods) {var row=new CityRow {Mod=m,Checked=pending.GetValueOrDefault(m.Id,m.Active)};row.PropertyChanged+=(_,_)=>UpdateActions();rows.Add(row);}
        var names=store.State.Mods.Where(m=>m.Active).SelectMany(m=>CityStore.Parse(m.Suitcode,m.Name)).Select(s=>s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var s in store.Suits.Where(s=>!names.Contains(s.Name)))rows.Add(new CityRow {Suit=s,Checked=true});
        foreach(var f in store.Findings.Where(f=>showUnverified||!f.Unverified))rows.Add(new CityRow {Finding=f,Checked=false});
        FilterRows();RenderSuits();
    }
    void FilterRows() {
        var shown=rows.Where(r=>(r.Name+" "+r.Source+" "+r.Status).Contains(search.Text,StringComparison.OrdinalIgnoreCase));
        list.ItemsSource=(sort.SelectedIndex==1?shown.OrderByDescending(r=>r.Name,StringComparer.OrdinalIgnoreCase):shown.OrderBy(r=>r.Name,StringComparer.OrdinalIgnoreCase)).ToList();
        UpdateActions();
    }
    void UpdateActions() {
        var visible=list.Items.Cast<CityRow>().Where(r=>r.Managed).ToList();int pending=rows.Count(r=>r.Managed&&r.Checked!=r.Mod!.Active);
        counts.Text=$"{visible.Count} managed · {list.Items.Cast<CityRow>().Count(r=>!r.Managed)} read-only";
        if(checkAll!=null) {checkAll.Content=visible.Count==0?"No managed ZIPs":visible.All(r=>r.Checked)?"Uncheck managed":"Check managed";checkAll.ToolTip="Only visible imported ZIP rows can be checked. Detected entries marked ◆ are read-only.";}
        if(applyAction!=null) {applyAction.IsEnabled=pending>0;applyAction.Content=pending>0?$"Apply ({pending})":"Apply";}
        if(revertAction!=null)revertAction.IsEnabled=pending>0;
        managementHint.Text=store.State.Mods.Count==0?"VIEW-ONLY DISCOVERY  ·  ◆ rows are detected files/suits, not managed installations. Use Install ZIP to install and register a future suit mod automatically; scanning does not create original backups.":$"New ZIP imports install automatically. Checkboxes manage existing library ZIPs only. ◆ detected entries stay read-only. {pending} pending change(s); click Apply to commit.";
    }
    void ToggleVisible() {
        var visible=list.Items.Cast<CityRow>().Where(r=>r.Managed).ToList();
        if(visible.Count==0) {ManagementHelp();return;}
        var desired=visible.ToDictionary(r=>r.Mod!.Id,r=>r.Checked);CitySelection.ToggleVisible(visible.Select(r=>r.Mod!),desired);
        foreach(var r in visible)r.Checked=desired[r.Mod!.Id];UpdateActions();
    }
    void ManagementHelp()=>ShowText("Managed vs detected mods","Why these actions are unavailable\n\n◆ rows were discovered in your existing game. Overstrike AC has no trustworthy original-file backups or complete mod-to-file mapping for them, so it cannot safely check/uncheck or clean them.\n\nInstall ZIP imports, installs and registers included suitcode automatically. Checkboxes apply only to existing managed-library ZIPs; check one and Apply to re-enable it. Restore managed mods only undoes enabled managed installations.\n\nImportant: importing a ZIP over an already manually modded installation does NOT recreate vanilla backups. Its backups would contain whatever files were there before. Restore from a known-clean source first if you need vanilla originals.\n\nTo remove existing manual mods, follow their authors' uninstall instructions or use a separately backed-up clean reinstall. This build will not guess and delete detected files.");
    void Scan()=>Work("Scanning game files… hashing comparisons can take several minutes.",()=>store.Scan());
    async void Work(string message,Action action,string? success=null,bool keepPending=true) {
        if(busy)return;busy=true;status.Text=message;
        if(Content is UIElement element)element.IsEnabled=false;
        try {await Task.Run(action);RefreshRows(keepPending);status.Text=success??$"Scan: {store.Suits.Count} suit entries · {store.Findings.Count(f=>!f.Unverified)} comparison findings · {store.Findings.Count(f=>f.Unverified)} unverified files. Settings contains baseline and archive-matching tools.";}
        catch(Exception e) {interaction.Message(this,e.Message,"Discovery stopped");RefreshRows(keepPending);status.Text="Action stopped. Review the current mod states before retrying; earlier completed changes may remain.";}
        finally {busy=false;if(Content is UIElement el)el.IsEnabled=true;}
    }
    void CleanInstallation() {
        store.Game();
        var active=store.State.Mods.Where(m=>m.Active).ToList();
        if(active.Count==0){ManagementHelp();return;}
        if(interaction.GameRunning())throw new IOException("Close Batman Arkham City before restoring managed mods.");
        string prompt=$"Restore {active.Count} enabled managed mod(s), covering {active.Sum(m=>m.Records.Count)} recorded file(s)?\n\n• Restore original files from Overstrike AC backups.\n• Remove files those managed mods added.\n• Keep the imported ZIP library and backups.\n\nManual mods, saves, user configuration and manually added suit entries are NOT removed. Tracked suit entries added by Overstrike AC ARE removed. This is not a full clean reinstall. Files installed before this manager may already be modded in its backups.\n\nAny pending checkbox selections will be discarded. Make an independent backup first. Continue?";
        if(interaction.Message(this,prompt,"Overstrike AC — Restore managed mods",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        Work("Checking backups and restoring managed mods…",()=>{store.CleanManaged();store.Scan();},"Managed mods restored. Imported archives retained. Manual mods, saves and configuration were left untouched.",false);
    }
    void AddReference()=>Reference(false);
    void Reference(bool remastered) {
        store.Game();var zip=new OpenFileDialog {Title="Select one direct-file mod variant as ZIP — no installers run",Filter="Reference ZIP|*.zip"};if(interaction.PickFile(zip,this)!=true)return;
        string? name=Ask("Mod reference name",remastered?"Batman Arkham City Remastered":Path.GetFileNameWithoutExtension(zip.FileName));if(string.IsNullOrWhiteSpace(name))return;
        string? prefix=remastered?"Binaries/Win32":Ask("Loose files target (explicit archive folders override this)","BmGame/CookedPCConsole");if(prefix==null)return;
        Work("Hashing archive reference and scanning…",()=>{store.AddReference(zip.FileName,name,prefix,remastered);store.Scan();});
    }
    void UserConfig() {
        var d=new OpenFileDialog {Title="Select Documents/WB Games/Batman Arkham City GOTY/BmGame/Config/BmInput.ini",Filter="User input config|BmInput.ini"};if(interaction.PickFile(d,this)!=true)return;
        if(interaction.Message(this,"Allow read-only scanning of INI/CFG files directly in this folder?\n"+Path.GetDirectoryName(d.FileName)+"\nNothing will be edited or uploaded.","External config scan",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        store.SelectUserConfig(Path.GetDirectoryName(d.FileName)!);Scan();
    }
    void Baseline() {
        store.Game();var d=new OpenFileDialog {Title="Select BatmanAC.exe from a SEPARATE known-clean GOTY installation",Filter="Arkham City executable|BatmanAC.exe"};if(interaction.PickFile(d,this)!=true)return;
        var root=Directory.GetParent(d.FileName)?.Parent?.Parent?.FullName??"";
        if(interaction.Message(this,"Confirm this is a separate, unmodified installation of the SAME game edition/build. Hashing may take several minutes. It will only be read; no game files are changed.","Clean baseline",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        Work("Hashing separate clean game and comparing…",()=>{store.CaptureBaseline(root);store.Scan();});
    }
    void ClearDetection() {
        if(interaction.Message(this,"Clear saved clean baseline and ZIP fingerprints? Game files, managed library and backups are not touched.","Reset detection",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        foreach(var name in new[]{"archive-references.json","clean-baseline.json"}) {var path=Path.Combine(CityStore.Home,name);if(File.Exists(path))File.Delete(path);}
        Scan();
    }
    string? Ask(string title,string initial) {
        var dialog=new Window {Owner=this,Title="Overstrike AC — "+title,Width=590,Height=165,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Panel,Foreground=Ink};
        dialog.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("/Overstrike;component/ArkhamCity/CityTheme.xaml",UriKind.Relative)});
        var panel=new StackPanel {Margin=new Thickness(15)};var entry=new TextBox {Text=initial,Margin=new Thickness(4),Padding=new Thickness(5)};panel.Children.Add(entry);
        var actions=new WrapPanel();var ok=Button("OK",()=>dialog.DialogResult=true);ok.IsDefault=true;actions.Children.Add(ok);var cancel=Button("Cancel",()=>dialog.DialogResult=false);cancel.IsCancel=true;actions.Children.Add(cancel);panel.Children.Add(actions);dialog.Content=panel;dialog.Height=195;
        return Modal(dialog)==true?entry.Text:null;
    }
    void Apply() {
        list.CommitEdit();var target=rows.Where(r=>r.Managed&&r.Checked).ToList();var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var r in target)foreach(var p in r.Mod!.Files)if(!paths.Add(p))throw new IOException("Selected ZIPs have conflicting file paths.");
        var changed=rows.Where(r=>r.Managed&&r.Checked!=r.Mod!.Active).ToList();if(changed.Count==0)return;
        if(interaction.Message(this,"Close the game first. Apply "+changed.Count+" changes? Suits with included suitcode are registered automatically. Disabling removes only config entries this manager added.","Apply changes",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        EnsureGameClosed();
        // Batch is not atomic: if an action fails, successful earlier operations remain recorded.
        foreach(var r in changed.Where(r=>r.Checked))EnsureAutoConfig(r.Mod!);
        foreach(var r in changed.Where(r=>!r.Checked))store.Disable(r.Mod!);
        foreach(var r in changed.Where(r=>r.Checked))store.InstallAutomatic(r.Mod!);
        RefreshRows(false);Scan();
    }
    void EnsureGameClosed() {
        if(interaction.GameRunning())throw new IOException("Close Batman Arkham City before installing or registering suits.");
    }
    void RegisterSelected() {
        if(list.SelectedItem is not CityRow row||!row.Managed)throw new IOException("Select the managed ZIP row in Mods, not a ◆ detection.");
        EnsureGameClosed();if(!row.Mod!.Active)throw new IOException("Use Install ZIP, or check this managed row and Apply first.");
        if(!CityStore.Parse(row.Mod.Suitcode,row.Name).Any())throw new IOException("This archive contains no usable suitcode. Automatic registration cannot infer entries from package filenames alone.");
        EnsureAutoConfig(row.Mod);store.InstallAutomatic(row.Mod);RefreshRows(false);Scan();
    }
    void EnsureAutoConfig(CityMod mod) {
        if(!CityStore.Parse(mod.Suitcode,mod.Name).Any()||mod.Registration!=null)return;
        if(store.ResolveAutoSuitConfig()!=null)return;
        if(!ChooseAutoConfig())throw new OperationCanceledException("Setup cancelled. The ZIP remains in the library but was not newly installed.");
    }
    void SetupAutoSuitConfig(){store.Game();ChooseAutoConfig();}
    bool ChooseAutoConfig() {
        var candidates=store.SuitConfigCandidates();
        var dialog=new Window {Owner=this,Title="Overstrike AC — One-time suit config setup",Width=770,Height=470,MinHeight=360,Background=Panel,Foreground=Ink,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        dialog.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("/Overstrike;component/ArkhamCity/CityTheme.xaml",UriKind.Relative)});
        var content=new StackPanel {Margin=new Thickness(22)};dialog.Content=new ScrollViewer {Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        content.Children.Add(new TextBlock {Text="Choose the active suit config once",FontSize=21});
        content.Children.Add(new TextBlock {Text="Several config candidates were found, or none could be selected safely. Choose the file your setup uses for PlayableCharactersV2 entries. Overstrike AC will remember this choice for this game folder and automatically back up, assign IDs and register future suit ZIPs.",TextWrapping=TextWrapping.Wrap,LineHeight=22,Margin=new Thickness(0,12,0,12)});
        var choice=new ComboBox {ItemsSource=candidates,Margin=new Thickness(0,5,0,8)};if(candidates.Count==1)choice.SelectedIndex=0;content.Children.Add(choice);
        content.Children.Add(Button("Browse…",()=>{var d=new OpenFileDialog {InitialDirectory=Path.Combine(store.Game(),"BmGame","Config"),Filter="Config files|*.ini;*.cfg;*.txt"};if(interaction.PickFile(d,dialog)==true){var rel=Path.GetRelativePath(store.Game(),d.FileName);if(!candidates.Contains(rel)){candidates.Add(rel);choice.ItemsSource=null;choice.ItemsSource=candidates;}choice.SelectedItem=rel;}}));
        content.Children.Add(Button("Save and continue",()=>{if(choice.SelectedItem is not string rel){interaction.Message(dialog,"Choose the active config file.");return;}store.SetAutoSuitConfig(rel);dialog.DialogResult=true;}));
        content.Children.Add(Button("Cancel",()=>dialog.DialogResult=false));
        return Modal(dialog)==true;
    }
    void Remove() {
        if(list.SelectedItem is not CityRow r)throw new IOException("Select a managed ZIP in the Mods list first.");
        if(!r.Managed)throw new IOException("Detected registrations cannot be removed: original backups and file mapping are unknown.");
        if(r.Mod!.Active)throw new IOException("Disable the managed mod first.");
        if(interaction.Message(this,"Remove "+r.Name+" from library?","Remove",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        string folder=Path.Combine(CityStore.Home,"library",r.Mod.Id),quarantine=folder+".remove-"+Guid.NewGuid().ToString("N");bool moved=Directory.Exists(folder);
        if(moved)Directory.Move(folder,quarantine);
        int index=store.State.Mods.IndexOf(r.Mod);store.State.Mods.Remove(r.Mod);
        try{store.Save();}catch{store.State.Mods.Insert(index,r.Mod);if(moved)Directory.Move(quarantine,folder);throw;}
        if(moved)try{Directory.Delete(quarantine,true);}catch(IOException){status.Text="Removed from library; unused cache could not be deleted: "+quarantine;}
        RefreshRows(true);
    }
    void Launch() {var p=Path.Combine(store.Game(),"Binaries","Win32","BatmanAC.exe");if(!File.Exists(p))throw new IOException("BatmanAC.exe not found.");interaction.Start(new ProcessStartInfo(p) {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(p)});}
    void RenderSuits() {
        cards.Children.Clear();var character=filter.SelectedItem as string??"ALL";
        foreach(var s in store.AllSuits().Where(s=>s.Name.Contains(suitSearch.Text,StringComparison.OrdinalIgnoreCase))) {
            var type=new[]{"BATMAN","ROBIN","NIGHTWING","CATWOMAN"}.FirstOrDefault(c=>s.Name.StartsWith(c,StringComparison.OrdinalIgnoreCase))??"OTHER";if(character!="ALL"&&character!=type)continue;
            var panel=new StackPanel();string key=SuitKey(s);
            if(portraits.TryGetValue(key,out var p)&&File.Exists(p)) {try{panel.Children.Add(new Image {Source=new BitmapImage(new Uri(p)),Height=150,Stretch=Stretch.UniformToFill});}catch{panel.Children.Add(Text("NO PORTRAIT",12));}}
            else panel.Children.Add(new TextBlock {Text="◇\nNO PORTRAIT",Height=150,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Foreground=Color("#8D7478"),Padding=new Thickness(4,45,4,0)});
            panel.Children.Add(new TextBlock {Text=s.Name.Replace('_',' '),TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,Foreground=Ink,Margin=new Thickness(3)});
            var card=Button("",()=>SelectSuit(s));AutomationProperties.SetName(card,s.Name);AutomationProperties.SetAutomationId(card,"suit-"+s.Name);card.Content=panel;card.Width=150;card.Height=220;card.BorderBrush=Accent;cards.Children.Add(card);
        }
    }
    void SelectSuit(CitySuit s) {selectedSuit=s;details.Text=s.Name.Replace('_',' ').ToUpperInvariant()+"\n\nID: "+s.Id+"\nBaseId: "+s.BaseId+"\n\n"+s.Status+"\n\nSource:\n"+s.Source+"\n\nA config registration does not verify that its packages are present.";}
    static string SuitKey(CitySuit s)=>s.Name+"|"+s.Source;
    void LoadPortraits() {
        var p=Path.Combine(CityStore.Home,"native-portraits.json");if(!File.Exists(p))return;
        try {foreach(var kv in System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(p))??new())portraits[kv.Key]=kv.Value;}
        catch(Exception e) when(e is IOException||e is System.Text.Json.JsonException) {startupWarning="Portrait index could not be read; its file was kept unchanged.";}
    }
    void Portrait() {
        if(selectedSuit==null)throw new IOException("Select a suit first.");
        var d=new OpenFileDialog {Filter="Portrait images|*.png;*.jpg;*.jpeg;*.gif"};if(interaction.PickFile(d,this)!=true)return;
        using(var stream=File.OpenRead(d.FileName)) {BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);}
        var folder=Path.Combine(CityStore.Home,"portraits");Directory.CreateDirectory(folder);var target=Path.Combine(folder,Guid.NewGuid().ToString("N")+Path.GetExtension(d.FileName));File.Copy(d.FileName,target);
        portraits[SuitKey(selectedSuit)]=target;var db=Path.Combine(CityStore.Home,"native-portraits.json");File.WriteAllText(db+".tmp",System.Text.Json.JsonSerializer.Serialize(portraits));File.Move(db+".tmp",db,true);RenderSuits();
    }
    void Report()=>ShowText("Read-only scan report","WARNINGS\n"+string.Join("\n",store.ScanWarnings)+"\n\nREGISTERED SUITS\n"+string.Join("\n",store.Suits.Select(s=>s.Name+" — "+s.Source))+"\n\nFILE DISCOVERY\n"+string.Join("\n\n",store.Findings.Select(f=>f.Category+" | "+f.Name+" | "+f.Status+"\n"+f.Source+"\n"+f.Evidence)));
    void Suitcode() {if(list.SelectedItem is not CityRow r||!r.Managed)throw new IOException("Select an imported ZIP in Mods first.");ShowText("Included suitcode",r.Mod!.Suitcode+"\n\nInstall ZIP handles registration automatically. Settings can choose the active suit config once if discovery is ambiguous. Repair suits… retries registration for an older enabled ZIP.");}
    bool? Modal(Window dialog) {
        dialog.Loaded+=(_,_)=>dialog.Dispatcher.BeginInvoke(new Action(()=>interaction.DialogOpened(dialog)));
        return dialog.ShowDialog();
    }
    void ShowText(string title,string value) {
        var window=new Window {Owner=this,Title="Overstrike AC — "+title,Width=850,Height=550,Content=new TextBox {Text=value,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=Bg,Foreground=Ink,Padding=new Thickness(15)}};
        window.Loaded+=(_,_)=>window.Dispatcher.BeginInvoke(new Action(()=>interaction.DialogOpened(window)));window.Show();
    }
}

// OS interaction boundary for Windows-side WPF handler tests. GPL-3.0.
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;
namespace Overstrike.ArkhamCity;
public class CityUi {
    public virtual bool? PickFile(OpenFileDialog picker,Window owner)=>picker.ShowDialog(owner);
    public virtual MessageBoxResult Message(Window owner,string text,string caption="Overstrike AC",MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage image=MessageBoxImage.None,MessageBoxResult defaultResult=MessageBoxResult.None)=>MessageBox.Show(owner,text,caption,buttons,image,defaultResult);
    public virtual void Start(ProcessStartInfo start)=>Process.Start(start);
    // Tests drive real custom WPF dialogs after Loaded; production has no override.
    public virtual void DialogOpened(Window window) { }
    public virtual void ButtonInvoked(string automationId,string label) { }
    public virtual bool GameRunning() {
        var processes=Process.GetProcessesByName("BatmanAC");bool running=processes.Length>0;
        foreach(var process in processes)process.Dispose();return running;
    }
}

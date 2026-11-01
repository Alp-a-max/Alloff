using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace Nashi.Windows;
public sealed class ToastWindow:Window
{
    private readonly TextBlock label=new(){FontSize=16,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock detail=new(){Margin=new(0,4,0,8),TextWrapping=TextWrapping.Wrap};
    private readonly ProgressBar bar=new(){Height=5,Minimum=0,Maximum=1,IsIndeterminate=true};
    private readonly Button action=new(){Content="Cancel",MinWidth=85};private readonly Action? cancel;private bool complete;
    public ToastWindow(Window owner,Action? cancel)
    {
        Theme.Attach(this);bar.SetResourceReference(Control.ForegroundProperty,"Gold");bar.SetResourceReference(Control.BackgroundProperty,"Inset");this.cancel=cancel;L10n.Content(action);Owner=owner;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;SizeToContent=SizeToContent.Height;Width=510;
        var content=new StackPanel();content.Children.Add(label);content.Children.Add(detail);content.Children.Add(bar);
        var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});grid.Children.Add(content);Grid.SetColumn(action,1);grid.Children.Add(action);action.Margin=new(18,0,0,0);action.VerticalAlignment=VerticalAlignment.Center;
        Content=new Border{CornerRadius=new(22),Background=Theme.Brush("Surface"),BorderBrush=Theme.Brush("Line"),BorderThickness=new(1),Padding=new(24),Child=grid};
        Loaded+=(_,_)=>{Left=Math.Max(8,owner.Left+(owner.ActualWidth-Width)/2);Top=Math.Max(8,owner.Top+owner.ActualHeight-ActualHeight-30);};
        action.Click+=(_,_)=>{if(!complete){this.cancel?.Invoke();action.IsEnabled=false;}};Update(new(null,"A fresh file is on its way…"));
    }
    public void Update(Nashi.Core.OperationProgress progress){L10n.SetText(label,progress.Label);bar.IsIndeterminate=progress.Fraction is null;if(progress.Fraction is {} fraction)bar.Value=Math.Clamp(fraction,0,1);L10n.SetText(detail,progress.Fraction is {} value?$"{value:P0}":"Working…");}
    public void Complete(IReadOnlyList<string> outputs,Action? reuse=null)
    {
        if(!App.IsSmokeTest&&Owner is MainWindow main)main.TriggerPillMediaPeek();
        complete=true;L10n.SetText(label,outputs.Count==0?"All set. You’re ready to go.":$"Nicely done. {outputs.Count} {(outputs.Count==1?"file is":"files are")} ready.");detail.Text=outputs.Count>0?Path.GetFileName(outputs[0]):"Ready.";bar.Visibility=Visibility.Collapsed;L10n.Bind(action,ContentControl.ContentProperty,"Show in Explorer");action.IsEnabled=true;
        // Replace the cancel handler by disabling its callback after completion.
        action.Click+=(_,_)=>{if(outputs.Count>0&&(File.Exists(outputs[0])||Directory.Exists(outputs[0])))MainWindow.Reveal(outputs[0]);};
        if(Content is Border {Child:Grid grid})
        {
            var commands=new StackPanel{Margin=new(16,4,0,0)};Grid.SetColumn(commands,1);grid.Children.Remove(action);commands.Children.Add(action);
            if(outputs.Count>0){var open=MainWindow.Button(L10n.Language=="tr"?"Aç":"Open",()=>{if(File.Exists(outputs[0])||Directory.Exists(outputs[0]))System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(outputs[0]){UseShellExecute=true})?.Dispose();});commands.Children.Add(open);}
            if(reuse is not null)commands.Children.Add(MainWindow.Button(L10n.Language=="tr"?"Yeni işlemde kullan":"Use in another tool",()=>{reuse();Close();}));
            commands.Children.Add(MainWindow.Button("Close",Close));grid.Children.Add(commands);
        }
        // Keep completion actions available until dismissed; a three-second toast was easy to miss.
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using Microsoft.Win32;
using Nashi.Core;
namespace Nashi.Windows;

public sealed partial class SettingsWindow:Window
{
    private readonly LibraryStore? appearanceStore;
    private readonly PreferencesStore store;private readonly Action changed;
    private readonly StackPanel page=new(){Margin=new(28)};
    private readonly ListBox sections=new(){Width=175,ItemsSource=new[]{"Appearance","Shortcuts","Files","Behavior","Formats","Compatibility","Updates","About"},SelectedIndex=0};
    public SettingsWindow(Window owner,PreferencesStore store,Action changed,Func<Task> update,LibraryStore? appearanceStore=null)
    {
        this.appearanceStore=appearanceStore;L10n.Choices(sections);this.store=store;this.changed=changed;Owner=owner;Theme.Attach(this);Title="Make it yours · Nashi";Width=850;Height=680;MinWidth=740;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel();Content=root;DockPanel.SetDock(sections,Dock.Left);root.Children.Add(sections);root.Children.Add(new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});sections.SelectionChanged+=(_,_)=>ShowSection(update);ShowSection(update);PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape){Close();e.Handled=true;}};
    }
    private void Save(Preferences value){try{store.Save(value);changed();}catch(Exception error)when(error is IOException or ArgumentException or UnauthorizedAccessException){MessageBox.Show(this,L10n.Text(error.Message),L10n.Text("Settings could not be saved"));}}
    private void Heading(string title,string hint){page.Children.Add(L10n.Label(new TextBlock{Text=title,FontSize=28,FontWeight=FontWeights.Bold}));page.Children.Add(L10n.Label(new TextBlock{Text=hint,TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,20),Foreground=Theme.Brush("Muted")}));}
    private void Toggle(string title,bool value,Action<bool> save){var box=L10n.Content(new CheckBox{Content=title,IsChecked=value,Margin=new(4,12,4,12)});box.Checked+=(_,_)=>save(true);box.Unchecked+=(_,_)=>save(false);page.Children.Add(box);}
    private void Pick(string title,string[] values,string value,Action<string> save){page.Children.Add(L10n.Label(new TextBlock{Text=title,Margin=new(4,12,4,0)}));var combo=new ComboBox{ItemsSource=values,SelectedItem=value};combo.SelectionChanged+=(_,_)=>{if(combo.SelectedItem is string selected)save(selected);};page.Children.Add(combo);}
    private static string PillText(string english,string turkish)=>L10n.Language=="tr"?turkish:english;
    private Action DebounceSave(Action save)
    {
        var pending=false;var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};
        void Flush(){timer.Stop();if(!pending)return;pending=false;save();}
        timer.Tick+=(_,_)=>Flush();Closed+=(_,_)=>Flush();return ()=>{pending=true;timer.Stop();timer.Start();};
    }
    private void PillNumber(string title,double min,double max,double value,double step,Func<double,string> label,Action<double> save)
    {
        page.Children.Add(new TextBlock{Text=title,Margin=new(4,12,4,0)});var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=step,IsSnapToTickEnabled=true,Margin=new(4,4,4,4)};
        var caption=new TextBlock{Text=label(value),Foreground=Theme.Brush("Muted"),Margin=new(4,0,4,4)};var commit=DebounceSave(()=>save(slider.Value));slider.ValueChanged+=(_,_)=>{caption.Text=label(slider.Value);commit();};page.Children.Add(slider);page.Children.Add(caption);
    }
    private void PillVisibilitySettings()
    {
        var mode=store.Value.PillVisibilityMode=="always"?"fixed":store.Value.PillVisibilityMode;
        AddChoice(PillText("Pill visibility","Hap görünürlüğü"),new[]{("shortcut",PillText("Shortcut only","Yalnızca kısayol")),("fixed",PillText("Fixed","Sabit")),("smart",PillText("Smart","Akıllı")),("peek",PillText("Peek","Kenar"))},mode,v=>Save(store.Value with{PillVisibilityMode=v}));
        AddChoice(PillText("Pill position","Hap konumu"),new[]{("left",PillText("Left","Sol")),("center",PillText("Center","Orta")),("right",PillText("Right","Sağ"))},store.Value.PillPosition,v=>Save(store.Value with{PillPosition=v}));
        AddChoice(PillText("Show on","Gösterileceği ekran"),new[]{("primary",PillText("Primary monitor","Birincil ekran")),("specific",PillText("Selected monitor","Seçilen ekran")),("all",PillText("All monitors","Tüm ekranlar"))},store.Value.PillMonitorMode,v=>Save(store.Value with{PillMonitorMode=v}));
        var displays=PillDisplays.Enumerate();if(displays.Count>0)
        {
            var selected=string.IsNullOrWhiteSpace(store.Value.PillMonitorId)?(displays.FirstOrDefault(d=>d.Primary)??displays[0]).Id:store.Value.PillMonitorId;
            var choices=displays.Select((d,i)=>(d.Id,$"{PillText("Monitor","Ekran")} {i+1} · {d.Id}{(d.Primary?PillText(" (primary)"," (birincil)"):"")}")).ToList();
            if(!choices.Any(choice=>choice.Id==selected))choices.Add((selected,selected+PillText(" (disconnected)"," (bağlı değil)")));
            AddChoice(PillText("Selected monitor","Seçilen ekran"),choices.Select(choice=>(choice.Id,choice.Item2)).ToArray(),selected,v=>Save(store.Value with{PillMonitorId=v}));
        }
        PillNumber(PillText("Hover delay","Üzerine gelme gecikmesi"),80,1000,store.Value.PillHoverDelayMs,20,v=>$"{v:0} ms",v=>Save(store.Value with{PillHoverDelayMs=(int)v}));
        PillNumber(PillText("Close delay","Kapanma gecikmesi"),250,1500,store.Value.PillCollapseDelayMs,50,v=>$"{v:0} ms",v=>Save(store.Value with{PillCollapseDelayMs=(int)v}));
        PillNumber(PillText("Spring stiffness","Yay sertliği"),100,600,store.Value.PillSpringStiffness,25,v=>$"{v:0}",v=>Save(store.Value with{PillSpringStiffness=v}));
    }
    private void LanguagePicker()
    {
        page.Children.Add(L10n.Label(new TextBlock{Text="Language",Margin=new(4,12,4,0)}));var language=new ComboBox{ItemsSource=new[]{"English","Türkçe"},SelectedIndex=store.Value.Language=="tr"?1:0};
        System.Windows.Automation.AutomationProperties.SetAutomationId(language,"AppLanguage");language.SelectionChanged+=(_,_)=>Save(store.Value with{Language=language.SelectedIndex==1?"tr":"en"});page.Children.Add(language);
    }
    private void PillModeToggle()
    {
        var enabled=new CheckBox{Content=PillText("Enable compact pill mode","Kompakt hap modunu etkinleştir"),IsChecked=store.Value.PillModeEnabled,Margin=new Thickness(4,12,4,12)};
        enabled.Checked+=(_,_)=>Save(store.Value with{PillModeEnabled=true});enabled.Unchecked+=(_,_)=>Save(store.Value with{PillModeEnabled=false});page.Children.Add(enabled);
        page.Children.Add(new TextBlock{Text=PillText("The pill keeps its data on this PC.","Hap verileri bu bilgisayarda kalır."),Foreground=Theme.Brush("Muted"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,0,4,8)});
        page.Children.Add(new TextBlock{Text=PillText("Close Settings to apply a change to pill mode.","Hap modu değişikliğini uygulamak için Ayarlar'ı kapat."),Foreground=Theme.Brush("Muted"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,0,4,8)});
    }
    private void AddChoice(string title,(string Value,string Label)[] choices,string selected,Action<string> save)
    {
        page.Children.Add(L10n.Label(new TextBlock{Text=title,Margin=new Thickness(4,12,4,0)}));var picker=new ComboBox{Margin=new Thickness(4,5,4,4)};
        foreach(var (value,label) in choices)picker.Items.Add(new ComboBoxItem{Content=label,Tag=value});
        picker.SelectedItem=picker.Items.OfType<ComboBoxItem>().First(item=>(string)item.Tag==selected);
        picker.SelectionChanged+=(_,_)=>{if(picker.SelectedItem is ComboBoxItem item&&item.Tag is string value)save(value);};page.Children.Add(picker);
    }
    private void DockOrderSetting()
    {
        page.Children.Add(L10n.Label(new TextBlock{Text=PillText("Module order","Modül sırası"),Margin=new Thickness(4,16,4,4),FontWeight=FontWeights.SemiBold}));
        var rows=new StackPanel{Margin=new Thickness(4,2,4,8)};
        string Label(string key)=>key switch{"now"=>PillText("Files","Dosyalar"),"tools"=>PillText("Tools","Araçlar"),"download"=>PillText("Download","İndir"),"media"=>PillText("Media","Medya"),"history"=>PillText("History","Geçmiş"),"search"=>PillText("Search","Ara"),_=>PillText("Settings","Ayarlar")};
        void Render()
        {
            rows.Children.Clear();var order=store.Value.PillDockOrder;
            for(var index=0;index<order.Length;index++)
            {
                var current=index;var row=new DockPanel{Margin=new Thickness(2,2,2,2)};var move=new StackPanel{Orientation=Orientation.Horizontal};var up=MainWindow.Button("↑",()=>Move(current,-1));up.Width=32;up.Height=30;up.Padding=new Thickness(0);up.Margin=new Thickness(2);up.IsEnabled=current>0;up.ToolTip=PillText("Move up","Yukarı taşı");var down=MainWindow.Button("↓",()=>Move(current,1));down.Width=32;down.Height=30;down.Padding=new Thickness(0);down.Margin=new Thickness(2);down.IsEnabled=current<order.Length-1;down.ToolTip=PillText("Move down","Aşağı taşı");move.Children.Add(up);move.Children.Add(down);DockPanel.SetDock(move,Dock.Right);row.Children.Add(move);var label=new TextBlock{Text=Label(order[current]),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,4,0)};row.Children.Add(label);var surface=new Border{Child=row,CornerRadius=new CornerRadius(12),Padding=new Thickness(5),Margin=new Thickness(0,1,0,1),BorderThickness=new Thickness(1)};surface.SetResourceReference(Border.BackgroundProperty,"GlassCard");surface.SetResourceReference(Border.BorderBrushProperty,"GlassEdge");rows.Children.Add(surface);
            }
        }
        void Move(int index,int delta)
        {
            var order=store.Value.PillDockOrder.ToList();var target=index+delta;if(target<0||target>=order.Count)return;(order[index],order[target])=(order[target],order[index]);Save(store.Value with{PillDockOrder=order.ToArray()});Render();
        }
        Render();page.Children.Add(rows);
    }
    private void ShowSection(Func<Task> update)
    {
        page.Children.Clear();switch(sections.SelectedItem as string)
        {
            case "Appearance":
                Heading("Make it yours.","Choose a look and see changes on the pill right away.");LanguagePicker();
                page.Children.Add(new TextBlock{Text=PillText("Monochrome glass appearance","Siyah beyaz cam görünüm"),Foreground=Theme.Brush("Muted"),Margin=new Thickness(4,14,4,4)});
                page.Children.Add(L10n.Label(new TextBlock{Text="Pill size",Margin=new Thickness(4,15,4,3)}));var pillSize=new Slider{Minimum=.85,Maximum=1.2,Value=store.Value.PillScale,TickFrequency=.05,IsSnapToTickEnabled=true,Margin=new Thickness(4,4,4,4)};var sizeValue=new TextBlock{Text=$"{Math.Round(pillSize.Value*100)}%",Foreground=Theme.Brush("Muted"),Margin=new Thickness(4,0,4,5)};var savePillSize=DebounceSave(()=>Save(store.Value with{PillScale=pillSize.Value}));pillSize.ValueChanged+=(_,_)=>{sizeValue.Text=$"{Math.Round(pillSize.Value*100)}%";savePillSize();};page.Children.Add(pillSize);page.Children.Add(sizeValue);break;
            case "Shortcuts":
                Heading("Shortcuts.","Choose how you open the pill and trigger file actions.");AddChoice(PillText("Show / hide pill","Hap görünürlüğü kısayolu"),new[]{("Ctrl+Alt+N","Ctrl+Alt+N"),("Ctrl+Shift+N","Ctrl+Shift+N"),("Ctrl+Alt+Space","Ctrl+Alt+Space")},store.Value.PillHotkey,v=>Save(store.Value with{PillHotkey=v}));
                var modifiers=new[]{"Ctrl","Shift","Alt","Ctrl+Shift","Ctrl+Alt","Shift+Alt"};Pick("Tools · while dragging",modifiers.Where(m=>m!=store.Value.ConvertModifier&&m!=store.Value.ActionsModifier).ToArray(),store.Value.ToolsModifier,v=>Save(store.Value with{ToolsModifier=v}));Pick("Convert · while dragging",modifiers.Where(m=>m!=store.Value.ToolsModifier&&m!=store.Value.ActionsModifier).ToArray(),store.Value.ConvertModifier,v=>Save(store.Value with{ConvertModifier=v}));Pick("File actions · while dragging",modifiers.Where(m=>m!=store.Value.ToolsModifier&&m!=store.Value.ConvertModifier).ToArray(),store.Value.ActionsModifier,v=>Save(store.Value with{ActionsModifier=v}));break;
            case "Files":
                Heading("Your files, your way.","Original files stay untouched. Set where new copies are saved.");Pick("Save new copies",["beside","ask","folder"],store.Value.OutputMode,v=>Save(store.Value with{OutputMode=v}));
                page.Children.Add(MainWindow.Button("Choose output folder…",()=>{var picker=new OpenFolderDialog{Title="Save new copies here"};if(picker.ShowDialog(this)==true){Save(store.Value with{OutputFolder=picker.FolderName,OutputMode="folder"});ShowSection(update);}}));page.Children.Add(L10n.Label(new TextBlock{Text=store.Value.OutputFolder,TextWrapping=TextWrapping.Wrap}));
                page.Children.Add(MainWindow.Button("Choose downloads folder…",()=>{var picker=new OpenFolderDialog{Title="Save downloaded media here"};if(picker.ShowDialog(this)==true){Save(store.Value with{DownloadFolder=picker.FolderName});ShowSection(update);}}));page.Children.Add(L10n.Label(new TextBlock{Text=store.Value.DownloadFolder,TextWrapping=TextWrapping.Wrap}));Toggle("Reveal finished files in Explorer",store.Value.RevealFinished,v=>Save(store.Value with{RevealFinished=v}));break;
            case "Behavior":
                Heading("How Nashi behaves.","Tune startup, visibility and small quality-of-life details.");PillModeToggle();AddChoice("When Nashi opens",new[]{("shortcut",PillText("Start hidden · use shortcut","Gizli başlat · kısayolla aç")),("expanded",PillText("Open the pill expanded","Hap genişletilmiş açılsın"))},store.Value.PillStartupBehavior,v=>Save(store.Value with{PillStartupBehavior=v}));PillVisibilitySettings();DockOrderSetting();
                Toggle("Enable Nashi drag shortcuts",store.Value.Enabled,v=>Save(store.Value with{Enabled=v}));if(appearanceStore is not null)Toggle("Window management · drag a title bar, then Shift or Alt",appearanceStore.WindowManagementEnabled,v=>{appearanceStore.SetWindowManagement(v);changed();});Toggle("Sound feedback",store.Value.Feedback,v=>Save(store.Value with{Feedback=v}));page.Children.Add(MainWindow.Button("Try feedback",Feedback.Play));Toggle("Subtle animations",store.Value.Animations,v=>Save(store.Value with{Animations=v}));Toggle("Keep running in the system tray",store.Value.KeepInTray,v=>Save(store.Value with{KeepInTray=v}));Pick("Wheel finish",["glass","solid"],store.Value.WheelFinish,v=>Save(store.Value with{WheelFinish=v}));var wheelSize=new Slider{Minimum=.85,Maximum=1.4,Value=store.Value.WheelScale,Margin=new(4,12,4,8),ToolTip="Wheel size"};var saveWheelSize=DebounceSave(()=>Save(store.Value with{WheelScale=wheelSize.Value}));wheelSize.ValueChanged+=(_,_)=>saveWheelSize();page.Children.Add(L10n.Label(new TextBlock{Text="Wheel size"}));page.Children.Add(wheelSize);Pick("Float behavior",["createNew","addToExisting"],store.Value.FloatBehavior,v=>Save(store.Value with{FloatBehavior=v}));
                using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){var enabled=key?.GetValue("Nashi") is string;Toggle("Launch at login",enabled,v=>{try{using var run=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(v)run.SetValue("Nashi","\""+Environment.ProcessPath+"\" --background");else run.DeleteValue("Nashi",false);}catch(Exception error){MessageBox.Show(this,L10n.Text(error.Message),L10n.Text("Launch at login"));}});}break;
            case "Formats":FormatDefaults();break;
            case "Compatibility":Heading("Everything has a next form.","The same conversion and tool catalog as macOS.");foreach(var pair in FeatureCatalog.Conversions.OrderBy(p=>p.Key))page.Children.Add(new TextBlock{Text=pair.Key.ToUpperInvariant()+"  →  "+string.Join(", ",pair.Value.Select(v=>v.ToUpperInvariant())),TextWrapping=TextWrapping.Wrap,Margin=new(4,8,4,8)});break;
            case "Updates":Heading("Fresh things, on their way.","Updates are downloaded from the public Nashi repository and verified before installation. No GitHub account or token is required.");Toggle("Check for updates automatically",store.Value.AutomaticUpdateChecks,v=>Save(store.Value with{AutomaticUpdateChecks=v}));page.Children.Add(MainWindow.Button("Check for updates",async()=>await update()));break;
            default:Heading("Nashi","A little pear. A lot of possibility.");var build=System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(App).Assembly)?.InformationalVersion??UpdateInstaller.Current.ToString();page.Children.Add(new TextBlock{Text=L10n.Text("Version")+" "+build,Margin=new(4,12,4,8),TextWrapping=TextWrapping.Wrap});page.Children.Add(new TextBlock{Text=L10n.Text("Running copy")+"\n"+Environment.ProcessPath,Margin=new(4,0,4,12),TextWrapping=TextWrapping.Wrap,Foreground=Theme.Brush("Muted")});page.Children.Add(MainWindow.Button("Source & third-party notices",()=>MainWindow.OpenUrl("https://github.com/Alp-a-max/Alloff")));break;
        }
    }
}

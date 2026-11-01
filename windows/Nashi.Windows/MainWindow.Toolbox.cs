using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Nashi.Core;

namespace Nashi.Windows;

public sealed partial class MainWindow
{
    private readonly List<Button> islandToolboxButtons=[];
    private TextBox? islandToolboxSearch;
    private string islandToolboxCategory="All";
    private static readonly string[] ToolboxCategories=["All","Images","Video","Audio","PDF","Files"];
    private static string ToolboxCategory(string id)=>id switch
    {
        "editImage" or "annotateImage" or "frameImage" or "cropImage" or "createCollage" or "redactImage" or "readQRCode"=>"Images",
        "muteVideo" or "trimVideo" or "cropVideo" or "changeVideoSpeed" or "joinVideos" or "videoSnapshots" or "splitVideo" or "redactVideo"=>"Video",
        "normalizeAudio" or "audioToVideo" or "trimAudio" or "audioChannels" or "redactAudio"=>"Audio",
        "splitPDF" or "organizePDF" or "createPDF" or "mergePDF"=>"PDF",
        _=>"Files"
    };
    private UIElement BuildIslandToolbox()
    {
        islandToolboxButtons.Clear();islandToolboxCategory="All";
        var page=new StackPanel();var title=Text("Tools",21);title.FontWeight=FontWeights.SemiBold;title.Margin=new(2,0,2,10);page.Children.Add(title);
        var quick=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(0,0,0,9)};
        var choose=Button("+  "+L10n.Text("Choose files"),Choose);choose.Padding=new(11,6,11,6);choose.Margin=new(2,0,6,0);quick.Children.Add(choose);
        var convert=Button("↗  "+L10n.Text("Convert"),async()=>{if(files.Count==0)Choose();if(files.Count>0)await EditAsync(new("convert","Convert","↗"));});convert.Padding=new(11,6,11,6);convert.Margin=new(0);quick.Children.Add(convert);
        var extract=Button(IslandText("Extract audio from a video · MP3","Videodan ses çıkar · MP3"),async()=>await ExtractAudioFromVideoAsync());extract.Padding=new(10,6,10,6);extract.Margin=new(6,0,0,0);AutomationProperties.SetAutomationId(extract,"Toolbox.Intent.ExtractAudio");quick.Children.Add(extract);
        quick.Children.Add(new TextBlock{Text=files.Count==0?L10n.Text("Pick a tool to choose compatible files."):string.Format(L10n.Text("{0} files selected"),files.Count),FontSize=11,Foreground=Theme.Brush("Muted"),VerticalAlignment=VerticalAlignment.Center,Margin=new(12,0,0,0)});page.Children.Add(quick);
        islandToolboxSearch=new TextBox{Padding=new(10,7,10,7),Margin=new(2,0,2,9),MinHeight=34,ToolTip=L10n.Text("Search tools, e.g. PDF, crop, audio")};
        AutomationProperties.SetName(islandToolboxSearch,L10n.Text("Search tools"));AutomationProperties.SetAutomationId(islandToolboxSearch,"Toolbox.Search");
        var searchSurface=new Grid{Margin=new(2,0,2,9)};islandToolboxSearch.Margin=new(0);searchSurface.Children.Add(islandToolboxSearch);
        var searchHint=Text("Search tools, e.g. PDF, crop, audio",12,"Muted");searchHint.Margin=new(11,0,8,0);searchHint.VerticalAlignment=VerticalAlignment.Center;searchHint.IsHitTestVisible=false;searchSurface.Children.Add(searchHint);page.Children.Add(searchSurface);
        var categories=new WrapPanel{Margin=new(0,0,0,8)};var categoryButtons=new Dictionary<string,Button>();
        var grid=new UniformGrid{Columns=3};var empty=Text("No matching tools.",13,"Muted");empty.Margin=new(5,14,5,14);empty.Visibility=Visibility.Collapsed;
        var extractIntent=Button(IslandText("Extract audio from video · Save as MP3","Videodan ses çıkar · MP3 olarak kaydet"),async()=>await ExtractAudioFromVideoAsync());extractIntent.Padding=new(12,8,12,8);extractIntent.Margin=new(3,0,3,8);extractIntent.HorizontalContentAlignment=HorizontalAlignment.Left;extractIntent.Visibility=Visibility.Collapsed;AutomationProperties.SetAutomationId(extractIntent,"Toolbox.Intent.ExtractAudioSearch");
        void Filter()
        {
            var query=islandToolboxSearch.Text.Trim();var count=0;
            var extractAudio=ToolDescriptions.IsExtractAudioIntent(query);extractIntent.Visibility=extractAudio?Visibility.Visible:Visibility.Collapsed;
            foreach(var button in islandToolboxButtons)
            {
                var choice=(ToolChoice)button.Tag;var id=button.Uid;var group=ToolboxCategory(id);
                var show=(islandToolboxCategory=="All"||group==islandToolboxCategory)&&ToolDescriptions.Matches(id,choice.Title+" "+group+" "+L10n.Text(group),query);
                button.Visibility=show?Visibility.Visible:Visibility.Collapsed;if(show)count++;
            }
            empty.Visibility=count==0&&!extractAudio?Visibility.Visible:Visibility.Collapsed;searchHint.Visibility=query.Length==0?Visibility.Visible:Visibility.Collapsed;
            foreach(var (category,button) in categoryButtons){button.Background=category==islandToolboxCategory?Theme.Brush("Gold"):Theme.Brush("Surface");button.Foreground=category==islandToolboxCategory?Brushes.Black:Theme.Brush("Ink");}
        }
        foreach(var category in ToolboxCategories)
        {
            var button=Button(category,()=>{islandToolboxCategory=category;Filter();});button.Padding=new(10,5,10,5);button.Margin=new(2,0,4,0);button.FontSize=11;AutomationProperties.SetAutomationId(button,"Toolbox.Category."+category);categoryButtons[category]=button;categories.Children.Add(button);
        }
        page.Children.Add(categories);
        foreach(var (id,titleText) in FeatureCatalog.ToolNames.OrderBy(pair=>Array.IndexOf(ToolboxCategories,ToolboxCategory(pair.Key))).ThenBy(pair=>L10n.Text(pair.Value),StringComparer.CurrentCultureIgnoreCase))
        {
            var choice=new ToolChoice(id switch{"cropImage"=>"crop","annotateImage"=>"annotate","mergePDF"=>"mergePDFs",_=>id},titleText,ToolSymbol(id));
            var row=new DockPanel{LastChildFill=true};var glyph=new TextBlock{Text=ToolSymbol(id),Width=27,FontFamily=new FontFamily("Segoe UI Symbol"),FontSize=17,Foreground=Theme.Brush("Gold"),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(glyph,Dock.Left);row.Children.Add(glyph);
            var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center};labels.Children.Add(new TextBlock{Text=L10n.Text(titleText),FontSize=12,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Foreground=Theme.Brush("Ink")});var description=ToolDescriptions.For(id);labels.Children.Add(new TextBlock{Text=L10n.Language=="tr"?description.Tr:description.En,FontSize=10,TextWrapping=TextWrapping.Wrap,Foreground=Theme.Brush("Muted"),Margin=new(0,3,0,0)});row.Children.Add(labels);
            var button=new Button{Content=row,Uid=id,Tag=choice,MinHeight=76,Margin=new(3),Padding=new(10,9,10,9),HorizontalContentAlignment=HorizontalAlignment.Stretch,Background=Theme.Brush("Surface"),BorderBrush=Theme.Brush("Line"),BorderThickness=new(1),Cursor=Cursors.Hand,ToolTip=L10n.Text(titleText)};
            AutomationProperties.SetName(button,L10n.Text(titleText));AutomationProperties.SetAutomationId(button,"Toolbox.Tool."+id);
            button.Click+=async(_,_)=>{if(busy)return;if(files.Count==0||!Choices().Any(item=>item.Id==choice.Id)){await ChooseForTool(choice);return;}await EditAsync(choice);};
            islandToolboxButtons.Add(button);grid.Children.Add(button);
        }
        islandToolboxSearch.TextChanged+=(_,_)=>Filter();page.Children.Add(extractIntent);page.Children.Add(grid);page.Children.Add(empty);Filter();return page;
    }
    private async Task ExtractAudioFromVideoAsync()
    {
        if(busy)return;
        if(!Video)
        {
            var picker=new NashiFilePickerWindow(this,fileExtensions:FeatureCatalog.InputsForTool("muteVideo"),allowMultiple:false);
            if(picker.ShowDialog()!=true)return;
            files.Clear();Add(picker.SelectedPaths);
        }
        if(Video)await EditAsync(new("convert:mp3","Extract audio · MP3","♫"));
        else L10n.SetText(status,IslandText("Choose one video to extract its audio.","Sesini çıkarmak için bir video seç."));
    }
}

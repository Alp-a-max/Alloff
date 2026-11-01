#if DEBUG
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Nashi.Core;

namespace Nashi.Windows;

// Explicit native-input audit: select synthetic files, click the real tool, edit, Save,
// and inspect the resulting files. No fake export callbacks or real user documents.
internal static class ToolExportChecks
{
    internal static int Run(string root)
    {
        root=Path.GetFullPath(root);if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any())throw new IOException("Use an empty audit folder.");Directory.CreateDirectory(root);
        var data=Path.Combine(root,"data");var destination=Path.Combine(root,"exports");Directory.CreateDirectory(destination);
        new PreferencesStore(data).Save(new Preferences{Language="en",PillModeEnabled=true,PillVisibilityMode="shortcut",PillOnboardingCompleted=true,AutomaticUpdateChecks=false,KeepInTray=false,Feedback=false,Animations=false,OutputMode="folder",OutputFolder=destination,RevealFinished=false});
        var library=new LibraryStore(data);var runtime=new Toolchain(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nashi","Tools","v1"),Environment.GetEnvironmentVariable("PATH"));
        var engine=new MediaEngine(runtime.Executable("ffmpeg"),runtime.Executable("ffprobe"));
        var qrData=new ZXing.BarcodeWriterPixelData{Format=ZXing.BarcodeFormat.QR_CODE,Options=new ZXing.Common.EncodingOptions{Width=320,Height=320,Margin=4}}.Write("Nashi synthetic audit");
        var bitmap=BitmapSource.Create(qrData.Width,qrData.Height,96,96,PixelFormats.Bgra32,null,qrData.Pixels,qrData.Width*4);bitmap.Freeze();
        var picture=Path.Combine(root,"QR.png");ImageEngine.Save(bitmap,picture,"png");var picture2=Path.Combine(root,"Other.png");ImageEngine.Save(bitmap,picture2,"png");
        var pdf=ImageEngine.Apply("createPDF",[picture,picture2],root,new()).Single();
        var audio=Path.Combine(root,"Tone.wav");ProcessRunner.RunAsync(runtime.Executable("ffmpeg"),["-v","error","-f","lavfi","-i","sine=frequency=660:duration=3","-metadata","title=Audit tone",audio]).GetAwaiter().GetResult();
        var video=Path.Combine(root,"Clip.mp4");ProcessRunner.RunAsync(runtime.Executable("ffmpeg"),["-v","error","-f","lavfi","-i","testsrc2=size=320x180:rate=24:duration=3","-i",audio,"-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac","-shortest",video]).GetAwaiter().GetResult();
        var archive=Path.Combine(root,"Bundle.zip");using(var zip=System.IO.Compression.ZipFile.Open(archive,System.IO.Compression.ZipArchiveMode.Create)){var entry=zip.CreateEntry("proof.txt");using var writer=new StreamWriter(entry.Open());writer.Write("Nashi archive proof");}
        var pdf2=Path.Combine(root,"Other.pdf");File.Copy(pdf,pdf2);var video2=Path.Combine(root,"Other.mp4");File.Copy(video,video2);
        var originals=new[]{picture,picture2,pdf,audio,video,archive}.ToDictionary(path=>path,path=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        var results=new List<object>();var failures=new List<string>();
        void Report(string tool,bool passed,string detail){results.Add(new{tool,passed,detail});if(!passed)failures.Add(tool+": "+detail);File.WriteAllText(Path.Combine(root,"report.json"),JsonSerializer.Serialize(new{checks=results.Count,failed=failures.Count,failures,results},new JsonSerializerOptions{WriteIndented=true}));}
        var owner=new MainWindow([],library,checkForUpdates:false,offerToolSetup:false);owner.Show();DesktopInput.Pump(200);
        try
        {
            foreach(var id in FeatureCatalog.ToolNames.Keys)
            {
                File.AppendAllText(Path.Combine(root,"progress.txt"),"start "+id+Environment.NewLine);
                string[] inputs=id switch
                {
                    "createCollage" or "createPDF"=>[picture,picture2],"mergePDF"=>[pdf,pdf2],"splitPDF" or "organizePDF"=>[pdf],"extractArchive"=>[archive],
                    "normalizeAudio" or "trimAudio" or "audioChannels" or "audioToVideo" or "redactAudio" or "removeMetadata"=>[audio],
                    "joinVideos"=>[video,video2],"muteVideo" or "trimVideo" or "cropVideo" or "videoSnapshots" or "splitVideo" or "redactVideo"=>[video],"changeVideoSpeed"=>[video],_=>[picture]
                };
                owner.UIAuditSetFiles(inputs);File.AppendAllText(Path.Combine(root,"progress.txt"),"selected files"+Environment.NewLine);owner.UIAuditPresentIsland(true,"actions");DesktopInput.Pump(120);File.AppendAllText(Path.Combine(root,"progress.txt"),"presented tools"+Environment.NewLine);
                var button=owner.UIAuditToolButtons.Single(item=>item.Uid==id);var previous=library.Entries.Select(entry=>entry.Id).ToHashSet();var editorSeen=false;var saved=false;var qrRead=false;string? error=null;var start=DateTime.UtcNow;
                var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};
                timer.Tick+=(_,_)=>
                {
                    var dialog=Application.Current.Windows.OfType<Window>().FirstOrDefault(window=>window.Owner==owner&&window.IsVisible&&window is not ToastWindow);
                    if(dialog is null)return;timer.Stop();File.AppendAllText(Path.Combine(root,"progress.txt"),"dialog "+dialog.Title+Environment.NewLine);
                    try
                    {
                        editorSeen=true;
                        if(id=="readQRCode"){qrRead=Descendants(dialog).OfType<TextBox>().Any(box=>box.Text=="Nashi synthetic audit");dialog.Close();return;}
                        if(dialog is ToolEditor editor)
                        {
                            void Set(string name,string value)=>editor.FieldForTest(name).Text=value;
                            if(id=="editImage"){Set("Width","160");Set("Height","160");}
                            if(id is "cropImage" or "cropVideo"){Set("Width","128");Set("Height","96");}
                            if(id is "trimAudio" or "trimVideo" or "redactAudio"){Set("Start","0.5");Set("End","1.5");}
                            if(id=="changeVideoSpeed")Set("Speed","2");
                            if(id=="audioToVideo"){Set("Width","320");Set("Height","180");}
                            if(id=="redactAudio"){var range=Descendants(editor).OfType<Button>().Single(item=>item.Content as string=="Add selected range");DesktopInput.Click(range);if(Descendants(editor).OfType<ListBox>().Any(list=>list.Items.Count!=1))throw new InvalidOperationException("Audio redaction range was not added.");}
                            if(id is "annotateImage" or "redactImage" or "redactVideo")
                            {
                                var canvas=Descendants(editor).OfType<Canvas>().First(c=>c.Children.OfType<Image>().Any());canvas.BringIntoView();DesktopInput.Pump(100);DesktopInput.Drag(canvas,new Point(30,30),new Point(120,90));
                            }
                        }
                        if(dialog is MetadataEditor metadataEditor){metadataEditor.UIAuditRemoveAll.IsChecked=true;if(!metadataEditor.UIAuditRemoveAll.IsChecked.Value)throw new InvalidOperationException("Could not select removal of metadata.");}
                        if(dialog is PdfOrganizer organizer)
                        {
                            var page=Descendants(organizer).OfType<ListBoxItem>().First();DesktopInput.Click(page);var remove=Descendants(organizer).OfType<Button>().Single(item=>item.Content as string=="Remove");DesktopInput.Click(remove);
                        }
                        var save=Descendants(dialog).OfType<Button>().FirstOrDefault(item=>(item.Content as string)?.StartsWith("Save",StringComparison.Ordinal)==true);
                        if(save is null)throw new InvalidOperationException("Expected an editor with a Save button: "+dialog.Title);
                        Capture(dialog,Path.Combine(root,id+"-edited.png"));DesktopInput.Click(save);saved=true;
                    }
                    catch(Exception exception){error=exception.ToString();dialog.Close();}
                };
                button.BringIntoView();DesktopInput.Pump(150);Capture(owner,Path.Combine(root,id+"-tool.png"));File.AppendAllText(Path.Combine(root,"progress.txt"),"click "+id+Environment.NewLine);timer.Start();DesktopInput.Click(button);File.AppendAllText(Path.Combine(root,"progress.txt"),"clicked "+id+Environment.NewLine);
                while(DateTime.UtcNow-start<TimeSpan.FromSeconds(90)&&error is null&&!qrRead&&!library.Entries.Any(entry=>!previous.Contains(entry.Id)))DesktopInput.Pump(70);
                timer.Stop();var outputs=library.Entries.Where(entry=>!previous.Contains(entry.Id)).SelectMany(entry=>entry.Paths).ToArray();
                try
                {
                    if(error is not null)throw new InvalidOperationException(error);
                    if(id=="readQRCode"){if(!qrRead)throw new InvalidOperationException("QR value was not decoded in the UI.");}
                    else
                    {
                        if(!editorSeen||!saved||outputs.Length==0||outputs.Any(path=>!Directory.Exists(path)&&(!File.Exists(path)||new FileInfo(path).Length==0)))throw new InvalidOperationException("No saved output from the real editor. Last status: "+owner.UIAuditStatusText);
                        foreach(var output in outputs)
                        {
                            if(FeatureCatalog.Extension(output)=="pdf"){var count=DocumentEngine.PdfPageCount(output);if(count<1||id=="organizePDF"&&count!=1||id=="mergePDF"&&count!=4)throw new InvalidDataException("Unexpected PDF page count: "+count);}
                            else if(FeatureCatalog.IsImage(output))
                            {
                                var actual=ImageEngine.Load(output);if(id=="cropImage"&&(actual.PixelWidth!=128||actual.PixelHeight!=96)||id=="editImage"&&(actual.PixelWidth!=160||actual.PixelHeight!=160)||id=="createCollage"&&(actual.PixelWidth<640||actual.PixelHeight<320)||id=="frameImage"&&actual.PixelWidth<=bitmap.PixelWidth)throw new InvalidDataException("Image dimensions did not reflect edits.");
                                if(id is "annotateImage" or "redactImage"){var changed=new FormatConvertedBitmap(actual,PixelFormats.Bgra32,null,0);var pixels=new byte[changed.PixelWidth*changed.PixelHeight*4];changed.CopyPixels(pixels,changed.PixelWidth*4,0);if(pixels.SequenceEqual(qrData.Pixels))throw new InvalidDataException("Drawing did not change any pixels.");}
                            }
                            else if(FeatureCatalog.IsAudio(output)||FeatureCatalog.IsVideo(output))
                            {
                                var pending=engine.InspectAsync(output);Wait(pending);var info=pending.GetAwaiter().GetResult();
                                if(info.Duration<=0)throw new InvalidDataException("Empty media duration.");
                                if(id=="audioToVideo"&&(info.Video is null||info.Audio is null)||id=="muteVideo"&&info.Audio is not null||id=="cropVideo"&&(info.Video?.Width!=128||info.Video.Height!=96))throw new InvalidDataException("Media streams did not reflect the requested edit.");
                                if(id is "trimAudio" or "trimVideo"&&Math.Abs(info.Duration-1)>.2)throw new InvalidDataException("Trim range was not applied.");
                                if(id=="changeVideoSpeed"&&Math.Abs(info.Duration-1.5)>.25)throw new InvalidDataException("Playback speed was not applied.");
                            }
                        }
                        if(id=="splitPDF"&&outputs.Length!=2||id=="splitVideo"&&outputs.Length!=2)throw new InvalidDataException("Expected two split outputs.");
                        if(id=="extractArchive"&&!outputs.Any(path=>Directory.Exists(path)&&File.ReadAllText(Path.Combine(path,"proof.txt"))=="Nashi archive proof"))throw new InvalidDataException("Archive content mismatch.");
                    }
                    Report(id,true,id=="readQRCode"?"Decoded expected QR text in the UI.":"Native Save produced and validated: "+string.Join(", ",outputs.Select(Path.GetFileName)));
                }
                catch(Exception exception){Report(id,false,exception.Message);}
                foreach(var toast in Application.Current.Windows.OfType<ToastWindow>().ToArray())toast.Close();
                File.AppendAllText(Path.Combine(root,"progress.txt"),"done "+id+Environment.NewLine);
            }
            Report("originals-unchanged",originals.All(pair=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pair.Key)))==pair.Value),"SHA-256 of every source fixture checked after all tools.");
        }
        finally{owner.Close();}
        return failures.Count==0?0:1;
    }
    private static void Wait(Task task){var end=DateTime.UtcNow.AddSeconds(30);while(!task.IsCompleted&&DateTime.UtcNow<end)DesktopInput.Pump(50);if(!task.IsCompleted)throw new TimeoutException();}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent){for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
    private static void Capture(Window window,string path){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);ImageEngine.Save(bitmap,path,"png");}
}
#endif

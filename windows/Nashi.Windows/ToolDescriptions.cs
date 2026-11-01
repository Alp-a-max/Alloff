namespace Nashi.Windows;

internal static class ToolDescriptions
{
    internal static (string En,string Tr) For(string id)=>id switch
    {
        "editImage"=>("Adjust light, color and size","Işık, renk ve boyut ayarla"),
        "annotateImage"=>("Draw, write or highlight","Çiz, yaz veya işaretle"),
        "frameImage"=>("Add a background and border","Arka plan ve çerçeve ekle"),
        "cropImage"=>("Keep only part of a photo","Fotoğrafın bir bölümünü al"),
        "createCollage"=>("Combine images into a layout","Görselleri bir araya getir"),
        "redactImage"=>("Cover private areas","Özel alanları gizle"),
        "readQRCode"=>("Read a code from an image or PDF","Görsel veya PDF içindeki kodu oku"),
        "muteVideo"=>("Remove the audio from a video","Videonun sesini kaldır"),
        "trimVideo"=>("Keep a selected time range","Videonun istediğin kısmını kes"),
        "cropVideo"=>("Crop the video frame","Videonun görüntü alanını kırp"),
        "changeVideoSpeed"=>("Speed up or slow down","Hızlandır veya yavaşlat"),
        "joinVideos"=>("Join clips in your chosen order","Klipleri sırayla birleştir"),
        "videoSnapshots"=>("Save frames as images","Videodan fotoğraf çıkar"),
        "splitVideo"=>("Divide a video into clips","Videoyu parçalara ayır"),
        "redactVideo"=>("Cover a region in a video","Videoda özel bir alanı gizle"),
        "normalizeAudio"=>("Balance loudness","Ses yüksekliğini dengele"),
        "audioToVideo"=>("Create a waveform video from audio","Sesten dalga animasyonlu video oluştur"),
        "trimAudio"=>("Cut a song or recording","Şarkı veya ses kaydını kes"),
        "audioChannels"=>("Adjust mono, stereo and channels","Mono, stereo ve kanalları ayarla"),
        "redactAudio"=>("Beep over a selected interval","Seçilen aralığı bip sesiyle kapat"),
        "splitPDF"=>("Separate pages into new PDFs","Sayfaları ayrı PDF dosyalarına böl"),
        "organizePDF"=>("Delete, reorder or rotate pages · Remove a PDF page","PDF sayfası sil, sırala veya döndür"),
        "createPDF"=>("Turn images into a document","Görsellerden belge oluştur"),
        "mergePDF"=>("Combine PDF documents","PDF belgelerini birleştir"),
        "compress"=>("Reduce file size · Shrink photo · Compress picture","Dosya boyutunu küçült, fotoğraf küçült veya sıkıştır"),
        "removeMetadata"=>("Inspect and remove file details","Dosya bilgilerini incele ve temizle"),
        "extractArchive"=>("Open ZIP, TAR and other archives","ZIP, TAR ve diğer arşivleri aç"),
        _=>("Create a new copy","Yeni bir kopya oluştur")
    };
    internal static bool IsExtractAudioIntent(string query)
    {
        var culture=System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var compare=culture.CompareInfo;
        var hasAction=new[]{"extract","rip","save","çıkar","ayıkla","kaydet"}.Any(word=>compare.IndexOf(query,word,System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0);
        var hasAudio=new[]{"audio","sound","mp3","ses"}.Any(word=>compare.IndexOf(query,word,System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0);
        var hasVideo=new[]{"video","movie","clip","videodan"}.Any(word=>compare.IndexOf(query,word,System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0);
        return hasAction&&hasAudio&&hasVideo;
    }
    internal static bool Matches(string id,string title,string query)
    {
        if(string.IsNullOrWhiteSpace(query))return true;
        var description=For(id);var source=title+" "+L10n.Text(title)+" "+description.En+" "+description.Tr;
        var compare=System.Globalization.CultureInfo.GetCultureInfo(L10n.Language=="tr"?"tr-TR":"en-US").CompareInfo;
        var words=query.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        return words.All(word=>compare.IndexOf(source,word,System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0);
    }
}

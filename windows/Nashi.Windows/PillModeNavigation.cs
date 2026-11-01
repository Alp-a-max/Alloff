namespace Nashi.Windows;

internal static class PillModeNavigation
{
    internal static string Next(IEnumerable<string> order,string? current,int direction)
    {
        var available=order.Where(item=>item is "now" or "tools" or "download" or "media" or "history").ToArray();
        if(available.Length==0)return "now";var key=current=="actions"?"tools":current;var index=Array.IndexOf(available,key);
        var next=index<0?0:(index+(direction<0?-1:1)+available.Length)%available.Length;return available[next]=="tools"?"actions":available[next];
    }
}

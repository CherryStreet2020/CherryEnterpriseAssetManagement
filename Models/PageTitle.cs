namespace Abs.FixedAssets.Models;

public class PageTitle
{
    public string Title { get; set; } = "Dashboard";
    public string? Subtitle { get; set; }
    public string? EntityLabel { get; set; }
    public string? EntityName { get; set; }
    
    public string GetBrowserTitle()
    {
        if (!string.IsNullOrEmpty(EntityLabel))
        {
            return $"{EntityLabel} — {Title}";
        }
        return Title;
    }
    
    public string GetSubtitleDisplay()
    {
        if (!string.IsNullOrEmpty(EntityLabel) && !string.IsNullOrEmpty(EntityName))
        {
            return $"{EntityLabel} • {EntityName}";
        }
        if (!string.IsNullOrEmpty(EntityLabel))
        {
            return EntityLabel;
        }
        if (!string.IsNullOrEmpty(Subtitle))
        {
            return Subtitle;
        }
        return string.Empty;
    }
}

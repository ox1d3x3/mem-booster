namespace MemBooster.Models;

public sealed class SelectedAppItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string ExeName { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;

    public SelectedAppItem()
    {
    }

    public SelectedAppItem(string displayName, string exeName, string subtitle)
    {
        DisplayName = displayName;
        ExeName = exeName;
        Subtitle = subtitle;
    }
}

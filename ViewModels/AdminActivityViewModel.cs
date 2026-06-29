namespace StudyConnect.ViewModels;

public class AdminActivityViewModel
{
    public DateTime Time { get; set; }

    public string ActorName { get; set; } = "Hệ thống";

    public string ActorRole { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Tone { get; set; } = "blue";

    public string? Url { get; set; }
}

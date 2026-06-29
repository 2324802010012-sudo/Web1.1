namespace StudyConnect.ViewModels;

public class NotificationBellViewModel
{
    public int UnreadCount { get; set; }

    public string ReturnUrl { get; set; } = "/";

    public List<NotificationBellItemViewModel> Items { get; set; } = [];
}

public class NotificationBellItemViewModel
{
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string TimeText { get; set; } = string.Empty;

    public bool IsUnread { get; set; }

    public string Tone { get; set; } = "blue";
}

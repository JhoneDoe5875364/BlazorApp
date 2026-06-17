namespace HCP.HRPortal.Models;

/// <summary>
/// A single user notification. <see cref="Recipient"/> is the employee email the item
/// belongs to. <see cref="Tone"/> maps to the shared tone-* / reminder-ic colour classes.
/// </summary>
public class NotificationItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Recipient { get; init; } = "";
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public string Icon { get; init; } = "bi-bell-fill";
    public string Tone { get; init; } = "info"; // primary/success/warning/danger/info/purple
    /// <summary>Route to open when the notification is clicked (to action/resolve it).</summary>
    public string Link { get; init; } = "/notifications";
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public bool IsRead { get; set; }
}

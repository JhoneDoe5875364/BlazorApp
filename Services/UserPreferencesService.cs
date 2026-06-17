namespace HCP.HRPortal.Services;

public class UserPreferences
{
    public string Language { get; set; } = "English (US)";
    public string DateFormat { get; set; } = "DD MMM YYYY";
    public string TimeZone { get; set; } = "(GMT+04:00) Dubai";
    public string Theme { get; set; } = "Light";
    public string Density { get; set; } = "Comfortable";
    public string DefaultPage { get; set; } = "Dashboard";
    public int ItemsPerPage { get; set; } = 25;
    public bool EmailNotifications { get; set; } = true;
    public bool PushNotifications { get; set; } = true;
    public bool WeeklyDigest { get; set; }
    public bool DocExpiryReminders { get; set; } = true;

    public UserPreferences Clone() => (UserPreferences)MemberwiseClone();
}

/// <summary>Per-user preferences. In-memory singleton (prototype) — persists for the app lifetime.</summary>
public interface IUserPreferencesService
{
    UserPreferences Get(string userEmail);
    void Save(string userEmail, UserPreferences prefs);
}

public class UserPreferencesService : IUserPreferencesService
{
    private readonly object _lock = new();
    private readonly Dictionary<string, UserPreferences> _prefs = new();

    public UserPreferences Get(string userEmail)
    {
        lock (_lock)
        {
            if (!_prefs.TryGetValue(userEmail, out var p)) { p = new(); _prefs[userEmail] = p; }
            return p.Clone(); // edit a copy; persists only on Save
        }
    }

    public void Save(string userEmail, UserPreferences prefs)
    {
        if (string.IsNullOrWhiteSpace(userEmail)) return;
        lock (_lock) _prefs[userEmail] = prefs.Clone();
    }
}

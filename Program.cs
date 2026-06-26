using HCP.HRPortal.Components;
using HCP.HRPortal.Components.Account;
using HCP.HRPortal.Data;
using HCP.HRPortal.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Pin ContentRoot to the directory containing the executable, so wwwroot / appsettings.json
// resolve correctly regardless of the working directory the exe was launched from. Without
// this, launching the published exe via a different CWD falls back to that CWD and 404s the
// scoped CSS bundle (and other published assets).
var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = string.IsNullOrEmpty(exeDir) ? AppContext.BaseDirectory : exeDir,
});

// Serve the per-component scoped CSS bundle ({Assembly}.styles.css) in every environment.
// WebApplicationBuilder enables this automatically only under Development, so without this
// call the published / direct-run-exe (Production) loses the shell layout styling.
builder.WebHost.UseStaticWebAssets();

// --- Blazor / Razor components ------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

// --- Authentication state -----------------------------------------------------
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
})
.AddIdentityCookies();

// --- Database ----------------------------------------------------------------
// SQLite (file-based) by default — zero install for the recipient. The .db file
// is created next to the executable on first boot and survives restarts.
// Override with appsettings "UseInMemoryDatabase": true for ephemeral testing.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    // Default: file next to the exe so it travels with the publish folder.
    var dbPath = Path.Combine(builder.Environment.ContentRootPath, "hcp_hrportal.db");
    connectionString = $"Data Source={dbPath}";
}

var forceInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");
if (forceInMemory)
{
    Console.WriteLine("[DB] UseInMemoryDatabase=true — running on an in-memory database (data lost on restart).");
    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("hcp_hrportal"));
}
else
{
    Console.WriteLine($"[DB] SQLite — {connectionString}");
    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}

// A scoped context (bridged from the factory) is what Identity's stores resolve.
builder.Services.AddScoped<ApplicationDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

// --- ASP.NET Core Identity ----------------------------------------------------
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// --- HR domain services -------------------------------------------------------
// Registered by interface (brief §8) so the EF/in-memory implementations can be
// swapped for SharePoint / Microsoft Graph later by changing only these lines.
builder.Services.AddSingleton<IEmployeeService, FakeEmployeeService>();
builder.Services.AddSingleton<ITimeOffService, FakeTimeOffService>();
builder.Services.AddSingleton<IExpenseService, FakeExpenseService>();
builder.Services.AddSingleton<IDocumentService, FakeDocumentService>();
builder.Services.AddSingleton<ICalendarService, FakeCalendarService>();
builder.Services.AddSingleton<ITrainingService, FakeTrainingService>();
builder.Services.AddSingleton<IInvoiceService, FakeInvoiceService>();

// --- Microsoft 365 / SharePoint integration -----------------------------------
// Real credentials come from User Secrets (development) or environment variables /
// Key Vault (production). appsettings.json should hold empty placeholders only.
builder.Services.Configure<M365Options>(builder.Configuration.GetSection(M365Options.SectionName));
builder.Services.AddSingleton<ISharePointFileService, SharePointFileService>();
builder.Services.AddSingleton<IPerformanceService, FakePerformanceService>();
builder.Services.AddSingleton<ISalaryCertificateService, FakeSalaryCertificateService>();
builder.Services.AddSingleton<ITravelService, FakeTravelService>();
builder.Services.AddSingleton<IAnnouncementService, FakeAnnouncementService>();
builder.Services.AddSingleton<IPayslipService, FakePayslipService>();

// Resolves the signed-in user's Employee profile (scoped to the circuit).
builder.Services.AddScoped<CurrentUserService>();

// App-wide notification hub (real-time badge via the Blazor Server circuit).
builder.Services.AddSingleton<INotificationService, NotificationService>();

// In-memory stores for prototype features (policies, preferences, timesheets).
builder.Services.AddSingleton<IPolicyService, PolicyService>();
builder.Services.AddSingleton<IUserPreferencesService, UserPreferencesService>();
builder.Services.AddSingleton<ITimesheetService, TimesheetService>();
builder.Services.AddScoped<ITimesheetExporter, TimesheetExporter>();

var app = builder.Build();

// --- Apply migrations + seed roles/users/demo data ----------------------------
await DbInitializer.InitializeAsync(app.Services);

// --- Optional SharePoint connectivity self-test (set HCP_SP_SELFTEST=1) --------
await SharePointSelfTest.RunAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Auto-open the user's default browser at the bound URL the instant Kestrel is ready,
// so a recipient who double-clicks the exe gets the login page without having to copy
// the URL from the console. Set HCP_NO_BROWSER=1 to skip (headless / service / smoke
// tests). The try/catch keeps the server alive even if the browser launch fails.
if (Environment.GetEnvironmentVariable("HCP_NO_BROWSER") != "1")
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = app.Urls.FirstOrDefault() ?? "http://localhost:5000";
        var browserUrl = url.Replace("0.0.0.0", "localhost").Replace("[::]", "localhost");
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = browserUrl,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Browser launch is best-effort — the server keeps running regardless.
        }
    });
}

app.Run();

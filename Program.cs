using MaintenanceSandbox.Data;
using MaintenanceSandbox.Directory.Data;
using MaintenanceSandbox.Directory.Models;
using MaintenanceSandbox.Directory.Services;
using MaintenanceSandbox.Middleware;
using MaintenanceSandbox.Security;
using MaintenanceSandbox.Services;
using MaintenanceSandbox.Services.Ai;
using MaintenanceSandbox.Services.Onboarding;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;





var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

var supportedCultures = new[]
{
    new CultureInfo("en-CA"),
    new CultureInfo("fr-CA"),
    new CultureInfo("es-MX")
};

builder.Services.AddRazorPages();


builder.Services.AddSignalR();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en-CA");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider(),
        new QueryStringRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

builder.Services.AddScoped<IClaimsTransformation, TenantClaimsTransformation>();
builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

builder.Services.AddHttpContextAccessor();

// =====================================================
// 1) BUSINESS DB (existing) - unchanged
// =====================================================
var businessConn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=MaintenanceSandbox;Trusted_Connection=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(businessConn));

// =====================================================
// 2) DIRECTORY DB (new) - Identity + Tenants + Subs
// =====================================================
var directoryConn = builder.Configuration.GetConnectionString("DirectoryConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=SentinelDirectory;Trusted_Connection=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<DirectoryDbContext>(options =>
    options.UseSqlServer(directoryConn));

// =====================================================
// 3) IDENTITY (Directory DB)
// =====================================================
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<DirectoryDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();   // ✅ this is what creates /Identity/Account/Login

    

builder.Services.ConfigureApplicationCookie(options =>
{
    // If you are using Identity UI scaffolding, use these:
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.SlidingExpiration = true;
});


// =====================================================
// 4) TENANCY SERVICES
// =====================================================
// IMPORTANT: your current TenantProvider falls back to a dev GUID.
// For production, remove fallback and force Subscribe when missing.
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<RequireTenantFilter>();
builder.Services.AddScoped<MaintenanceSandbox.Filters.BlockDemoFilter>();



// TODO (later today): register provisioning once moved in
// builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
// builder.Services.AddScoped<RequireTenantFilter>();

// =====================================================
// 5) AI + OTHER SERVICES (unchanged)
// =====================================================
builder.Services.AddSingleton<IMaintenanceSuggestionService, MaintenanceSuggestionService>();
builder.Services.AddSingleton<ITierProvider, TierProvider>();

// ❌ Demo auth provider: remove once Identity is live
builder.Services.AddScoped<IDemoUserProvider, DemoUserProvider>();

builder.Services.AddMemoryCache();

builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));

var claudeApiKey = builder.Configuration["Ai:ApiKey"];
if (!string.IsNullOrWhiteSpace(claudeApiKey))
{
    builder.Services.AddHttpClient<IChatModel, ClaudeChatModel>((sp, client) =>
    {
        client.DefaultRequestHeaders.Add("x-api-key", claudeApiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        client.Timeout = TimeSpan.FromSeconds(30);
    });
}
else
{
    builder.Services.AddSingleton<IChatModel, NullChatModel>();
}

builder.Services.AddScoped<MaintenanceAiService>();
builder.Services.AddScoped<IAiAssistantClient, AiAssistantClient>();
builder.Services.AddScoped<IOnboardingAiService, OnboardingAiService>();
builder.Services.AddScoped<IOnboardingAiClient, OnboardingAiClient>();

// =====================================================
// AI ORCHESTRATOR (Ollama-backed incident assistant)
// =====================================================
builder.Services.AddHttpClient<IOllamaService, OllamaService>((sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddScoped<IIncidentAiTools, IncidentAiTools>();
builder.Services.AddScoped<IAiOrchestrator, AiOrchestrator>();

builder.Services.Configure<MaintenanceSandbox.Demo.DemoOptions>(
    builder.Configuration.GetSection("Demo"));

builder.Services.AddScoped<MaintenanceSandbox.Demo.IDemoMode, MaintenanceSandbox.Demo.DemoMode>();
builder.Services.AddSingleton<MaintenanceSandbox.Demo.IDemoAiRateLimiter, MaintenanceSandbox.Demo.DemoAiRateLimiter>();
builder.Services.AddSingleton<MaintenanceSandbox.Demo.DemoSmsLinkTokenService>();

var smtpHost = builder.Configuration["Email:SmtpHost"];
if (!string.IsNullOrWhiteSpace(smtpHost))
{
    builder.Services.AddSingleton<MaintenanceSandbox.Services.IEmailService>(
        new MaintenanceSandbox.Services.SmtpEmailService(
            smtpHost,
            int.TryParse(builder.Configuration["Email:SmtpPort"], out var port) ? port : 587,
            builder.Configuration["Email:SmtpUser"] ?? "",
            builder.Configuration["Email:SmtpPassword"] ?? "",
            builder.Configuration["Email:FromAddress"] ?? ""));
}
else
{
    builder.Services.AddSingleton<MaintenanceSandbox.Services.IEmailService,
        MaintenanceSandbox.Services.NullEmailService>();
}


builder.Services.AddAuthorization();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// =====================================================
// MIGRATIONS / SEEDING
// =====================================================
using (var scope = app.Services.CreateScope())
{

    // Business DB - skip migration, database already exists on Azure
    var businessDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // businessDb.Database.Migrate(); // Commented out - Azure DB already exists
    DbInitializer.PurgeExpiredDemoTenantsAsync(businessDb, TimeSpan.FromHours(2)).GetAwaiter().GetResult();
    DbInitializer.SeedAsync(businessDb).GetAwaiter().GetResult();

    // Directory DB - skip migration, database already exists on Azure
    var directoryDb = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
    // directoryDb.Database.Migrate(); // Commented out - Azure DB already exists

    // Optional: seed demo tenant/user later via a seeder you control
    // DirectorySeeder.Seed(directoryDb, scope.ServiceProvider);
}

// =====================================================
// HTTP PIPELINE
// =====================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.MapHub<MaintenanceSandbox.Hubs.MaintenanceHub>("/hubs/maintenance");

app.UseAuthentication();

app.Use(async (ctx, next) =>
{
    // Only redirect browser page loads (avoid breaking APIs, fetch, etc.)
    var acceptsHtml =
        ctx.Request.Headers.Accept.Any(a => a!.Contains("text/html", StringComparison.OrdinalIgnoreCase));

    if (!acceptsHtml)
    {
        await next();
        return;
    }

    if (ctx.User?.Identity?.IsAuthenticated == true)
    {
        var path = ctx.Request.Path;

        // Don’t interfere with onboarding flow itself or Identity endpoints
        if (!path.StartsWithSegments("/onboarding")
    && !path.StartsWithSegments("/subscribe")   // IMPORTANT
    && !path.StartsWithSegments("/Identity")
    && !path.StartsWithSegments("/css")
    && !path.StartsWithSegments("/js")
    && !path.StartsWithSegments("/lib")
    && !path.StartsWithSegments("/images")
    && !path.StartsWithSegments("/hubs")
    && !path.StartsWithSegments("/api"))
        {
            using var scope = ctx.RequestServices.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await userManager.GetUserAsync(ctx.User);

            // Define your “needs onboarding” rule
            var needsOnboarding =
                user != null &&
                (user.TenantId == null || user.TenantId == Guid.Empty
                 || !await db.OnboardingSessions.AnyAsync(s =>
                        s.UserId == user.Id && s.OnboardedAtUtc != null));

            if (needsOnboarding)
            {
                ctx.Response.Redirect("/onboarding");
                return;
            }
        }
    }

    await next();
});

app.UseMiddleware<SubscriptionGateMiddleware>();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();


// ❌ Remove duplicate seeding call (you had two seeders)
// If you still need DbSeeder, do it once above in the seeding block.
// using (var scope = app.Services.CreateScope())
// {
//     var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//     DbSeeder.Seed(db);
// }

app.Run();

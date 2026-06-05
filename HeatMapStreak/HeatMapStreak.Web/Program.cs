using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HeatMapStreak.Web.Components;
using HeatMapStreak.Web.Components.Account;
using HeatMapStreak.Web.Data;
using HeatMapStreak.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    });

authBuilder.AddIdentityCookies();

var googleId = builder.Configuration["Authentication:Google:ClientId"];
if (!string.IsNullOrEmpty(googleId))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleId;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
    });
}

var githubId = builder.Configuration["Authentication:GitHub:ClientId"];
if (!string.IsNullOrEmpty(githubId))
{
    authBuilder.AddGitHub(options =>
    {
        options.ClientId = githubId;
        options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"] ?? "";
        options.Scope.Add("user:email");
    });
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddScoped<IHabitService, HabitService>();
builder.Services.AddScoped<IStreakService, StreakService>();
builder.Services.AddScoped<IThemeService, ThemeService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

app.Run();

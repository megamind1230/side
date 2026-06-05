using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WeNeedA.Web.Data;
using WeNeedA.Web.Models;
using WeNeedA.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=weneeda.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddIdentity<WeNeedAUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

builder.Services.AddRazorPages();
builder.Services.AddControllers();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IListingService, ListingService>();
builder.Services.AddScoped<ITagService, TagService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    var userManager = sp.GetRequiredService<UserManager<WeNeedAUser>>();
    var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
    await DataSeeder.SeedAsync(db, userManager, roleManager);
}

app.Run();

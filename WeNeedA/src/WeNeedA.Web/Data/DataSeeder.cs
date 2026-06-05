using Microsoft.AspNetCore.Identity;
using WeNeedA.Web.Models;

namespace WeNeedA.Web.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db, UserManager<WeNeedAUser>? userManager = null, RoleManager<IdentityRole>? roleManager = null)
    {
        if (db.Countries.Any() && db.Tags.Any())
            return;

        if (!db.Countries.Any())
        {
            var egypt = new Country { Name = "Egypt", Slug = "egypt" };
            db.Countries.Add(egypt);
            await db.SaveChangesAsync();

            var cairo = new Governorate { Name = "Cairo", Slug = "cairo", CountryId = egypt.Id };
            var giza = new Governorate { Name = "Giza", Slug = "giza", CountryId = egypt.Id };
            var alex = new Governorate { Name = "Alexandria", Slug = "alexandria", CountryId = egypt.Id };
            db.Governorates.AddRange(cairo, giza, alex);
            await db.SaveChangesAsync();

            var cairoCities = new List<City>
            {
                new() { Name = "Nasr City", Slug = "nasr-city", GovernorateId = cairo.Id },
                new() { Name = "Maadi", Slug = "maadi", GovernorateId = cairo.Id },
                new() { Name = "Heliopolis", Slug = "heliopolis", GovernorateId = cairo.Id },
                new() { Name = "Downtown Cairo", Slug = "downtown-cairo", GovernorateId = cairo.Id },
            };
            var gizaCities = new List<City>
            {
                new() { Name = "6th of October", Slug = "6th-of-october", GovernorateId = giza.Id },
                new() { Name = "Dokki", Slug = "dokki", GovernorateId = giza.Id },
                new() { Name = "Mohandessin", Slug = "mohandessin", GovernorateId = giza.Id },
            };
            var alexCities = new List<City>
            {
                new() { Name = "Al Montaza", Slug = "al-montaza", GovernorateId = alex.Id },
                new() { Name = "Al Raml", Slug = "al-raml", GovernorateId = alex.Id },
            };
            db.Cities.AddRange(cairoCities);
            db.Cities.AddRange(gizaCities);
            db.Cities.AddRange(alexCities);
            await db.SaveChangesAsync();

            var villages = new List<Village>
            {
                new() { Name = "Sheraton", Slug = "sheraton", CityId = cairoCities[0].Id },
                new() { Name = "El Nozha", Slug = "el-nozha", CityId = cairoCities[0].Id },
                new() { Name = "Hadayek El Maadi", Slug = "hadayek-el-maadi", CityId = cairoCities[1].Id },
                new() { Name = "Wahat", Slug = "wahat", CityId = gizaCities[0].Id },
            };
            db.Villages.AddRange(villages);
            await db.SaveChangesAsync();
        }

        if (!db.Tags.Any())
        {
            var tags = new List<Tag>
            {
                new() { Name = "Plumber", Slug = "plumber", IsApproved = true },
                new() { Name = "Electrician", Slug = "electrician", IsApproved = true },
                new() { Name = "Carpenter", Slug = "carpenter", IsApproved = true },
                new() { Name = "Painter", Slug = "painter", IsApproved = true },
                new() { Name = "Tiler", Slug = "tiler", IsApproved = true },
                new() { Name = "Blacksmith", Slug = "blacksmith", IsApproved = true },
                new() { Name = "Welder", Slug = "welder", IsApproved = true },
                new() { Name = "Tailor", Slug = "tailor", IsApproved = true },
                new() { Name = "Baker", Slug = "baker", IsApproved = true },
                new() { Name = "Butcher", Slug = "butcher", IsApproved = true },
                new() { Name = "Taxi Driver", Slug = "taxi-driver", IsApproved = true },
                new() { Name = "Truck Driver", Slug = "truck-driver", IsApproved = true },
                new() { Name = "Bus Driver", Slug = "bus-driver", IsApproved = true },
                new() { Name = "Mechanic", Slug = "mechanic", IsApproved = true },
                new() { Name = "Barber", Slug = "barber", IsApproved = true },
                new() { Name = "Plasterer", Slug = "plasterer", IsApproved = true },
                new() { Name = "Mason", Slug = "mason", IsApproved = true },
                new() { Name = "Farmer", Slug = "farmer", IsApproved = true },
            };
            db.Tags.AddRange(tags);
            await db.SaveChangesAsync();
        }

        if (roleManager != null && userManager != null)
        {
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            var adminEmail = "admin@weneeda.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new WeNeedAUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    IsEmailVerified = true,
                    IsSsidVerified = true,
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}

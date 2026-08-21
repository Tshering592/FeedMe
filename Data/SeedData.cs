using FeedMe.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FeedMe.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Create roles
            // ADDED "Kitchen" - a dedicated role so kitchen order notifications don't fan out to
            // regular Staff members (teachers etc.) who also place food orders and share the "Staff" role.
            string[] roles = { "Admin", "Staff", "Student", "Kitchen" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // 2a. Admin user setup
            const string adminEmail = "admin@canteen.com";
            const string adminStaffId = "ADMIN001";
            const string adminPassword = "Admin@Can@1991";
            const string defaultContact = "0000000000";
            const string defaultDietary = "None";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new AppUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Admin",
                    StaffId = adminStaffId,
                    StudentId = null, // Set to null instead of "N/A"
                    EmailConfirmed = true,
                    ContactNumber = defaultContact,
                    DietaryPreference = defaultDietary,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    throw new Exception($"Admin creation failed: {errors}");
                }
            }
            else
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(adminUser);
                await userManager.ResetPasswordAsync(adminUser, token, adminPassword);

                adminUser.EmailConfirmed = true;
                adminUser.FullName = "System Admin";
                await userManager.UpdateAsync(adminUser);
            }

            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // 2b. Kitchen Staff user setup
            const string kitchenEmail = "kitchen@tcmit.edu.np";
            const string kitchenUsername = "kitchen_staff";
            const string kitchenPassword = "Kitchen@12345";

            var kitchenUser = await userManager.FindByEmailAsync(kitchenEmail)
                              ?? await userManager.FindByNameAsync(kitchenUsername);

            if (kitchenUser == null)
            {
                kitchenUser = new AppUser
                {
                    UserName = kitchenUsername,
                    Email = kitchenEmail,
                    FullName = "TCMIT Canteen Kitchen",
                    StaffId = "KITCHEN-01",
                    StudentId = null, // Set to null instead of "N/A"
                    ContactNumber = "9800000000",
                    DietaryPreference = defaultDietary,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createKitchenResult = await userManager.CreateAsync(kitchenUser, kitchenPassword);
                if (!createKitchenResult.Succeeded)
                {
                    var errors = string.Join(", ", createKitchenResult.Errors.Select(e => e.Description));
                    throw new Exception($"Kitchen user creation failed: {errors}");
                }
            }

            // Kept in "Staff" role so [Authorize(Roles = "Staff,Admin")] on KitchenController still works unchanged.
            if (!await userManager.IsInRoleAsync(kitchenUser, "Staff"))
            {
                await userManager.AddToRoleAsync(kitchenUser, "Staff");
            }

            // ADDED: dedicated "Kitchen" role, used only for targeting order notifications correctly.
            if (!await userManager.IsInRoleAsync(kitchenUser, "Kitchen"))
            {
                await userManager.AddToRoleAsync(kitchenUser, "Kitchen");
            }

            // 3. Seed categories
            if (!context.Categories.Any())
            {
                context.Categories.AddRange(
                    new Category { Name = "Meals", DisplayOrder = 1 },
                    new Category { Name = "Snacks", DisplayOrder = 2 },
                    new Category { Name = "Beverages", DisplayOrder = 3 }
                );
                await context.SaveChangesAsync();
            }

            // 4. Seed menu items
            if (!context.MenuItems.Any())
            {
                var meals = await context.Categories.FirstAsync(c => c.Name == "Meals");
                var snacks = await context.Categories.FirstAsync(c => c.Name == "Snacks");
                var beverages = await context.Categories.FirstAsync(c => c.Name == "Beverages");

                const string defaultImage = "https://via.placeholder.com/150";

                context.MenuItems.AddRange(
                    new MenuItem
                    {
                        Name = "Chicken Rice",
                        Description = "Grilled chicken with rice",
                        Price = 8.50m,
                        Category = meals,
                        IsAvailable = true,
                        IsHotMeal = true,
                        ImageUrl = defaultImage
                    },
                    new MenuItem
                    {
                        Name = "Veggie Wrap",
                        Description = "Fresh vegetables in tortilla",
                        Price = 5.00m,
                        Category = snacks,
                        IsAvailable = true,
                        IsHotMeal = false,
                        ImageUrl = defaultImage
                    },
                    new MenuItem
                    {
                        Name = "Iced Latte",
                        Description = "Chilled coffee with milk",
                        Price = 3.50m,
                        Category = beverages,
                        IsAvailable = true,
                        IsHotMeal = true,
                        ImageUrl = defaultImage
                    }
                );
                await context.SaveChangesAsync();
            }

            // 5. Seed time slots
            if (!context.TimeSlots.Any())
            {
                var now = DateTime.UtcNow.Date;
                context.TimeSlots.AddRange(
                    new TimeSlot { StartTime = now.AddHours(10), EndTime = now.AddHours(10).AddMinutes(30), MaxOrders = 20, IsActive = true },
                    new TimeSlot { StartTime = now.AddHours(11), EndTime = now.AddHours(11).AddMinutes(30), MaxOrders = 20, IsActive = true },
                    new TimeSlot { StartTime = now.AddHours(12), EndTime = now.AddHours(12).AddMinutes(30), MaxOrders = 20, IsActive = true }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}
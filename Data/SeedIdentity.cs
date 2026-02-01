using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.Extensions.Hosting;

namespace DoAnTotNghiep.Data
{
    public static class SeedIdentity
    {
        private const string DefaultPassword = "268191Thiena@";

        public static async Task SeedRolesAndAdminAsync(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var logger = services.GetRequiredService<ILogger<Program>>();

          
            string[] roleNames = { "Admin", "Sales", "Warehouse", "Logistics", "Purchasing" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                    if (!roleResult.Succeeded)
                    {
                        logger.LogError("Failed to create role {Role}: {Errors}",
                            roleName, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    }
                    else
                    {
                        logger.LogInformation("Role '{Role}' created successfully.", roleName);
                    }
                }
            }

         
            string adminEmail = "admin@kbhome.vn";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                logger.LogInformation("Admin not found. Creating admin with default password.");

                adminUser = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(adminUser, DefaultPassword);
                if (!createResult.Succeeded)
                {
                    logger.LogError("Failed to create admin: {Errors}",
                        string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    return;
                }

                logger.LogInformation("Admin created successfully with default password.");
            }
            else
            {
                logger.LogInformation("Admin exists. Resetting password to default.");

                var resetToken = await userManager.GeneratePasswordResetTokenAsync(adminUser);
                var resetResult = await userManager.ResetPasswordAsync(adminUser, resetToken, DefaultPassword);

                if (!resetResult.Succeeded)
                {
                    logger.LogError("Failed to reset admin password: {Errors}",
                        string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                }
                else
                {
                    logger.LogInformation("Admin password reset to default successfully.");
                }
            }

            // Đảm bảo Admin có đủ role
            await EnsureRolesForUserAsync(userManager, logger, adminUser, roleNames);

            // --- Tạo / Reset demo users ---
            await CreateOrResetDemoUserAsync(userManager, logger, "purchasing@kbhome.vn", "Purchasing");
            await CreateOrResetDemoUserAsync(userManager, logger, "warehouse@kbhome.vn", "Warehouse");
            await CreateOrResetDemoUserAsync(userManager, logger, "sales@kbhome.vn", "Sales");
            await CreateOrResetDemoUserAsync(userManager, logger, "logistics@kbhome.vn", "Logistics");
        }

        private static async Task EnsureRolesForUserAsync(
            UserManager<IdentityUser> userManager,
            ILogger logger,
            IdentityUser user,
            string[] roleNames)
        {
            var currentRoles = await userManager.GetRolesAsync(user);
            var missingRoles = roleNames.Except(currentRoles).ToArray();

            if (missingRoles.Any())
            {
                var addRes = await userManager.AddToRolesAsync(user, missingRoles);
                if (!addRes.Succeeded)
                {
                    logger.LogError("Failed to assign roles to {Email}: {Errors}",
                        user.Email, string.Join(", ", addRes.Errors.Select(e => e.Description)));
                }
                else
                {
                    logger.LogInformation("Assigned roles {Roles} to {Email}.",
                        string.Join(", ", missingRoles), user.Email);
                }
            }
        }

        private static async Task CreateOrResetDemoUserAsync(
            UserManager<IdentityUser> userManager,
            ILogger logger,
            string email,
            string role)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                logger.LogInformation("Demo user {Email} not found. Creating with role {Role}.", email, role);

                user = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var createRes = await userManager.CreateAsync(user, DefaultPassword);
                if (!createRes.Succeeded)
                {
                    logger.LogError("Failed to create demo user {Email}: {Errors}",
                        email, string.Join(", ", createRes.Errors.Select(e => e.Description)));
                    return;
                }

                await userManager.AddToRoleAsync(user, role);

                logger.LogInformation("Demo user {Email} created with role {Role}.", email, role);
            }
            else
            {
                logger.LogInformation("Demo user {Email} exists. Resetting password and ensuring role {Role}.", email, role);

                var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                var resetRes = await userManager.ResetPasswordAsync(user, resetToken, DefaultPassword);

                if (!resetRes.Succeeded)
                {
                    logger.LogError("Failed to reset password for demo user {Email}: {Errors}",
                        email, string.Join(", ", resetRes.Errors.Select(e => e.Description)));
                }

                var roles = await userManager.GetRolesAsync(user);
                if (!roles.Contains(role))
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }
    }
}

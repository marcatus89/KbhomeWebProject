using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using DoAnTotNghiep.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace DoAnTotNghiep
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<Program>>();

                try
                {
                    var config = services.GetRequiredService<IConfiguration>();
                    var connectionString = config.GetConnectionString("DefaultConnection");

                    if (string.IsNullOrWhiteSpace(connectionString))
                    {
                        logger.LogWarning(
                            "ConnectionStrings:DefaultConnection is not configured. Skipping migrations and seeding.");
                    }
                    else
                    {
                        var context = services.GetRequiredService<ApplicationDbContext>();

                        // Apply migrations with retry
                        var maxRetry = 5;
                        var delaySeconds = 2;

                        for (int attempt = 1; attempt <= maxRetry; attempt++)
                        {
                            try
                            {
                                await context.Database.MigrateAsync();
                                logger.LogInformation("Database migrations applied successfully.");
                                break;
                            }
                            catch (Exception ex)
                            {
                                if (attempt == maxRetry)
                                {
                                    logger.LogError(ex, "Migration failed after {Attempts} attempts.", maxRetry);
                                    throw;
                                }

                                logger.LogWarning(ex,
                                    "Migration failed (attempt {Attempt}/{Max}), retrying in {Delay}s...",
                                    attempt, maxRetry, delaySeconds);

                                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                                delaySeconds = Math.Min(delaySeconds * 2, 30);
                            }
                        }

                        // Seed sample data
                        try
                        {
                            SeedData.Initialize(context, logger);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "SeedData.Initialize failed.");
                        }

                        // Seed roles + admin + demo accounts
                        await SeedIdentity.SeedRolesAndAdminAsync(services);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred during database initialization or seeding.");
                    throw;
                }
            }

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}

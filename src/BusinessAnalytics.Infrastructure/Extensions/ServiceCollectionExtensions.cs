using System.Globalization;
using AutoMapper;
using BusinessAnalytics.Domain.Entities;
using BusinessAnalytics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessAnalytics.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseSqlServer(config.GetConnectionString("DefaultConnection"),
                    sql => sql.EnableRetryOnFailure());
            });

            services.AddIdentityCore<ApplicationUser>(opt =>
            {
                opt.User.RequireUniqueEmail = true;
            })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>();

            services.AddAutoMapper(cfg=> { },typeof(ServiceCollectionExtensions).Assembly);

            return services;
        }

        public static async Task EnsureDatabaseSeededAsync(this IServiceProvider sp, CancellationToken ct = default)
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync(ct);

            // Seed DimDate 2018..2030 if empty
            if (!db.DimDates.Any())
            {
                var start = new DateOnly(2018, 1, 1);
                var end = new DateOnly(2030, 12, 31);
                var list = new List<DimDate>();
                var ci = CultureInfo.InvariantCulture;

                for (var d = start; d <= end; d = d.AddDays(1))
                {
                    list.Add(new DimDate
                    {
                        DateKey = DimDate.ToDateKey(d),
                        Date = d,
                        Year = d.Year,
                        Quarter = (d.Month - 1) / 3 + 1,
                        Month = d.Month,
                        MonthName = ci.DateTimeFormat.GetMonthName(d.Month),
                        Day = d.Day,
                        IsoWeek = ISOWeek.GetWeekOfYear(d.ToDateTime(TimeOnly.MinValue))
                    });
                }
                db.DimDates.AddRange(list);
                await db.SaveChangesAsync(ct);
            }

            // Seed roles
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var r in new[] { "Admin", "Analyst", "Viewer" })
                if (!await roleManager.RoleExistsAsync(r))
                    await roleManager.CreateAsync(new IdentityRole(r));
        }
    }
}

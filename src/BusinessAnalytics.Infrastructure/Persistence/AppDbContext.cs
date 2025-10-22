using BusinessAnalytics.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace BusinessAnalytics.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<DataSource> DataSources => Set<DataSource>();
        public DbSet<RawImport> RawImports => Set<RawImport>();
        public DbSet<DimDate> DimDates => Set<DimDate>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<DimDate>().HasKey(x => x.DateKey);
            b.Entity<DimDate>()
                .Property(x => x.DateKey)
                .ValueGeneratedNever();     // <-- important: NOT identity
            b.Entity<DimDate>()
                .Property(x => x.MonthName)
                .HasMaxLength(20);
            b.Entity<DimDate>()
                .Property(x => x.Date)
                .HasColumnType("date");     // optional, keeps pure date in SQL

            b.Entity<DataSource>()
                .HasOne(x => x.Owner)
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<RawImport>()
                .HasOne(x => x.DataSource)
                .WithMany(x => x.Imports)
                .HasForeignKey(x => x.DataSourceId);

            b.Entity<DataSource>()
                .Property(x => x.Name).HasMaxLength(150).IsRequired();

            b.Entity<DataSource>()
                .HasIndex(x => new { x.OwnerId, x.CreatedAtUtc });

            b.Entity<RawImport>()
                .HasIndex(x => new { x.DataSourceId, x.Status });
        }
    }
}

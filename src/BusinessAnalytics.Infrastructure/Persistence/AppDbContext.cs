using BusinessAnalytics.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;


namespace BusinessAnalytics.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<DataSource> DataSources => Set<DataSource>();
        public DbSet<RawImport> RawImports => Set<RawImport>();
        public DbSet<DimDate> DimDates => Set<DimDate>();
        public DbSet<DimProduct> DimProducts => Set<DimProduct>();
        public DbSet<DimCustomer> DimCustomers => Set<DimCustomer>();
        public DbSet<FactSales> FactSales => Set<FactSales>();
        public DbSet<DataSourceMapping> DataSourceMappings => Set<DataSourceMapping>();

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

            // DimProduct
            b.Entity<DimProduct>(e =>
            {
                e.HasKey(p => p.ProductKey);
                e.Property(p => p.ProductName)
                    .HasMaxLength(200)
                    .IsRequired();
            });

            // DimCustomer
            b.Entity<DimCustomer>(e =>
            {
                e.HasKey(c => c.CustomerKey);
                e.Property(c => c.CustomerName)
                    .HasMaxLength(200)
                    .IsRequired();
            });

            // FactSales
            b.Entity<FactSales>(e =>
            {
                e.HasKey(f => f.Id);

                b.Entity<FactSales>()
                    .Property(f => f.Quantity)
                    .HasColumnType("decimal(18,2)");

                e.Property(f => f.Amount)
                    .HasColumnType("decimal(18,2)");

                e.HasIndex(f => f.DateKey);

                e.HasOne(f => f.Product)
                    .WithMany()
                    .HasForeignKey(f => f.ProductKey)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(f => f.Customer)
                    .WithMany()
                    .HasForeignKey(f => f.CustomerKey)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(f => f.DimDate)
                    .WithMany()
                    .HasForeignKey(f => f.DateKey)
                    .OnDelete(DeleteBehavior.Restrict);

                // FactSales ↔ DataSource
                b.Entity<FactSales>()
                    .HasOne(fs => fs.DataSource)
                    .WithMany(ds => ds.FactSales)
                    .HasForeignKey(fs => fs.DataSourceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<DataSource>()
                .Property(x => x.Name).HasMaxLength(150).IsRequired();

            b.Entity<DataSource>()
                .HasIndex(x => new { x.OwnerId, x.CreatedAtUtc });

            b.Entity<RawImport>()
                .HasIndex(x => new { x.DataSourceId, x.Status });

            b.Entity<DataSourceMapping>()
                .HasOne(m => m.DataSource).WithMany()
                .HasForeignKey(m => m.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

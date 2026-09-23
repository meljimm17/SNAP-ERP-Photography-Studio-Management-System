using Microsoft.EntityFrameworkCore;
using SNAP.domain.entities;

namespace SNAP.infrastructure.Data
{
    public class MasterErpDbContext : DbContext
    {
        public MasterErpDbContext()
        {
        }

        public MasterErpDbContext(DbContextOptions<MasterErpDbContext> options)
            : base(options)
        {
        }

        public DbSet<Company> Companies => Set<Company>();
        public DbSet<CompanyDatabase> CompanyDatabases => Set<CompanyDatabase>();
        public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Design-time only fallback (e.g. `dotnet ef migrations add`). This is never used at
                // application runtime, since real usage always supplies explicit DbContextOptions.
                optionsBuilder.UseSqlServer(SNAP.infrastructure.config.ConnectionStrings.MasterErp);
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Company>(entity =>
            {
                entity.HasKey(x => x.CompanyId);
                entity.Property(x => x.CompanyCode).IsRequired().HasMaxLength(50);
                entity.Property(x => x.CompanyName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.PlanTier).IsRequired().HasDefaultValue(SNAP.domain.enums.PlanTier.Micro);
                entity.Property(x => x.IsActive).HasDefaultValue(true);
                entity.HasIndex(x => x.CompanyCode).IsUnique();
            });

            builder.Entity<CompanyDatabase>(entity =>
            {
                entity.HasKey(x => x.CompanyDatabaseId);
                entity.Property(x => x.ServerName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.DatabaseName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.CredentialKey).HasMaxLength(100);
                entity.Property(x => x.IsActive).HasDefaultValue(true);
                entity.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PlatformUser>(entity =>
            {
                entity.HasKey(x => x.PlatformUserId);
                entity.Property(x => x.Username).IsRequired().HasMaxLength(100);
                entity.Property(x => x.PasswordHash).IsRequired().HasMaxLength(512);
                entity.Property(x => x.Role).IsRequired();
                entity.Property(x => x.IsActive).HasDefaultValue(true);
                entity.HasIndex(x => x.Username).IsUnique();
            });
        }
    }
}
using Microsoft.EntityFrameworkCore;
using SNAP.domain.entities;

namespace SNAP.infrastructure.Data
{
    public class TenantErpDbContext : DbContext
    {
        public TenantErpDbContext()
        {
        }

        public TenantErpDbContext(DbContextOptions<TenantErpDbContext> options)
            : base(options)
        {
        }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<PhotographyPackage> PhotographyPackages => Set<PhotographyPackage>();
        public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<SyncOutboxEntry> SyncOutboxEntries => Set<SyncOutboxEntry>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Inventory> InventoryItems => Set<Inventory>();
        public DbSet<Equipment> Equipment => Set<Equipment>();
        public DbSet<Expense> Expenses => Set<Expense>();
        public DbSet<SalaryPayment> SalaryPayments => Set<SalaryPayment>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<ProductSale> ProductSales => Set<ProductSale>();
        public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
        public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
        public DbSet<OwnerTransaction> OwnerTransactions => Set<OwnerTransaction>();
        public DbSet<Branch> Branches => Set<Branch>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Design-time only fallback (e.g. `dotnet ef migrations add`). This is never used at
                // application runtime, since real usage always supplies explicit DbContextOptions.
                optionsBuilder.UseSqlServer(SNAP.infrastructure.config.ConnectionStrings.TenantErpDefault);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(x => x.CustomerId);
                entity.Property(x => x.FullName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.ContactNumber).IsRequired().HasMaxLength(50);
                entity.Property(x => x.EmailAddress).HasMaxLength(200);
                entity.Property(x => x.Address).HasMaxLength(500);
                entity.Property(x => x.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<PhotographyPackage>(entity =>
            {
                entity.HasKey(x => x.PackageId);
                entity.Property(x => x.PackageName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Description).HasMaxLength(500);
                entity.Property(x => x.Price).HasColumnType("decimal(18,2)");
                entity.Property(x => x.DurationHours).HasColumnType("decimal(18,2)");
                entity.Property(x => x.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<TenantUser>(entity =>
            {
                entity.HasKey(x => x.UserId);
                entity.Property(x => x.FullName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Username).IsRequired().HasMaxLength(100);
                entity.Property(x => x.PasswordHash).IsRequired().HasMaxLength(512);
                entity.Property(x => x.Role).IsRequired();
                entity.Property(x => x.Status).IsRequired().HasDefaultValue(SNAP.domain.enums.UserStatus.Active);
                entity.Property(x => x.MonthlySalary).HasColumnType("decimal(18,2)");
                entity.HasIndex(x => x.Username).IsUnique();
            });

            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasKey(x => x.BookingId);
                entity.Property(x => x.PackageNameSnapshot).IsRequired().HasMaxLength(200);
                entity.Property(x => x.PriceSnapshot).HasColumnType("decimal(18,2)");
                entity.Property(x => x.DurationSnapshot).HasColumnType("decimal(18,2)");
                entity.Property(x => x.Location).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Status).IsRequired();
                entity.HasOne(x => x.Customer)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Package)
                    .WithMany()
                    .HasForeignKey(x => x.PackageId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.AssignedPhotographer)
                    .WithMany()
                    .HasForeignKey(x => x.AssignedPhotographerUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(x => x.PaymentId);
                entity.Property(x => x.AmountPaid).HasColumnType("decimal(18,2)");
                entity.Property(x => x.PaymentMethod).IsRequired();
                entity.Property(x => x.ReferenceNumber).HasMaxLength(200);
                entity.Property(x => x.IsDeleted).HasDefaultValue(false);
                entity.HasIndex(x => x.BookingId);
                entity.HasOne(x => x.Booking)
                    .WithMany()
                    .HasForeignKey(x => x.BookingId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SyncOutboxEntry>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.EntityName).IsRequired().HasMaxLength(100);
                entity.HasIndex(x => x.SyncedAt);
            });

            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.HasKey(x => x.SupplierId);
                entity.Property(x => x.SupplierName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.ContactPerson).HasMaxLength(200);
                entity.Property(x => x.ContactNumber).HasMaxLength(50);
                entity.Property(x => x.Notes).HasMaxLength(500);
                entity.Property(x => x.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<Inventory>(entity =>
            {
                entity.HasKey(x => x.InventoryId);
                entity.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Category).HasMaxLength(100);
                entity.Property(x => x.QuantityOnHand).HasColumnType("decimal(18,2)");
                entity.Property(x => x.ReorderLevel).HasColumnType("decimal(18,2)");
                entity.Property(x => x.SellingPrice).HasColumnType("decimal(18,2)");
                entity.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
                entity.Property(x => x.IsActive).HasDefaultValue(true);
                entity.HasOne(x => x.Supplier)
                    .WithMany()
                    .HasForeignKey(x => x.SupplierId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ProductSale>(entity =>
            {
                entity.HasKey(x => x.ProductSaleId);
                entity.Property(x => x.Quantity).HasColumnType("decimal(18,2)");
                entity.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(x => x.PaymentMethod).IsRequired();
                entity.Property(x => x.IsDeleted).HasDefaultValue(false);
                entity.HasIndex(x => x.BookingId);
                entity.HasOne(x => x.Inventory)
                    .WithMany()
                    .HasForeignKey(x => x.InventoryId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Customer)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.Booking)
                    .WithMany()
                    .HasForeignKey(x => x.BookingId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Equipment>(entity =>
            {
                entity.HasKey(x => x.EquipmentId);
                entity.Property(x => x.EquipmentName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Category).IsRequired().HasDefaultValue(SNAP.domain.enums.EquipmentCategory.PhotographyGear);
                entity.Property(x => x.AcquisitionCost).HasColumnType("decimal(18,2)");
                entity.Property(x => x.Status).IsRequired();
                entity.Property(x => x.Notes).HasMaxLength(500);
                entity.Property(x => x.IsActive).HasDefaultValue(true);
                entity.HasOne(x => x.AssignedToUser)
                    .WithMany()
                    .HasForeignKey(x => x.AssignedToUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasKey(x => x.ExpenseId);
                entity.Property(x => x.Category).IsRequired();
                entity.Property(x => x.Description).HasMaxLength(500);
                entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
                entity.Property(x => x.IsPaid).HasDefaultValue(true);
                entity.Property(x => x.IsDeleted).HasDefaultValue(false);
                entity.HasOne(x => x.Supplier)
                    .WithMany()
                    .HasForeignKey(x => x.SupplierId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.Equipment)
                    .WithMany()
                    .HasForeignKey(x => x.EquipmentId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.PurchaseOrder)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SalaryPayment>(entity =>
            {
                entity.HasKey(x => x.SalaryPaymentId);
                entity.Property(x => x.GrossAmount).HasColumnType("decimal(18,2)");
                entity.Property(x => x.SssDeduction).HasColumnType("decimal(18,2)");
                entity.Property(x => x.PhilHealthDeduction).HasColumnType("decimal(18,2)");
                entity.Property(x => x.PagIbigDeduction).HasColumnType("decimal(18,2)");
                entity.Property(x => x.WithholdingTax).HasColumnType("decimal(18,2)");
                entity.Property(x => x.NetAmount).HasColumnType("decimal(18,2)");
                entity.Property(x => x.Notes).HasMaxLength(500);
                entity.Property(x => x.IsDeleted).HasDefaultValue(false);
                entity.HasOne(x => x.TenantUser)
                    .WithMany()
                    .HasForeignKey(x => x.TenantUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PurchaseOrder>(entity =>
            {
                entity.HasKey(x => x.PurchaseOrderId);
                entity.Property(x => x.Status).IsRequired();
                entity.Property(x => x.Notes).HasMaxLength(500);
                entity.HasOne(x => x.Supplier)
                    .WithMany()
                    .HasForeignKey(x => x.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
                // TenantUsers are only ever soft-deleted (Status flips to Inactive/Suspended, see
                // UserManagementForm) - never hard-deleted - so Restrict has no practical behavior
                // difference from SetNull here. Two SetNull cascade paths from TenantUsers into the
                // same PurchaseOrders table is what SQL Server actually rejects at DDL time
                // ("may cause cycles or multiple cascade paths"), so Restrict is required, not just
                // a stylistic choice.
                entity.HasOne(x => x.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.ReceivedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.ReceivedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PurchaseOrderLine>(entity =>
            {
                entity.HasKey(x => x.PurchaseOrderLineId);
                entity.Property(x => x.QuantityOrdered).HasColumnType("decimal(18,2)");
                entity.Property(x => x.QuantityReceived).HasColumnType("decimal(18,2)");
                entity.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
                entity.HasOne(x => x.PurchaseOrder)
                    .WithMany(x => x.Lines)
                    .HasForeignKey(x => x.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Inventory)
                    .WithMany()
                    .HasForeignKey(x => x.InventoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OwnerTransaction>(entity =>
            {
                entity.HasKey(x => x.OwnerTransactionId);
                entity.Property(x => x.Type).IsRequired();
                entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
                entity.Property(x => x.Notes).HasMaxLength(500);
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(x => x.AuditLogId);
                entity.Property(x => x.ActorName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.EntityName).IsRequired().HasMaxLength(100);
                entity.Property(x => x.Summary).HasMaxLength(500);
                entity.HasIndex(x => x.Timestamp);
                entity.HasIndex(x => x.EntityName);
                entity.HasOne(x => x.TenantUser)
                    .WithMany()
                    .HasForeignKey(x => x.TenantUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Branch>(entity =>
            {
                entity.HasKey(x => x.BranchId);
                entity.Property(x => x.BranchName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Address).HasMaxLength(500);
                entity.Property(x => x.ContactNumber).HasMaxLength(50);
                entity.Property(x => x.IsActive).HasDefaultValue(true);
            });

            // Every branch relationship is Restrict, never Cascade: a branch that still holds
            // bookings, stock or expenses must not be deletable out from under them (branches are
            // retired via IsActive instead). Restrict also keeps SQL Server from seeing multiple
            // cascade paths into the same table, which is what broke an earlier migration here.
            modelBuilder.Entity<Booking>()
                .HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Inventory>()
                .HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Equipment>()
                .HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Expense>()
                .HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PurchaseOrder>()
                .HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ProductSale>()
                .HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TenantUser>()
                .HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AuditLog>()
                .HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        }

    }
}
using BlockingApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlockingApi.Data.Context
{
    public class BlockingApiDbContext : DbContext
    {
        public BlockingApiDbContext(DbContextOptions<BlockingApiDbContext> options) : base(options) { }
        public BlockingApiDbContext() { }

        // DbSets
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<UserRolePermission> UserRolePermissions => Set<UserRolePermission>();

        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<BlockRecord> BlockRecords => Set<BlockRecord>();

        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<TransactionFlow> TransactionFlows => Set<TransactionFlow>();


        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<Area> Areas => Set<Area>();

        public DbSet<Reason> Reasons => Set<Reason>();
        public DbSet<Source> Sources => Set<Source>();

        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Settings> Settings => Set<Settings>();
        public DbSet<Document> Documents => Set<Document>();

        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<UserActivity> UserActivities => Set<UserActivity>();


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=DESKTOP-Q6SN24Q,1433;Database=BlockingDb;User Id=ccadmin;Password=ccadmin;Trusted_Connection=False;MultipleActiveResultSets=true;TrustServerCertificate=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Unique Constraints
            builder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique()
                .HasDatabaseName("Unique_Email");

            builder.Entity<Customer>()
                .HasIndex(c => c.NationalId)
                .IsUnique()
                .HasDatabaseName("Unique_NationalId");

            builder.Entity<Settings>()
                .HasIndex(s => s.Id)
                .IsUnique();

            // User - Role Relationship
            builder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // User - Branch Relationship (Employees)
            builder.Entity<User>()
                .HasOne(u => u.Branch)
                .WithMany(b => b.Employees)
                .HasForeignKey(u => u.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            // Branch - Manager Relationship
            // Ensure the property is correctly mapped by using the existing property name.
            builder.Entity<Branch>()
                .HasOne(b => b.BranchManager)
                .WithMany() // If you have a navigation in User for ManagedBranches, use: .WithMany(u => u.ManagedBranches)
                .HasForeignKey(b => b.BranchManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Area - HeadOfSection Relationship
            builder.Entity<Area>()
                .HasOne(a => a.HeadOfSection)
                .WithMany()
                .HasForeignKey(a => a.HeadOfSectionId)
                .OnDelete(DeleteBehavior.SetNull);

            // BlockRecord - Customer Relationship
            builder.Entity<BlockRecord>()
                .HasOne(b => b.Customer)
                .WithMany(c => c.BlockRecords)
                .HasForeignKey(b => b.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // BlockRecord - User Relationship (BlockedBy)
            builder.Entity<BlockRecord>()
                .HasOne(b => b.BlockedBy)
                .WithMany()
                .HasForeignKey(b => b.BlockedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // BlockRecord - Reason Relationship
            builder.Entity<BlockRecord>()
                .HasOne(b => b.Reason)
                .WithMany(r => r.BlockRecords)
                .HasForeignKey(b => b.ReasonId)
                .OnDelete(DeleteBehavior.Restrict);

            // BlockRecord - Source Relationship
            builder.Entity<BlockRecord>()
                .HasOne(b => b.Source)
                .WithMany(s => s.BlockRecords)
                .HasForeignKey(b => b.SourceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Transaction - set precision for Amount to avoid silent truncation.
            builder.Entity<Transaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);

            // Notification - Configure foreign keys with NoAction to avoid cascade cycles.
            builder.Entity<Notification>()
                .HasOne(n => n.FromUser)
                .WithMany()
                .HasForeignKey(n => n.FromUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Notification>()
                .HasOne(n => n.ToUser)
                .WithMany()
                .HasForeignKey(n => n.ToUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // AuditLog - User Relationship
            builder.Entity<AuditLog>()
                .HasOne(al => al.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // RolePermission - Role Relationship
            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // RolePermission - Permission Relationship
            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // UserRolePermission - Configure with navigation to avoid shadow keys.
            builder.Entity<UserRolePermission>()
                .HasOne(urp => urp.User)
                .WithMany(u => u.UserRolePermissions)
                .HasForeignKey(urp => urp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserRolePermission>()
                .HasOne(urp => urp.Role)
                .WithMany(r => r.UserRolePermissions)
                .HasForeignKey(urp => urp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserRolePermission>()
                .HasOne(urp => urp.Permission)
                .WithMany(p => p.UserRolePermissions)
                .HasForeignKey(urp => urp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Transaction - InitiatorUser Relationship
            builder.Entity<Transaction>()
                .HasOne(t => t.InitiatorUser)
                .WithMany()
                .HasForeignKey(t => t.InitiatorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Transaction - CurrentPartyUser Relationship
            builder.Entity<Transaction>()
                .HasOne(t => t.CurrentPartyUser)
                .WithMany()
                .HasForeignKey(t => t.CurrentPartyUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Transaction - ApprovedByUser Relationship
            builder.Entity<Transaction>()
                .HasOne(t => t.ApprovedBy)
                .WithMany()
                .HasForeignKey(t => t.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);


            // Document: configure conversion for DocumentType.
            builder.Entity<Document>()
                .Property(d => d.DocumentType)
                .HasConversion(v => v.ToLower(), v => v);
        }

        // Override SaveChangesAsync to track CreatedAt and UpdatedAt timestamps
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is Auditable && (e.State == EntityState.Added || e.State == EntityState.Modified))
                .Select(e => e.Entity as Auditable);

            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    entry.UpdatedAt = DateTime.UtcNow;
                    if (entry.CreatedAt == default)
                    {
                        entry.CreatedAt = DateTime.UtcNow;
                    }
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}

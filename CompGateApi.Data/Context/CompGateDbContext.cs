using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompGateApi.Data.Context
{
       public class CompGateApiDbContext : DbContext
       {
              public CompGateApiDbContext(DbContextOptions<CompGateApiDbContext> options)
                  : base(options) { }

              // ── CORE ───────────────────────────────────────────────────────────────
              public DbSet<User> Users => Set<User>();
              public DbSet<Role> Roles => Set<Role>();
              public DbSet<Permission> Permissions => Set<Permission>();
              public DbSet<UserRolePermission> UserRolePermissions => Set<UserRolePermission>();
              public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
              public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
              public DbSet<Settings> Settings => Set<Settings>();

              // ── BANK ACCOUNTS ──────────────────────────────────────────────────────
              public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
              public DbSet<Currency> Currencies => Set<Currency>();

              // ── SERVICE PACKAGES & LIMITS ─────────────────────────────────────────
              public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
              public DbSet<ServicePackageDetail> ServicePackageDetails => Set<ServicePackageDetail>();
              public DbSet<TransactionCategory> TransactionCategories => Set<TransactionCategory>();
              public DbSet<TransferLimit> TransferLimits => Set<TransferLimit>();

              // ── TRANSFER REQUESTS ──────────────────────────────────────────────────
              public DbSet<TransferRequest> TransferRequests => Set<TransferRequest>();

              // ── EXISTING “REQUEST” ENTITIES ────────────────────────────────────────
              public DbSet<CblRequest> CblRequests => Set<CblRequest>();
              public DbSet<CheckBookRequest> CheckBookRequests => Set<CheckBookRequest>();
              public DbSet<CheckRequest> CheckRequests => Set<CheckRequest>();
              public DbSet<CheckRequestLineItem> CheckRequestLineItems => Set<CheckRequestLineItem>();
              public DbSet<RtgsRequest> RtgsRequests => Set<RtgsRequest>();

              public DbSet<VisaRequest> VisaRequests => Set<VisaRequest>();

              public DbSet<ForeignTransfer> ForeignTransferRequests => Set<ForeignTransfer>();

              protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
              {
                     if (!optionsBuilder.IsConfigured)
                     {
                            // adjust your connection string as needed
                            optionsBuilder.UseSqlServer(
                                "Data Source=tcp:10.3.3.11;Database=CompGateDb;User Id=ccadmin;Password=ccadmin;Trusted_Connection=False;MultipleActiveResultSets=true;TrustServerCertificate=True;");
                     }
              }

              protected override void OnModelCreating(ModelBuilder builder)
              {
                     base.OnModelCreating(builder);

                     // ── USER & ROLE ─────────────────────────────────────────────────────
                     builder.Entity<User>()
                            .HasIndex(u => u.Email)
                            .IsUnique()
                            .HasDatabaseName("Unique_Email");

                     builder.Entity<User>()
                            .HasOne(u => u.Role)
                            .WithMany(r => r.Users)
                            .HasForeignKey(u => u.RoleId)
                            .OnDelete(DeleteBehavior.Restrict);

                     builder.Entity<User>()
                            .HasOne(u => u.ServicePackage)
                            .WithMany(p => p.Users)
                            .HasForeignKey(u => u.ServicePackageId)
                            .OnDelete(DeleteBehavior.Restrict);

                     // ── AUDIT LOG ─────────────────────────────────────────────────────────
                     builder.Entity<AuditLog>()
                            .HasOne(al => al.User)
                            .WithMany(u => u.AuditLogs)
                            .HasForeignKey(al => al.UserId)
                            .OnDelete(DeleteBehavior.Restrict);

                     // ── ROLES & PERMISSIONS ───────────────────────────────────────────────
                     builder.Entity<RolePermission>()
                            .HasOne(rp => rp.Role)
                            .WithMany(r => r.RolePermissions)
                            .HasForeignKey(rp => rp.RoleId)
                            .OnDelete(DeleteBehavior.Cascade);

                     builder.Entity<RolePermission>()
                            .HasOne(rp => rp.Permission)
                            .WithMany(p => p.RolePermissions)
                            .HasForeignKey(rp => rp.PermissionId)
                            .OnDelete(DeleteBehavior.Cascade);

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

                     // ── BANK ACCOUNTS & CURRENCY ──────────────────────────────────────────
                     builder.Entity<BankAccount>()
                            .HasOne(b => b.User)
                            .WithMany(u => u.BankAccounts)
                            .HasForeignKey(b => b.UserId)
                            .OnDelete(DeleteBehavior.Cascade);

                     builder.Entity<BankAccount>()
                            .HasOne(b => b.Currency)
                            .WithMany()
                            .HasForeignKey(b => b.CurrencyId)
                            .OnDelete(DeleteBehavior.Restrict);

                     builder.Entity<BankAccount>()
                            .Property(b => b.Balance)
                            .HasPrecision(18, 2);

                     builder.Entity<Currency>()
                            .Property(c => c.Rate)
                            .HasPrecision(18, 6);

                     // ── SERVICE PACKAGE DETAILS ────────────────────────────────────────────
                     builder.Entity<ServicePackageDetail>()
                            .HasOne(d => d.ServicePackage)
                            .WithMany(p => p.ServicePackageDetails)
                            .HasForeignKey(d => d.ServicePackageId)
                            .OnDelete(DeleteBehavior.Cascade);

                     builder.Entity<ServicePackageDetail>()
                            .HasOne(d => d.TransactionCategory)
                            .WithMany(c => c.ServicePackageDetails)
                            .HasForeignKey(d => d.TransactionCategoryId)
                            .OnDelete(DeleteBehavior.Cascade);

                     builder.Entity<ServicePackageDetail>()
                            .Property(d => d.CommissionPct)
                            .HasPrecision(18, 4);

                     builder.Entity<ServicePackageDetail>()
                            .Property(d => d.FeeFixed)
                            .HasPrecision(18, 4);

                     // ── TRANSFER LIMITS ─────────────────────────────────────────────────────
                     builder.Entity<TransferLimit>()
                            .HasOne(l => l.ServicePackage)
                            .WithMany(p => p.TransferLimits)
                            .HasForeignKey(l => l.ServicePackageId)
                            .OnDelete(DeleteBehavior.Cascade);

                     builder.Entity<TransferLimit>()
                            .HasOne(l => l.TransactionCategory)
                            .WithMany(c => c.TransferLimits)
                            .HasForeignKey(l => l.TransactionCategoryId)
                            .OnDelete(DeleteBehavior.Cascade);

                     builder.Entity<TransferLimit>()
                            .HasOne(l => l.Currency)
                            .WithMany()
                            .HasForeignKey(l => l.CurrencyId)
                            .OnDelete(DeleteBehavior.Restrict);

                     builder.Entity<TransferLimit>()
                            .Property(l => l.MinAmount)
                            .HasPrecision(18, 4);

                     builder.Entity<TransferLimit>()
                            .Property(l => l.MaxAmount)
                            .HasPrecision(18, 4);

                     // ── TRANSFER REQUESTS ──────────────────────────────────────────────────
                     builder.Entity<TransferRequest>()
                            .HasOne(tr => tr.User)
                            .WithMany(u => u.TransferRequests)
                            .HasForeignKey(tr => tr.UserId)
                            .OnDelete(DeleteBehavior.Cascade);

                     builder.Entity<TransferRequest>()
                            .HasOne(tr => tr.Currency)
                            .WithMany()
                            .HasForeignKey(tr => tr.CurrencyId)
                            .OnDelete(DeleteBehavior.Restrict);

                     builder.Entity<TransferRequest>()
                            .HasOne(tr => tr.ServicePackage)
                            .WithMany()    // no navigation back on ServicePackage
                            .HasForeignKey(tr => tr.ServicePackageId)
                            .OnDelete(DeleteBehavior.Restrict);

                     builder.Entity<TransferRequest>()
                            .HasOne(tr => tr.TransactionCategory)
                            .WithMany()    // no navigation back on TransactionCategory
                            .HasForeignKey(tr => tr.TransactionCategoryId)
                            .OnDelete(DeleteBehavior.Restrict);

                     builder.Entity<TransferRequest>()
                            .Property(tr => tr.Amount)
                            .HasPrecision(18, 4);

                     // ── EXISTING “REQUEST” ENTITIES ────────────────────────────────────────
                     builder.Entity<CblRequest>()
                            .HasOne(r => r.User)
                            .WithMany()
                            .HasForeignKey(r => r.UserId)
                            .OnDelete(DeleteBehavior.Cascade);

                     builder.Entity<CheckBookRequest>()
                            .HasOne(r => r.User)
                            .WithMany()
                            .HasForeignKey(r => r.UserId)
                            .OnDelete(DeleteBehavior.Cascade);

                     builder.Entity<CheckRequest>()
                            .HasOne(r => r.User)
                            .WithMany()
                            .HasForeignKey(r => r.UserId)
                            .OnDelete(DeleteBehavior.Cascade);

                     builder.Entity<CheckRequestLineItem>()
                            .HasOne(li => li.CheckRequest)
                            .WithMany(r => r.LineItems)
                            .HasForeignKey(li => li.CheckRequestId)
                            .OnDelete(DeleteBehavior.Cascade);

                     builder.Entity<RtgsRequest>()
                            .HasOne(r => r.User)
                            .WithMany()
                            .HasForeignKey(r => r.UserId)
                            .OnDelete(DeleteBehavior.Cascade);

                     builder.Entity<VisaRequest>(b =>
                     {
                            b.ToTable("VisaRequests");
                            b.HasKey(x => x.Id);

                            b.Property(x => x.Branch)
                            .HasMaxLength(100);

                            b.Property(x => x.AccountHolderName)
                            .HasMaxLength(150);


                            b.HasOne(x => x.User)
                            .WithMany(u => u.VisaRequests)
                            .HasForeignKey(x => x.UserId)
                            .OnDelete(DeleteBehavior.Cascade);
                     });

                     builder.Entity<ForeignTransfer>(b =>
                     {
                            b.ToTable("ForeignTransfers");
                            b.HasKey(x => x.Id);

                            b.Property(x => x.ToBank)
                             .HasMaxLength(150);

                            b.Property(x => x.BeneficiaryName)
                            .HasMaxLength(150);

                            b.HasOne(x => x.User)
                            .WithMany(u => u.ForeignTransferRequests)
                            .HasForeignKey(x => x.UserId)
                            .OnDelete(DeleteBehavior.Cascade);
                     });
              }

              public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
              {
                     var now = DateTimeOffset.UtcNow;
                     foreach (var entry in ChangeTracker.Entries()
                                                   .Where(e => e.Entity is Auditable &&
                                                              (e.State == EntityState.Added || e.State == EntityState.Modified)))
                     {
                            var aud = (Auditable)entry.Entity;
                            if (entry.State == EntityState.Added) aud.CreatedAt = now;
                            aud.UpdatedAt = now;
                     }
                     return base.SaveChangesAsync(cancellationToken);
              }
       }
}

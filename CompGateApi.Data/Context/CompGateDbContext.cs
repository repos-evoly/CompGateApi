// CompGateApi.Data.Context/CompGateApiDbContext.cs
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

              // ── MULTI-TENANT ────────────────────────────────────────────────────────
              public DbSet<Company> Companies => Set<Company>();

              // ── BANK ACCOUNTS ──────────────────────────────────────────────────────
              public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
              public DbSet<Currency> Currencies => Set<Currency>();

              // ── SERVICE PACKAGES & LIMITS ─────────────────────────────────────────
              public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
              public DbSet<ServicePackageDetail> ServicePackageDetails => Set<ServicePackageDetail>();
              public DbSet<TransactionCategory> TransactionCategories => Set<TransactionCategory>();

              // ── REQUESTS ───────────────────────────────────────────────────────────
              public DbSet<TransferRequest> TransferRequests => Set<TransferRequest>();
              public DbSet<VisaRequest> VisaRequests => Set<VisaRequest>();
              public DbSet<ForeignTransfer> ForeignTransferRequests => Set<ForeignTransfer>();
              public DbSet<CheckRequest> CheckRequests => Set<CheckRequest>();
              public DbSet<CheckBookRequest> CheckBookRequests => Set<CheckBookRequest>();
              public DbSet<CheckRequestLineItem> CheckRequestLineItems => Set<CheckRequestLineItem>();
              public DbSet<CblRequest> CblRequests => Set<CblRequest>();
              public DbSet<RtgsRequest> RtgsRequests => Set<RtgsRequest>();

              public DbSet<CreditFacilitiesOrLetterOfGuaranteeRequest> CreditFacilitiesOrLetterOfGuaranteeRequests => Set<CreditFacilitiesOrLetterOfGuaranteeRequest>();

              public DbSet<CertifiedBankStatementRequest> CertifiedBankStatementRequests => Set<CertifiedBankStatementRequest>();
              public DbSet<Attachment> Attachments => Set<Attachment>();

              public DbSet<EconomicSector> EconomicSectors => Set<EconomicSector>();

              public DbSet<Representative> Representatives => Set<Representative>();

              public DbSet<FormStatus> FormStatuses => Set<FormStatus>();




              protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
              {
                     if (!optionsBuilder.IsConfigured)
                     {
                            optionsBuilder.UseSqlServer(
                                "Data Source=tcp:10.3.3.11;Database=CompGateDb;User Id=ccadmin;Password=ccadmin;Trusted_Connection=False;MultipleActiveResultSets=true;TrustServerCertificate=True;");
                     }
              }

              protected override void OnModelCreating(ModelBuilder builder)
              {
                     base.OnModelCreating(builder);

                     // ── COMPANY ────────────────────────────────────────────────────────────
                     builder.Entity<Company>(b =>
            {
                   b.ToTable("Companies");
                   b.HasKey(c => c.Id);
                   b.Property(c => c.Code).HasMaxLength(6).IsRequired();
                   b.Property(c => c.Name).HasMaxLength(150).IsRequired();
                   b.Property(c => c.IsActive);

                   b.HasMany(c => c.Users)
                    .WithOne(u => u.Company)
                    .HasForeignKey(u => u.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                   b.HasOne(c => c.ServicePackage)
                    .WithMany(p => p.Companies)
                    .HasForeignKey(c => c.ServicePackageId)
                    .OnDelete(DeleteBehavior.Restrict);

                   b.HasMany(c => c.TransferRequests)
                    .WithOne(r => r.Company)
                    .HasForeignKey(r => r.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                   b.HasMany(c => c.VisaRequests)
                    .WithOne(r => r.Company)
                    .HasForeignKey(r => r.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                   b.HasMany(c => c.ForeignTransfers)
                    .WithOne(r => r.Company)
                    .HasForeignKey(r => r.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                   b.HasMany(c => c.CheckRequests)
                    .WithOne(r => r.Company)
                    .HasForeignKey(r => r.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                   b.HasMany(c => c.CheckBookRequests)
                    .WithOne(r => r.Company)
                    .HasForeignKey(r => r.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                   b.HasMany(c => c.CblRequests)
                    .WithOne(r => r.Company)
                    .HasForeignKey(r => r.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                   b.HasMany(c => c.RtgsRequests)
                    .WithOne(r => r.Company)
                    .HasForeignKey(r => r.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                   b.HasMany(c => c.CreditFacilitiesRequests)
                    .WithOne(r => r.Company)
                    .HasForeignKey(r => r.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                   b.HasMany(c => c.CertifiedBankStatementRequests)
                    .WithOne(r => r.Company)
                    .HasForeignKey(r => r.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                   b.HasMany(c => c.Representatives)
                     .WithOne(r => r.Company)
                     .HasForeignKey(r => r.CompanyId)
                     .OnDelete(DeleteBehavior.Cascade);
            });

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
                            .HasOne(u => u.Company)
                            .WithMany(c => c.Users)
                            .HasForeignKey(u => u.CompanyId)
                            .OnDelete(DeleteBehavior.Restrict)
                            .IsRequired(false);

                     // ── ATTACHMENTS ─────────────────────────────────────────────────────
                     builder.Entity<Attachment>(b =>
                     {
                            b.ToTable("Attachments");
                            b.HasKey(a => a.Id);

                            b.HasOne(a => a.Company)
                    .WithMany(c => c.Attachments)
                    .HasForeignKey(a => a.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
                     });

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

                     // ── SERVICE PACKAGE & DETAILS ────────────────────────────────────────────
                     builder.Entity<ServicePackage>(b =>
                     {
                            b.ToTable("ServicePackages");
                            b.HasKey(p => p.Id);
                            b.Property(p => p.Name).HasMaxLength(100).IsRequired();
                            b.Property(p => p.Description);
                            b.Property(p => p.DailyLimit).HasPrecision(18, 2).IsRequired();
                            b.Property(p => p.MonthlyLimit).HasPrecision(18, 2).IsRequired();

                            b.HasMany(p => p.Details)
                    .WithOne(d => d.ServicePackage)
                    .HasForeignKey(d => d.ServicePackageId)
                    .OnDelete(DeleteBehavior.Cascade);
                     });

                     builder.Entity<TransactionCategory>(b =>
                     {
                            b.ToTable("TransactionCategories");
                            b.HasKey(c => c.Id);
                            b.Property(c => c.Name).HasMaxLength(100).IsRequired();


                            b.HasMany(c => c.PackageDetails)
                    .WithOne(d => d.TransactionCategory)
                    .HasForeignKey(d => d.TransactionCategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
                     });

                     builder.Entity<ServicePackageDetail>(b =>
                     {
                            b.ToTable("ServicePackageDetails");
                            b.HasKey(d => d.Id);

                            b.Property(d => d.IsEnabledForPackage)
                            .IsRequired();

                            b.Property(d => d.B2BTransactionLimit)
                            .HasPrecision(18, 4).IsRequired();
                            b.Property(d => d.B2CTransactionLimit)
                            .HasPrecision(18, 4).IsRequired();

                            b.Property(d => d.B2BFixedFee)
                            .HasPrecision(18, 4).IsRequired();
                            b.Property(d => d.B2CFixedFee)
                            .HasPrecision(18, 4).IsRequired();

                            b.Property(d => d.B2BMinPercentage)
                            .HasPrecision(18, 4).IsRequired();
                            b.Property(d => d.B2CMinPercentage)
                            .HasPrecision(18, 4).IsRequired();

                            b.HasOne(d => d.ServicePackage)
                            .WithMany(p => p.Details)
                            .HasForeignKey(d => d.ServicePackageId)
                            .OnDelete(DeleteBehavior.Cascade);

                            b.HasOne(d => d.TransactionCategory)
                            .WithMany(c => c.PackageDetails)
                            .HasForeignKey(d => d.TransactionCategoryId)
                            .OnDelete(DeleteBehavior.Cascade);
                     });

                     // ── ECONOMIC SECTORS ──────────────────────────────────────────────────────
                     builder.Entity<EconomicSector>(b =>
                     {
                            b.ToTable("EconomicSectors");
                            b.HasKey(es => es.Id);
                            b.Property(es => es.Name).HasMaxLength(100).IsRequired();
                            b.Property(es => es.Description).HasMaxLength(500);
                     });



                     // ── SETTINGS ─────────────────────────────────────────────────────
                     builder.Entity<Settings>(b =>
                     {
                            b.ToTable("Settings");
                            b.HasKey(s => s.Id);
                            b.Property(s => s.GlobalLimit).HasPrecision(18, 2).IsRequired();
                     });

                     // ── TRANSFER REQUESTS ──────────────────────────────────────────────────
                     builder.Entity<TransferRequest>(b =>
                     {
                            b.HasOne(tr => tr.User)
                    .WithMany(u => u.TransferRequests)
                    .HasForeignKey(tr => tr.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                            b.HasOne(tr => tr.Company)
                    .WithMany(c => c.TransferRequests)
                    .HasForeignKey(tr => tr.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                            b.HasOne(tr => tr.Currency)
                    .WithMany()
                    .HasForeignKey(tr => tr.CurrencyId)
                    .OnDelete(DeleteBehavior.Restrict);

                            b.HasOne(tr => tr.ServicePackage)
                    .WithMany()
                    .HasForeignKey(tr => tr.ServicePackageId)
                    .OnDelete(DeleteBehavior.Restrict);

                            b.HasOne(tr => tr.TransactionCategory)
                    .WithMany()
                    .HasForeignKey(tr => tr.TransactionCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                            b.Property(tr => tr.Amount).HasPrecision(18, 4);

                            b.HasOne(tr => tr.EconomicSector)
                                     .WithMany(es => es.Transfers)
                                     .HasForeignKey(tr => tr.EconomicSectorId)
                                     .OnDelete(DeleteBehavior.Restrict); // or .SetNull if desired
                     });

                     // ── VISA REQUESTS ─────────────────────────────────────────────────────
                     builder.Entity<VisaRequest>(b =>
                     {
                            b.ToTable("VisaRequests");
                            b.HasKey(x => x.Id);

                            b.HasOne(x => x.User)
                    .WithMany(u => u.VisaRequests)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                            b.HasOne(x => x.Company)
                    .WithMany(c => c.VisaRequests)
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                            b.Property(x => x.ForeignAmount)
                    .HasPrecision(18, 4);
                            b.Property(x => x.LocalAmount)
                    .HasPrecision(18, 4);
                     });

                     // ── FOREIGN TRANSFERS ─────────────────────────────────────────────────
                     builder.Entity<ForeignTransfer>(b =>
                     {
                            b.ToTable("ForeignTransfers");
                            b.HasKey(x => x.Id);

                            b.HasOne(x => x.User)
                    .WithMany(u => u.ForeignTransferRequests)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                            b.HasOne(x => x.Company)
                    .WithMany(c => c.ForeignTransfers)
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                            b.Property(x => x.TransferAmount)
                    .HasPrecision(18, 4);
                     });

                     // ── CHECK REQUESTS ────────────────────────────────────────────────────
                     builder.Entity<CheckRequest>(b =>
                     {
                            b.ToTable("CheckRequests");
                            b.HasKey(x => x.Id);

                            b.HasOne(x => x.User)
                    .WithMany(u => u.CheckRequests)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                            b.HasOne(x => x.Representative)
                     .WithMany()                        // no navigation collection on Representative
                     .HasForeignKey(x => x.RepresentativeId)
                     .OnDelete(DeleteBehavior.Restrict);
                            b.HasOne(x => x.Company)
                    .WithMany(c => c.CheckRequests)
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
                     });
                     builder.Entity<CheckRequestLineItem>(b =>
                     {
                            b.ToTable("CheckRequestLineItems");
                            b.HasKey(x => x.Id);

                            b.HasOne(li => li.CheckRequest)
                    .WithMany(r => r.LineItems)
                    .HasForeignKey(li => li.CheckRequestId)
                    .OnDelete(DeleteBehavior.Cascade);
                     });

                     // ── CREDIT FACILITIES / LETTERS OF GUARANTEE ─────────────────────────────
                     builder.Entity<CreditFacilitiesOrLetterOfGuaranteeRequest>(b =>
                     {
                            b.ToTable("CreditFacilities");
                            b.HasKey(x => x.Id);

                            // user relationship
                            b.HasOne(x => x.User)
                            .WithMany(u => u.CreditFacilitiesRequests)
                            .HasForeignKey(x => x.UserId)
                            .OnDelete(DeleteBehavior.Cascade);

                            // company relationship
                            b.HasOne(x => x.Company)
                            .WithMany(c => c.CreditFacilitiesRequests)
                            .HasForeignKey(x => x.CompanyId)
                            .OnDelete(DeleteBehavior.Cascade);

                            // decimal precision
                            b.Property(x => x.Amount).HasPrecision(18, 4);
                     });

                     // ── CERTIFIED BANK STATEMENT REQUESTS ────────────────────────────────────
                     builder.Entity<CertifiedBankStatementRequest>(b =>
                     {
                            b.ToTable("CertifiedBankStatementRequests");
                            b.HasKey(x => x.Id);

                            // user relationship
                            b.HasOne(x => x.User)
                            .WithMany(u => u.CertifiedBankStatementRequests)
                            .HasForeignKey(x => x.UserId)
                            .OnDelete(DeleteBehavior.Cascade);

                            // company relationship
                            b.HasOne(x => x.Company)
                            .WithMany(c => c.CertifiedBankStatementRequests)
                            .HasForeignKey(x => x.CompanyId)
                            .OnDelete(DeleteBehavior.Cascade);

                            b.OwnsOne(x => x.ServiceRequests);
                            b.OwnsOne(x => x.StatementRequest);
                     });




                     // ── CHECKBOOK REQUESTS ────────────────────────────────────────────────
                     builder.Entity<CheckBookRequest>(b =>
                     {
                            b.ToTable("CheckBookRequests");
                            b.HasKey(x => x.Id);

                            b.HasOne(x => x.User)
                    .WithMany(u => u.CheckBookRequests)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                            b.HasOne(x => x.Representative)
                            .WithMany()
                            .HasForeignKey(x => x.RepresentativeId)
                            .OnDelete(DeleteBehavior.Restrict);


                            b.HasOne(x => x.Company)
                    .WithMany(c => c.CheckBookRequests)
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
                     });

                     // ── CBL REQUESTS ──────────────────────────────────────────────────────
                     builder.Entity<CblRequest>(b =>
                     {
                            b.ToTable("CblRequests");
                            b.HasKey(x => x.Id);

                            b.HasOne(x => x.User)
                    .WithMany(u => u.CblRequests)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                            b.HasOne(x => x.Company)
                    .WithMany(c => c.CblRequests)
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                            b.HasOne(x => x.Attachment)
                     .WithMany()
                     .HasForeignKey(x => x.AttachmentId)
                     .OnDelete(DeleteBehavior.Restrict);

                            b.Property(x => x.Capital)
                    .HasPrecision(18, 4);
                     });
                     builder.Entity<CblRequestOfficial>(b =>
                     {
                            b.ToTable("CblRequestOfficials");
                            b.HasKey(x => x.Id);
                            b.HasOne(x => x.CblRequest)
                    .WithMany(r => r.Officials)
                    .HasForeignKey(x => x.CblRequestId)
                    .OnDelete(DeleteBehavior.Cascade);
                     });
                     builder.Entity<CblRequestSignature>(b =>
                     {
                            b.ToTable("CblRequestSignatures");
                            b.HasKey(x => x.Id);
                            b.HasOne(x => x.CblRequest)
                    .WithMany(r => r.Signatures)
                    .HasForeignKey(x => x.CblRequestId)
                    .OnDelete(DeleteBehavior.Cascade);
                     });

                     // ── RTGS REQUESTS ──────────────────────────────────────────────────────
                     builder.Entity<RtgsRequest>(b =>
                     {
                            b.ToTable("RtgsRequests");
                            b.HasKey(x => x.Id);

                            b.HasOne(x => x.User)
                    .WithMany(u => u.RtgsRequests)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                            b.HasOne(x => x.Company)
                    .WithMany(c => c.RtgsRequests)
                    .HasForeignKey(x => x.CompanyId)
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

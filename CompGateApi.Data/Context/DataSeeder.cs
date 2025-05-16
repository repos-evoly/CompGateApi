// CompGateApi.Data.Seeding/DataSeeder.cs
using System;
using System.Collections.Generic;
using System.Linq;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CompGateApi.Data.Seeding
{
    public class DataSeeder
    {
        private readonly CompGateApiDbContext _context;
        public DataSeeder(CompGateApiDbContext context)
            => _context = context ?? throw new ArgumentNullException(nameof(context));

        public void Seed()
        {
            SeedRoles();
            SeedPermissions();
            SeedRolePermissions();       // map perms → roles
            SeedSettings();
            SeedCurrencies();
            SeedTransactionCategories();
            SeedServicePackages();
            SeedServicePackageDetails();
            SeedTransferLimits();
            SeedAdminUser();             // after roles & perms
            SeedUserRolePermissions();   // map perms → admin
            SeedSampleTransferRequests();
            SeedSampleRequests();        // CBL, CheckBook, Check, RTGS
        }

        private void SeedRoles()
        {
            if (_context.Roles.Any()) return;
            var roleNames = new[]
            {
                "SuperAdmin", "Admin", "Support", "Auditor",
                "CompanyManager", "Accountant", "Maker", "Checker", "Viewer"
            };
            _context.Roles.AddRange(roleNames.Select(n => new Role { NameLT = n }));
            _context.SaveChanges();
        }

        private void SeedPermissions()
        {
            if (_context.Permissions.Any()) return;
            var perms = new[]
            {
                new Permission { Name="ManageRoles",       Description="Create/Update/Delete Roles" },
                new Permission { Name="ManagePermissions", Description="Create/Update/Delete Permissions" },
                new Permission { Name="ViewDocuments",     Description="Can view uploaded documents" },
                new Permission { Name="ManageSettings",    Description="Can modify system settings" },
                new Permission { Name="ExecuteTransfers",  Description="Can initiate transfers" },
                new Permission { Name="ApproveTransfers",  Description="Can approve transfer requests" },
                new Permission { Name="ViewReports",       Description="Can view system reports" }
            };
            _context.Permissions.AddRange(perms);
            _context.SaveChanges();
        }

        private void SeedRolePermissions()
        {
            if (_context.RolePermissions.Any()) return;
            var allRoles = _context.Roles.ToList();
            var allPerms = _context.Permissions.ToList();

            foreach (var role in allRoles)
            {
                // SuperAdmin gets everything
                if (role.NameLT == "SuperAdmin")
                {
                    foreach (var p in allPerms)
                        _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = p.Id });
                }
                // Admin: core management perms
                else if (role.NameLT == "Admin")
                {
                    foreach (var p in allPerms.Where(x => x.Name.StartsWith("Manage") || x.Name == "ViewReports"))
                        _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = p.Id });
                }
                // Maker: ExecuteTransfers, ViewDocuments
                else if (role.NameLT == "Maker")
                {
                    var makerPerms = allPerms.Where(x => x.Name == "ExecuteTransfers" || x.Name == "ViewDocuments");
                    foreach (var p in makerPerms)
                        _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = p.Id });
                }
                // Checker: ApproveTransfers + ViewReports
                else if (role.NameLT == "Checker")
                {
                    var chkPerms = allPerms.Where(x => x.Name == "ApproveTransfers" || x.Name == "ViewReports");
                    foreach (var p in chkPerms)
                        _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = p.Id });
                }
                // Others: only view
                else
                {
                    var view = allPerms.First(p => p.Name == "ViewDocuments");
                    _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = view.Id });
                }
            }
            _context.SaveChanges();
        }

        private void SeedSettings()
        {
            if (_context.Settings.Any()) return;
            _context.Settings.Add(new Settings
            {
                TopAtmRefundLimit = 10,
                TopReasonLimit = 10
            });
            _context.SaveChanges();
        }

        private void SeedCurrencies()
        {
            if (_context.Currencies.Any()) return;
            _context.Currencies.AddRange(new[]
            {
                new Currency { Code="LYD", Rate=1m,    Description="Libyan Dinar" },
                new Currency { Code="USD", Rate=6.25m, Description="US Dollar" },
                new Currency { Code="EUR", Rate=0.85m, Description="Euro" }
            });
            _context.SaveChanges();
        }

        private void SeedTransactionCategories()
        {
            if (_context.TransactionCategories.Any()) return;
            var cats = new[]
            {
                new TransactionCategory { Name="Internal",    Description="Same-bank transfer" },
                new TransactionCategory { Name="External",    Description="Inter-bank transfer" },
                new TransactionCategory { Name="International",Description="Cross-border transfer" },
                new TransactionCategory { Name="RTGS",        Description="Real-time gross settlement" },
                new TransactionCategory { Name="CBL",         Description="Certificate of bank letter" },
                new TransactionCategory { Name="CheckBook",   Description="Check book request" }
            };
            _context.TransactionCategories.AddRange(cats);
            _context.SaveChanges();
        }

        private void SeedServicePackages()
        {
            if (_context.ServicePackages.Any()) return;
            var packages = new[]
            {
                new ServicePackage { Name="Inquiry", Description="Read-only access" },
                new ServicePackage { Name="Standard",Description="Standard transfers" },
                new ServicePackage { Name="Premium", Description="High volume & limits" }
            };
            _context.ServicePackages.AddRange(packages);
            _context.SaveChanges();
        }

        private void SeedServicePackageDetails()
        {
            if (_context.ServicePackageDetails.Any()) return;
            var pkgs = _context.ServicePackages.ToList();
            var cats = _context.TransactionCategories.ToList();

            foreach (var pkg in pkgs)
                foreach (var cat in cats)
                {
                    // Commission % tiers
                    decimal pct = pkg.Name == "Premium" ? 0.25m
                                 : pkg.Name == "Standard" ? 0.5m
                                 : 1.0m;
                    // Fixed fee tiers
                    decimal fee = pkg.Name == "Premium" ? 1.00m
                               : pkg.Name == "Standard" ? 2.50m
                               : 5.00m;

                    _context.ServicePackageDetails.Add(new ServicePackageDetail
                    {
                        ServicePackageId = pkg.Id,
                        TransactionCategoryId = cat.Id,
                        CommissionPct = pct,
                        FeeFixed = fee
                    });
                }
            _context.SaveChanges();
        }

        private void SeedTransferLimits()
        {
            if (_context.TransferLimits.Any()) return;
            var pkgs = _context.ServicePackages.ToList();
            var cats = _context.TransactionCategories.ToList();
            var currs = _context.Currencies.ToList();

            foreach (var pkg in pkgs)
                foreach (var cat in cats)
                    foreach (var cur in currs)
                    {
                        // Minimum is always 1.00
                        decimal minAmt = 1.00m;
                        // Max tiers
                        decimal maxDaily = pkg.Name == "Premium" ? 100000m : pkg.Name == "Standard" ? 50000m : 10000m;
                        decimal maxWeekly = maxDaily * 5;
                        decimal maxMonthly = maxDaily * 20;

                        _context.TransferLimits.AddRange(new[]
                        {
                    new TransferLimit
                    {
                        ServicePackageId      = pkg.Id,
                        TransactionCategoryId = cat.Id,
                        CurrencyId            = cur.Id,
                        Period                = LimitPeriod.Daily,
                        MinAmount             = minAmt,
                        MaxAmount             = maxDaily
                    },
                    new TransferLimit
                    {
                        ServicePackageId      = pkg.Id,
                        TransactionCategoryId = cat.Id,
                        CurrencyId            = cur.Id,
                        Period                = LimitPeriod.Weekly,
                        MinAmount             = minAmt,
                        MaxAmount             = maxWeekly
                    },
                    new TransferLimit
                    {
                        ServicePackageId      = pkg.Id,
                        TransactionCategoryId = cat.Id,
                        CurrencyId            = cur.Id,
                        Period                = LimitPeriod.Monthly,
                        MinAmount             = minAmt,
                        MaxAmount             = maxMonthly
                    }
                });
                    }
            _context.SaveChanges();
        }

        private void SeedAdminUser()
        {
            if (_context.Users.Any(u => u.Email == "admin@example.com")) return;
            var adminRole = _context.Roles.Single(r => r.NameLT == "Admin");
            var stdPackage = _context.ServicePackages.Single(p => p.Name == "Standard");

            var admin = new User
            {
                AuthUserId = 1,
                CompanyId = "000001",
                FirstName = "System",
                LastName = "Administrator",
                Email = "admin@example.com",
                Phone = "999999999",
                RoleId = adminRole.Id,
                ServicePackageId = stdPackage.Id
            };
            _context.Users.Add(admin);
            _context.SaveChanges();
        }

        private void SeedUserRolePermissions()
        {
            var admin = _context.Users.SingleOrDefault(u => u.Email == "admin@example.com");
            if (admin == null) return;

            // map every RolePermission → UserRolePermission
            var rolePerms = _context.RolePermissions.Where(rp => rp.RoleId == admin.RoleId).ToList();
            foreach (var rp in rolePerms)
            {
                if (!_context.UserRolePermissions.Any(urp =>
                    urp.UserId == admin.Id
                    && urp.RoleId == rp.RoleId
                    && urp.PermissionId == rp.PermissionId))
                {
                    _context.UserRolePermissions.Add(new UserRolePermission
                    {
                        UserId = admin.Id,
                        RoleId = rp.RoleId,
                        PermissionId = rp.PermissionId
                    });
                }
            }
            _context.SaveChanges();
        }

        private void SeedSampleTransferRequests()
        {
            if (_context.TransferRequests.Any()) return;
            var user = _context.Users.First();
            var cats = _context.TransactionCategories.ToList();
            var cur = _context.Currencies.First();
            var pkg = _context.ServicePackages.First(p => p.Name == "Standard");

            foreach (var cat in cats)
            {
                _context.TransferRequests.Add(new TransferRequest
                {
                    UserId = user.Id,
                    TransactionCategoryId = cat.Id,
                    FromAccount = "ACCT-0001",
                    ToAccount = "ACCT-1001",
                    Amount = 100m * (cats.IndexOf(cat) + 1),
                    CurrencyId = cur.Id,
                    ServicePackageId = pkg.Id,
                    Status = "Pending",
                    RequestedAt = DateTime.UtcNow.AddDays(-cats.IndexOf(cat))
                });
            }
            _context.SaveChanges();
        }

        private void SeedSampleRequests()
        {
            // reuse admin user
            var admin = _context.Users.Single(u => u.Email == "admin@example.com");

            if (!_context.CblRequests.Any())
            {
                _context.CblRequests.Add(new CblRequest
                {
                    UserId = admin.Id,
                    PartyName = "Example Co. LLC",
                    Capital = 250000m,
                    FoundingDate = DateTime.Today.AddYears(-4),
                    LegalForm = "LLC",
                    BranchOrAgency = "Main Branch",
                    CurrentAccount = "AC-100200",
                    AccountOpening = DateTime.Today.AddYears(-3),
                    CommercialLicense = "LIC-2025-001",
                    ValidatyLicense = DateTime.Today.AddYears(1),
                    CommercialRegistration = "REG-12345",
                    ValidatyRegister = DateTime.Today.AddMonths(6),
                    StatisticalCode = "ST-98765",
                    ValidatyCode = DateTime.Today.AddYears(2),
                    ChamberNumber = "CH-54321",
                    ValidatyChamber = DateTime.Today.AddYears(3),
                    TaxNumber = "TAX-112233",
                    Office = "HQ",
                    LegalRepresentative = "Alice Manager",
                    RepresentativeNumber = "REP-2025",
                    BirthDate = new DateTime(1980, 1, 1),
                    PassportNumber = "P-567890",
                    PassportIssuance = new DateTime(2015, 5, 1),
                    PassportExpiry = new DateTime(2025, 5, 1),
                    Mobile = "+218912345678",
                    Address = "123 Corporate Ave",
                    PackingDate = DateTime.Today,
                    SpecialistName = "Bob Specialist"
                });
                _context.SaveChanges();
            }

            if (!_context.CheckBookRequests.Any())
            {
                _context.CheckBookRequests.Add(new CheckBookRequest
                {
                    UserId = admin.Id,
                    FullName = "John Doe",
                    Address = "456 Finance St",
                    AccountNumber = "AC-334455",
                    PleaseSend = "Mail to HQ",
                    Branch = "Downtown",
                    Date = DateTime.Today,
                    BookContaining = "50 leaves"
                });
                _context.SaveChanges();
            }

            if (!_context.CheckRequests.Any())
            {
                var chk = new CheckRequest
                {
                    UserId = admin.Id,
                    Branch = "Central",
                    BranchNum = "C-100",
                    Date = DateTime.Today,
                    CustomerName = "Mary Customer",
                    CardNum = "CARD-998877",
                    AccountNum = "AC-112233",
                    Beneficiary = "Vendor Ltd."
                };
                chk.LineItems.Add(new CheckRequestLineItem { Dirham = "100", Lyd = "450" });
                chk.LineItems.Add(new CheckRequestLineItem { Dirham = "250", Lyd = "1125" });
                _context.CheckRequests.Add(chk);
                _context.SaveChanges();
            }

            if (!_context.RtgsRequests.Any())
            {
                _context.RtgsRequests.Add(new RtgsRequest
                {
                    UserId = admin.Id,
                    RefNum = DateTime.Now,
                    Date = DateTime.Now,
                    PaymentType = "Domestic",
                    AccountNo = "RTGS-556677",
                    ApplicantName = "Sam Sender",
                    Address = "789 Payments Blvd",
                    BeneficiaryName = "Receiver Co.",
                    BeneficiaryAccountNo = "BEN-445566",
                    BeneficiaryBank = "OtherBank",
                    BranchName = "North Branch",
                    Amount = "2000",
                    RemittanceInfo = "Invoice #2025",
                    Invoice = true,
                    Contract = true,
                    Claim = false,
                    OtherDoc = false
                });
                _context.SaveChanges();
            }
        }

        public static void Initialize(IServiceProvider services)
        {
            using var ctx = services.GetRequiredService<CompGateApiDbContext>();
            new DataSeeder(ctx).Seed();
        }
    }
}

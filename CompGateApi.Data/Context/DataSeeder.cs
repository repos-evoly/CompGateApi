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
            SeedRolePermissions();
            SeedSettings();
            SeedCurrencies();

            SeedTransactionCategories();
            SeedServicePackages();
            SeedServicePackageDetails();
            SeedCompanies();
            SeedCompanyAdmins();

            SeedAdminUser();
            SeedUserRolePermissions();
            // SeedSampleTransferRequests();
        }

        private void SeedRoles()
        {
            var desired = new[]
            {
                "SuperAdmin",
                "Admin",
                "CompanyManager",
                "CompanyUser",
                "CompanyAccountant",
                "CompanyAuditor",
                "Maker",
                "Checker",
                "Viewer"
            };

            var existing = _context.Roles
                .Select(r => r.NameLT)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var toAdd = desired
                .Where(name => !existing.Contains(name))
                .Select(name => new Role { NameLT = name })
                .ToList();

            if (toAdd.Any())
            {
                _context.Roles.AddRange(toAdd);
                _context.SaveChanges();
            }
        }

        private void SeedPermissions()
        {
            if (_context.Permissions.Any()) return;

            var permissions = new[]
            {
        new Permission { NameAr = "CompanyCanDashboard",                        Description = "Company: view dashboard",                          IsGlobal = false },
        new Permission { NameAr = "CompanyCanStatementOfAccount",              Description = "Company: statement of accounts",                     IsGlobal = false },
        new Permission { NameAr = "CompanyCanEmployees",                       Description = "Company: manage employees",                          IsGlobal = false },
        new Permission { NameAr = "CompanyCanTransfer",                        Description = "Company: initiate transfers",                        IsGlobal = false },
        new Permission { NameAr = "CompanyCanTransferInternal",                Description = "Company: internal transfers",                         IsGlobal = false },
        new Permission { NameAr = "CompanyCanTransferExternal",                Description = "Company: external transfers",                         IsGlobal = false },
        new Permission { NameAr = "CompanyCanRequests",                        Description = "Company: submit requests",                            IsGlobal = false },
        new Permission { NameAr = "CompanyCanRequestCheckBook",                Description = "Company: request check book",                         IsGlobal = false },
        new Permission { NameAr = "CompanyCanRequestCertifiedCheck",           Description = "Company: request certified check",                    IsGlobal = false },
        new Permission { NameAr = "CompanyCanRequestGuaranteeLetter",          Description = "Company: request guarantee letter",                   IsGlobal = false },
        new Permission { NameAr = "CompanyCanRequestCreditFacility",           Description = "Company: request credit facility",                    IsGlobal = false },
        new Permission { NameAr = "CompanyCanRequestVisa",                     Description = "Company: request visa",                                IsGlobal = false },
        new Permission { NameAr = "CompanyCanRequestCertifiedBankStatement",   Description = "Company: request certified bank statement",           IsGlobal = false },
        new Permission { NameAr = "CompanyCanRequestRTGS",                     Description = "Company: request RTGS",                                IsGlobal = false },
        new Permission { NameAr = "CompanyCanRequestForeignTransfers",         Description = "Company: request foreign transfers",                  IsGlobal = false },
        new Permission { NameAr = "CompanyCanRequestCBL",                      Description = "Company: request CBL",                                 IsGlobal = false },
        new Permission { NameAr = "CompanyCanCurrencies",                      Description = "Company: manage currencies",                           IsGlobal = false },
        new Permission { NameAr = "CompanyCanServicePackages",                 Description = "Company: manage service packages",                     IsGlobal = false },
        new Permission { NameAr = "CompanyCanCompanies",                       Description = "Company: manage companies",                            IsGlobal = false },
        new Permission { NameAr = "CompanyCanSettings",                        Description = "Company: manage settings",                            IsGlobal = false },

        // employee-scoped (IsGlobal = true)
        new Permission { NameAr = "EmployeeCanDashboard",                      Description = "Employee: view dashboard",                            IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanStatementOfAccount",            Description = "Employee: view statement of accounts",                IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanEmployees",                     Description = "Employee: manage employees",                          IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanTransfer",                      Description = "Employee: initiate transfers",                        IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanTransferInternal",              Description = "Employee: internal transfers",                         IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanTransferExternal",              Description = "Employee: external transfers",                         IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanRequests",                      Description = "Employee: submit requests",                            IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanRequestCheckBook",              Description = "Employee: request check book",                         IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanRequestCertifiedCheck",         Description = "Employee: request certified check",                    IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanRequestGuaranteeLetter",        Description = "Employee: request guarantee letter",                   IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanRequestCreditFacility",         Description = "Employee: request credit facility",                    IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanRequestVisa",                   Description = "Employee: request visa",                                IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanRequestCertifiedBankStatement", Description = "Employee: request certified bank statement",           IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanRequestRTGS",                   Description = "Employee: request RTGS",                                IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanRequestForeignTransfers",       Description = "Employee: request foreign transfers",                  IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanRequestCBL",                    Description = "Employee: request CBL",                                 IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanCurrencies",                    Description = "Employee: manage currencies",                           IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanServicePackages",               Description = "Employee: manage service packages",                     IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanCompanies",                     Description = "Employee: manage companies",                            IsGlobal = true  },
        new Permission { NameAr = "EmployeeCanSettings",                      Description = "Employee: manage settings",                            IsGlobal = true  },
    };

            _context.Permissions.AddRange(permissions);
            _context.SaveChanges();
        }

        private void SeedRolePermissions()
        {
            if (_context.RolePermissions.Any()) return;

            // build lookup dictionaries
            var roleIds = _context.Roles
                .ToDictionary(r => r.NameLT, r => r.Id, StringComparer.OrdinalIgnoreCase);
            var permIds = _context.Permissions
                .ToDictionary(p => p.NameAr, p => p.Id, StringComparer.OrdinalIgnoreCase);

            void AddPerms(string role, params string[] perms)
            {
                var rid = roleIds[role];
                foreach (var pn in perms)
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = rid,
                        PermissionId = permIds[pn]
                    });
                }
            }

            // SuperAdmin gets everything
            AddPerms("SuperAdmin", permIds.Keys.ToArray());

            // Admin (back-office) gets all employee-scoped perms
            AddPerms("Admin",
                permIds.Keys
                    .Where(n => n.StartsWith("EmployeeCan", StringComparison.OrdinalIgnoreCase))
                    .ToArray()
            );

            // CompanyManager gets the full company footprint
            AddPerms("CompanyManager",
                "CompanyCanDashboard",
                "CompanyCanStatementOfAccount",
                "CompanyCanEmployees",
                "CompanyCanTransfer",
                "CompanyCanTransferInternal",
                "CompanyCanTransferExternal",
                "CompanyCanRequests",
                "CompanyCanRequestCheckBook",
                "CompanyCanRequestCertifiedCheck",
                "CompanyCanRequestGuaranteeLetter",
                "CompanyCanRequestCreditFacility",
                "CompanyCanRequestVisa",
                "CompanyCanRequestCertifiedBankStatement",
                "CompanyCanRequestRTGS",
                "CompanyCanRequestForeignTransfers",
                "CompanyCanRequestCBL",
                "CompanyCanCurrencies",
                "CompanyCanServicePackages",
                "CompanyCanCompanies",
                "CompanyCanSettings"
            );

            // CompanyAccountant: dashboard, statements, currency
            AddPerms("CompanyAccountant",
                "CompanyCanDashboard",
                "CompanyCanStatementOfAccount",
                "CompanyCanCurrencies"
            );

            // CompanyUser: dashboard + basic transfer & request
            AddPerms("CompanyUser",
                "CompanyCanDashboard",
                "CompanyCanTransfer",
                "CompanyCanRequests"
            );

            // CompanyAuditor: statements only
            AddPerms("CompanyAuditor",
                "CompanyCanStatementOfAccount"
            );

            // Maker: can update requests
            AddPerms("Maker",
                "EmployeeCanRequestCheckBook",
                "EmployeeCanRequestCertifiedCheck",
                "EmployeeCanRequestGuaranteeLetter",
                "EmployeeCanRequestCreditFacility",
                "EmployeeCanRequestVisa",
                "EmployeeCanRequestCertifiedBankStatement",
                "EmployeeCanRequestRTGS",
                "EmployeeCanRequestForeignTransfers",
                "EmployeeCanRequestCBL",
                "EmployeeCanRequests",
                "EmployeeCanTransfer"
            );

            // Checker: can inspect
            AddPerms("Checker",
                "EmployeeCanDashboard",
                "EmployeeCanStatementOfAccount",
                "EmployeeCanTransfer",
                "EmployeeCanRequests"
            );

            // Viewer: read-only
            AddPerms("Viewer",
                "EmployeeCanDashboard",
                "EmployeeCanStatementOfAccount",
                "EmployeeCanRequests"
            );

            _context.SaveChanges();
        }


        private void SeedSettings()
        {
            if (_context.Settings.Any()) return;
            _context.Settings.Add(new Settings
            {
                GlobalLimit = 1000000m
            });
            _context.SaveChanges();
        }

        private void SeedCurrencies()
        {
            if (_context.Currencies.Any()) return;
            _context.Currencies.AddRange(new[]
            {
                new Currency { Code = "001", Rate = 1m, Description = "LYD" },
                new Currency { Code = "002", Rate = 6.25m, Description = "USD" },
            });
            _context.SaveChanges();
        }

        private void SeedTransactionCategories()
        {
            // key = Name, value = HasLimits
            var wanted = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["Transfers"] = true,
                ["InternalTransfer"] = true,
                ["Requests"] = false,
                ["Checkbook"] = false,
                ["CheckRequest"] = false,
                ["LetterOfGuarantee"] = false,
                ["CreditFacility"] = false,
                ["VisaRequest"] = false,
                ["CertifiedBankStatement"] = false,
                ["Rtgs"] = false,
                ["ForeignTransfer"] = false,
                ["CBL"] = false,

                // ➜ new categories
                ["Group Transfer"] = true,   // use limits like internal
                ["Salary Payment"] = true    // payroll category
            };

            var existing = _context.TransactionCategories.ToList();

            // 1️⃣  Update HasLimits on rows that already exist but have wrong flag
            foreach (var cat in existing)
            {
                if (wanted.TryGetValue(cat.Name, out bool shouldHaveLimits) &&
                    cat.HasLimits != shouldHaveLimits)
                {
                    cat.HasLimits = shouldHaveLimits;
                }
                // remove from dictionary so we know it's handled
                wanted.Remove(cat.Name);
            }

            // 2️⃣  Insert any remaining (missing) categories
            foreach (var kv in wanted)
            {
                _context.TransactionCategories.Add(new TransactionCategory
                {
                    Name = kv.Key,
                    HasLimits = kv.Value
                });
            }

            _context.SaveChanges();
        }

        private void SeedServicePackages()
        {
            if (_context.ServicePackages.Any()) return;

            var packages = new[]
            {
                new ServicePackage { Name = "Inquiry Package",  Description = "Inquiry Package", DailyLimit = 1000m,   MonthlyLimit = 10000m },
                new ServicePackage { Name = "Full Package", Description = "Full Package", DailyLimit = 5000m,   MonthlyLimit = 50000m },
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
            {
                foreach (var cat in cats)
                {
                    // default values per package
                    decimal b2bLimit, b2cLimit, b2bFee, b2cFee, b2bMinPct, b2cMinPct, b2bCommPct, b2cCommPct;


                    switch (pkg.Name)
                    {
                        case "Full Package":
                            b2bLimit = 16000m; b2cLimit = 8000m;
                            b2bFee = 1.5m; b2cFee = 3m;
                            b2bMinPct = 0.10m; b2cMinPct = 0.20m;
                            b2bCommPct = 0.30m; b2cCommPct = 0.50m;
                            break;

                        default: // Inquiry
                            b2bLimit = 1000m; b2cLimit = 500m;
                            b2bFee = 5m; b2cFee = 10m;
                            b2bMinPct = 0.50m; b2cMinPct = 1.00m;
                            b2bCommPct = 1.00m; b2cCommPct = 1.50m;
                            break;
                    }

                    _context.ServicePackageDetails.Add(new ServicePackageDetail
                    {
                        ServicePackageId = pkg.Id,
                        TransactionCategoryId = cat.Id,
                        IsEnabledForPackage = true,
                        B2BTransactionLimit = b2bLimit,
                        B2CTransactionLimit = b2cLimit,
                        B2BFixedFee = b2bFee,
                        B2CFixedFee = b2cFee,
                        B2BMinPercentage = b2bMinPct,
                        B2CMinPercentage = b2cMinPct,
                        B2BMaxAmount = 100000m, // default max amount
                        B2CMaxAmount = 50000m, // default max amount

                        B2BCommissionPct = b2bCommPct,

                        B2CCommissionPct = b2cCommPct
                    });
                }
            }

            _context.SaveChanges();
        }

        private void SeedCompanies()
        {
            if (_context.Companies.Any()) return;

            var Inquiry = _context.ServicePackages.Single(p => p.Name == "Inquiry");

            var companies = new[]
            {
                new Company { Code = "725010", Name = "Company 725010", IsActive = true, ServicePackageId = Inquiry.Id }
            };

            _context.Companies.AddRange(companies);
            _context.SaveChanges();
        }

        private void SeedCompanyAdmins()
        {
            var managerRole = _context.Roles.Single(r => r.NameLT == "CompanyManager");


            var co1 = _context.Companies.Single(c => c.Code == "725010");
            if (!_context.Users.Any(u => u.Email == "nader@gmail.com"))
            {
                _context.Users.Add(new User
                {
                    AuthUserId = 8,
                    CompanyId = co1.Id,
                    FirstName = "Nader",
                    LastName = "Owner",
                    Email = "nader@gmail.com",
                    Phone = "8888888888",
                    RoleId = managerRole.Id,
                    IsCompanyAdmin = true
                });
            }

            _context.SaveChanges();
        }

        private void SeedAdminUser()
        {
            if (_context.Users.Any(u => u.Email == "admin@example.com")) return;
            var adminRole = _context.Roles.Single(r => r.NameLT == "Admin");

            var admin = new User
            {
                AuthUserId = 1,
                FirstName = "System",
                LastName = "Administrator",
                Email = "admin@example.com",
                Phone = "999999999",
                RoleId = adminRole.Id,
                IsCompanyAdmin = false
            };

            _context.Users.Add(admin);
            _context.SaveChanges();
        }

        private void SeedUserRolePermissions()
        {
            var admin = _context.Users.SingleOrDefault(u => u.Email == "admin@example.com");
            if (admin == null) return;

            var rolePerms = _context.RolePermissions.Where(rp => rp.RoleId == admin.RoleId).ToList();
            foreach (var rp in rolePerms)
            {
                if (!_context.UserRolePermissions.Any(urp => urp.UserId == admin.Id && urp.RoleId == rp.RoleId && urp.PermissionId == rp.PermissionId))
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

        // private void SeedSampleTransferRequests()
        // {
        //     if (_context.TransferRequests.Any()) return;
        //     var user = _context.Users.First(u => u.CompanyId.HasValue);
        //     var cats = _context.TransactionCategories.ToList();
        //     var pkg = _context.ServicePackages.Single(p => p.Name == "Standard");

        //     foreach (var cat in cats)
        //     {
        //         _context.TransferRequests.Add(new TransferRequest
        //         {
        //             UserId = user.Id,
        //             CompanyId = user.CompanyId!.Value,
        //             TransactionCategoryId = cat.Id,
        //             FromAccount = "ACCT-0001",
        //             ToAccount = "ACCT-1001",
        //             Amount = 10000,
        //             CurrencyId = _context.Currencies.First().Id,
        //             ServicePackageId = pkg.Id,
        //             Status = "Pending",
        //             RequestedAt = DateTime.UtcNow
        //         });
        //     }

        //     _context.SaveChanges();
        // }

        public static void Initialize(IServiceProvider services)
        {
            using var ctx = services.GetRequiredService<CompGateApiDbContext>();
            new DataSeeder(ctx).Seed();
        }
    }
}

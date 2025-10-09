// CompGateApi.Data.Seeding/DataSeeder.cs
using System;
using System.Collections.Generic;
using System.Linq;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

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
            // SeedCompanies();
            // SeedCompanyAdmins();

            SeedPricings();
            SeedAdminUser();
            SeedUserRolePermissions();
        }

        private void SeedRoles()
        {
            if (_context.Roles.Any()) return;

            // Enable explicit identity inserts for fixed IDs within same connection/transaction
            var conn = _context.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) conn.Open();
            using (var tx = _context.Database.BeginTransaction())
            {
                _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Roles] ON;");
                try
                {
                    _context.Roles.AddRange(new[]
                    {
                        new Role { Id = 1, NameAR = "المدير الأعلى", NameLT = "SuperAdmin", Description = "المدير الأعلى", IsGlobal = true },
                        new Role { Id = 2, NameAR = "المدير", NameLT = "Admin", Description = "المدير", IsGlobal = true },
                        new Role { Id = 3, NameAR = "مدير الشركة", NameLT = "CompanyManager", Description = "مدير الشركة", IsGlobal = false },
                        new Role { Id = 4, NameAR = "مستخدم الشركة", NameLT = "CompanyUser", Description = "مستخدم الشركة", IsGlobal = false },
                        new Role { Id = 5, NameAR = "محاسب الشركة", NameLT = "CompanyAccountant", Description = "محاسب الشركة", IsGlobal = false },
                        new Role { Id = 6, NameAR = "مدقق الشركة", NameLT = "CompanyAuditor", Description = "مدقق الشركة", IsGlobal = false },
                        new Role { Id = 7, NameAR = "المُنشئ", NameLT = "Maker", Description = "المُنشئ", IsGlobal = true },
                        new Role { Id = 8, NameAR = "المراجِع", NameLT = "Checker", Description = "المراجِع", IsGlobal = true },
                        new Role { Id = 9, NameAR = "المشاهد", NameLT = "Viewer", Description = "المشاهد", IsGlobal = true },
                    });
                    _context.SaveChanges();
                }
                finally
                {
                    _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Roles] OFF;");
                    tx.Commit();
                    if (!wasOpen) conn.Close();
                }
            }
        }

        private void SeedPermissions()
        {
            if (_context.Permissions.Any()) return;
            var list = new List<Permission>
            {
                new Permission { Id=1,  NameAr="CompanyCanDashboard", Description="Company: view dashboard", IsGlobal=false, NameEn="CompanyCanDashboard" },
                new Permission { Id=2,  NameAr="CompanyCanStatementOfAccount", Description="Company: statement of accounts", IsGlobal=false, NameEn="CompanyCanStatementOfAccount" },
                new Permission { Id=3,  NameAr="CompanyCanEmployees", Description="Company: manage employees", IsGlobal=false, NameEn="CompanyCanEmployees" },
                new Permission { Id=4,  NameAr="CompanyCanTransfer", Description="Company: initiate transfers", IsGlobal=false, NameEn="CompanyCanTransfer" },
                new Permission { Id=6,  NameAr="CompanyCanTransferExternal", Description="Company: external transfers", IsGlobal=false, NameEn="CompanyCanTransferExternal" },
                new Permission { Id=7,  NameAr="CompanyCanRequests", Description="Company: submit requests", IsGlobal=false, NameEn="CompanyCanRequests" },
                new Permission { Id=8,  NameAr="CompanyCanRequestCheckBook", Description="Company: request check book", IsGlobal=false, NameEn="CompanyCanRequestCheckBook" },
                new Permission { Id=9,  NameAr="CompanyCanRequestCertifiedCheck", Description="Company: request certified check", IsGlobal=false, NameEn="CompanyCanRequestCertifiedCheck" },
                new Permission { Id=10, NameAr="CompanyCanRequestGuaranteeLetter", Description="Company: request guarantee letter", IsGlobal=false, NameEn="CompanyCanRequestGuaranteeLetter" },
                new Permission { Id=11, NameAr="CompanyCanRequestCreditFacility", Description="Company: request credit facility", IsGlobal=false, NameEn="CompanyCanRequestCreditFacility" },
                new Permission { Id=12, NameAr="CompanyCanRequestVisa", Description="Company: request visa", IsGlobal=false, NameEn="CompanyCanRequestVisa" },
                new Permission { Id=13, NameAr="CompanyCanRequestCertifiedBankStatement", Description="Company: request certified bank statement", IsGlobal=false, NameEn="CompanyCanRequestCertifiedBankStatement" },
                new Permission { Id=14, NameAr="CompanyCanRequestRTGS", Description="Company: request RTGS", IsGlobal=false, NameEn="CompanyCanRequestRTGS" },
                new Permission { Id=15, NameAr="CompanyCanRequestForeignTransfers", Description="Company: request foreign transfers", IsGlobal=false, NameEn="CompanyCanRequestForeignTransfers" },
                new Permission { Id=16, NameAr="CompanyCanRequestCBL", Description="Company: request CBL", IsGlobal=false, NameEn="CompanyCanRequestCBL" },
                new Permission { Id=17, NameAr="CompanyCanCurrencies", Description="Company: manage currencies", IsGlobal=false, NameEn="CompanyCanCurrencies" },
                new Permission { Id=18, NameAr="CompanyCanServicePackages", Description="Company: manage service packages", IsGlobal=false, NameEn="CompanyCanServicePackages" },
                new Permission { Id=19, NameAr="CompanyCanCompanies", Description="Company: manage companies", IsGlobal=false, NameEn="CompanyCanCompanies" },
                new Permission { Id=20, NameAr="CompanyCanSettings", Description="Company: manage settings", IsGlobal=false, NameEn="CompanyCanSettings" },
                new Permission { Id=21, NameAr="EmployeeCanDashboard", Description="Employee: view dashboard", IsGlobal=true, NameEn="EmployeeCanDashboard" },
                new Permission { Id=22, NameAr="EmployeeCanStatementOfAccount", Description="Employee: view statement of accounts", IsGlobal=true, NameEn="EmployeeCanStatementOfAccount" },
                new Permission { Id=23, NameAr="EmployeeCanEmployees", Description="Employee: manage employees", IsGlobal=true, NameEn="EmployeeCanEmployees" },
                new Permission { Id=24, NameAr="EmployeeCanTransfer", Description="Employee: initiate transfers", IsGlobal=true, NameEn="EmployeeCanTransfer" },
                new Permission { Id=25, NameAr="EmployeeCanTransferInternal", Description="Employee: internal transfers", IsGlobal=true, NameEn="EmployeeCanTransferInternal" },
                new Permission { Id=26, NameAr="EmployeeCanTransferExternal", Description="Employee: external transfers", IsGlobal=true, NameEn="EmployeeCanTransferExternal" },
                new Permission { Id=27, NameAr="EmployeeCanRequests", Description="Employee: submit requests", IsGlobal=true, NameEn="EmployeeCanRequests" },
                new Permission { Id=28, NameAr="EmployeeCanRequestCheckBook", Description="Employee: request check book", IsGlobal=true, NameEn="EmployeeCanRequestCheckBook" },
                new Permission { Id=29, NameAr="EmployeeCanRequestCertifiedCheck", Description="Employee: request certified check", IsGlobal=true, NameEn="EmployeeCanRequestCertifiedCheck" },
                new Permission { Id=30, NameAr="EmployeeCanRequestGuaranteeLetter", Description="Employee: request guarantee letter", IsGlobal=true, NameEn="EmployeeCanRequestGuaranteeLetter" },
                new Permission { Id=31, NameAr="EmployeeCanRequestCreditFacility", Description="Employee: request credit facility", IsGlobal=true, NameEn="EmployeeCanRequestCreditFacility" },
                new Permission { Id=32, NameAr="EmployeeCanRequestVisa", Description="Employee: request visa", IsGlobal=true, NameEn="EmployeeCanRequestVisa" },
                new Permission { Id=33, NameAr="EmployeeCanRequestCertifiedBankStatement", Description="Employee: request certified bank statement", IsGlobal=true, NameEn="EmployeeCanRequestCertifiedBankStatement" },
                new Permission { Id=34, NameAr="EmployeeCanRequestRTGS", Description="Employee: request RTGS", IsGlobal=true, NameEn="EmployeeCanRequestRTGS" },
                new Permission { Id=35, NameAr="EmployeeCanRequestForeignTransfers", Description="Employee: request foreign transfers", IsGlobal=true, NameEn="EmployeeCanRequestForeignTransfers" },
                new Permission { Id=36, NameAr="EmployeeCanRequestCBL", Description="Employee: request CBL", IsGlobal=true, NameEn="EmployeeCanRequestCBL" },
                new Permission { Id=37, NameAr="EmployeeCanCurrencies", Description="Employee: manage currencies", IsGlobal=true, NameEn="EmployeeCanCurrencies" },
                new Permission { Id=38, NameAr="EmployeeCanServicePackages", Description="Employee: manage service packages", IsGlobal=true, NameEn="EmployeeCanServicePackages" },
                new Permission { Id=39, NameAr="EmployeeCanCompanies", Description="Employee: manage companies", IsGlobal=true, NameEn="EmployeeCanCompanies" },
                new Permission { Id=40, NameAr="EmployeeCanSettings", Description="Employee: manage settings", IsGlobal=true, NameEn="EmployeeCanSettings" },
                new Permission { Id=41, NameAr="CanAddCheckbook", Description="CanAddCheckbook", IsGlobal=false, NameEn="CanAddCheckbook" },
                new Permission { Id=47, NameAr="CBLCanAdd", Description="CBLCanAdd", IsGlobal=false, NameEn="CBLCanAdd" },
                new Permission { Id=48, NameAr="RTGSCanEdit", Description="RTGSCanEdit", IsGlobal=false, NameEn="RTGSCanEdit" },
                new Permission { Id=49, NameAr="RTGSCanAdd", Description="RTGSCanAdd", IsGlobal=false, NameEn="RTGSCanAdd" },
                new Permission { Id=50, NameAr="VisaRequestCanEdit", Description="VisaRequestCanEdit", IsGlobal=false, NameEn="VisaRequestCanEdit" },
                new Permission { Id=51, NameAr="VisaRequestCanAdd", Description="VisaRequestCanAdd", IsGlobal=false, NameEn="VisaRequestCanAdd" },
                new Permission { Id=52, NameAr="LetterOfGuaranteeCanEdit", Description="LetterOfGuaranteeCanEdit", IsGlobal=false, NameEn="LetterOfGuaranteeCanEdit" },
                new Permission { Id=53, NameAr="LetterOfGuaranteeCanAdd", Description="LetterOfGuaranteeCanAdd", IsGlobal=false, NameEn="LetterOfGuaranteeCanAdd" },
                new Permission { Id=54, NameAr="CheckRequestCanEdit", Description="CheckRequestCanEdit", IsGlobal=false, NameEn="CheckRequestCanEdit" },
                new Permission { Id=55, NameAr="CheckRequestCanAdd", Description="CheckRequestCanAdd", IsGlobal=false, NameEn="CheckRequestCanAdd" },
                new Permission { Id=56, NameAr="CreditFacilityCanEdit", Description="CreditFacilityCanEdit", IsGlobal=false, NameEn="CreditFacilityCanEdit" },
                new Permission { Id=57, NameAr="CreditFacilityCanAdd", Description="CreditFacilityCanAdd", IsGlobal=false, NameEn="CreditFacilityCanAdd" },
                new Permission { Id=58, NameAr="ForeignTransfersCanAdd", Description="ForeignTransfersCanAdd", IsGlobal=false, NameEn="ForeignTransfersCanAdd" },
                new Permission { Id=59, NameAr="ForeignTransfersCanEdit", Description="ForeignTransfersCanEdit", IsGlobal=false, NameEn="ForeignTransfersCanEdit" },
                new Permission { Id=60, NameAr="CertifiedBankStatementCanEdit", Description="CertifiedBankStatementCanEdit", IsGlobal=false, NameEn="CertifiedBankStatementCanEdit" },
                new Permission { Id=61, NameAr="CertifiedBankStatementCanAdd", Description="CertifiedBankStatementCanAdd", IsGlobal=false, NameEn="CertifiedBankStatementCanAdd" },
                new Permission { Id=62, NameAr="CheckbookCanEdit", Description="CheckbookCanEdit", IsGlobal=false, NameEn="CheckbookCanEdit" },
                new Permission { Id=63, NameAr="CheckbookCanAdd", Description="CheckbookCanAdd", IsGlobal=false, NameEn="CheckbookCanAdd" },
                new Permission { Id=64, NameAr="CompanyCanTransferInternal", Description="CompanyCanTransferInternal", IsGlobal=false, NameEn="CompanyCanTransferInternal" },
                new Permission { Id=65, NameAr="canCreateOrEditSalaryCycle", Description="canCreateOrEditSalaryCycle", IsGlobal=false, NameEn="canCreateOrEditSalaryCycle" },
                new Permission { Id=66, NameAr="canPostSalaryCycle", Description="canPostSalaryCycle", IsGlobal=false, NameEn="canPostSalaryCycle" },
                new Permission { Id=67, NameAr="companyCanEditUser", Description="companyCanEditUser", IsGlobal=true, NameEn="companyCanEditUser" },
                new Permission { Id=68, NameAr="companyCanAddUser", Description="companyCanAddUser", IsGlobal=false, NameEn="companyCanAddUser" },
            };

            // Enable explicit identity inserts for fixed IDs within same connection/transaction
            var conn = _context.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) conn.Open();
            using (var tx = _context.Database.BeginTransaction())
            {
                _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Permissions] ON;");
                try
                {
                    _context.Permissions.AddRange(list);
                    _context.SaveChanges();
                }
                finally
                {
                    _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Permissions] OFF;");
                    tx.Commit();
                    if (!wasOpen) conn.Close();
                }
            }
        }

        private void SeedRolePermissions()
        {
            if (_context.RolePermissions.Any()) return;

            var pairs = new (int roleId, int permId)[]
            {
                // Admin (2): EmployeeCan* (21..40)
                (2,21),(2,22),(2,23),(2,24),(2,25),(2,26),(2,27),(2,28),(2,29),(2,30),
                (2,31),(2,32),(2,33),(2,34),(2,35),(2,36),(2,37),(2,38),(2,39),(2,40),

                // CompanyAccountant (5): 1,2,17
                (5,1),(5,2),(5,17),

                // CompanyAuditor (6): 2
                (6,2),

                // Maker (7): 28..36, 27, 24
                (7,28),(7,29),(7,30),(7,31),(7,32),(7,33),(7,34),(7,35),(7,36),(7,27),(7,24),

                // Checker (8): 21,22,24,27
                (8,21),(8,22),(8,24),(8,27),

                // Viewer (9): 21,22,27
                (9,21),(9,22),(9,27),

                // SuperAdmin (1): 2
                (1,2),

                // CompanyUser (4): 1,4,7,47,16,36
                (4,1),(4,4),(4,7),(4,47),(4,16),(4,36),

                // CompanyManager (3): 1,2,3,4,6..20,64
                (3,1),(3,2),(3,3),(3,4),(3,6),(3,7),(3,8),(3,9),(3,10),(3,11),
                (3,12),(3,13),(3,14),(3,15),(3,16),(3,17),(3,18),(3,19),(3,20),(3,64),
            };

            foreach (var p in pairs)
            {
                if (!_context.RolePermissions.Any(rp => rp.RoleId == p.roleId && rp.PermissionId == p.permId))
                {
                    _context.RolePermissions.Add(new RolePermission { RoleId = p.roleId, PermissionId = p.permId });
                }
            }

            var conn = _context.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) conn.Open();
            using (var tx = _context.Database.BeginTransaction())
            {
                _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Companies] ON;");
                try
                {
                    _context.SaveChanges();
                }
                finally
                {
                    _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Companies] OFF;");
                    tx.Commit();
                    if (!wasOpen) conn.Close();
                }
            }
        }


        private void SeedSettings()
        {
            if (!_context.Settings.Any())
            {
                _context.Settings.Add(new Settings
                {
                    CommissionAccount = "0012430126001",
                    CommissionAccountUSD = "0015798000002",
                    GlobalLimit = 1_000_000m,
                    EvoWallet = null
                });
                _context.SaveChanges();
            }

            if (!_context.EconomicSectors.Any())
            {
                var conn = _context.Database.GetDbConnection();
                var wasOpen = conn.State == System.Data.ConnectionState.Open;
                if (!wasOpen) conn.Open();
                using (var tx = _context.Database.BeginTransaction())
                {
                    _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [EconomicSectors] ON;");
                    try
                    {
                        _context.EconomicSectors.AddRange(new[]
                        {
                    new EconomicSector { Id = 1,  Name = "Suppliers", Description = "Sending to a supplier" },
                    new EconomicSector { Id = 15, Name = "التجارة بالجملة والتجزئة", Description = "بيع وشراء السلع" },
                    new EconomicSector { Id = 16, Name = "التصنيع والصناعات التحويلية", Description = "تحويل المواد إلى منتجات." },
                    new EconomicSector { Id = 17, Name = "المقاولات والبناء", Description = "تشييد مبانٍ وبنية تحتية." },
                    new EconomicSector { Id = 18, Name = "النقل والخدمات اللوجستية", Description = "شحن وتخزين البضائع." },
                });
                        _context.SaveChanges();
                    }
                    finally
                    {
                        _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [EconomicSectors] OFF;");
                        tx.Commit();
                        if (!wasOpen) conn.Close();
                    }
                }
            }

            if (!_context.FormStatuses.Any())
            {
                var conn = _context.Database.GetDbConnection();
                var wasOpen = conn.State == System.Data.ConnectionState.Open;
                if (!wasOpen) conn.Open();
                using (var tx = _context.Database.BeginTransaction())
                {
                    _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [FormStatuses] ON;");
                    try
                    {
                        _context.FormStatuses.AddRange(new[]
                        {
                    new FormStatus { Id = 1, NameEn = "approvedEn", NameAr = "approvedAr", DescriptionEn = "Approved", DescriptionAr = "Approved" },
                    new FormStatus { Id = 2, NameEn = "2 editedtesting new en", NameAr = "2 testing new ar", DescriptionEn = "2 testing new en", DescriptionAr = "2 testing new ar" },
                    new FormStatus { Id = 3, NameEn = "approved", NameAr = "موافقة", DescriptionEn = "approved", DescriptionAr = "موافقة" },
                    new FormStatus { Id = 4, NameEn = "declined", NameAr = "رفض", DescriptionEn = "declined", DescriptionAr = "رفض" },
                    new FormStatus { Id = 5, NameEn = "printed", NameAr = "مطبوع", DescriptionEn = "printed", DescriptionAr = "مطبوع" },
                });
                        _context.SaveChanges();
                    }
                    finally
                    {
                        _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [FormStatuses] OFF;");
                        tx.Commit();
                        if (!wasOpen) conn.Close();
                    }
                }
            }
        }

        private void SeedCurrencies()
        {
            if (_context.Currencies.Any()) return;
            var conn = _context.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) conn.Open();
            using (var tx = _context.Database.BeginTransaction())
            {
                _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Currencies] ON;");
                try
                {
                    _context.Currencies.AddRange(new[]
                    {
                        new Currency { Id = 1, Code = "001", Rate = 1m, Description = "LYD" },
                        new Currency { Id = 2, Code = "002", Rate = 6.25m, Description = "USD" },
                    });
                    _context.SaveChanges();
                }
                finally
                {
                    _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Currencies] OFF;");
                    tx.Commit();
                    if (!wasOpen) conn.Close();
                }
            }
        }

        private void SeedTransactionCategories()
        {
            // Explicit IDs to match compseederscript.sql
            // From the script's INSERTs for [dbo].[TransactionCategories]
            var desired = new List<TransactionCategory>
            {
                new TransactionCategory { Id = 1,  Name = "Transfers",               HasLimits = true,  CountsTowardTxnLimits = false },
                new TransactionCategory { Id = 2,  Name = "InternalTransfer",       HasLimits = true,  CountsTowardTxnLimits = false },
                new TransactionCategory { Id = 3,  Name = "Requests",               HasLimits = false, CountsTowardTxnLimits = false },
                new TransactionCategory { Id = 4,  Name = "Checkbook",              HasLimits = true,  CountsTowardTxnLimits = true  },
                new TransactionCategory { Id = 5,  Name = "CheckRequest",           HasLimits = true,  CountsTowardTxnLimits = false },
                new TransactionCategory { Id = 6,  Name = "LetterOfGuarantee",      HasLimits = false, CountsTowardTxnLimits = false },
                new TransactionCategory { Id = 7,  Name = "CreditFacility",         HasLimits = false, CountsTowardTxnLimits = false },
                new TransactionCategory { Id = 8,  Name = "VisaRequest",            HasLimits = false, CountsTowardTxnLimits = false },
                new TransactionCategory { Id = 9,  Name = "CertifiedBankStatement", HasLimits = false, CountsTowardTxnLimits = false },
                // Note the gap in IDs per the script (10,11,12 unused)
                new TransactionCategory { Id = 13, Name = "Rtgs",                   HasLimits = false, CountsTowardTxnLimits = false },
                new TransactionCategory { Id = 14, Name = "ForeignTransfer",        HasLimits = false, CountsTowardTxnLimits = false },
                new TransactionCategory { Id = 15, Name = "CBL",                    HasLimits = false, CountsTowardTxnLimits = false },
                new TransactionCategory { Id = 16, Name = "Group Transfer",         HasLimits = true,  CountsTowardTxnLimits = false },
                new TransactionCategory { Id = 17, Name = "Salary Payment",         HasLimits = true,  CountsTowardTxnLimits = false },
            };

            // Upsert to match IDs exactly: if a row exists with the same Id update it;
            // if a row exists with the same Name but different Id, replace it with the correct Id.
            var byId = _context.TransactionCategories.ToDictionary(c => c.Id, c => c);
            var byName = _context.TransactionCategories.ToDictionary(c => c.Name, c => c, StringComparer.Ordinal);

            bool changed = false;
            foreach (var d in desired)
            {
                if (byId.TryGetValue(d.Id, out var withId))
                {
                    withId.Name = d.Name;
                    withId.HasLimits = d.HasLimits;
                    withId.CountsTowardTxnLimits = d.CountsTowardTxnLimits;
                    changed = true;
                }
                else if (byName.TryGetValue(d.Name, out var withName))
                {
                    // Name exists with wrong Id: remove and re-insert with fixed Id
                    _context.TransactionCategories.Remove(withName);
                    _context.SaveChanges();

                    var conn = _context.Database.GetDbConnection();
                    var wasOpen = conn.State == System.Data.ConnectionState.Open;
                    if (!wasOpen) conn.Open();
                    using (var tx = _context.Database.BeginTransaction())
                    {
                        _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [TransactionCategories] ON;");
                        try
                        {
                            _context.TransactionCategories.Add(new TransactionCategory
                            {
                                Id = d.Id,
                                Name = d.Name,
                                HasLimits = d.HasLimits,
                                CountsTowardTxnLimits = d.CountsTowardTxnLimits
                            });
                            _context.SaveChanges();
                        }
                        finally
                        {
                            _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [TransactionCategories] OFF;");
                            tx.Commit();
                            if (!wasOpen) conn.Close();
                        }
                    }
                }
                else
                {
                    // Not present at all: insert with explicit Id
                    var conn = _context.Database.GetDbConnection();
                    var wasOpen = conn.State == System.Data.ConnectionState.Open;
                    if (!wasOpen) conn.Open();
                    using (var tx = _context.Database.BeginTransaction())
                    {
                        _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [TransactionCategories] ON;");
                        try
                        {
                            _context.TransactionCategories.Add(new TransactionCategory
                            {
                                Id = d.Id,
                                Name = d.Name,
                                HasLimits = d.HasLimits,
                                CountsTowardTxnLimits = d.CountsTowardTxnLimits
                            });
                            _context.SaveChanges();
                        }
                        finally
                        {
                            _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [TransactionCategories] OFF;");
                            tx.Commit();
                            if (!wasOpen) conn.Close();
                        }
                    }
                }
            }

            // Remove any extra categories not in the desired set
            var desiredNames = new HashSet<string>(desired.Select(x => x.Name), StringComparer.Ordinal);
            var extras = _context.TransactionCategories.Where(c => !desiredNames.Contains(c.Name)).ToList();
            if (extras.Count > 0)
            {
                _context.TransactionCategories.RemoveRange(extras);
                changed = true;
            }

            if (changed)
            {
                _context.SaveChanges();
            }
        }

        private void SeedServicePackages()
        {
            if (_context.ServicePackages.Any()) return;
            var conn = _context.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) conn.Open();
            using (var tx = _context.Database.BeginTransaction())
            {
                _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [ServicePackages] ON;");
                try
                {
                    _context.ServicePackages.AddRange(new[]
                    {
                        new ServicePackage { Id = 1, Name = "Inquiry",  Description = "Read-only access", DailyLimit = 1_000_000m, MonthlyLimit = 1_000_000m },
                        new ServicePackage { Id = 2, Name = "Full Package", Description = "Full Package", DailyLimit = 1_000_000m, MonthlyLimit = 10_000_000m }
                    });
                    _context.SaveChanges();
                }
                finally
                {
                    _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [ServicePackages] OFF;");
                    tx.Commit();
                    if (!wasOpen) conn.Close();
                }
            }
        }

        private void SeedPricings()
        {
            // Map category names -> IDs (stable, avoids hardcoding numeric IDs)
            var catIdByName = _context.TransactionCategories
                .ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);

            int Cat(string name) => catIdByName.TryGetValue(name, out var id)
                ? id : throw new InvalidOperationException($"TransactionCategory '{name}' not found. Seed categories first.");

            // Desired pricing rows (from your SQL INSERTs)
            var desired = new[]
            {
        new { TrxCatName = "Checkbook",             PctAmt = (decimal?)null, Price = (decimal?)20.0000m, Desc = "CheckBook - 24 leaves", GL1 = "0012111273001", GL2 = (string?)null, GL3 = (string?)null, GL4 = (string?)null, DTC = "044", CTC = "900", DTC2 = (string?)null, CTC2 = (string?)null, NR2 = (string?)null, APPLYTR2 = false, Unit = 24, AmountRule = (string?)null },
        new { TrxCatName = "Checkbook",             PctAmt = (decimal?)null, Price = (decimal?)40.0000m, Desc = "CheckBook - 48 leaves", GL1 = "0012111273001", GL2 = (string?)null, GL3 = (string?)null, GL4 = (string?)null, DTC = "044", CTC = "900", DTC2 = (string?)null, CTC2 = (string?)null, NR2 = (string?)null, APPLYTR2 = false, Unit = 48, AmountRule = (string?)null },
        new { TrxCatName = "CheckRequest",          PctAmt = (decimal?)null, Price = (decimal?)null,      Desc = "Check Request (per cheque amount)", GL1 = "{BRANCH}831892434", GL2 = (string?)null, GL3 = (string?)null, GL4 = (string?)null, DTC = "044", CTC = "900", DTC2 = (string?)null, CTC2 = (string?)null, NR2 = "Check request", APPLYTR2 = false, Unit = 1, AmountRule = "amount" },
        new { TrxCatName = "VisaRequest",           PctAmt = (decimal?)null, Price = (decimal?)null,      Desc = "Visa Request ", GL1 = "0012111273001", GL2 = (string?)null, GL3 = (string?)null, GL4 = (string?)null, DTC = "044", CTC = "900", DTC2 = (string?)null, CTC2 = (string?)null, NR2 = (string?)null, APPLYTR2 = false, Unit = 1, AmountRule = "amount" },
        new { TrxCatName = "CertifiedBankStatement",PctAmt = (decimal?)null, Price = (decimal?)1.0000m,   Desc = "Certified Bank Statement", GL1 = "0010932700434", GL2 = (string?)null, GL3 = (string?)null, GL4 = (string?)null, DTC = "044", CTC = "900", DTC2 = (string?)null, CTC2 = (string?)null, NR2 = (string?)null, APPLYTR2 = false, Unit = 31, AmountRule = (string?)null },
        new { TrxCatName = "Rtgs",                  PctAmt = (decimal?)null, Price = (decimal?)0.0000m,   Desc = (string?)null, GL1 = "{BRANCH}12111273001", GL2 = (string?)null, GL3 = (string?)null, GL4 = (string?)null, DTC = "", CTC = "", DTC2 = "", CTC2 = "", NR2 = "RTGS transfer", APPLYTR2 = false, Unit = 1, AmountRule = (string?)null },
        new { TrxCatName = "Salary Payment",        PctAmt = (decimal?)0.0000m, Price = (decimal?)5.0000m,Desc = "Salary", GL1 = "0010932702434", GL2 = (string?)null, GL3 = (string?)null, GL4 = (string?)null, DTC = (string?)null, CTC = (string?)null, DTC2 = (string?)null, CTC2 = (string?)null, NR2 = "Salary ", APPLYTR2 = false, Unit = 1, AmountRule = (string?)null },
    }
            .Select(x => new Pricing
            {
                TrxCatId = Cat(x.TrxCatName),
                PctAmt = x.PctAmt,
                Price = x.Price,
                Description = x.Desc,
                GL1 = x.GL1,
                GL2 = x.GL2,
                GL3 = x.GL3,
                GL4 = x.GL4,
                DTC = x.DTC,
                CTC = x.CTC,
                DTC2 = x.DTC2,
                CTC2 = x.CTC2,
                NR2 = x.NR2,
                APPLYTR2 = x.APPLYTR2,
                Unit = x.Unit,
                AmountRule = x.AmountRule
            })
            .ToList();

            // Upsert by (TrxCatId, Unit, COALESCE(AmountRule,'')) as a natural key
            foreach (var row in desired)
            {
                var keyRule = row.AmountRule ?? string.Empty;
                var existing = _context.Pricings
                    .FirstOrDefault(p => p.TrxCatId == row.TrxCatId
                                      && p.Unit == row.Unit
                                      && (p.AmountRule ?? "") == keyRule);

                if (existing == null)
                {
                    _context.Pricings.Add(row);
                }
                else
                {
                    existing.PctAmt = row.PctAmt;
                    existing.Price = row.Price;
                    existing.Description = row.Description;
                    existing.GL1 = row.GL1; existing.GL2 = row.GL2; existing.GL3 = row.GL3; existing.GL4 = row.GL4;
                    existing.DTC = row.DTC; existing.CTC = row.CTC; existing.DTC2 = row.DTC2; existing.CTC2 = row.CTC2;
                    existing.NR2 = row.NR2;
                    existing.APPLYTR2 = row.APPLYTR2;
                    // keep existing.Id and audit fields; Unit/AmountRule are part of natural key
                }
            }

            _context.SaveChanges();
        }


        private void SeedServicePackageDetails()
        {
            // Ensure every service package has ALL transaction categories
            // with the exact numeric values as in compseederscript.sql inserts.

            var pkgs = _context.ServicePackages.ToList();
            var cats = _context.TransactionCategories.ToList();

            // Map by category name to the values used in compseederscript.sql (ServicePackageId = 1 rows)
            // We will apply the same values for every service package as requested.
            var byName = new Dictionary<string, (decimal b2bLimit, decimal b2cLimit, decimal b2bFee, decimal b2cFee,
                                                decimal b2bMinPct, decimal b2cMinPct, decimal b2bCommPct, decimal b2cCommPct,
                                                decimal b2bMax, decimal b2cMax)>(StringComparer.OrdinalIgnoreCase)
            {
                // (B2BTransactionLimit, B2CTransactionLimit, B2BFixedFee, B2CFixedFee,
                //  B2BMinPercentage, B2CMinPercentage, B2BCommissionPct, B2CCommissionPct,
                //  B2BMaxAmount, B2CMaxAmount)
                ["Transfers"]                = (0m,      0m,      0m,    0m,    0m,   0m,   0.1000m, 0.0000m, 1000000m, 1000000m),
                ["InternalTransfer"]         = (0m,      0m,      10m,   10m,   0m,   0m,   0.1000m, 0.1000m, 1000000m, 1000000m),
                ["Requests"]                 = (0m,      0m,      0m,    0m,    0m,   0m,   0m,      0m,      0m,      0m),
                ["Checkbook"]                = (10000m,  10000m,  0m,    0m,    0m,   0m,   0m,      0m,      0m,      0m),
                ["CheckRequest"]             = (10000m,  10000m,  2m,    4m,    1m,   2m,   1m,      1m,      1m,      1m),
                ["LetterOfGuarantee"]        = (0m,      0m,      0m,    0m,    0m,   0m,   0m,      0m,      0m,      0m),
                ["CreditFacility"]           = (0m,      0m,      0m,    0m,    0m,   0m,   0m,      0m,      0m,      0m),
                ["VisaRequest"]              = (0m,      0m,      0m,    0m,    0m,   0m,   0m,      0m,      0m,      0m),
                ["CertifiedBankStatement"]   = (0m,      0m,      0m,    0m,    0m,   0m,   0m,      0m,      0m,      0m),
                ["Rtgs"]                     = (0m,      0m,      0m,    0m,    0m,   0m,   0m,      0m,      0m,      0m),
                ["ForeignTransfer"]          = (0m,      0m,      0m,    0m,    0m,   0m,   0m,      0m,      0m,      0m),
                ["CBL"]                      = (0m,      0m,      0m,    0m,    0m,   0m,   0m,      0m,      0m,      0m),
                ["Group Transfer"]           = (0m,      0m,      0m,    0m,    0m,   0m,   0m,      0m,      0m,      0m),
                ["Salary Payment"]           = (10000m,  10000m,  1m,    1m,    1m,   1m,   3m,      3m,      10000m,  10000m),
            };

            foreach (var pkg in pkgs)
            {
                foreach (var cat in cats)
                {
                    if (!byName.TryGetValue(cat.Name, out var v))
                    {
                        // If a category ever exists that isn't in the SQL mapping, default to zeros.
                        v = (0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
                    }

                    var existing = _context.ServicePackageDetails
                        .FirstOrDefault(d => d.ServicePackageId == pkg.Id && d.TransactionCategoryId == cat.Id);

                    if (existing == null)
                    {
                        _context.ServicePackageDetails.Add(new ServicePackageDetail
                        {
                            ServicePackageId = pkg.Id,
                            TransactionCategoryId = cat.Id,
                            IsEnabledForPackage = true,
                            B2BTransactionLimit = v.b2bLimit,
                            B2CTransactionLimit = v.b2cLimit,
                            B2BFixedFee = v.b2bFee,
                            B2CFixedFee = v.b2cFee,
                            B2BMinPercentage = v.b2bMinPct,
                            B2CMinPercentage = v.b2cMinPct,
                            B2BMaxAmount = v.b2bMax,
                            B2CMaxAmount = v.b2cMax,
                            B2BCommissionPct = v.b2bCommPct,
                            B2CCommissionPct = v.b2cCommPct,
                        });
                    }
                    else
                    {
                        // Update to match exact SQL numeric values and ensure enabled.
                        existing.IsEnabledForPackage = true;
                        existing.B2BTransactionLimit = v.b2bLimit;
                        existing.B2CTransactionLimit = v.b2cLimit;
                        existing.B2BFixedFee = v.b2bFee;
                        existing.B2CFixedFee = v.b2cFee;
                        existing.B2BMinPercentage = v.b2bMinPct;
                        existing.B2CMinPercentage = v.b2cMinPct;
                        existing.B2BMaxAmount = v.b2bMax;
                        existing.B2CMaxAmount = v.b2cMax;
                        existing.B2BCommissionPct = v.b2bCommPct;
                        existing.B2CCommissionPct = v.b2cCommPct;
                    }
                }
            }

            _context.SaveChanges();
        }

        private void SeedCompanies()
        {
            var inquiry = _context.ServicePackages.Single(p => p.Name == "Inquiry");
            var byCode = _context.Companies.ToDictionary(c => c.Code, c => c, StringComparer.OrdinalIgnoreCase);

            void AddOrSkip(Company c)
            {
                if (!byCode.ContainsKey(c.Code))
                {
                    _context.Companies.Add(c);
                    byCode[c.Code] = c;
                }
            }

            AddOrSkip(new Company { Code = "798000", Name = "شركة ايفو للحلول الرقمية", IsActive = true, RegistrationStatus = RegistrationStatus.Approved, ServicePackageId = inquiry.Id });
            AddOrSkip(new Company { Code = "725121", Name = "تشاركيه كمبراكي للبناء والصيانه", IsActive = true, RegistrationStatus = RegistrationStatus.Approved, ServicePackageId = inquiry.Id });
            AddOrSkip(new Company { Code = "725005", Name = "شركه الصفاء للتنميه المساهمه", IsActive = true, RegistrationStatus = RegistrationStatus.Approved, ServicePackageId = inquiry.Id });
            AddOrSkip(new Company { Code = "725007", Name = "الشركه العربيه للاستثمارات السياحيه", IsActive = true, RegistrationStatus = RegistrationStatus.Approved, ServicePackageId = inquiry.Id });
            AddOrSkip(new Company { Code = "777777", Name = "صندوق اعادة اعمار بنغازي الخيري", IsActive = true, RegistrationStatus = RegistrationStatus.UnderReview, ServicePackageId = inquiry.Id });
            AddOrSkip(new Company { Code = "777776", Name = "AL DOK AL MOMEZ", IsActive = true, RegistrationStatus = RegistrationStatus.UnderReview, ServicePackageId = inquiry.Id });
            AddOrSkip(new Company { Code = "777775", Name = "university of benghazi support", IsActive = true, RegistrationStatus = RegistrationStatus.UnderReview, ServicePackageId = inquiry.Id });
            AddOrSkip(new Company { Code = "777774", Name = "مح?ت الخليج للموادالصحيةوالكهربائية", IsActive = true, RegistrationStatus = RegistrationStatus.UnderReview, ServicePackageId = inquiry.Id });
            AddOrSkip(new Company { Code = "777773", Name = "تشاركية الخليل لصناعة المفروشات", IsActive = true, RegistrationStatus = RegistrationStatus.UnderReview, ServicePackageId = inquiry.Id });
            AddOrSkip(new Company { Code = "777772", Name = "AL ZHAB  ALASUAD", IsActive = true, RegistrationStatus = RegistrationStatus.UnderReview, ServicePackageId = inquiry.Id });
            AddOrSkip(new Company { Code = "777771", Name = "شركة الضحى لاستراد مواد البناء", IsActive = true, RegistrationStatus = RegistrationStatus.UnderReview, ServicePackageId = inquiry.Id });
            AddOrSkip(new Company { Code = "783060", Name = "ajwa banghazi advertising company", IsActive = true, RegistrationStatus = RegistrationStatus.Approved, ServicePackageId = inquiry.Id });

            _context.SaveChanges();

            // Pricing entries from compseederscript.sql
            var catByName = _context.TransactionCategories
                .ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);

            void AddPrice(string catName, decimal? pct, decimal? price, string? description,
                          string? gl1, string? gl2, string? gl3, string? gl4,
                          string? dtc, string? ctc, string? dtc2, string? ctc2,
                          string? nr2, bool applyTr2, int unit, string? amountRule)
            {
                if (!catByName.TryGetValue(catName, out var catId)) return;
                var exists = _context.Pricings.Any(p => p.TrxCatId == catId && p.Unit == unit && p.AmountRule == amountRule);
                if (exists) return;
                _context.Pricings.Add(new Pricing
                {
                    TrxCatId = catId,
                    PctAmt = pct,
                    Price = price,
                    Description = description,
                    GL1 = gl1,
                    GL2 = gl2,
                    GL3 = gl3,
                    GL4 = gl4,
                    DTC = dtc,
                    CTC = ctc,
                    DTC2 = dtc2,
                    CTC2 = ctc2,
                    NR2 = nr2,
                    APPLYTR2 = applyTr2,
                    Unit = unit,
                    AmountRule = amountRule
                });
            }

            // Checkbook - 24 leaves
            AddPrice(
                catName: "Checkbook",
                pct: null,
                price: 20.0000m,
                description: "CheckBook - 24 leaves",
                gl1: "0012111273001", gl2: null, gl3: null, gl4: null,
                dtc: "044", ctc: "900", dtc2: null, ctc2: null,
                nr2: null, applyTr2: false, unit: 24, amountRule: null);

            // Checkbook - 48 leaves
            AddPrice(
                catName: "Checkbook",
                pct: null,
                price: 40.0000m,
                description: "CheckBook - 48 leaves",
                gl1: "0012111273001", gl2: null, gl3: null, gl4: null,
                dtc: "044", ctc: "900", dtc2: null, ctc2: null,
                nr2: null, applyTr2: false, unit: 48, amountRule: null);

            // Check Request (per cheque amount)
            AddPrice(
                catName: "CheckRequest",
                pct: null,
                price: null,
                description: "Check Request (per cheque amount)",
                gl1: "{BRANCH}831892434", gl2: null, gl3: null, gl4: null,
                dtc: "044", ctc: "900", dtc2: null, ctc2: null,
                nr2: "Check request", applyTr2: false, unit: 1, amountRule: "amount");

            // Visa Request
            AddPrice(
                catName: "VisaRequest",
                pct: null,
                price: null,
                description: "Visa Request",
                gl1: "0012111273001", gl2: null, gl3: null, gl4: null,
                dtc: "044", ctc: "900", dtc2: null, ctc2: null,
                nr2: null, applyTr2: false, unit: 1, amountRule: "amount");

            // Certified Bank Statement
            AddPrice(
                catName: "CertifiedBankStatement",
                pct: null,
                price: 1.0000m,
                description: "Certified Bank Statement",
                gl1: "0010932700434", gl2: null, gl3: null, gl4: null,
                dtc: "044", ctc: "900", dtc2: null, ctc2: null,
                nr2: null, applyTr2: false, unit: 31, amountRule: null);

            // RTGS
            AddPrice(
                catName: "Rtgs",
                pct: null,
                price: 0.0000m,
                description: null,
                gl1: "{BRANCH}12111273001", gl2: null, gl3: null, gl4: null,
                dtc: "", ctc: "", dtc2: "", ctc2: "",
                nr2: "RTGS transfer", applyTr2: false, unit: 1, amountRule: null);

            // Salary Payment
            AddPrice(
                catName: "Salary Payment",
                pct: 0.0000m,
                price: 5.0000m,
                description: "Salary",
                gl1: "0010932702434", gl2: null, gl3: null, gl4: null,
                dtc: null, ctc: null, dtc2: null, ctc2: null,
                nr2: "Salary ", applyTr2: false, unit: 1, amountRule: null);

            _context.SaveChanges();
        }

        private void SeedCompanyAdmins()
        {
            var managerRole = _context.Roles.Single(r => r.NameLT == "CompanyManager");

            var co1 = _context.Companies.FirstOrDefault(c => c.Code == "725010");
            if (co1 != null)
            {
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
        }

        private void SeedAdminUser()
        {
            var adminRole = _context.Roles.Single(r => r.NameLT == "Admin");
            var mgrRole = _context.Roles.Single(r => r.NameLT == "CompanyManager");
            var accountantRole = _context.Roles.Single(r => r.NameLT == "CompanyAccountant");
            var coByCode = _context.Companies.ToDictionary(c => c.Code, c => c);

            void AddUser(User u)
            {
                if (!_context.Users.Any(x => x.Email == u.Email))
                    _context.Users.Add(u);
            }

            AddUser(new User { Id = 1, AuthUserId = 1, CompanyId = null, FirstName = "System", LastName = "Administrator", Email = "admin@example.com", Phone = "999999999", RoleId = adminRole.Id, IsCompanyAdmin = false, IsActive = false });

            // if (coByCode.TryGetValue("798000", out var c798000))
            // {
            //     AddUser(new User { Id = 1, AuthUserId = , CompanyId = c798000.Id, FirstName = "Evo", LastName = "Evo", Email = "mohamad.taha@evo-ly.com", Phone = "81611436", RoleId = mgrRole.Id, IsCompanyAdmin = true, IsActive = true });
            //     AddUser(new User { Id = 17, AuthUserId = 15, CompanyId = c798000.Id, FirstName = "Mohamad", LastName = "Taha", Email = "mhmd@gmail.com", Phone = "81611436", RoleId = accountantRole.Id, IsCompanyAdmin = false, IsActive = true });
            //     AddUser(new User { Id = 19, AuthUserId = 17, CompanyId = c798000.Id, FirstName = "moftah", LastName = "alobidy", Email = "moftah0550@gmail.com", Phone = "218924469845", RoleId = mgrRole.Id, IsCompanyAdmin = false, IsActive = false });
            //     AddUser(new User { Id = 20, AuthUserId = 18, CompanyId = c798000.Id, FirstName = "test1", LastName = "test", Email = "test@email.com", Phone = "218924469845", RoleId = _context.Roles.Single(r=>r.NameLT=="Viewer").Id, IsCompanyAdmin = false, IsActive = false });
            //     AddUser(new User { Id = 21, AuthUserId = 19, CompanyId = c798000.Id, FirstName = "test2", LastName = "test", Email = "test2@email.com", Phone = "218924469845", RoleId = _context.Roles.Single(r=>r.NameLT=="CompanyUser").Id, IsCompanyAdmin = false, IsActive = false });
            //     AddUser(new User { Id = 22, AuthUserId = 20, CompanyId = c798000.Id, FirstName = "test01", LastName = "test01", Email = "test01@email.com", Phone = "218924469845", RoleId = _context.Roles.Single(r=>r.NameLT=="Viewer").Id, IsCompanyAdmin = false, IsActive = true });
            //     AddUser(new User { Id = 23, AuthUserId = 21, CompanyId = c798000.Id, FirstName = "test02", LastName = "test020", Email = "test02@gmail.com", Phone = "0928831669", RoleId = _context.Roles.Single(r=>r.NameLT=="Viewer").Id, IsCompanyAdmin = false, IsActive = true });
            //     AddUser(new User { Id = 24, AuthUserId = 22, CompanyId = c798000.Id, FirstName = "taha1", LastName = "taha", Email = "tesst03@gmail.com", Phone = "12341", RoleId = _context.Roles.Single(r=>r.NameLT=="Viewer").Id, IsCompanyAdmin = false, IsActive = true });
            // }

            // if (coByCode.TryGetValue("725121", out var c725121))
            // {
            //     AddUser(new User { Id = 15, AuthUserId = 13, CompanyId = c725121.Id, FirstName = "testing121", LastName = "testing121", Email = "testing121@example.com", Phone = "81611436", RoleId = mgrRole.Id, IsCompanyAdmin = true, IsActive = false });
            // }

            // if (coByCode.TryGetValue("725005", out var c725005))
            // {
            //     AddUser(new User { Id = 16, AuthUserId = 14, CompanyId = c725005.Id, FirstName = "testing005", LastName = "test", Email = "mhmd.m.taha@gmail.com", Phone = "12312312", RoleId = mgrRole.Id, IsCompanyAdmin = true, IsActive = false });
            // }

            if (!_context.ChangeTracker.HasChanges()) return;

            // For explicit IDs above, enable identity insert just for Users within same connection/transaction
            var conn = _context.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) conn.Open();
            using (var tx = _context.Database.BeginTransaction())
            {
                _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Users] ON;");
                try
                {
                    _context.SaveChanges();
                }
                finally
                {
                    _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Users] OFF;");
                    tx.Commit();
                    if (!wasOpen) conn.Close();
                }
            }
        }

        private void SeedUserRolePermissions()
        {
            var users = _context.Users.ToList();
            foreach (var u in users)
            {
                var rolePerms = _context.RolePermissions.Where(rp => rp.RoleId == u.RoleId).ToList();
                foreach (var rp in rolePerms)
                {
                    if (!_context.UserRolePermissions.Any(urp => urp.UserId == u.Id && urp.RoleId == rp.RoleId && urp.PermissionId == rp.PermissionId))
                    {
                        _context.UserRolePermissions.Add(new UserRolePermission
                        {
                            UserId = u.Id,
                            RoleId = rp.RoleId,
                            PermissionId = rp.PermissionId
                        });
                    }
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




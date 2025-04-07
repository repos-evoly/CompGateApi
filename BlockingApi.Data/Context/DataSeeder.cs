using System;
using System.Collections.Generic;
using System.Linq;
using BlockingApi.Data.Models;
using BlockingApi.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlockingApi.Data.Seeding
{
    public class DataSeeder
    {
        private readonly BlockingApiDbContext _context;

        public DataSeeder(BlockingApiDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void Seed()
        {
            SeedRoles();
            SeedPermissions();
            SeedAreas();
            SeedBranches();
            SeedReasons();
            SeedSources();
            SeedAdminUser();
            SeedCustomers();
            SeedRolePermissions();
            SeedSettings();
        }

        #region Role Seeding
        private void SeedRoles()
        {
            if (!_context.Roles.Any())
            {
                var roles = new List<Role>
        {
            new() { NameLT = "SuperAdmin",  NameAR = "SuperAdminAR",Description = "Full control over the system" },
            new() { NameLT = "Admin",NameAR = "AdminAR", Description = "Manages users, roles, and transactions" },
            new() { NameLT = "Manager",NameAR = "المدير", Description = "Manages a specific area or department" },
            new() { NameLT = "AssistantManager",NameAR = "مساعد المدير", Description = "Assists the Manager in overseeing operations" },
            new() { NameLT = "DeputyManager", NameAR = "نائب المدير", Description = "Assists with managerial tasks and decision-making" },
            new() { NameLT = "Maker",NameAR = "رئيس قسم", Description = "Handles transaction creation and data entry" },
            new() { NameLT = "Checker", NameAR = "مراقب 1", Description = "Reviews and approves transactions" },
            new() { NameLT = "Viewer",NameAR = "مراقب 2", Description = "Can only view information, no modification rights" },
            new() { NameLT = "Auditor",NameAR = "مراقب 3", Description = "Can only view audit logs and system changes" }
        };

                _context.Roles.AddRange(roles);
                _context.SaveChanges();
            }
        }

        #endregion

        #region Permission Seeding
        private void SeedPermissions()
        {
            if (!_context.Permissions.Any())
            {
                var permissions = new List<Permission>
        {
            new() { Name = "BlockPermission", Description = "Can block a customer" },
            new() { Name = "UnblockPermission", Description = "Can unblock a customer" },
            new() { Name = "ViewBlockedCustomers", Description = "Can view blocked customers" },
            new() { Name = "ViewUnblockedCustomers", Description = "Can view unblocked customers" },
            new() { Name = "ViewCustomers", Description = "Can view customer details" },
            new() { Name = "ManageUsers", Description = "Can add, edit, and delete users" },
            new() { Name = "ManageAreas", Description = "Can manage areas" },
            new() { Name = "ManageBranches", Description = "Can manage branches" },
            new() { Name = "ManageReasons", Description = "Can manage reasons for actions" },
            new() { Name = "ManageSources", Description = "Can manage sources of transactions" },
            new() { Name = "ApproveTransactions", Description = "Can approve transactions" },
            new() { Name = "ViewAuditLogs", Description = "Can view audit logs" },
            new() { Name = "ManageDocuments", Description = "Can manage documents" },
            new() { Name = "ViewDocuments", Description = "Can view documents" },
            new() { Name = "ManageTransactions", Description = "Can manage transactions" },
            new() { Name = "CanDashboard", Description = "Can Dashboard" },
            new() { Name = "CanTransactions", Description = "Can Transactions" },
            new() { Name = "CanBlock", Description = "Can Block" },
            new() { Name = "CanUnblock", Description = "Can Unblock" },
            new() { Name = "CanUsers", Description = "Can Users" },
            new() { Name = "CanDefinitions", Description = "Definitions" },
            new() { Name = "CanArea", Description = "Can Area" },
            new() { Name = "CanBranches", Description = "Can Branches" },
            new() { Name = "CanReasons", Description = "Can Reasons" },
            new() { Name = "CanSources", Description = "Can Sources" },
            new() { Name = "CanRoles", Description = "Can Roles" },
            new() { Name = "CanPermissions", Description = "Can Permissions" },
            new() { Name = "CanDocuments", Description = "Can Documents" },
            new() { Name = "CanBankDocuments", Description = "Can Bank Documents" },
            new() { Name = "CanUserActivity", Description = "Can Activity" },
            new() { Name = "CanSettings", Description = "Can Settings" },

        };

                _context.Permissions.AddRange(permissions);
                _context.SaveChanges();
            }
        }

        #endregion

        #region Settings Seeding
        private void SeedSettings()
        {
            if (!_context.Settings.Any())
            {
                var settings = new Settings
                {
                    TransactionAmount = 50000,              // Adjust as needed
                    TransactionAmountForeign = 10000,         // Adjust as needed
                    TransactionTimeTo = "10",             // Example value
                    TimeToIdle = "15"                        // Example: idle time in minutes
                };

                _context.Settings.Add(settings);
                _context.SaveChanges();
            }
        }
        #endregion

        private void SeedRolePermissions()
        {
            if (!_context.RolePermissions.Any())
            {
                var roles = _context.Roles.ToList();
                var permissions = _context.Permissions.ToList();
                var rolePermissions = new List<RolePermission>();

                // Define the exclusion sets for Maker and for Checker/Auditor/Viewer
                var makerExclusions = new HashSet<string>(new[]
                {
            "blockpermission", "unblockpermission", "cansettings", "canusers", "cansources", "canreasons", "canarea", "canbranches", "canblock" , "canunblock" , "candefinitions"
        });

                var otherExclusions = new HashSet<string>(new[]
                {
            "blockpermission", "unblockpermission", "cansettings", "canusers", "cansources", "canreasons", "canuseractivity", "canarea", "canbranches", "canblock" , "canunblock"
        });

                foreach (var role in roles)
                {
                    // Normalize role name for comparison.
                    string roleName = role.NameLT?.ToLower() ?? string.Empty;

                    // For SuperAdmin, Admin, Manager, AssistantManager, DeputyManager: assign all permissions.
                    if (roleName == "superadmin" ||
                        roleName == "admin" ||
                        roleName == "manager" ||
                        roleName == "assistantmanager" ||
                        roleName == "deputymanager")
                    {
                        foreach (var perm in permissions)
                        {
                            rolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
                        }
                    }
                    else if (roleName == "maker")
                    {
                        // Maker gets all permissions except those in makerExclusions.
                        foreach (var perm in permissions)
                        {
                            if (!makerExclusions.Contains(perm.Name.ToLower()))
                            {
                                rolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
                            }
                        }
                    }
                    else if (roleName == "checker" ||
                             roleName == "auditor" ||
                             roleName == "viewer")
                    {
                        // Checker, Auditor, Viewer get all permissions except those in otherExclusions.
                        foreach (var perm in permissions)
                        {
                            if (!otherExclusions.Contains(perm.Name.ToLower()))
                            {
                                rolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
                            }
                        }
                    }
                }

                _context.RolePermissions.AddRange(rolePermissions);
                _context.SaveChanges();
            }
        }




        #region Area Seeding
        private void SeedAreas()
        {
            if (!_context.Areas.Any())
            {
                var areas = new List<Area>
                {
                    new() { Name = "المنطقة الغربية" },
                    new() { Name = "المنطقة الشرقية" },
                    new() { Name = "فروع بنغازي" },
                    new() { Name = "فروع المنطقة الوسطى والجنوبية" }
                };

                _context.Areas.AddRange(areas);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Branch Seeding
        private void SeedBranches()
        {
            if (!_context.Branches.Any())
            {
                var defaultAreaId = _context.Areas.FirstOrDefault()?.Id ?? 1;

                var branches = new List<Branch>
                {
                    new() { CABBN = "0010", Name = "الادارة العامة", Address = "Head Office", Phone = "000000001", AreaId = defaultAreaId },
                    new() { CABBN = "0011", Name = "الفرع الرئيسي-بنغازي", Address = "Benghazi Main Branch", Phone = "000000002", AreaId = defaultAreaId },
                    new() { CABBN = "0012", Name = "فرع الفروسية", Address = "Equestrian Branch", Phone = "000000003", AreaId = defaultAreaId },
                    new() { CABBN = "0013", Name = "مطار بنينة الدولي", Address = "Benina Airport Branch", Phone = "000000004", AreaId = defaultAreaId },
                    new() { CABBN = "0014", Name = "وكالة الدعوة الاسلامية", Address = "Islamic Call Agency", Phone = "000000005", AreaId = defaultAreaId },
                    new() { CABBN = "0015", Name = "فرع البركة", Address = "Baraka Branch", Phone = "000000006", AreaId = defaultAreaId },
                    new() { CABBN = "0016", Name = "وكالة جالو", Address = "Jalo Agency", Phone = "000000007", AreaId = defaultAreaId },
                    new() { CABBN = "0017", Name = "فرع الحدائق", Address = "Gardens Branch", Phone = "000000008", AreaId = defaultAreaId },
                    new() { CABBN = "0018", Name = "وكالة الاطفال", Address = "Children’s Agency", Phone = "000000009", AreaId = defaultAreaId },
                    new() { CABBN = "0021", Name = "فرع رئيسي-طرابلس", Address = "Tripoli Main Branch", Phone = "000000010", AreaId = defaultAreaId },
                    new() { CABBN = "0022", Name = "وكالة غوط الشعال", Address = "Ghout Shaal Agency", Phone = "000000011", AreaId = defaultAreaId },
                    new() { CABBN = "0023", Name = "وكالة برج طرابلس", Address = "Tripoli Tower Agency", Phone = "000000012", AreaId = defaultAreaId },
                    new() { CABBN = "0024", Name = "مطار طرابلس العالمي", Address = "Tripoli Airport Branch", Phone = "000000013", AreaId = defaultAreaId },
                    new() { CABBN = "0025", Name = "فرع قرقارش", Address = "Gargaresh Branch", Phone = "000000014", AreaId = defaultAreaId },
                    new() { CABBN = "0026", Name = "فرع ذات العماد", Address = "That Al-Imad Branch", Phone = "000000015", AreaId = defaultAreaId },
                    new() { CABBN = "0027", Name = "وكالة الفندق الكبير", Address = "Grand Hotel Agency", Phone = "000000016", AreaId = defaultAreaId },
                    new() { CABBN = "0029", Name = "وكالة المدار", Address = "Al-Madar Agency", Phone = "000000017", AreaId = defaultAreaId },
                    new() { CABBN = "0031", Name = "فرع رئيسي-مصراتة", Address = "Misrata Main Branch", Phone = "000000018", AreaId = defaultAreaId },
                    new() { CABBN = "0032", Name = "وكالة قصر أحمد-مصراتة", Address = "Qasr Ahmed Agency, Misrata", Phone = "000000019", AreaId = defaultAreaId },
                    new() { CABBN = "0051", Name = "فرع الزاوية الرئيسي", Address = "Az-Zawiya Main Branch", Phone = "000000020", AreaId = defaultAreaId },
                    new() { CABBN = "0052", Name = "وكالة زوارة", Address = "Zuwara Agency", Phone = "000000021", AreaId = defaultAreaId },
                    new() { CABBN = "0071", Name = "فرع زليتن الرئيسي", Address = "Zliten Main Branch", Phone = "000000022", AreaId = defaultAreaId },
                    new() { CABBN = "0111", Name = "فرع الفويهات", Address = "Fouihat Branch", Phone = "000000023", AreaId = defaultAreaId },
                    new() { CABBN = "0041", Name = "فرع طبرق الرئيسي", Address = "Tobruk Main Branch", Phone = "000000024", AreaId = defaultAreaId },
                    new() { CABBN = "0061", Name = "فرع البيضاء الرئيسي", Address = "Al-Bayda Main Branch", Phone = "000000025", AreaId = defaultAreaId },
                    new() { CABBN = "0112", Name = "فرع الوحدة العربية", Address = "Arab Unity Branch", Phone = "000000026", AreaId = defaultAreaId },
                    new() { CABBN = "0019", Name = "فرع اجدابيا الرئيسي", Address = "Ajdabiya Main Branch", Phone = "000000027", AreaId = defaultAreaId },
                    new() { CABBN = "0042", Name = "وكالة امساعد الحدودية", Address = "Umsaad Border Agency", Phone = "000000028", AreaId = defaultAreaId },
                    new() { CABBN = "0081", Name = "فرع سرت الرئيسي", Address = "Sirte Main Branch", Phone = "000000029", AreaId = defaultAreaId },
                    new() { CABBN = "0221", Name = "فرع سوق الجمعة", Address = "Souq Al-Jumaa Branch", Phone = "000000030", AreaId = defaultAreaId },
                    new() { CABBN = "0091", Name = "فرع درنة", Address = "Derna Branch", Phone = "000000031", AreaId = defaultAreaId },
                    new() { CABBN = "0222", Name = "وكالة تاجوراء", Address = "Tajoura Agency", Phone = "000000032", AreaId = defaultAreaId },
                    new() { CABBN = "0123", Name = "المصرف الافتراضي", Address = "Virtual Bank", Phone = "000000033", AreaId = defaultAreaId },
                    new() { CABBN = "0101", Name = "فرع الخمس الرئيسي", Address = "Khums Main Branch", Phone = "000000034", AreaId = defaultAreaId },
                    new() { CABBN = "0113", Name = "وكالة أوجلة", Address = "Awjila Agency", Phone = "000000035", AreaId = defaultAreaId },
                    new() { CABBN = "0124", Name = "المصرف المتحرك", Address = "Mobile Bank", Phone = "000000036", AreaId = defaultAreaId },
                    new() { CABBN = "0201", Name = "فرع راس لانوف", Address = "Ras Lanuf Branch", Phone = "000000037", AreaId = defaultAreaId }
                };

                _context.Branches.AddRange(branches);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Transaction Type Seeding

        #endregion

        #region Reason Seeding
        private void SeedReasons()
        {
            if (!_context.Reasons.Any())
            {
                var reasons = new List<Reason>
                {
                    new() { NameLT = "Fraudulent Activity", NameAR = "نشاط احتيالي" },
                    new() { NameLT = "Suspicious Transactions", NameAR = "معاملات مشبوهة" },
                    new() { NameLT = "Regulatory Violation", NameAR = "انتهاك تنظيمي" }
                };

                _context.Reasons.AddRange(reasons);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Source Seeding
        private void SeedSources()
        {
            if (!_context.Sources.Any())
            {
                var sources = new List<Source>
                {
                    new() { NameLT = "System Rule", NameAR = "قاعدة النظام" },
                    new() { NameLT = "Manual Review", NameAR = "مراجعة يدوية" },
                    new() { NameLT = "Customer Complaint", NameAR = "شكوى العميل" }
                };

                _context.Sources.AddRange(sources);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Admin User Seeding
        private void SeedAdminUser()
        {
            if (!_context.Users.Any(u => u.Email == "admin@example.com"))
            {
                var adminRole = _context.Roles.FirstOrDefault(r => r.NameLT == "Admin");
                if (adminRole == null) return;

                var mainBranch = _context.Branches.FirstOrDefault();
                if (mainBranch == null) return;

                var adminUser = new User
                {
                    FirstName = "Admin",
                    LastName = "User",
                    Email = "admin@example.com",
                    Phone = "999999999",
                    RoleId = adminRole.Id,
                    BranchId = mainBranch.Id
                };

                _context.Users.Add(adminUser);
                _context.SaveChanges();

                // 🔹 Automatically assign role-based permissions
                var rolePermissions = _context.RolePermissions.Where(rp => rp.RoleId == adminRole.Id).ToList();

                var userRolePermissions = rolePermissions.Select(rp => new UserRolePermission
                {
                    UserId = adminUser.Id,
                    RoleId = rp.RoleId,
                    PermissionId = rp.PermissionId
                }).ToList();

                _context.UserRolePermissions.AddRange(userRolePermissions);
                _context.SaveChanges();
            }
        }

        #endregion

        #region Customer Seeding
        private void SeedCustomers()
        {
            if (!_context.Customers.Any())
            {
                var customers = new List<Customer>
                {
                    new() { CID = "C12345", FirstName = "John", LastName = "Doe", Email = "johndoe@example.com", Phone = "123456789", NationalId = "A123456", BranchId = 1 },
                    new() { CID = "C67890", FirstName = "Jane", LastName = "Smith", Email = "janesmith@example.com", Phone = "987654321", NationalId = "B789123", BranchId = 2 }
                };

                _context.Customers.AddRange(customers);
                _context.SaveChanges();
            }
        }
        #endregion

        #region Public Method to Run Seeder
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using var context = serviceProvider.GetRequiredService<BlockingApiDbContext>();
            var seeder = new DataSeeder(context);
            seeder.Seed();
        }
        #endregion
    }
}

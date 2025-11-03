using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CompGateApi.Data.Context;
using CompGateApi.Core.Dtos;

using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.DependencyInjection;
using CompGateApi.Core.Filters;
using CompGateApi.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using CompGateApi.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using CompGateApi.Data.Seeding;
using CompGateApi.Data.Repositories;
using CompGateApi.Data.Abstractions;
using CompGateApi.Validators;
using CompGateApi.Core.Validators;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;




namespace CompGateApi.Core.Startup
{
  public static class DependencyInjectionSetup
  {
    public static WebApplicationBuilder RegisterServices(this WebApplicationBuilder builder)
    {
      var issuer = builder.Configuration["Jwt:Issuer"] ?? throw new ArgumentNullException("Jwt:Issuer is missing in configuration.");
      var audience = builder.Configuration["Jwt:Audience"] ?? throw new ArgumentNullException("Jwt:Audience is missing in configuration.");
      var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key is missing in configuration.");

      builder.Services.RegisterCors();
      builder.Services.RegisterSwagger();
      builder.Services.RegisterAuths(issuer, audience, jwtKey);
      if (builder.Environment.IsDevelopment())
      {
        builder.Services.AddDbContext<CompGateApiDbContext>(opt => opt.UseSqlServer(builder.Configuration["ConnectionStrings:DevConnection"]));
      }
      else if (builder.Environment.IsStaging())
      {
        builder.Services.AddDbContext<CompGateApiDbContext>(opt => opt.UseSqlServer(builder.Configuration["ConnectionStrings:StagingConnection"]));
      }
      else
      {
        builder.Services.AddDbContext<CompGateApiDbContext>(opt =>
        {
          opt.UseSqlServer(builder.Configuration["ConnectionStrings:ProdConnection"]);
        });
      }
      builder.Services.AddHttpContextAccessor();
      builder.Services.AddTransient<DataSeeder>();
      builder.Services.AddAutoMapper(typeof(MappingConfig));
      builder.Services.Configure<JsonOptions>(options =>
      {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.WriteIndented = true;
        options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
      });

      builder.Services.RegisterValidators();
      builder.Services.AddSignalR();

      // Optionally, add your NotificationService to DI:

      builder.Services.RegisterRepos();
      return builder;
    }

    public static IServiceCollection RegisterCors(this IServiceCollection cors)
    {
      cors.AddCors(options =>
            {
              options.AddPolicy("AllowSpecificOrigins",
                    builder =>
                    {
                      builder.WithOrigins("http://localhost:3000",
                                          "http://localhost:3012",
                                          "http://10.3.3.11:3012",
                                          "http://10.3.3.11:3013",
                                          "http://localhost:3012",
                                          "http://10.3.3.11:3012",
                                          "http://localhost:5000",
                                          "http://10.1.1.205:3012",
                                         "http://192.168.0.245:3012",
                                         "http://192.168.0.245:3013",
                                          "http://localhost:3013",
                                          "https://webanking.bcd.ly/Companygw",
                                          "http://192.168.113.10",
                                          "http://192.168.113.10:3012",
                                          "http://192.168.113.11",
                                          "http://192.168.113.11:3012",
                                          "http://10.1.1.205:3013")
                             .AllowAnyHeader()
                             .AllowAnyMethod()
                             .AllowCredentials();
                    });
            });
      return cors;
    }

    public static IServiceCollection RegisterAuths(
           this IServiceCollection services,
           string issuer,
           string audience,
           string jwtKey)
    {
      // keep "nameid" and "role" as-is
      JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
      JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

      services.AddAuthentication(options =>
      {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
      })
      .AddJwtBearer(options =>
      {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        // use System.IdentityModel handler only
        options.TokenHandlers.Clear();
        options.TokenHandlers.Add(new JwtSecurityTokenHandler());

        options.TokenValidationParameters = new TokenValidationParameters
        {
          NameClaimType = "nameid",
          RoleClaimType = "role",
          ValidateIssuer = true,
          ValidIssuer = issuer,
          ValidateAudience = true,
          ValidAudience = audience,
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
          ValidateLifetime = true,
          ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
          OnMessageReceived = ctx =>
                {
                  var hdr = ctx.Request.Headers["Authorization"].FirstOrDefault();
                  if (!string.IsNullOrEmpty(hdr) && hdr.StartsWith("Bearer "))
                    ctx.Token = hdr.Substring("Bearer ".Length).Trim();
                  return Task.CompletedTask;
                },
          OnAuthenticationFailed = ctx =>
                {
                  var logger = ctx.HttpContext.RequestServices
                                       .GetRequiredService<ILoggerFactory>()
                                       .CreateLogger("JwtBearer");
                  logger.LogError(ctx.Exception, "JWT authentication failed");
                  return Task.CompletedTask;
                },
          OnTokenValidated = ctx =>
         {
           var logger = ctx.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("JwtBearer");

           // pull out all of the "role" claims
           var roles = ctx.Principal?
               .FindAll(ctx.Options.TokenValidationParameters.RoleClaimType ?? "role")
               .Select(c => c.Value)
               .ToArray()
             ?? Array.Empty<string>();

           logger.LogInformation("Token validated. Roles = {Roles}", string.Join(", ", roles));
           return Task.CompletedTask;
         }


        };
      });

      services.AddAuthorization(opts =>
      {
        // company-level
        opts.AddPolicy("RequireCompanyUser", p =>
                  p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                   .RequireAuthenticatedUser());

        // Only COMPANY-ADMIN (CompanyManager) may manage employees
        opts.AddPolicy("RequireCompanyAdmin", p =>
        p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .RequireRole("CompanyManager"));

        opts.AddPolicy("CanRequestCheckBook", p =>
                  p.RequireRole("CompanyManager", "Accountant", "Maker"));

        opts.AddPolicy("CanRequestRTGS", p =>
                  p.RequireRole("CompanyManager", "Accountant", "Maker"));
        opts.AddPolicy("CanRequestCBL", p =>
                  p.RequireRole("CompanyManager", "Accountant", "Maker"));
        opts.AddPolicy("CanRequestCheck", p =>
                  p.RequireRole("CompanyManager", "Accountant", "Maker"));



        // admin-level
        opts.AddPolicy("RequireAdminUser", p =>
                  p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                   .RequireAuthenticatedUser());

        opts.AddPolicy("AdminAccess", p =>
                  p.RequireRole("SuperAdmin", "Admin", "Support", "Auditor"));
      });

      return services;
    }


    public static IServiceCollection RegisterValidators(this IServiceCollection validators)
    {

      validators.AddScoped<IValidator<DefinitionCreateDto>, DefinitionCreateDtoValidator>();
      validators.AddScoped<IValidator<DefinitionUpdateDto>, DefinitionUpdateDtoValidator>();
      // validators.AddScoped<IValidator<TransactionCreateDto>, TransactionCreateDtoValidator>();
      // validators.AddScoped<IValidator<TransactionUpdateDto>, TransactionUpdateDtoValidator>();
      validators.AddScoped<IValidator<CurrencyCreateDto>, CurrencyCreateDtoValidator>();
      validators.AddScoped<IValidator<CurrencyUpdateDto>, CurrencyUpdateDtoValidator>();
      validators.AddScoped<IValidator<CheckBookRequestCreateDto>, CheckBookRequestCreateDtoValidator>();
      validators.AddScoped<IValidator<CheckBookRequestStatusUpdateDto>, CheckBookRequestStatusUpdateDtoValidator>();
      validators.AddScoped<IValidator<CheckRequestCreateDto>, CheckRequestCreateDtoValidator>();
      validators.AddScoped<IValidator<CheckRequestStatusUpdateDto>, CheckRequestStatusUpdateDtoValidator>();
      validators.AddScoped<IValidator<RtgsRequestCreateDto>, RtgsRequestCreateDtoValidator>();
      validators.AddScoped<IValidator<RtgsRequestStatusUpdateDto>, RtgsRequestStatusUpdateDtoValidator>();
      validators.AddScoped<IValidator<CblRequestCreateDto>, CblRequestCreateDtoValidator>();
      validators.AddScoped<IValidator<CblRequestStatusUpdateDto>, CblRequestStatusUpdateDtoValidator>();
      validators.AddScoped<IValidator<TransferRequestCreateDto>,
                        TransferRequestCreateDtoValidator>();
      validators.AddScoped<IValidator<TransferRequestStatusUpdateDto>,
                           TransferRequestStatusUpdateDtoValidator>();
      validators.AddScoped<IValidator<ServicePackageCreateDto>, ServicePackageCreateDtoValidator>();
      validators.AddScoped<IValidator<ServicePackageUpdateDto>, ServicePackageUpdateDtoValidator>();
      validators.AddScoped<IValidator<ForeignTransferCreateDto>, ForeignTransferCreateDtoValidator>();
      validators.AddScoped<IValidator<ForeignTransferStatusUpdateDto>, ForeignTransferStatusUpdateDtoValidator>();
      validators.AddScoped<IValidator<VisaRequestCreateDto>, VisaRequestCreateDtoValidator>();
      validators.AddScoped<IValidator<VisaRequestStatusUpdateDto>, VisaRequestStatusUpdateDtoValidator>();
      validators.AddScoped<IValidator<CompanyEmployeeRegistrationDto>, CompanyEmployeeRegistrationDtoValidator>();
      validators.AddScoped<IValidator<CreditFacilitiesOrLetterOfGuaranteeRequestCreateDto>, CreditFacilitiesOrLetterOfGuaranteeRequestCreateDtoValidator>();
      validators.AddScoped<IValidator<CreditFacilitiesOrLetterOfGuaranteeRequestStatusUpdateDto>, CreditFacilitiesOrLetterOfGuaranteeRequestStatusUpdateDtoValidator>();
      validators.AddScoped<IValidator<CertifiedBankStatementRequestCreateDto>, CertifiedBankStatementRequestCreateDtoValidator>();
      validators.AddScoped<IValidator<CertifiedBankStatementRequestStatusUpdateDto>, CertifiedBankStatementRequestStatusUpdateDtoValidator>();
      validators.AddScoped<IValidator<EdfaaliRequestCreateDto>, EdfaaliRequestCreateDtoValidator>();
      validators.AddScoped<IValidator<EdfaaliRequestStatusUpdateDto>, EdfaaliRequestStatusUpdateDtoValidator>();


      return validators;
    }

    public static IServiceCollection RegisterRepos(this IServiceCollection services)
    {
      services.AddEndpointsApiExplorer();

      // Generic
      services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

      // --- Add this typed‐client registration back in FIRST ---
      services.AddHttpClient<IUserRepository, UserRepository>();

      services.AddHttpClient("AuthApi", c =>
      {
        c.BaseAddress = new Uri("http://10.1.1.205/compauthapi/");
        // (optional) c.DefaultRequestHeaders.Add("Accept", "application/json");
      });

      services.AddHttpClient("BankApi", client =>
      {
        client.BaseAddress = new Uri("http://10.1.1.205:7070");
        client.DefaultRequestHeaders.Accept.Add(
              new MediaTypeWithQualityHeaderValue("application/json"));
      });

      services.AddHttpClient("KycApi", c =>
      {
        c.BaseAddress = new Uri("http://10.1.1.205");
        c.DefaultRequestHeaders.Accept.Add(
          new MediaTypeWithQualityHeaderValue("application/json"));
      });

      // Core
      services.AddScoped<IUserRepository, UserRepository>();
      services.AddScoped<IRoleRepository, RoleRepository>();
      services.AddScoped<IAuditLogRepository, AuditLogRepository>();

      // Settings & Definitions
      services.AddScoped<ISettingsRepository, SettingsRepository>();
      // services.AddScoped<IDefinitionRepository, DefinitionRepository>();
      // services.AddScoped<ITransactionRepository, TransactionRepository>();
      services.AddScoped<ITransferRequestRepository, TransferRequestRepository>();
      services.AddScoped<ICurrencyRepository, CurrencyRepository>();
      // services.AddScoped<IReasonRepository, ReasonRepository>();

      // Bank Accounts
      services.AddScoped<IBankAccountRepository, BankAccountRepository>();

      //attachments
      services.AddScoped<IAttachmentRepository, AttachmentRepository>();

      // Service Packages
      services.AddScoped<IServicePackageRepository, ServicePackageRepository>();
      // services.AddScoped<IServicePackageDetailRepository, ServicePackageDetailRepository>();
      services.AddScoped<ITransactionCategoryRepository, TransactionCategoryRepository>();
      // services.AddScoped<ITransferLimitRepository, TransferLimitRepository>();


      // CBL Request
      services.AddScoped<ICblRequestRepository, CblRequestRepository>();

      // Check-Book Request
      services.AddScoped<ICheckBookRequestRepository, CheckBookRequestRepository>();

      // Check Request
      services.AddScoped<ICheckRequestRepository, CheckRequestRepository>();

      // RTGS Request
      services.AddScoped<IRtgsRequestRepository, RtgsRequestRepository>();

      // Notifications
      //services.AddScoped<INotificationRepository, NotificationRepository>();

      // Unit of Work
      services.AddScoped<IUnitOfWork, UnitOfWork>();

      services.AddScoped<ICompanyRepository, CompanyRepository>();

      services.AddScoped<IVisaRequestRepository, VisaRequestRepository>();

      services.AddScoped<IForeignTransferRepository, ForeignTransferRepository>();

      services.AddScoped<ICreditFacilitiesOrLetterOfGuaranteeRequestRepository, CreditFacilitiesOrLetterOfGuaranteeRequestRepository>();

      services.AddScoped<ICertifiedBankStatementRequestRepository, CertifiedBankStatementRequestRepository>();
      services.AddScoped<IEdfaaliRequestRepository, EdfaaliRequestRepository>();
      //economic sector
      services.AddScoped<IEconomicSectorRepository, EconomicSectorRepository>();
      //representatives
      services.AddScoped<IRepresentativeRepository, RepresentativeRepository>();
      // FormStatus
      services.AddScoped<IFormStatusRepository, FormStatusRepository>();
      // Beneficiaries
      services.AddScoped<IBeneficiaryRepository, BeneficiaryRepository>();
      //EmployeeSalary
      services.AddScoped<IEmployeeSalaryRepository, EmployeeSalaryRepository>();

      services.AddScoped<IPricingRepository, PricingRepository>();

      services.AddScoped<IVisaRepository, VisaRepository>();

      services.AddScoped<IGenericTransferRepository, GenericTransferRepository>();

      return services;
    }


    public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
          //services.AddHostedService<EscalationTimeoutService>();
          // Add other services, repositories, etc.
        });


    public static IServiceCollection RegisterSwagger(this IServiceCollection services)
    {
      services.AddSwaggerGen(options =>
          {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
              Scheme = "Bearer",
              BearerFormat = "JWT",
              In = ParameterLocation.Header,
              Name = "Authorization",
              Description = "Bearer Authentication with JWT Token",
              Type = SecuritySchemeType.Http
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
              {
                new OpenApiSecurityScheme
                {
                  Reference = new OpenApiReference
                  {
                    Id = "Bearer",
                    Type = ReferenceType.SecurityScheme
                  }
                },
                new List<string>()
              }
            });
            options.OperationFilter<FileUploadOperationFilter>();
          });

      return services;
    }
  }
}



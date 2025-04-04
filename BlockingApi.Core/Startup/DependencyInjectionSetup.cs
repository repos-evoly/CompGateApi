using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlockingApi.Data.Context;
using BlockingApi.Core.Dtos;

using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.DependencyInjection;
using BlockingApi.Core.Filters;
using BlockingApi.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using BlockingApi.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using BlockingApi.Data.Seeding;
using BlockingApi.Abstractions;
using BlockingApi.Repositories;
using BlockingApi.Data.Repositories;
using BlockingApi.Data.Abstractions;
using BlockingApi.Core.Services;


namespace BlockingApi.Core.Startup
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
        builder.Services.AddDbContext<BlockingApiDbContext>(opt => opt.UseSqlServer(builder.Configuration["ConnectionStrings:DevConnection"]));
      }
      else if (builder.Environment.IsStaging())
      {
        builder.Services.AddDbContext<BlockingApiDbContext>(opt => opt.UseSqlServer(builder.Configuration["ConnectionStrings:StagingConnection"]));
      }
      else
      {
        builder.Services.AddDbContext<BlockingApiDbContext>(opt =>
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
      builder.Services.AddScoped<NotificationService>();
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
                                          "http://localhost:3010",
                                          "http://10.3.3.11:3010",
                                          "http://localhost:5000",
                                          "http://10.1.1.205:3010")
                             .AllowAnyHeader()
                             .AllowAnyMethod()
                             .AllowCredentials();
                    });
            });
      return cors;
    }

    public static IServiceCollection RegisterAuths(this IServiceCollection auth, string issuer, string audience, string jwtKey)
    {
      auth.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
          .AddJwtBearer(options =>
          {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters()
            {
              ValidateIssuer = true,
              ValidateAudience = true,
              ValidateLifetime = true,
              ValidateIssuerSigningKey = true,

              ValidIssuer = issuer,
              ValidAudience = audience,
              IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };

            options.Events = new JwtBearerEvents
            {
              OnMessageReceived = context =>
                {
                  // Get token from Authorization header
                  var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                  if (authHeader != null && authHeader.StartsWith("Bearer "))
                  {
                    context.Token = authHeader.Substring("Bearer ".Length).Trim();
                  }
                  return Task.CompletedTask;
                }
            };
          });

      auth.AddAuthorization(a =>
     {
       a.AddPolicy("requireAuthUser", b => b
             .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
             .RequireAuthenticatedUser());


       a.AddPolicy("BlockPermission", b => b.RequireRole(
      "SuperAdmin",
      "Admin",
      "Maker",
      "Manager",
      "AssistantManager",
      "DeputyManager",
      "Checker",
      "Viewer",
      "Auditor"));

       a.AddPolicy("UnblockPermission", b => b.RequireRole(
           "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ViewBlockedCustomers", b => b.RequireRole(
           "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ViewUnblockedCustomers", b => b.RequireRole(
           "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ViewCustomers", b => b.RequireRole(
           "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ManageUsers", b => b.RequireRole(
            "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ManageAreas", b => b.RequireRole(
            "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ManageBranches", b => b.RequireRole(
           "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ManageReasons", b => b.RequireRole(
           "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ManageSources", b => b.RequireRole(
          "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ApproveTransactions", b => b.RequireRole(
           "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ViewAuditLogs", b => b.RequireRole(
          "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ManageDocuments", b => b.RequireRole(
          "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ViewDocuments", b => b.RequireRole(
          "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("ManageTransactions", b => b.RequireRole(
           "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));

       a.AddPolicy("EscalateTransactions", b => b.RequireRole(
           "SuperAdmin",
           "Admin",
           "Maker",
           "Manager",
           "AssistantManager",
           "DeputyManager",
           "Checker",
           "Viewer",
           "Auditor"));
     });
      return auth;
    }


    public static IServiceCollection RegisterValidators(this IServiceCollection validators)
    {



      return validators;
    }

    public static IServiceCollection RegisterRepos(this IServiceCollection services)
    {
      services.AddEndpointsApiExplorer();
      services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

      services.AddHttpClient<IExternalApiRepository, ExternalApiRepository>();
      services.AddHttpClient<IKycApiRepository, KycApiRepository>();
      services.AddScoped<ICustomerRepository, CustomerRepository>();
      services.AddScoped<IUserRepository, UserRepository>();
      services.AddScoped<IRoleRepository, RoleRepository>();
      services.AddScoped<IDocumentRepository, DocumentRepository>();
      services.AddScoped<IUserActivityRepository, UserActivityRepository>();
      services.AddScoped<IExternalTransactionRepository, ExternalTransactionRepository>();
      services.AddScoped<ITransactionRepository, TransactionRepository>();
      services.AddScoped<ISettingsRepository, SettingsRepository>();
      services.AddScoped<IBranchRepository, BranchRepository>();
      services.AddScoped<INotificationRepository, NotificationRepository>();
      services.AddScoped<ITransactionFlowRepository, TransactionFlowRepository>();
      services.AddHttpClient<IUserRepository, UserRepository>();
      services.AddScoped<IAuditLogRepository, AuditLogRepository>();



      services.AddTransient<IUnitOfWork, UnitOfWork>();

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
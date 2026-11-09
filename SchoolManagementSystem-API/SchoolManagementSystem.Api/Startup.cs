using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Middlewares;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Application.Services.Auth;
using SchoolManagementSystem.Application.Services.Dashboard;
using SchoolManagementSystem.Application.Services.Org;
using SchoolManagementSystem.Application.Services.Roles;
using SchoolManagementSystem.Application.Services.Users;
using SchoolManagementSystem.Application.Utilities.Security;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Configuration;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.Entities.Identity;
using SchoolManagementSystem.Infrastructure;
using SchoolManagementSystem.Infrastructure.Seeding;

namespace SchoolManagementSystem.Api;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<RefreshCookieOptions>(configuration.GetSection(RefreshCookieOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        services.AddDbContext<SchoolManagementSystemDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Default"));
            options.ConfigureWarnings(w =>
                w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddScoped<CampusScope>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<SchoolManagementSystemRepositories>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<RefreshCookie>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddSingleton<IPermissionChecker, PermissionChecker>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICampusService, CampusService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ISchoolService, SchoolService>();
        services.AddScoped<IDashboardService, DashboardService>();

        AddAuthentication(services);
        AddWeb(services);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddHostedService<DatabaseInitializerService>();
    }

    private static readonly string VersionLine = "SchoolManagementSystem API v" +
        (typeof(Startup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
         ?? typeof(Startup).Assembly.GetName().Version?.ToString(3));

    public void Configure(IApplicationBuilder app)
    {
        app.UseExceptionHandler();

        if (environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseMiddleware<CampusScopeMiddleware>();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/", () => Results.Text(VersionLine)).AllowAnonymous().ExcludeFromDescription();
        });
    }

    private void AddAuthentication(IServiceCollection services)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters (use user-secrets or env).");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep JWT claim names as issued (sub/role). Default inbound mapping breaks HasPermission + CurrentUserId.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new ResponseDto<object>(null, 401, "Unauthorized."));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new ResponseDto<object>(null, 403, "Forbidden."));
                    }
                };
            });

        services.AddAuthorization();
    }

    private void AddWeb(IServiceCollection services)
    {
        services.AddControllers(options =>
            {
                options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer()));
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var message = context.ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
                              ?? "Validation failed.";
                return new BadRequestObjectResult(new ResponseDto<object>(null, 400, message));
            };
        });

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins(
                        configuration.GetSection("Cors:Origins").Get<string[]>()
                        ?? ["http://localhost:5173", "http://localhost:4173"])
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // One bucket per signed-in user (or per IP for anonymous calls), not one for the whole app.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirst("sub")?.Value is { } sub
                        ? $"user:{sub}"
                        : $"ip:{context.Connection.RemoteIpAddress}",
                    _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1)
                }));
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "SchoolManagementSystem API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the accessToken from /api/auth/login"
            });
            options.AddSecurityRequirement(document =>
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
        });
    }
}

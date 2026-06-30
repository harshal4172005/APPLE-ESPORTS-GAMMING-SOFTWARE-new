using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Api.Hubs;
using AppleEsportsErp.Api.Middleware;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Infrastructure.Data;
using AppleEsportsErp.Infrastructure.Identity;
using AppleEsportsErp.Infrastructure.Services;
using Serilog;

// ═══════════════════════════════════════════════
//  AppleEsports ERP — .NET 8 Enterprise Backend
//  SOP Master Source of Truth compliance
// ═══════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

// Enable DI validation on build
builder.Host.UseDefaultServiceProvider((context, options) => {
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// ── Serilog ──
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [req:{RequestId}] [op:{OperatorId}] [br:{BranchId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/appleesports-.log", 
        rollingInterval: RollingInterval.Day, 
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [req:{RequestId}] [op:{OperatorId}] [br:{BranchId}] [sh:{ShiftId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
builder.Host.UseSerilog();

// ── Configuration sections ──
var jwtConfig = builder.Configuration.GetSection("Jwt");
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173", "http://127.0.0.1:5173" };

// ═══════════════════════════════════════════════
//  SERVICES — maps from server/src/index.js
// ═══════════════════════════════════════════════

// ── 1. EF Core / PostgreSQL (SOP §23) ──
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// ── 2. JWT Authentication (SOP §21 + Q1: full claims embedded) ──
var jwtKey = Encoding.UTF8.GetBytes(jwtConfig["Secret"]!);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.UseSecurityTokenValidators = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
        ValidateIssuer = true,
        ValidIssuer = jwtConfig["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtConfig["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name,
    };

    // Q2 Decision: SignalR JWT via query string for WebSocket connections
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var revocationService = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationService>();
            var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
            var userIdString = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (jti != null && Guid.TryParse(userIdString, out var userId))
            {
                var validFrom = context.SecurityToken.ValidFrom;
                var issueTime = validFrom == DateTime.MinValue ? DateTimeOffset.UtcNow : new DateTimeOffset(validFrom, TimeSpan.Zero);
                if (await revocationService.IsTokenRevokedAsync(jti, userId, issueTime))
                {
                    context.Fail("Token has been revoked.");
                }
            }
        }
    };
});

// ── 3. Authorization Policies (SOP §5 + §19) ──
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireClaim(ClaimTypes.Role, Roles.SuperAdmin));

    options.AddPolicy("AdminOrSuperAdmin", policy =>
        policy.RequireClaim(ClaimTypes.Role, Roles.SuperAdmin, Roles.Admin));

    options.AddPolicy("OperatorOrAdmin", policy =>
        policy.RequireClaim(ClaimTypes.Role, Roles.SuperAdmin, Roles.Admin, Roles.Operator, "Agent"));

    // Dashboard-specific policies
    foreach (var dashboard in new[] { Dashboards.BillingCounter, Dashboards.Sessions, Dashboards.Reservations,
        Dashboards.FoodOrders, Dashboards.CashRegister, Dashboards.CashDesk, Dashboards.Members,
        Dashboards.MenuEditor, Dashboards.MainDashboard, Dashboards.PcStatus, Dashboards.Eod, Dashboards.Settings })
    {
        options.AddPolicy($"Dashboard:{dashboard}", policy =>
            policy.Requirements.Add(new DashboardRequirement(dashboard)));
    }
});
builder.Services.AddSingleton<IAuthorizationHandler, DashboardAuthorizationHandler>();

// ── 4. CORS (maps from Helmet + CORS in Node.js) ──
builder.Services.AddCors(options =>
{
    options.AddPolicy("AppleEsportsCors", policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ── 5. Rate Limiting (maps from rateLimit.js) ──
var rateLimitConfig = builder.Configuration.GetSection("RateLimiting");
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitConfig.GetValue("PermitLimit", 100),
                Window = TimeSpan.FromSeconds(rateLimitConfig.GetValue("WindowSeconds", 60)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5,
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            error = "Rate limit exceeded. Please try again later.",
            code = "RATE_LIMIT",
        }, cancellationToken: cancellationToken);
    };
});

// ── 6. SignalR (Q2: auto-negotiation, WebSocket primary) ──
var signalRBuilder = builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 128 * 1024; // 128 KB
});

var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrEmpty(redisConnectionString))
{
    signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("AppleEsportsSignalR");
    });
}

// ── 7. FluentValidation ──
builder.Services.AddValidatorsFromAssemblyContaining<AppleEsportsErp.Application.Validators.Auth.AdminLoginValidator>();

// ── 8. Application Services ──
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>().GetSection("Jwt");
    return new JwtTokenService(
        config["Secret"]!, config["RefreshSecret"]!,
        config["AccessExpiry"] ?? "15m", config["RefreshExpiry"] ?? "7d",
        config["Issuer"] ?? "AppleEsportsErp", config["Audience"] ?? "AppleEsportsErpClient"
    );
});
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPcStatusService, PcStatusService>();
builder.Services.AddScoped<ITokenRevocationService, TokenRevocationService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IHubNotificationService, AppleEsportsErp.Api.Services.HubNotificationService>();

// Sprint 2 Services
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IFoodOrderService, FoodOrderService>();
builder.Services.AddScoped<ICashRegisterService, CashRegisterService>();
builder.Services.AddScoped<ICashDeskService, CashDeskService>();
builder.Services.AddScoped<IEodService, EodService>();
builder.Services.AddScoped<IPcManagementService, PcManagementService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<ISystemDesksService, AppleEsportsErp.Infrastructure.Services.SystemDesksService>();
builder.Services.AddScoped<IUnitOfWork, AppleEsportsErp.Infrastructure.Repositories.UnitOfWork>();
builder.Services.AddHostedService<AppleEsportsErp.Api.Services.ReservationBackgroundService>();
builder.Services.AddHostedService<AppleEsportsErp.Api.Services.OpenSessionMonitorService>();
builder.Services.AddHostedService<AppleEsportsErp.Api.Services.DeferredBillingMonitorService>();
builder.Services.AddScoped<IOfflineSyncService, OfflineSyncService>();

// ── 9. Controllers + Swagger ──
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AppleEsports ERP API", Version = "v2.0" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter JWT token",
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// ═══════════════════════════════════════════════
//  MIDDLEWARE PIPELINE
//  SOP order: Helmet → CORS → Auth → RBAC → Branch → Audit → Rate Limit
//  .NET mapping: ExceptionHandler → CORS → Auth → Authz → RateLimiter → Controllers
// ═══════════════════════════════════════════════

var app = builder.Build();

// ── Auto-migrate in Development (SOP §23: Database Architecture) ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        if (await db.Database.CanConnectAsync())
        {
            Log.Information("PostgreSQL connection verified ✓");
            await db.Database.MigrateAsync();
            Log.Information("Database migrations applied ✓");
            AppleEsportsErp.Api.DbUpdater.UpdateSchema(app);
            Log.Information("Database schema patches applied ✓");
            AppleEsportsErp.Api.DataSeeder.SeedBranchesAsync(db).GetAwaiter().GetResult();
            Log.Information("Database seeded with default branches and PCs ✓");
        }
        else
        {
            Log.Warning("PostgreSQL not available — skipping migration. Start Docker PostgreSQL to enable.");
        }
    }
    catch (Exception ex)
    {
        Log.Warning("Database migration skipped: {Message} | Inner: {Inner}", ex.Message, ex.InnerException?.Message);
    }
}

// ── 1. Global Exception Handler (maps from errorHandler.js) ──
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

// ── Development tools ──
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AppleEsports ERP API v2.0"));
}

// ── 2. CORS (maps from Helmet + CORS) ──
app.UseCors("AppleEsportsCors");

// ── 3. Authentication (maps from auth.js verifyToken) ──
app.UseAuthentication();

// ── Auto Migrate Database ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ── 4. Authorization (maps from roles.js authorize/requireDashboardAccess) ──
app.UseAuthorization();

// ── 5. Rate Limiting (maps from rateLimit.js) ──
app.UseRateLimiter();

// ── 6. Map Controllers (maps from routes/index.js registerRoutes) ──
app.MapControllers();

// ── 7. Map SignalR Hubs (maps from sockets/index.js registerSocketHandlers) ──
app.MapHub<SessionHub>("/hubs/sessions");
app.MapHub<BillingHub>("/hubs/billing");
app.MapHub<ReservationHub>("/hubs/reservations");
app.MapHub<PcStatusHub>("/hubs/pc-status");
app.MapHub<FoodOrderHub>("/hubs/food-orders");
app.MapHub<CashHub>("/hubs/cash");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapHub<PcOverlayHub>("/hubs/pc-overlay");

// ── Startup banner ──
app.Lifetime.ApplicationStarted.Register(() =>
{
    Log.Information("╔═══════════════════════════════════════════════╗");
    Log.Information("║   AppleEsports ERP — .NET 8 Enterprise Backend  ║");
    Log.Information("║   Environment: {Env}", app.Environment.EnvironmentName);
    Log.Information("║   Hubs: 7 SignalR endpoints mapped            ║");
    Log.Information("║   Controllers: 15 API controllers mapped      ║");
    Log.Information("╚═══════════════════════════════════════════════╝");
});

app.Run();

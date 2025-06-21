using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AuthService.Data;
using AuthService.Services;
using AuthService.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);



// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AuthService API", Version = "v1" });
    
    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT Configuration for Internal AuthService
var jwtKey = builder.Configuration["JWT:SecretKey"];
var jwtIssuer = builder.Configuration["JWT:Issuer"];
var jwtAudience = builder.Configuration["JWT:Audience"];

// Keycloak Configuration - Dual Realm Support
var keycloakBaseUrl = builder.Configuration["Keycloak:BaseUrl"];
var keycloakServiceAuthority = builder.Configuration["Keycloak:ServiceRealm:Authority"];
var keycloakPlatformAuthority = builder.Configuration["Keycloak:PlatformRealm:Authority"];
var keycloakServiceClientId = builder.Configuration["Keycloak:ServiceRealm:ClientId"];
var keycloakPlatformClientId = builder.Configuration["Keycloak:PlatformRealm:ClientId"];
var keycloakDefaultRealm = builder.Configuration["Keycloak:DefaultRealm"] ?? "platform";

if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
{
    throw new InvalidOperationException("JWT configuration is missing. Please check JWT:SecretKey, JWT:Issuer, and JWT:Audience in appsettings.json");
}

// Multi-Authentication: Internal JWT + Keycloak Service Realm + Keycloak Platform Realm
builder.Services.AddAuthentication()
    .AddJwtBearer("Internal", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddJwtBearer("KeycloakService", options =>
    {
        if (!string.IsNullOrEmpty(keycloakServiceAuthority))
        {
            options.Authority = keycloakServiceAuthority;
            options.RequireHttpsMetadata = false; // For development
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidAudience = "account",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        }
    })
    .AddJwtBearer("KeycloakPlatform", options =>
    {
        if (!string.IsNullOrEmpty(keycloakPlatformAuthority))
        {
            options.Authority = keycloakPlatformAuthority;
            options.RequireHttpsMetadata = false; // For development
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidAudience = "account",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        }
    });

// Set default authentication scheme
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Internal";
    options.DefaultChallengeScheme = "Internal";
});

// Authorization Policies for Role-Based Access
builder.Services.AddAuthorization(options =>
{

    // Permission-based policies
    options.AddPolicy("CanManageUsers", policy =>
        policy.RequireClaim("permission", "user:manage"));

    options.AddPolicy("CanViewReports", policy =>
        policy.RequireClaim("permission", "reports:view"));

    options.AddPolicy("CanWriteData", policy =>
        policy.RequireClaim("permission", "data:write"));

    // Platform-specific policies (for end users: homeowners, contractors, platform admins)
    options.AddPolicy("PlatformAdmin", policy =>
        policy.RequireRole("platform-admin")
              .RequireAuthenticatedUser());

    options.AddPolicy("Homeowner", policy =>
        policy.RequireRole("homeowner")
              .RequireAuthenticatedUser());

    options.AddPolicy("Contractor", policy =>
        policy.RequireRole("contractor")
              .RequireAuthenticatedUser());

    options.AddPolicy("ProjectManager", policy =>
        policy.RequireRole("project-manager")
              .RequireAuthenticatedUser());

    // Platform user policies
    options.AddPolicy("PlatformUser", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("platform-admin") || 
            context.User.IsInRole("homeowner") || 
            context.User.IsInRole("contractor") ||
            context.User.IsInRole("project-manager")));

    // Service-to-service policies (for microservice authentication)
    options.AddPolicy("ServiceAccess", policy =>
        policy.RequireRole("service-client")
              .AddAuthenticationSchemes("KeycloakService"));

    // Multi-realm authentication policies
    options.AddPolicy("AnyRealm", policy =>
        policy.AddAuthenticationSchemes("Internal", "KeycloakService", "KeycloakPlatform")
              .RequireAuthenticatedUser());

    options.AddPolicy("PlatformRealm", policy =>
        policy.AddAuthenticationSchemes("KeycloakPlatform")
              .RequireAuthenticatedUser());

    options.AddPolicy("ServiceRealm", policy =>
        policy.AddAuthenticationSchemes("KeycloakService")
              .RequireAuthenticatedUser());
});

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Email Configuration
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// Register HTTP Client for Keycloak
builder.Services.AddHttpClient<IKeycloakService, KeycloakService>();

// Register Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IKeycloakService, KeycloakService>();

// Background Services
builder.Services.AddHostedService<OtpCleanupService>();
builder.Services.AddHostedService<RefreshTokenCleanupService>();
builder.Services.AddHostedService<EmailVerificationCleanupService>();
builder.Services.AddHostedService<PasswordResetCleanupService>();

// Logging Configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthService API V1");
        c.RoutePrefix = "swagger";
    });
}

// Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    await next();
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Database Migration and Seeding
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

    try
    {
        // Apply any pending migrations
        await context.Database.MigrateAsync();

        app.Logger.LogInformation("Database migration completed successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while migrating the database");
        throw;
    }
}

// Health Check Endpoint
app.MapGet("/health", async (AuthDbContext context) =>
{
    try
    {
        await context.Database.CanConnectAsync();
        return Results.Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Database = "Connected",
            Version = "1.1.0"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 503,
            title: "Service Unavailable"
        );
    }
});

// API Info Endpoint
app.MapGet("/", () => new
{
    Service = "AuthService API",
    Version = "2.0.0",
    Features = new[] {
        "Email + Password Authentication",
        "Phone + OTP Authentication", 
        "Email Verification System",
        "Password Reset System",
        "Role-Based Access Control (Platform Roles)",
        "Dual-Realm Keycloak Integration (Platform + Service)",
        "Platform Roles (Admin, Homeowner, Contractor, Project Manager)",
        "Service-to-Service Authentication",
        "JWT Access Tokens (15 min)",
        "Refresh Tokens (7 days)",
        "Token Refresh & Rotation",
        "Multi-device Support",
        "Background Token Cleanup",
        "JWKS Token Verification Support"
    },
    SupportedRoles = new[] {
        "PlatformAdmin - Platform administrator with full access",
        "Homeowner - Property owners who create renovation projects",
        "Contractor - Professional contractors who bid on projects", 
        "ProjectManager - Managers who oversee renovation projects",
        "ServiceClient - Service-to-service authentication"
    },
    Realms = new {
        Platform = "End-user authentication (homeowners, contractors, admins)",
        Service = "Service-to-service authentication between microservices"
    },
    Documentation = "/swagger",
    Health = "/health",
    JWKS = "/.well-known/jwks.json",
    OpenIDConfig = "/.well-known/openid_configuration"
});

app.Run();

public partial class Program { } // For testing purposes
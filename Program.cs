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
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
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

// Keycloak Configuration
var keycloakAuthority = builder.Configuration["Keycloak:Authority"];
var keycloakClientId = builder.Configuration["Keycloak:ClientId"];
var keycloakAudience = builder.Configuration["Keycloak:Audience"];

if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
{
    throw new InvalidOperationException("JWT configuration is missing. Please check JWT:SecretKey, JWT:Issuer, and JWT:Audience in appsettings.json");
}

// Dual Authentication: Internal JWT + Keycloak
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
    .AddJwtBearer("Keycloak", options =>
    {
        if (!string.IsNullOrEmpty(keycloakAuthority))
        {
            options.Authority = keycloakAuthority;
            options.RequireHttpsMetadata = false; // For development
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidAudience = keycloakAudience ?? "account",
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
    // Admin-only policies
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin")
              .RequireAuthenticatedUser());

    // User1-specific policies
    options.AddPolicy("User1Access", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") || context.User.IsInRole("User1")));

    // User2-specific policies
    options.AddPolicy("User2Access", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") || context.User.IsInRole("User2")));

    // Admin or User1 policies
    options.AddPolicy("AdminOrUser1", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") || context.User.IsInRole("User1")));

    // Admin or User2 policies
    options.AddPolicy("AdminOrUser2", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") || context.User.IsInRole("User2")));

    // Any authenticated user with valid role
    options.AddPolicy("ValidUser", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") || 
            context.User.IsInRole("User1") || 
            context.User.IsInRole("User2")));

    // Permission-based policies
    options.AddPolicy("CanManageUsers", policy =>
        policy.RequireClaim("permission", "user:manage"));

    options.AddPolicy("CanViewReports", policy =>
        policy.RequireClaim("permission", "reports:view"));

    options.AddPolicy("CanWriteData", policy =>
        policy.RequireClaim("permission", "data:write"));
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

// Register Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IRoleService, RoleService>();

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
    Version = "1.1.0",
    Features = new[] {
        "Email + Password Authentication",
        "Phone + OTP Authentication",
        "Email Verification System",
        "Password Reset System",
        "Role-Based Access Control (Admin, User1, User2)",
        "Keycloak Integration Support",
        "JWT Access Tokens (15 min)",
        "Refresh Tokens (7 days)",
        "Token Refresh & Rotation",
        "Multi-device Support",
        "Background Token Cleanup"
    },
    Documentation = "/swagger",
    Health = "/health"
});

app.Run();

public partial class Program { } // For testing purposes
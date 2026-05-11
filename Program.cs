
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BookApi.Data;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Force Kestrel to listen on Render's PORT
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(port));
});

// Database configuration - Neon PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("BookQuote");
if (string.IsNullOrEmpty(connectionString))
    connectionString = Environment.GetEnvironmentVariable("ConnectionStrings_BookQuote");
if (string.IsNullOrEmpty(connectionString))
    connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
                         ?? Environment.GetEnvironmentVariable("NEON_DATABASE_URL");
if (string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("Database connection string not configured.");

Console.WriteLine($"Database connection string read (length: {connectionString.Length} characters)");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT Authentication - Read secret from environment variable
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
                 ?? throw new InvalidOperationException("JWT_SECRET environment variable is not set.");
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "BookApi",
            ValidAudience = "BookApp",
            IssuerSigningKey = key
        };
    });

// ========== Rate Limiting Configuration ==========
// PURPOSE: Mitigate brute-force and enumeration attacks by limiting request frequency.
// No changes needed – existing policies are sufficient.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Login: 5 attempts per 5 minutes per IP
    options.AddPolicy("LoginPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Register: 3 attempts per hour per IP
    options.AddPolicy("RegisterPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromHours(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});
// =====================================================

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNetlify", policy =>
    {
        policy.WithOrigins("https://bookapp2026.netlify.app")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var app = builder.Build();

app.UseCors("AllowNetlify");
app.UseRateLimiter(); // Must be placed before Authentication/Authorization
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
        Console.WriteLine("Database migration successful!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database migration failed: {ex.Message}");
        throw;
    }
}

app.Run();

// ================================ Keep the code below ======================================
// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.IdentityModel.Tokens;
// using System.Text;
// using BookApi.Data;
// using System.Threading.RateLimiting;   // Added for rate limiting
//
// var builder = WebApplication.CreateBuilder(args);
//
// // Force Kestrel to listen on Render's PORT
// var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
// builder.WebHost.ConfigureKestrel(options =>
// {
//     options.ListenAnyIP(int.Parse(port));
// });
//
// // Database configuration - Neon PostgreSQL
// var connectionString = builder.Configuration.GetConnectionString("BookQuote");
// if (string.IsNullOrEmpty(connectionString))
//     connectionString = Environment.GetEnvironmentVariable("ConnectionStrings_BookQuote");
// if (string.IsNullOrEmpty(connectionString))
//     connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
//                          ?? Environment.GetEnvironmentVariable("NEON_DATABASE_URL");
// if (string.IsNullOrEmpty(connectionString))
//     throw new InvalidOperationException("Database connection string not configured.");
//
// Console.WriteLine($"Database connection string read (length: {connectionString.Length} characters)");
//
// builder.Services.AddDbContext<AppDbContext>(options =>
//     options.UseNpgsql(connectionString));
//
// // ===== JWT Authentication - Read secret from environment variable =====
// var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
//                  ?? throw new InvalidOperationException("JWT_SECRET environment variable is not set.");
// var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
//
// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options =>
//     {
//         options.TokenValidationParameters = new TokenValidationParameters
//         {
//             ValidateIssuer = true,
//             ValidateAudience = true,
//             ValidateLifetime = true,
//             ValidateIssuerSigningKey = true,
//             ValidIssuer = "BookApi",
//             ValidAudience = "BookApp",
//             IssuerSigningKey = key
//         };
//     });
//
// // ========== NEW: Rate Limiting Configuration ==========
// builder.Services.AddRateLimiter(options =>
// {
//     // Global fallback policy (rejection status code)
//     options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
//
//     // Policy for Login endpoint: 5 attempts per 5 minutes per IP address
//     options.AddPolicy("LoginPolicy", httpContext =>
//         RateLimitPartition.GetFixedWindowLimiter(
//             partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
//             factory: partition => new FixedWindowRateLimiterOptions
//             {
//                 PermitLimit = 5,
//                 Window = TimeSpan.FromMinutes(5),
//                 QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
//                 QueueLimit = 0
//             }));
//
//     // Policy for Register endpoint: 3 attempts per hour per IP address
//     options.AddPolicy("RegisterPolicy", httpContext =>
//         RateLimitPartition.GetFixedWindowLimiter(
//             partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
//             factory: partition => new FixedWindowRateLimiterOptions
//             {
//                 PermitLimit = 3,
//                 Window = TimeSpan.FromHours(1),
//                 QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
//                 QueueLimit = 0
//             }));
// });
// // =====================================================
//
// builder.Services.AddControllers();
// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("AllowNetlify", policy =>
//     {
//         policy.WithOrigins("https://bookapp2026.netlify.app")
//               .AllowAnyMethod()
//               .AllowAnyHeader();
//     });
// });
//
// AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
// var app = builder.Build();
//
// app.UseCors("AllowNetlify");
// app.UseRateLimiter();          // NEW: Enable rate limiting middleware (must be before Authentication/Authorization)
// app.UseAuthentication();
// app.UseAuthorization();
// app.MapControllers();
//
// // Auto-migrate database
// using (var scope = app.Services.CreateScope())
// {
//     var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//     try
//     {
//         db.Database.Migrate();
//         Console.WriteLine("Database migration successful!");
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"Database migration failed: {ex.Message}");
//         throw;
//     }
// }
//
// app.Run();





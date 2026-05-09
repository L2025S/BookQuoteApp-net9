using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BookApi.Data;
using Microsoft.Extensions.Caching.Memory;

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

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNetlify", policy =>
    {
        policy.WithOrigins("https://bookapp-angular20.netlify.app")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add logging
builder.Services.AddLogging();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var app = builder.Build();


// Simple in-memory rate limiting middleware
app.Use(async (context, next) =>
{
    var endpoint = context.Request.Path;
    if (endpoint.StartsWithSegments("/api/auth/login") || endpoint.StartsWithSegments("/api/auth/register"))
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var key = $"{ip}:{endpoint}";
        var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
        
        int attemptCount = cache.Get<int>(key);
        if (attemptCount >= 5) // 5 attempts per minute
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Too many attempts. Please try again later.");
            return;
        }
        
        cache.Set(key, attemptCount + 1, TimeSpan.FromMinutes(1));
    }
    await next();
});


// Enable HTTPS redirection (important for production)
app.UseHttpsRedirection();

app.UseCors("AllowNetlify");
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



//==========================Keep the code below ==========================================
// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.IdentityModel.Tokens;
// using System.Text;
// using BookApi.Data;
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
//                         ?? Environment.GetEnvironmentVariable("NEON_DATABASE_URL");
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
//                 ?? throw new InvalidOperationException("JWT_SECRET environment variable is not set.");
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
// builder.Services.AddControllers();
// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("AllowNetlify", policy =>
//     {
//         policy.WithOrigins("https://bookapp-angular20.netlify.app")
//               .AllowAnyMethod()
//               .AllowAnyHeader();
//     });
// });
//
// AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
// var app = builder.Build();
//
// app.UseCors("AllowNetlify");
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




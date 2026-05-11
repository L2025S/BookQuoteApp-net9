using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BookApi.Data;

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

// JWT Authentication
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

// ⭐ FIX: Proper CORS configuration to prevent OPTIONS returning 204
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNetlify", policy =>
    {
        policy.WithOrigins("https://bookapp2026.netlify.app")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .SetPreflightMaxAge(TimeSpan.FromHours(1)); 
              // ⭐ This ensures OPTIONS responses are cached and not repeatedly sent.
    });
});

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var app = builder.Build();

// ⭐ FIX: CORS MUST be placed BEFORE Authentication/Authorization
// Otherwise OPTIONS requests will not receive proper CORS headers.
app.UseCors("AllowNetlify");

// Authentication & Authorization
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

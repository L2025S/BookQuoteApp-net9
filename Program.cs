
using System.Text;
using BookApi.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

//============Force Kestrel to listen on Render's PORT===============
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(port));
});

// ========== Database configuration - Neon PostgreSQL ==========
// Read connection string from configuration (environment variable ConnectionStrings__BookQuote)
var connectionString = builder.Configuration.GetConnectionString("BookQuote");

// Fallback: read directly from environment variable
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("ConnectionStrings_BookQuote");
}

// Additional fallback for common variable names
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? Environment.GetEnvironmentVariable("NEON_DATABASE_URL");
}

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("Error: Unable to read database connection string!");
    throw new InvalidOperationException("Database connection string not configured");
}

Console.WriteLine($"Database connection string read (length: {connectionString.Length} characters)");

// Use PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ========== JWT authentication configuration ==========
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("din-hemliga-nyckel-som-ar-minst-32-tecken-lang123!"))
        };
    });

builder.Services.AddControllers();

// ========== CORS configuration – allow only  Netlify frontend ==========

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNetlify", policy =>
    {
        policy.WithOrigins("https://bookapp-angular20.netlify.app") 
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var app = builder.Build();

// Apply the CORS policy
app.UseCors("AllowNetlify");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ========== Automatically apply database migrations ==========
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
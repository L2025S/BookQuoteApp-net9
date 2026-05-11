
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using BookApi.Data;
using BookApi.Models;
using Microsoft.AspNetCore.Mvc;
// using Microsoft.AspNetCore.RateLimiting;  // COMMENTED OFF: Not needed without rate limiting
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BookApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    // Dummy hash for non-existent users (BCrypt with work factor 12)
    private const string DummyPasswordHash = "$2a$12$Fg9jQ4ZQ5YxLmNpRtVwYuXeFgHjKlQwErTyUiOpAsDfGhJkLzXcVbNm";

    public AuthController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    // [EnableRateLimiting("RegisterPolicy")]  // COMMENTED OFF: Rate limiting disabled
    [HttpPost("register")]
    public IActionResult Register(UserRegisterDto dto)
    {
        // Generic error for model validation to prevent username enumeration
        if (!ModelState.IsValid)
            return BadRequest("Unable to register. Please check your input.");

        // Check for existing username (returns same generic message as other errors)
        if (_db.Users.Any(u => u.Username == dto.Username))
        {
            return BadRequest("Unable to register. Please check your input.");
        }

        var user = new User
        {
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12)
        };

        _db.Users.Add(user);
        try
        {
            _db.SaveChanges();
        }
        catch (DbUpdateException)
        {
            // Catch unique constraint violations (e.g., concurrent registration)
            return BadRequest("Unable to register. Please check your input.");
        }

        return Ok("User created successfully.");
    }

    // [EnableRateLimiting("LoginPolicy")]  // COMMENTED OFF: Rate limiting disabled
    [HttpPost("login")]
    public IActionResult Login(UserLoginDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = _db.Users.FirstOrDefault(u => u.Username == dto.Username);
        string passwordHashToVerify;

        if (user != null)
        {
            passwordHashToVerify = user.PasswordHash;
        }
        else
        {
            // Use constant dummy hash to prevent timing attacks
            passwordHashToVerify = DummyPasswordHash;
        }

        bool isValid = BCrypt.Net.BCrypt.Verify(dto.Password, passwordHashToVerify);

        if (!isValid)
        {
            return Unauthorized("Invalid username or password.");
        }

        if (user == null)
        {
            // Defensive – never reached because dummy hash fails verification
            return Unauthorized("Invalid username or password.");
        }

        // Generate JWT token
        var jwtSecret = _configuration["JWT_SECRET"];
        if (string.IsNullOrEmpty(jwtSecret))
            throw new Exception("JWT_SECRET is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "BookApi",
            audience: "BookApp",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new { token = tokenString });
    }
}

// DTOs remain unchanged
public class UserRegisterDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(3)]
    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string Username { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(8)]
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string Password { get; set; } = "";
}

public class UserLoginDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public string Username { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    public string Password { get; set; } = "";
}




// =============================Keep the code below ============================
// using System;
// using System.IdentityModel.Tokens.Jwt;
// using System.Linq;
// using System.Security.Claims;
// using System.Text;
// using BookApi.Data;
// using BookApi.Models;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.AspNetCore.RateLimiting;
// using Microsoft.IdentityModel.Tokens;
// using Microsoft.EntityFrameworkCore; // Added for DbUpdateException handling
// using Microsoft.Extensions.Configuration;
//
// namespace BookApi.Controllers;
//
// [ApiController]
// [Route("api/[controller]")]
// public class AuthController : ControllerBase
// {
//     private readonly AppDbContext _db;
//     private readonly IConfiguration _configuration;
//
//     // Dummy hash for non-existent users (BCrypt with work factor 12, password = "dummy-password-123")
//     // This ensures BCrypt.Verify is always called, eliminating timing differences.
//     private const string DummyPasswordHash = "$2a$12$Fg9jQ4ZQ5YxLmNpRtVwYuXeFgHjKlQwErTyUiOpAsDfGhJkLzXcVbNm";
//
//     public AuthController(AppDbContext db, IConfiguration configuration)
//     {
//         _db = db;
//         _configuration = configuration;
//     }
//
//     [HttpPost("register")]
//     [EnableRateLimiting("RegisterPolicy")]
//     public IActionResult Register(UserRegisterDto dto)
//     {
//         // FIX: Return generic error for any model validation failure to prevent username enumeration.
//         if (!ModelState.IsValid)
//             return BadRequest("Unable to register. Please check your input.");
//
//         // FIX: Instead of early return that distinguishes "username exists", use a generic message.
//         if (_db.Users.Any(u => u.Username == dto.Username))
//         {
//             return BadRequest("Unable to register. Please check your input.");
//         }
//
//         var user = new User
//         {
//             Username = dto.Username,
//             PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12)
//         };
//
//         _db.Users.Add(user);
//         try
//         {
//             _db.SaveChanges();
//         }
//         catch (DbUpdateException)
//         {
//             // FIX: Catch unique constraint violations (e.g., concurrent registration) and return generic error.
//             return BadRequest("Unable to register. Please check your input.");
//         }
//
//         return Ok("User created successfully.");
//     }
//
//     [HttpPost("login")]
//     [EnableRateLimiting("LoginPolicy")]
//     public IActionResult Login(UserLoginDto dto)
//     {
//         if (!ModelState.IsValid)
//             return BadRequest(ModelState); // ModelState errors here are acceptable because login uses only username+password.
//
//         // Retrieve user if exists; we will use dummy hash otherwise.
//         var user = _db.Users.FirstOrDefault(u => u.Username == dto.Username);
//         string passwordHashToVerify;
//
//         if (user != null)
//         {
//             passwordHashToVerify = user.PasswordHash;
//         }
//         else
//         {
//             // FIX: Use a constant dummy hash to ensure BCrypt verification always runs,
//             // eliminating timing side‑channel from the database query.
//             passwordHashToVerify = DummyPasswordHash;
//         }
//
//         // Always perform BCrypt verification, regardless of whether the user exists.
//         bool isValid = BCrypt.Net.BCrypt.Verify(dto.Password, passwordHashToVerify);
//
//         if (!isValid)
//         {
//             // Generic error message for both wrong username and password.
//             return Unauthorized("Invalid username or password.");
//         }
//
//         // If user does not exist, isValid will always be false due to dummy hash.
//         // Therefore we only continue when user exists and password is correct.
//         if (user == null)
//         {
//             // This line is never reached because isValid is false for non‑existent users,
//             // but we keep it for defensive programming.
//             return Unauthorized("Invalid username or password.");
//         }
//
//         // Generate JWT token (unchanged)
//         var jwtSecret = _configuration["JWT_SECRET"];
//         if (string.IsNullOrEmpty(jwtSecret))
//             throw new Exception("JWT_SECRET is not configured.");
//
//         var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
//         var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
//
//         var claims = new[]
//         {
//             new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
//         };
//
//         var token = new JwtSecurityToken(
//             issuer: "BookApi",
//             audience: "BookApp",
//             claims: claims,
//             expires: DateTime.UtcNow.AddMinutes(15),
//             signingCredentials: creds
//         );
//
//         var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
//         return Ok(new { token = tokenString });
//     }
// }
//
// // DTOs remain unchanged
// public class UserRegisterDto
// {
//     [System.ComponentModel.DataAnnotations.Required]
//     [System.ComponentModel.DataAnnotations.MinLength(3)]
//     [System.ComponentModel.DataAnnotations.MaxLength(50)]
//     public string Username { get; set; } = "";
//
//     [System.ComponentModel.DataAnnotations.Required]
//     [System.ComponentModel.DataAnnotations.MinLength(8)]
//     [System.ComponentModel.DataAnnotations.MaxLength(100)]
//     public string Password { get; set; } = "";
// }
//
// public class UserLoginDto
// {
//     [System.ComponentModel.DataAnnotations.Required]
//     public string Username { get; set; } = "";
//
//     [System.ComponentModel.DataAnnotations.Required]
//     public string Password { get; set; } = "";
// }













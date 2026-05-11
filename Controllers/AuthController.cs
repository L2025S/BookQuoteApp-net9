

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using BookApi.Data;
using BookApi.Models;
using Microsoft.AspNetCore.Mvc;
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

    private const string DummyPasswordHash = "$2a$12$Fg9jQ4ZQ5YxLmNpRtVwYuXeFgHjKlQwErTyUiOpAsDfGhJkLzXcVbNm";

    public AuthController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public IActionResult Register(UserRegisterDto dto)
    {
        // CHANGE 1: Return detailed validation errors instead of a generic message
        if (!ModelState.IsValid)
        {
            // Extract all error messages from ModelState
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { message = string.Join("; ", errors), errors });
        }

        if (_db.Users.Any(u => u.Username == dto.Username))
        {
            return BadRequest(new { message = "Username already exists." });
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
            return BadRequest(new { message = "Database error, please try again." });
        }

        return Ok(new { message = "User created successfully." });
    }

    [HttpPost("login")]
    public IActionResult Login(UserLoginDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = _db.Users.FirstOrDefault(u => u.Username == dto.Username);
        string passwordHashToVerify;

        if (user != null)
            passwordHashToVerify = user.PasswordHash;
        else
            passwordHashToVerify = DummyPasswordHash;

        bool isValid = BCrypt.Net.BCrypt.Verify(dto.Password, passwordHashToVerify);

        if (!isValid)
            return Unauthorized(new { message = "Invalid username or password." });

        if (user == null)
            return Unauthorized(new { message = "Invalid username or password." });

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

// DTOs with validation attributes (already correct)
public class UserRegisterDto
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Username is required.")]
    [System.ComponentModel.DataAnnotations.MinLength(3, ErrorMessage = "Username must be at least 3 characters.")]
    [System.ComponentModel.DataAnnotations.MaxLength(50, ErrorMessage = "Username cannot exceed 50 characters.")]
    public string Username { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Password is required.")]
    [System.ComponentModel.DataAnnotations.MinLength(8, ErrorMessage = "Password must be between 8 and 100 characters.")]
    [System.ComponentModel.DataAnnotations.MaxLength(100, ErrorMessage = "Password cannot exceed 100 characters.")]
    public string Password { get; set; } = "";
}

public class UserLoginDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public string Username { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    public string Password { get; set; } = "";
}




// ========================== Keep the code below ===================================
// using System;
// using System.IdentityModel.Tokens.Jwt;
// using System.Linq;
// using System.Security.Claims;
// using System.Text;
// using BookApi.Data;
// using BookApi.Models;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.IdentityModel.Tokens;
// using Microsoft.EntityFrameworkCore;
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
//     private const string DummyPasswordHash = "$2a$12$Fg9jQ4ZQ5YxLmNpRtVwYuXeFgHjKlQwErTyUiOpAsDfGhJkLzXcVbNm";
//
//     public AuthController(AppDbContext db, IConfiguration configuration)
//     {
//         _db = db;
//         _configuration = configuration;
//     }
//
//     [HttpPost("register")]
//     public IActionResult Register(UserRegisterDto dto)
//     {
//         if (!ModelState.IsValid)
//             return BadRequest(new { message = "Unable to register. Please check your input." });
//
//         if (_db.Users.Any(u => u.Username == dto.Username))
//         {
//             return BadRequest(new { message = "Unable to register. Please check your input." });
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
//             return BadRequest(new { message = "Unable to register. Please check your input." });
//         }
//
//         // FIX: Return JSON instead of plain text so Angular HttpClient can parse it correctly.
//         // Without this, Angular's default JSON parser would throw SyntaxError and enter error callback.
//         return Ok(new { message = "User created successfully." });
//     }
//
//     [HttpPost("login")]
//     public IActionResult Login(UserLoginDto dto)
//     {
//         if (!ModelState.IsValid)
//             return BadRequest(ModelState);
//
//         var user = _db.Users.FirstOrDefault(u => u.Username == dto.Username);
//         string passwordHashToVerify;
//
//         if (user != null)
//             passwordHashToVerify = user.PasswordHash;
//         else
//             passwordHashToVerify = DummyPasswordHash;
//
//         bool isValid = BCrypt.Net.BCrypt.Verify(dto.Password, passwordHashToVerify);
//
//         if (!isValid)
//             return Unauthorized(new { message = "Invalid username or password." });
//
//         if (user == null)
//             return Unauthorized(new { message = "Invalid username or password." });
//
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
// // DTOs unchanged
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
















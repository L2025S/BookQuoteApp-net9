using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using BookApi.Data;
using BookApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;

namespace BookApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public IActionResult Register(UserRegisterDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (_db.Users.Any(u => u.Username == dto.Username))
        {
            // Prevent user enumeration
            return BadRequest("Unable to register. Please check your input.");
        }

        var user = new User
        {
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12)
        };

        _db.Users.Add(user);
        _db.SaveChanges();

        return Ok("User created successfully.");
    }

    [HttpPost("login")]
    public IActionResult Login(UserLoginDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = _db.Users.FirstOrDefault(u => u.Username == dto.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            // Generic error message for both wrong username and password
            return Unauthorized("Invalid username or password.");
        }

        // Read JWT secret from configuration (environment variable)
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
            expires: DateTime.UtcNow.AddMinutes(15),   // Short lifetime
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new { token = tokenString });
    }
}

// DTOs with validation
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










//====================================TODO : Keep the code below and might need it again ===================================
// using System;
// using System.IdentityModel.Tokens.Jwt;
// using System.Linq;
// using System.Security.Claims;
// using System.Text;
// using BookApi.Data;
// using BookApi.Models;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.IdentityModel.Tokens;
//
// namespace BookApi.Controllers;
//
//
// [ApiController]
// [Route("api/[controller]")]
// public class AuthController : ControllerBase
// {
//     private readonly AppDbContext _db;
//
//     public AuthController(AppDbContext db)
//     {
//         _db = db;
//     }
//
//     [HttpPost("register")]
//     public IActionResult Register(UserRegisterDto dto)
//     {
//         if (_db.Users.Any(u => u.Username == dto.Username))
//             return BadRequest("Username already exists");
//
//         var user = new User
//         {
//             Username = dto.Username,
//             PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
//         };
//
//         _db.Users.Add(user);
//         _db.SaveChanges();
//         return Ok("User created successfully");
//     }
//
//     [HttpPost("login")]
//     public IActionResult Login(UserLoginDto dto)
//     {
//         var user = _db.Users.FirstOrDefault(u => u.Username == dto.Username);
//         if (user == null)
//             return Unauthorized("Invalid username or password");
//
//         if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
//             return Unauthorized("Invalid username or password");
//
//         var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
//             "din-hemliga-nyckel-som-ar-minst-32-tecken-lang123!"));
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
//             expires: DateTime.Now.AddDays(7),
//             signingCredentials: creds
//         );
//
//         return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
//     }
// }
//
// // DTOs with nullable warnings fixed
// public class UserRegisterDto
// {
//     public string Username { get; set; } = "";
//     public string Password { get; set; } = "";
// }
//
// public class UserLoginDto
// {
//     public string Username { get; set; } = "";
//     public string Password { get; set; } = "";
// }
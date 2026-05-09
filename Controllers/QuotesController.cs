using System;
using System.Linq;
using System.Security.Claims;
using BookApi.Data;
using BookApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookApi.Controllers;


[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuotesController : ControllerBase
{
    private readonly AppDbContext _db;

    public QuotesController(AppDbContext db)
    {
        _db = db;
    }

    
    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("User ID not found in token");
        return int.Parse(userIdClaim);
    }

    [HttpGet]
    public IActionResult GetQuotes()
    {
        var quotes = _db.Quotes.Where(q => q.UserId == GetUserId()).ToList();
        return Ok(quotes);
    }

    [HttpGet("{id}")]
    public IActionResult GetQuote(int id)
    {
        var quote = _db.Quotes.FirstOrDefault(q => q.Id == id && q.UserId == GetUserId());
        if (quote == null)
            return NotFound();
        return Ok(quote);
    }

    [HttpPost]
    public IActionResult CreateQuote(Quote quote)
    {
        quote.UserId = GetUserId();
        _db.Quotes.Add(quote);
        _db.SaveChanges();
        return Ok(quote);
    }

    [HttpPut("{id}")]
    public IActionResult UpdateQuote(int id, Quote updatedQuote)
    {
        var quote = _db.Quotes.FirstOrDefault(q => q.Id == id && q.UserId == GetUserId());
        if (quote == null)
            return NotFound();

        quote.Text = updatedQuote.Text;
        quote.Author = updatedQuote.Author;
        _db.SaveChanges();
        return Ok(quote);
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteQuote(int id)
    {
        var quote = _db.Quotes.FirstOrDefault(q => q.Id == id && q.UserId == GetUserId());
        if (quote == null)
            return NotFound();

        _db.Quotes.Remove(quote);
        _db.SaveChanges();
        return Ok();
    }
}
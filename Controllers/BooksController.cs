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
public class BooksController : ControllerBase
{
    private readonly AppDbContext _db;

    public BooksController(AppDbContext db)
    {
        _db = db;
    }

    //Get the currently logged-in user's ID from the JWT token (fix nullable warnings
    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("User ID not found in token");
        return int.Parse(userIdClaim);
    }

    [HttpGet]
    public IActionResult GetBooks()
    {
        var books = _db.Books.Where(b => b.UserId == GetUserId()).ToList();
        return Ok(books);
    }

    [HttpGet("{id}")]
    public IActionResult GetBook(int id)
    {
        var book = _db.Books.FirstOrDefault(b => b.Id == id && b.UserId == GetUserId());
        if (book == null)
            return NotFound();
        return Ok(book);
    }

    [HttpPost]
    public IActionResult CreateBook(Book book)
    {
        book.UserId = GetUserId();
        _db.Books.Add(book);
        _db.SaveChanges();
        return Ok(book);
    }

    [HttpPut("{id}")]
    public IActionResult UpdateBook(int id, Book updatedBook)
    {
        var book = _db.Books.FirstOrDefault(b => b.Id == id && b.UserId == GetUserId());
        if (book == null)
            return NotFound();

        book.Title = updatedBook.Title;
        book.Author = updatedBook.Author;
        book.PublishedDate = updatedBook.PublishedDate;
        _db.SaveChanges();
        return Ok(book);
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteBook(int id)
    {
        var book = _db.Books.FirstOrDefault(b => b.Id == id && b.UserId == GetUserId());
        if (book == null)
            return NotFound();

        _db.Books.Remove(book);
        _db.SaveChanges();
        return Ok();
    }
}
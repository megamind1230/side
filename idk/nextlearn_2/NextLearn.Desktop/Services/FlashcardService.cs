using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public class FlashcardService
{
    private readonly AppDbContext _context;
    private readonly UserService _userService;

    public FlashcardService(AppDbContext context, UserService userService)
    {
        _context = context;
        _userService = userService;
    }

    public List<Flashcard> GetUserFlashcards()
    {
        var userId = _userService.GetCurrentUserId();
        return _context.Flashcards
            .Include(f => f.Page)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToList();
    }

    public async Task<Flashcard> CreateFromPageAsync(Guid pageId, string front, string back)
    {
        var userId = _userService.GetCurrentUserId();
        
        var existing = await _context.Flashcards
            .FirstOrDefaultAsync(f => f.UserId == userId && f.PageId == pageId);

        if (existing != null)
        {
            existing.Front = front;
            existing.Back = back;
            await _context.SaveChangesAsync();
            return existing;
        }

        var flashcard = new Flashcard
        {
            UserId = userId,
            PageId = pageId,
            Front = front,
            Back = back
        };
        _context.Flashcards.Add(flashcard);
        await _context.SaveChangesAsync();
        return flashcard;
    }

    public async Task GenerateFromPageAsync(Guid pageId)
    {
        var page = await _context.Pages.FirstOrDefaultAsync(p => p.Id == pageId);
        if (page == null) return;

        var front = $"What is: {page.Title}?";
        var back = page.TextContent ?? "See page content";
        
        await CreateFromPageAsync(pageId, front, back);
    }

    public async Task DeleteAsync(Guid flashcardId)
    {
        var userId = _userService.GetCurrentUserId();
        var flashcard = await _context.Flashcards
            .FirstOrDefaultAsync(f => f.Id == flashcardId && f.UserId == userId);
        
        if (flashcard != null)
        {
            _context.Flashcards.Remove(flashcard);
            await _context.SaveChangesAsync();
        }
    }

    public List<Flashcard> GetDueFlashcards()
    {
        var userId = _userService.GetCurrentUserId();
        var now = DateTime.UtcNow;
        
        return _context.Flashcards
            .Where(f => f.UserId == userId)
            .Where(f => f.LastReviewedAt == null || 
                        (now - f.LastReviewedAt.Value).TotalHours >= 24)
            .ToList();
    }

    public async Task MarkReviewedAsync(Guid flashcardId)
    {
        var userId = _userService.GetCurrentUserId();
        var flashcard = await _context.Flashcards
            .FirstOrDefaultAsync(f => f.Id == flashcardId && f.UserId == userId);
        
        if (flashcard != null)
        {
            flashcard.ReviewCount++;
            flashcard.LastReviewedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}

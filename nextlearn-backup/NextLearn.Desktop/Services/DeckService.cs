using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public class DeckService
{
    private readonly AppDbContext _context;
    private readonly UserService _userService;

    public DeckService(AppDbContext context, UserService userService)
    {
        _context = context;
        _userService = userService;
    }

    public List<Deck> GetPublishedDecks()
    {
        return _context.Decks
            .Where(d => d.IsPublished && d.IsReviewed)
            .Include(d => d.Pages)
            .OrderByDescending(d => d.CreatedAt)
            .ToList();
    }

    public List<Deck> GetDecksByCategory(string category)
    {
        return _context.Decks
            .Where(d => d.IsPublished && d.IsReviewed && d.Category == category)
            .Include(d => d.Pages)
            .ToList();
    }

    public Deck? GetDeckById(Guid deckId)
    {
        return _context.Decks
            .Include(d => d.Pages.OrderBy(p => p.PageNumber))
            .FirstOrDefault(d => d.Id == deckId);
    }

    public void SaveOrUpdateDeck(Deck deck)
    {
        var existing = _context.Decks.Include(d => d.Pages)
            .FirstOrDefault(d => d.Id == deck.Id);
        if (existing != null)
        {
            existing.Title = deck.Title;
            existing.HasExplicitTitle = deck.HasExplicitTitle;
            existing.Description = deck.Description;
            existing.Category = deck.Category;
            existing.PageCount = deck.PageCount;
            existing.CreatedAt = deck.CreatedAt;

            _context.Pages.RemoveRange(existing.Pages);
            foreach (var page in deck.Pages)
            {
                page.DeckId = deck.Id;
                _context.Pages.Add(page);
            }
        }
        else
        {
            deck.AuthorId = _userService.GetCurrentUserId();
            _context.Decks.Add(deck);
        }
        _context.SaveChanges();
    }

    public Page? GetPage(Guid pageId)
    {
        return _context.Pages.FirstOrDefault(p => p.Id == pageId);
    }

    public async Task<UserProgress?> GetUserProgressAsync(Guid deckId)
    {
        var userId = _userService.GetCurrentUserId();
        return await _context.UserProgress
            .FirstOrDefaultAsync(up => up.UserId == userId && up.DeckId == deckId);
    }

    public async Task<UserProgress> StartLearningAsync(Guid deckId)
    {
        var userId = _userService.GetCurrentUserId();
        
        var progress = await _context.UserProgress
            .FirstOrDefaultAsync(up => up.UserId == userId && up.DeckId == deckId);

        if (progress == null)
        {
            progress = new UserProgress
            {
                UserId = userId,
                DeckId = deckId,
                CurrentPage = 1,
                IsCompleted = false
            };
            _context.UserProgress.Add(progress);
            await _context.SaveChangesAsync();
        }

        var activeCount = await _context.ActiveLearning
            .CountAsync(al => al.UserId == userId);

        if (activeCount < 2)
        {
            var slot = activeCount + 1;
            var active = new ActiveLearning
            {
                UserId = userId,
                DeckId = deckId,
                Slot = slot
            };
            _context.ActiveLearning.Add(active);
            await _context.SaveChangesAsync();
        }

        return progress;
    }

    public async Task UpdateProgressAsync(Guid deckId, int currentPage)
    {
        var userId = _userService.GetCurrentUserId();
        
        var progress = await _context.UserProgress
            .FirstOrDefaultAsync(up => up.UserId == userId && up.DeckId == deckId);

        if (progress == null)
        {
            progress = new UserProgress
            {
                UserId = userId,
                DeckId = deckId,
                CurrentPage = currentPage,
                IsCompleted = false
            };
            _context.UserProgress.Add(progress);
        }
        else
        {
            progress.CurrentPage = currentPage;
        }
        progress.LastAccessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task MarkCompletedAsync(Guid deckId)
    {
        var userId = _userService.GetCurrentUserId();
        
        var progress = await _context.UserProgress
            .FirstOrDefaultAsync(up => up.UserId == userId && up.DeckId == deckId);

        if (progress != null)
        {
            progress.IsCompleted = true;
            var user = _userService.GetCurrentUser();
            user.TotalDecksCompleted++;
            await _context.SaveChangesAsync();
        }
    }

    public List<ActiveLearning> GetActiveLearningSlots()
    {
        var userId = _userService.GetCurrentUserId();
        return _context.ActiveLearning
            .Include(al => al.Deck)
            .Where(al => al.UserId == userId)
            .OrderBy(al => al.Slot)
            .ToList();
    }

    public async Task RemoveActiveSlotAsync(int slot)
    {
        var userId = _userService.GetCurrentUserId();
        var active = await _context.ActiveLearning
            .FirstOrDefaultAsync(al => al.UserId == userId && al.Slot == slot);
        
        if (active != null)
        {
            _context.ActiveLearning.Remove(active);
            await _context.SaveChangesAsync();
        }
    }

    public async Task GiveFeedbackAsync(Guid deckId, Guid? pageId, string message, string requestedTopic)
    {
        var feedback = new Feedback
        {
            UserId = _userService.GetCurrentUserId(),
            DeckId = deckId,
            PageId = pageId,
            Message = message,
            RequestedTopic = requestedTopic
        };
        _context.Feedbacks.Add(feedback);
        await _context.SaveChangesAsync();
    }
}

using System;
using System.Collections.Generic;
using System.IO;
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
            existing.PageCount = deck.PageCount;
            existing.CreatedAt = deck.CreatedAt;
            existing.IsArchived = deck.IsArchived;
            existing.IsPinned = deck.IsPinned;

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

    public void ArchiveDeck(Guid deckId, string decksPath)
    {
        var deck = _context.Decks.FirstOrDefault(d => d.Id == deckId);
        if (deck == null) return;
        var oldPath = Path.Combine(decksPath, deck.FileName);
        var newPath = oldPath + "~";
        if (File.Exists(oldPath))
            File.Move(oldPath, newPath);
        deck.IsArchived = true;
        _context.SaveChanges();
    }

    public void UnarchiveDeck(Guid deckId, string decksPath)
    {
        var deck = _context.Decks.FirstOrDefault(d => d.Id == deckId);
        if (deck == null) return;
        var archivedPath = Path.Combine(decksPath, deck.FileName + "~");
        var restoredPath = Path.Combine(decksPath, deck.FileName);
        if (File.Exists(archivedPath))
            File.Move(archivedPath, restoredPath);
        deck.IsArchived = false;
        _context.SaveChanges();
    }

    public void PinDeck(Guid deckId, string decksPath)
    {
        var deck = _context.Decks.FirstOrDefault(d => d.Id == deckId);
        if (deck == null) return;
        var oldPath = Path.Combine(decksPath, deck.FileName);
        var newPath = Path.Combine(decksPath, "+" + deck.FileName);
        if (File.Exists(oldPath))
            File.Move(oldPath, newPath);
        deck.IsPinned = true;
        deck.FileName = "+" + deck.FileName;
        _context.SaveChanges();
    }

    public void UnpinDeck(Guid deckId, string decksPath)
    {
        var deck = _context.Decks.FirstOrDefault(d => d.Id == deckId);
        if (deck == null) return;
        var fileName = deck.FileName;
        if (!fileName.StartsWith('+')) return;
        var oldPath = Path.Combine(decksPath, fileName);
        var newFileName = fileName[1..];
        var newPath = Path.Combine(decksPath, newFileName);
        if (File.Exists(oldPath))
            File.Move(oldPath, newPath);
        deck.IsPinned = false;
        deck.FileName = newFileName;
        _context.SaveChanges();
    }

    public List<Deck> GetArchivedDecks(string decksPath)
    {
        var list = new List<Deck>();
        var extensions = new[] { "*.md~", "*.org~" };
        foreach (var ext in extensions)
        {
            foreach (var file in Directory.GetFiles(decksPath, ext))
            {
                var deck = DeckFileParser.LoadDeckFromFile(file);
                if (deck != null)
                    list.Add(deck);
            }
        }
        return list;
    }

    public List<Deck> GetPinnedDecks(string decksPath)
    {
        var list = new List<Deck>();
        var extensions = new[] { "+*.md", "+*.org" };
        foreach (var ext in extensions)
        {
            foreach (var file in Directory.GetFiles(decksPath, ext))
            {
                var deck = DeckFileParser.LoadDeckFromFile(file);
                if (deck != null)
                    list.Add(deck);
            }
        }
        return list;
    }
}

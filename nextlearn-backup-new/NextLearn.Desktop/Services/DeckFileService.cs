using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public class DeckFileService
{
    private readonly AppDbContext _context;

    public DeckFileService(AppDbContext context)
    {
        _context = context;
    }

    public void ArchiveDeck(Guid deckId, string decksPath)
    {
        var deck = _context.Decks.FirstOrDefault(d => d.Id == deckId);
        if (deck == null)
        {
            return;
        }

        var oldPath = Path.Combine(decksPath, deck.FileName);
        var newPath = oldPath + "~";
        if (File.Exists(oldPath))
        {
            File.Move(oldPath, newPath);
        }

        deck.IsArchived = true;
        _context.SaveChanges();
    }

    public void UnarchiveDeck(Guid deckId, string decksPath)
    {
        var deck = _context.Decks.FirstOrDefault(d => d.Id == deckId);
        if (deck == null)
        {
            return;
        }

        var archivedPath = Path.Combine(decksPath, deck.FileName + "~");
        var restoredPath = Path.Combine(decksPath, deck.FileName);
        if (File.Exists(archivedPath))
        {
            File.Move(archivedPath, restoredPath);
        }

        deck.IsArchived = false;
        _context.SaveChanges();
    }

    public void PinDeck(Guid deckId, string decksPath)
    {
        var deck = _context.Decks.FirstOrDefault(d => d.Id == deckId);
        if (deck == null)
        {
            return;
        }

        var oldPath = Path.Combine(decksPath, deck.FileName);
        var newPath = Path.Combine(decksPath, "+" + deck.FileName);
        if (File.Exists(oldPath))
        {
            File.Move(oldPath, newPath);
        }

        deck.IsPinned = true;
        deck.FileName = "+" + deck.FileName;
        _context.SaveChanges();
    }

    public void UnpinDeck(Guid deckId, string decksPath)
    {
        var deck = _context.Decks.FirstOrDefault(d => d.Id == deckId);
        if (deck == null)
        {
            return;
        }

        var fileName = deck.FileName;
        if (!fileName.StartsWith('+'))
        {
            return;
        }

        var oldPath = Path.Combine(decksPath, fileName);
        var newFileName = fileName[1..];
        var newPath = Path.Combine(decksPath, newFileName);
        if (File.Exists(oldPath))
        {
            File.Move(oldPath, newPath);
        }

        deck.IsPinned = false;
        deck.FileName = newFileName;
        _context.SaveChanges();
    }

    public static List<Deck> GetArchivedDecks(string decksPath)
    {
        var list = new List<Deck>();
        var extensions = new[] { "*.md~", "*.org~" };
        foreach (var ext in extensions)
        {
            foreach (var file in Directory.GetFiles(decksPath, ext))
            {
                var deck = DeckFileParser.LoadDeckFromFile(file);
                if (deck != null)
                {
                    list.Add(deck);
                }
            }
        }

        return list;
    }

    public static List<Deck> GetPinnedDecks(string decksPath)
    {
        var list = new List<Deck>();
        var extensions = new[] { "+*.md", "+*.org" };
        foreach (var ext in extensions)
        {
            foreach (var file in Directory.GetFiles(decksPath, ext))
            {
                var deck = DeckFileParser.LoadDeckFromFile(file);
                if (deck != null)
                {
                    list.Add(deck);
                }
            }
        }

        return list;
    }
}

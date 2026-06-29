using System;
using System.Collections.Generic;
using System.IO;
using NextLearn.Desktop.Models;
using Serilog;

namespace NextLearn.Desktop.Services;

public class DeckFileService : IDeckFileService
{
    public void ArchiveDeck(Deck deck, string decksPath)
    {
        ArgumentNullException.ThrowIfNull(deck);

        var name = Path.GetFileName(deck.FileName);
        if (name.EndsWith('~'))
        {
            return;
        }

        var oldPath = Path.Combine(decksPath, deck.FileName);
        var newFileName = deck.FileName + "~";
        var newPath = oldPath + "~";
        if (File.Exists(oldPath))
        {
            try
            {
                File.Move(oldPath, newPath);
            }
            catch (IOException ex)
            {
                Log.Error(ex, "Failed to archive deck {FileName} from {OldPath} to {NewPath}", deck.FileName, oldPath, newPath);
                return;
            }
        }

        deck.FileName = newFileName;
        deck.IsArchived = true;
    }

    public void UnarchiveDeck(Deck deck, string decksPath)
    {
        ArgumentNullException.ThrowIfNull(deck);

        var name = Path.GetFileName(deck.FileName);
        if (!name.EndsWith('~'))
        {
            return;
        }

        var oldPath = Path.Combine(decksPath, deck.FileName);
        var newFileName = deck.FileName[..^1];
        var newPath = Path.Combine(decksPath, newFileName);
        if (File.Exists(oldPath))
        {
            try
            {
                File.Move(oldPath, newPath);
            }
            catch (IOException ex)
            {
                Log.Error(ex, "Failed to unarchive deck {FileName} from {OldPath} to {NewPath}", deck.FileName, oldPath, newPath);
                return;
            }
        }

        deck.FileName = newFileName;
        deck.IsArchived = false;
    }

    public void PinDeck(Deck deck, string decksPath)
    {
        ArgumentNullException.ThrowIfNull(deck);

        var name = Path.GetFileName(deck.FileName);
        if (name.StartsWith('+'))
        {
            return;
        }

        var dir = Path.GetDirectoryName(deck.FileName);
        var oldPath = Path.Combine(decksPath, deck.FileName);
        var newName = "+" + name;
        var newFileName = string.IsNullOrEmpty(dir) ? newName : Path.Combine(dir, newName);
        var newPath = Path.Combine(decksPath, newFileName);
        if (File.Exists(oldPath))
        {
            var destDir = Path.GetDirectoryName(newPath);
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            try
            {
                File.Move(oldPath, newPath);
            }
            catch (IOException ex)
            {
                Log.Error(ex, "Failed to pin deck {FileName} from {OldPath} to {NewPath}", deck.FileName, oldPath, newPath);
                return;
            }
        }

        deck.FileName = newFileName;
        deck.IsPinned = true;
    }

    public void UnpinDeck(Deck deck, string decksPath)
    {
        ArgumentNullException.ThrowIfNull(deck);

        var name = Path.GetFileName(deck.FileName);
        if (!name.StartsWith('+'))
        {
            return;
        }

        var dir = Path.GetDirectoryName(deck.FileName);
        var oldPath = Path.Combine(decksPath, deck.FileName);
        var newName = name[1..];
        var newFileName = string.IsNullOrEmpty(dir) ? newName : Path.Combine(dir, newName);
        var newPath = Path.Combine(decksPath, newFileName);
        if (File.Exists(oldPath))
        {
            try
            {
                File.Move(oldPath, newPath);
            }
            catch (IOException ex)
            {
                Log.Error(ex, "Failed to unpin deck {FileName} from {OldPath} to {NewPath}", deck.FileName, oldPath, newPath);
                return;
            }
        }

        deck.FileName = newFileName;
        deck.IsPinned = false;
    }

    public static List<Deck> GetArchivedDecks(string decksPath)
    {
        var list = new List<Deck>();
        var extensions = new[] { "*.md~", "*.org~" };
        foreach (var ext in extensions)
        {
            foreach (var file in Directory.GetFiles(decksPath, ext, SearchOption.AllDirectories))
            {
                var deck = DeckFileParser.LoadDeckFromFile(file, decksPath);
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
            foreach (var file in Directory.GetFiles(decksPath, ext, SearchOption.AllDirectories))
            {
                var deck = DeckFileParser.LoadDeckFromFile(file, decksPath);
                if (deck != null)
                {
                    list.Add(deck);
                }
            }
        }

        return list;
    }
}

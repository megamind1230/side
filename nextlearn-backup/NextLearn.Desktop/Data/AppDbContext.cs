using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Deck> Decks => Set<Deck>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<UserProgress> UserProgress => Set<UserProgress>();
    public DbSet<ActiveLearning> ActiveLearning => Set<ActiveLearning>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<DailyActivity> DailyActivities => Set<DailyActivity>();

    private readonly string _dbPath;

    public AppDbContext()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "nextlearn");
        Directory.CreateDirectory(appFolder);
        _dbPath = Path.Combine(appFolder, "nextlearn.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Deck>()
            .HasIndex(d => d.Title);

        modelBuilder.Entity<Page>()
            .HasIndex(p => new { p.DeckId, p.PageNumber })
            .IsUnique();

        modelBuilder.Entity<UserProgress>()
            .HasIndex(up => new { up.UserId, up.DeckId })
            .IsUnique();

        modelBuilder.Entity<ActiveLearning>()
            .HasIndex(al => new { al.UserId, al.Slot })
            .IsUnique();

        modelBuilder.Entity<Flashcard>()
            .HasIndex(f => new { f.UserId, f.PageId })
            .IsUnique();

        modelBuilder.Entity<DailyActivity>()
            .HasIndex(da => new { da.UserId, da.Date })
            .IsUnique();

        // SeedData(modelBuilder);
    }

    /* // seeding disabled - using files from ~/nextlearn/decks/ instead
    private static void SeedData(ModelBuilder modelBuilder)
    {
        var guestUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = guestUserId,
            DisplayName = "Guest",
            IsGuest = true,
            CreatedAt = DateTime.UtcNow,
            LastActiveDate = DateTime.UtcNow.Date
        });

        var deckId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        modelBuilder.Entity<Deck>().HasData(new Deck
        {
            Id = deckId,
            Title = "How to use WhatsApp",
            Description = "Learn the basics of WhatsApp messaging app",
            Category = "moms",
            Difficulty = "lvl0",
            AuthorId = guestUserId,
            IsPublished = true,
            IsReviewed = true,
            CreatedAt = DateTime.UtcNow,
            PageCount = 3
        });

        modelBuilder.Entity<Page>().HasData(
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000020"),
                DeckId = deckId,
                PageNumber = 1,
                Title = "What is WhatsApp?",
                ContentType = ContentType.Text,
                TextContent = "WhatsApp is a free messaging app that lets you send text messages, voice calls, video calls, images, and documents to people who also have WhatsApp. It uses your internet connection to work, so you don't pay for SMS messages."
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000021"),
                DeckId = deckId,
                PageNumber = 2,
                Title = "Download WhatsApp",
                ContentType = ContentType.Text,
                TextContent = "To download WhatsApp:\n1. Open the App Store (iPhone) or Play Store (Android)\n2. Search for 'WhatsApp Messenger'\n3. Tap Install\n4. Open the app when downloaded"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000022"),
                DeckId = deckId,
                PageNumber = 3,
                Title = "Send your first message",
                ContentType = ContentType.Text,
                TextContent = "To send a message:\n1. Tap the Chats tab\n2. Tap 'New Chat' (speech bubble icon)\n3. Search for or select a contact\n4. Type your message in the text box\n5. Tap the green arrow to send"
            }
        );

        var deckId2 = Guid.Parse("00000000-0000-0000-0000-000000000011");
        modelBuilder.Entity<Deck>().HasData(new Deck
        {
            Id = deckId2,
            Title = "Learn Numbers 1-10",
            Description = "Fun way to learn numbers for children",
            Category = "children",
            Difficulty = "lvl0",
            AuthorId = guestUserId,
            IsPublished = true,
            IsReviewed = true,
            CreatedAt = DateTime.UtcNow,
            PageCount = 5
        });

        modelBuilder.Entity<Page>().HasData(
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000030"),
                DeckId = deckId2,
                PageNumber = 1,
                Title = "Number 1",
                ContentType = ContentType.Text,
                TextContent = "1 - One\nThis is the number 1. It comes first when we count!"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000031"),
                DeckId = deckId2,
                PageNumber = 2,
                Title = "Number 2",
                ContentType = ContentType.Text,
                TextContent = "2 - Two\nTwo is one more than one. We have two eyes!"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000032"),
                DeckId = deckId2,
                PageNumber = 3,
                Title = "Number 3",
                ContentType = ContentType.Text,
                TextContent = "3 - Three\nThree is a special number! A triangle has 3 sides."
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000033"),
                DeckId = deckId2,
                PageNumber = 4,
                Title = "Number 4",
                ContentType = ContentType.Text,
                TextContent = "4 - Four\nFour is a square number! A car has 4 wheels."
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000034"),
                DeckId = deckId2,
                PageNumber = 5,
                Title = "Number 5",
                ContentType = ContentType.Text,
                TextContent = "5 - Five\nHigh five! We have 5 fingers on each hand!"
            }
        );

        var deckId3 = Guid.Parse("00000000-0000-0000-0000-000000000012");
        modelBuilder.Entity<Deck>().HasData(new Deck
        {
            Id = deckId3,
            Title = "VS Code Shortcuts",
            Description = "Essential keyboard shortcuts for VS Code",
            Category = "students",
            Difficulty = "lvl1",
            AuthorId = guestUserId,
            IsPublished = true,
            IsReviewed = true,
            CreatedAt = DateTime.UtcNow,
            PageCount = 3
        });

        modelBuilder.Entity<Page>().HasData(
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000040"),
                DeckId = deckId3,
                PageNumber = 1,
                Title = "Command Palette",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+Shift+P (Windows/Linux) or Cmd+Shift+P (Mac)\n\nThis opens the Command Palette - your gateway to everything in VS Code! You can run any command without using your mouse."
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000041"),
                DeckId = deckId3,
                PageNumber = 2,
                Title = "Quick File Open",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+P (Windows/Linux) or Cmd+P (Mac)\n\nQuickly open any file in your project by typing its name. Use arrows to navigate and Enter to open."
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000042"),
                DeckId = deckId3,
                PageNumber = 3,
                Title = "Multi-cursor Editing",
                ContentType = ContentType.Text,
                TextContent = "Alt+Click (Windows/Linux) or Option+Click (Mac)\n\nHold Alt/Option and click multiple places to edit at once! Also try Ctrl+D to select the next occurrence of the current word."
            }
        );

        var deckId4 = Guid.Parse("00000000-0000-0000-0000-000000000013");
        modelBuilder.Entity<Deck>().HasData(new Deck
        {
            Id = deckId4,
            Title = "VS Code Shortcuts",
            Description = "Essential keyboard shortcuts for VS Code - comprehensive guide",
            Category = "text-editors",
            Difficulty = "lvl0",
            AuthorId = guestUserId,
            IsPublished = true,
            IsReviewed = true,
            CreatedAt = DateTime.UtcNow,
            PageCount = 5
        });

        modelBuilder.Entity<Page>().HasData(
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000050"),
                DeckId = deckId4,
                PageNumber = 1,
                Title = "Command Palette & Quick Open",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+Shift+P - Command Palette (your best friend!)\n\nCtrl+P - Quick file open - write down filename\n\nCtrl+K Ctrl+P - Show all keybindings\n\nCtrl+Tab - Switch between open files\n\nCtrl+1, Ctrl+2 - Focus tab in split view"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000051"),
                DeckId = deckId4,
                PageNumber = 2,
                Title = "Terminal & Navigation",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+` - Toggle terminal\n\nIn terminal: code folderName - opens folder in VS Code\n\nCtrl+G - Go to line number\n\nCtrl+Shift+O - List all functions/variables to navigate\n\nCtrl+P then @ - Navigate symbols in file\n\nAlt+Left/Right - Switch between files"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000052"),
                DeckId = deckId4,
                PageNumber = 3,
                Title = "Editor & Multi-cursor",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+\\ - Split editor\n\nCtrl+B - Toggle sidebar for more view space\n\nAlt+Arrow - Move line up/down\n\nAlt+Shift+Arrow - Duplicate line\n\nAlt+Click - Multi-cursor editing\n\nCtrl+D - Add next match to selection\n\nCtrl+Shift+L - Select all matches"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000053"),
                DeckId = deckId4,
                PageNumber = 4,
                Title = "Code Editing Features",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+. - View actions you can do with selection\n\nCtrl+Space - Recommend functions\n\nCtrl+Shift+Alt+Arrow - Multi-line cursor without mouse\n\nShift+Alt+Mouse - Box selection\n\nCtrl+Click - Go to definition\n\nCtrl+Hover - More info on symbol\n\nAlt+Enter (after searching word) - Put cursor on each occurrence"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000054"),
                DeckId = deckId4,
                PageNumber = 5,
                Title = "Advanced Shortcuts",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+K Z - Zen mode (focus on just opened tab)\n\nCtrl+K W - Close all windows\n\nAlt+Z - Toggle word wrap\n\nCtrl+Up/Down - Scroll like mouse\n\nType path like 'folder/folder2/file.ext' - Creates folder hierarchy\n\nEmmet: h1.someTitle - Creates h1 tag with class"
            }
        );

        var deckId5 = Guid.Parse("00000000-0000-0000-0000-000000000014");
        modelBuilder.Entity<Deck>().HasData(new Deck
        {
            Id = deckId5,
            Title = "Emacs Shortcuts",
            Description = "Essential keyboard shortcuts for Emacs editor",
            Category = "text-editors",
            Difficulty = "lvl0",
            AuthorId = guestUserId,
            IsPublished = true,
            IsReviewed = true,
            CreatedAt = DateTime.UtcNow,
            PageCount = 4
        });

        modelBuilder.Entity<Page>().HasData(
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000060"),
                DeckId = deckId5,
                PageNumber = 1,
                Title = "Movement & Navigation",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+P/N - Previous/Next line\n\nCtrl+B/F - Backward/Forward character\n\nAlt+B/F - Backward/Forward word\n\nCtrl+A/E - Beginning/End of line\n\nAlt+A/E - Beginning/End of sentence\n\nPageUp/PageDown - Scroll"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000061"),
                DeckId = deckId5,
                PageNumber = 2,
                Title = "Buffers & Windows",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+X K - Kill buffer\n\nCtrl+X Left/Right - Previous/Next buffer\n\nCtrl+X 0 - Delete current window\n\nCtrl+X 1 - Delete other windows\n\nCtrl+X 2 - Horizontal split\n\nCtrl+X 3 - Vertical split"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000062"),
                DeckId = deckId5,
                PageNumber = 3,
                Title = "Editing (Cut/Paste/Undo)",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+Space (move) Ctrl+W - Cut selection\n\nCtrl+Y - Paste (Yank)\n\nAlt+Y - Cycle through copy history\n\nCtrl+X U or Ctrl+/ or Ctrl+_ - Undo\n\nCtrl+H K - Help with keybinding\n\nCtrl+D or BS - Delete character\n\nAlt+D or Alt+BS - Delete word\n\nCtrl+K - Delete to end of line"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000063"),
                DeckId = deckId5,
                PageNumber = 4,
                Title = "Org Mode",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+C C - Add tag\n\nCtrl+C X - Save file\n\nCtrl+C L - Insert link\n\nCtrl+C O - Open link\n\nCtrl+C B - Toggle checkbox"
            }
        );

        var deckId6 = Guid.Parse("00000000-0000-0000-0000-000000000015");
        modelBuilder.Entity<Deck>().HasData(new Deck
        {
            Id = deckId6,
            Title = "Discord Shortcuts",
            Description = "Essential keyboard shortcuts for Discord desktop app",
            Category = "social-media",
            Difficulty = "lvl0",
            AuthorId = guestUserId,
            IsPublished = true,
            IsReviewed = true,
            CreatedAt = DateTime.UtcNow,
            PageCount = 5
        });

        modelBuilder.Entity<Page>().HasData(
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000070"),
                DeckId = deckId6,
                PageNumber = 1,
                Title = "General & Quick Switcher",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+/ - Shortcuts helper\n\nCtrl+F - Find/Search\n\nCtrl+K - Quick switcher (servers, channels, DMs)\n\nF6 - Switch between sections/panes (4 panes: servers, channels, chat, sidebar)\n\nShift+hover message - Shows more options"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000071"),
                DeckId = deckId6,
                PageNumber = 2,
                Title = "In Call Shortcuts",
                ContentType = ContentType.Text,
                TextContent = "Ctrl+Shift+M - Toggle mute (others can't hear you)\n\nCtrl+Shift+D - Toggle deafen (you can't hear others)\n\nCtrl+' - Start call in DM chat\n\nCtrl+Enter - Answer call\n\nEsc - Cancel call"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000072"),
                DeckId = deckId6,
                PageNumber = 3,
                Title = "Navigation & Panes",
                ContentType = ContentType.Text,
                TextContent = "Alt+Up/Down - Navigate channels within server or DMs\n\nAlt+Left/Right - Navigate channel history or DM history\n\nCtrl+Alt+Up/Down - Navigate servers (pane 1)\n\nCtrl+Alt+Shift+V - Focus current voice call room\n\nTab - Move focus, Space/Enter to select\n\nEsc - Mark channel as read"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000073"),
                DeckId = deckId6,
                PageNumber = 4,
                Title = "Chat Features",
                ContentType = ContentType.Text,
                TextContent = "Tab - Move focus to text area, then Up to focus messages\n\nE - Edit message\n\nBackspace - Delete message\n\nP - Pin message\n\nR - Reply\n\n+ - React to message\n\nCtrl+C - Copy message text\n\nCtrl+E - Emoji, Ctrl+S - Sticker, Ctrl+G - GIF\n\nCtrl+Shift+U - Upload file\n\nCtrl+P - Show pinned messages\n\nCtrl+I - Inbox\n\nCtrl+U - Toggle right sidebar"
            },
            new Page
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000074"),
                DeckId = deckId6,
                PageNumber = 5,
                Title = "Extra Shortcuts",
                ContentType = ContentType.Text,
                TextContent = "Alt+Shift+Up/Down - Switch between unread channels\n\nCtrl+Alt+Shift+Up/Dow n - Switch between unread channels with mentions\n\nCtrl+Shift+H - Discord help/support page\n\nCtrl+Shift+T - Create private group\n\nSelect text with mouse then Up - Edit your last message"
            }
        );
    }
    */ // end seeding disabled
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextLearn.Desktop.Models;

/// <summary>Tracks a user's learning progress through a deck.</summary>
public class UserProgress
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    public Guid DeckId { get; set; }

    [ForeignKey(nameof(DeckId))]
    public Deck? Deck { get; set; }

    public int CurrentPage { get; set; } = 1;

    public bool IsCompleted { get; set; }

    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
}

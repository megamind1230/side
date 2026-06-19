using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextLearn.Desktop.Models;

/// <summary>An active learning slot (up to 2 concurrent decks).</summary>
public class ActiveLearning
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

    public int Slot { get; set; }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextLearn.Models;

public class Feedback
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
    
    public Guid? PageId { get; set; }
    
    [ForeignKey(nameof(PageId))]
    public Page? Page { get; set; }
    
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string RequestedTopic { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsResolved { get; set; }
}

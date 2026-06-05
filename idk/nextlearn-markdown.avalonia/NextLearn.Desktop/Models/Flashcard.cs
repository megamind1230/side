using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextLearn.Desktop.Models;

public class Flashcard
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }
    
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
    
    [Required]
    public Guid PageId { get; set; }
    
    [ForeignKey(nameof(PageId))]
    public Page? Page { get; set; }
    
    [Required]
    [MaxLength(1000)]
    public string Front { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(2000)]
    public string Back { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public int ReviewCount { get; set; }
    
    public DateTime? LastReviewedAt { get; set; }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextLearn.Models;

public class Deck
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string Category { get; set; } = "general";
    
    [MaxLength(10)]
    public string Difficulty { get; set; } = "lvl0";
    
    public Guid AuthorId { get; set; }
    
    [ForeignKey(nameof(AuthorId))]
    public User? Author { get; set; }
    
    public bool IsPublished { get; set; }
    
    public bool IsReviewed { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public int PageCount { get; set; }
    
    public int DownloadsCount { get; set; }
    
    public ICollection<Page> Pages { get; set; } = new List<Page>();
    
    public ICollection<UserProgress> UserProgress { get; set; } = new List<UserProgress>();
}

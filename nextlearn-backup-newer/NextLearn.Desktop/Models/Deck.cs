using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextLearn.Desktop.Models;

/// <summary>A deck of study pages loaded from a markdown or org file.</summary>
public class Deck
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public bool HasExplicitTitle { get; set; }

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Tags { get; set; } = string.Empty;

    public Guid AuthorId { get; set; }

    [ForeignKey(nameof(AuthorId))]
    public User? Author { get; set; }

    public bool IsPublished { get; set; }

    public bool IsReviewed { get; set; }

    public bool IsArchived { get; set; }

    public bool IsPinned { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int PageCount { get; set; }

    public ICollection<Page> Pages { get; set; } = new List<Page>();

    public ICollection<UserProgress> UserProgress { get; set; } = new List<UserProgress>();
}

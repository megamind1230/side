using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextLearn.Desktop.Models;

/// <summary>A single study page within a deck.</summary>
public class Page
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DeckId { get; set; }

    [ForeignKey(nameof(DeckId))]
    public Deck? Deck { get; set; }

    public int PageNumber { get; set; }

    [MaxLength(200)]
    public string? SectionTitle { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public ContentType ContentType { get; set; } = ContentType.Text;

    public string? TextContent { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsPreHeadingPage { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsTocPage { get; set; }
}

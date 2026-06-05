namespace WeNeedA.Web.Models;

public class City
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int GovernorateId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Governorate Governorate { get; set; } = null!;
    public ICollection<Village> Villages { get; set; } = new List<Village>();
}

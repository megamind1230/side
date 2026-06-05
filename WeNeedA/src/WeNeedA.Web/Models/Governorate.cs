namespace WeNeedA.Web.Models;

public class Governorate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int CountryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Country Country { get; set; } = null!;
    public ICollection<City> Cities { get; set; } = new List<City>();
}

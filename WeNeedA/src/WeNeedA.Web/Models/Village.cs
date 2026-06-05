namespace WeNeedA.Web.Models;

public class Village
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int CityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public City City { get; set; } = null!;
    public ICollection<PersonListing> PersonListings { get; set; } = new List<PersonListing>();
}

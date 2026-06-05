namespace WeNeedA.Web.Models;

public class PersonListingTag
{
    public int PersonListingId { get; set; }
    public PersonListing PersonListing { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}

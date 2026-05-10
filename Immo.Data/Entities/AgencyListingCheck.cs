namespace Immo.Data.Entities;

public class AgencyListingCheck
{
    public int Id { get; set; }
    public int AgencyId { get; set; }
    public Agency Agency { get; set; } = null!;
    public List<string> UrlPosibilities { get; set; } = [];
}
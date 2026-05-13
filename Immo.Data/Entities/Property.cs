using System;

namespace Immo.Data.Entities;

public class Property
{
    public int Id { get; set; }
    public required string SourceUrl { get; set; }
    public required string SourceDomain { get; set; }
    public string? ExternalId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public int? Bedrooms { get; set; }
    public double? LivingArea { get; set; }
    public double? PlotArea { get; set; }
    public string? ImageUrl { get; set; }
    public string? EpcScore { get; set; }
    public bool Sold { get; set; } = false;
    public bool UnderOption { get; set; } = false;
    
    public int RawPageId { get; set; }
    
    public int? AgencyId { get; set; }
    public Agency? Agency { get; set; }
}

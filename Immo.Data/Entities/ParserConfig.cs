using System.ComponentModel.DataAnnotations;

namespace Immo.Data.Entities;

public class ParserConfig
{
    public int Id { get; set; }
    public int AgencyId { get; set; }
    public Agency? Agency { get; set; } = null!;

    // Direct field selectors (XPath)
    [Required]
    public string? TitleSelector { get; set; }
    [Required]
    public string? PriceSelector { get; set; }
    public string? DescriptionSelector { get; set; }
    public string? ImageSelector { get; set; }
    public string? AddressSelector { get; set; }
    public string? BedroomSelector { get; set; }

    // ExternalId regex applied to URL
    public string? ExternalIdPattern { get; set; }  // e.g. "/(\d+)$"

    // Spec table structure
    public string? SpecContainerSelector { get; set; }  // e.g. "//dl//div"
    public string? SpecLabelSelector { get; set; }       // e.g. ".//dt"
    public string? SpecValueSelector { get; set; }       // e.g. ".//dd"

    // Label mappings (case-insensitive contains match)
    public string? BedroomLabel { get; set; }     // e.g. "Slaapkamers"
    public bool CountBedrooms { get; set; } = false;
    public string? LivingAreaLabel { get; set; }  // e.g. "Bewoonbare oppervlakte"
    public string? PlotAreaLabel { get; set; }    // e.g. "Grondoppervlakte"
    public string? EpcLabel { get; set; }         // e.g. "EPC"
    public string? ReferenceLabel { get; set; }   // e.g. "Referentie"
    public string? AddressLabel { get; set; }     // e.g. "Adres"
    public string? ZipCodeLabel { get; set; }     // e.g. "Postcode"
    public string? CityLabel { get; set; }        // e.g. "Stad"
    
    public string? PriceLabel { get; set; }        // e.g. "Prijs"
}

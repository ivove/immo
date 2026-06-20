using System.ComponentModel.DataAnnotations;

namespace Immo.Data.Entities;

public class ParserConfig
{
    public int Id { get; set; }
    public int AgencyId { get; set; }
    public Agency? Agency { get; set; } = null!;

    // Direct field selectors (XPath)
    public string? TitleSelector { get; set; }
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

    // ── JSON API field paths (dot-notation, used when Agency.DataSourceType = "json_api") ──

    /// <summary>Path to the array of property items within the JSON response. Null = root is the array.</summary>
    public string? JsonArrayPath        { get; set; }  // e.g. "data" or "results.listings"

    /// <summary>Path from each item to its detail URL stored as SourceUrl.</summary>
    public string? JsonUrlPath          { get; set; }  // e.g. "detailUrl"

    /// <summary>
    /// Static URL template for the detail page, with {id} as a placeholder for the external ID.
    /// When set, this takes priority over <see cref="JsonUrlPath"/>.
    /// E.g. "https://agency.com/properties/{id}"
    /// </summary>
    public string? JsonDetailUrlTemplate { get; set; }

    public string? JsonExternalIdPath   { get; set; }  // e.g. "id"
    public string? JsonTitlePath        { get; set; }  // e.g. "title"
    public string? JsonPricePath        { get; set; }  // e.g. "price.amount"
    public string? JsonDescriptionPath  { get; set; }  // e.g. "description"
    public string? JsonImagePath        { get; set; }  // e.g. "photos[0].url"
    public string? JsonBedroomsPath     { get; set; }  // e.g. "specs.bedrooms"
    public string? JsonLivingAreaPath   { get; set; }  // e.g. "specs.livingArea"
    public string? JsonPlotAreaPath     { get; set; }  // e.g. "specs.plotArea"
    public string? JsonEpcPath          { get; set; }  // e.g. "epc.score"
    public string? JsonZipCodePath      { get; set; }  // e.g. "address.zip"
    public string? JsonCityPath         { get; set; }  // e.g. "address.city"

    /// <summary>Path to a property-type field in each JSON item used for filtering.</summary>
    public string? JsonTypeFilterPath   { get; set; }  // e.g. "type"
    /// <summary>Comma-separated list of accepted type values. Items not matching are skipped. Empty = no filter.</summary>
    public string? JsonTypeFilterValues { get; set; }  // e.g. "house,apartment"

    /// <summary>Path to a status field in each JSON item.</summary>
    public string? JsonStatusPath       { get; set; }  // e.g. "status"
    /// <summary>Value of the status field that means the property is sold.</summary>
    public string? JsonSoldValue        { get; set; }  // e.g. "sold"
    /// <summary>Value of the status field that means the property is under option.</summary>
    public string? JsonUnderOptionValue { get; set; }  // e.g. "option"
}

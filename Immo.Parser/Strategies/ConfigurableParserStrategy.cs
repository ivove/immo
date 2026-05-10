using Immo.Data;
using Immo.Data.Entities;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Immo.Parser.Strategies;

public class ConfigurableParserStrategy : IParserStrategy
{
    private readonly ImmoContext _context;

    public ConfigurableParserStrategy(ImmoContext context)
    {
        _context = context;
    }

    public bool CanParse(string url)
    {
        var domain = new Uri(url).Host.Replace("www.", "");
        return _context.ParserConfigs.Any(c => c.Agency.AgencyDomain.Contains(domain));
    }

    public Property Parse(RawPage page, HtmlDocument document)
    {
        var domain = new Uri(page.Url).Host.Replace("www.", "");
        var config = _context.ParserConfigs
            .Include(c => c.Agency)
            .FirstOrDefault(c => c.Agency.AgencyDomain.Contains(domain));

        if (config == null) return null!;

        var title = ExtractString(document, config.TitleSelector);
        var description = ExtractString(document, config.DescriptionSelector);
        var price = ExtractDecimal(document, config.PriceSelector);
        var imageUrl = ExtractImageUrl(document, page.Url, config.ImageSelector);
        
        var externalId = string.IsNullOrEmpty(config.ExternalIdPattern) 
            ? null 
            : Regex.Match(page.Url, config.ExternalIdPattern).Groups[1].Value;

        int? bedrooms = null;
        if (!string.IsNullOrEmpty(config.BedroomSelector)) {
            bedrooms = ExtractInt(document, config.BedroomSelector);
        }

        double? livingArea = null;
        double? plotArea = null;
        string? epcScore = null;
        string? zipCode = null;
        string? city = null;
        if (config.CountBedrooms) {
            bedrooms = 0;
        }

        var addressText = ExtractString(document, config.AddressSelector);
        if (!string.IsNullOrEmpty(addressText))
        {
            (zipCode, city) = ParseAddress(addressText);
        }

        if (!string.IsNullOrEmpty(config.SpecContainerSelector))
        {
            var specNodes = document.DocumentNode.SelectNodes(config.SpecContainerSelector);
            if (specNodes != null)
            {
                foreach (var node in specNodes)
                {
                    var label = node.SelectSingleNode(config.SpecLabelSelector ?? ".")?.InnerText?.Trim();
                    var value = node.SelectSingleNode(config.SpecValueSelector ?? ".")?.InnerText?.Trim();

                    if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(value)) continue;

                    if (Matches(label, config.BedroomLabel))
                    {
                        if (!config.CountBedrooms){
                            if (int.TryParse(Regex.Replace(value, @"[^0-9]", ""), out var b)) bedrooms = b;
                        }
                        else{
                            bedrooms++;
                        }
                    }
                    else if (Matches(label, config.LivingAreaLabel))
                    {                        
                        var areaValue = value.ToLowerInvariant();
                        if (areaValue.Contains("m")){
                            var temp = areaValue.Split("m");
                            areaValue = temp[0];
                        }
                        areaValue = areaValue.Replace(",00 ", "", StringComparison.OrdinalIgnoreCase);
                        if (double.TryParse(Regex.Replace(areaValue, @"[^0-9]", ""), out var a)) livingArea = a;
                    }
                    else if (Matches(label, config.PlotAreaLabel))
                    {
                        var areaValue = value.ToLowerInvariant();
                        if (areaValue.Contains("m")){
                            var temp = areaValue.Split("m");
                            areaValue = temp[0];
                        }
                        areaValue = areaValue.Replace(",00 ", "", StringComparison.OrdinalIgnoreCase);
                        if (double.TryParse(Regex.Replace(areaValue, @"[^0-9]", ""), out var a)) plotArea = a;
                    }
                    else if (Matches(label, config.EpcLabel))
                    {
                        if (string.IsNullOrEmpty(epcScore)) epcScore = value;
                    }
                    else if (Matches(label, config.ReferenceLabel) && string.IsNullOrEmpty(externalId))
                    {
                        externalId = value;
                    }
                    else if (Matches(label, config.AddressLabel))
                    {
                        var (z, c) = ParseAddress(value);
                        zipCode = z ?? zipCode;
                        city = c ?? city;
                    }
                    else if (Matches(label, config.ZipCodeLabel))
                    {
                        zipCode = value;
                    }
                    else if (Matches(label, config.CityLabel))
                    {
                        city = value;
                    }
                    else if (Matches(label, config.PriceLabel))
                    {
                        price = ExtractDecimal(value);
                    }
                }
            }
        }

        // Fallback for image from OpenGraph
        if (string.IsNullOrEmpty(imageUrl))
        {
            var ogImage = document.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            imageUrl = ogImage?.GetAttributeValue("content", null);
        }

        var settings = _context.AppSettings.FirstOrDefault() ?? new AppSettings();
        bool isSold = false;
        bool isUnderOption = false;
        var fullText = document.DocumentNode.InnerText;
        
        if (!string.IsNullOrEmpty(fullText))
        {
            var soldKeywords = settings.GetSoldKeywords();
            foreach (var keyword in soldKeywords)
            {
                if (Regex.IsMatch(fullText, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase))
                {
                    isSold = true;
                    break;
                }
            }

            var optionKeywords = settings.GetUnderOptionKeywords();
            foreach (var keyword in optionKeywords)
            {
                if (Regex.IsMatch(fullText, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase))
                {
                    isUnderOption = true;
                    break;
                }
            }
        }

        return new Property
        {
            SourceUrl = page.Url,
            SourceDomain = config.Agency.AgencyDomain,
            ExternalId = externalId,
            Title = title,
            Description = description,
            Price = price,
            ZipCode = zipCode,
            City = city,
            Bedrooms = bedrooms,
            LivingArea = livingArea,
            PlotArea = plotArea,
            ImageUrl = imageUrl,
            EpcScore = epcScore,
            Sold = isSold,
            UnderOption = isUnderOption
        };
    }

    private int? ExtractInt(HtmlDocument doc, string? selector)
    {
        if (string.IsNullOrEmpty(selector)) return null;
        var text = doc.DocumentNode.SelectSingleNode(selector)?.InnerText?.Trim();
        if (int.TryParse(text, out var i)) return i;
        return null;
    }

    private string? ExtractString(HtmlDocument doc, string? selector)
    {
        if (string.IsNullOrEmpty(selector)) return null;
        return doc.DocumentNode.SelectSingleNode(selector)?.InnerText?.Trim();
    }

    private decimal? ExtractDecimal(HtmlDocument doc, string? selector)
    {
        var text = ExtractString(doc, selector);
        if (string.IsNullOrEmpty(text)) return null;
        if (text.EndsWith(",00")) text = text.Substring(0, text.Length - 3);
        var digits = Regex.Replace(text, @"[^0-9]", "");
        if (decimal.TryParse(digits, out var d)) return d;
        return null;
    }
     private decimal? ExtractDecimal(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (text.EndsWith(",00")) text = text.Substring(0, text.Length - 3);
        var digits = Regex.Replace(text, @"[^0-9]", "");
        if (decimal.TryParse(digits, out var d)) return d;
        return null;
    }

    private string? ExtractImageUrl(HtmlDocument doc, string baseUrl, string? selector)
    {
        if (string.IsNullOrEmpty(selector)) return null;
        var node = doc.DocumentNode.SelectSingleNode(selector);
        if (node == null) return null;

        var url = node.Name == "meta" 
            ? node.GetAttributeValue("content", null) 
            : node.GetAttributeValue("src", null);

        if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
        {
            url = new Uri(new Uri(baseUrl), url).ToString();
        }
        return url;
    }

    private bool Matches(string label, string? configLabel)
    {
        if (string.IsNullOrEmpty(configLabel)) return false;
        var cleanLabel = label.Replace("m²", "", StringComparison.OrdinalIgnoreCase).Trim();
        var cleanConfigLabel = configLabel.Replace("m²", "", StringComparison.OrdinalIgnoreCase).Trim();
        return cleanLabel.Contains(cleanConfigLabel, StringComparison.OrdinalIgnoreCase);
    }

    private (string? zip, string? city) ParseAddress(string address)
    {
        if (string.IsNullOrEmpty(address)) return (null, null);
        
        var match = Regex.Match(address, @"(\d{4})\s+(.+)");
        if (match.Success)
        {
            return (match.Groups[1].Value, match.Groups[2].Value.Trim());
        }
        
        if (Regex.IsMatch(address.Trim(), @"^\d{4}$")) return (address.Trim(), null);
        
        return (null, address.Trim());
    }
}

using Immo.Data.Entities;
using HtmlAgilityPack;

namespace Immo.Parser;

public interface IParserStrategy
{
    bool CanParse(string url);
    Property Parse(RawPage page, HtmlDocument? document);
}

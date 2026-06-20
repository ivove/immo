using Immo.Data.Entities;

namespace Immo.Parser;

/// <summary>
/// Extended strategy interface for data sources (e.g. JSON APIs) where a single
/// <see cref="RawPage"/> can yield multiple <see cref="Property"/> records.
/// <para>
/// <see cref="ParserService"/> checks for this interface first; only strategies that
/// cannot implement it fall back to the single-item <see cref="IParserStrategy.Parse"/> method.
/// </para>
/// </summary>
public interface IMultiPropertyParserStrategy : IParserStrategy
{
    IEnumerable<Property> ParseMany(RawPage page);
}

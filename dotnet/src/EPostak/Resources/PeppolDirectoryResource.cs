using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Search the Peppol Business Card directory for registered participants.
/// The directory contains public registration data for all Peppol participants
/// and can be searched by name, identifier, or country.
/// </summary>
public sealed class PeppolDirectoryResource
{
    private readonly HttpRequestor _http;

    internal PeppolDirectoryResource(HttpRequestor http) => _http = http;

    /// <summary>
    /// Search the Peppol directory for registered participants by name, identifier, or country.
    /// Results are paginated. Use this to discover potential invoice recipients on the Peppol network.
    /// </summary>
    /// <param name="params">Optional search filters: query string, country code, page number, and page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated search results with Peppol ID, name, country, and registration date for each match.</returns>
    /// <example>
    /// <code>
    /// var results = await client.Peppol.Directory.SearchAsync(new DirectorySearchParams
    /// {
    ///     Q = "Slovnaft",
    ///     Country = "SK",
    ///     PageSize = 10
    /// });
    /// foreach (var entry in results.Results)
    ///     Console.WriteLine($"{entry.PeppolId}: {entry.Name} ({entry.Country})");
    /// </code>
    /// </example>
    public Task<DirectorySearchResult> SearchAsync(DirectorySearchParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("q", @params?.Q),
            ("country", @params?.Country),
            ("page", @params?.Page?.ToString()),
            ("page_size", @params?.PageSize?.ToString()));
        return _http.RequestAsync<DirectorySearchResult>(HttpMethod.Get, $"/peppol/directory/search{qs}", ct);
    }
}

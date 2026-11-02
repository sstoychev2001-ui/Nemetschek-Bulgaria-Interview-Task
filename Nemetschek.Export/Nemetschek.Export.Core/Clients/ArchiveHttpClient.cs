using System.Globalization;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Nemetschek.Export.Core.Clients.Interfaces;
using Nemetschek.Export.Core.Clients.Requests;
using Nemetschek.Export.Core.Clients.Responses;
using Nemetschek.Export.Core.Domain.Exports;

namespace Nemetschek.Export.Core.Clients;

/// <summary>
/// An <see cref="IArchiveClient"/> that reads the Archive over its HTTP API.
/// </summary>
public sealed class ArchiveHttpClient(HttpClient http) : IArchiveClient
{
    /// <inheritdoc />
    /// <exception cref="HttpRequestException">The Archive refused the request or is unreachable.</exception>
    /// <exception cref="InvalidDataException">The Archive answered something unusable.</exception>
    public async IAsyncEnumerable<ArchiveDocument> ReadDocumentsAsync(
        string cabinetName,
        DateOnly from,
        DateOnly to,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var number = 1;
        var page = await ReadDocumentPageAsync(cabinetName, from, to, number, cancellationToken);
        var read = 0;

        while (page.Documents.Count > 0)
        {
            foreach (var document in page.Documents)
            {
                read++;
                yield return document;
            }

            if (read >= page.Total)
            {
                break;
            }

            page = await ReadDocumentPageAsync(cabinetName, from, to, ++number, cancellationToken);
        }
    }

    /// <summary>Reads one page of a cabinet's documents.</summary>
    /// <exception cref="HttpRequestException">The Archive refused the request or is unreachable.</exception>
    /// <exception cref="InvalidDataException">The Archive answered something unusable.</exception>
    private async Task<ArchiveDocumentPageResponse> ReadDocumentPageAsync(
        string cabinetName,
        DateOnly from,
        DateOnly to,
        int number,
        CancellationToken cancellationToken)
    {
        var response = await http.GetAsync(
            $"cabinets/{Uri.EscapeDataString(cabinetName)}/documents" +
            $"?page={number}&size={int.MaxValue}" +
            $"&from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" +
            $"&to={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ArchiveDocumentPageResponse>(cancellationToken)
            ?? throw new InvalidDataException("The Archive returned an empty body for a page of documents.");
    }

    /// <inheritdoc />
    /// <exception cref="HttpRequestException">The Archive refused the request or is unreachable.</exception>
    public async Task<Stream> OpenPartAsync(
        string cabinetName,
        Guid documentId,
        Guid partId,
        CancellationToken cancellationToken)
    {
        var response = await http.GetAsync(
            $"cabinets/{Uri.EscapeDataString(cabinetName)}/documents/{documentId}" +
            $"/parts/{partId}/content",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="HttpRequestException">The Archive refused the write or is unreachable.</exception>
    public async Task SetTextIndexValueAsync(
        string cabinetName,
        IReadOnlyCollection<Guid> documentIds,
        string fieldName,
        string value,
        CancellationToken cancellationToken)
    {
        var response = await http.PatchAsJsonAsync(
            $"cabinets/{Uri.EscapeDataString(cabinetName)}/documents/index-values",
            new IndexValuesBatchRequest(
                documentIds,
                new Dictionary<string, IndexValueRequest>
                {
                    [fieldName] = new IndexValueRequest("Text", value),
                }),
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}

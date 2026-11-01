namespace Nemetschek.Archive.Api.Contracts.Documents.Responses;

/// <summary>
/// What a batch of index-value changes did.
/// </summary>
/// <param name="Cabinet">The file cabinet the documents belong to.</param>
/// <param name="Updated">How many documents were changed.</param>
public sealed record IndexValuesUpdatedResponse(string Cabinet, int Updated);

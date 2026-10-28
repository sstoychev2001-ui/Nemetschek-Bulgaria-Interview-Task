using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain.Cabinets;

namespace Nemetschek.Archive.Infrastructure.Cabinets;

/// <summary>
/// Loads file cabinet definitions from cabinets.json.
/// </summary>
public static class CabinetsFileLoader
{
    /// <summary>Loads and validates the cabinet definitions in a file.</summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="JsonException">The file is not valid JSON, or a value is missing or unusable.</exception>
    /// <exception cref="ValidationException">The file does not describe usable cabinets.</exception>
    public static IReadOnlyList<Cabinet> Load(string filePath) =>
        JsonSerializer.Deserialize<CabinetsFile>(File.ReadAllText(filePath))!.Cabinets;

    /// <summary>The root object of a cabinets.json file.</summary>
    private sealed class CabinetsFile : IValidatableObject, IJsonOnDeserialized
    {
        [JsonPropertyName("cabinets")]
        public IReadOnlyList<Cabinet> Cabinets { get; init; } = [];

        public void OnDeserialized() =>
            Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (Cabinets.Count == 0)
            {
                results.Add(new ValidationResult("cabinets.json declares no file cabinets"));
            }

            var duplicates = Cabinets
                .GroupBy(cabinet => cabinet.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1);

            results.AddRange(duplicates.Select(duplicate =>
                new ValidationResult($"Cabinet name {duplicate.Key} is not unique.")));

            return results;
        }
    }
}

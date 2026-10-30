using System.Text.Json;
using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Tests.Cabinets;

/// <summary>
/// Keeps <see cref="TestCabinets"/> honest against the real <c>fixtures/cabinets.json</c>.
/// </summary>
public sealed class TestCabinetsTests
{
    [Theory]
    [InlineData("Invoices")]
    [InlineData("Contracts")]
    public void The_hand_written_cabinet_matches_the_fixture(string cabinetName)
    {
        var expected = cabinetName == "Invoices"
            ? TestCabinets.Invoices()
            : TestCabinets.Contracts();

        var fromFixture = Fixture(cabinetName);

        Assert.Equal(expected.Name, fromFixture.Name);
        Assert.Equal(expected.DisplayName, fromFixture.DisplayName);
        Assert.Equal(expected.Fields, fromFixture.Fields);
    }

    [Fact]
    public void The_fixture_holds_exactly_the_two_cabinets_the_tests_know_about()
    {
        Assert.Equal(["Invoices", "Contracts"], Cabinets().Select(cabinet => cabinet.Name));
    }

    private static Cabinet Fixture(string cabinetName) =>
        Cabinets().Single(cabinet => cabinet.Name == cabinetName);

    /// <summary>The fixture's cabinets, read straight out of the JSON.</summary>
    private static IReadOnlyList<Cabinet> Cabinets()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(RepositoryFixtures.CabinetsFile));

        return
        [
            .. file.RootElement.GetProperty("cabinets").EnumerateArray().Select(cabinet =>
                new Cabinet
                {
                    Name = cabinet.GetProperty("name").GetString()!,
                    DisplayName = cabinet.GetProperty("displayName").GetString()!,
                    Fields =
                    [
                        .. cabinet.GetProperty("fields").EnumerateArray().Select(field =>
                            new IndexField
                            {
                                Name = field.GetProperty("name").GetString()!,
                                Label = field.GetProperty("label").GetString()!,
                                Type = Enum.Parse<FieldType>(
                                    field.GetProperty("type").GetString()!, ignoreCase: true),
                                MaxLength = field.TryGetProperty("maxLength", out var maxLength)
                                    ? maxLength.GetInt32()
                                    : null,
                                Writable = field.GetProperty("writable").GetBoolean(),
                            }),
                    ],
                }),
        ];
    }
}

using PhotoOrganizer.Domain;

namespace PhotoOrganizer.Domain.Tests;

/// <summary>
/// Tests for <see cref="FolderTypeExtensions.Parse"/> — the method that drives crawler folder
/// preference. A regression here (e.g. stopping to lowercase) would silently break deduplication.
/// </summary>
[TestClass]
public sealed class FolderTypeParseTests
{
    // --- Recognized values ---

    [TestMethod]
    [DataRow("originals", FolderType.Originals, DisplayName = "originals → Originals")]
    [DataRow("edits",     FolderType.Edits,     DisplayName = "edits → Edits")]
    public void Parse_KnownLowercaseValue_ReturnsExpectedType(string value, FolderType expected)
    {
        Assert.AreEqual(expected, FolderTypeExtensions.Parse(value));
    }

    [TestMethod]
    [DataRow("ORIGINALS", FolderType.Originals, DisplayName = "ORIGINALS (upper) → Originals")]
    [DataRow("Originals", FolderType.Originals, DisplayName = "Originals (mixed) → Originals")]
    [DataRow("EDITS",     FolderType.Edits,     DisplayName = "EDITS (upper) → Edits")]
    [DataRow("Edits",     FolderType.Edits,     DisplayName = "Edits (mixed) → Edits")]
    public void Parse_KnownValueCaseInsensitive_ReturnsExpectedType(string value, FolderType expected)
    {
        Assert.AreEqual(expected, FolderTypeExtensions.Parse(value));
    }

    // --- Unknown / default values → Mixed ---

    [TestMethod]
    [DataRow(null,      DisplayName = "null → Mixed")]
    [DataRow("",        DisplayName = "empty string → Mixed")]
    [DataRow("  ",      DisplayName = "whitespace → Mixed")]
    [DataRow("mixed",   DisplayName = "mixed (explicit) → Mixed")]
    [DataRow("MIXED",   DisplayName = "MIXED (upper) → Mixed")]
    [DataRow("unknown", DisplayName = "unknown string → Mixed")]
    [DataRow("raw",     DisplayName = "raw (non-folder type) → Mixed")]
    public void Parse_UnknownOrDefaultValue_ReturnsMixed(string? value)
    {
        Assert.AreEqual(FolderType.Mixed, FolderTypeExtensions.Parse(value));
    }
}

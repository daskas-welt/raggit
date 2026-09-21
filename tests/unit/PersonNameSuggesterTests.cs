using System;
using System.Collections.Generic;
using FluentAssertions;
using RAGGit.Core.Abstractions;
using RAGGit.Retrieval;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Near-miss Greek person names: surname match suggests the closest person
/// present in retrieved chunks without transferring facts.
/// </summary>
public sealed class PersonNameSuggesterTests
{
    [Fact]
    public void Normalize_StripsAccents_AndFoldsFinalSigma()
    {
        GreekNameNormalizer.Normalize("Δημοπούλου").Should().Be("δημοπουλου");
        GreekNameNormalizer.Normalize("ΟΔΥΣΣΕΑΣ").Should().Be("οδυσσεασ");
        GreekNameNormalizer.Normalize("οδυσσεας").Should().Be("οδυσσεασ");
    }

    [Fact]
    public void Suggest_NearMissFirstName_SuggestsSurnameMatch()
    {
        var docId = Guid.NewGuid().ToString();
        var chunks = new List<SearchResult>
        {
            new(
                Guid.NewGuid(),
                docId,
                "Αναγνωρίζουμε μισθολογικά στην υπάλληλο Δημοπούλου Πηνελόπη του Σωτηρίου συνολικό χρόνο δύο μηνών.",
                0,
                0.9f
            ),
        };

        var suggestions = PersonNameSuggester.Suggest(
            "συνολικό χρόνο προϋπηρεσίας εντός δημοσίου τομέα της δημοπούλου αρετής",
            chunks
        );

        suggestions.Should().HaveCount(1);
        suggestions[0].Name.Should().Contain("Δημοπούλου");
        suggestions[0].Name.Should().Contain("Πηνελόπη");
    }

    [Fact]
    public void Suggest_DifferentSurname_ReturnsEmpty()
    {
        var chunks = new List<SearchResult>
        {
            new(
                Guid.NewGuid(),
                Guid.NewGuid().ToString(),
                "Αναγνωρίζουμε μισθολογικά στην υπάλληλο Δημοπούλου Πηνελόπη του Σωτηρίου.",
                0,
                0.9f
            ),
        };

        var suggestions = PersonNameSuggester.Suggest("Παπαδόπουλος Γιώργος προϋπηρεσία", chunks);

        suggestions.Should().BeEmpty();
    }

    [Fact]
    public void Suggest_NoChunks_ReturnsEmpty()
    {
        var suggestions = PersonNameSuggester.Suggest(
            "δημοπούλου αρετής",
            Array.Empty<SearchResult>()
        );

        suggestions.Should().BeEmpty();
    }
}

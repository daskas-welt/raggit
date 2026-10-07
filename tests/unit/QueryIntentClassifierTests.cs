using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Retrieval;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T010: Rule-based <see cref="QueryIntentClassifier"/> — broad triggers
/// classify <see cref="QueryIntent.Broad"/>, plain fact questions stay
/// <see cref="QueryIntent.Granular"/>, whitespace/empty is the safe
/// granular default (FR-008), and classification is deterministic.
/// </summary>
public sealed class QueryIntentClassifierTests
{
    [Theory]
    [InlineData("Summarize the onboarding process")]
    [InlineData("Give me a summary of the leave policy")]
    [InlineData("Provide an overview of benefits")]
    [InlineData("Compare the Starter and Enterprise plans")]
    [InlineData("What is the comparison between the two tiers?")]
    [InlineData("Contrast the old and new policy")]
    [InlineData("What are the differences between the plans?")]
    [InlineData("What is the difference between X and Y?")]
    [InlineData("List all the steps to onboard")]
    [InlineData("Show me all the steps end-to-end")]
    [InlineData("Explain how onboarding works")]
    [InlineData("Walk me through the setup")]
    [InlineData("What are the pros and cons?")]
    [InlineData("Give me the high-level picture")]
    [InlineData("Show me the onboarding guide")]
    public void Classify_BroadTrigger_ReturnsBroad(string query)
    {
        QueryIntentClassifier.Classify(query).Should().Be(QueryIntent.Broad);
    }

    [Theory]
    [InlineData("What is the renewal term?")]
    [InlineData("When was the security policy last updated?")]
    [InlineData("How many vacation days do I get?")]
    [InlineData("How much is the deductible?")]
    [InlineData("What is the warranty period for the Pro plan?")]
    [InlineData("refund policy")]
    [InlineData("onboarding")]
    public void Classify_FactQuestion_ReturnsGranular(string query)
    {
        QueryIntentClassifier.Classify(query).Should().Be(QueryIntent.Granular);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Classify_EmptyOrWhitespace_ReturnsGranularSafeDefault(string? query)
    {
        QueryIntentClassifier.Classify(query).Should().Be(QueryIntent.Granular);
    }

    [Fact]
    public void Classify_IsDeterministic()
    {
        const string query = "Summarize the onboarding process and compare the plans";

        var first = QueryIntentClassifier.Classify(query);
        var second = QueryIntentClassifier.Classify(query);

        second.Should().Be(first);
        first.Should().Be(QueryIntent.Broad);
    }

    [Fact]
    public void Classify_IsCaseInsensitive()
    {
        QueryIntentClassifier.Classify("SUMMARIZE EVERYTHING").Should().Be(QueryIntent.Broad);
    }
}

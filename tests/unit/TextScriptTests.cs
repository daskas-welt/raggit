using FluentAssertions;
using RAGGit.Core;
using RAGGit.Retrieval;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 033: document labels follow the question's language. The shared script
/// check lives in Core so the renderer does not depend on Retrieval.
/// </summary>
public sealed class TextScriptTests
{
    [Theory]
    [InlineData("Ποιος είναι ο χρόνος;", true)]
    [InlineData("What is the term?", false)]
    [InlineData("Maria Παπαδοπούλου works here", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsGreek_DetectsScript(string? value, bool expected)
    {
        TextScript.IsGreek(value).Should().Be(expected);
    }

    [Fact]
    public void AnswerLanguage_DelegatesToTheSharedCheck()
    {
        AnswerLanguage.IsGreek("Ποιος είναι ο χρόνος;").Should().BeTrue();
        AnswerLanguage.IsGreek("What is the term?").Should().BeFalse();
    }
}

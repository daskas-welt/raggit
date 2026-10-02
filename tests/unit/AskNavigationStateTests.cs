using FluentAssertions;
using RAGGit.Client.Core.Services;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 028-pages-ux-polish T025: the pending-ask state carries one prompt from
/// History/QueryDetail to the Ask page — a single read returns it and clears
/// it, and every later read returns null until written again.
/// </summary>
public sealed class AskNavigationStateTests
{
    [Fact]
    public void NewState_HasNoPendingPrompt()
    {
        new AskNavigationState().PendingPrompt.Should().BeNull();
    }

    [Fact]
    public void Write_ThenSingleRead_ReturnsPromptAndClears()
    {
        var state = new AskNavigationState { PendingPrompt = "original question" };

        state.PendingPrompt.Should().Be("original question");
        state.PendingPrompt.Should().BeNull();
        state.PendingPrompt.Should().BeNull();
    }

    [Fact]
    public void Write_AfterRead_ReturnsLatestPromptOnce()
    {
        var state = new AskNavigationState { PendingPrompt = "first" };
        _ = state.PendingPrompt;

        state.PendingPrompt = "second";

        state.PendingPrompt.Should().Be("second");
        state.PendingPrompt.Should().BeNull();
    }
}

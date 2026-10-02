using FluentAssertions;
using RAGGit.Client.Core.Services;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 028-pages-ux-polish T002: the notification seam's in-memory double records
/// every shown notification (title, message, kind).
/// </summary>
public sealed class NotificationServiceTests
{
    [Theory]
    [InlineData(NotificationKind.Success)]
    [InlineData(NotificationKind.Information)]
    [InlineData(NotificationKind.Danger)]
    public void Show_RecordsTitleMessageAndKind(NotificationKind kind)
    {
        var notifications = new InMemoryNotificationService();

        notifications.Show("Copied", "Answer copied to clipboard.", kind);

        notifications
            .Shown.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new InMemoryNotificationService.ShownNotification(
                    "Copied",
                    "Answer copied to clipboard.",
                    kind
                )
            );
    }

    [Fact]
    public void Show_MultipleNotifications_AccumulatesInOrder()
    {
        var notifications = new InMemoryNotificationService();

        notifications.Show("Copied", "First.", NotificationKind.Success);
        notifications.Show("Download failed", "Second.", NotificationKind.Danger);

        notifications.Shown.Should().HaveCount(2);
        notifications.Shown[0].Title.Should().Be("Copied");
        notifications.Shown[0].Message.Should().Be("First.");
        notifications.Shown[0].Kind.Should().Be(NotificationKind.Success);
        notifications.Shown[1].Title.Should().Be("Download failed");
        notifications.Shown[1].Kind.Should().Be(NotificationKind.Danger);
    }

    [Fact]
    public void NewDouble_ShowsNothing()
    {
        new InMemoryNotificationService().Shown.Should().BeEmpty();
    }
}

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using RAGGit.Core.Models;
using RAGGit.Workstation.Api.Controllers;
using RAGGit.Workstation.Api.Filters;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// A body far over the file cap used to reach the framework's own size limit
/// first, which answers 400 instead of the contract's 413. The guard rejects it
/// from the declared Content-Length, before the multipart form is read.
/// </summary>
public sealed class RejectOversizedUploadTests
{
    [Fact]
    public void OnResourceExecuting_DeclaredBodyFarOverTheCap_RejectsWith413()
    {
        var context = FilterContext(DocumentValidation.MaxFileSizeBytes + 5 * 1024 * 1024);

        new RejectOversizedUploadAttribute().OnResourceExecuting(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
    }

    [Fact]
    public void OnResourceExecuting_FileAtTheCapPlusEnvelopeHeadroom_IsLeftToTheAction()
    {
        var context = FilterContext(DocumentValidation.MaxFileSizeBytes + 1024);

        new RejectOversizedUploadAttribute().OnResourceExecuting(context);

        context.Result.Should().BeNull("a file within the cap must reach the action's own check");
    }

    [Fact]
    public void UploadAction_IsGuardedByTheSizeFilter()
    {
        var upload = typeof(DocumentsController).GetMethod(nameof(DocumentsController.Upload));
        upload.Should().NotBeNull();

        upload!
            .GetCustomAttributes(typeof(RejectOversizedUploadAttribute), inherit: false)
            .Should()
            .NotBeEmpty("the 413 contract depends on this guard running before model binding");
    }

    private static ResourceExecutingContext FilterContext(long? contentLength)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentLength = contentLength;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        return new ResourceExecutingContext(
            actionContext,
            Array.Empty<IFilterMetadata>(),
            Array.Empty<IValueProviderFactory>()
        );
    }
}

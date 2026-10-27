using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RAGGit.Core.Models;

namespace RAGGit.Workstation.Api.Filters;

/// <summary>
/// Rejects an upload whose declared body length already exceeds the file cap,
/// before the multipart form is read. Without it the framework's own size limit
/// trips first and answers 400 ("Failed to read the request form"), so how far
/// over the cap a file is decided the status code instead of always being 413.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RejectOversizedUploadAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var declaredLength = context.HttpContext.Request.ContentLength;
        if (
            declaredLength
            > DocumentValidation.MaxFileSizeBytes + DocumentValidation.MultipartEnvelopeBytes
        )
        {
            context.Result = new ObjectResult(new { error = "File exceeds 100MB." })
            {
                StatusCode = StatusCodes.Status413PayloadTooLarge,
            };
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }
}

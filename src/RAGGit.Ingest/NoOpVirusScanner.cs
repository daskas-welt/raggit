using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RAGGit.Core.Abstractions;

namespace RAGGit.Ingest;

/// <summary>
/// Default virus scanner stub: always reports clean and logs the scan event.
/// Replace with a real scanner (e.g. Windows Defender, ClamAV, enterprise SOAR)
/// by registering a custom <see cref="IVirusScanner"/> in DI.
/// </summary>
public sealed class NoOpVirusScanner : IVirusScanner
{
    private readonly ILogger<NoOpVirusScanner> _logger;

    public NoOpVirusScanner(ILogger<NoOpVirusScanner> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<bool> ScanAsync(
        Stream stream,
        string filename,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(stream);
        _logger.LogInformation(
            "Virus scan stub passed for {Filename} ({Length} bytes)",
            filename,
            stream.Length
        );
        return Task.FromResult(true);
    }
}

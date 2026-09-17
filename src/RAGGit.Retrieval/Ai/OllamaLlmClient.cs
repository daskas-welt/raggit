using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using RAGGit.Core.Abstractions;

namespace RAGGit.Retrieval.Ai;

/// <summary>
/// Local LLM client using Ollama <c>POST /api/chat</c>.
/// </summary>
public sealed class OllamaLlmClient : ILlmClient
{
    private readonly OllamaApiClient _client;
    private readonly string _modelName;
    private readonly int _timeoutMs;
    private readonly ILogger<OllamaLlmClient>? _logger;

    // Health-probe budget: ListLocalModelsAsync is cheap once Ollama is warm,
    // but the first call on a cold/slow box can take several seconds.
    private const int HealthProbeTimeoutSeconds = 10;

    public OllamaLlmClient(
        string baseUrl,
        string modelName,
        int timeoutMs = 5000,
        ILogger<OllamaLlmClient>? logger = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        if (timeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));

        _modelName = modelName;
        _timeoutMs = timeoutMs;
        _logger = logger;
        var httpClient = new System.Net.Http.HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMilliseconds(timeoutMs),
        };
        _client = new OllamaApiClient(httpClient, modelName);
    }

    /// <inheritdoc />
    public async Task<string> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);

        var request = new ChatRequest
        {
            Model = _modelName,
            Stream = true,
            Messages =
            [
                new Message(ChatRole.System, systemPrompt),
                new Message(ChatRole.User, userPrompt),
            ],
        };

        var responseBuilder = new StringBuilder();
        await foreach (var chunk in _client.ChatAsync(request, cancellationToken))
        {
            if (chunk?.Message?.Content is not null)
            {
                responseBuilder.Append(chunk.Message.Content);
            }
        }

        return responseBuilder.ToString().Trim();
    }

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(HealthProbeTimeoutSeconds));
            _ = await _client.ListLocalModelsAsync(cts.Token);
            return true;
        }
        catch (Exception ex)
            when (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            _logger?.LogDebug("Ollama health probe aborted by caller (model {Model})", _modelName);
            return false;
        }
        catch (Exception ex) when (ex is OperationCanceledException)
        {
            _logger?.LogWarning(
                "Ollama health probe timed out after {TimeoutSeconds}s (model {Model})",
                HealthProbeTimeoutSeconds,
                _modelName
            );
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Ollama health probe failed (model {Model})", _modelName);
            return false;
        }
    }
}

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

    public OllamaLlmClient(string baseUrl, string modelName, int timeoutMs = 5000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));

        _modelName = modelName;
        _timeoutMs = timeoutMs;
        var httpClient = new System.Net.Http.HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMilliseconds(timeoutMs)
        };
        _client = new OllamaApiClient(httpClient, modelName);
    }

    /// <inheritdoc />
    public async Task<string> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
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
                new Message(ChatRole.User, userPrompt)
            ]
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
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            _ = await _client.ListLocalModelsAsync(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

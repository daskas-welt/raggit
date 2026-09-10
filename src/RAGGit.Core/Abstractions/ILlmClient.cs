using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Core.Abstractions;

/// <summary>
/// Abstraction over a local LLM client (Ollama /api/chat, LLamaSharp, ONNX, etc.).
/// </summary>
public interface ILlmClient
{
    Task<string> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the LLM is reachable and operational.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

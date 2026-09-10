using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Core.Abstractions;

/// <summary>
/// Abstraction over a local embedding model (Ollama, LLamaSharp, ONNX, etc.).
/// </summary>
public interface IEmbedder
{
    Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
        IEnumerable<string> inputs,
        CancellationToken cancellationToken = default);
}

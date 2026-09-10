using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using RAGGit.Core.Abstractions;

namespace RAGGit.Ingest.Ai;

/// <summary>
/// Local embedding client using an ONNX model (e.g. bge-micro-v2) as a fallback
/// when Ollama is unavailable. Expects <c>model.onnx</c> and an optional
/// <c>vocab.txt</c> alongside it for WordPiece tokenization.
/// </summary>
public sealed class OnnxEmbedder : IEmbedder, IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _modelPath;
    private readonly IReadOnlyDictionary<string, int> _vocab;
    private readonly int _maxSequenceLength;

    public OnnxEmbedder(string modelPath, int maxSequenceLength = 128)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        _modelPath = modelPath;
        _maxSequenceLength = maxSequenceLength;

        if (!File.Exists(_modelPath))
        {
            throw new FileNotFoundException("ONNX embedding model not found.", _modelPath);
        }

        _session = new InferenceSession(_modelPath);

        var vocabPath = Path.Combine(Path.GetDirectoryName(_modelPath) ?? ".", "vocab.txt");
        _vocab = File.Exists(vocabPath)
            ? LoadVocab(vocabPath)
            : new Dictionary<string, int>();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
        IEnumerable<string> inputs,
        CancellationToken cancellationToken = default)
    {
        var inputList = inputs?.ToList() ?? throw new ArgumentNullException(nameof(inputs));
        if (inputList.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<float[]>>(Array.Empty<float[]>());
        }

        var embeddings = inputList.Select(GetEmbedding).ToList();
        return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
    }

    private float[] GetEmbedding(string text)
    {
        var tokenIds = Tokenize(text);
        var sequenceLength = Math.Min(tokenIds.Count, _maxSequenceLength);

        var inputIds = new long[_maxSequenceLength];
        var attentionMask = new long[_maxSequenceLength];
        var tokenTypeIds = new long[_maxSequenceLength];

        for (var i = 0; i < sequenceLength; i++)
        {
            inputIds[i] = tokenIds[i];
            attentionMask[i] = 1;
        }

        for (var i = sequenceLength; i < _maxSequenceLength; i++)
        {
            inputIds[i] = 0;
            attentionMask[i] = 0;
            tokenTypeIds[i] = 0;
        }

        var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, _maxSequenceLength });
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, _maxSequenceLength });
        var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, new[] { 1, _maxSequenceLength });

        var inputNames = _session.InputMetadata.Keys.ToList();
        var inputs = new List<NamedOnnxValue>();

        foreach (var name in inputNames)
        {
            if (name.Contains("input", StringComparison.OrdinalIgnoreCase) && name.Contains("id", StringComparison.OrdinalIgnoreCase))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(name, inputIdsTensor));
            }
            else if (name.Contains("attention", StringComparison.OrdinalIgnoreCase) || name.Contains("mask", StringComparison.OrdinalIgnoreCase))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(name, attentionMaskTensor));
            }
            else if (name.Contains("token", StringComparison.OrdinalIgnoreCase) && name.Contains("type", StringComparison.OrdinalIgnoreCase))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(name, tokenTypeIdsTensor));
            }
        }

        using var results = _session.Run(inputs);

        var output = results.FirstOrDefault()?.AsTensor<float>();
        if (output is null)
        {
            throw new InvalidOperationException("ONNX model produced no output tensor.");
        }

        // output shape: [batch=1, seq_len, hidden_size]
        var hiddenSize = output.Dimensions[2];
        var embedding = new float[hiddenSize];
        var validTokens = 0L;

        for (var i = 0; i < _maxSequenceLength; i++)
        {
            if (attentionMask[i] == 0)
            {
                continue;
            }

            for (var j = 0; j < hiddenSize; j++)
            {
                embedding[j] += output[0, i, j];
            }

            validTokens++;
        }

        if (validTokens > 0)
        {
            for (var j = 0; j < hiddenSize; j++)
            {
                embedding[j] /= validTokens;
            }
        }

        return embedding;
    }

    private List<long> Tokenize(string text)
    {
        var tokens = new List<long> { 101 }; // [CLS]
        var words = text.ToLowerInvariant().Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (_vocab.TryGetValue(word, out var id))
            {
                tokens.Add(id);
                continue;
            }

            // Basic WordPiece fallback: try to decompose unknown words.
            var remaining = word;
            while (!string.IsNullOrEmpty(remaining))
            {
                var longest = remaining;
                while (!string.IsNullOrEmpty(longest) && !_vocab.ContainsKey(longest))
                {
                    longest = longest[..^1];
                }

                if (string.IsNullOrEmpty(longest))
                {
                    tokens.Add(100); // [UNK]
                    break;
                }

                tokens.Add(_vocab[longest]);
                remaining = remaining[longest.Length..];
                if (!string.IsNullOrEmpty(remaining))
                {
                    remaining = "##" + remaining;
                }
            }
        }

        tokens.Add(102); // [SEP]
        return tokens;
    }

    private static Dictionary<string, int> LoadVocab(string path)
    {
        var vocab = new Dictionary<string, int>();
        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            var token = lines[i].Trim();
            if (!string.IsNullOrEmpty(token))
            {
                vocab[token] = i;
            }
        }

        return vocab;
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}

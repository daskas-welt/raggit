using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using RAGGit.Retrieval.Ai;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Reasoning models (qwen3.5) emit thinking separately from content. A grounded
/// RAG answer does not need chain-of-thought, so the client must send
/// <c>think: false</c>; otherwise the model can spend the whole timeout
/// thinking and return an empty answer.
/// </summary>
public sealed class OllamaThinkingTests
{
    [Fact]
    public async Task ChatAsync_DisablesThinking_SoReasoningModelsReturnContent()
    {
        var port = FreePort();
        var baseUrl = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(baseUrl);
        listener.Start();

        var capturedBody = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var serve = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                capturedBody.SetResult(await reader.ReadToEndAsync());
            }

            var first =
                "{\"model\":\"qwen3.5:2b-q4_K_M\",\"created_at\":\"2026-01-01T00:00:00Z\","
                + "\"message\":{\"role\":\"assistant\",\"content\":\"Grounded answer.\"},"
                + "\"done\":false}\n";
            var last =
                "{\"model\":\"qwen3.5:2b-q4_K_M\",\"created_at\":\"2026-01-01T00:00:00Z\","
                + "\"message\":{\"role\":\"assistant\",\"content\":\"\"},"
                + "\"done\":true,\"done_reason\":\"stop\"}\n";
            var bytes = Encoding.UTF8.GetBytes(first + last);
            context.Response.ContentType = "application/x-ndjson";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
        });

        var llm = new OllamaLlmClient(baseUrl, "qwen3.5:2b-q4_K_M", timeoutMs: 10_000);
        var answer = await llm.ChatAsync("system prompt", "user prompt");
        var body = await capturedBody.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await serve;

        Assert.Equal("Grounded answer.", answer);
        Assert.Contains("\"think\":false", body);
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}

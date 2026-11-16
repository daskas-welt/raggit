using RAGGit.Core.Models;

namespace RAGGit.Client.Core.Models;

/// <summary>A per-message Save as request (033 US3): the message and chosen format.</summary>
public sealed record DocumentSaveRequest(ChatMessage Message, DocumentOutputFormat Format);

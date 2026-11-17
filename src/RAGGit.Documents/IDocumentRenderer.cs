using RAGGit.Core.Models;

namespace RAGGit.Documents;

/// <summary>Renders a document in one format from a transient draft.</summary>
public interface IDocumentRenderer
{
    DocumentOutputFormat Format { get; }

    /// <summary>Adapts a persisted grounded answer for the shared renderers.</summary>
    GeneratedDocument Render(QueryDetail detail);

    /// <summary>Renders new composed content, which may have no library sources.</summary>
    GeneratedDocument Render(DocumentDraft draft);
}

using RAGGit.Core.Models;

namespace RAGGit.Documents;

/// <summary>Renders one persisted grounded answer into one document format.</summary>
public interface IDocumentRenderer
{
    DocumentOutputFormat Format { get; }

    GeneratedDocument Render(QueryDetail detail);
}

using System;
using RAGGit.Core.Models;

namespace RAGGit.Ingest;

public sealed class SpreadsheetCellCapExceededException : Exception
{
    public int Cap { get; }
    public long ActualCount { get; }

    public SpreadsheetCellCapExceededException(int cap, long actualCount)
        : base($"Spreadsheet exceeds {cap:N0} cell limit (found {actualCount:N0} cells)")
    {
        Cap = cap;
        ActualCount = actualCount;
    }

    public SpreadsheetCellCapExceededException(int cap, long actualCount, Exception inner)
        : base($"Spreadsheet exceeds {cap:N0} cell limit (found {actualCount:N0} cells)", inner)
    {
        Cap = cap;
        ActualCount = actualCount;
    }
}

using HDecompressHelper.Models;

namespace HDecompressHelper.Infrastructure;

public interface IProgressStreamParser
{
    IReadOnlyList<ExtractionProgress> Append(string chunk);

    IReadOnlyList<ExtractionProgress> Complete();
}


using System.Collections.Generic;
using Core.Models;

namespace API.Dtos.Collections;

public record CollectionImportResult(
    List<Card> Cards,
    List<string> Errors,
    int SkippedLines);

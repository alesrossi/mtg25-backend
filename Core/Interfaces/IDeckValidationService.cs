using Core.Enums;
using Core.Models;

namespace Core.Interfaces;

public interface IDeckValidationService
{
    /// <summary>
    /// Validates a deck against format-specific rules
    /// </summary>
    Task<DeckValidationResult> ValidateDeckAsync(Deck deck);
    
    /// <summary>
    /// Validates a deck with its cards against format-specific rules
    /// </summary>
    Task<DeckValidationResult> ValidateDeckWithCardsAsync(Deck deck, IEnumerable<DeckCard> deckCards);
    
    /// <summary>
    /// Gets the minimum card count for a specific format
    /// </summary>
    int GetMinimumCardCount(DeckFormat format);
    
    /// <summary>
    /// Gets the maximum card count for a specific format (if applicable)
    /// </summary>
    int? GetMaximumCardCount(DeckFormat format);
    
    /// <summary>
    /// Checks if a format allows sideboards
    /// </summary>
    bool FormatAllowsSideboard(DeckFormat format);
    
    /// <summary>
    /// Gets the maximum sideboard size for a format
    /// </summary>
    int GetMaximumSideboardSize(DeckFormat format);
}

public class DeckValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = [];
    private List<ValidationWarning> Warnings { get; set; } = [];
    
    public void AddError(string message, ValidationErrorType type = ValidationErrorType.General)
    {
        Errors.Add(new ValidationError { Message = message, Type = type });
        IsValid = false;
    }
    
    public void AddWarning(string message, ValidationWarningType type = ValidationWarningType.General)
    {
        Warnings.Add(new ValidationWarning { Message = message, Type = type });
    }
}

public class ValidationError
{
    public required string Message { get; set; }
    public ValidationErrorType Type { get; set; } = ValidationErrorType.General;
}

public class ValidationWarning
{
    public required string Message { get; set; }
    public ValidationWarningType Type { get; set; } = ValidationWarningType.General;
}

public enum ValidationErrorType
{
    General,
    CardCount,
    Format,
    Sideboard,
    CardLegality,
    Singleton
}

public enum ValidationWarningType
{
    General,
    Performance,
    Strategy,
    Cost
}

using System.ComponentModel.DataAnnotations;

namespace API.Services;

public class ValidationService : IValidationService
{
    public (bool IsValid, Dictionary<string, string[]> Errors) ValidateModel<T>(T model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model!);
            
        var isValid = Validator.TryValidateObject(model!, validationContext, validationResults, true);
            
        var errors = validationResults
            .GroupBy(vr => vr.MemberNames.FirstOrDefault() ?? "")
            .ToDictionary(
                g => g.Key,
                g => g.Select(vr => vr.ErrorMessage ?? "").ToArray()
            );

        return (isValid, errors);
    }
}
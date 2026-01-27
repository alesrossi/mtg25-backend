using System.ComponentModel.DataAnnotations;

namespace API.Services;

public class ValidationService : IValidationService
{
    public (bool IsValid, Dictionary<string, string[]> Errors) ValidateModel<T>(T model)
    {
        if (model is IEnumerable<object?> enumerable && model is not string)
        {
            var aggregateErrors = new Dictionary<string, string[]>();
            var isAggregateValid = true;
            var index = 0;

            foreach (var item in enumerable)
            {
                if (item is null)
                {
                    isAggregateValid = false;
                    aggregateErrors.Add($"[{index}]", ["Item cannot be null."]);
                    index++;
                    continue;
                }

                var (isValid, errors) = ValidateSingle(item);
                if (!isValid)
                {
                    isAggregateValid = false;
                    foreach (var kvp in errors)
                    {
                        var key = string.IsNullOrEmpty(kvp.Key)
                            ? $"[{index}]"
                            : $"[{index}].{kvp.Key}";
                        aggregateErrors[key] = kvp.Value;
                    }
                }

                index++;
            }

            return (isAggregateValid, aggregateErrors);
        }

        return ValidateSingle(model!);
    }

    private static (bool IsValid, Dictionary<string, string[]> Errors) ValidateSingle(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);

        var isValid = Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);

        var errors = validationResults
            .GroupBy(vr => vr.MemberNames.FirstOrDefault() ?? string.Empty)
            .ToDictionary(
                g => g.Key,
                g => g.Select(vr => vr.ErrorMessage ?? string.Empty).ToArray());

        return (isValid, errors);
    }
}

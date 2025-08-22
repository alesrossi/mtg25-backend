namespace API.Services;

public interface IValidationService
{
    (bool IsValid, Dictionary<string, string[]> Errors) ValidateModel<T>(T model);
}
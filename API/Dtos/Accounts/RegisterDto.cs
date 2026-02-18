using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Accounts;

public class RegisterDto
{
    [Required(ErrorMessage = "Display name is required")]
    [StringLength(32, MinimumLength = 1, ErrorMessage = "Display name must be between 1 and 32 characters")]
    public required string DisplayName { get; set; }
    [Required(ErrorMessage = "First name is required")]
    [StringLength(32, MinimumLength = 1, ErrorMessage = "First name must be between 1 and 32 characters")]
    public required string FirstName { get; set; }
    [Required(ErrorMessage = "Last name is required")]
    [StringLength(32, MinimumLength = 1, ErrorMessage = "Last name must be between 1 and 32 characters")]
    public required string LastName { get; set; }
    [EmailAddress]
    public required string Email { get; set; }
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
        ErrorMessage = "Password must have 1 Uppercase, 1 Lowercase, 1 number, 1 non alphanumeric and at least 8 characters")]
    public required string Password { get; set; }
    [StringLength(100, ErrorMessage = "Companion name must be at most 100 characters")]
    public string? CompanionName { get; set; }
}
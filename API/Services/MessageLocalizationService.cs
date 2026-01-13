using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IMessageLocalizer
{
    Task<string> GetMessageAsync(string userId, string key, CancellationToken cancellationToken = default, params object[] args);
    string GetMessageForLanguage(string? language, string key, params object[] args);
}

public sealed class MessageLocalizationService : IMessageLocalizer
{
    private static readonly CultureInfo DefaultCulture = CultureInfo.GetCultureInfo("en");
    private readonly IUserSettingsService _userSettingsService;
    private readonly ILogger<MessageLocalizationService> _logger;
    private readonly ResourceManager _resourceManager;

    public MessageLocalizationService(
        IUserSettingsService userSettingsService,
        ILogger<MessageLocalizationService> logger)
    {
        _userSettingsService = userSettingsService;
        _logger = logger;
        _resourceManager = new ResourceManager("API.Resources.Messages", typeof(MessageLocalizationService).Assembly);
    }

    public async Task<string> GetMessageAsync(
        string userId,
        string key,
        CancellationToken cancellationToken = default,
        params object[] args)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return GetMessageForLanguage(null, key, args);
        }

        var settings = await _userSettingsService.GetSettingsAsync(userId, cancellationToken);
        return GetMessageForLanguage(settings?.LanguageUi, key, args);
    }

    public string GetMessageForLanguage(string? language, string key, params object[] args)
    {
        var culture = ResolveCulture(language);
        var localized = GetLocalizedString(culture, key, args);
        if (localized is not null)
        {
            return localized;
        }

        _logger.LogWarning("Localization key not found. Key={Key}, Culture={Culture}", key, culture.Name);
        localized = GetLocalizedString(DefaultCulture, key, args);
        if (localized is null)
        {
            _logger.LogWarning("Localization key not found in fallback culture. Key={Key}, Culture={Culture}", key, DefaultCulture.Name);
            return key;
        }

        return localized;
    }

    private static CultureInfo ResolveCulture(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return DefaultCulture;
        }

        var normalized = language.Trim().ToLowerInvariant();

        try
        {
            return CultureInfo.GetCultureInfo(normalized);
        }
        catch (CultureNotFoundException)
        {
            return DefaultCulture;
        }
    }

    private string? GetLocalizedString(CultureInfo culture, string key, object[] args)
    {
        var value = _resourceManager.GetString(key, culture);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return args.Length == 0
            ? value
            : string.Format(culture, value, args);
    }
}

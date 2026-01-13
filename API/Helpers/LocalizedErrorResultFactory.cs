using API.Services;

namespace API.Helpers;

public static class LocalizedErrorResultFactory
{
    public static async Task<IResult> BadRequestAsync(
        HttpContext context,
        IMessageLocalizer messageLocalizer,
        string? userId,
        string messageKey,
        params object[] args)
    {
        var message = await messageLocalizer.GetMessageAsync(userId ?? string.Empty, messageKey, context.RequestAborted, args);
        return Results.BadRequest(new { Error = message });
    }

    public static async Task<IResult> BadRequestMessageAsync(
        HttpContext context,
        IMessageLocalizer messageLocalizer,
        string? userId,
        string messageKey,
        params object[] args)
    {
        var message = await messageLocalizer.GetMessageAsync(userId ?? string.Empty, messageKey, context.RequestAborted, args);
        return Results.BadRequest(message);
    }

    public static async Task<IResult> NotFoundAsync(
        HttpContext context,
        IMessageLocalizer messageLocalizer,
        string? userId,
        string messageKey,
        params object[] args)
    {
        var message = await messageLocalizer.GetMessageAsync(userId ?? string.Empty, messageKey, context.RequestAborted, args);
        return Results.NotFound(new { Error = message });
    }

    public static async Task<IResult> NotFoundMessageAsync(
        HttpContext context,
        IMessageLocalizer messageLocalizer,
        string? userId,
        string messageKey,
        params object[] args)
    {
        var message = await messageLocalizer.GetMessageAsync(userId ?? string.Empty, messageKey, context.RequestAborted, args);
        return Results.NotFound(message);
    }

    public static async Task<IResult> ProblemAsync(
        HttpContext context,
        IMessageLocalizer messageLocalizer,
        string? userId,
        int statusCode,
        string title,
        string detailKey,
        string? errorCode,
        params object[] args)
    {
        var detail = await messageLocalizer.GetMessageAsync(userId ?? string.Empty, detailKey, context.RequestAborted, args);
        return ProblemResultFactory.Create(context, statusCode, title, detail, errorCode);
    }
}

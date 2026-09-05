using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LifeLink.Api.Infrastructure;

/// <summary>Builds one consistent, human-readable 400 response for model-binding and data-annotation failures.</summary>
public static class ValidationProblemFactory
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = new Dictionary<string, string[]>();
        // When the JSON body cannot be read (e.g. an unknown enum value), MVC also reports the whole action
        // parameter as "required". That second error names no real field, so only keep the precise JSON-path one.
        var bodyUnreadable = context.ModelState.Keys.Any(x => x.StartsWith('$'));
        var parameterNames = context.ActionDescriptor.Parameters.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, entry) in context.ModelState.Where(x => x.Value?.Errors.Count > 0))
        {
            if (bodyUnreadable && parameterNames.Contains(key)) continue;
            var field = FieldName(key);
            errors[field] = entry!.Errors.Select(Friendly).Distinct().ToArray();
        }
        var problem = new ValidationProblemDetails(errors)
        {
            Status = 400,
            Title = "validation_failed",
            Detail = errors.Count == 0 ? "The request is invalid." : $"Please fix {errors.Count} field(s): " + string.Join("; ", errors.Select(x => $"{x.Key} - {x.Value[0]}")),
        };
        problem.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
        return new ObjectResult(problem) { StatusCode = 400, ContentTypes = { "application/problem+json" } };
    }

    private static string FieldName(string key)
    {
        var name = key.TrimStart('$', '.');
        if (name.Length == 0) return "body";
        name = name[(name.LastIndexOf('.') + 1)..];
        return name.Length == 0 ? "body" : char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string Friendly(ModelError error)
    {
        var message = error.ErrorMessage;
        if (string.IsNullOrEmpty(message) || message.Contains("could not be converted", StringComparison.OrdinalIgnoreCase) || message.StartsWith("The value '", StringComparison.Ordinal))
            return "The value has the wrong format or is not one of the allowed options.";
        if (message.Contains("field is required", StringComparison.OrdinalIgnoreCase)) return "This field is required.";
        if (message.StartsWith("A non-empty request body is required", StringComparison.OrdinalIgnoreCase)) return "A request body is required.";
        return message;
    }
}

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace CompGateApi.Core.Startup
{
  public class Always200ResponseMiddleware
  {
    private readonly RequestDelegate _next;
    private static readonly string[] _skipPrefixes = new[] { "/swagger", "/notificationHub" };

    public Always200ResponseMiddleware(RequestDelegate next)
    {
      _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
      var path = context.Request.Path.Value ?? string.Empty;
      if (_skipPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
      {
        await _next(context);
        return;
      }

      var originalBody = context.Response.Body;
      await using var memStream = new MemoryStream();
      context.Response.Body = memStream;

      try
      {
        await _next(context);

        // After downstream writes
        memStream.Position = 0;
        var status = context.Response.StatusCode;

        if (status >= 400)
        {
          var originalText = await new StreamReader(memStream, Encoding.UTF8).ReadToEndAsync();
          object? details = TryParseJson(originalText);

          var computedMessage = ExtractMessage(originalText, details) 
                                 ?? ReasonPhrases.GetReasonPhrase(status) 
                                 ?? "Error";

          var payload = new
          {
            success = false,
            status,
            message = computedMessage,
            details = details ?? (!string.IsNullOrWhiteSpace(originalText) ? originalText : null)
          };

          context.Response.Body = originalBody;
          context.Response.StatusCode = StatusCodes.Status200OK;
          context.Response.ContentType = "application/json";
          await JsonSerializer.SerializeAsync(context.Response.Body, payload, new JsonSerializerOptions
          {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
          });
        }
        else
        {
          // pass-through on success
          context.Response.Body = originalBody;
          if (memStream.Length > 0)
          {
            memStream.Position = 0;
            await memStream.CopyToAsync(context.Response.Body);
          }
        }
      }
      finally
      {
        // ensure body is restored in case of exception
        context.Response.Body = originalBody;
      }
    }

    private static object? TryParseJson(string text)
    {
      if (string.IsNullOrWhiteSpace(text)) return null;
      try
      {
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
      }
      catch
      {
        return null;
      }
    }

    private static string TrimForMessage(string input)
    {
      var msg = input.Trim();
      var firstLine = msg.Replace("\r", string.Empty)
                         .Split('\n')
                         .FirstOrDefault()?.Trim();
      return string.IsNullOrWhiteSpace(firstLine) ? msg : firstLine!;
    }

    private static string? ExtractMessage(string originalText, object? parsed)
    {
      if (parsed is JsonElement je)
      {
        // Common shapes: { message: "..." }, { error: "..." }, ProblemDetails { title: "..." }
        if (je.ValueKind == JsonValueKind.Object)
        {
          if (je.TryGetProperty("message", out var messageProp) && messageProp.ValueKind == JsonValueKind.String)
            return messageProp.GetString();
          if (je.TryGetProperty("error", out var errorProp) && errorProp.ValueKind == JsonValueKind.String)
            return errorProp.GetString();
          if (je.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
            return titleProp.GetString();

          // ASP.NET Core validation errors: { errors: { Field: ["msg1", "msg2"] } }
          if (je.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Object)
          {
            foreach (var field in errorsProp.EnumerateObject())
            {
              if (field.Value.ValueKind == JsonValueKind.Array)
              {
                foreach (var item in field.Value.EnumerateArray())
                {
                  if (item.ValueKind == JsonValueKind.String)
                    return item.GetString();
                }
              }
              else if (field.Value.ValueKind == JsonValueKind.String)
              {
                return field.Value.GetString();
              }
            }
          }

          // Fallback: compact the JSON as message
          return JsonSerializer.Serialize(je);
        }
        if (je.ValueKind == JsonValueKind.String)
        {
          return je.GetString();
        }
      }

      // Not JSON or couldn't extract: use first non-empty line of text
      if (!string.IsNullOrWhiteSpace(originalText))
        return TrimForMessage(originalText);

      return null;
    }
  }

  public static class Always200ResponseExtensions
  {
    public static IApplicationBuilder UseAlways200ResponseWrapper(this IApplicationBuilder app)
      => app.UseMiddleware<Always200ResponseMiddleware>();
  }
}

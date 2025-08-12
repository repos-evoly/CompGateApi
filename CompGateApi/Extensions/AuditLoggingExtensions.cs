using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;          // IAuditLogRepository, IUserRepository
using CompGateApi.Data.Models;                // AuditLog
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CompGateApi.Extensions
{
    public static class AuditLoggingExtensions
    {
    
        public static IApplicationBuilder UseAuditLogging(
            this IApplicationBuilder app,
            Action<AuditLoggingOptions>? configure = null)
        {
            var options = new AuditLoggingOptions();
            configure?.Invoke(options);

            return app.Use(async (context, next) =>
            {
                // Skip trivial routes if configured
                if (options.ShouldSkip(context))
                {
                    await next();
                    return;
                }

                var repo = context.RequestServices.GetRequiredService<IAuditLogRepository>();
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // ── Capture request body safely ───────────────────────────────
                string? requestBody = null;
                if (options.CaptureRequestBody && CanHaveBody(context.Request.Method) && context.Request.ContentLength is > 0)
                {
                    context.Request.EnableBuffering();
                    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                    var raw = await reader.ReadToEndAsync();
                    context.Request.Body.Position = 0;
                    requestBody = Trim(options.Scrub(raw), options.MaxBodyChars);
                }

                // ── Capture response body ────────────────────────────────────
                string? responseBody = null;
                var originalBody = context.Response.Body;
                await using var mem = new MemoryStream();
                if (options.CaptureResponseBody)
                    context.Response.Body = mem;

                int status = 500; // default in case an exception occurs before we set it
                try
                {
                    await next();
                    status = context.Response.StatusCode;
                }
                catch
                {
                    status = 500; // ensure it's assigned on error
                    throw;        // let your exception middleware handle it
                }
                finally
                {
                    if (options.CaptureResponseBody)
                    {
                        context.Response.Body.Seek(0, SeekOrigin.Begin);
                        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
                        var raw = await reader.ReadToEndAsync();
                        responseBody = Trim(options.Scrub(raw), options.MaxBodyChars);

                        context.Response.Body.Seek(0, SeekOrigin.Begin);
                        await context.Response.Body.CopyToAsync(originalBody);
                        context.Response.Body = originalBody;
                    }

                    var log = await BuildAuditLogAsync(context, status, sw.ElapsedMilliseconds, requestBody, responseBody, options);
                    try { await repo.AddAsync(log); } catch { /* don't break pipeline */ }
                }

            });
        }

        // ───────────────────────── helpers ─────────────────────────

        private static bool CanHaveBody(string method)
            => method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            || method.Equals("PUT", StringComparison.OrdinalIgnoreCase)
            || method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);

        private static string Trim(string input, int max)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Length <= max ? input : input[..max];
        }

        private static async Task<AuditLog> BuildAuditLogAsync(
            HttpContext ctx,
            int statusCode,
            long durationMs,
            string? reqBody,
            string? respBody,
            AuditLoggingOptions options)
        {
            var user = ctx.User;

            int? authUserId = TryParseInt(
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("nameid")?.Value
            );

            string? role = user.FindFirst(ClaimTypes.Role)?.Value;
            string? username = user.FindFirst(ClaimTypes.Name)?.Value
                               ?? user.FindFirst(ClaimTypes.Email)?.Value;

            int? companyId = null;
            int? appUserId = null;

            // Optional enrichment via IUserRepository (if available)
            try
            {
                var userRepo = ctx.RequestServices.GetService<IUserRepository>();
                if (userRepo != null && authUserId.HasValue)
                {
                    var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                    var me = await userRepo.GetUserByAuthId(authUserId.Value, bearer);
                    if (me != null)
                    {
                        appUserId = me.UserId;
                        companyId = me.CompanyId;
                    }
                }
            }
            catch { /* ignore */ }

            var log = new AuditLog
            {
                AuthUserId = authUserId,
                AppUserId = appUserId,
                CompanyId = companyId,
                Username = username,
                Role = role,

                Method = ctx.Request.Method,
                Path = ctx.Request.Path.Value ?? string.Empty,
                QueryString = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : null,
                RouteName = ctx.GetEndpoint()?.DisplayName,

                Ip = ctx.Connection.RemoteIpAddress?.ToString(),
                UserAgent = ctx.Request.Headers["User-Agent"].FirstOrDefault(),

                StatusCode = statusCode,
                DurationMs = durationMs,

                RequestBody = reqBody,
                ResponseBody = respBody,

                CreatedAt = DateTimeOffset.UtcNow
            };

            // Custom extras object -> JSON
            if (options.EnrichExtras != null)
            {
                try
                {
                    var extra = options.EnrichExtras(ctx);
                    if (extra != null)
                        log.ExtrasJson = JsonSerializer.Serialize(extra);
                }
                catch { /* ignore */ }
            }

            return log;
        }

        private static int? TryParseInt(string? s) => int.TryParse(s, out var v) ? v : null;
    }

    // ───────────────────── options (in same namespace) ─────────────────────
    public sealed class AuditLoggingOptions
    {
        /// <summary>Capture and store request body (POST/PUT/PATCH).</summary>
        public bool CaptureRequestBody { get; set; } = true;

        /// <summary>Capture and store response body.</summary>
        public bool CaptureResponseBody { get; set; } = true;

        /// <summary>Max chars to persist for bodies.</summary>
        public int MaxBodyChars { get; set; } = 4000;

        /// <summary>Route/Path prefixes to skip (e.g. "/swagger", "/health").</summary>
        public string[] SkipPathStartsWith { get; set; } = new[] { "/swagger", "/health" };

        /// <summary>Additional per-request metadata.</summary>
        public Func<HttpContext, object?>? EnrichExtras { get; set; }

        /// <summary>Mask sensitive fields in serialized JSON bodies.</summary>
        public Func<string, string> Scrub { get; set; } = DefaultScrubber;

        /// <summary>If true, skip this request based on configured paths.</summary>
        public bool ShouldSkip(HttpContext ctx)
        {
            var path = ctx.Request.Path.Value ?? string.Empty;
            return SkipPathStartsWith.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        // Very simple JSON scrubber (mask common keys). Customize as needed.
        private static string DefaultScrubber(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return body;

            static string Mask(string src, string key)
            {
                // naive masking: "password":"...." or 'password':'....'
                var patterns = new[]
                {
                    $"\"{key}\":\"",
                    $"'{key}':'"
                };
                foreach (var p in patterns)
                {
                    var idx = src.IndexOf(p, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        var start = idx + p.Length;
                        var end = src.IndexOfAny(new[] { '"', '\'' }, start);
                        if (end > start)
                        {
                            var len = end - start;
                            var masked = new string('*', Math.Min(len, 12));
                            src = src.Remove(start, len).Insert(start, masked);
                        }
                    }
                }
                return src;
            }

            var scrubbed = body;
            foreach (var key in new[] { "password", "pwd", "token", "accessToken", "refreshToken", "secret", "otp", "twoFactorCode" })
                scrubbed = Mask(scrubbed, key);

            return scrubbed;
        }
    }
}

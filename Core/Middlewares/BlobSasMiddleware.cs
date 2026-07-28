using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Core.Interfaces.Services;
using Microsoft.AspNetCore.Http;

namespace Core.Middlewares;

/// <summary>
/// Blob URLs are stored bare in the DB (no SAS token — tokens expire, so a saved
/// one is dead weight and leaks a credential into every row). This middleware
/// re-attaches a short-lived read SAS to every blob URL on the way out, so the
/// client always receives a URL it can actually load.
///
/// Only JSON responses are touched, and only URLs on the storage account host
/// that don't already carry a query string (the upload endpoint signs its own).
/// </summary>
public class BlobSasMiddleware(RequestDelegate _next)
{
    // Compiled once per host — the host never changes at runtime.
    private static Regex _blobUrlPattern;
    private static string _patternHost;

    public async Task InvokeAsync(HttpContext context, IBlobService blob)
    {
        var host = blob.BlobHost;
        if (string.IsNullOrEmpty(host))
        {
            await _next(context);
            return;
        }

        // Buffer the response so the body can be rewritten. Headers aren't flushed
        // while Body points at a MemoryStream, so ContentLength stays editable.
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            buffer.Position = 0;

            var isJson = context.Response.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;
            if (!isJson || buffer.Length == 0)
            {
                context.Response.Body = originalBody;
                await buffer.CopyToAsync(originalBody);
                return;
            }

            var body = Encoding.UTF8.GetString(buffer.ToArray());
            var rewritten = Sign(body, host, blob);

            context.Response.Body = originalBody;
            var bytes = Encoding.UTF8.GetBytes(rewritten);
            // Length changed — a stale Content-Length would truncate the response.
            context.Response.ContentLength = bytes.Length;
            await context.Response.Body.WriteAsync(bytes);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static string Sign(string body, string host, IBlobService blob)
    {
        if (!body.Contains(host, StringComparison.OrdinalIgnoreCase)) return body;

        if (_blobUrlPattern == null || _patternHost != host)
        {
            _patternHost = host;
            // Stops at the JSON string terminator; a URL that already has "?" is
            // matched and then skipped below.
            _blobUrlPattern = new Regex($@"https?://{Regex.Escape(host)}/[^""\\\s]+",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        // Same blob repeats across a list — sign each distinct URL once.
        var signed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return _blobUrlPattern.Replace(body, m =>
        {
            var url = m.Value;
            if (url.Contains('?')) return url;
            if (signed.TryGetValue(url, out var cached)) return cached;

            try
            {
                var sas = blob.GenerateSasUrl(url);
                signed[url] = sas;
                return sas;
            }
            catch
            {
                // A malformed URL in the DB shouldn't take the whole response down.
                signed[url] = url;
                return url;
            }
        });
    }
}

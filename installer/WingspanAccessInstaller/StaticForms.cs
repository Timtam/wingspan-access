using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WingspanAccessInstaller;

internal enum StaticFormsFailure
{
    /// <summary>No connection, or no answer in time.</summary>
    Network,
    /// <summary>400: the form refused the data.</summary>
    Rejected,
    /// <summary>401: the key is wrong.</summary>
    InvalidKey,
    /// <summary>403: spam protection failed, domain not allowed, form paused or account blocked.</summary>
    Forbidden,
    /// <summary>413: more than 4.5 MB.</summary>
    TooLarge,
    /// <summary>429: too many reports or challenges in a short time, or the monthly quota.</summary>
    RateLimited,
    /// <summary>5xx and anything else.</summary>
    Server,
}

internal sealed class StaticFormsException : Exception
{
    public StaticFormsException(StaticFormsFailure failure, int status, string serverMessage, Exception inner = null)
        : base($"{failure} ({(status > 0 ? status.ToString() : "no answer")}): {serverMessage}", inner)
    {
        Failure = failure;
        Status = status;
        ServerMessage = serverMessage;
    }

    public StaticFormsFailure Failure { get; }

    public int Status { get; }

    public string ServerMessage { get; }
}

/// <summary>
/// Sends a form to Static Forms (staticforms.dev) the way a web page does: multipart/form-data to
/// the submit endpoint with the form's key in "apiKey", files as file parts, and the solved ALTCHA
/// challenge in "altchaToken". Answers come as JSON because of "Accept: application/json"; errors
/// as {"error": ...}.
/// </summary>
internal sealed class StaticFormsClient : IDisposable
{
    public const string DefaultSubmitUrl = "https://api.staticforms.dev/submit";
    public const string DefaultChallengeUrl = "https://www.staticforms.dev/api/altcha/challenge";

    private readonly HttpClient _http;
    private readonly string _key;
    private readonly string _submitUrl;
    private readonly string _challengeUrl;

    public StaticFormsClient(string apiKey, string submitUrl = null, string challengeUrl = null, TimeSpan? timeout = null)
    {
        _key = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _submitUrl = submitUrl ?? DefaultSubmitUrl;
        _challengeUrl = challengeUrl ?? DefaultChallengeUrl;
        _http = new HttpClient { Timeout = timeout ?? TimeSpan.FromMinutes(3) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"WingspanAccessInstaller/{BuildInfo.Version}");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    /// <summary>A new ALTCHA challenge for this form (JSON). The key goes along as a query parameter.</summary>
    public string FetchChallenge(CancellationToken token)
    {
        Log.Detail($"GET {_challengeUrl}");
        string url = _challengeUrl + (_challengeUrl.Contains("?") ? "&" : "?") + "apiKey=" + Uri.EscapeDataString(_key);
        (int status, string body) = Send(() => new HttpRequestMessage(HttpMethod.Get, url), token);
        Log.Detail($"Challenge: HTTP {status}, {body.Length} characters");
        return body;
    }

    /// <summary>
    /// Sends the form. Fields with a null value are left out; <paramref name="file"/> (may be null)
    /// goes as a file part. Returns the submission ID if Static Forms names one.
    /// </summary>
    public string Submit(IEnumerable<KeyValuePair<string, string>> fields, string fileField, string fileName, byte[] file, CancellationToken token)
    {
        var parts = new List<KeyValuePair<string, string>> { new("apiKey", _key) };
        parts.AddRange(fields);

        HttpRequestMessage NewRequest()
        {
            var content = new MultipartFormDataContent("----WingspanAccessReport" + Guid.NewGuid().ToString("N"));
            foreach (KeyValuePair<string, string> part in parts)
            {
                if (part.Value == null)
                    continue;
                // Like a browser: UTF-8 text without a Content-Type of its own, the name quoted.
                var text = new ByteArrayContent(Encoding.UTF8.GetBytes(part.Value));
                text.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = Quote(part.Key) };
                content.Add(text);
            }

            if (file != null)
            {
                var data = new ByteArrayContent(file);
                data.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = Quote(fileField), FileName = Quote(fileName) };
                data.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                content.Add(data);
            }

            var request = new HttpRequestMessage(HttpMethod.Post, _submitUrl) { Content = content };
            // The whole report at once, without waiting for "100 Continue" first.
            request.Headers.ExpectContinue = false;
            return request;
        }

        Log.Detail($"POST {_submitUrl} ({parts.Count} fields, {fileName}: {file?.Length ?? 0} bytes)");
        (int status, string body) = Send(NewRequest, token);
        Log.Detail($"Submitted: HTTP {status}");
        return JsonValue(body, "id");
    }

    public void Dispose() => _http.Dispose();

    /// <summary>The message of an error answer: {"error": "..."} or {"error": {"message": "..."}}, else the text itself.</summary>
    public static string ErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;
        try
        {
            if (new JavaScriptSerializer().DeserializeObject(body) is Dictionary<string, object> map)
            {
                foreach (string name in new[] { "error", "message" })
                {
                    if (!map.TryGetValue(name, out object value) || value == null)
                        continue;
                    if (value is Dictionary<string, object> inner && inner.TryGetValue("message", out object text) && text != null)
                        return text.ToString();
                    if (value is string s)
                        return s;
                }
            }
        }
        catch (ArgumentException)
        {
            // Not JSON (a proxy's HTML page, for example).
        }

        string plain = body.Trim();
        return plain.Length > 300 ? plain.Substring(0, 300) + " ..." : plain;
    }

    public static StaticFormsFailure FailureFor(int status) => status switch
    {
        400 => StaticFormsFailure.Rejected,
        401 => StaticFormsFailure.InvalidKey,
        403 => StaticFormsFailure.Forbidden,
        413 => StaticFormsFailure.TooLarge,
        429 => StaticFormsFailure.RateLimited,
        _ => StaticFormsFailure.Server,
    };

    private (int Status, string Body) Send(Func<HttpRequestMessage> newRequest, CancellationToken token)
    {
        try
        {
            using HttpRequestMessage request = newRequest();
            using HttpResponseMessage response = _http.SendAsync(request, token).GetAwaiter().GetResult();
            string body = response.Content == null ? string.Empty : response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            int status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                string message = ErrorMessage(body);
                Log.Detail($"HTTP {status}: {message}");
                throw new StaticFormsException(FailureFor(status), status, message);
            }

            // Static Forms answers 200 for a submission; one that says it failed counts as refused.
            if (JsonValue(body, "success") is string success && success.Equals("false", StringComparison.OrdinalIgnoreCase))
                throw new StaticFormsException(StaticFormsFailure.Rejected, status, ErrorMessage(body));
            return (status, body);
        }
        catch (TaskCanceledException e) when (!token.IsCancellationRequested)
        {
            throw new StaticFormsException(StaticFormsFailure.Network, 0, "timeout", e);
        }
        catch (HttpRequestException e)
        {
            throw new StaticFormsException(StaticFormsFailure.Network, 0, InnermostMessage(e), e);
        }
        catch (Exception e) when (e is IOException || e is WebException)
        {
            throw new StaticFormsException(StaticFormsFailure.Network, 0, InnermostMessage(e), e);
        }
    }

    /// <summary>A top-level value of a JSON object as text ("true", "abc", "12"), or null.</summary>
    private static string JsonValue(string body, string name)
    {
        try
        {
            if (new JavaScriptSerializer().DeserializeObject(body ?? string.Empty) is Dictionary<string, object> map &&
                map.TryGetValue(name, out object value) && value != null && value is not Dictionary<string, object> && value is not object[])
                return value is bool b ? (b ? "true" : "false") : value.ToString();
        }
        catch (ArgumentException)
        {
        }

        return null;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "%22") + "\"";

    private static string InnermostMessage(Exception e)
    {
        while (e.InnerException != null)
            e = e.InnerException;
        return e.Message;
    }
}

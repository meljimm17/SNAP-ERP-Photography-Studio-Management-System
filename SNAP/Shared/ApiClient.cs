using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SNAP
{
    /// <summary>Matches SNAP.api's AuthController.CompanyLoginResponse shape - can't reference that
    /// type directly since SNAP doesn't (and shouldn't) depend on the SNAP.api project.</summary>
    public sealed record ApiCompanyLoginResponse(string Token, string Role, string DisplayName, int CompanyId, string PlanTier, int UserId, int? BranchId);

    /// <summary>
    /// Thin HttpClient wrapper every converted WinForms Controller (Presenter) calls instead of
    /// touching TenantErpDbContext directly. Attaches the bearer token from AppSession
    /// automatically, and translates a non-2xx response into the same
    /// InvalidOperationException-with-message the WinForms Controllers already catch and display -
    /// a business-rule violation the API returns (400) and a genuine connectivity failure both
    /// surface to the View the same way a local EF exception used to. Kept synchronous (HttpClient's
    /// Send, not SendAsync) to match the rest of the app, which has never used async/await - every
    /// database call was already blocking the UI thread before this, so this isn't a new tradeoff.
    /// </summary>
    public static class ApiClient
    {
        private static readonly HttpClient Client = new();

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public static T Get<T>(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(path));
            return Send<T>(request);
        }

        public static T Post<T>(string path, object? body = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(path));
            if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
            return Send<T>(request);
        }

        public static void Post(string path, object? body = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(path));
            if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
            SendNoContent(request);
        }

        public static void Put(string path, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, BuildUrl(path));
            request.Content = JsonContent.Create(body, options: JsonOptions);
            SendNoContent(request);
        }

        public static void Delete(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, BuildUrl(path));
            SendNoContent(request);
        }

        private static string BuildUrl(string path) => $"{ApiConfig.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

        private static void AttachAuth(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(AppSession.CurrentApiToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSession.CurrentApiToken);
            }
        }

        private static HttpResponseMessage SendRequest(HttpRequestMessage request)
        {
            AttachAuth(request);
            try
            {
                return Client.Send(request);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to reach the SNAP server: {ex.Message}", ex);
            }
        }

        private static T Send<T>(HttpRequestMessage request)
        {
            using var response = SendRequest(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(ExtractErrorMessage(response));
            }

            using var stream = response.Content.ReadAsStream();
            return JsonSerializer.Deserialize<T>(stream, JsonOptions)
                ?? throw new InvalidOperationException("The server returned an empty response.");
        }

        private static void SendNoContent(HttpRequestMessage request)
        {
            using var response = SendRequest(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(ExtractErrorMessage(response));
            }
        }

        private static string ExtractErrorMessage(HttpResponseMessage response)
        {
            try
            {
                using var stream = response.Content.ReadAsStream();
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString() ?? response.ReasonPhrase ?? "Request failed.";
                }
            }
            catch
            {
                // The error body wasn't the expected { "message": "..." } shape (or there wasn't
                // one at all) - fall through to the generic message below.
            }

            return $"Request failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
        }
    }
}

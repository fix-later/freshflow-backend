using System.Net.Http.Headers;
using Google.Apis.Auth.OAuth2;

namespace FreshFlow.API.Assistant.Llm;

/// <summary>
/// Injects a fresh GCP OAuth2 bearer token on every outbound request to the Vertex AI
/// OpenAI-compatible endpoint. The OpenAI SDK is configured with a throwaway API-key credential;
/// this handler overwrites the <c>Authorization</c> header it sets so real auth flows through
/// Application Default Credentials instead.
/// <para>
/// <see cref="GoogleCredential"/> caches the access token and refreshes it before expiry
/// internally, so there is no token-refresh logic to hand-roll here.
/// </para>
/// </summary>
internal sealed class GcpAuthHandler : DelegatingHandler
{
    private const string CloudPlatformScope = "https://www.googleapis.com/auth/cloud-platform";

    // Load ADC once; the credential is thread-safe and refreshes its token on demand.
    private readonly Task<GoogleCredential> _credential = LoadScopedCredentialAsync();

    private static async Task<GoogleCredential> LoadScopedCredentialAsync()
    {
        var credential = await GoogleCredential.GetApplicationDefaultAsync();
        return credential.IsCreateScopedRequired
            ? credential.CreateScoped(CloudPlatformScope)
            : credential;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var credential = await _credential;
        var token = await ((ITokenAccess)credential).GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}

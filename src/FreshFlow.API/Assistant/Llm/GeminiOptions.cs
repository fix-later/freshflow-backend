using System.ComponentModel.DataAnnotations;

namespace FreshFlow.API.Assistant.Llm;

/// <summary>
/// Binds to config section "Assistant:Gemini". Backs the assistant with Gemini on Vertex AI
/// (Gemini Enterprise Agent Platform) via its OpenAI-compatible Chat Completions endpoint.
/// <para>
/// There is no API-key field: Vertex authenticates with a short-lived OAuth2 access token minted
/// from Application Default Credentials (see <see cref="GcpAuthHandler"/>). Locally run
/// <c>gcloud auth application-default login</c>; in prod use Workload Identity or a service-account
/// key via <c>GOOGLE_APPLICATION_CREDENTIALS</c> (role <c>roles/aiplatform.user</c>). Validated with
/// DataAnnotations + ValidateOnStart so a missing project/location fails fast at boot.
/// </para>
/// </summary>
public sealed class GeminiOptions
{
    /// <summary>GCP project id that owns the Vertex AI quota, e.g. <c>freshflow-xxxxx</c>.</summary>
    [Required]
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>Vertex AI region, e.g. <c>asia-southeast1</c>. Also forms the endpoint host.</summary>
    [Required]
    public string Location { get; init; } = "asia-southeast1";

    /// <summary>Model id in OpenAI-compat form, e.g. <c>google/gemini-2.5-flash</c>.</summary>
    [Required]
    public string Model { get; init; } = "google/gemini-2.5-flash";

    [Range(1, 600)]
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>
    /// Vertex AI OpenAI-compatible base endpoint. The OpenAI SDK appends <c>/chat/completions</c>.
    /// Regional shape: <c>https://{Location}-aiplatform.googleapis.com/v1/projects/{ProjectId}/locations/{Location}/endpoints/openapi</c>.
    /// The <c>global</c> location has no region prefix — its host is <c>aiplatform.googleapis.com</c>.
    /// </summary>
    public Uri BuildEndpoint()
    {
        var host = Location == "global" ? "aiplatform.googleapis.com" : $"{Location}-aiplatform.googleapis.com";
        return new($"https://{host}/v1/projects/{ProjectId}/locations/{Location}/endpoints/openapi");
    }
}

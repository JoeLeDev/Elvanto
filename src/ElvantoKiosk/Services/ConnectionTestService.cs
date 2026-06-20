using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ElvantoKiosk.Services;

public static class ConnectionTestService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static async Task<(bool Success, string Message)> TestAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (false, "Aucune URL à tester.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return (false, "URL invalide.");

        try
        {
            using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode
                ? (true, $"Connexion OK ({(int)response.StatusCode}) — {uri.Host}")
                : (false, $"Réponse HTTP {(int)response.StatusCode} — {uri.Host}");
        }
        catch (Exception ex)
        {
            return (false, $"Échec : {ex.Message}");
        }
    }
}

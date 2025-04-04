namespace Zona.Web;

public class ZonaApiClient(HttpClient httpClient)
{
    public async Task<AuthUrlResponse?> GetAuthUrlAsync(CancellationToken cancellationToken = default) => 
        await httpClient.GetFromJsonAsync<AuthUrlResponse>("/auth/strava-url", cancellationToken);
}

public record AuthUrlResponse(string Url);
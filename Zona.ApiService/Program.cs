using System.Net.Http.Headers;
using System.Text.Json;
using Zona.ApiService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/auth/strava-url", (IConfiguration config) =>
{
    var clientId = config["Strava:ClientId"];
    var redirectUri = config["Strava:RedirectUri"];
    var scope = "activity:read_all";

    var url = $"https://www.strava.com/oauth/authorize" +
              $"?client_id={clientId}" +
              $"&response_type=code" +
              $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
              $"&scope={Uri.EscapeDataString(scope)}" +
              $"&approval_prompt=auto";

    return Results.Ok(new { url });
});

app.MapGet("/auth/callback", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    var code = context.Request.Query["code"].ToString();
    if (string.IsNullOrEmpty(code))
    {
        return Results.BadRequest("Código não fornecido.");
    }

    var clientId = config["Strava:ClientId"];
    var clientSecret = config["Strava:ClientSecret"];
    var redirectUri = config["Strava:RedirectUri"];

    var httpClient = httpClientFactory.CreateClient();

    var tokenResponse = await httpClient.PostAsync("https://www.strava.com/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        { "client_id", clientId },
        { "client_secret", clientSecret },
        { "code", code },
        { "grant_type", "authorization_code" },
        { "redirect_uri", redirectUri }
    }));

    if (!tokenResponse.IsSuccessStatusCode)
    {
        var error = await tokenResponse.Content.ReadAsStringAsync();
        return Results.BadRequest($"Erro ao obter token: {error}");
    }

    
    //return Results.Ok(JsonDocument.Parse(athleteJson));
    var json = await tokenResponse.Content.ReadAsStringAsync();
    var tokenObj = JsonDocument.Parse(json).RootElement;

    var accessToken = tokenObj.GetProperty("access_token").GetString();
    var athleteId = tokenObj.GetProperty("athlete").GetProperty("id").GetInt32();

    // [2] Salva esse token temporariamente (em memória, banco ou cache)
    TokensStore.StoreToken(athleteId, accessToken!);

    // [3] Redireciona o usuário de volta ao Blazor Web
    return Results.Redirect($"https://localhost:7213/auth-completa?athleteId={athleteId}");
});

app.MapGet("/athlete/activities/{athleteId:int}", async (IHttpClientFactory httpClientFactory, int athleteId) =>
{
    var accessToken = TokensStore.GetToken(athleteId);
    if (accessToken is null) return Results.Unauthorized();

    // Agora chama o /athlete com o token
    var apiClient = httpClientFactory.CreateClient();
    apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    var athleteResponse = await apiClient.GetAsync("https://www.strava.com/api/v3/athlete");

    if (!athleteResponse.IsSuccessStatusCode)
    {
        var error = await athleteResponse.Content.ReadAsStringAsync();
        return Results.BadRequest($"Erro ao obter atleta: {error}");
    }

    var athleteJson = await athleteResponse.Content.ReadAsStringAsync();
    return Results.Ok(athleteJson);
});


app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

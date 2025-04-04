namespace Zona.ApiService.Services;

public static class TokensStore
{
    private static Dictionary<int, string> _tokens = [];

    public static void StoreToken(int athleteId, string token)
    {
        _tokens[athleteId] = token;
    }

    public static string? GetToken(int athleteId)
    {
        return _tokens.TryGetValue(athleteId, out var token) ? token : null;
    }
}

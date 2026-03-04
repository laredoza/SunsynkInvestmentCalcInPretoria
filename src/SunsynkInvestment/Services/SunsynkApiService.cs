using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SunsynkInvestment.Configuration;
using SunsynkInvestment.Models;

namespace SunsynkInvestment.Services;

public class SunsynkApiService
{
    private readonly HttpClient _httpClient;
    private readonly SunsynkSettings _settings;
    private string? _accessToken;

    private const string Source = "sunsynk";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SunsynkApiService(HttpClient httpClient, SunsynkSettings settings)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(settings.ApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _settings = settings;
    }

    public async Task AuthenticateAsync()
    {
        // Step 1: Generate nonce and sign, fetch RSA public key
        var nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var publicKeySign = ComputeMd5($"nonce={nonce}&source={Source}POWER_VIEW");

        var publicKeyUrl = $"anonymous/publicKey?nonce={nonce}&source={Source}&sign={publicKeySign}";
        var pkResponse = await _httpClient.GetAsync(publicKeyUrl);
        var pkContent = await pkResponse.Content.ReadAsStringAsync();

        if (!pkResponse.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch public key ({pkResponse.StatusCode}): {pkContent}");

        var pkResult = JsonSerializer.Deserialize<SunsynkApiResponse<string>>(pkContent, JsonOptions);
        if (pkResult is not { Success: true } || string.IsNullOrEmpty(pkResult.Data))
            throw new Exception($"Public key fetch failed: {pkResult?.Msg ?? pkContent}");

        var publicKeyBase64 = pkResult.Data;

        // Step 2: Encrypt password with RSA public key
        var encryptedPassword = RsaEncrypt(_settings.Password, publicKeyBase64);

        // Step 3: Compute token sign (uses first 10 chars of public key)
        var tokenNonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var tokenSign = ComputeMd5($"nonce={tokenNonce}&source={Source}{publicKeyBase64[..10]}");

        // Step 4: POST to /oauth/token/new
        var tokenBody = new
        {
            sign = tokenSign,
            nonce = tokenNonce,
            username = _settings.Username,
            password = encryptedPassword,
            grant_type = "password",
            client_id = "csp-web",
            source = Source
        };

        var tokenResponse = await _httpClient.PostAsJsonAsync("oauth/token/new", tokenBody);
        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
            throw new Exception($"Authentication failed ({tokenResponse.StatusCode}): {tokenContent}");

        var authResponse = JsonSerializer.Deserialize<SunsynkAuthResponse>(tokenContent, JsonOptions);

        if (authResponse is not { Success: true } || string.IsNullOrEmpty(authResponse.Data.AccessToken))
            throw new Exception($"Authentication failed: {authResponse?.Msg ?? tokenContent}");

        _accessToken = authResponse.Data.AccessToken;
    }

    public async Task<List<MonthlyEnergy>> FetchYearlyEnergyAsync(int year)
    {
        if (_accessToken == null)
            throw new InvalidOperationException("Not authenticated. Call AuthenticateAsync first.");

        var plantId = _settings.PlantId;
        var url = $"api/v1/plant/energy/{plantId}/year?lan=en&date={year}&id={plantId}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Bearer {_accessToken}");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch energy data for {year} ({response.StatusCode}): {content}");

        var energyResponse = JsonSerializer.Deserialize<SunsynkEnergyResponse>(content, JsonOptions);

        if (energyResponse is not { Success: true })
            throw new Exception($"Energy API error for {year}: {energyResponse?.Msg ?? content}");

        return PivotToMonthlyEnergy(year, energyResponse.Data);
    }

    private static string ComputeMd5(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    private static string RsaEncrypt(string plainText, string publicKeyBase64)
    {
        var keyBytes = Convert.FromBase64String(publicKeyBase64);
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
        var encrypted = rsa.Encrypt(Encoding.UTF8.GetBytes(plainText), RSAEncryptionPadding.Pkcs1);
        return Convert.ToBase64String(encrypted);
    }

    private static List<MonthlyEnergy> PivotToMonthlyEnergy(int year, SunsynkEnergyData data)
    {
        var months = data.Infos
            .SelectMany(i => i.Records)
            .Select(r => int.Parse(r.Time))
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        var lookup = new Dictionary<string, Dictionary<int, decimal>>();
        foreach (var info in data.Infos)
        {
            var monthValues = new Dictionary<int, decimal>();
            foreach (var record in info.Records)
            {
                if (decimal.TryParse(record.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
                    monthValues[int.Parse(record.Time)] = val;
            }
            lookup[info.Label] = monthValues;
        }

        decimal GetValue(string label, int month) =>
            lookup.TryGetValue(label, out var dict) && dict.TryGetValue(month, out var val) ? val : 0m;

        var now = DateTime.Now;
        return months
            .Where(m => GetValue("PV", m) > 0 || GetValue("Load", m) > 0)
            .Select(m => new MonthlyEnergy
            {
                Year = year,
                Month = m,
                Pv = GetValue("PV", m),
                Load = GetValue("Load", m),
                Export = GetValue("Export", m),
                Import = GetValue("Import", m),
                Discharge = GetValue("Discharge", m),
                Charge = GetValue("Charge", m),
                FetchedAt = now
            })
            .ToList();
    }
}

public class SunsynkApiResponse<T>
{
    public int Code { get; set; }
    public string Msg { get; set; } = "";
    public bool Success { get; set; }
    public T? Data { get; set; }
}

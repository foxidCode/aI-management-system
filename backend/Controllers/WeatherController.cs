using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WeatherController : ControllerBase
{
    private readonly HttpClient _http;

    public WeatherController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient();
    }

    private async Task<string?> ReverseGeocodeAsync(double lat, double lon)
    {
        // 服务1: Nominatim（国际）
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var res = await _http.GetStringAsync(
                $"https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lon}&format=json&zoom=10&accept-language=zh",
                cts.Token);
            using var doc = JsonDocument.Parse(res);
            var addr = doc.RootElement.GetProperty("address");
            foreach (var key in new[] { "county", "city", "town", "state" })
                if (addr.TryGetProperty(key, out var v))
                    return v.GetString();
        }
        catch { }

        // 服务2: BigDataCloud
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var res = await _http.GetStringAsync(
                $"https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={lat}&longitude={lon}&localityLanguage=zh",
                cts.Token);
            using var doc = JsonDocument.Parse(res);
            var root = doc.RootElement;
            return root.TryGetProperty("locality", out var l) ? l.GetString()
                : root.TryGetProperty("city", out var c) ? c.GetString()
                : root.TryGetProperty("principalSubdivision", out var p) ? p.GetString()
                : null;
        }
        catch { return null; }
    }

    private static string? BuildCityName(string? city, string? district)
    {
        static string AddSuffix(string name, string suffix)
        {
            if (string.IsNullOrEmpty(name)) return name!;
            if (name.EndsWith('市') || name.EndsWith('县') || name.EndsWith('区')
                || name.EndsWith('州') || name.EndsWith('镇') || name.EndsWith('乡'))
                return name;
            return name + suffix;
        }

        var c = AddSuffix(city ?? "", "市");
        var d = AddSuffix(district ?? "", "县");
        return (!string.IsNullOrEmpty(c), !string.IsNullOrEmpty(d)) switch
        {
            (true, true)  => c + d,
            (true, false) => c,
            (false, true) => d,
            _             => null,
        };
    }

    private async Task<(double lat, double lon, string? city)> GetIpLocationAsync()
    {
        // 服务1: ipapi.co（不填key可用，1200次/天免费额度）
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var res = await _http.GetStringAsync("https://ipapi.co/json/", cts.Token);
            using var doc = JsonDocument.Parse(res);
            var root = doc.RootElement;
            var lat = root.TryGetProperty("latitude", out var la) && la.TryGetDouble(out var laVal) ? laVal : 0;
            var lon = root.TryGetProperty("longitude", out var lo) && lo.TryGetDouble(out var loVal) ? loVal : 0;
            var city = root.TryGetProperty("city", out var ct) ? ct.GetString() : null;
            var region = root.TryGetProperty("region", out var rg) ? rg.GetString() : null;
            if (lat != 0)
                return (lat, lon, BuildCityName(city, region));
        }
        catch { }

        // 服务2: ipinfo.io
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var res = await _http.GetStringAsync("https://ipinfo.io/json", cts.Token);
            using var doc = JsonDocument.Parse(res);
            var root = doc.RootElement;
            var loc = root.TryGetProperty("loc", out var l) ? l.GetString() : null;
            if (!string.IsNullOrEmpty(loc))
            {
                var parts = loc.Split(',');
                if (parts.Length == 2 && double.TryParse(parts[0], out var latVal) && double.TryParse(parts[1], out var lonVal))
                {
                    var city = root.TryGetProperty("city", out var ct) ? ct.GetString() : null;
                    var region = root.TryGetProperty("region", out var rg) ? rg.GetString() : null;
                    return (latVal, lonVal, BuildCityName(city, region));
                }
            }
        }
        catch { }

        // 服务3: my.ip.cn（旧备用）
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var res = await _http.GetStringAsync(
                "https://my.ip.cn/json/?ticket=6c3c421722c502e40e2dd7acb216d6721780007658",
                cts.Token);
            using var doc = JsonDocument.Parse(res);
            var data = doc.RootElement.GetProperty("data");
            var city = data.TryGetProperty("city", out var ct2) ? ct2.GetString() : null;
            var district = data.TryGetProperty("district", out var dt) ? dt.GetString() : null;
            return (1, 1, BuildCityName(city, district));
        }
        catch { }

        return (0, 0, null);
    }

    [HttpGet]
    public async Task<IActionResult> GetWeather([FromQuery] string? city = null, [FromQuery] double? lat = null, [FromQuery] double? lon = null)
    {
        try
        {
            string location;
            string? resolvedCity = null;

            if (!string.IsNullOrEmpty(city))
            {
                resolvedCity = city;
                location = Uri.EscapeDataString(city);
            }
            else if (lat.HasValue && lon.HasValue)
            {
                resolvedCity = await ReverseGeocodeAsync(lat.Value, lon.Value);
                location = resolvedCity ?? $"{lat.Value},{lon.Value}";
            }
            else
            {
                var (ipLat, ipLon, ipCity) = await GetIpLocationAsync();
                if (ipLat != 0)
                {
                    resolvedCity = ipCity ?? await ReverseGeocodeAsync(ipLat, ipLon);
                    location = resolvedCity ?? $"{ipLat},{ipLon}";
                }
                else
                {
                    // IP 定位全部失败，使用默认城市作为兜底
                    resolvedCity = "成都";
                    location = "Chengdu";
                }
            }

            var url = $"https://wttr.in/{location}?format=j1";
            var response = await _http.GetStringAsync(url);
            var weatherData = JsonDocument.Parse(response);

            var result = weatherData.RootElement.Clone();
            return new JsonResult(new { resolvedCity, data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = $"天气数据获取失败: {ex.Message}" });
        }
    }
}

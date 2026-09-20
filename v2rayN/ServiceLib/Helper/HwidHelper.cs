using System.Linq;
using System.Security.Cryptography;

namespace ServiceLib.Helper;

public static class HwidHelper
{
    public const string HappAlphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
    public const int HappHwidLength = 16;

    public static string GenerateHappHwid(int length = HappHwidLength)
    {
        return RandomNumberGenerator.GetString(HappAlphabet, length);
    }

    public static bool IsValidHappHwid(string? hwid)
    {
        if (string.IsNullOrEmpty(hwid) || hwid.Length != HappHwidLength)
        {
            return false;
        }
        foreach (var c in hwid)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z')))
            {
                return false;
            }
        }
        return true;
    }

    public static string GetDeviceOS()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macOS";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }
        return "Unknown";
    }

    public static string GetOSVersion()
    {
        try
        {
            return Environment.OSVersion.Version.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string GetDeviceModel()
    {
        try
        {
            var name = Environment.MachineName;
            return name.IsNullOrEmpty() ? "PC" : name;
        }
        catch
        {
            return "PC";
        }
    }

    public static bool IsHappSubscription(string? url, string? userAgent)
    {
        if (userAgent?.StartsWith("Happ", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (url.IsNullOrEmpty())
        {
            return false;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme.Equals("happ", StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals("happplus", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var host = uri.Host;
            if (host.Equals("mycitadel.ru", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".mycitadel.ru", StringComparison.OrdinalIgnoreCase)
                || host.Equals("happproxy.ru", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".happproxy.ru", StringComparison.OrdinalIgnoreCase)
                || host.Equals("happhost.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".happhost.com", StringComparison.OrdinalIgnoreCase)
                || host.Equals("happvpn.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".happvpn.com", StringComparison.OrdinalIgnoreCase)
                || host.Equals("happ.im", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".happ.im", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var hostParts = host.Split('.');
            if (hostParts.Any(p => p.Equals("happ", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (uri.AbsolutePath.TrimEnd('/').EndsWith("/happ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        else
        {
            if (url.StartsWith("happ://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("happplus://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string GetHappUserAgent()
    {
        var os = GetDeviceOS().ToLowerInvariant();
        if (os == "osx" || os == "macos")
        {
            os = "macos";
        }
        return $"Happ/4.2.5/{os}";
    }

    public static string GetEffectiveHwid(Config? config, SubItem? item)
    {
        if (item != null && !item.RequestHeaders.IsNullOrEmpty())
        {
            if (HttpRequestHeadersHelper.TryParse(item.RequestHeaders, out var customHeaders))
            {
                if (customHeaders.TryGetValue("X-HWID", out var customHwid) && customHwid.IsNotEmpty())
                {
                    return customHwid;
                }
            }
        }

        var isHapp = item != null && IsHappSubscription(item.Url, item.UserAgent);
        var configuredHwid = config?.GuiItem?.Hwid;

        if (isHapp)
        {
            if (IsValidHappHwid(configuredHwid))
            {
                return configuredHwid!;
            }
            return GenerateHappHwid();
        }

        if (configuredHwid.IsNotEmpty())
        {
            return configuredHwid!;
        }

        return GenerateHappHwid();
    }

    public static Dictionary<string, string> BuildSubscriptionHeaders(Config config, SubItem? item)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var isHapp = item != null && IsHappSubscription(item.Url, item.UserAgent);
        var hwid = GetEffectiveHwid(config, item);

        if (config.GuiItem != null && config.GuiItem.Hwid.IsNullOrEmpty())
        {
            config.GuiItem.Hwid = hwid;
        }

        if ((config.GuiItem?.EnableHwid == true || isHapp) && hwid.IsNotEmpty())
        {
            headers["X-HWID"] = hwid;
            var os = GetDeviceOS();
            if (os.IsNotEmpty())
            {
                headers["X-Device-OS"] = os;
            }
            var ver = GetOSVersion();
            if (ver.IsNotEmpty())
            {
                headers["X-Ver-OS"] = ver;
            }
            if (config.GuiItem?.SendDeviceModel == true || isHapp)
            {
                var model = isHapp ? (os == "macOS" ? "Mac" : "PC") : GetDeviceModel();
                if (model.IsNotEmpty())
                {
                    headers["X-Device-Model"] = model;
                }
            }
            if (isHapp)
            {
                headers["X-App-Version"] = "4.2.5";
            }
        }

        if (item != null && !item.RequestHeaders.IsNullOrEmpty())
        {
            if (!HttpRequestHeadersHelper.TryParse(item.RequestHeaders, out var customHeaders))
            {
                throw new FormatException(ResUI.SubRequestHeadersInvalid);
            }
            foreach (var kvp in customHeaders)
            {
                headers[kvp.Key] = kvp.Value;
            }
        }

        return headers;
    }

    public static string ApplyHwidMacro(string url, string? hwid)
    {
        if (url.IsNullOrEmpty() || hwid.IsNullOrEmpty())
        {
            return url;
        }
        if (url.Contains("{hwid}", StringComparison.OrdinalIgnoreCase))
        {
            return url.Replace("{hwid}", hwid, StringComparison.OrdinalIgnoreCase);
        }
        return url;
    }
}

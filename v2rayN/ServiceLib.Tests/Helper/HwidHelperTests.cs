namespace ServiceLib.Tests.Helper;

public class HwidHelperTests
{
    [Test]
    public async Task BuildSubscriptionHeaders_ShouldIncludeHwidHeadersWhenEnabled()
    {
        var config = new Config
        {
            GuiItem = new GUIItem
            {
                EnableHwid = true,
                Hwid = "3f7a1c9e-5b2d-4e8a-9a1b-2c3d4e5f6a7b",
                SendDeviceModel = true
            }
        };
        var subItem = new SubItem();

        var headers = HwidHelper.BuildSubscriptionHeaders(config, subItem);

        await headers.ContainsKey("X-HWID").Should().BeTrue();
        await headers["X-HWID"].Should().BeEqualTo("3f7a1c9e-5b2d-4e8a-9a1b-2c3d4e5f6a7b");
        await headers.ContainsKey("X-Device-OS").Should().BeTrue();
        await headers["X-Device-OS"].Should().BeEqualTo(HwidHelper.GetDeviceOS());
        await headers.ContainsKey("X-Ver-OS").Should().BeTrue();
        await headers.ContainsKey("X-Device-Model").Should().BeTrue();
    }

    [Test]
    public async Task BuildSubscriptionHeaders_ShouldOmitHwidWhenDisabled()
    {
        var config = new Config
        {
            GuiItem = new GUIItem
            {
                EnableHwid = false,
                Hwid = "3f7a1c9e-5b2d-4e8a-9a1b-2c3d4e5f6a7b",
                SendDeviceModel = true
            }
        };
        var subItem = new SubItem();

        var headers = HwidHelper.BuildSubscriptionHeaders(config, subItem);

        await headers.ContainsKey("X-HWID").Should().BeFalse();
        await headers.ContainsKey("X-Device-OS").Should().BeFalse();
        await headers.ContainsKey("X-Ver-OS").Should().BeFalse();
        await headers.ContainsKey("X-Device-Model").Should().BeFalse();
    }

    [Test]
    public async Task BuildSubscriptionHeaders_ShouldRespectSendDeviceModelFalse()
    {
        var config = new Config
        {
            GuiItem = new GUIItem
            {
                EnableHwid = true,
                Hwid = "test-hwid-value",
                SendDeviceModel = false
            }
        };
        var subItem = new SubItem();

        var headers = HwidHelper.BuildSubscriptionHeaders(config, subItem);

        await headers["X-HWID"].Should().BeEqualTo("test-hwid-value");
        await headers.ContainsKey("X-Device-Model").Should().BeFalse();
    }

    [Test]
    public async Task BuildSubscriptionHeaders_CustomHeadersShouldOverrideDefaultHwid()
    {
        var config = new Config
        {
            GuiItem = new GUIItem
            {
                EnableHwid = true,
                Hwid = "default-hwid",
                SendDeviceModel = true
            }
        };
        var subItem = new SubItem
        {
            RequestHeaders = """{"X-HWID": "custom-override-hwid", "X-Custom": "custom-val"}"""
        };

        var headers = HwidHelper.BuildSubscriptionHeaders(config, subItem);

        await headers["X-HWID"].Should().BeEqualTo("custom-override-hwid");
        await headers["X-Custom"].Should().BeEqualTo("custom-val");
    }

    [Test]
    public async Task BuildSubscriptionHeaders_ShouldThrowOnInvalidCustomHeaders()
    {
        var config = new Config { GuiItem = new GUIItem { EnableHwid = true, Hwid = "test" } };
        var subItem = new SubItem { RequestHeaders = "invalid json {" };

        var thrown = false;
        try
        {
            HwidHelper.BuildSubscriptionHeaders(config, subItem);
        }
        catch (FormatException)
        {
            thrown = true;
        }
        await thrown.Should().BeTrue();
    }

    [Test]
    public async Task ApplyHwidMacro_ShouldReplaceHwidMacroCaseInsensitive()
    {
        const string hwid = "my-unique-hwid-12345";
        const string url1 = "https://example.com/sub?token=abc&hwid={hwid}";
        const string url2 = "https://example.com/sub?token=abc&device={HWID}";

        var result1 = HwidHelper.ApplyHwidMacro(url1, hwid);
        var result2 = HwidHelper.ApplyHwidMacro(url2, hwid);

        await result1.Should().BeEqualTo("https://example.com/sub?token=abc&hwid=my-unique-hwid-12345");
        await result2.Should().BeEqualTo("https://example.com/sub?token=abc&device=my-unique-hwid-12345");
    }

    [Test]
    public async Task BuildSubscriptionHeaders_ShouldMimicHappHeadersWhenCitadelSubscription()
    {
        var config = new Config
        {
            GuiItem = new GUIItem
            {
                EnableHwid = false,
                Hwid = "0123456789abcdef",
                SendDeviceModel = true
            }
        };
        var subItem = new SubItem
        {
            Url = "https://mycitadel.ru/sub_token"
        };

        var headers = HwidHelper.BuildSubscriptionHeaders(config, subItem);

        await headers.ContainsKey("X-HWID").Should().BeTrue();
        await headers["X-HWID"].Should().BeEqualTo("0123456789abcdef");
        await headers.ContainsKey("X-Device-OS").Should().BeTrue();
        await headers.ContainsKey("X-App-Version").Should().BeTrue();
        await headers["X-App-Version"].Should().BeEqualTo("4.2.5");
        await headers.ContainsKey("X-Device-Model").Should().BeTrue();
    }

    [Test]
    public async Task BuildSubscriptionHeaders_ShouldDynamicallyGenerateHappHwidWhenEmpty()
    {
        var config = new Config
        {
            GuiItem = new GUIItem
            {
                EnableHwid = false,
                Hwid = string.Empty,
                SendDeviceModel = true
            }
        };
        var subItem = new SubItem
        {
            Url = "https://mycitadel.ru/sample_test_token_123"
        };

        var headers = HwidHelper.BuildSubscriptionHeaders(config, subItem);

        await headers.ContainsKey("X-HWID").Should().BeTrue();
        var generated = headers["X-HWID"];
        await HwidHelper.IsValidHappHwid(generated).Should().BeTrue();
        await config.GuiItem.Hwid.Should().BeEqualTo(generated);
    }

    [Test]
    public async Task GenerateHappHwid_ShouldGenerate16CharAlphanumeric()
    {
        var hwid1 = HwidHelper.GenerateHappHwid();
        var hwid2 = HwidHelper.GenerateHappHwid();

        await hwid1.Length.Should().BeEqualTo(16);
        await hwid2.Length.Should().BeEqualTo(16);
        await HwidHelper.IsValidHappHwid(hwid1).Should().BeTrue();
        await HwidHelper.IsValidHappHwid(hwid2).Should().BeTrue();
        await hwid1.Should().NotBeEqualTo(hwid2);
    }

    [Test]
    public async Task IsValidHappHwid_ShouldValidateCorrectly()
    {
        await HwidHelper.IsValidHappHwid("0123456789abcdef").Should().BeTrue();
        await HwidHelper.IsValidHappHwid("abcdef0123456789").Should().BeTrue();
        await HwidHelper.IsValidHappHwid("short").Should().BeFalse();
        await HwidHelper.IsValidHappHwid("toolongstringwithmorethan16chars").Should().BeFalse();
        await HwidHelper.IsValidHappHwid("209956b8-8ca4-4eee-9e4d-589de1d9a27d").Should().BeFalse(); // UUID
        await HwidHelper.IsValidHappHwid("0123456789ABCDEF").Should().BeFalse(); // Uppercase
        await HwidHelper.IsValidHappHwid(null).Should().BeFalse();
    }

    [Test]
    public async Task BuildSubscriptionHeaders_HappSubscription_ShouldNotMutateCustomHwidInConfig()
    {
        const string customHwid = "3f7a1c9e-5b2d-4e8a-9a1b-2c3d4e5f6a7b";
        var config = new Config
        {
            GuiItem = new GUIItem
            {
                EnableHwid = true,
                Hwid = customHwid,
                SendDeviceModel = true
            }
        };
        var subItem = new SubItem
        {
            Url = "https://mycitadel.ru/token123"
        };

        var headers = HwidHelper.BuildSubscriptionHeaders(config, subItem);

        // Header must contain a valid 16-char Happ HWID
        await HwidHelper.IsValidHappHwid(headers["X-HWID"]).Should().BeTrue();
        // But original global config HWID must remain untouched!
        await config.GuiItem.Hwid.Should().BeEqualTo(customHwid);
    }

    [Test]
    public async Task GetEffectiveHwid_ShouldPrioritizeRequestHeaderThenConfig()
    {
        var config = new Config
        {
            GuiItem = new GUIItem
            {
                Hwid = "config-hwid-val"
            }
        };
        var subWithCustomHeader = new SubItem
        {
            RequestHeaders = """{"X-HWID": "sub-header-hwid"}"""
        };

        var effectiveHwid = HwidHelper.GetEffectiveHwid(config, subWithCustomHeader);
        await effectiveHwid.Should().BeEqualTo("sub-header-hwid");

        var effectiveFromConfig = HwidHelper.GetEffectiveHwid(config, new SubItem());
        await effectiveFromConfig.Should().BeEqualTo("config-hwid-val");
    }

    [Test]
    public async Task IsHappSubscription_ShouldDetectHappAndCitadelUrls()
    {
        await HwidHelper.IsHappSubscription("https://mycitadel.ru/xyz", null).Should().BeTrue();
        await HwidHelper.IsHappSubscription("https://sub.mycitadel.ru/xyz", null).Should().BeTrue();
        await HwidHelper.IsHappSubscription("https://happhost.com/sub", null).Should().BeTrue();
        await HwidHelper.IsHappSubscription("https://happproxy.ru/sub", null).Should().BeTrue();
        await HwidHelper.IsHappSubscription("https://happvpn.com/sub", null).Should().BeTrue();
        await HwidHelper.IsHappSubscription("happ://base64payload", null).Should().BeTrue();
        await HwidHelper.IsHappSubscription("https://happ.example.com/api", null).Should().BeTrue();
        await HwidHelper.IsHappSubscription("https://example.com/path/happ", null).Should().BeTrue();
        await HwidHelper.IsHappSubscription("https://example.com/sub", "Happ/4.2.5/macos").Should().BeTrue();

        // False positive prevention
        await HwidHelper.IsHappSubscription("https://happy-vpn.com/sub", null).Should().BeFalse();
        await HwidHelper.IsHappSubscription("https://example.com/sub?token=unhappy_user", null).Should().BeFalse();
        await HwidHelper.IsHappSubscription("https://example.com/sub", "v2rayN/7.8.2").Should().BeFalse();
        await HwidHelper.IsHappSubscription(null, null).Should().BeFalse();
        await HwidHelper.IsHappSubscription(string.Empty, null).Should().BeFalse();
    }

    [Test]
    public async Task GetHappUserAgent_ShouldReturnValidFormat()
    {
        var ua = HwidHelper.GetHappUserAgent();
        await ua.StartsWith("Happ/4.2.5/").Should().BeTrue();
    }

    [Test]
    public async Task ExpandProcessNamesForPlatform_ShouldExpandChromeAndZenHelpersOnMacOS()
    {
        var chromeList = CoreConfigSingboxService.ExpandProcessNamesForPlatform("Google Chrome").ToList();
        var chromeAppList = CoreConfigSingboxService.ExpandProcessNamesForPlatform("Google Chrome.app").ToList();
        var chromeExeList = CoreConfigSingboxService.ExpandProcessNamesForPlatform("chrome.exe").ToList();
        var zenList = CoreConfigSingboxService.ExpandProcessNamesForPlatform("zen").ToList();
        var zenAppList = CoreConfigSingboxService.ExpandProcessNamesForPlatform("Zen Browser.app").ToList();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await chromeList.Contains("Google Chrome Helper").Should().BeTrue();
            await chromeList.Contains("Google Chrome Helper (Renderer)").Should().BeTrue();
            await chromeAppList.Contains("Google Chrome Helper").Should().BeTrue();
            await chromeExeList.Contains("Google Chrome Helper").Should().BeTrue();
            await zenList.Contains("plugin-container").Should().BeTrue();
            await zenAppList.Contains("plugin-container").Should().BeTrue();
            await zenAppList.Contains("Zen Browser").Should().BeTrue();
        }
        else
        {
            await chromeList.Contains("Google Chrome").Should().BeTrue();
            await zenList.Contains("zen").Should().BeTrue();
        }
    }
}

using ServiceLib.Common;
using ServiceLib.Handler;
using ServiceLib.Models.Configs;
using ServiceLib.Models.Entities;

namespace ServiceLib.Tests.Handler;

public class BuiltinRoutingTests
{
    [Test]
    public async Task EmbeddedRuRouting_RuBypass_IsValidAndDeserializable()
    {
        var json = EmbedUtils.GetEmbedText(Global.CustomRoutingFileName + "ru_bypass");
        await (!string.IsNullOrEmpty(json)).Should().BeTrue();

        var rules = JsonUtils.Deserialize<List<RulesItem>>(json);
        await (rules != null).Should().BeTrue();
        await (rules!.Count >= 5).Should().BeTrue();

        // Verify key rules
        await rules.Any(r => r.OutboundTag == "direct" && r.Protocol?.Contains("bittorrent") == true).Should().BeTrue();
        await rules.Any(r => r.OutboundTag == "block" && r.Domain?.Contains("geosite:category-ads-all") == true).Should().BeTrue();
        await rules.Any(r => r.OutboundTag == "direct" && r.Ip?.Contains("geoip:ru") == true).Should().BeTrue();
        await rules.Any(r => r.OutboundTag == "proxy" && r.Port == "0-65535").Should().BeTrue();
    }

    [Test]
    public async Task EmbeddedRuRouting_RuBlocked_IsValidAndDeserializable()
    {
        var json = EmbedUtils.GetEmbedText(Global.CustomRoutingFileName + "ru_blocked");
        await (!string.IsNullOrEmpty(json)).Should().BeTrue();

        var rules = JsonUtils.Deserialize<List<RulesItem>>(json);
        await (rules != null).Should().BeTrue();
        await (rules!.Count >= 5).Should().BeTrue();

        await rules.Any(r => r.OutboundTag == "proxy" && r.Domain?.Contains("geosite:ru-blocked") == true).Should().BeTrue();
        await rules.Any(r => r.OutboundTag == "proxy" && r.Ip?.Contains("geoip:ru-blocked") == true).Should().BeTrue();
        await rules.Any(r => r.OutboundTag == "direct" && r.Port == "0-65535").Should().BeTrue();
    }

    [Test]
    public async Task EmbeddedRuRouting_RuAll_IsValidAndDeserializable()
    {
        var json = EmbedUtils.GetEmbedText(Global.CustomRoutingFileName + "ru_all");
        await (!string.IsNullOrEmpty(json)).Should().BeTrue();

        var rules = JsonUtils.Deserialize<List<RulesItem>>(json);
        await (rules != null).Should().BeTrue();
        await (rules!.Count >= 3).Should().BeTrue();

        await rules.Any(r => r.OutboundTag == "block" && r.Domain?.Contains("geosite:category-ads-all") == true).Should().BeTrue();
        await rules.Any(r => r.OutboundTag == "proxy" && r.Port == "0-65535").Should().BeTrue();
    }

    [Test]
    public async Task LoadConfig_InitializesRussiaPresetsByDefault()
    {
        var config = ConfigHandler.LoadConfig();
        await (config != null).Should().BeTrue();
        await (config!.ConstItem.GeoSourceUrl == Global.GeoFilesSources[1]).Should().BeTrue();
        await (config.ConstItem.SrsSourceUrl == Global.SingboxRulesetSources[1]).Should().BeTrue();
        await (config.ConstItem.RouteRulesTemplateSourceUrl == Global.RoutingRulesSources[1]).Should().BeTrue();
    }
}

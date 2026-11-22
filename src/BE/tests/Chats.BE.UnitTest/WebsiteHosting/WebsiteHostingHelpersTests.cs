using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Chats.BE.Services.WebsiteHosting;
using Chats.BE.Services.WebsiteHosting.Dns;
using Chats.BE.Services.WebsiteHosting.Gateways;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.WebsiteHosting;

public sealed class WebsiteHostingHelpersTests
{
    [Theory]
    [InlineData("blog", null)]
    [InlineData("a", null)]
    [InlineData("my-site-01", null)]
    [InlineData("", "required")]
    [InlineData("-blog", "hyphen")]
    [InlineData("blog-", "hyphen")]
    [InlineData("my_blog", "hyphen")]
    [InlineData("a.b", "hyphen")]
    [InlineData("xn--abc", "punycode")]
    [InlineData("www", "reserved")]
    [InlineData("admin", "reserved")]
    public void ValidateSubdomain(string subdomain, string? expectedErrorFragment)
    {
        string? error = SiteNaming.ValidateSubdomain(subdomain, "sites.example.com");
        if (expectedErrorFragment == null) Assert.Null(error);
        else Assert.Contains(expectedErrorFragment, error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSubdomain_RejectsTooLongLabelAndHostname()
    {
        Assert.NotNull(SiteNaming.ValidateSubdomain(new string('a', 64), "example.com"));
        Assert.NotNull(SiteNaming.ValidateSubdomain(new string('a', 63), string.Join(".", Enumerable.Repeat(new string('b', 60), 4))));
    }

    [Theory]
    [InlineData(" Blog ", "sites.example.com", "blog")]
    [InlineData("Blog.Sites.Example.com.", "sites.example.com", "blog")]
    [InlineData("blog.other.com", "sites.example.com", "blog.other.com")]
    public void Normalize(string input, string domain, string expected)
        => Assert.Equal(expected, SiteNaming.Normalize(input, domain));

    [Theory]
    [InlineData("https", 443, "https://a.example.com")]
    [InlineData("http", 80, "http://a.example.com")]
    [InlineData("https", 8443, "https://a.example.com:8443")]
    [InlineData("http", 443, "http://a.example.com:443")]
    [InlineData("HTTPS", 443, "https://a.example.com")]
    public void BuildPublicUrl(string scheme, int port, string expected)
        => Assert.Equal(expected, SiteNaming.BuildPublicUrl(scheme, "a.example.com", port));

    [Fact]
    public void GetRelativeName_AndZoneChecks()
    {
        Assert.Equal("t1.chats-test", SiteNaming.GetRelativeName("t1.chats-test.sdcb.ai", "sdcb.ai"));
        Assert.Equal("@", SiteNaming.GetRelativeName("sdcb.ai", "sdcb.ai"));
        Assert.Throws<WebsiteHostingException>(() => SiteNaming.GetRelativeName("t1.example.com", "sdcb.ai"));
        Assert.True(SiteNaming.IsDomainInsideZone("chats-test.sdcb.ai", "sdcb.ai"));
        Assert.False(SiteNaming.IsDomainInsideZone("notsdcb.ai", "sdcb.ai"));
        Assert.True(SiteNaming.IsValidHostname("chats-test.sdcb.ai"));
        Assert.False(SiteNaming.IsValidHostname("localhost"));
    }

    [Fact]
    public void DnsPlanner_UnmanagedAddressRecord_IsConflict()
    {
        DnsRecordInfo[] existing = [new("1", "A", "a.example.com", "1.2.3.4", 300, null, "someone else")];
        DnsPlan plan = DnsRecordPlanner.Plan(existing, [new("A", "5.6.7.8")], DnsMarker.For(1));
        Assert.True(plan.HasConflicts);
        Assert.Empty(plan.Ops);
    }

    [Fact]
    public void DnsPlanner_UnrelatedTxtRecord_IsIgnoredForAddressModeButConflictsWithCname()
    {
        DnsRecordInfo[] existing = [new("1", "TXT", "a.example.com", "v=spf1", 300, null, null)];
        DnsPlan address = DnsRecordPlanner.Plan(existing, [new("A", "5.6.7.8")], DnsMarker.For(1));
        Assert.False(address.HasConflicts);
        Assert.Equal(DnsPlanOpKind.Create, Assert.Single(address.Ops).Kind);

        DnsPlan cname = DnsRecordPlanner.Plan(existing, [new("CNAME", "gw.example.com")], DnsMarker.For(1));
        Assert.True(cname.HasConflicts);
    }

    [Fact]
    public void DnsPlanner_ManagedRecords_AreKeptUpdatedOrDeleted()
    {
        string marker = DnsMarker.For(2);
        DnsRecordInfo[] existing =
        [
            new("a", "A", "x.example.com", "1.1.1.1", 300, null, marker),
            new("b", "AAAA", "x.example.com", "::1", 300, null, DnsMarker.For(1)),
            new("c", "CNAME", "x.example.com", "old.example.com", 300, null, DnsMarker.For(1)),
        ];
        DnsPlan plan = DnsRecordPlanner.Plan(existing, [new("A", "1.1.1.1"), new("AAAA", "2001:db8::1")], marker);

        Assert.False(plan.HasConflicts);
        Assert.Collection(plan.Ops,
            op => Assert.Equal((DnsPlanOpKind.Keep, "a"), (op.Kind, op.RecordId)),
            op => Assert.Equal((DnsPlanOpKind.Update, "b"), (op.Kind, op.RecordId)),
            op => Assert.Equal((DnsPlanOpKind.Delete, "c"), (op.Kind, op.RecordId)));
    }

    [Fact]
    public void DnsPlanner_BuildDesired()
    {
        Assert.Equal([new DesiredDnsRecord("A", "1.2.3.4"), new DesiredDnsRecord("AAAA", "2001:db8::1")],
            DnsRecordPlanner.BuildDesired((byte)DBHostedDomainRecordMode.Address, "1.2.3.4", "2001:db8::1", "ignored.example.com"));
        Assert.Equal([new DesiredDnsRecord("CNAME", "gw.example.com")],
            DnsRecordPlanner.BuildDesired((byte)DBHostedDomainRecordMode.Cname, "1.2.3.4", null, "gw.example.com."));
        Assert.Throws<WebsiteHostingException>(() => DnsRecordPlanner.BuildDesired((byte)DBHostedDomainRecordMode.Address, null, null, "gw.example.com"));
        Assert.Throws<WebsiteHostingException>(() => DnsRecordPlanner.BuildDesired((byte)DBHostedDomainRecordMode.Cname, "1.2.3.4", null, null));
    }

    [Fact]
    public void DnsMarker_Recognition()
    {
        Assert.Equal("managed-by:chats site:42", DnsMarker.For(42));
        Assert.True(DnsMarker.IsManaged("managed-by:chats site:7"));
        Assert.False(DnsMarker.IsManaged(null));
        Assert.False(DnsMarker.IsManaged("manual record"));
    }

    [Fact]
    public void AliyunRemark_UsesAllowedCharactersAndRoundTrips()
    {
        string encoded = AliyunDnsProvider.EncodeRemark(DnsMarker.For(42));
        Assert.Equal("managed-by-chats.site-42", encoded);
        Assert.Matches("^[A-Za-z0-9_\\-,.]{1,50}$", encoded);
        Assert.True(DnsMarker.IsManaged(AliyunDnsProvider.DecodeRemark(encoded)));
        Assert.Equal("manual", AliyunDnsProvider.DecodeRemark("manual"));
    }

    [Fact]
    public void AliyunSigner_MatchesDocumentedExample()
    {
        Dictionary<string, string> p = new()
        {
            ["Format"] = "XML",
            ["AccessKeyId"] = "testid",
            ["Action"] = "DescribeRegions",
            ["SignatureMethod"] = "HMAC-SHA1",
            ["SignatureNonce"] = "3ee8c1b8-83d3-44af-a94f-4e0ad82fd6cf",
            ["SignatureVersion"] = "1.0",
            ["Timestamp"] = "2016-02-23T12:46:24Z",
            ["Version"] = "2014-05-26",
        };
        Assert.Equal(
            "GET&%2F&AccessKeyId%3Dtestid%26Action%3DDescribeRegions%26Format%3DXML%26SignatureMethod%3DHMAC-SHA1%26SignatureNonce%3D3ee8c1b8-83d3-44af-a94f-4e0ad82fd6cf%26SignatureVersion%3D1.0%26Timestamp%3D2016-02-23T12%253A46%253A24Z%26Version%3D2014-05-26",
            AliyunRpcSigner.BuildStringToSign(p));
        Assert.Equal("OLeaidS1JvxuMvnyHOwuJ+uX5qY=", AliyunRpcSigner.ComputeSignature(p, "testsecret"));
    }

    [Fact]
    public void AliyunSigner_PercentEncode()
    {
        Assert.Equal("a%20b%2A~%2F%E4%B8%AD", AliyunRpcSigner.PercentEncode("a b*~/中"));
    }

    [Fact]
    public void CaddyRoute_JsonShape()
    {
        JsonObject route = CaddyRouteBuilder.BuildRoute("chats-site-5", "a.example.com", "10.0.0.2:41000");
        Assert.Equal(
            """{"@id":"chats-site-5","match":[{"host":["a.example.com"]}],"handle":[{"handler":"reverse_proxy","upstreams":[{"dial":"10.0.0.2:41000"}]}],"terminal":true}""",
            route.ToJsonString());
    }

    [Fact]
    public void CaddyRoute_IdsAndParsing()
    {
        Assert.Equal("chats-site-5", CaddyRouteBuilder.RouteId(5, "srv0", false));
        Assert.Equal("chats-site-5-srv1", CaddyRouteBuilder.RouteId(5, "srv1", true));
        Assert.Equal(5, CaddyRouteBuilder.TryParseSiteId("chats-site-5-srv1"));
        Assert.Null(CaddyRouteBuilder.TryParseSiteId("other-5"));
        Assert.Equal(["srv0", "srv1"], CaddyWebGateway.ParseServerNames(" srv0, srv1;"));

        string routes = """
            [{"@id":"chats-site-5","match":[{"host":["a.example.com"]}],"handle":[{"handler":"reverse_proxy","upstreams":[{"dial":"h:1"}]}]},
             {"@id":"manual","match":[{"host":["b.example.com"]}]},
             {"handle":[{"handler":"static_response"}]}]
            """;
        GatewayRouteInfo info = Assert.Single(CaddyWebGateway.ParseManagedRoutes("srv0", routes));
        Assert.Equal(("chats-site-5", "srv0", (long?)5), (info.RouteId, info.Server, info.SiteId));
        Assert.Equal(["a.example.com"], info.Hosts);
        Assert.Equal(["h:1"], info.Dials);
        Assert.Empty(CaddyWebGateway.ParseManagedRoutes("srv0", "null"));
    }

    [Fact]
    public void CredentialProtector_RoundTripAndPlaintextPassthrough()
    {
        WebsiteCredentialProtector p = new("secret-password");
        string stored = p.Protect("""{"apiToken":"abc"}""");
        Assert.StartsWith(WebsiteCredentialProtector.Prefix, stored);
        Assert.DoesNotContain("abc", stored);
        Assert.Equal("""{"apiToken":"abc"}""", p.Unprotect(stored));
        Assert.Equal("plain", p.Unprotect("plain"));
        Assert.ThrowsAny<CryptographicException>(() => new WebsiteCredentialProtector("other").Unprotect(stored));
    }

    [Fact]
    public void CloudflareTtl_Normalization()
    {
        Assert.Equal(1, CloudflareDnsProvider.NormalizeTtl(600, proxied: true));
        Assert.Equal(1, CloudflareDnsProvider.NormalizeTtl(1, proxied: false));
        Assert.Equal(60, CloudflareDnsProvider.NormalizeTtl(30, proxied: false));
        Assert.Equal(600, CloudflareDnsProvider.NormalizeTtl(600, proxied: false));
    }

    [Fact]
    public void WebsiteTools_ResolveContainer()
    {
        Chats.DB.ContainerResource[] containers =
        [
            new() { Id = 1, Name = "web", BackendResourceId = "abcdef0123456789aaaa", Image = "x" },
            new() { Id = 2, Name = "db", BackendResourceId = "abcdef0123456789bbbb", Image = "x" },
        ];
        Assert.Equal(1, WebsiteTools.ResolveContainer(containers, "WEB")!.Id);
        Assert.Equal(2, WebsiteTools.ResolveContainer(containers, "abcdef0123456789bbbb")!.Id);
        Assert.Equal(2, WebsiteTools.ResolveContainer(containers, "abcdef0123456789b")!.Id);
        Assert.Null(WebsiteTools.ResolveContainer(containers, "abcdef012345"));
        Assert.Null(WebsiteTools.ResolveContainer(containers, "abc"));
    }
}

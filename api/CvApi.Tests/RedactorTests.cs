using System.Text.Json.Nodes;
using CvApi.Redaction;
using CvApi.Tenants;

namespace CvApi.Tests;

public sealed class RedactorTests
{
    private static JsonObject Master() => JsonNode.Parse("""
        {
          "profile": { "name": "A", "photoUrl": "/p.jpg", "photoUrlLarge": "/p-large.jpg" },
          "details": { "email": "a@b.c", "phone": "1", "birthDate": "1990-01-01", "location": "X",
                       "fieldRequires": { "location": ["geo"] } },
          "experiences": [
            { "id": 1, "company": "C", "startDate": "2020-03-15", "endDate": "2021-07-31", "images": ["/i.png"],
              "projects": [ { "name": "inner", "startDate": "2020-05-05" } ] },
            { "id": 2, "company": "D", "requires": ["private", "other"], "startDate": "2019" }
          ],
          "projects": [ { "name": "P", "client": "Client", "clientAlias": "Bank", "screenshots": ["/s.png"] } ]
        }
        """)!.AsObject();

    private static JsonObject Redact(AccessPolicy profile, AccessPolicy? overrides = null) =>
        CvRedactor.Redact(Master(), EffectivePolicy.From(profile, overrides));

    [Fact]
    public void Invite_revision_replaces_profile_pin_and_empty_unpins()
    {
        var profile = new AccessPolicy { Revision = "ABCDEF1" };
        Assert.Equal("abcdef1", EffectivePolicy.From(profile).Revision);
        Assert.Equal("1234567", EffectivePolicy.From(profile, new AccessPolicy { Revision = "1234567" }).Revision);
        Assert.Null(EffectivePolicy.From(profile, new AccessPolicy { Revision = "" }).Revision);
        Assert.Null(EffectivePolicy.From(new AccessPolicy()).Revision);
    }

    [Fact]
    public void Hide_by_default_hides_unset_flags_but_keeps_explicit_ones()
    {
        var policy = EffectivePolicy.From(
            new AccessPolicy { Flags = new RedactionFlags { HidePhoto = false } },
            new AccessPolicy { Flags = new RedactionFlags { HideMedia = false } },
            hideByDefault: true);

        Assert.False(policy.Flags.HidePhoto);
        Assert.False(policy.Flags.HideMedia);
        Assert.True(policy.Flags.HideCompanies);
        Assert.True(policy.Flags.HideTimeframeMonths);
        Assert.True(policy.Flags.HideContactDetails);
        Assert.True(policy.Flags.HideBirthDate);
        Assert.False(EffectivePolicy.From(new AccessPolicy()).Flags.HideCompanies);
    }

    [Fact]
    public void Requires_and_field_requires_are_enforced_and_stripped()
    {
        var cv = Redact(new AccessPolicy());
        Assert.Single(cv["experiences"]!.AsArray());
        Assert.False(cv["details"]!.AsObject().ContainsKey("location"));
        Assert.False(cv["details"]!.AsObject().ContainsKey("fieldRequires"));

        var granted = Redact(new AccessPolicy { Grants = ["OTHER", "geo"] });
        Assert.Equal(2, granted["experiences"]!.AsArray().Count);
        Assert.Equal("X", granted["details"]!["location"]!.GetValue<string>());
        Assert.DoesNotContain("requires", granted.ToJsonString());
    }

    [Fact]
    public void Hidden_fields_apply_to_array_elements_and_whole_sections()
    {
        var cv = Redact(new AccessPolicy { HiddenFields = ["experiences.company", "projects", "details.phone"] });
        Assert.Null(cv["experiences"]![0]!["company"]);
        Assert.False(cv.ContainsKey("projects"));
        Assert.False(cv["details"]!.AsObject().ContainsKey("phone"));
    }

    [Theory]
    [InlineData(false, true, "2020-03", "2020-05")]
    [InlineData(true, false, "2020", "2020")]
    [InlineData(true, true, "2020", "2020")]
    public void Timeframe_flags_reduce_date_precision_everywhere(bool hideMonths, bool hideDays, string start, string nested)
    {
        var cv = Redact(new AccessPolicy { Flags = new RedactionFlags { HideTimeframeMonths = hideMonths, HideTimeframeDays = hideDays } });
        var exp = cv["experiences"]![0]!;
        Assert.Equal(start, exp["startDate"]!.GetValue<string>());
        Assert.Equal(nested, exp["projects"]![0]!["startDate"]!.GetValue<string>());
        Assert.Equal("1990-01-01", cv["details"]!["birthDate"]!.GetValue<string>()); // not a timeframe
    }

    [Fact]
    public void Hide_companies_uses_aliases_and_removes_company_media()
    {
        var cv = Redact(new AccessPolicy { Flags = new RedactionFlags { HideCompanies = true } });
        Assert.Null(cv["experiences"]![0]!["company"]);
        Assert.Null(cv["experiences"]![0]!["images"]);
        Assert.Equal("Bank", cv["projects"]![0]!["client"]!.GetValue<string>());
        Assert.DoesNotContain("Client", cv.ToJsonString());
    }

    [Fact]
    public void Personal_flags_remove_photo_contact_birthdate_and_media()
    {
        var cv = Redact(new AccessPolicy
        {
            Flags = new RedactionFlags { HidePhoto = true, HideContactDetails = true, HideBirthDate = true, HideMedia = true },
        });
        var json = cv.ToJsonString();
        Assert.DoesNotContain("photoUrl", json);
        Assert.DoesNotContain("a@b.c", json);
        Assert.DoesNotContain("\"phone\"", json);
        Assert.DoesNotContain("1990", json);
        Assert.DoesNotContain("/s.png", json);
        Assert.DoesNotContain("/i.png", json);
    }

    [Fact]
    public void Invite_overrides_replace_flags_add_hidden_fields_and_replace_grants()
    {
        var profile = new AccessPolicy
        {
            Grants = ["private"],
            Flags = new RedactionFlags { HideCompanies = true, HidePhoto = true },
            HiddenFields = ["details.phone"],
        };
        var overrides = new AccessPolicy
        {
            Grants = [],
            Flags = new RedactionFlags { HideCompanies = false },
            HiddenFields = ["details.email"],
        };
        var policy = EffectivePolicy.From(profile, overrides);

        Assert.Empty(policy.Grants);
        Assert.False(policy.Flags.HideCompanies);
        Assert.True(policy.Flags.HidePhoto);
        Assert.Equal(["details.phone", "details.email"], policy.HiddenFields);
    }

    [Fact]
    public void Master_is_not_modified()
    {
        var master = Master();
        var before = master.ToJsonString();
        CvRedactor.Redact(master, EffectivePolicy.From(new AccessPolicy { Flags = new RedactionFlags { HideMedia = true } }));
        Assert.Equal(before, master.ToJsonString());
    }
}

using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using CvApi.Import;

namespace CvApi.Tests;

public sealed class LinkedInImporterTests
{
    private static MemoryStream Zip(params (string Name, string Content)[] files)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(true));
                writer.Write(content);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static LinkedInImportResult Import(params (string, string)[] files) => LinkedInImporter.Import(Zip(files));

    private static string? Str(JsonNode? node) => node?.GetValue<string>();

    private const string Positions = """
        Company Name,Title,Description,Location,Started On,Finished On
        "Acme, Inc.",Senior Engineer,"Built ""things"",
        and more things",Berlin,Mar 2020,
        Beta GmbH,Developer,,Vienna,2017,Feb 2020
        """;

    [Fact]
    public void Positions_with_quoted_fields_dates_and_ongoing_job()
    {
        var result = Import(("Positions.csv", Positions));
        var experiences = result.Cv["experiences"]!.AsArray();

        Assert.Equal(2, result.Experiences);
        var acme = experiences[0]!.AsObject();
        Assert.Equal(1, acme["id"]!.GetValue<int>());
        Assert.Equal("Acme, Inc.", Str(acme["company"]));
        Assert.Equal("Senior Engineer", Str(acme["position"]));
        Assert.Equal("Built \"things\",\nand more things", Str(acme["description"])!.Replace("\r\n", "\n"));
        Assert.Equal("2020-03", Str(acme["startDate"]));
        Assert.True(acme.ContainsKey("endDate"));
        Assert.Null(acme["endDate"]);   // ongoing, like current jobs in the sample data

        var beta = experiences[1]!.AsObject();
        Assert.Equal("2017", Str(beta["startDate"]));
        Assert.Equal("2020-02", Str(beta["endDate"]));
        Assert.False(beta.ContainsKey("description"));
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("Mar 2020", "2020-03")]
    [InlineData("March 2020", "2020-03")]
    [InlineData("Dec 1999", "1999-12")]
    [InlineData("2020", "2020")]
    [InlineData("2020-03", "2020-03")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void Linkedin_dates_map_to_iso_with_their_precision(string input, string? expected)
    {
        var csv = $"Company Name,Title,Started On,Finished On\nX,Y,\"{input}\",2024\n";
        var item = Import(("Positions.csv", csv)).Cv["experiences"]![0]!.AsObject();
        if (expected == null) Assert.False(item.ContainsKey("startDate"));
        else Assert.Equal(expected, Str(item["startDate"]));
    }

    [Fact]
    public void Unknown_date_format_is_reported_and_not_treated_as_ongoing()
    {
        var result = Import(("Positions.csv", "Company Name,Title,Started On,Finished On\nX,Y,soon,whenever\n"));
        var item = result.Cv["experiences"]![0]!.AsObject();
        Assert.False(item.ContainsKey("startDate"));
        Assert.False(item.ContainsKey("endDate"));
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public void Preamble_before_header_is_skipped()
    {
        const string skills = """
            Notes:
            "This file has a preamble, with commas,
            and a line break"

            Name
            C#
            Kubernetes
            c#
            """;
        var result = Import(("Skills.csv", skills));
        Assert.Equal(2, result.Skills);
        var skilled = result.Cv["skills"]!["skilled"]!.AsArray().Select(Str).ToList();
        Assert.Equal(["C#", "Kubernetes"], skilled);
    }

    [Fact]
    public void Files_in_a_subfolder_with_any_case_are_found_and_unknown_files_ignored()
    {
        var result = Import(
            ("Basic_LinkedInDataExport_10-01-2026/POSITIONS.CSV", Positions),
            ("Basic_LinkedInDataExport_10-01-2026/Connections.csv", "Notes:\n\"x\"\n\nFirst Name,Last Name\nA,B\n"),
            ("other/Profile.csv", "First Name,Last Name,Headline\nJane,Doe,Engineer\n"));

        Assert.Equal(2, result.Experiences);
        Assert.Equal("Jane Doe", Str(result.Cv["profile"]!["name"]));
        Assert.Equal("Engineer", Str(result.Cv["profile"]!["title"]));
        Assert.Equal(2, result.FilesRead.Count);
    }

    [Fact]
    public void Missing_files_leave_sections_empty_without_inventing_data()
    {
        var result = Import(("Skills.csv", "Name\nGo\n"));
        var cv = result.Cv;
        Assert.False(cv.ContainsKey("profile"));
        Assert.False(cv.ContainsKey("details"));
        Assert.False(cv.ContainsKey("languages"));
        Assert.Empty(cv["experiences"]!.AsArray());
        Assert.Empty(cv["studies"]!.AsArray());
        Assert.Empty(cv["projects"]!.AsArray());
        Assert.Empty(cv["otherEntries"]!.AsArray());
        Assert.Equal(0, result.Experiences);
        Assert.Equal(1, result.Skills);
    }

    [Fact]
    public void Full_export_maps_to_cv_schema()
    {
        var result = Import(
            ("Profile.csv", "First Name,Last Name,Maiden Name,Address,Birth Date,Headline,Summary,Industry,Zip Code,Geo Location,Twitter Handles,Websites,Instant Messengers\r\n" +
                            "Jane,Doe,,,\"Jan 15, 1990\",Software Architect,\"Hello,\r\nworld\",IT,,\"Berlin, Germany\",,,\r\n"),
            ("Email Addresses.csv", "Email Address,Confirmed,Primary,Updated On\nold@example.com,Yes,No,\njane@example.com,Yes,Yes,\n"),
            ("PhoneNumbers.csv", "Extension,Number,Type\n,+49 123,Mobile\n"),
            ("Education.csv", "School Name,Start Date,End Date,Notes,Degree Name,Activities\nTU Berlin,2012,2015,Distributed systems,BSc Computer Science,Chess club\nNew School,2025,,,,\n"),
            ("Languages.csv", "Name,Proficiency\nEnglish,Full professional proficiency\nGerman,Native or bilingual proficiency\n"),
            ("Projects.csv", "Title,Description,Url,Started On,Finished On\nCV Site,Personal CV,https://example.com,Jan 2024,Mar 2024\n"),
            ("Certifications.csv", "Name,Url,Authority,Started On,Finished On,License Number\nCKA,https://cncf.io,CNCF,Jun 2023,,ABC-1\n"));

        var cv = result.Cv;
        Assert.Equal("Jane Doe", Str(cv["profile"]!["name"]));
        Assert.Equal("Software Architect", Str(cv["profile"]!["title"]));
        Assert.Equal("Berlin, Germany", Str(cv["details"]!["location"]));
        Assert.Equal("1990-01-15", Str(cv["details"]!["birthDate"]));
        Assert.Equal("jane@example.com", Str(cv["details"]!["email"]));   // primary wins
        Assert.Equal("+49 123", Str(cv["details"]!["phone"]));
        Assert.Equal("Hello,\r\nworld", Str(cv["intro"]!["text"]));

        var study = cv["studies"]![0]!.AsObject();
        Assert.Equal("TU Berlin", Str(study["institution"]));
        Assert.Equal("BSc Computer Science", Str(study["degree"]));
        Assert.Equal("2012", Str(study["startDate"]));
        Assert.Equal("2015", Str(study["endDate"]));
        Assert.Equal("Distributed systems\n\nChess club", Str(study["focus"]));
        Assert.Null(cv["studies"]![1]!["endDate"]);   // ongoing
        Assert.Equal(2, result.Studies);

        var languages = cv["languages"]!.AsArray();
        Assert.Equal("English", Str(languages[0]!["name"]));
        Assert.Equal("Full professional proficiency", Str(languages[0]!["level"]));
        Assert.NotNull(languages[0]!["code"]);

        var project = cv["projects"]![0]!.AsObject();
        Assert.Equal("CV Site", Str(project["name"]));
        Assert.Equal("Personal CV\n\nhttps://example.com", Str(project["description"]));
        Assert.Equal("2024-01", Str(project["startDate"]));
        Assert.Equal("2024-03", Str(project["endDate"]));

        var cert = cv["otherEntries"]![0]!.AsObject();
        Assert.Equal("CKA", Str(cert["title"]));
        Assert.Equal("CNCF", Str(cert["institution"]));
        Assert.Equal("2023-06", Str(cert["startDate"]));
        Assert.Equal("2023-06", Str(cert["endDate"]));   // no expiry: issue date only, not "present"
        Assert.Equal("License: ABC-1\nhttps://cncf.io", Str(cert["description"]));
        Assert.True(cert["showPeriod"]!.GetValue<bool>());

        Assert.Equal((0, 2, 0, 2, 1, 1),
            (result.Experiences, result.Studies, result.Skills, result.Languages, result.Projects, result.Certifications));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Birth_date_without_year_is_not_imported()
    {
        var result = Import(("Profile.csv", "First Name,Last Name,Birth Date\nJane,Doe,Jan 15\n"));
        Assert.False(result.Cv["details"]?.AsObject().ContainsKey("birthDate") ?? false);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Not_a_zip_is_rejected()
    {
        var ex = Assert.Throws<LinkedInImportException>(() =>
            LinkedInImporter.Import(new MemoryStream(Encoding.UTF8.GetBytes("First Name,Last Name\nJane,Doe\n"))));
        Assert.Contains("not a valid ZIP", ex.Message);
        Assert.Throws<LinkedInImportException>(() => LinkedInImporter.Import(new MemoryStream()));
    }

    [Fact]
    public void Empty_zip_or_zip_without_linkedin_files_is_rejected()
    {
        var ex = Assert.Throws<LinkedInImportException>(() => Import());
        Assert.Contains("no recognised LinkedIn", ex.Message);
        Assert.Throws<LinkedInImportException>(() => Import(("readme.txt", "hi"), ("Connections.csv", "First Name\nA\n")));
    }

    [Fact]
    public void Oversized_entry_is_skipped_with_warning()
    {
        var big = "Name\n" + new string('a', (int)LinkedInImporter.MaxEntryBytes + 10) + "\n";
        var result = Import(("Skills.csv", big), ("Positions.csv", Positions));
        Assert.Equal(0, result.Skills);
        Assert.Equal(2, result.Experiences);
        Assert.Contains(result.Warnings, w => w.Contains("Skills.csv") && w.Contains("MB"));

        Assert.Throws<LinkedInImportException>(() => Import(("Skills.csv", big)));
    }

    [Fact]
    public void Header_row_missing_is_reported()
    {
        var result = Import(("Skills.csv", "Something,Else\n1,2\n"), ("Positions.csv", Positions));
        Assert.Contains(result.Warnings, w => w.Contains("Skills.csv") && w.Contains("header"));
    }
}

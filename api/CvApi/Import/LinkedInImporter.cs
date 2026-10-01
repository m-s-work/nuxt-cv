using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CvApi.Import;

/// <summary>Result of <see cref="LinkedInImporter.Import"/>: the CV JSON (cv.&lt;locale&gt;.json schema) plus diagnostics.</summary>
public sealed record LinkedInImportResult(
    JsonObject Cv,
    List<string> Warnings,
    int Experiences,
    int Studies,
    int Skills,
    int Languages,
    int Projects,
    int Certifications)
{
    /// <summary>LinkedIn files that were recognised and read (file names as found in the ZIP).</summary>
    public List<string> FilesRead { get; init; } = [];
}

/// <summary>The upload is not a ZIP or contains no recognised LinkedIn export file.</summary>
public sealed class LinkedInImportException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Converts LinkedIn's "Get a copy of your data" ZIP (CSV files) into the app's CV JSON format
/// (see api/sample-data/tenants/*/cv.en.json and src/app/composables/useCv.ts).
/// Only known CSV file names are read (any folder, case-insensitive); entry sizes and counts are capped.
/// Nothing is invented: missing values are left out.
/// </summary>
public static partial class LinkedInImporter
{
    /// <summary>Maximum uncompressed size of one CSV entry.</summary>
    public const long MaxEntryBytes = 5 * 1024 * 1024;

    /// <summary>Maximum number of ZIP entries inspected (directory listing, not decompressed).</summary>
    public const int MaxEntriesScanned = 5000;

    /// <summary>Maximum total uncompressed bytes read from all recognised entries.</summary>
    public const long MaxTotalBytes = 25 * 1024 * 1024;

    private const string ProfileFile = "Profile.csv";
    private const string PositionsFile = "Positions.csv";
    private const string EducationFile = "Education.csv";
    private const string SkillsFile = "Skills.csv";
    private const string LanguagesFile = "Languages.csv";
    private const string CertificationsFile = "Certifications.csv";
    private const string ProjectsFile = "Projects.csv";
    private const string EmailsFile = "Email Addresses.csv";
    private const string PhonesFile = "PhoneNumbers.csv";

    /// <summary>Known files and a column that identifies their header row (used to skip "Notes:" preambles).</summary>
    private static readonly Dictionary<string, string> KnownFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        [ProfileFile] = "First Name",
        [PositionsFile] = "Company Name",
        [EducationFile] = "School Name",
        [SkillsFile] = "Name",
        [LanguagesFile] = "Name",
        [CertificationsFile] = "Name",
        [ProjectsFile] = "Title",
        [EmailsFile] = "Email Address",
        [PhonesFile] = "Number",
    };

    public static LinkedInImportResult Import(Stream zip)
    {
        ArgumentNullException.ThrowIfNull(zip);
        var warnings = new List<string>();
        var tables = ReadTables(zip, warnings);
        if (tables.Count == 0)
            throw new LinkedInImportException(
                "The ZIP contains no recognised LinkedIn export file (expected e.g. Profile.csv, Positions.csv, Education.csv, Skills.csv).");

        var cv = new JsonObject();

        // profile / details / intro
        var profileRow = Table(tables, ProfileFile)?.FirstOrDefault();
        var profile = new JsonObject();
        var details = new JsonObject();
        if (profileRow != null)
        {
            var name = string.Join(' ', new[] { profileRow.Get("First Name"), profileRow.Get("Last Name") }.Where(s => s != null));
            SetIf(profile, "name", name);
            SetIf(profile, "title", profileRow.Get("Headline"));
            SetIf(details, "location", profileRow.Get("Geo Location") ?? profileRow.Get("Address"));
            if (profileRow.Get("Birth Date") is { } birth)
            {
                if (ParseBirthDate(birth) is { } iso) details["birthDate"] = iso;
                else warnings.Add($"{ProfileFile}: birth date \"{birth}\" has no year or an unknown format and was not imported.");
            }
            if (profileRow.Get("Summary") is { } summary) cv["intro"] = new JsonObject { ["text"] = summary };
        }

        var emails = Table(tables, EmailsFile) ?? [];
        var email = emails.FirstOrDefault(r => IsYes(r.Get("Primary")) && r.Get("Email Address") != null)
                    ?? emails.FirstOrDefault(r => r.Get("Email Address") != null);
        SetIf(details, "email", email?.Get("Email Address"));

        var phone = (Table(tables, PhonesFile) ?? []).FirstOrDefault(r => r.Get("Number") != null);
        if (phone != null)
        {
            var number = phone.Get("Number")!;
            if (phone.Get("Extension") is { } ext) number += " ext. " + ext;
            details["phone"] = number;
        }

        if (profile.Count > 0) cv["profile"] = profile;
        if (details.Count > 0) cv["details"] = details;
        // Keep "intro" after profile/details for readability.
        if (cv["intro"] is JsonObject intro) { cv.Remove("intro"); cv["intro"] = intro; }

        // skills: LinkedIn has a single flat list -> "skilled"
        var skills = (Table(tables, SkillsFile) ?? [])
            .Select(r => r.Get("Name")).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (skills.Count > 0)
            cv["skills"] = new JsonObject { ["skilled"] = new JsonArray(skills.Select(s => (JsonNode?)s).ToArray()) };

        // languages
        var languages = new JsonArray();
        foreach (var row in Table(tables, LanguagesFile) ?? [])
        {
            if (row.Get("Name") is not { } lang) continue;
            languages.Add(new JsonObject
            {
                ["name"] = lang,
                ["level"] = row.Get("Proficiency") ?? "",
                ["code"] = LanguageCode(lang),
            });
        }
        if (languages.Count > 0) cv["languages"] = languages;

        // experiences
        var experiences = new JsonArray();
        foreach (var row in Table(tables, PositionsFile) ?? [])
        {
            if (row.Get("Company Name") == null && row.Get("Title") == null) continue;
            var item = new JsonObject { ["id"] = experiences.Count + 1 };
            SetIf(item, "company", row.Get("Company Name"));
            SetIf(item, "position", row.Get("Title"));
            AddDates(item, row, "Started On", "Finished On", PositionsFile, warnings);
            SetIf(item, "description", row.Get("Description"));
            experiences.Add(item);
        }
        cv["experiences"] = experiences;

        // studies
        var studies = new JsonArray();
        foreach (var row in Table(tables, EducationFile) ?? [])
        {
            if (row.Get("School Name") == null && row.Get("Degree Name") == null) continue;
            var item = new JsonObject { ["id"] = studies.Count + 1 };
            SetIf(item, "institution", row.Get("School Name"));
            SetIf(item, "degree", row.Get("Degree Name"));
            AddDates(item, row, "Start Date", "End Date", EducationFile, warnings);
            var focus = string.Join("\n\n", new[] { row.Get("Notes"), row.Get("Activities") }.Where(s => s != null));
            SetIf(item, "focus", focus);
            studies.Add(item);
        }
        cv["studies"] = studies;

        // projects
        var projects = new JsonArray();
        foreach (var row in Table(tables, ProjectsFile) ?? [])
        {
            if (row.Get("Title") == null) continue;
            var item = new JsonObject { ["id"] = projects.Count + 1, ["name"] = row.Get("Title") };
            var description = string.Join("\n\n", new[] { row.Get("Description"), row.Get("Url") }.Where(s => s != null));
            SetIf(item, "description", description);
            AddDates(item, row, "Started On", "Finished On", ProjectsFile, warnings);
            projects.Add(item);
        }
        cv["projects"] = projects;

        // certifications -> otherEntries
        var others = new JsonArray();
        foreach (var row in Table(tables, CertificationsFile) ?? [])
        {
            if (row.Get("Name") == null) continue;
            var item = new JsonObject { ["id"] = others.Count + 1, ["title"] = row.Get("Name") };
            SetIf(item, "institution", row.Get("Authority"));
            var start = ParseDate(row.Get("Started On"), CertificationsFile, warnings);
            var end = ParseDate(row.Get("Finished On"), CertificationsFile, warnings);
            if (start != null) item["startDate"] = start;
            // A certificate without expiry is not "ongoing": show the issue date only (start == end).
            if (start != null || end != null) item["endDate"] = end ?? start;
            var parts = new List<string>();
            if (row.Get("License Number") is { } license) parts.Add("License: " + license);
            if (row.Get("Url") is { } url) parts.Add(url);
            if (parts.Count > 0) item["description"] = string.Join("\n", parts);
            item["showPeriod"] = start != null || end != null;
            item["icon"] = "award";
            others.Add(item);
        }
        cv["otherEntries"] = others;

        return new LinkedInImportResult(cv, warnings,
            experiences.Count, studies.Count, skills.Count, languages.Count, projects.Count, others.Count)
        {
            FilesRead = tables.Keys.ToList(),
        };
    }

    // ---------------------------------------------------------------- ZIP

    private static Dictionary<string, List<Row>> ReadTables(Stream zip, List<string> warnings)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException or IOException)
        {
            throw new LinkedInImportException("The upload is not a valid ZIP file.", ex);
        }

        var tables = new Dictionary<string, List<Row>>(StringComparer.OrdinalIgnoreCase);
        using (archive)
        {
            long total = 0;
            var scanned = 0;
            IEnumerable<ZipArchiveEntry> entries;
            try { entries = archive.Entries; }
            catch (InvalidDataException ex) { throw new LinkedInImportException("The upload is not a valid ZIP file.", ex); }

            foreach (var entry in entries)
            {
                if (++scanned > MaxEntriesScanned)
                {
                    warnings.Add($"The ZIP has more than {MaxEntriesScanned} entries; the rest was ignored.");
                    break;
                }
                var fileName = entry.FullName.Replace('\\', '/');
                fileName = fileName[(fileName.LastIndexOf('/') + 1)..];
                if (!KnownFiles.TryGetValue(fileName, out var keyColumn)) continue;
                var canonical = KnownFiles.Keys.First(k => k.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                if (tables.ContainsKey(canonical))
                {
                    warnings.Add($"{entry.FullName}: duplicate of {canonical}, ignored.");
                    continue;
                }
                if (entry.Length > MaxEntryBytes)
                {
                    warnings.Add($"{entry.FullName}: larger than {MaxEntryBytes / (1024 * 1024)} MB, ignored.");
                    continue;
                }
                if (total + entry.Length > MaxTotalBytes)
                {
                    warnings.Add($"{entry.FullName}: total size limit reached, ignored.");
                    continue;
                }

                string text;
                try
                {
                    text = ReadCapped(entry, MaxEntryBytes);
                }
                catch (InvalidDataException ex)
                {
                    warnings.Add($"{entry.FullName}: {ex.Message} Ignored.");
                    continue;
                }
                total += Encoding.UTF8.GetByteCount(text);

                var rows = ParseTable(text, keyColumn);
                if (rows == null)
                {
                    warnings.Add($"{entry.FullName}: no header row with \"{keyColumn}\" found, ignored.");
                    continue;
                }
                tables[canonical] = rows;
            }
        }
        return tables;
    }

    /// <summary>Decompresses an entry, refusing more than <paramref name="cap"/> bytes regardless of the declared size.</summary>
    private static string ReadCapped(ZipArchiveEntry entry, long cap)
    {
        using var source = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > cap)
                throw new InvalidDataException($"larger than {cap / (1024 * 1024)} MB when decompressed.");
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    // ---------------------------------------------------------------- CSV

    private sealed class Row(Dictionary<string, string> values)
    {
        /// <summary>Trimmed value of a column, null when missing or blank.</summary>
        public string? Get(string column) =>
            values.TryGetValue(column, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
    }

    private static List<Row>? Table(Dictionary<string, List<Row>> tables, string file) =>
        tables.TryGetValue(file, out var rows) ? rows : null;

    /// <summary>Parses CSV text, skipping any preamble before the first record that contains <paramref name="keyColumn"/>.</summary>
    private static List<Row>? ParseTable(string text, string keyColumn)
    {
        var records = ParseCsv(text);
        var headerIndex = records.FindIndex(r => r.Any(f => f.Trim().Equals(keyColumn, StringComparison.OrdinalIgnoreCase)));
        if (headerIndex < 0) return null;
        var header = records[headerIndex].Select(h => h.Trim()).ToList();
        var rows = new List<Row>();
        foreach (var record in records.Skip(headerIndex + 1))
        {
            if (record.All(string.IsNullOrWhiteSpace)) continue;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < header.Count && i < record.Count; i++)
                if (header[i].Length > 0) values.TryAdd(header[i], record[i]);
            rows.Add(new Row(values));
        }
        return rows;
    }

    /// <summary>RFC 4180 parser: quoted fields may contain commas, doubled quotes and line breaks; CRLF, LF or CR line ends.</summary>
    internal static List<List<string>> ParseCsv(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var fieldStarted = false;   // distinguishes an empty last line from a record with one empty field
        var i = 0;
        if (text.Length > 0 && text[0] == '﻿') i = 1;

        void EndField() { record.Add(field.ToString()); field.Clear(); }
        void EndRecord()
        {
            EndField();
            records.Add(record);
            record = [];
            fieldStarted = false;
        }

        for (; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }
            switch (c)
            {
                case '"':
                    inQuotes = true;   // lenient: a quote inside an unquoted field also starts quoting
                    fieldStarted = true;
                    break;
                case ',':
                    EndField();
                    fieldStarted = true;
                    break;
                case '\r':
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    EndRecord();
                    break;
                case '\n':
                    EndRecord();
                    break;
                default:
                    field.Append(c);
                    fieldStarted = true;
                    break;
            }
        }
        if (fieldStarted || field.Length > 0 || record.Count > 0) EndRecord();
        return records;
    }

    // ---------------------------------------------------------------- values

    private static void SetIf(JsonObject obj, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) obj[key] = value;
    }

    private static bool IsYes(string? value) =>
        value != null && (value.Equals("Yes", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase));

    /// <summary>startDate / endDate like the sample data; an empty end date means "ongoing" (endDate: null).</summary>
    private static void AddDates(JsonObject item, Row row, string startColumn, string endColumn, string file, List<string> warnings)
    {
        var start = ParseDate(row.Get(startColumn), file, warnings);
        if (start != null) item["startDate"] = start;
        var rawEnd = row.Get(endColumn);
        var end = ParseDate(rawEnd, file, warnings);
        // Unparseable end date: leave it out instead of claiming the entry is ongoing.
        if (rawEnd == null) item["endDate"] = null;
        else if (end != null) item["endDate"] = end;
    }

    private static readonly string[] MonthFormats = ["MMM yyyy", "MMMM yyyy", "MMM. yyyy"];

    /// <summary>Maps LinkedIn dates to ISO with the precision given: "Mar 2020" → "2020-03", "2020" → "2020".</summary>
    internal static string? ParseDate(string? value, string? file = null, List<string>? warnings = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = value.Trim();
        if (YearRegex().IsMatch(s)) return s;
        if (IsoRegex().IsMatch(s)) return s;
        if (DateTime.TryParseExact(s, MonthFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var month))
            return month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var slash = MonthSlashYearRegex().Match(s);
        if (slash.Success && int.Parse(slash.Groups[1].Value, CultureInfo.InvariantCulture) is >= 1 and <= 12)
            return $"{slash.Groups[2].Value}-{int.Parse(slash.Groups[1].Value, CultureInfo.InvariantCulture):00}";
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var full) && s.Any(char.IsDigit) && FourDigits().IsMatch(s))
            return full.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        warnings?.Add($"{file}: date \"{s}\" not recognised, left out.");
        return null;
    }

    /// <summary>LinkedIn birth dates are e.g. "Jan 15, 1990" or "Jan 15" (no year → not importable).</summary>
    private static string? ParseBirthDate(string value)
    {
        var s = value.Trim();
        if (!FourDigits().IsMatch(s)) return null;
        if (IsoRegex().IsMatch(s) && s.Length == 10) return s;
        string[] formats = ["MMM d, yyyy", "MMMM d, yyyy", "d MMM yyyy", "d MMMM yyyy", "MM/dd/yyyy", "yyyy-MM-dd"];
        return DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var d)
            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>ISO 639-1 code for an English (or native) language name, "" when unknown.</summary>
    internal static string LanguageCode(string name)
    {
        var n = name.Trim();
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            if (culture.TwoLetterISOLanguageName.Length != 2) continue;
            if (culture.EnglishName.Equals(n, StringComparison.OrdinalIgnoreCase) ||
                culture.NativeName.Equals(n, StringComparison.OrdinalIgnoreCase))
                return culture.TwoLetterISOLanguageName;
        }
        return "";
    }

    [GeneratedRegex(@"^\d{4}$")] private static partial Regex YearRegex();
    [GeneratedRegex(@"^\d{4}-\d{2}(-\d{2})?$")] private static partial Regex IsoRegex();
    [GeneratedRegex(@"^(\d{1,2})/(\d{4})$")] private static partial Regex MonthSlashYearRegex();
    [GeneratedRegex(@"\d{4}")] private static partial Regex FourDigits();
}

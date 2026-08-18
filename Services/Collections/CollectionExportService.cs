using System.IO.Compression;
using System.Security;
using System.Text;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

// Extra - Collection Export START
public sealed class CollectionExportService
{
    public string ExportCsv(IReadOnlyList<(KoleksiyonListItem Collection, List<SearchResultItem> Vehicles)> items)
    {
        var path = CreatePath("csv", "koleksiyonlar");
        var builder = new StringBuilder();

        AddRow(builder, "Koleksiyon", "Alış yeri", "Alış tarihi", "Alış saati", "Dönüş tarihi", "Dönüş saati", "Vites", "Yakıt", "Araç", "Detay", "Fiyat", "Günlük fiyat", "Tedarikçi");
        foreach (var item in items)
        {
            if (item.Vehicles.Count == 0)
            {
                AddCollectionRow(builder, item.Collection, null);
                continue;
            }

            foreach (var vehicle in item.Vehicles)
                AddCollectionRow(builder, item.Collection, vehicle);
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
        return path;
    }

    public string ExportExcel(IReadOnlyList<(KoleksiyonListItem Collection, List<SearchResultItem> Vehicles)> items)
    {
        var path = CreatePath("xlsx", "koleksiyonlar");
        var rows = new List<string[]>
        {
            new[] { "Koleksiyon", "Alış yeri", "Alış tarihi", "Alış saati", "Dönüş tarihi", "Dönüş saati", "Vites", "Yakıt", "Araç", "Detay", "Fiyat", "Günlük fiyat", "Tedarikçi" }
        };

        foreach (var item in items)
        {
            if (item.Vehicles.Count == 0)
            {
                rows.Add(CreateRow(item.Collection, null));
                continue;
            }

            rows.AddRange(item.Vehicles.Select(vehicle => CreateRow(item.Collection, vehicle)));
        }

        using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
        WriteEntry(archive, "_rels/.rels", RootRelationshipsXml);
        WriteEntry(archive, "xl/workbook.xml", WorkbookXml);
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml);
        WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(rows));
        return path;
    }

    private static void AddCollectionRow(StringBuilder builder, KoleksiyonListItem collection, SearchResultItem? vehicle)
    {
        AddRow(builder, CreateRow(collection, vehicle));
    }

    private static string[] CreateRow(KoleksiyonListItem collection, SearchResultItem? vehicle)
    {
        return new[]
        {
            collection.OzelAd,
            collection.AlisYeri,
            collection.AlisTarihi.ToString("dd.MM.yyyy"),
            collection.AlisSaati,
            collection.DonusTarihi.ToString("dd.MM.yyyy"),
            collection.DonusSaati,
            FormatFilter(collection.SecilenVitesFiltresi),
            FormatFilter(collection.SecilenYakitFiltresi),
            vehicle?.Title ?? string.Empty,
            vehicle?.Subtitle ?? string.Empty,
            vehicle?.Price ?? string.Empty,
            vehicle?.DailyPrice ?? string.Empty,
            vehicle?.Supplier ?? string.Empty
        };
    }

    private static string FormatFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "Farketmez" ? "-" : value;

    private static void AddRow(StringBuilder builder, params string[] values)
    {
        builder.AppendLine(string.Join(';', values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string CreatePath(string extension, string prefix)
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        Directory.CreateDirectory(downloads);
        return Path.Combine(downloads, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}");
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string BuildWorksheetXml(IReadOnlyList<string[]> rows)
    {
        var worksheet = new StringBuilder();
        worksheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        worksheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            worksheet.Append($"<row r=\"{rowIndex + 1}\">");
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
            {
                var cell = rows[rowIndex][columnIndex];
                worksheet.Append($"<c r=\"{GetColumnName(columnIndex)}{rowIndex + 1}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{SecurityElement.Escape(cell)}</t></is></c>");
            }

            worksheet.Append("</row>");
        }

        worksheet.Append("</sheetData></worksheet>");
        return worksheet.ToString();
    }

    private static string GetColumnName(int index)
    {
        var name = string.Empty;
        do
        {
            name = (char)('A' + index % 26) + name;
            index = index / 26 - 1;
        } while (index >= 0);

        return name;
    }

    private const string ContentTypesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="xml" ContentType="application/xml" />
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
        </Types>
        """;

    private const string RootRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
        </Relationships>
        """;

    private const string WorkbookXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets><sheet name="Koleksiyonlar" sheetId="1" r:id="rId1" /></sheets>
        </workbook>
        """;

    private const string WorkbookRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
        </Relationships>
        """;
}
// Extra - Collection Export END

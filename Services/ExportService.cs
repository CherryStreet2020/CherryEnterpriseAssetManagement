using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services;

// DepreciationRow is defined in DepreciationService.cs in this namespace

public class ExportService
{
    static ExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] ExportAssetsToCsv(IEnumerable<Asset> assets)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ID,Asset Number,Description,Location,Bay,Model,Serial Number,Status,In Service Date,Acquisition Cost,Accumulated Depreciation,FMV");

        foreach (var asset in assets)
        {
            sb.AppendLine($"{asset.Id},{Escape(asset.AssetNumber)},{Escape(asset.Description)},{Escape(asset.LocationRef?.Name)},{Escape(asset.Bay)},{Escape(asset.Model)},{Escape(asset.SerialNumber)},{asset.Status},{asset.InServiceDate:yyyy-MM-dd},{asset.AcquisitionCost:F2},{asset.AccumulatedDepreciation:F2},{asset.FairMarketValue:F2}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] ExportAssetsToExcel(IEnumerable<Asset> assets)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Assets");

        var headers = new[] { "ID", "Asset Number", "Description", "Location", "Bay", "Model", "Serial Number", "Status", "In Service Date", "Acquisition Cost", "Accumulated Depreciation", "FMV" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }

        int row = 2;
        foreach (var asset in assets)
        {
            worksheet.Cell(row, 1).Value = asset.Id;
            worksheet.Cell(row, 2).Value = asset.AssetNumber;
            worksheet.Cell(row, 3).Value = asset.Description;
            worksheet.Cell(row, 4).Value = asset.LocationRef?.Name;
            worksheet.Cell(row, 5).Value = asset.Bay;
            worksheet.Cell(row, 6).Value = asset.Model;
            worksheet.Cell(row, 7).Value = asset.SerialNumber;
            worksheet.Cell(row, 8).Value = asset.Status.ToString();
            worksheet.Cell(row, 9).Value = asset.InServiceDate;
            worksheet.Cell(row, 10).Value = asset.AcquisitionCost;
            worksheet.Cell(row, 11).Value = asset.AccumulatedDepreciation;
            worksheet.Cell(row, 12).Value = asset.FairMarketValue;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportAssetsToPdf(IEnumerable<Asset> assets, string title = "Fixed Assets Report")
    {
        var assetList = assets.ToList();
        
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(title).Bold().FontSize(18).FontColor(Colors.Blue.Darken2);
                    col.Item().AlignCenter().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(10).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(35);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.ConstantColumn(50);
                        columns.RelativeColumn(1);
                        columns.ConstantColumn(70);
                        columns.ConstantColumn(85);
                        columns.ConstantColumn(85);
                    });

                    table.Header(header =>
                    {
                        var headers = new[] { "ID", "Asset #", "Description", "Location", "Bay", "Status", "Date", "Cost", "Acc. Dep." };
                        foreach (var h in headers)
                        {
                            header.Cell().Background(Colors.Blue.Medium).Padding(5)
                                .Text(h).Bold().FontColor(Colors.White);
                        }
                    });

                    foreach (var asset in assetList)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(asset.Id.ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(asset.AssetNumber ?? "");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(asset.Description ?? "");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(asset.LocationRef?.Name ?? "");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(asset.Bay ?? "");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(asset.Status.ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(asset.InServiceDate.ToString("yyyy-MM-dd"));
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"${asset.AcquisitionCost:N2}");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"${asset.AccumulatedDepreciation:N2}");
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportJournalsToCsv(IEnumerable<JournalEntry> journals)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ID,Batch,Posting Date,Description,Period,Book,Source,Total Debits,Total Credits");

        foreach (var journal in journals)
        {
            var totalDebits = journal.Lines?.Sum(l => l.Debit) ?? 0;
            var totalCredits = journal.Lines?.Sum(l => l.Credit) ?? 0;
            sb.AppendLine($"{journal.Id},{Escape(journal.Batch)},{journal.PostingDate:yyyy-MM-dd},{Escape(journal.Description)},{journal.Period},{Escape(journal.Book?.Name)},{Escape(journal.Source)},{totalDebits:F2},{totalCredits:F2}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] ExportJournalsToExcel(IEnumerable<JournalEntry> journals)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Journal Entries");

        var headers = new[] { "ID", "Batch", "Posting Date", "Description", "Period", "Book", "Source", "Total Debits", "Total Credits" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
        }

        int row = 2;
        foreach (var journal in journals)
        {
            var totalDebits = journal.Lines?.Sum(l => l.Debit) ?? 0;
            var totalCredits = journal.Lines?.Sum(l => l.Credit) ?? 0;
            worksheet.Cell(row, 1).Value = journal.Id;
            worksheet.Cell(row, 2).Value = journal.Batch;
            worksheet.Cell(row, 3).Value = journal.PostingDate;
            worksheet.Cell(row, 4).Value = journal.Description;
            worksheet.Cell(row, 5).Value = journal.Period;
            worksheet.Cell(row, 6).Value = journal.Book?.Name;
            worksheet.Cell(row, 7).Value = journal.Source;
            worksheet.Cell(row, 8).Value = totalDebits;
            worksheet.Cell(row, 9).Value = totalCredits;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportCcaReportToCsv(IEnumerable<CcaClass> classes, IEnumerable<CcaClassBalance> balances, int fiscalYear)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CCA Class Report - Fiscal Year {fiscalYear}");
        sb.AppendLine("Class,Description,Rate,Opening UCC,Additions,Disposals,CCA Claimed,Closing UCC");

        foreach (var cls in classes)
        {
            var balance = balances.FirstOrDefault(b => b.CcaClassId == cls.Id && b.FiscalYear == fiscalYear);
            sb.AppendLine($"Class {cls.ClassNumber},{Escape(cls.Description)},{cls.Rate:P0},{balance?.OpeningUcc:F2},{balance?.Additions:F2},{balance?.Dispositions:F2},{balance?.CcaClaimed:F2},{balance?.ClosingUcc:F2}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] ExportCcaReportToExcel(IEnumerable<CcaClass> classes, IEnumerable<CcaClassBalance> balances, int fiscalYear)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("CCA Report");

        worksheet.Cell(1, 1).Value = $"CCA Class Report - Fiscal Year {fiscalYear}";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Range(1, 1, 1, 8).Merge();

        var headers = new[] { "Class", "Description", "Rate", "Opening UCC", "Additions", "Disposals", "CCA Claimed", "Closing UCC" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(3, i + 1).Value = headers[i];
            worksheet.Cell(3, i + 1).Style.Font.Bold = true;
            worksheet.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
        }

        int row = 4;
        foreach (var cls in classes)
        {
            var balance = balances.FirstOrDefault(b => b.CcaClassId == cls.Id && b.FiscalYear == fiscalYear);
            worksheet.Cell(row, 1).Value = $"Class {cls.ClassNumber}";
            worksheet.Cell(row, 2).Value = cls.Description;
            worksheet.Cell(row, 3).Value = cls.Rate;
            worksheet.Cell(row, 3).Style.NumberFormat.Format = "0%";
            worksheet.Cell(row, 4).Value = balance?.OpeningUcc ?? 0;
            worksheet.Cell(row, 5).Value = balance?.Additions ?? 0;
            worksheet.Cell(row, 6).Value = balance?.Dispositions ?? 0;
            worksheet.Cell(row, 7).Value = balance?.CcaClaimed ?? 0;
            worksheet.Cell(row, 8).Value = balance?.ClosingUcc ?? 0;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportMaintenanceToExcel(IEnumerable<MaintenanceEvent> events)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Maintenance Events");

        var headers = new[] { "ID", "Asset #", "Type", "Description", "Scheduled", "Completed", "Status", "Priority", "Est. Cost", "Actual Cost", "Technician", "Work Order" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }

        int row = 2;
        foreach (var evt in events)
        {
            worksheet.Cell(row, 1).Value = evt.Id;
            worksheet.Cell(row, 2).Value = evt.Asset?.AssetNumber ?? "";
            worksheet.Cell(row, 3).Value = evt.Type.ToString();
            worksheet.Cell(row, 4).Value = evt.Description;
            worksheet.Cell(row, 5).Value = evt.ScheduledDate;
            worksheet.Cell(row, 6).Value = evt.CompletedDate;
            worksheet.Cell(row, 7).Value = evt.Status.ToString();
            worksheet.Cell(row, 8).Value = evt.Priority.ToString();
            worksheet.Cell(row, 9).Value = evt.EstimatedCost;
            worksheet.Cell(row, 10).Value = evt.ActualCost ?? 0;
            worksheet.Cell(row, 11).Value = evt.TechnicianName ?? "";
            worksheet.Cell(row, 12).Value = evt.WorkOrderNumber ?? "";
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportCipToExcel(IEnumerable<CipProject> projects)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("CIP Projects");

        var headers = new[] { "ID", "Project #", "Name", "Status", "Start Date", "Est. Completion", "Budget", "Total Costs", "Variance", "Location", "Project Manager" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }

        int row = 2;
        foreach (var proj in projects)
        {
            worksheet.Cell(row, 1).Value = proj.Id;
            worksheet.Cell(row, 2).Value = proj.ProjectNumber;
            worksheet.Cell(row, 3).Value = proj.Name;
            worksheet.Cell(row, 4).Value = proj.Status.ToString();
            worksheet.Cell(row, 5).Value = proj.StartDate;
            worksheet.Cell(row, 6).Value = proj.EstimatedCompletionDate;
            worksheet.Cell(row, 7).Value = proj.BudgetAmount;
            worksheet.Cell(row, 8).Value = proj.TotalCosts;
            worksheet.Cell(row, 9).Value = proj.BudgetAmount - proj.TotalCosts;
            worksheet.Cell(row, 10).Value = proj.Location ?? "";
            worksheet.Cell(row, 11).Value = proj.ProjectManager != null ? proj.ProjectManager.Name : (proj.ProjectManagerName ?? "");
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportDepreciationScheduleToExcel(Asset asset, IEnumerable<DepreciationRow> schedule, string bookName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Depreciation Schedule");

        worksheet.Cell(1, 1).Value = $"Depreciation Schedule - {asset.AssetNumber}: {asset.Description}";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Range(1, 1, 1, 5).Merge();

        worksheet.Cell(2, 1).Value = $"Book: {bookName}";
        worksheet.Cell(2, 1).Style.Font.Italic = true;

        var headers = new[] { "Period", "Period End", "Depreciation", "Accumulated", "Net Book Value" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(4, i + 1).Value = headers[i];
            worksheet.Cell(4, i + 1).Style.Font.Bold = true;
            worksheet.Cell(4, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }

        int row = 5;
        foreach (var item in schedule)
        {
            worksheet.Cell(row, 1).Value = item.PeriodNumber;
            worksheet.Cell(row, 2).Value = item.PeriodEnd;
            worksheet.Cell(row, 3).Value = item.DepreciationAmount;
            worksheet.Cell(row, 4).Value = item.AccumulatedDepreciation;
            worksheet.Cell(row, 5).Value = item.EndingBookValue;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}

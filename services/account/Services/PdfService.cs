using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using EBanking.AccountService.Models.DTOs;
using EBanking.AccountService.Services.Interfaces;

namespace EBanking.AccountService.Services;

public class PdfService : IPdfService
{
    private readonly ILogger<PdfService> _logger;

    public PdfService(ILogger<PdfService> logger)
    {
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateStatementAsync(string userId, IEnumerable<TransactionDto> transactions, int year, int month)
    {
        return await Task.Run(() =>
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text($"E-Banking Statement - {year}/{month:D2}")
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(x =>
                        {
                            x.Spacing(20);

                            x.Item().Text($"Account Holder: {userId}").FontSize(14);
                            x.Item().Text($"Statement Period: {year}/{month:D2}").FontSize(14);
                            x.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(10).FontColor(Colors.Grey.Medium);

                            x.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);

                            // Transactions table
                            x.Item().Table(table =>
                            {
                                // Define columns
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(80);  // Date
                                    columns.RelativeColumn(3);  // Description
                                    columns.ConstantColumn(60);  // Type
                                    columns.ConstantColumn(80);  // Amount
                                    columns.ConstantColumn(80);  // Reference
                                });

                                // Header
                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Date").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Description").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Type").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Amount").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Reference").SemiBold();

                                    static IContainer CellStyle(IContainer container)
                                    {
                                        return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                    }
                                });

                                // Transactions
                                foreach (var transaction in transactions)
                                {
                                    table.Cell().Element(CellStyle).Text(transaction.TransactionDate.ToString("dd/MM/yyyy"));
                                    table.Cell().Element(CellStyle).Text(transaction.Description);
                                    table.Cell().Element(CellStyle).Text(transaction.Type);
                                    table.Cell().Element(CellStyle).Text($"{transaction.Amount:C} {transaction.Currency}");
                                    table.Cell().Element(CellStyle).Text(transaction.Reference ?? "-");

                                    static IContainer CellStyle(IContainer container)
                                    {
                                        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                                    }
                                }
                            });

                            // Summary
                            var totalCredits = transactions.Where(t => t.Type == "CREDIT").Sum(t => t.Amount);
                            var totalDebits = transactions.Where(t => t.Type == "DEBIT").Sum(t => t.Amount);
                            var netAmount = totalCredits - totalDebits;

                            x.Item().PaddingTop(20).Column(summary =>
                            {
                                summary.Item().Text("Summary").SemiBold().FontSize(16);
                                summary.Item().Text($"Total Credits: {totalCredits:C} EUR");
                                summary.Item().Text($"Total Debits: {totalDebits:C} EUR");
                                summary.Item().Text($"Net Amount: {netAmount:C} EUR").SemiBold();
                                summary.Item().Text($"Transaction Count: {transactions.Count()}");
                            });
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                            x.Span(" | Generated by E-Banking System | This is a computer-generated document.");
                        });
                });
            });

            return document.GeneratePdf();
        });
    }
}

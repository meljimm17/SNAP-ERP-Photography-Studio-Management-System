using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SNAP.infrastructure.services;

namespace SNAP
{
    /// <summary>
    /// Turns the Reports & Analytics screen's already-loaded data into a QuestPDF document - the
    /// Controller/Service half of ReportsForm's MVC split (see InventoryPresenter for the same
    /// pattern applied to the Inventory module). ReportsForm (the View) only gathers what it has
    /// already queried/rendered on screen and hands it here; this class never touches the
    /// database, a control, or a dialog - it just lays out the given data as PDF content.
    /// </summary>
    internal static class ReportDocumentBuilder
    {
        internal readonly record struct SummaryStat(string Label, string Value);

        public static IDocument Build(
            DateTime from,
            DateTime to,
            string? companyCode,
            string? generatedBy,
            bool includeExtendedSections,
            string? branchLabel,
            IReadOnlyList<SummaryStat> summaryStats,
            IReadOnlyList<byte[]> chartImages,
            IReadOnlyList<ReportsForm.OutstandingRow> outstanding,
            IReadOnlyList<ReportsForm.EquipmentUsageRow> equipmentUsage,
            IReadOnlyList<ReportsForm.LowStockRow> lowStock,
            IReadOnlyList<ReportsForm.AuditLogRow> auditLog,
            FinancialStatementService.IncomeStatementResult? incomeStatement,
            FinancialStatementService.BalanceSheetResult? balanceSheet,
            FinancialStatementService.CashFlowStatementResult? cashFlow)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9).FontColor(AppTheme.TextPrimary));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(inner =>
                            {
                                inner.Item().Text("Reports & Analytics").FontSize(18).Bold().FontColor(AppTheme.Navy);
                                inner.Item().Text(companyCode ?? string.Empty).FontSize(10).FontColor(AppTheme.TextSecondary);
                                // Without this, a one-branch statement is indistinguishable on
                                // paper from the whole company's.
                                if (!string.IsNullOrWhiteSpace(branchLabel))
                                {
                                    inner.Item().Text(branchLabel).FontSize(10).FontColor(AppTheme.TextSecondary);
                                }
                            });
                            row.ConstantItem(220).Column(inner =>
                            {
                                inner.Item().AlignRight().Text($"{from:MMMM d, yyyy} - {to:MMMM d, yyyy}").FontSize(10);
                                inner.Item().AlignRight().Text($"Generated {DateTime.Now:MMMM d, yyyy h:mm tt}").FontSize(8).FontColor(AppTheme.TextSecondary);
                                inner.Item().AlignRight().Text($"By {generatedBy ?? "—"}").FontSize(8).FontColor(AppTheme.TextSecondary);
                            });
                        });
                        col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingVertical(12).Column(col =>
                    {
                        col.Spacing(16);

                        col.Item().Element(c => ComposeSummary(c, summaryStats));
                        col.Item().Element(c => ComposeCharts(c, chartImages));
                        col.Item().Element(c => ComposeOutstanding(c, outstanding));

                        if (includeExtendedSections)
                        {
                            col.Item().Element(c => ComposeEquipmentUsage(c, equipmentUsage));
                            col.Item().Element(c => ComposeLowStock(c, lowStock));
                            col.Item().Element(c => ComposeIncomeStatement(c, incomeStatement));
                            col.Item().Element(c => ComposeBalanceSheet(c, balanceSheet));
                            col.Item().Element(c => ComposeCashFlow(c, cashFlow));
                            col.Item().Element(c => ComposeAuditLog(c, auditLog));
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });
        }

        private static void ComposeSummary(IContainer container, IReadOnlyList<SummaryStat> stats)
        {
            container.Column(col =>
            {
                col.Item().Text("Summary").FontSize(12).Bold().FontColor(AppTheme.Navy);
                col.Item().PaddingTop(4).Row(row =>
                {
                    foreach (var stat in stats)
                    {
                        AddStat(row.RelativeItem(), stat.Label, stat.Value);
                    }
                });
            });
        }

        /// <summary>The on-screen charts, two per row. Each pair is kept whole so a page break never
        /// slices a chart in half.</summary>
        private static void ComposeCharts(IContainer container, IReadOnlyList<byte[]> chartImages)
        {
            if (chartImages.Count == 0) return;

            container.Column(col =>
            {
                col.Item().Text("Charts").FontSize(12).Bold().FontColor(AppTheme.Navy);

                for (var i = 0; i < chartImages.Count; i += 2)
                {
                    var left = chartImages[i];
                    var right = i + 1 < chartImages.Count ? chartImages[i + 1] : null;

                    col.Item().PaddingTop(6).ShowEntire().Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Image(left).FitWidth();
                        if (right is null) row.RelativeItem();
                        else row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Image(right).FitWidth();
                    });
                }
            });
        }

        private static void AddStat(IContainer container, string label, string value)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
            {
                c.Item().Text(label).FontSize(8).FontColor(AppTheme.TextSecondary);
                c.Item().PaddingTop(2).Text(value).FontSize(14).Bold();
            });
        }

        private static void ComposeTable(IContainer container, string title, string[] headers, IReadOnlyList<string[]> rows)
        {
            container.Column(col =>
            {
                col.Item().Text(title).FontSize(12).Bold().FontColor(AppTheme.Navy);

                if (rows.Count == 0)
                {
                    col.Item().PaddingTop(4).Text("No records for this period.").FontSize(8).FontColor(AppTheme.TextSecondary).Italic();
                    return;
                }

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in headers) columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var h in headers)
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(h).FontSize(8).Bold();
                        }
                    });

                    foreach (var rowValues in rows)
                    {
                        foreach (var cell in rowValues)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cell).FontSize(8);
                        }
                    }
                });
            });
        }

        private static void ComposeOutstanding(IContainer container, IReadOnlyList<ReportsForm.OutstandingRow> rows)
        {
            var data = rows.Select(r => new[]
            {
                r.Customer,
                r.BookingDate.ToString("MMM d, yyyy"),
                $"₱{r.PriceSnapshot:N2}",
                $"₱{r.AmountPaid:N2}",
                $"₱{r.AmountDue:N2}"
            }).ToList();

            ComposeTable(container, "Outstanding Payments", new[] { "Customer", "Booking Date", "Price", "Paid", "Due" }, data);
        }

        private static void ComposeEquipmentUsage(IContainer container, IReadOnlyList<ReportsForm.EquipmentUsageRow> rows)
        {
            var data = rows.Select(r => new[] { r.Status, r.Count.ToString() }).ToList();
            ComposeTable(container, "Equipment Usage", new[] { "Status", "Count" }, data);
        }

        private static void ComposeLowStock(IContainer container, IReadOnlyList<ReportsForm.LowStockRow> rows)
        {
            var data = rows.Select(r => new[]
            {
                r.ItemName,
                r.QuantityOnHand.ToString("N2"),
                r.ReorderLevel.ToString("N2"),
                r.Supplier
            }).ToList();

            ComposeTable(container, "Low Stock Alerts", new[] { "Item", "On Hand", "Reorder Level", "Supplier" }, data);
        }

        private static void ComposeAuditLog(IContainer container, IReadOnlyList<ReportsForm.AuditLogRow> rows)
        {
            var data = rows.Select(r => new[]
            {
                r.Timestamp.ToString("MMM d, yyyy h:mm tt"),
                r.Actor,
                r.Entity,
                r.EntityId.ToString(),
                r.Action,
                r.Summary
            }).ToList();

            ComposeTable(container, "Audit Log", new[] { "Timestamp", "Actor", "Entity", "Entity ID", "Action", "Summary" }, data);
        }

        private static void ComposeIncomeStatement(IContainer container, FinancialStatementService.IncomeStatementResult? r)
        {
            container.Column(col =>
            {
                col.Item().Text("Income Statement").FontSize(12).Bold().FontColor(AppTheme.Navy);
                if (r is null)
                {
                    col.Item().PaddingTop(4).Text("No data.").FontSize(8).FontColor(AppTheme.TextSecondary);
                    return;
                }

                col.Item().PaddingTop(4).Column(inner =>
                {
                    StatementLine(inner.Item(), "Revenue (Payments + Product Sales)", r.Revenue);
                    StatementLine(inner.Item(), "Less: Operating Expenses", -r.OperatingExpenses);
                    StatementLine(inner.Item(), "Less: Salary & Wages", -r.SalaryWages);
                    inner.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    StatementLine(inner.Item(), "Net Income", r.NetIncome, bold: true);
                });
            });
        }

        private static void ComposeBalanceSheet(IContainer container, FinancialStatementService.BalanceSheetResult? r)
        {
            container.Column(col =>
            {
                col.Item().Text("Balance Sheet").FontSize(12).Bold().FontColor(AppTheme.Navy);
                if (r is null)
                {
                    col.Item().PaddingTop(4).Text("No data.").FontSize(8).FontColor(AppTheme.TextSecondary);
                    return;
                }

                col.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Column(assets =>
                    {
                        assets.Item().Text("ASSETS").FontSize(9).Bold();
                        StatementLine(assets.Item(), "Cash", r.Cash);
                        StatementLine(assets.Item(), "Accounts Receivable", r.AccountsReceivable);
                        StatementLine(assets.Item(), "Inventory (at cost)", r.Inventory);
                        StatementLine(assets.Item(), "Equipment (at cost)", r.Equipment);
                        assets.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        StatementLine(assets.Item(), "Total Assets", r.TotalAssets, bold: true);
                    });
                    row.RelativeItem().Column(liab =>
                    {
                        liab.Item().Text("LIABILITIES").FontSize(9).Bold();
                        StatementLine(liab.Item(), "Accounts Payable", r.AccountsPayable);
                        liab.Item().PaddingTop(6).Text("EQUITY").FontSize(9).Bold();
                        StatementLine(liab.Item(), "Owner's Capital", r.OwnersCapital);
                        StatementLine(liab.Item(), "Retained Earnings", r.RetainedEarnings);
                        StatementLine(liab.Item(), "Total Equity", r.TotalEquity, bold: true);
                        liab.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        StatementLine(liab.Item(), "Total Liabilities + Equity", r.TotalLiabilitiesAndEquity, bold: true);
                    });
                });

                var balanced = r.TotalAssets == r.TotalLiabilitiesAndEquity;
                col.Item().PaddingTop(4).Text(balanced
                        ? "Balanced (Assets = Liabilities + Equity)"
                        : $"Out of balance by ₱{Math.Abs(r.TotalAssets - r.TotalLiabilitiesAndEquity):N2}")
                    .FontSize(9).Bold().FontColor(balanced ? AppTheme.Success : AppTheme.Danger);
            });
        }

        private static void ComposeCashFlow(IContainer container, FinancialStatementService.CashFlowStatementResult? r)
        {
            container.Column(col =>
            {
                col.Item().Text("Cash Flow Statement").FontSize(12).Bold().FontColor(AppTheme.Navy);
                if (r is null)
                {
                    col.Item().PaddingTop(4).Text("No data.").FontSize(8).FontColor(AppTheme.TextSecondary);
                    return;
                }

                col.Item().PaddingTop(4).Column(inner =>
                {
                    StatementLine(inner.Item(), "Net Income", r.NetIncome, bold: true);
                    StatementLine(inner.Item(), "+/- Change in Accounts Receivable", r.AccountsReceivableChange);
                    StatementLine(inner.Item(), "+/- Change in Accounts Payable", r.AccountsPayableChange);
                    StatementLine(inner.Item(), "+/- Change in Inventory", r.InventoryChange);
                    inner.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    StatementLine(inner.Item(), "Net Cash from Operating Activities", r.NetCashFromOperations, bold: true);
                    StatementLine(inner.Item(), "Equipment purchased (Investing)", -r.EquipmentPurchased);
                    StatementLine(inner.Item(), "Owner Contributions/Withdrawals (Financing)", r.OwnerFinancingNet);
                    inner.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    StatementLine(inner.Item(), "Net Change in Cash", r.NetChangeInCash, bold: true);
                    StatementLine(inner.Item(), "Beginning Cash", r.BeginningCash);
                    StatementLine(inner.Item(), "Ending Cash", r.EndingCash, bold: true);
                });
            });
        }

        private static void StatementLine(IContainer container, string label, decimal value, bool bold = false)
        {
            var text = value < 0 ? $"(₱{-value:N2})" : $"₱{value:N2}";
            container.Row(row =>
            {
                if (bold)
                {
                    row.RelativeItem(2).Text(label).FontSize(9).Bold();
                    row.RelativeItem(1).AlignRight().Text(text).FontSize(9).Bold();
                }
                else
                {
                    row.RelativeItem(2).Text(label).FontSize(9);
                    row.RelativeItem(1).AlignRight().Text(text).FontSize(9);
                }
            });
        }
    }
}

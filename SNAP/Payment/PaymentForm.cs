using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP
{
    /// <summary>
    /// Booking-based Payment Management: one row per booking, always shown whether paid or not.
    /// There is no standalone "Add Payment" button - each row carries its own inline action(s)
    /// instead (Pay when nothing's been paid yet; Update/Delete once something has).
    /// </summary>
    public class PaymentForm : UserControl
    {
        private const string ActionColumnName = "Action";
        private const string DeleteColumnName = "DeleteAction";
        private const string AddOnsActionColumnName = "AddOnsAction";

        private readonly DataGridView _grid = new();
        private readonly AccessLevel _accessLevel;

        public PaymentForm(AccessLevel accessLevel = AccessLevel.Full)
        {
            _accessLevel = accessLevel;
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            AppTheme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Customer", DataPropertyName = "Customer", HeaderText = "Customer", FillWeight = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Package", DataPropertyName = "Package", HeaderText = "Package", FillWeight = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SessionDate",
                DataPropertyName = "SessionDate",
                HeaderText = "Session Date",
                FillWeight = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "MMM d, yyyy" }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PriceSnapshot",
                DataPropertyName = "PriceSnapshot",
                HeaderText = "Price",
                FillWeight = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "AmountPaid",
                DataPropertyName = "AmountPaid",
                HeaderText = "Amount Paid",
                FillWeight = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", DataPropertyName = "Status", HeaderText = "Status", FillWeight = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AddOnsSummary", DataPropertyName = "AddOnsSummary", HeaderText = "Add-ons", FillWeight = 100 });

            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = AddOnsActionColumnName,
                HeaderText = "",
                FillWeight = 80,
                UseColumnTextForButtonValue = true,
                FlatStyle = FlatStyle.Flat,
                Text = "Add-ons"
            });
            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = ActionColumnName,
                HeaderText = "",
                FillWeight = 80,
                UseColumnTextForButtonValue = false,
                FlatStyle = FlatStyle.Flat,
                Text = "Pay"
            });
            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = DeleteColumnName,
                HeaderText = "",
                FillWeight = 80,
                UseColumnTextForButtonValue = false,
                FlatStyle = FlatStyle.Flat,
                Text = "Delete"
            });

            AppTheme.StyleStatusColumn(_grid, "Status", value => (value?.ToString() ?? string.Empty) switch
            {
                "Paid" => ("Paid", StatusKind.Success),
                "Partially Paid" => ("Partially Paid", StatusKind.Warning),
                "Unpaid" => ("Unpaid", StatusKind.Danger),
                var other => (other, StatusKind.Neutral)
            });

            _grid.CellFormatting += Grid_CellFormatting;
            _grid.CellContentClick += Grid_CellContentClick;

            var header = AppTheme.CreatePageHeader("Payments", "Manage payments, deposits, balances, and payment status.");
            Controls.Add(AppTheme.BuildPage(header, null, AppTheme.WrapContent(_grid)));

            LoadData();
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_grid.Rows[e.RowIndex].DataBoundItem is not BookingPaymentRow row) return;

            var canEdit = _accessLevel is AccessLevel.Manage or AccessLevel.Full;
            var columnName = _grid.Columns[e.ColumnIndex].Name;

            var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];

            if (columnName == ActionColumnName)
            {
                e.Value = row.HasPayment ? "Update" : "Pay";
                cell.ReadOnly = !canEdit;
                AppTheme.StyleActionButtonCell(cell, AppTheme.NavyColor);
            }
            else if (columnName == DeleteColumnName)
            {
                e.Value = row.HasPayment ? "Delete" : string.Empty;
                cell.ReadOnly = !canEdit || !row.HasPayment;
                if (row.HasPayment)
                {
                    AppTheme.StyleActionButtonCell(cell, AppTheme.DangerColor);
                }
                else
                {
                    AppTheme.ClearActionButtonCell(cell);
                }
            }
            else if (columnName == AddOnsActionColumnName)
            {
                cell.ReadOnly = !canEdit;
                AppTheme.StyleActionButtonCell(cell, AppTheme.InfoColor);
            }
        }

        private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_grid.Rows[e.RowIndex].DataBoundItem is not BookingPaymentRow row) return;

            var columnName = _grid.Columns[e.ColumnIndex].Name;
            if (columnName == ActionColumnName)
            {
                PayOrUpdate(row);
            }
            else if (columnName == DeleteColumnName && row.HasPayment)
            {
                DeletePayments(row);
            }
            else if (columnName == AddOnsActionColumnName)
            {
                OpenAddOns(row);
            }
        }

        private void OpenAddOns(BookingPaymentRow row)
        {
            try
            {
                OpenAddOnsCore(row);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not update add-ons for this booking:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenAddOnsCore(BookingPaymentRow row)
        {
            using var db = CreateDbContext();
            var forSaleItems = db.InventoryItems.Where(x => x.IsActive && x.IsForSale).OrderBy(x => x.ItemName).ToList();
            var existing = db.ProductSales.Where(p => p.BookingId == row.BookingId && !p.IsDeleted).ToList();

            using var dialog = new AddOnsDialogForm(forSaleItems, existing);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var booking = db.Bookings.Find(row.BookingId);
            if (booking is null) return;

            AddOnsService.ApplyAddOns(db, row.BookingId, booking.CustomerId, dialog.Selections);
            TenantSyncService.SaveAndSync(db);
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using var db = CreateDbContext();

                var bookings = db.Bookings
                    .Include(b => b.Customer)
                    .Where(b => b.Status != BookingStatus.Cancelled)
                    .OrderByDescending(b => b.CreatedAt)
                    .ThenByDescending(b => b.BookingId)
                    .ToList();

                var paidTotals = db.Payments
                    .Where(p => !p.IsDeleted)
                    .GroupBy(p => p.BookingId)
                    .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountPaid));

                var addOnsByBooking = db.ProductSales
                    .Where(p => !p.IsDeleted && p.BookingId != null)
                    .GroupBy(p => p.BookingId!.Value)
                    .ToDictionary(g => g.Key, g => new { Total = g.Sum(p => p.TotalAmount), Count = g.Sum(p => p.Quantity) });

                _grid.DataSource = bookings.Select(b =>
                {
                    var amountPaid = paidTotals.GetValueOrDefault(b.BookingId);
                    var addOns = addOnsByBooking.GetValueOrDefault(b.BookingId);
                    // Add-ons are billed on top of the package, so they count toward what's owed.
                    var totalPrice = b.PriceSnapshot + (addOns?.Total ?? 0m);
                    var amountDue = totalPrice - amountPaid;
                    return new BookingPaymentRow
                    {
                        BookingId = b.BookingId,
                        Customer = b.Customer?.FullName ?? $"Customer #{b.CustomerId}",
                        Package = b.PackageNameSnapshot,
                        SessionDate = b.SessionDate,
                        PriceSnapshot = totalPrice,
                        AmountPaid = amountPaid,
                        HasPayment = amountPaid > 0,
                        Status = amountPaid <= 0 ? "Unpaid" : amountDue > 0 ? "Partially Paid" : "Paid",
                        AddOnsSummary = addOns is null ? "—" : $"₱{addOns.Total:N0} · {addOns.Count:0.##} item(s)"
                    };
                }).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static TenantErpDbContext CreateDbContext()
        {
            var connectionString = AppSession.CurrentTenantConnectionString
                ?? AppDbConfig.TenantErpConnectionString;

            var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
                .Options;

            return new TenantErpDbContext(options);
        }

        private void PayOrUpdate(BookingPaymentRow row)
        {
            try
            {
                PayOrUpdateCore(row);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save this payment:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PayOrUpdateCore(BookingPaymentRow row)
        {
            using var db = CreateDbContext();
            var booking = db.Bookings.Find(row.BookingId);
            if (booking is null) return;

            var alreadyPaid = db.Payments
                .Where(p => !p.IsDeleted && p.BookingId == row.BookingId)
                .Sum(p => (decimal?)p.AmountPaid) ?? 0m;

            using var dialog = new PaymentDialogForm(db, booking, alreadyPaid);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            LoadData();
            OfferReceipt(dialog);
        }

        private void OfferReceipt(PaymentDialogForm dialog)
        {
            if (dialog.SavedPayment is null || dialog.SavedBooking is null) return;

            var changeLine = dialog.SavedChange > 0 ? $"\nChange: ₱{dialog.SavedChange:N2}" : string.Empty;
            var openReceipt = MessageBox.Show(
                $"Payment recorded.\nTotal Paid: ₱{dialog.SavedTotalPaid:N2}\nRemaining Balance: ₱{dialog.SavedRemainingBalance:N2}{changeLine}\n\nGenerate a receipt now?",
                "Payment Recorded", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (openReceipt == DialogResult.Yes)
            {
                using var receipt = new ReceiptForm(dialog.SavedPayment, dialog.SavedBooking, dialog.SavedTotalPaid, dialog.SavedRemainingBalance, dialog.SavedChange, dialog.SavedAddOnsTotal);
                receipt.ShowDialog(this);
            }
        }

        private void DeletePayments(BookingPaymentRow row)
        {
            if (MessageBox.Show(
                    $"Delete all payments recorded for {row.Customer}'s booking ({row.Package})? This will show as Unpaid again. Payment history is kept for audit, not erased.",
                    "Delete Payment", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var db = CreateDbContext();
                var payments = db.Payments.Where(p => !p.IsDeleted && p.BookingId == row.BookingId).ToList();
                foreach (var payment in payments)
                {
                    payment.IsDeleted = true;
                }
                TenantSyncService.SaveAndSync(db);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not delete these payments:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private sealed class BookingPaymentRow
        {
            public int BookingId { get; set; }
            public string Customer { get; set; } = string.Empty;
            public string Package { get; set; } = string.Empty;
            public DateTime SessionDate { get; set; }
            public decimal PriceSnapshot { get; set; }
            public decimal AmountPaid { get; set; }
            public bool HasPayment { get; set; }
            public string Status { get; set; } = string.Empty;
            public string AddOnsSummary { get; set; } = string.Empty;
        }
    }
}

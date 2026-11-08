using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.infrastructure.services;

namespace SNAP
{
    /// <summary>
    /// Runs one payroll cycle for the whole studio: pick a pay period, review every active
    /// employee's computed net pay side by side, then create all their payslips in one action.
    ///
    /// This only works out what each employee is owed - it does not pay them. The payslips are
    /// created unpaid and settled one at a time from the Salary Payments list, mirroring how an
    /// Expense is recorded first and paid later.
    ///
    /// Two states take an employee out of the run and say why, rather than silently producing a
    /// wrong payslip: an employee with no MonthlySalary can't have statutory deductions computed
    /// (they'd save as zero), and an employee already paid for an overlapping period would be a
    /// duplicate. Both are listed but unchecked, so it's visible that they were skipped. The
    /// already-paid lock can be lifted for corrections and back pay; the missing-salary one can't,
    /// since there would be nothing to compute from.
    /// </summary>
    public class PayrollRunDialogForm : Form
    {
        private const string IncludeColumn = "Include";
        private const string StatusColumn = "Status";

        private readonly List<TenantUser> _users;
        private readonly List<SalaryPayment> _existingPayments;

        private readonly DataGridView _grid = new();
        private readonly RoundedDatePicker _periodStartPicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedDatePicker _periodEndPicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedTextBox _notesBox = new() { PlaceholderText = "Notes applied to every payslip in this run (optional)" };
        private readonly CheckBox _allowAlreadyPaid = new() { Text = "Allow employees already paid for this period (corrections / back pay)", ForeColor = AppTheme.TextPrimaryColor, Font = AppTheme.BodyFont, AutoSize = false, UseMnemonic = false };
        private readonly Label _summaryLabel = new() { ForeColor = AppTheme.TextPrimaryColor, Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = false };

        private readonly RoundedButton _saveButton = new() { Text = "Create Payslips" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        /// <summary>One payslip per employee included in the run, ready to persist.</summary>
        public List<PayrollRunEntry> Entries { get; private set; } = new();

        public DateTime PayPeriodStart { get; private set; }
        public DateTime PayPeriodEnd { get; private set; }
        public string? Notes { get; private set; }

        public PayrollRunDialogForm(List<TenantUser> users, List<SalaryPayment> existingPayments)
        {
            _users = users;
            _existingPayments = existingPayments;

            Text = "Run Payroll";
            Width = 960;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 880;
            _periodStartPicker.Width = 280;
            _periodEndPicker.Width = 280;
            _notesBox.Width = width;
            _allowAlreadyPaid.Width = width;
            _allowAlreadyPaid.Height = 24;
            _allowAlreadyPaid.Margin = new Padding(0, 0, 0, 14);
            _summaryLabel.Width = width;
            _summaryLabel.Height = 30;
            _summaryLabel.Margin = new Padding(0, 0, 0, 14);
            _saveButton.Width = 220;
            _saveButton.Height = 44;
            _cancelButton.Width = 220;
            _cancelButton.Height = 44;

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            _periodStartPicker.Value = monthStart;
            _periodEndPicker.Value = monthStart.AddMonths(1).AddDays(-1);

            BuildGrid(width);

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton,
                AppTheme.BuildFieldGroup("Pay Period Start", _periodStartPicker),
                AppTheme.BuildFieldGroup("Pay Period End", _periodEndPicker),
                _allowAlreadyPaid,
                _grid,
                _summaryLabel,
                AppTheme.BuildFieldGroup("Notes", _notesBox)));

            _periodStartPicker.ValueChanged += (_, _) => PopulateRows();
            _periodEndPicker.ValueChanged += (_, _) => PopulateRows();
            _allowAlreadyPaid.CheckedChanged += (_, _) => PopulateRows();
            _grid.CellValueChanged += (_, e) =>
            {
                if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == IncludeColumn) UpdateSummary();
            };
            // Checkbox edits aren't committed until focus leaves the cell, so the summary would
            // lag one click behind without this.
            _grid.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();

            PopulateRows();
        }

        private void BuildGrid(int width)
        {
            AppTheme.StyleGrid(_grid);
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.Width = width;
            _grid.Height = 300;
            _grid.Margin = new Padding(0, 0, 0, 14);

            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = IncludeColumn, HeaderText = "Pay", FillWeight = 40 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Employee", HeaderText = "Employee", FillWeight = 150, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Gross", HeaderText = "Gross", FillWeight = 85, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sss", HeaderText = "SSS", FillWeight = 75, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PhilHealth", HeaderText = "PhilHealth", FillWeight = 85, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PagIbig", HeaderText = "Pag-IBIG", FillWeight = 80, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tax", HeaderText = "Withholding Tax", FillWeight = 105, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Net", HeaderText = "Net Pay", FillWeight = 95, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = StatusColumn, HeaderText = "Status", FillWeight = 150, ReadOnly = true });

            AppTheme.StyleStatusColumn(_grid, StatusColumn, value => (value?.ToString() ?? string.Empty) switch
            {
                ReadyStatus => (ReadyStatus, StatusKind.Success),
                AlreadyPaidStatus => (AlreadyPaidStatus, StatusKind.Warning),
                var other => (other, StatusKind.Danger)
            });
        }

        private const string ReadyStatus = "Ready to pay";
        private const string AlreadyPaidStatus = "Already paid this period";
        private const string NoSalaryStatus = "No monthly salary set";

        private void PopulateRows()
        {
            var start = _periodStartPicker.Value.Date;
            var end = _periodEndPicker.Value.Date;

            _grid.Rows.Clear();

            foreach (var user in _users)
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Cells["Employee"].Value = user.FullName;

                // Always locked, override or not: with no salary on file there are no figures to
                // compute, so a payslip here could only be zeroes.
                if (user.MonthlySalary is null)
                {
                    row.Cells[StatusColumn].Value = NoSalaryStatus;
                    row.Cells[IncludeColumn].Value = false;
                    row.Cells[IncludeColumn].ReadOnly = true;
                    continue;
                }

                var breakdown = PayrollCalculator.Compute(user.MonthlySalary.Value);
                row.Tag = new PayrollRunEntry(
                    user.UserId,
                    breakdown.Gross,
                    breakdown.SssDeduction,
                    breakdown.PhilHealthDeduction,
                    breakdown.PagIbigDeduction,
                    breakdown.WithholdingTax,
                    breakdown.NetAmount);

                row.Cells["Gross"].Value = breakdown.Gross.ToString("N2");
                row.Cells["Sss"].Value = breakdown.SssDeduction.ToString("N2");
                row.Cells["PhilHealth"].Value = breakdown.PhilHealthDeduction.ToString("N2");
                row.Cells["PagIbig"].Value = breakdown.PagIbigDeduction.ToString("N2");
                row.Cells["Tax"].Value = breakdown.WithholdingTax.ToString("N2");
                row.Cells["Net"].Value = breakdown.NetAmount.ToString("N2");

                if (HasOverlappingPayment(user.UserId, start, end))
                {
                    row.Cells[StatusColumn].Value = AlreadyPaidStatus;
                    // Unlocked by the override, but still left unticked - paying someone twice for
                    // the same period stays a deliberate act, never the default.
                    row.Cells[IncludeColumn].Value = false;
                    row.Cells[IncludeColumn].ReadOnly = !_allowAlreadyPaid.Checked;
                    continue;
                }

                row.Cells[StatusColumn].Value = ReadyStatus;
                row.Cells[IncludeColumn].Value = true;
            }

            UpdateSummary();
        }

        /// <summary>
        /// Any existing payslip whose period overlaps the one being run counts as already paid, so
        /// re-running September 1-30 is blocked while genuine semi-monthly runs (1-15 then 16-30)
        /// stay allowed.
        /// </summary>
        private bool HasOverlappingPayment(int userId, DateTime start, DateTime end) =>
            _existingPayments.Any(p =>
                p.TenantUserId == userId &&
                p.PayPeriodStart.Date <= end &&
                p.PayPeriodEnd.Date >= start);

        private void UpdateSummary()
        {
            var included = IncludedEntries();
            var total = included.Sum(x => x.NetAmount);
            _summaryLabel.Text = $"{included.Count} employee(s) selected  •  Total Net Pay: ₱{total:N2}";
        }

        private List<PayrollRunEntry> IncludedEntries()
        {
            var entries = new List<PayrollRunEntry>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is PayrollRunEntry entry && row.Cells[IncludeColumn].Value is true)
                {
                    entries.Add(entry);
                }
            }
            return entries;
        }

        private void Save()
        {
            if (_periodEndPicker.Value.Date < _periodStartPicker.Value.Date)
            {
                MessageBox.Show("Pay Period End cannot be before Pay Period Start.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var entries = IncludedEntries();
            if (entries.Count == 0)
            {
                MessageBox.Show("Select at least one employee to pay.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Entries = entries;
            PayPeriodStart = _periodStartPicker.Value.Date;
            PayPeriodEnd = _periodEndPicker.Value.Date;
            Notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public sealed record PayrollRunEntry(
        int TenantUserId,
        decimal GrossAmount,
        decimal SssDeduction,
        decimal PhilHealthDeduction,
        decimal PagIbigDeduction,
        decimal WithholdingTax,
        decimal NetAmount);
}

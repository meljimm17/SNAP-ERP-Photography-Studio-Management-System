using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.infrastructure.services;

namespace SNAP
{
    /// <summary>
    /// Runs one monthly payroll cycle for a TenantUser: picking the employee auto-computes the
    /// full Philippine statutory deduction breakdown (SSS, PhilHealth, Pag-IBIG, BIR withholding
    /// tax) from their MonthlySalary via PayrollCalculator, shown before saving and editable for
    /// manual adjustment - Net Amount stays a live read-only total of whatever Gross/deductions are
    /// currently entered, so the numbers can never drift out of sync with each other.
    /// </summary>
    public class SalaryPaymentDialogForm : Form
    {
        private readonly List<TenantUser> _users;

        private readonly RoundedComboBox _userCombo = new();
        private readonly RoundedTextBox _grossBox = new() { PlaceholderText = "Gross Amount (decimal number)" };
        private readonly RoundedTextBox _sssBox = new() { PlaceholderText = "SSS Deduction" };
        private readonly RoundedTextBox _philHealthBox = new() { PlaceholderText = "PhilHealth Deduction" };
        private readonly RoundedTextBox _pagIbigBox = new() { PlaceholderText = "Pag-IBIG Deduction" };
        private readonly RoundedTextBox _withholdingTaxBox = new() { PlaceholderText = "Withholding Tax" };
        private readonly Label _netAmountLabel = new() { ForeColor = AppTheme.TextPrimaryColor, Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = false };
        private readonly RoundedDatePicker _periodStartPicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedDatePicker _periodEndPicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedDatePicker _paymentDatePicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedTextBox _notesBox = new() { PlaceholderText = "Notes (text, optional)" };

        private readonly RoundedButton _saveButton = new() { Text = "Run Payroll" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public int TenantUserId { get; private set; }
        public decimal GrossAmount { get; private set; }
        public decimal SssDeduction { get; private set; }
        public decimal PhilHealthDeduction { get; private set; }
        public decimal PagIbigDeduction { get; private set; }
        public decimal WithholdingTax { get; private set; }
        public decimal NetAmount { get; private set; }
        public DateTime PayPeriodStart { get; private set; }
        public DateTime PayPeriodEnd { get; private set; }
        public DateTime PaymentDate { get; private set; }
        public string? Notes { get; private set; }

        public SalaryPaymentDialogForm(SalaryPayment? existing, List<TenantUser> users)
        {
            _users = users;

            Text = existing is null ? "Run Payroll" : "Edit Salary Payment";
            Width = 480;
            Height = 800;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _userCombo.Width = width;
            _grossBox.Width = width;
            _sssBox.Width = width;
            _philHealthBox.Width = width;
            _pagIbigBox.Width = width;
            _withholdingTaxBox.Width = width;
            _netAmountLabel.Width = width;
            _netAmountLabel.Height = 30;
            _netAmountLabel.Margin = new Padding(0, 0, 0, 14);
            _periodStartPicker.Width = width;
            _periodEndPicker.Width = width;
            _paymentDatePicker.Width = width;
            _notesBox.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            foreach (var user in users) _userCombo.Items.Add(new ComboItem(user.UserId, user.FullName));

            Controls.Add(AppTheme.BuildDialogBody(
                AppTheme.BuildFieldGroup("Employee", _userCombo),
                AppTheme.BuildFieldGroup("Gross Amount (₱)", _grossBox),
                AppTheme.BuildFieldGroup("SSS Deduction (₱)", _sssBox),
                AppTheme.BuildFieldGroup("PhilHealth Deduction (₱)", _philHealthBox),
                AppTheme.BuildFieldGroup("Pag-IBIG Deduction (₱)", _pagIbigBox),
                AppTheme.BuildFieldGroup("Withholding Tax (₱)", _withholdingTaxBox),
                _netAmountLabel,
                AppTheme.BuildFieldGroup("Pay Period Start", _periodStartPicker),
                AppTheme.BuildFieldGroup("Pay Period End", _periodEndPicker),
                AppTheme.BuildFieldGroup("Payment Date", _paymentDatePicker),
                AppTheme.BuildFieldGroup("Notes", _notesBox),
                AppTheme.BuildDialogButtonRow(_saveButton, _cancelButton)));

            _userCombo.SelectedIndexChanged += (_, _) => RecomputeFromSelectedUser();
            foreach (var box in new[] { _grossBox, _sssBox, _philHealthBox, _pagIbigBox, _withholdingTaxBox })
            {
                box.TextChanged += (_, _) => UpdateNetAmountLabel();
            }

            if (existing is not null)
            {
                for (var i = 0; i < _userCombo.Items.Count; i++)
                {
                    if (((ComboItem)_userCombo.Items[i]!).Id == existing.TenantUserId)
                    {
                        _userCombo.SelectedIndex = i;
                        break;
                    }
                }
                _grossBox.Text = existing.GrossAmount.ToString("0.##");
                _sssBox.Text = existing.SssDeduction.ToString("0.##");
                _philHealthBox.Text = existing.PhilHealthDeduction.ToString("0.##");
                _pagIbigBox.Text = existing.PagIbigDeduction.ToString("0.##");
                _withholdingTaxBox.Text = existing.WithholdingTax.ToString("0.##");
                _periodStartPicker.Value = existing.PayPeriodStart;
                _periodEndPicker.Value = existing.PayPeriodEnd;
                _paymentDatePicker.Value = existing.PaymentDate;
                _notesBox.Text = existing.Notes ?? string.Empty;
            }
            else
            {
                if (_userCombo.Items.Count > 0) _userCombo.SelectedIndex = 0;
                var today = DateTime.Today;
                var monthStart = new DateTime(today.Year, today.Month, 1);
                _periodStartPicker.Value = monthStart;
                _periodEndPicker.Value = monthStart.AddMonths(1).AddDays(-1);
                _paymentDatePicker.Value = today;
                RecomputeFromSelectedUser();
            }

            UpdateNetAmountLabel();

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        /// <summary>Fills Gross/SSS/PhilHealth/Pag-IBIG/Withholding Tax from PayrollCalculator based
        /// on the selected employee's MonthlySalary - a starting point the fields can still be
        /// manually adjusted from afterward.</summary>
        private void RecomputeFromSelectedUser()
        {
            if (_userCombo.SelectedItem is not ComboItem selected) return;
            var user = _users.FirstOrDefault(u => u.UserId == selected.Id);
            if (user is null || user.MonthlySalary is null) return;

            var breakdown = PayrollCalculator.Compute(user.MonthlySalary.Value);
            _grossBox.Text = breakdown.Gross.ToString("0.##");
            _sssBox.Text = breakdown.SssDeduction.ToString("0.##");
            _philHealthBox.Text = breakdown.PhilHealthDeduction.ToString("0.##");
            _pagIbigBox.Text = breakdown.PagIbigDeduction.ToString("0.##");
            _withholdingTaxBox.Text = breakdown.WithholdingTax.ToString("0.##");
        }

        private void UpdateNetAmountLabel()
        {
            var gross = ParseOrZero(_grossBox.Text);
            var sss = ParseOrZero(_sssBox.Text);
            var philHealth = ParseOrZero(_philHealthBox.Text);
            var pagIbig = ParseOrZero(_pagIbigBox.Text);
            var tax = ParseOrZero(_withholdingTaxBox.Text);
            var net = gross - sss - philHealth - pagIbig - tax;
            _netAmountLabel.Text = $"Net Pay: ₱{net:N2}";
        }

        private static decimal ParseOrZero(string text) => decimal.TryParse(text, out var value) ? value : 0m;

        private void Save()
        {
            if (_userCombo.SelectedItem is not ComboItem employee)
            {
                MessageBox.Show("Select an employee.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_grossBox.Text, out var gross) || gross <= 0)
            {
                MessageBox.Show("Enter a valid Gross Amount greater than zero.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_sssBox.Text, out var sss) || sss < 0 ||
                !decimal.TryParse(_philHealthBox.Text, out var philHealth) || philHealth < 0 ||
                !decimal.TryParse(_pagIbigBox.Text, out var pagIbig) || pagIbig < 0 ||
                !decimal.TryParse(_withholdingTaxBox.Text, out var tax) || tax < 0)
            {
                MessageBox.Show("Enter valid, non-negative deduction amounts.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_periodEndPicker.Value.Date < _periodStartPicker.Value.Date)
            {
                MessageBox.Show("Pay Period End cannot be before Pay Period Start.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TenantUserId = employee.Id;
            GrossAmount = gross;
            SssDeduction = sss;
            PhilHealthDeduction = philHealth;
            PagIbigDeduction = pagIbig;
            WithholdingTax = tax;
            NetAmount = gross - sss - philHealth - pagIbig - tax;
            PayPeriodStart = _periodStartPicker.Value.Date;
            PayPeriodEnd = _periodEndPicker.Value.Date;
            PaymentDate = _paymentDatePicker.Value.Date;
            Notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed record ComboItem(int Id, string Display)
        {
            public override string ToString() => Display;
        }
    }
}

using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.infrastructure.services;
using SNAP.domain.entities;

namespace SNAP
{
    /// <summary>
    /// View/Edit dialog for an existing customer. New customers are registered as part of the
    /// Booking &amp; Scheduling flow (see BookingDialogForm), not from here - Customer Management
    /// is for searching, viewing, updating, and reviewing booking history for existing customers.
    /// </summary>
    public class CustomerDialogForm : Form
    {
        private readonly RoundedTextBox _nameBox = new() { PlaceholderText = "Full Name (text, e.g. Juan Dela Cruz)" };
        private readonly RoundedTextBox _contactBox = new() { PlaceholderText = "Contact Number (numbers, e.g. 09171234567)" };
        private readonly RoundedTextBox _emailBox = new() { PlaceholderText = "Email Address (text, optional, e.g. name@example.com)" };
        private readonly RoundedTextBox _addressBox = new() { PlaceholderText = "Address (text, optional)" };

        private readonly RoundedButton _saveButton = new() { Text = "Save Changes" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public string FullName { get; private set; } = string.Empty;
        public string ContactNumber { get; private set; } = string.Empty;
        public string? EmailAddress { get; private set; }
        public string? Address { get; private set; }

        public CustomerDialogForm(int customerId)
        {
            Text = "Edit Customer";
            Width = 510;
            Height = 480;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _nameBox.Width = width;
            _contactBox.Width = width;
            _emailBox.Width = width;
            _addressBox.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton,
                AppTheme.BuildFieldGroup("Full Name", _nameBox),
                AppTheme.BuildFieldGroup("Contact Number", _contactBox),
                AppTheme.BuildFieldGroup("Email Address", _emailBox),
                AppTheme.BuildFieldGroup("Address", _addressBox)));

            // Load customer data from API if customerId is provided
            try
            {
                var customer = ApiClient.Get<dynamic>($"/api/customers/{customerId}");
                if (customer != null)
                {
                    _nameBox.Text = customer.fullName ?? string.Empty;
                    _contactBox.Text = customer.contactNumber ?? string.Empty;
                    _emailBox.Text = customer.emailAddress ?? string.Empty;
                    _addressBox.Text = customer.address ?? string.Empty;
                    Text = $"Edit Customer - {customer.fullName}";
                }
            }
            catch
            {
                // If load fails, just show empty form
            }

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text) || string.IsNullOrWhiteSpace(_contactBox.Text))
            {
                MessageBox.Show("Full Name and Contact Number are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Validation.IsValidPhMobileNumber(_contactBox.Text))
            {
                MessageBox.Show("Enter a valid PH mobile number, e.g. 09171234567 or +639171234567.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_emailBox.Text) && !Validation.IsValidEmail(_emailBox.Text))
            {
                MessageBox.Show("Enter a valid email address, e.g. name@example.com.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            FullName = _nameBox.Text.Trim();
            ContactNumber = _contactBox.Text.Trim();
            EmailAddress = string.IsNullOrWhiteSpace(_emailBox.Text) ? null : _emailBox.Text.Trim();
            Address = string.IsNullOrWhiteSpace(_addressBox.Text) ? null : _addressBox.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

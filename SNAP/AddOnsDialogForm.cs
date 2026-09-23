using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;

namespace SNAP
{
    /// <summary>
    /// Thin popup wrapper around the shared AddOnsPanel, used by Payment Management's "Add-ons"
    /// button (the entry point that isn't already inside a bigger dialog to embed the checklist
    /// into).
    /// </summary>
    public class AddOnsDialogForm : Form
    {
        private readonly AddOnsPanel _panel;
        private readonly RoundedButton _saveButton = new() { Text = "Save Add-ons" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public List<AddOnsService.AddOnSelection> Selections { get; private set; } = new();

        public AddOnsDialogForm(List<Inventory> forSaleItems, List<ProductSale> existing)
        {
            Text = "Booking Add-ons";
            Width = 560;
            Height = 620;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 440;
            _panel = new AddOnsPanel(forSaleItems, width) { Margin = new Padding(0, 0, 0, 14) };
            _panel.LoadExisting(existing);

            _saveButton.Width = 210;
            _saveButton.Height = 44;
            _cancelButton.Width = 210;
            _cancelButton.Height = 44;

            Controls.Add(AppTheme.BuildDialogBody(
                _panel,
                AppTheme.BuildDialogButtonRow(_saveButton, _cancelButton)));

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) =>
            {
                Selections = _panel.GetSelections();
                DialogResult = DialogResult.OK;
                Close();
            };
        }
    }
}

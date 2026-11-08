using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;

namespace SNAP
{
    /// <summary>
    /// Simple printable payment receipt: a plain-text summary with a Print Preview action.
    /// No PDF library dependency is needed - PrintPreviewDialog covers "generate/print a receipt".
    /// </summary>
    public class ReceiptForm : Form
    {
        private readonly string _receiptText;
        private readonly TextBox _preview = new();
        private readonly RoundedButton _printButton = new() { Text = "Print / Preview" };
        private readonly RoundedButton _closeButton = new() { Text = "Close", IsPrimary = false };

        public ReceiptForm(Payment payment, Booking booking, decimal totalPaid, decimal remainingBalance, decimal change = 0m, decimal addOnsTotal = 0m)
        {
            _receiptText = BuildReceiptText(payment, booking, totalPaid, remainingBalance, change, addOnsTotal);

            Text = "Payment Receipt";
            Width = 480;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _preview.Multiline = true;
            _preview.ReadOnly = true;
            _preview.Font = new Font(FontFamily.GenericMonospace, 10);
            _preview.Location = new Point(20, 20);
            _preview.Width = 420;
            _preview.Height = 380;
            _preview.ScrollBars = ScrollBars.Vertical;
            _preview.Text = _receiptText;
            Controls.Add(_preview);

            _printButton.Location = new Point(20, 420);
            _printButton.Width = 200;
            _printButton.Height = 40;
            _printButton.Click += (_, _) => PrintReceipt();
            Controls.Add(_printButton);

            _closeButton.Location = new Point(240, 420);
            _closeButton.Width = 200;
            _closeButton.Height = 40;
            _closeButton.Click += (_, _) => Close();
            Controls.Add(_closeButton);
        }

        private static string BuildReceiptText(Payment payment, Booking booking, decimal totalPaid, decimal remainingBalance, decimal change, decimal addOnsTotal)
        {
            var priceLines = addOnsTotal > 0
                ? $"Package Price:  P{booking.PriceSnapshot:N2}\n" +
                  $"Add-ons:        P{addOnsTotal:N2}\n" +
                  $"Total Price:    P{booking.PriceSnapshot + addOnsTotal:N2}\n"
                : $"Booking Price:  P{booking.PriceSnapshot:N2}\n";
            var customerName = booking.Customer?.FullName ?? $"Customer #{booking.CustomerId}";
            return
                "======================================\n" +
                "           SNAP ERP - RECEIPT          \n" +
                "======================================\n" +
                $"Receipt Date:   {payment.PaymentDate:MMM d, yyyy h:mm tt}\n" +
                $"Payment ID:     {payment.PaymentId}\n" +
                "--------------------------------------\n" +
                $"Booking ID:     {booking.BookingId}\n" +
                $"Customer:       {customerName}\n" +
                $"Package:        {booking.PackageNameSnapshot}\n" +
                $"Session Date:   {booking.SessionDate:MMM d, yyyy}\n" +
                "--------------------------------------\n" +
                $"Amount Paid:    P{payment.AmountPaid:N2}\n" +
                $"Change:         P{change:N2}\n" +
                $"Method:         {payment.PaymentMethod}\n" +
                $"Reference #:    {(string.IsNullOrWhiteSpace(payment.ReferenceNumber) ? "-" : payment.ReferenceNumber)}\n" +
                "--------------------------------------\n" +
                priceLines +
                $"Total Paid:     P{totalPaid:N2}\n" +
                $"Balance Due:    P{remainingBalance:N2}\n" +
                "======================================\n" +
                "         Thank you for booking!        \n" +
                "======================================";
        }

        private void PrintReceipt()
        {
            using var printDocument = new PrintDocument();
            printDocument.PrintPage += (_, e) =>
            {
                e.Graphics?.DrawString(_receiptText, new Font(FontFamily.GenericMonospace, 10), Brushes.Black, 20, 20);
            };

            using var preview = new PrintPreviewDialog
            {
                Document = printDocument,
                Width = 800,
                Height = 900,
                StartPosition = FormStartPosition.CenterParent
            };
            preview.ShowDialog(this);
        }
    }
}

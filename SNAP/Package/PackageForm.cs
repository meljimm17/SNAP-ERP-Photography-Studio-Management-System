using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP
{
    public class PackageForm : UserControl
    {
        private readonly DataGridView _grid = new();
        private readonly RoundedButton _add = new();
        private readonly RoundedButton _edit = new() { IsPrimary = false };
        private readonly RoundedButton _delete = new() { IsPrimary = false };

        public PackageForm(AccessLevel accessLevel = AccessLevel.Full)
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            AppTheme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AutoGenerateColumns = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.BackgroundColor
            };

            _add.Text = "+ Add Package";
            _edit.Text = "Edit";
            _delete.Text = "Delete";
            _add.Width = 150;
            _edit.Width = 90;
            _delete.Width = 90;
            _edit.Margin = new Padding(0, 0, 8, 0);

            _add.Click += (_, _) => AddPackage();
            _edit.Click += (_, _) => EditPackage();
            _delete.Click += (_, _) => DeletePackage();

            toolbar.Controls.Add(_edit);
            toolbar.Controls.Add(_delete);

            var header = AppTheme.CreatePageHeader("Photography Packages", "Manage photography packages, pricing, duration, and inclusions.", _add);
            Controls.Add(AppTheme.BuildPage(header, toolbar, AppTheme.WrapContent(_grid)));

            var canEdit = accessLevel is AccessLevel.Manage or AccessLevel.Full;
            _add.Enabled = canEdit;
            _edit.Enabled = canEdit;
            _delete.Enabled = canEdit;

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using var db = CreateDbContext();
                _grid.DataSource = db.PhotographyPackages
                    .Where(x => x.IsActive)
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.PackageId)
                    .ToList();
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

        private void AddPackage()
        {
            try
            {
                AddPackageCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not add this package:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddPackageCore()
        {
            using var dialog = new PackageDialogForm(existing: null);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            using var db = CreateDbContext();
            db.PhotographyPackages.Add(new PhotographyPackage
            {
                PackageName = dialog.PackageName,
                Description = dialog.PackageDescription,
                Price = dialog.Price,
                DurationHours = dialog.DurationHours,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            TenantSyncService.SaveAndSync(db);
            LoadData();

            MessageBox.Show("Package added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditPackage()
        {
            try
            {
                EditPackageCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not update this package:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditPackageCore()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var selected = (PhotographyPackage)_grid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var entity = db.PhotographyPackages.Find(selected.PackageId);
            if (entity is null) return;

            using var dialog = new PackageDialogForm(entity);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            entity.PackageName = dialog.PackageName;
            entity.Description = dialog.PackageDescription;
            entity.Price = dialog.Price;
            entity.DurationHours = dialog.DurationHours;

            TenantSyncService.SaveAndSync(db);
            LoadData();
        }

        private void DeletePackage()
        {
            try
            {
                DeletePackageCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not delete this package:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeletePackageCore()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var selected = (PhotographyPackage)_grid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var entity = db.PhotographyPackages.Find(selected.PackageId);
            if (entity is null) return;

            var bookingCount = db.Bookings.Count(b => b.PackageId == entity.PackageId);
            var message = bookingCount > 0
                ? $"This package has {bookingCount} existing booking(s). It will be removed from this list, but its booking history stays intact. Continue?"
                : $"Delete '{entity.PackageName}'? It will no longer be offered for new bookings.";

            if (MessageBox.Show(message, "Delete Package", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            entity.IsActive = false;
            TenantSyncService.SaveAndSync(db);
            LoadData();
        }
    }
}

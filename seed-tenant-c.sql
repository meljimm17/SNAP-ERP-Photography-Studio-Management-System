-- ============================================================================
-- Tenant C (CompanyCode 003) - Medium tier demo data.
--
-- Run against the tenant_c database (db70799). Safe to re-run: every insert is
-- guarded, so this tops the tenant up rather than duplicating it.
--
-- Passwords are SHA-256 hex, matching AuthenticationService.HashPassword
-- exactly (HASHBYTES over a VARCHAR literal is byte-identical to
-- SHA256.HashData(Encoding.UTF8.GetBytes(...)) for ASCII passwords).
--
-- Enum values are the C# declaration order:
--   SystemRole      SuperAdmin=0 Admin=1 Staff=2 Photographer=3 BranchManager=4 FinanceManager=5
--   UserStatus      Active=0 Inactive=1 Suspended=2
--   BookingStatus   Pending=0 Confirmed=1 Completed=2 Cancelled=3
--   EquipmentStatus Available=0 InUse=1 Maintenance=2 Retired=3
--   EquipmentCategory PhotographyGear=0 StudioFurnishing=1
--   ExpenseCategory Rent=0 Utilities=1 Supplies=2 EquipmentMaintenance=3 Other=4
--   PaymentMethod   Cash=0 GCash=1 BankTransfer=2
--   PurchaseOrderStatus Draft=0 Ordered=1 PartiallyReceived=2 Received=3 Cancelled=4
--   OwnerTransactionType Contribution=0 Withdrawal=1
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ─── Branches ───────────────────────────────────────────────────────────────
-- The AddBranchAndBranchScoping migration already created a "Main Branch" and
-- pointed any existing rows at it. Give it a real name, then add the two extra
-- locations that make this tenant worth looking at on Medium tier.
UPDATE Branches
SET BranchName = 'Davao Main Studio',
    Address    = 'Ground Floor, Abreeza Corporate Center, J.P. Laurel Ave, Davao City',
    ContactNumber = '(082) 224-1010'
WHERE IsMainBranch = 1 AND BranchName = 'Main Branch';

IF NOT EXISTS (SELECT 1 FROM Branches WHERE BranchName = 'Tagum Branch')
    INSERT INTO Branches (BranchName, Address, ContactNumber, IsMainBranch, IsActive, CreatedAt)
    VALUES ('Tagum Branch', '2F Gaisano Grand Mall, Tagum City, Davao del Norte', '(084) 216-2020', 0, 1, DATEADD(MONTH, -8, SYSUTCDATETIME()));

IF NOT EXISTS (SELECT 1 FROM Branches WHERE BranchName = 'Digos Branch')
    INSERT INTO Branches (BranchName, Address, ContactNumber, IsMainBranch, IsActive, CreatedAt)
    VALUES ('Digos Branch', 'Rizal Ave cor. Quezon St, Digos City, Davao del Sur', '(082) 553-3030', 0, 1, DATEADD(MONTH, -5, SYSUTCDATETIME()));

DECLARE @Davao INT = (SELECT BranchId FROM Branches WHERE BranchName = 'Davao Main Studio');
DECLARE @Tagum INT = (SELECT BranchId FROM Branches WHERE BranchName = 'Tagum Branch');
DECLARE @Digos INT = (SELECT BranchId FROM Branches WHERE BranchName = 'Digos Branch');

-- ─── Users ──────────────────────────────────────────────────────────────────
-- Admin and FinanceManager are company-wide, so BranchId stays NULL - that is
-- what gives them the "All Branches" view. Staff/Photographer/BranchManager are
-- pinned to one branch and every query they make is filtered to it server-side.
MERGE TenantUsers AS target
USING (VALUES
    ('Maria Santos',    'admin',    'admin123',    1, NULL,   CAST(NULL AS DECIMAL(18,2))),
    ('Ramon Villanueva','finance',  'finance123',  5, NULL,   45000.00),
    ('Grace Lim',       'bm.davao', 'branch123',   4, @Davao, 38000.00),
    ('Noel Bautista',   'bm.tagum', 'branch123',   4, @Tagum, 36000.00),
    ('Cecilia Reyes',   'bm.digos', 'branch123',   4, @Digos, 36000.00),
    ('Jenny Cruz',      'staff1',   'staff123',    2, @Davao, 22000.00),
    ('Mark Dela Torre', 'staff2',   'staff123',    2, @Tagum, 21000.00),
    ('Paolo Mendoza',   'photog1',  'photo123',    3, @Davao, 28000.00),
    ('Aira Gonzales',   'photog2',  'photo123',    3, @Tagum, 27000.00),
    ('Kevin Ong',       'photog3',  'photo123',    3, @Digos, 26000.00)
) AS source (FullName, Username, PlainPassword, Role, BranchId, MonthlySalary)
ON target.Username = source.Username
WHEN NOT MATCHED THEN
    INSERT (FullName, Username, PasswordHash, Role, CreatedAt, Status, MonthlySalary, BranchId)
    VALUES (source.FullName, source.Username,
            CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', source.PlainPassword), 2),
            source.Role, SYSUTCDATETIME(), 0, source.MonthlySalary, source.BranchId);

DECLARE @Photog1 INT = (SELECT UserId FROM TenantUsers WHERE Username = 'photog1');
DECLARE @Photog2 INT = (SELECT UserId FROM TenantUsers WHERE Username = 'photog2');
DECLARE @Photog3 INT = (SELECT UserId FROM TenantUsers WHERE Username = 'photog3');
DECLARE @AdminId INT = (SELECT UserId FROM TenantUsers WHERE Username = 'admin');

-- ─── Customers (company-wide - one list serves every branch) ────────────────
MERGE Customers AS target
USING (VALUES
    ('Andrea Bautista','09171234567','andrea.bautista@example.com','Matina, Davao City'),
    ('Jomar Aquino',   '09182345678','jomar.aquino@example.com',   'Buhangin, Davao City'),
    ('Liza Fernandez', '09193456789','liza.fernandez@example.com', 'Tagum City'),
    ('Patrick Uy',     '09204567890','patrick.uy@example.com',     'Digos City'),
    ('Nicole Ramos',   '09215678901','nicole.ramos@example.com',   'Toril, Davao City'),
    ('Elmer Castillo', '09226789012','elmer.castillo@example.com', 'Panabo City')
) AS source (FullName, ContactNumber, EmailAddress, Address)
ON target.FullName = source.FullName
WHEN NOT MATCHED THEN
    INSERT (FullName, ContactNumber, EmailAddress, Address, IsActive, CreatedAt)
    VALUES (source.FullName, source.ContactNumber, source.EmailAddress, source.Address, 1, DATEADD(MONTH, -6, SYSUTCDATETIME()));

-- ─── Packages (company-wide menu) ───────────────────────────────────────────
MERGE PhotographyPackages AS target
USING (VALUES
    ('Wedding Premium',   'Full-day coverage, 2 photographers, album and online gallery', 45000.00, 10.0),
    ('Wedding Essential', 'Half-day coverage, 1 photographer, online gallery',            28000.00,  6.0),
    ('Debut Package',     'Debut coverage with prenup shoot and printed album',           22000.00,  6.0),
    ('Family Portrait',   'Studio session, 3 outfit changes, 10 retouched photos',         6500.00,  2.0),
    ('Corporate Headshot','Studio headshots, 2 looks, 5 retouched photos',                 3500.00,  1.0),
    ('Graduation Studio', 'Toga and formal shots with printed 8R prints',                  2800.00,  1.0)
) AS source (PackageName, Description, Price, DurationHours)
ON target.PackageName = source.PackageName
WHEN NOT MATCHED THEN
    INSERT (PackageName, Description, Price, DurationHours, IsActive, CreatedAt)
    VALUES (source.PackageName, source.Description, source.Price, source.DurationHours, 1, DATEADD(MONTH, -6, SYSUTCDATETIME()));

-- ─── Suppliers (company-wide - one supplier serves every branch) ────────────
MERGE Suppliers AS target
USING (VALUES
    ('Davao Photo Supply Co.','Ana Morales',  '(082) 227-4455','Primary supplier for frames, albums and printing stock'),
    ('Mindanao Print Works',  'Ruel Sandoval','(082) 298-7788','Bulk photo printing and canvas'),
    ('CamGear Philippines',   'Ivan Tolentino','09175559090',  'Camera bodies, lenses and lighting')
) AS source (SupplierName, ContactPerson, ContactNumber, Notes)
ON target.SupplierName = source.SupplierName
WHEN NOT MATCHED THEN
    INSERT (SupplierName, ContactPerson, ContactNumber, Notes, IsActive, CreatedAt)
    VALUES (source.SupplierName, source.ContactPerson, source.ContactNumber, source.Notes, 1, DATEADD(MONTH, -6, SYSUTCDATETIME()));

DECLARE @SupPhoto INT = (SELECT SupplierId FROM Suppliers WHERE SupplierName = 'Davao Photo Supply Co.');
DECLARE @SupPrint INT = (SELECT SupplierId FROM Suppliers WHERE SupplierName = 'Mindanao Print Works');
DECLARE @SupCam   INT = (SELECT SupplierId FROM Suppliers WHERE SupplierName = 'CamGear Philippines');

-- ─── Inventory (branch-scoped - stock physically sits at one branch) ────────
-- Deliberately includes items at or below reorder level so the Low Stock
-- Alerts tab has something to show, at more than one branch.
MERGE InventoryItems AS target
USING (VALUES
    ('8R Photo Paper (100s)','Printing Supplies', 45, 20, @SupPrint,  @Davao, 1,  450.00, 260.00),
    ('A4 Photo Album',       'Albums',            12, 15, @SupPhoto,  @Davao, 1, 1200.00, 720.00),
    ('Wooden Frame 8x10',    'Frames',            30, 10, @SupPhoto,  @Davao, 1,  650.00, 380.00),
    ('Canvas Print Roll',    'Printing Supplies',  8, 10, @SupPrint,  @Davao, 0,    NULL,1850.00),
    ('8R Photo Paper (100s)','Printing Supplies', 22, 20, @SupPrint,  @Tagum, 1,  450.00, 260.00),
    ('A4 Photo Album',       'Albums',             6, 15, @SupPhoto,  @Tagum, 1, 1200.00, 720.00),
    ('Wooden Frame 8x10',    'Frames',            18, 10, @SupPhoto,  @Tagum, 1,  650.00, 380.00),
    ('8R Photo Paper (100s)','Printing Supplies', 14, 20, @SupPrint,  @Digos, 1,  450.00, 260.00),
    ('Wooden Frame 8x10',    'Frames',             9, 10, @SupPhoto,  @Digos, 1,  650.00, 380.00)
) AS source (ItemName, Category, QuantityOnHand, ReorderLevel, SupplierId, BranchId, IsForSale, SellingPrice, UnitCost)
ON target.ItemName = source.ItemName AND target.BranchId = source.BranchId
WHEN NOT MATCHED THEN
    INSERT (ItemName, Category, QuantityOnHand, ReorderLevel, SupplierId, IsActive, LastUpdatedAt, IsForSale, SellingPrice, UnitCost, BranchId)
    VALUES (source.ItemName, source.Category, source.QuantityOnHand, source.ReorderLevel, source.SupplierId, 1,
            DATEADD(DAY, -10, SYSUTCDATETIME()), source.IsForSale, source.SellingPrice, source.UnitCost, source.BranchId);

-- ─── Equipment (branch-scoped - gear physically sits at one branch) ─────────
MERGE Equipment AS target
USING (VALUES
    ('Canon EOS R6 Body',        0, @Photog1, @Davao, 0, 128000.00),
    ('Canon RF 24-70mm f/2.8',   1, @Photog1, @Davao, 0,  118000.00),
    ('Godox AD600 Strobe Kit',   0, NULL,     @Davao, 0,   42000.00),
    ('Studio Backdrop Stand',    0, NULL,     @Davao, 1,    8500.00),
    ('Sony A7 IV Body',          0, @Photog2, @Tagum, 0,  135000.00),
    ('Sony FE 85mm f/1.8',       1, @Photog2, @Tagum, 0,   32000.00),
    ('Softbox Lighting Set',     2, NULL,     @Tagum, 0,   15500.00),
    ('Nikon Z6 II Body',         0, @Photog3, @Digos, 0,  105000.00),
    ('Manfrotto Tripod',         0, NULL,     @Digos, 0,    9800.00)
) AS source (EquipmentName, Status, AssignedToUserId, BranchId, Category, AcquisitionCost)
ON target.EquipmentName = source.EquipmentName AND target.BranchId = source.BranchId
WHEN NOT MATCHED THEN
    INSERT (EquipmentName, Status, AssignedToUserId, Notes, IsActive, CreatedAt, AcquisitionCost, Category, BranchId)
    VALUES (source.EquipmentName, source.Status, source.AssignedToUserId, NULL, 1,
            DATEADD(MONTH, -7, SYSUTCDATETIME()), source.AcquisitionCost, source.Category, source.BranchId);

-- ─── Bookings + Payments (branch-scoped) ────────────────────────────────────
-- Spread across the last three months so the Revenue-by-Month chart has a real
-- trend, and left deliberately part-paid in places so Outstanding Payments and
-- the Balance Sheet's Accounts Receivable are non-zero.
DECLARE @Bookings TABLE (RowKey NVARCHAR(100), CustomerName NVARCHAR(200), PackageName NVARCHAR(200),
                         BranchId INT, PhotographerId INT, DaysAgo INT, Status INT, PaidRatio DECIMAL(5,2));
INSERT INTO @Bookings VALUES
    ('TC-B01','Andrea Bautista','Wedding Premium',   @Davao, @Photog1, 62, 2, 1.00),
    ('TC-B02','Jomar Aquino',   'Family Portrait',   @Davao, @Photog1, 48, 2, 1.00),
    ('TC-B03','Nicole Ramos',   'Debut Package',     @Davao, @Photog1, 30, 2, 0.50),
    ('TC-B04','Elmer Castillo', 'Corporate Headshot',@Davao, @Photog1, 12, 2, 1.00),
    ('TC-B05','Andrea Bautista','Graduation Studio', @Davao, @Photog1, -8, 1, 0.00),
    ('TC-B06','Liza Fernandez', 'Wedding Essential', @Tagum, @Photog2, 55, 2, 1.00),
    ('TC-B07','Liza Fernandez', 'Family Portrait',   @Tagum, @Photog2, 25, 2, 0.60),
    ('TC-B08','Jomar Aquino',   'Graduation Studio', @Tagum, @Photog2,  9, 2, 1.00),
    ('TC-B09','Nicole Ramos',   'Corporate Headshot',@Tagum, @Photog2,-14, 0, 0.00),
    ('TC-B10','Patrick Uy',     'Debut Package',     @Digos, @Photog3, 40, 2, 1.00),
    ('TC-B11','Patrick Uy',     'Family Portrait',   @Digos, @Photog3, 18, 2, 0.40),
    ('TC-B12','Elmer Castillo', 'Graduation Studio', @Digos, @Photog3, -5, 1, 0.00);

INSERT INTO Bookings (CustomerId, PackageId, AssignedPhotographerUserId, PackageNameSnapshot, PriceSnapshot,
                      DurationSnapshot, SessionDate, SessionStartTime, SessionEndTime, Location, Status, Notes, CreatedAt, BranchId)
SELECT c.CustomerId, p.PackageId, b.PhotographerId, p.PackageName, p.Price, p.DurationHours,
       CAST(DATEADD(DAY, -b.DaysAgo, SYSUTCDATETIME()) AS DATE), '09:00:00', '15:00:00',
       br.BranchName, b.Status, b.RowKey, DATEADD(DAY, -b.DaysAgo - 7, SYSUTCDATETIME()), b.BranchId
FROM @Bookings b
JOIN Customers c ON c.FullName = b.CustomerName
JOIN PhotographyPackages p ON p.PackageName = b.PackageName
JOIN Branches br ON br.BranchId = b.BranchId
WHERE NOT EXISTS (SELECT 1 FROM Bookings x WHERE x.Notes = b.RowKey);

INSERT INTO Payments (BookingId, AmountPaid, PaymentMethod, PaymentDate, ReferenceNumber, CreatedAt, IsDeleted)
SELECT bk.BookingId,
       CAST(bk.PriceSnapshot * b.PaidRatio AS DECIMAL(18,2)),
       CASE WHEN b.BranchId = @Davao THEN 1 WHEN b.BranchId = @Tagum THEN 0 ELSE 2 END,
       CAST(DATEADD(DAY, -b.DaysAgo, SYSUTCDATETIME()) AS DATE),
       'OR-' + b.RowKey, DATEADD(DAY, -b.DaysAgo, SYSUTCDATETIME()), 0
FROM @Bookings b
JOIN Bookings bk ON bk.Notes = b.RowKey
WHERE b.PaidRatio > 0
  AND NOT EXISTS (SELECT 1 FROM Payments x WHERE x.ReferenceNumber = 'OR-' + b.RowKey);

-- ─── Product sales (branch-scoped, second revenue stream) ───────────────────
DECLARE @Sales TABLE (RowKey NVARCHAR(100), ItemName NVARCHAR(200), BranchId INT, Qty DECIMAL(18,2), DaysAgo INT, Method INT);
INSERT INTO @Sales VALUES
    ('TC-S01','Wooden Frame 8x10',    @Davao, 4, 20, 0),
    ('TC-S02','A4 Photo Album',       @Davao, 2, 11, 1),
    ('TC-S03','8R Photo Paper (100s)',@Davao, 3,  6, 0),
    ('TC-S04','Wooden Frame 8x10',    @Tagum, 3, 16, 0),
    ('TC-S05','A4 Photo Album',       @Tagum, 1,  8, 1),
    ('TC-S06','Wooden Frame 8x10',    @Digos, 2, 13, 0);

INSERT INTO ProductSales (InventoryId, CustomerId, BookingId, Quantity, UnitPrice, TotalAmount, SaleDate, PaymentMethod, IsDeleted, CreatedAt, BranchId)
SELECT i.InventoryId, NULL, NULL, s.Qty, i.SellingPrice, s.Qty * i.SellingPrice,
       CAST(DATEADD(DAY, -s.DaysAgo, SYSUTCDATETIME()) AS DATE), s.Method, 0,
       DATEADD(DAY, -s.DaysAgo, SYSUTCDATETIME()), s.BranchId
FROM @Sales s
JOIN InventoryItems i ON i.ItemName = s.ItemName AND i.BranchId = s.BranchId
WHERE NOT EXISTS (
    SELECT 1 FROM ProductSales x
    WHERE x.BranchId = s.BranchId AND x.InventoryId = i.InventoryId
      AND x.SaleDate = CAST(DATEADD(DAY, -s.DaysAgo, SYSUTCDATETIME()) AS DATE));

-- ─── Expenses (branch-scoped - each branch's own operating costs) ───────────
-- A mix of paid and unpaid: unpaid ones become Accounts Payable on the Balance
-- Sheet, so the statement has both sides to show.
DECLARE @Expenses TABLE (RowKey NVARCHAR(100), Category INT, Descr NVARCHAR(300), Amount DECIMAL(18,2),
                         BranchId INT, DaysAgo INT, IsPaid BIT);
INSERT INTO @Expenses VALUES
    ('TC-E01',0,'Studio rent - Abreeza',            35000.00, @Davao, 25, 1),
    ('TC-E02',1,'Davao Light electricity bill',      8400.00, @Davao, 18, 1),
    ('TC-E03',2,'Printing consumables restock',      6200.00, @Davao, 10, 1),
    ('TC-E04',3,'Camera sensor cleaning service',    3500.00, @Davao,  4, 0),
    ('TC-E05',0,'Stall rent - Gaisano Tagum',       18000.00, @Tagum, 24, 1),
    ('TC-E06',1,'Electricity and water',             4300.00, @Tagum, 16, 1),
    ('TC-E07',4,'Local advertising tarpaulins',      2500.00, @Tagum,  7, 0),
    ('TC-E08',0,'Studio rent - Digos',              14000.00, @Digos, 23, 1),
    ('TC-E09',1,'Electricity bill',                  3100.00, @Digos, 15, 1),
    ('TC-E10',2,'Frames and album restock',          4800.00, @Digos,  6, 0);

INSERT INTO Expenses (Category, Description, Amount, ExpenseDate, SupplierId, EquipmentId, CreatedAt, IsDeleted, IsPaid, PaidDate, PurchaseOrderId, BranchId)
SELECT e.Category, e.Descr, e.Amount, CAST(DATEADD(DAY, -e.DaysAgo, SYSUTCDATETIME()) AS DATE),
       NULL, NULL, DATEADD(DAY, -e.DaysAgo, SYSUTCDATETIME()), 0, e.IsPaid,
       CASE WHEN e.IsPaid = 1 THEN CAST(DATEADD(DAY, -e.DaysAgo + 2, SYSUTCDATETIME()) AS DATE) END,
       NULL, e.BranchId
FROM @Expenses e
WHERE NOT EXISTS (SELECT 1 FROM Expenses x WHERE x.Description = e.Descr AND x.BranchId = e.BranchId);

-- ─── Purchase orders (branch-scoped; suppliers stay company-wide) ───────────
IF NOT EXISTS (SELECT 1 FROM PurchaseOrders WHERE Notes = 'TC-PO01')
BEGIN
    INSERT INTO PurchaseOrders (SupplierId, Status, OrderDate, ExpectedDate, CreatedByUserId, ReceivedByUserId, Notes, CreatedAt, BranchId)
    VALUES (@SupPrint, 3, CAST(DATEADD(DAY, -21, SYSUTCDATETIME()) AS DATE), CAST(DATEADD(DAY, -14, SYSUTCDATETIME()) AS DATE),
            @AdminId, @AdminId, 'TC-PO01', DATEADD(DAY, -21, SYSUTCDATETIME()), @Davao);

    INSERT INTO PurchaseOrderLines (PurchaseOrderId, InventoryId, QuantityOrdered, QuantityReceived, UnitCost)
    SELECT SCOPE_IDENTITY(), i.InventoryId, 50, 50, 260.00
    FROM InventoryItems i WHERE i.ItemName = '8R Photo Paper (100s)' AND i.BranchId = @Davao;
END

IF NOT EXISTS (SELECT 1 FROM PurchaseOrders WHERE Notes = 'TC-PO02')
BEGIN
    INSERT INTO PurchaseOrders (SupplierId, Status, OrderDate, ExpectedDate, CreatedByUserId, ReceivedByUserId, Notes, CreatedAt, BranchId)
    VALUES (@SupPhoto, 1, CAST(DATEADD(DAY, -5, SYSUTCDATETIME()) AS DATE), CAST(DATEADD(DAY, 4, SYSUTCDATETIME()) AS DATE),
            @AdminId, NULL, 'TC-PO02', DATEADD(DAY, -5, SYSUTCDATETIME()), @Tagum);

    INSERT INTO PurchaseOrderLines (PurchaseOrderId, InventoryId, QuantityOrdered, QuantityReceived, UnitCost)
    SELECT SCOPE_IDENTITY(), i.InventoryId, 20, 0, 720.00
    FROM InventoryItems i WHERE i.ItemName = 'A4 Photo Album' AND i.BranchId = @Tagum;
END

IF NOT EXISTS (SELECT 1 FROM PurchaseOrders WHERE Notes = 'TC-PO03')
BEGIN
    INSERT INTO PurchaseOrders (SupplierId, Status, OrderDate, ExpectedDate, CreatedByUserId, ReceivedByUserId, Notes, CreatedAt, BranchId)
    VALUES (@SupCam, 0, CAST(DATEADD(DAY, -2, SYSUTCDATETIME()) AS DATE), NULL,
            @AdminId, NULL, 'TC-PO03', DATEADD(DAY, -2, SYSUTCDATETIME()), @Digos);

    INSERT INTO PurchaseOrderLines (PurchaseOrderId, InventoryId, QuantityOrdered, QuantityReceived, UnitCost)
    SELECT SCOPE_IDENTITY(), i.InventoryId, 15, 0, 380.00
    FROM InventoryItems i WHERE i.ItemName = 'Wooden Frame 8x10' AND i.BranchId = @Digos;
END

-- ─── Payroll (company-wide - no branch, by design) ──────────────────────────
-- Philippine statutory deductions, last full month, for the salaried staff.
DECLARE @PeriodStart DATE = DATEADD(DAY, 1, EOMONTH(SYSUTCDATETIME(), -2));
DECLARE @PeriodEnd   DATE = EOMONTH(SYSUTCDATETIME(), -1);

INSERT INTO SalaryPayments (TenantUserId, GrossAmount, SssDeduction, PhilHealthDeduction, PagIbigDeduction,
                            WithholdingTax, NetAmount, PayPeriodStart, PayPeriodEnd, PaymentDate, Notes,
                            CreatedAt, IsDeleted, IsPaid, PaidDate)
SELECT u.UserId,
       u.MonthlySalary,
       CAST(u.MonthlySalary * 0.045 AS DECIMAL(18,2)),
       CAST(u.MonthlySalary * 0.025 AS DECIMAL(18,2)),
       100.00,
       CAST(u.MonthlySalary * 0.08  AS DECIMAL(18,2)),
       u.MonthlySalary
         - CAST(u.MonthlySalary * 0.045 AS DECIMAL(18,2))
         - CAST(u.MonthlySalary * 0.025 AS DECIMAL(18,2))
         - 100.00
         - CAST(u.MonthlySalary * 0.08  AS DECIMAL(18,2)),
       @PeriodStart, @PeriodEnd, @PeriodEnd, 'Seeded payroll run',
       SYSUTCDATETIME(), 0, 1, @PeriodEnd
FROM TenantUsers u
WHERE u.MonthlySalary IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM SalaryPayments s WHERE s.TenantUserId = u.UserId AND s.PayPeriodEnd = @PeriodEnd);

-- ─── Owner transactions (company-wide - no branch, by design) ───────────────
MERGE OwnerTransactions AS target
USING (VALUES
    (0, 500000.00, 8, 'Initial capital to open Davao Main Studio'),
    (0, 250000.00, 5, 'Additional capital for Digos Branch build-out'),
    (1,  60000.00, 2, 'Owner drawing')
) AS source (Type, Amount, MonthsAgo, Notes)
ON target.Notes = source.Notes
WHEN NOT MATCHED THEN
    INSERT (Type, Amount, TransactionDate, Notes, CreatedAt)
    VALUES (source.Type, source.Amount,
            CAST(DATEADD(MONTH, -source.MonthsAgo, SYSUTCDATETIME()) AS DATE),
            source.Notes, SYSUTCDATETIME());

COMMIT TRANSACTION;

-- ─── Summary ────────────────────────────────────────────────────────────────
SELECT 'Branches' AS Entity, COUNT(*) AS Rows FROM Branches
UNION ALL SELECT 'TenantUsers',       COUNT(*) FROM TenantUsers
UNION ALL SELECT 'Customers',         COUNT(*) FROM Customers
UNION ALL SELECT 'Packages',          COUNT(*) FROM PhotographyPackages
UNION ALL SELECT 'Suppliers',         COUNT(*) FROM Suppliers
UNION ALL SELECT 'InventoryItems',    COUNT(*) FROM InventoryItems
UNION ALL SELECT 'Equipment',         COUNT(*) FROM Equipment
UNION ALL SELECT 'Bookings',          COUNT(*) FROM Bookings
UNION ALL SELECT 'Payments',          COUNT(*) FROM Payments
UNION ALL SELECT 'ProductSales',      COUNT(*) FROM ProductSales
UNION ALL SELECT 'Expenses',          COUNT(*) FROM Expenses
UNION ALL SELECT 'PurchaseOrders',    COUNT(*) FROM PurchaseOrders
UNION ALL SELECT 'SalaryPayments',    COUNT(*) FROM SalaryPayments
UNION ALL SELECT 'OwnerTransactions', COUNT(*) FROM OwnerTransactions;

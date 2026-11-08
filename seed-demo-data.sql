-- ============================================================================
-- Demo data for ANY tenant database - fills the Dashboard / Reports KPI cards
-- and every chart (Revenue by Month, Bookings by Status, Expenses by Category,
-- Equipment Status, Low Stock, Net Income by Branch) with a modest, realistic
-- photography-studio dataset: ~6 months of history plus the current month.
--
-- Run it against each LOCAL tenant DB ONLY, e.g.
--   sqlcmd -S .\SNAPERP -E -C -d TenantErp_TenantA -i seed-demo-data.sql
-- NEVER run it directly against a MonsterASP database. Sync copies rows by id,
-- so a remote seeded on its own hands out different ids and every later push
-- that references them is rejected. The script queues its rows in the sync
-- outbox instead, and the running app pushes them with matching ids.
--
-- Safe to re-run: every insert is guarded on a natural key, so a second run
-- adds nothing. Dates are relative to the day it is run (Philippine time), so
-- the "this month" figures are never empty right after seeding.
--
-- Branch-aware without being tenant-specific: rows carry a branch "slot"
-- (0 = main branch, 1/2 = the next active branches). Bookings and walk-in
-- sales fold onto the branches that exist; branch-owned assets and running
-- costs (inventory, equipment, rent, bills) for a slot the tenant doesn't have
-- are skipped instead, so a single-branch studio never pays rent twice.
--
-- Enum values (C# declaration order):
--   SystemRole      Admin=1 Staff=2 Photographer=3 BranchManager=4 FinanceManager=5
--   BookingStatus   Pending=0 Confirmed=1 Completed=2 Cancelled=3
--   EquipmentStatus Available=0 InUse=1 Maintenance=2 Retired=3
--   EquipmentCategory PhotographyGear=0 StudioFurnishing=1
--   ExpenseCategory Rent=0 Utilities=1 Supplies=2 EquipmentMaintenance=3 Other=4
--   PaymentMethod   Cash=0 GCash=1 BankTransfer=2
--   OwnerTransactionType Contribution=0 Withdrawal=1
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Today      DATE = CAST(DATEADD(HOUR, 8, SYSUTCDATETIME()) AS DATE);
DECLARE @MonthStart DATE = DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1);
DECLARE @Now        DATETIME2 = SYSUTCDATETIME();

-- ─── Branch slots ───────────────────────────────────────────────────────────
DECLARE @Br TABLE (Slot INT PRIMARY KEY, BranchId INT, BranchName NVARCHAR(200));
INSERT INTO @Br
SELECT ROW_NUMBER() OVER (ORDER BY IsMainBranch DESC, BranchId) - 1, BranchId, BranchName
FROM Branches WHERE IsActive = 1;
DECLARE @BranchCount INT = (SELECT COUNT(*) FROM @Br);
IF @BranchCount = 0 THROW 50000, 'Tenant has no active branch - run migrations first.', 1;

-- ─── Staff (only when the studio has no photographer at all) ────────────────
-- Bookings need someone to shoot them and payroll needs salaried staff. SHA-256
-- hex matches AuthenticationService.HashPassword for ASCII passwords.
IF NOT EXISTS (SELECT 1 FROM TenantUsers WHERE Role = 3)
BEGIN
    DECLARE @Main INT = (SELECT BranchId FROM @Br WHERE Slot = 0);
    INSERT INTO TenantUsers (FullName, Username, PasswordHash, Role, CreatedAt, Status, MonthlySalary, BranchId)
    SELECT v.FullName, v.Username, CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', v.Pwd), 2), v.Role,
           DATEADD(MONTH, -6, @Now), 0, v.Salary, @Main
    FROM (VALUES
        ('Carlo Mendoza', 'photog1', 'photo123', 3, 26000.00),
        ('Bea Navarro',   'staff1',  'staff123', 2, 18500.00)
    ) v (FullName, Username, Pwd, Role, Salary)
    WHERE NOT EXISTS (SELECT 1 FROM TenantUsers u WHERE u.Username = v.Username);
END

-- ─── Customers (company-wide) ───────────────────────────────────────────────
MERGE Customers AS t
USING (VALUES
    ('Kristine Mae Dela Cruz', '09171234501', 'kristine.delacruz@gmail.com',  'Matina Aplaya, Davao City'),
    ('Jerome Villanueva',      '09181234502', 'jerome.villanueva@yahoo.com',  'Lanang, Davao City'),
    ('Angelica Ramos',         '09191234503', 'angelica.ramos@gmail.com',     'Buhangin, Davao City'),
    ('Joanna Santiago',        '09201234504', 'joanna.santiago@gmail.com',    'Catalunan Grande, Davao City'),
    ('Mark Anthony Gonzaga',   '09211234505', 'markgonzaga@gmail.com',        'Bajada, Davao City'),
    ('Princess Joy Alcantara', '09221234506', 'pjalcantara@gmail.com',        'Panabo City, Davao del Norte'),
    ('Dennis Uy',              '09231234507', 'dennis.uy@uyholdings.ph',      'Damosa, Lanang, Davao City'),
    ('Lovely Grace Tan',       '09241234508', 'lovelygrace.tan@gmail.com',    'Toril, Davao City'),
    ('Christian Paul Bautista','09251234509', 'cp.bautista@gmail.com',        'Mintal, Davao City'),
    ('Kimberly Rose Lim',      '09261234510', 'kimberly.lim@gmail.com',       'Obrero, Davao City')
) AS s (FullName, ContactNumber, EmailAddress, Address)
ON t.FullName = s.FullName
WHEN NOT MATCHED THEN
    INSERT (FullName, ContactNumber, EmailAddress, Address, IsActive, CreatedAt)
    VALUES (s.FullName, s.ContactNumber, s.EmailAddress, s.Address, 1, DATEADD(MONTH, -6, @Now));

-- ─── Packages (company-wide menu) ───────────────────────────────────────────
MERGE PhotographyPackages AS t
USING (VALUES
    ('Wedding Full-Day Coverage', 'Prep to reception, 2 photographers, 300+ edited photos, 12x12 layflat album', 48000.00, 10.0),
    ('Prenup Photoshoot',         'On-location shoot, 2 outfits, 60 edited photos, 1-min teaser video',          15000.00,  4.0),
    ('Debut Coverage',            'Program coverage incl. 18 roses/candles, 150 edited photos, photo album',     22000.00,  6.0),
    ('Birthday Party Coverage',   'Event coverage, 100 edited photos, online gallery',                            8500.00,  3.0),
    ('Family Studio Session',     'Studio session up to 8 pax, 3 backdrops, 15 retouched photos, 1 framed 8x10',  4500.00,  2.0),
    ('Baby & Maternity Session',  'Studio session with props and outfits, 12 retouched photos',                   5500.00,  2.0),
    ('Corporate Headshots',       'Studio or on-site headshots, up to 5 pax, 2 retouched photos each',            6000.00,  2.0),
    ('Graduation Toga Shoot',     'Toga + formal attire, 5 retouched photos, 2 printed 8R',                       1800.00,  1.0)
) AS s (PackageName, Description, Price, DurationHours)
ON t.PackageName = s.PackageName
WHEN NOT MATCHED THEN
    INSERT (PackageName, Description, Price, DurationHours, IsActive, CreatedAt)
    VALUES (s.PackageName, s.Description, s.Price, s.DurationHours, 1, DATEADD(MONTH, -6, @Now));

-- ─── Suppliers (company-wide) ───────────────────────────────────────────────
MERGE Suppliers AS t
USING (VALUES
    ('Davao Photo Supply Co.', 'Ana Morales',    '(082) 227-4455', 'Frames, albums and photo paper'),
    ('Mindanao Print Works',   'Ruel Sandoval',  '(082) 298-7788', 'Ink, bulk printing and canvas'),
    ('CamGear Philippines',    'Ivan Tolentino', '09175559090',    'Camera bodies, lenses, lighting and repairs')
) AS s (SupplierName, ContactPerson, ContactNumber, Notes)
ON t.SupplierName = s.SupplierName
WHEN NOT MATCHED THEN
    INSERT (SupplierName, ContactPerson, ContactNumber, Notes, IsActive, CreatedAt)
    VALUES (s.SupplierName, s.ContactPerson, s.ContactNumber, s.Notes, 1, DATEADD(MONTH, -6, @Now));

DECLARE @SupPhoto INT = (SELECT MIN(SupplierId) FROM Suppliers WHERE SupplierName = 'Davao Photo Supply Co.');
DECLARE @SupPrint INT = (SELECT MIN(SupplierId) FROM Suppliers WHERE SupplierName = 'Mindanao Print Works');
DECLARE @SupCam   INT = (SELECT MIN(SupplierId) FROM Suppliers WHERE SupplierName = 'CamGear Philippines');

-- ─── Inventory (branch-scoped consumables / retail) ─────────────────────────
-- Several sit at or below reorder level so the Low Stock chart has content.
INSERT INTO InventoryItems (ItemName, Category, QuantityOnHand, ReorderLevel, SupplierId, IsActive, LastUpdatedAt, IsForSale, SellingPrice, UnitCost, BranchId)
SELECT v.ItemName, v.Category, v.Qty, v.Reorder, v.SupplierId, 1, DATEADD(DAY, -3, @Now), v.ForSale, v.Price, v.Cost, br.BranchId
FROM (VALUES
    (0, '4R Photo Paper Glossy (500 sheets)', 'Printing Supplies', 6,  5,  @SupPrint, 0, NULL,     980.00),
    (0, '8R Photo Paper Luster (100 sheets)', 'Printing Supplies', 14, 10, @SupPrint, 0, NULL,     620.00),
    (0, 'Epson L8050 Ink Set (6 colors)',     'Printing Supplies', 2,  3,  @SupPrint, 0, NULL,    2340.00),
    (0, 'Instax Mini Film (Twin Pack)',       'Instant Film',      8,  12, @SupPhoto, 1, 650.00,   480.00),
    (0, 'Wooden Frame 8x10 (Black)',          'Frames',            24, 10, @SupPhoto, 1, 650.00,   320.00),
    (0, 'Glass Frame 11x14 (Gold Trim)',      'Frames',            3,  6,  @SupPhoto, 1, 1250.00,  700.00),
    (0, 'Leather Photo Album 12x12',          'Albums',            9,  5,  @SupPhoto, 1, 3500.00, 2100.00),
    (0, 'Branded USB Flash Drive 32GB',       'Packaging & Media', 35, 20, @SupPhoto, 1, 450.00,   210.00),
    (0, 'Seamless Backdrop Paper 2.7m (Gray)','Backdrops',         1,  2,  @SupPhoto, 0, NULL,    3200.00),
    (1, '4R Photo Paper Glossy (500 sheets)', 'Printing Supplies', 4,  5,  @SupPrint, 0, NULL,     980.00),
    (1, 'Instax Mini Film (Twin Pack)',       'Instant Film',      15, 12, @SupPhoto, 1, 650.00,   480.00),
    (1, 'Wooden Frame 8x10 (Black)',          'Frames',            12, 10, @SupPhoto, 1, 650.00,   320.00),
    (1, 'Leather Photo Album 12x12',          'Albums',            2,  4,  @SupPhoto, 1, 3500.00, 2100.00),
    (2, '4R Photo Paper Glossy (500 sheets)', 'Printing Supplies', 7,  5,  @SupPrint, 0, NULL,     980.00),
    (2, 'Wooden Frame 8x10 (Black)',          'Frames',            5,  8,  @SupPhoto, 1, 650.00,   320.00),
    (2, 'Branded USB Flash Drive 32GB',       'Packaging & Media', 18, 15, @SupPhoto, 1, 450.00,   210.00)
) v (Slot, ItemName, Category, Qty, Reorder, SupplierId, ForSale, Price, Cost)
JOIN @Br br ON br.Slot = v.Slot
WHERE NOT EXISTS (SELECT 1 FROM InventoryItems i WHERE i.ItemName = v.ItemName AND i.BranchId = br.BranchId);

-- ─── Equipment (branch-scoped) - gear and studio furnishings ────────────────
-- Status mix covers Available / In Use / Maintenance / Retired for the donut.
INSERT INTO Equipment (EquipmentName, Status, AssignedToUserId, Notes, IsActive, CreatedAt, AcquisitionCost, Category, BranchId)
SELECT v.EquipmentName, v.Status,
       CASE WHEN v.Status = 1 AND v.Category = 0 THEN ph.UserId END,
       v.Notes, 1, DATEADD(MONTH, -v.AgeMonths, @Now), v.Cost, v.Category, br.BranchId
FROM (VALUES
    -- Photography gear
    (0, 'Sony A7 IV Mirrorless Body',          0, 1, 135000.00, 14, 'Main shooter body, SN 4521873'),
    (0, 'Sony FE 24-70mm f/2.8 GM II',         0, 1, 128000.00, 14, NULL),
    (0, 'Sigma 85mm f/1.4 DG DN Art',          0, 0,  62000.00, 10, 'Portrait lens'),
    (0, 'Godox AD600 Pro Strobe',              0, 0,  46000.00, 12, NULL),
    (0, 'Godox SL-150W II LED Light',          0, 2,   9800.00, 18, 'Fan noise - sent to CamGear for service'),
    (0, 'Octa Softbox 120cm with Grid',        0, 0,   4500.00, 12, NULL),
    (0, 'Backdrop Support System 3m',          0, 0,   6500.00, 20, NULL),
    (0, 'Canon EOS 6D (2016 body)',            0, 3,  45000.00, 60, 'Shutter count past 200k - retired, kept for parts'),
    -- Studio furnishing
    (0, 'Velvet Posing Sofa (3-seater)',       1, 0,  28000.00, 16, 'Emerald green, used for family sets'),
    (0, 'Hollywood Vanity Mirror with Lights', 1, 0,  12500.00, 16, 'Makeup / dressing area'),
    (0, 'Wardrobe Garment Rack',               1, 0,   3800.00, 16, NULL),
    (0, 'Client Lounge Chairs (set of 4)',     1, 0,  14000.00, 16, 'Reception waiting area'),
    (0, 'Carrier Inverter Aircon 2.0HP',       1, 2,  48000.00, 24, 'Annual cleaning and freon recharge'),
    (0, 'Editing Workstation Desk and Chair',  1, 1,  18500.00, 14, NULL),
    (1, 'Canon EOS R6 Mark II Body',           0, 1, 129000.00,  8, NULL),
    (1, 'Canon RF 50mm f/1.8 STM',             0, 0,  12500.00,  8, NULL),
    (1, 'Godox SK400 II Studio Flash Kit',     0, 0,  21000.00,  8, NULL),
    (1, 'Rattan Peacock Chair (prop)',         1, 0,   7500.00,  8, 'Maternity and baby sets'),
    (1, 'Changing Room Partition',             1, 0,   5200.00,  8, NULL),
    (2, 'Nikon Z6 II Body',                    0, 1, 105000.00,  5, NULL),
    (2, 'Neewer 660 LED Panel (pair)',         0, 0,   8900.00,  5, NULL),
    (2, 'Wooden Bench Prop (vintage)',         1, 0,   4800.00,  5, NULL),
    (2, 'Reception Counter and Cabinet',       1, 0,  35000.00,  5, NULL)
) v (Slot, EquipmentName, Category, Status, Cost, AgeMonths, Notes)
JOIN @Br br ON br.Slot = v.Slot
OUTER APPLY (
    SELECT TOP 1 u.UserId FROM TenantUsers u
    WHERE u.Role = 3 AND u.Status = 0
    ORDER BY CASE WHEN u.BranchId = br.BranchId THEN 0 WHEN u.BranchId IS NULL THEN 1 ELSE 2 END, u.UserId
) ph
WHERE NOT EXISTS (SELECT 1 FROM Equipment e WHERE e.EquipmentName = v.EquipmentName AND e.BranchId = br.BranchId);

-- ─── Bookings + Payments ────────────────────────────────────────────────────
-- M = month offset from the current month. For past months D is the day of the
-- month; for the current month D is "days before today" (negative = upcoming),
-- clamped so past sessions never slip into last month.
-- Payments follow how PH studios actually collect: a downpayment DpLead days
-- before the session, the balance on the shoot day. A few balances are left
-- short so Outstanding Payments / Accounts Receivable are non-zero.
DECLARE @B TABLE (Seq INT, M INT, D INT, Customer NVARCHAR(200), Package NVARCHAR(200), Slot INT, Status INT,
                  StartTime TIME, Location NVARCHAR(300), Notes NVARCHAR(500),
                  DpRatio DECIMAL(5,2), BalRatio DECIMAL(5,2), DpMethod INT, BalMethod INT, DpLead INT);
INSERT INTO @B VALUES
    ( 1,-5,10,'Kristine Mae Dela Cruz','Prenup Photoshoot',        0,2,'06:00',N'Eden Nature Park, Toril, Davao City',            N'Sunrise shoot, 2 outfit changes',                 0.50,0.50,1,1,30),
    ( 2,-5,24,'Jerome Villanueva',     'Corporate Headshots',      1,2,'10:00',NULL,                                              N'5 pax, white background',                         0.00,1.00,0,2,0),
    ( 3,-4, 8,'Angelica Ramos',        'Debut Coverage',           0,2,'17:00',N'Marco Polo Hotel Ballroom, Davao City',          N'18 roses and 18 candles segments',                0.50,0.50,1,0,30),
    ( 4,-4,19,'Joanna Santiago',       'Family Studio Session',    2,2,'14:00',NULL,                                              N'7 pax incl. grandparents',                        0.00,1.00,0,0,0),
    ( 5,-3, 6,'Kristine Mae Dela Cruz','Wedding Full-Day Coverage',0,2,'08:00',N'San Pedro Cathedral, Davao City',                N'Reception at The Ritz Hotel at Garden Oases',     0.50,0.50,2,2,60),
    ( 6,-3,15,'Mark Anthony Gonzaga',  'Graduation Toga Shoot',    1,2,'09:00',NULL,                                              N'UM graduate - add 1 extra 8R print',              0.00,1.00,0,1,0),
    ( 7,-3,27,'Princess Joy Alcantara','Baby & Maternity Session', 0,3,'10:00',NULL,                                              N'Cancelled - client delivered early',              0.00,0.00,0,0,0),
    ( 8,-2, 3,'Dennis Uy',             'Corporate Headshots',      0,2,'13:00',N'Uy Holdings Office, Damosa, Davao City',         N'On-site, bring portable backdrop',                0.00,1.00,0,2,0),
    ( 9,-2,13,'Angelica Ramos',        'Birthday Party Coverage',  1,2,'15:00',N'Seda Abreeza, Davao City',                       N'Sister''s 7th birthday',                          0.50,0.50,1,1,14),
    (10,-2,26,'Lovely Grace Tan',      'Prenup Photoshoot',        2,2,'05:30',N'Paradise Island Beach Resort, Samal',            N'Boat transfer shouldered by client',              0.50,0.30,1,1,21),
    (11,-1, 5,'Mark Anthony Gonzaga',  'Family Studio Session',    0,2,'10:00',NULL,                                              N'Christmas card photos',                           0.00,1.00,0,0,0),
    (12,-1,14,'Joanna Santiago',       'Baby & Maternity Session', 1,2,'09:00',NULL,                                              N'Newborn, 2 weeks old',                            0.50,0.50,1,0,14),
    (13,-1,20,'Christian Paul Bautista','Debut Coverage',          0,3,'17:00',N'The Pinnacle Hotel, Davao City',                 N'Cancelled - event moved, no downpayment made',    0.00,0.00,0,0,0),
    (14,-1,27,'Lovely Grace Tan',      'Wedding Full-Day Coverage',0,2,'07:00',N'Davao Cathedral / Pearl Farm Beach Resort',      N'Balance due on album delivery',                   0.50,0.25,2,2,60),
    (15, 0, 4,'Princess Joy Alcantara','Graduation Toga Shoot',    0,2,'10:00',NULL,                                              N'Nursing grad, white uniform + toga',              0.00,1.00,0,1,0),
    (16, 0, 2,'Dennis Uy',             'Family Studio Session',    1,2,'15:00',NULL,                                              N'Family of 5 with pet dog',                        0.50,0.50,1,0,3),
    (17, 0,-5,'Christian Paul Bautista','Birthday Party Coverage', 2,1,'16:00',N'Jollibee Party Room, Matina, Davao City',        N'1st birthday, superhero theme',                   0.50,0.00,1,0,7),
    (18, 0,-12,'Jerome Villanueva',    'Prenup Photoshoot',        0,1,'06:00',N'Philippine Eagle Center, Malagos, Davao City',   N'Rain date on standby',                            0.50,0.00,2,0,14),
    (19, 0,-19,'Kimberly Rose Lim',    'Debut Coverage',           1,0,'17:00',N'Grand Regal Hotel, Davao City',                  N'Awaiting downpayment to confirm date',            0.00,0.00,0,0,0);

DECLARE @BX TABLE (Seq INT, SessionDate DATE, BranchId INT, BranchName NVARCHAR(200));
INSERT INTO @BX
SELECT b.Seq,
       CASE WHEN b.M < 0 THEN DATEADD(DAY, b.D - 1, DATEADD(MONTH, b.M, @MonthStart))
            WHEN b.D >= 0 AND DATEADD(DAY, -b.D, @Today) < @MonthStart THEN @MonthStart
            ELSE DATEADD(DAY, -b.D, @Today) END,
       br.BranchId, br.BranchName
FROM @B b
JOIN @Br br ON br.Slot = b.Slot % @BranchCount;

INSERT INTO Bookings (CustomerId, PackageId, AssignedPhotographerUserId, PackageNameSnapshot, PriceSnapshot,
                      DurationSnapshot, SessionDate, SessionStartTime, SessionEndTime, Location, Status, Notes, CreatedAt, BranchId)
SELECT c.CustomerId, p.PackageId, ph.UserId, p.PackageName, p.Price, p.DurationHours,
       x.SessionDate, b.StartTime, CAST(DATEADD(MINUTE, CAST(p.DurationHours * 60 AS INT), CAST(b.StartTime AS DATETIME)) AS TIME),
       COALESCE(b.Location, x.BranchName + N' (studio)'), b.Status, b.Notes,
       DATEADD(DAY, -(b.DpLead + 5), CAST(x.SessionDate AS DATETIME2)), x.BranchId
FROM @B b
JOIN @BX x ON x.Seq = b.Seq
JOIN Customers c ON c.CustomerId = (SELECT MIN(CustomerId) FROM Customers WHERE FullName = b.Customer)
JOIN PhotographyPackages p ON p.PackageId = (SELECT MIN(PackageId) FROM PhotographyPackages WHERE PackageName = b.Package)
OUTER APPLY (
    SELECT TOP 1 u.UserId FROM TenantUsers u
    WHERE u.Role = 3 AND u.Status = 0
    ORDER BY CASE WHEN u.BranchId = x.BranchId THEN 0 WHEN u.BranchId IS NULL THEN 1 ELSE 2 END, u.UserId
) ph
WHERE NOT EXISTS (SELECT 1 FROM Bookings k WHERE k.CustomerId = c.CustomerId AND k.PackageId = p.PackageId AND k.SessionDate = x.SessionDate);

-- Downpayments (never dated after today) and shoot-day balances.
INSERT INTO Payments (BookingId, AmountPaid, PaymentMethod, PaymentDate, ReferenceNumber, CreatedAt, IsDeleted)
SELECT bk.BookingId, pay.Amount, pay.Method, pay.PayDate,
       CASE pay.Method
           WHEN 0 THEN 'OR-' + RIGHT('00000' + CAST(ABS(CHECKSUM(bk.BookingId, pay.Kind)) % 100000 AS VARCHAR(5)), 5)
           WHEN 1 THEN '10' + RIGHT('00000000000' + CAST(ABS(CHECKSUM(bk.BookingId, pay.Kind, 'gcash')) AS VARCHAR(11)), 11)
           ELSE 'BDO-' + RIGHT('00000000' + CAST(ABS(CHECKSUM(bk.BookingId, pay.Kind, 'bank')) % 100000000 AS VARCHAR(8)), 8)
       END,
       CAST(pay.PayDate AS DATETIME2), 0
FROM @B b
JOIN @BX x ON x.Seq = b.Seq
JOIN Customers c ON c.CustomerId = (SELECT MIN(CustomerId) FROM Customers WHERE FullName = b.Customer)
JOIN PhotographyPackages p ON p.PackageId = (SELECT MIN(PackageId) FROM PhotographyPackages WHERE PackageName = b.Package)
JOIN Bookings bk ON bk.CustomerId = c.CustomerId AND bk.PackageId = p.PackageId AND bk.SessionDate = x.SessionDate
CROSS APPLY (VALUES
    ('DP',  CAST(bk.PriceSnapshot * b.DpRatio AS DECIMAL(18,2)),  b.DpMethod,
            CASE WHEN DATEADD(DAY, -b.DpLead, x.SessionDate) > @Today THEN @Today ELSE DATEADD(DAY, -b.DpLead, x.SessionDate) END),
    ('BAL', CAST(bk.PriceSnapshot * b.BalRatio AS DECIMAL(18,2)), b.BalMethod, x.SessionDate)
) pay (Kind, Amount, Method, PayDate)
WHERE pay.Amount > 0
  AND pay.PayDate <= @Today
  AND NOT EXISTS (SELECT 1 FROM Payments z WHERE z.BookingId = bk.BookingId AND z.PaymentDate = pay.PayDate AND z.AmountPaid = pay.Amount);

-- ─── Walk-in product sales (second revenue stream) ──────────────────────────
INSERT INTO ProductSales (InventoryId, CustomerId, BookingId, Quantity, UnitPrice, TotalAmount, SaleDate, PaymentMethod, IsDeleted, CreatedAt, BranchId)
SELECT i.InventoryId, NULL, NULL, s.Qty, i.SellingPrice, s.Qty * i.SellingPrice, s.SaleDate, s.Method, 0, CAST(s.SaleDate AS DATETIME2), br.BranchId
FROM (
    SELECT v.*, CASE WHEN v.M < 0 THEN DATEADD(DAY, v.D - 1, DATEADD(MONTH, v.M, @MonthStart))
                     WHEN DATEADD(DAY, -v.D, @Today) < @MonthStart THEN @MonthStart
                     ELSE DATEADD(DAY, -v.D, @Today) END AS SaleDate
    FROM (VALUES
        ('Wooden Frame 8x10 (Black)',     0, -2,  9, 2, 0),
        ('Instax Mini Film (Twin Pack)',  0, -2, 21, 3, 1),
        ('Leather Photo Album 12x12',     1, -1,  7, 1, 1),
        ('Wooden Frame 8x10 (Black)',     0, -1, 16, 3, 0),
        ('Branded USB Flash Drive 32GB',  2, -1, 23, 2, 0),
        ('Instax Mini Film (Twin Pack)',  0,  0,  3, 2, 1),
        ('Glass Frame 11x14 (Gold Trim)', 0,  0,  1, 1, 0),
        ('Wooden Frame 8x10 (Black)',     1,  0,  2, 2, 1)
    ) v (ItemName, Slot, M, D, Qty, Method)
) s
JOIN @Br br ON br.Slot = s.Slot % @BranchCount
JOIN InventoryItems i ON i.InventoryId = (SELECT MIN(InventoryId) FROM InventoryItems WHERE ItemName = s.ItemName AND BranchId = br.BranchId)
WHERE NOT EXISTS (SELECT 1 FROM ProductSales z WHERE z.InventoryId = i.InventoryId AND z.SaleDate = s.SaleDate AND z.BranchId = br.BranchId);

-- ─── Expenses (branch-scoped running costs) ─────────────────────────────────
-- Current-month utility bills are left unpaid so Accounts Payable shows up.
INSERT INTO Expenses (Category, Description, Amount, ExpenseDate, SupplierId, EquipmentId, CreatedAt, IsDeleted, IsPaid, PaidDate, PurchaseOrderId, BranchId)
SELECT e.Category, e.Descr + CASE WHEN e.Monthly = 1 THEN N' - ' + FORMAT(e.ExpenseDate, 'MMMM yyyy') ELSE N'' END,
       e.Amount, e.ExpenseDate, e.SupplierId, NULL, CAST(e.ExpenseDate AS DATETIME2), 0, e.IsPaid,
       CASE WHEN e.IsPaid = 1 THEN e.ExpenseDate END, NULL, br.BranchId
FROM (
    SELECT v.*, CASE WHEN v.M < 0 THEN DATEADD(DAY, v.D - 1, DATEADD(MONTH, v.M, @MonthStart))
                     WHEN DATEADD(DAY, -v.D, @Today) < @MonthStart THEN @MonthStart
                     ELSE DATEADD(DAY, -v.D, @Today) END AS ExpenseDate
    FROM (VALUES
        -- Slot, M, D, Category, Description, Amount, Monthly, IsPaid, Supplier
        (0, -2,  1, 0, N'Studio space rent',                               32000.00, 1, 1, NULL),
        (0, -1,  1, 0, N'Studio space rent',                               32000.00, 1, 1, NULL),
        (0,  0, 31, 0, N'Studio space rent',                               32000.00, 1, 1, NULL),
        (0, -2, 18, 1, N'Davao Light electricity bill',                     9650.00, 1, 1, NULL),
        (0, -1, 18, 1, N'Davao Light electricity bill',                    10420.00, 1, 1, NULL),
        (0,  0,  0, 1, N'Davao Light electricity bill',                    10180.00, 1, 0, NULL),
        (0, -1, 12, 1, N'PLDT Fibr internet',                               2899.00, 1, 1, NULL),
        (0,  0,  1, 1, N'PLDT Fibr internet',                               2899.00, 1, 0, NULL),
        (0, -2, 22, 2, N'Photo paper and ink restock',                      7850.00, 0, 1, @SupPrint),
        (0,  0,  2, 2, N'Frames, albums and USB packaging restock',         6400.00, 0, 1, @SupPhoto),
        (0, -1,  9, 3, N'Camera sensor cleaning and lens calibration',      3500.00, 0, 1, @SupCam),
        (0,  0,  3, 3, N'Aircon cleaning and freon recharge',               2800.00, 0, 1, NULL),
        (0, -1, 25, 4, N'Facebook and Instagram ads boost',                 3000.00, 1, 1, NULL),
        (0,  0,  4, 4, N'Gas and toll fees - on-location shoots',           1650.00, 0, 1, NULL),
        (1, -1,  1, 0, N'Branch stall rent',                               18000.00, 1, 1, NULL),
        (1,  0, 31, 0, N'Branch stall rent',                               18000.00, 1, 1, NULL),
        (1, -1, 17, 1, N'Electricity and water',                            4850.00, 1, 1, NULL),
        (1,  0,  1, 1, N'Electricity and water',                            4620.00, 1, 0, NULL),
        (1,  0,  2, 2, N'Props and backdrop paper',                         2350.00, 0, 1, @SupPhoto),
        (2, -1,  1, 0, N'Branch stall rent',                               15000.00, 1, 1, NULL),
        (2,  0, 31, 0, N'Branch stall rent',                               15000.00, 1, 1, NULL),
        (2,  0,  1, 1, N'Electricity and water',                            3980.00, 1, 0, NULL),
        (2, -1, 20, 4, N'Tarpaulin and flyer printing',                     1800.00, 0, 1, @SupPrint)
    ) v (Slot, M, D, Category, Descr, Amount, Monthly, IsPaid, SupplierId)
) e
JOIN @Br br ON br.Slot = e.Slot
WHERE NOT EXISTS (
    SELECT 1 FROM Expenses z
    WHERE z.BranchId = br.BranchId
      AND z.Description = e.Descr + CASE WHEN e.Monthly = 1 THEN N' - ' + FORMAT(e.ExpenseDate, 'MMMM yyyy') ELSE N'' END);

-- ─── Payroll (company-wide) - last three closed months ──────────────────────
-- Simplified PH statutory deductions, same rates as seed-tenant-c.sql.
INSERT INTO SalaryPayments (TenantUserId, GrossAmount, SssDeduction, PhilHealthDeduction, PagIbigDeduction,
                            WithholdingTax, NetAmount, PayPeriodStart, PayPeriodEnd, PaymentDate, Notes,
                            CreatedAt, IsDeleted, IsPaid, PaidDate)
SELECT u.UserId, u.MonthlySalary,
       CAST(u.MonthlySalary * 0.045 AS DECIMAL(18,2)),
       CAST(u.MonthlySalary * 0.025 AS DECIMAL(18,2)),
       100.00,
       CAST(u.MonthlySalary * 0.08 AS DECIMAL(18,2)),
       u.MonthlySalary - CAST(u.MonthlySalary * 0.045 AS DECIMAL(18,2)) - CAST(u.MonthlySalary * 0.025 AS DECIMAL(18,2))
                       - 100.00 - CAST(u.MonthlySalary * 0.08 AS DECIMAL(18,2)),
       pp.PeriodStart, pp.PeriodEnd, pp.PeriodEnd, N'Monthly payroll - ' + FORMAT(pp.PeriodEnd, 'MMMM yyyy'),
       CAST(pp.PeriodEnd AS DATETIME2), 0, 1, pp.PeriodEnd
FROM TenantUsers u
CROSS APPLY (VALUES (-3), (-2), (-1)) m (Offset)
CROSS APPLY (SELECT DATEADD(MONTH, m.Offset, @MonthStart) AS PeriodStart,
                    EOMONTH(DATEADD(MONTH, m.Offset, @MonthStart)) AS PeriodEnd) pp
WHERE u.MonthlySalary IS NOT NULL AND u.Status = 0
  AND NOT EXISTS (SELECT 1 FROM SalaryPayments s WHERE s.TenantUserId = u.UserId AND s.PayPeriodEnd = pp.PeriodEnd);

-- ─── Owner transactions (company-wide) ──────────────────────────────────────
INSERT INTO OwnerTransactions (Type, Amount, TransactionDate, Notes, CreatedAt)
SELECT v.Type, v.Amount, DATEADD(MONTH, v.M, @MonthStart), v.Notes, CAST(DATEADD(MONTH, v.M, @MonthStart) AS DATETIME2)
FROM (VALUES
    (0, 350000.00, -6, N'Owner capital - studio equipment and fit-out'),
    (0, 100000.00, -3, N'Owner capital - working capital top-up'),
    (1,  40000.00, -1, N'Owner drawing - monthly')
) v (Type, Amount, M, Notes)
WHERE NOT EXISTS (SELECT 1 FROM OwnerTransactions o WHERE o.Notes = v.Notes);

-- ─── Keep every seeded timestamp in the past ────────────────────────────────
-- Every list sorts newest CreatedAt first, and the app stamps real input with
-- DateTime.UtcNow. Seeded rows derived from future session dates (or from a PH
-- calendar date read as UTC) would otherwise sit above anything the user enters
-- until those dates pass. Pull any such row back to just under an hour ago,
-- keeping their relative order. Also repairs rows from older seed scripts.
DECLARE @Latest DATETIME2 = DATEADD(HOUR, -1, @Now);
WITH f AS (SELECT CreatedAt, ROW_NUMBER() OVER (ORDER BY CreatedAt DESC, BookingId DESC) rn FROM Bookings WHERE CreatedAt > @Latest)
UPDATE f SET CreatedAt = DATEADD(SECOND, -rn, @Latest);
WITH f AS (SELECT CreatedAt, ROW_NUMBER() OVER (ORDER BY CreatedAt DESC, PaymentId DESC) rn FROM Payments WHERE CreatedAt > @Latest)
UPDATE f SET CreatedAt = DATEADD(SECOND, -rn, @Latest);
WITH f AS (SELECT CreatedAt, ROW_NUMBER() OVER (ORDER BY CreatedAt DESC, ProductSaleId DESC) rn FROM ProductSales WHERE CreatedAt > @Latest)
UPDATE f SET CreatedAt = DATEADD(SECOND, -rn, @Latest);
WITH f AS (SELECT CreatedAt, ROW_NUMBER() OVER (ORDER BY CreatedAt DESC, ExpenseId DESC) rn FROM Expenses WHERE CreatedAt > @Latest)
UPDATE f SET CreatedAt = DATEADD(SECOND, -rn, @Latest);
WITH f AS (SELECT CreatedAt, ROW_NUMBER() OVER (ORDER BY CreatedAt DESC, SalaryPaymentId DESC) rn FROM SalaryPayments WHERE CreatedAt > @Latest)
UPDATE f SET CreatedAt = DATEADD(SECOND, -rn, @Latest);
WITH f AS (SELECT CreatedAt, ROW_NUMBER() OVER (ORDER BY CreatedAt DESC, OwnerTransactionId DESC) rn FROM OwnerTransactions WHERE CreatedAt > @Latest)
UPDATE f SET CreatedAt = DATEADD(SECOND, -rn, @Latest);
WITH f AS (SELECT CreatedAt, ROW_NUMBER() OVER (ORDER BY CreatedAt DESC, PurchaseOrderId DESC) rn FROM PurchaseOrders WHERE CreatedAt > @Latest)
UPDATE f SET CreatedAt = DATEADD(SECOND, -rn, @Latest);
WITH f AS (SELECT CreatedAt, ROW_NUMBER() OVER (ORDER BY CreatedAt DESC, EquipmentId DESC) rn FROM Equipment WHERE CreatedAt > @Latest)
UPDATE f SET CreatedAt = DATEADD(SECOND, -rn, @Latest);
WITH f AS (SELECT LastUpdatedAt, ROW_NUMBER() OVER (ORDER BY LastUpdatedAt DESC, InventoryId DESC) rn FROM InventoryItems WHERE LastUpdatedAt > @Latest)
UPDATE f SET LastUpdatedAt = DATEADD(SECOND, -rn, @Latest);

-- ─── Hand everything to the sync outbox ─────────────────────────────────────
-- Plain SQL inserts bypass TenantSyncService, so without this nothing seeded here
-- would ever reach MonsterASP. Queue every row that has never been through the
-- outbox; the app pushes them with the SAME ids within seconds. Parents first by
-- CreatedAt order (the push also fills in any missing parent itself).
INSERT INTO SyncOutboxEntries (EntityName, EntityId, CreatedAt, SyncedAt)
SELECT q.EntityName, q.EntityId, DATEADD(MILLISECOND, q.Ord, @Now), NULL
FROM (
              SELECT 1 Ord, 'Branch' EntityName, BranchId EntityId FROM Branches
    UNION ALL SELECT 2, 'TenantUser', UserId FROM TenantUsers
    UNION ALL SELECT 3, 'Customer', CustomerId FROM Customers
    UNION ALL SELECT 4, 'PhotographyPackage', PackageId FROM PhotographyPackages
    UNION ALL SELECT 5, 'Supplier', SupplierId FROM Suppliers
    UNION ALL SELECT 6, 'Inventory', InventoryId FROM InventoryItems
    UNION ALL SELECT 7, 'Equipment', EquipmentId FROM Equipment
    UNION ALL SELECT 8, 'Booking', BookingId FROM Bookings
    UNION ALL SELECT 9, 'Payment', PaymentId FROM Payments
    UNION ALL SELECT 10, 'ProductSale', ProductSaleId FROM ProductSales
    UNION ALL SELECT 11, 'PurchaseOrder', PurchaseOrderId FROM PurchaseOrders
    UNION ALL SELECT 12, 'PurchaseOrderLine', PurchaseOrderLineId FROM PurchaseOrderLines
    UNION ALL SELECT 13, 'Expense', ExpenseId FROM Expenses
    UNION ALL SELECT 14, 'SalaryPayment', SalaryPaymentId FROM SalaryPayments
    UNION ALL SELECT 15, 'OwnerTransaction', OwnerTransactionId FROM OwnerTransactions
) q
WHERE NOT EXISTS (SELECT 1 FROM SyncOutboxEntries o WHERE o.EntityName = q.EntityName AND o.EntityId = q.EntityId);

COMMIT TRANSACTION;

-- ─── Summary ────────────────────────────────────────────────────────────────
SELECT DB_NAME() AS [Database], @BranchCount AS Branches,
       (SELECT COUNT(*) FROM Customers)        AS Customers,
       (SELECT COUNT(*) FROM Bookings)         AS Bookings,
       (SELECT COUNT(*) FROM Payments)         AS Payments,
       (SELECT COUNT(*) FROM ProductSales)     AS Sales,
       (SELECT COUNT(*) FROM Expenses)         AS Expenses,
       (SELECT COUNT(*) FROM Equipment)        AS Equipment,
       (SELECT COUNT(*) FROM InventoryItems)   AS Inventory,
       (SELECT COUNT(*) FROM SalaryPayments)   AS Payroll,
       (SELECT COUNT(*) FROM Bookings WHERE SessionDate >= @MonthStart AND SessionDate < DATEADD(MONTH, 1, @MonthStart)) AS BookingsThisMonth,
       (SELECT COUNT(*) FROM Payments WHERE PaymentDate >= @MonthStart AND PaymentDate < DATEADD(MONTH, 1, @MonthStart)) AS PaymentsThisMonth;

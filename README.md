# SNAP-ERP-Photography-Studio-Management-System
SNAP ERP is a robust, multi-tenant Enterprise Resource Planning (ERP) platform tailored for photography studios and creative agencies. Built on .NET 10, it combines a desktop Windows Forms client with a secure ASP.NET Core Web API and SQL Server backend.

🚀 Key Features
Multi-Tenant Architecture: Self-healing schema migrations, tenant database isolation via JWT tokens, and multi-database routing.
Branch Scoping & Role-Based Access Control: Fine-grained permissions by role (Admin, Branch Manager, Staff, Photographer) and branch isolation.

Booking & Scheduling: Client appointments, packages, add-ons, and session tracking.
Inventory & Equipment Tracking: Stock management, low-stock alerts, purchase order workflows, and studio equipment logging.
Point of Sale (POS) & Payments: Receipts, billing, payment processing, and customer history.
Financial Management & Analytics: Expense categorization, payroll/salary tracking, owner equity transactions, and BI charts (Revenue vs. Expenses, Branch Performance).

Master Admin Panel: Company provisioning, subscription plan tier management (Micro, Small, Medium), and system audit logs.
  
🛠️ Tech Stack
Runtime: .NET 10 (net10.0-windows & net10.0)
UI: Windows Forms (Modern C# controls & Chart data visualization)
API: ASP.NET Core Web API (JWT authentication, role/tier authorization attributes)
ORM & Database: Entity Framework Core, SQL Server (Master & Tenant database separation)
Reporting: QuestPDF & System.Windows.Forms.DataVisualization

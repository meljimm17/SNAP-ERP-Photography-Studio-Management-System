using Microsoft.Extensions.DependencyInjection;
using SNAP.infrastructure.services;

namespace SNAP
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Unlike .NET Framework, .NET (Core/5+) WinForms terminates the whole process on any
            // unhandled exception with no dialog at all - it just silently disappears. Routing both
            // UI-thread and non-UI-thread unhandled exceptions to a message box turns a silent crash
            // into a visible, reportable error instead.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
                MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception}", "Unexpected Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                MessageBox.Show($"A fatal error occurred:\n\n{e.ExceptionObject}", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            // Initialize databases with migrations. A failure here (e.g. no DB configured yet) is
            // not fatal - the demo login on LoginForm works without any database connection, and
            // every data-backed module already shows its own "Database Connection Error" message.
            try
            {
                InitializeDatabasesAsync().Wait();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not initialize the database:\n\n{ex.Message}\n\n" +
                    "You can still continue with the demo login credentials (see README.md). " +
                    "Modules that need real data will show a connection error until this is fixed.",
                    "Database Initialization Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            // Run application with LoginForm
            Application.Run(new LoginForm());

            // Dispose service provider
            serviceProvider.Dispose();
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            // Register database services
            services.AddSingleton<DatabaseContextProvider>();
            services.AddSingleton<TenantDatabaseResolver>();

            // Additional services can be registered here as needed
        }

        private static async Task InitializeDatabasesAsync()
        {
            Console.WriteLine("Initializing databases...");

            // Initialize Master ERP database
            await DatabaseInitializer.EnsureMasterDatabaseAsync(AppDbConfig.MasterErpConnectionString);

            // Initialize default Tenant database (for development/testing)
            await DatabaseInitializer.EnsureTenantDatabaseAsync(AppDbConfig.TenantErpConnectionString);

            Console.WriteLine("Database initialization complete!");
        }
    }
}

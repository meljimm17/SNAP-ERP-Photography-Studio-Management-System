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
            // Free under QuestPDF's Community license (companies with < $1M USD annual revenue);
            // required to be set once before any QuestPDF document is generated.
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            // QuestPDF only uses its own bundled font (Lato) unless this is enabled - needed so
            // the Reports PDF can use "Segoe UI" (matching the rest of the app's UI) and render the
            // Peso sign (Lato's bundled glyph set doesn't include it). Safe here since this is a
            // Windows-only WinForms app (net10.0-windows) - Segoe UI is always present.
            QuestPDF.Settings.UseSystemFonts = true;

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

            // Database creation/migration is now SNAP.api's responsibility (TenantDbContextFactory
            // and AuthenticationService self-heal each database's schema server-side) - the client
            // no longer holds connection strings at all (see appsettings.example.json), so there is
            // nothing left for the client to initialize here.

            // Run application with LoginForm
            Application.Run(new LoginForm());
        }
    }
}

using System;
using System.Windows.Forms;
using SymptomCheckerApp.Services;

namespace SymptomCheckerApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            var logger = new LoggerService(logDir);
            logger.Info("Application starting");
            Application.ThreadException += (s, e) =>
            {
                try { MessageBox.Show(e.Exception.ToString(), "Unhandled UI Exception", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                catch { }
                try { logger.Error("Unhandled UI thread exception", e.Exception); } catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { MessageBox.Show((e.ExceptionObject?.ToString() ?? "Unknown error"), "Unhandled Exception", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                catch { }
                try { logger.Error("Unhandled domain exception", e.ExceptionObject as Exception); } catch { }
            };
            try
            {
                var form = new UI.MainForm();
                logger.Info("MainForm created");
                Application.Run(form);
            }
            catch (Exception ex)
            {
                try { MessageBox.Show(ex.ToString(), "Startup Exception", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                catch { }
                try { logger.Error("Startup exception", ex); } catch { }
            }
            try { logger.Info("Application exiting"); } catch { }
        }
    }
}

using System;
using System.Windows.Forms;

namespace SymptomCheckerApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                try { MessageBox.Show(e.Exception.ToString(), "Unhandled UI Exception", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { MessageBox.Show((e.ExceptionObject?.ToString() ?? "Unknown error"), "Unhandled Exception", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                catch { }
            };
            try
            {
                Application.Run(new UI.MainForm());
            }
            catch (Exception ex)
            {
                try { MessageBox.Show(ex.ToString(), "Startup Exception", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                catch { }
            }
        }
    }
}

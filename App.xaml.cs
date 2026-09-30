using System;
using System.IO;
using System.Windows;

namespace Freedeeeff
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (s, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[DispatcherUnhandledException] {args.Exception.Message}\n{args.Exception.StackTrace}");
                if (args.Exception is InvalidOperationException || args.Exception is ArgumentException)
                {
                    args.Handled = true;
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                System.Diagnostics.Debug.WriteLine($"[AppDomain UnhandledException] {ex?.Message}\n{ex?.StackTrace}");
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] {args.Exception.Message}");
                args.SetObserved();
            };

            string? initialFile = null;
            if (e.Args.Length > 0 && File.Exists(e.Args[0]))
            {
                initialFile = e.Args[0];
            }

            var mainWindow = new MainWindow(initialFile);
            mainWindow.Show();
        }
    }
}

namespace FileSync
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using FileSync.Model;
    using FileSync.View;
    using FileSync.ViewModel;
    using Newtonsoft.Json;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly MainWindow _view;

        public App()
        {
            var fullVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var lastPeriod = fullVersion.LastIndexOf(".");
            Version = string.Format("v{0}", fullVersion.Substring(0, lastPeriod));

            Exit += App_Exit;
            var settingsPath = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault();
            if (settingsPath == null || !File.Exists(settingsPath))
            {
                settingsPath = "FileSync.settings.json";
            }

            _view = new MainWindow();
            _view.Present(settingsPath);
        }

        public static bool FirstRun { get; set; }

        public static bool Exiting { get; private set; }

        public static string Version { get; private set; }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            Exiting = true;
            _view?.Dispose();
        }
    }
}

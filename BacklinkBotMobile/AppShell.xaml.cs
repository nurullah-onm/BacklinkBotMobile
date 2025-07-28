namespace BacklinkBotMobile
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            RegisterRoutes();
        }

        private void RegisterRoutes()
        {
            // TÜM SAYFA ROUTE'LARINI KAYDET
            Routing.RegisterRoute(nameof(BacklinkPage), typeof(BacklinkPage));
            Routing.RegisterRoute(nameof(ValidatorPage), typeof(ValidatorPage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        }
    }
}
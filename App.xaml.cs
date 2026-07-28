namespace Nimbus_Internet_Blocker
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // TEMP DEV RESET — clears forgotten password/recovery protection state.
            // Runs once on launch via the same Preferences store the app uses, so it
            // works regardless of where MAUI persists it. REMOVE after a single run.
            foreach (var key in new[]
            {
                "nimbus_password_hash",
                "nimbus_password_enabled",
                "nimbus_accountability_active",
                "nimbus_recovery_mode",
                "nimbus_guardian_recovery_hash",
            })
            {
                Microsoft.Maui.Storage.Preferences.Default.Remove(key);
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        { 
            return new Window(new MainPage()) { 
                Title = "Nimbus-Internet-Blocker",
                Width = 1050,
                Height = 680,
                MinimumWidth = 900,
                MinimumHeight = 600,
                //MaximumHeight = 3840,
                //MaximumWidth = 2160
            };
        }
    }
}

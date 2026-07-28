using Nimbus_Internet_Blocker.Services;

namespace Nimbus_Internet_Blocker
{
    public partial class App : Application
    {
        public App(IPasswordService passwordService)
        {
            InitializeComponent();

            // Developer safety net: if reset-on-launch is armed (Settings, DEBUG only)
            // clear all protection state now so testing can't lock you out. No-op in
            // Release and whenever the switch is off.
            passwordService.RunDebugWipeIfEnabled();
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

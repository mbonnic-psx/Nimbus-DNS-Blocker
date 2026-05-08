namespace Nimbus_Internet_Blocker
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
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

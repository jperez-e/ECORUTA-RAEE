using Microsoft.Extensions.DependencyInjection;

namespace ECORUTA_RAEE
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new Views.SolicitudFormPage());
        }
    }
}
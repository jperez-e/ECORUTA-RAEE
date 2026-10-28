using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class SolicitudFormPage : ContentPage
{
    public SolicitudFormPage()
    {
        InitializeComponent();
        BindingContext = new SolicitudFormViewModel();
    }
}
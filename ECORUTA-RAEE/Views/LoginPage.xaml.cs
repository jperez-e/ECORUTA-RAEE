using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class LoginPage : ContentPage
{
	public LoginPage()
	{
		InitializeComponent();
        BindingContext = new LoginViewModel();

    }
}
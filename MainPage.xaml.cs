namespace Re_RunApp;

using Re_RunApp.Views;
public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

   
    private async void OnStartClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new RouteSelectionScreen());
    }
}

using Microsoft.Maui.Controls;
using System.IO;
using System.Threading;


namespace Re_RunApp.Views;

public partial class LegalDisclaimerModal : ContentPage
{
    public LegalDisclaimerModal()
    {
        InitializeComponent();
        LoadLegalDisclaimer();
    }

    private void LoadLegalDisclaimer()
    {
        DisclaimerLabel.Text = "⚠️ Legal Disclaimer\n\n" +
            "This app (Re-Run) is provided as-is, free of charge, and open source. " +
            "It is designed to interface with Bluetooth-enabled treadmills to enhance your running experience. " +
            "By using this software, you acknowledge and agree to the following:\n\n" +
            "You use this app entirely at your own risk.\n\n" +
            "The developer(s) of this app are not responsible for any injuries, accidents, damages, or losses " +
            "that may occur while using the app — whether caused by software behavior, hardware malfunction, " +
            "user error, or sheer bad luck.\n\n" +
            "Always follow the safety guidelines provided by your treadmill manufacturer.\n\n" +
            "This app does not replace professional fitness advice, medical guidance, or common sense.\n\n" +
            "If you’re unsure whether it’s safe to use this app with your treadmill, don’t use it.\n\n" +
            "By installing or using the app, you accept full responsibility for your actions and agree to waive " +
            "any claims against the developer(s) or contributors.\n\n" +
            "Run smart. Stay safe. And don’t try to break land speed records indoors 🏃‍♂️💨";
        
    }

    private async void OnCloseButtonClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

   
}
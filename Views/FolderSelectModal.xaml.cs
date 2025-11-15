using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Re_RunApp.Views;

public partial class FolderSelectModal : ContentPage
{
    TaskCompletionSource<bool>? _tcs;

    public FolderSelectModal()
    {
        InitializeComponent();
    }

    // Optional: caller can await this to know when OK was pressed.
    public Task WaitForOkAsync()
    {
        _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _tcs.Task;
    }

    private async void OnOkClicked(object? sender, System.EventArgs e)
    {
        _tcs?.TrySetResult(true);
        await Navigation.PopModalAsync(true);
    }
}
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.AccessCache;
using Microsoft.Maui.Controls;
using Microsoft.UI.Xaml;
using Re_RunApp.Core;
using Application = Microsoft.Maui.Controls.Application;
using MauiWindow = Microsoft.UI.Xaml.Window;

namespace Re_RunApp.Platforms.Windows
{
    public class FolderPickerImplementation : IFolderPicker
    {
        public async Task<string?> PickFolderAsync()
        {
            try
            {
                var hwnd = GetMainWindowHandle();
                if (hwnd == IntPtr.Zero)
                    return null;

                var picker = new FolderPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                // Start in Documents by default
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                StorageFolder? folder = await picker.PickSingleFolderAsync();
                if (folder is not null)
                {
                    try
                    {
                        // Add to FutureAccessList and persist the returned token together with the path
                        var token = StorageApplicationPermissions.FutureAccessList.Add(folder);
                        Runtime.SetUserAppFolderWithToken(folder.Path, token);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"FutureAccessList.Add failed: {ex.Message}");
                        // fallback: persist path only
                        Runtime.SetUserAppFolder(folder.Path);
                    }

                    return folder.Path;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FolderPicker failed: {ex.Message}");
                return null;
            }
        }

        static IntPtr GetMainWindowHandle()
        {
            var mauiWindow = Application.Current?.Windows?.FirstOrDefault()?.Handler?.PlatformView as MauiWindow;
            return mauiWindow is null ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(mauiWindow);
        }
    }
}
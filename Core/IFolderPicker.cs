using System.Threading.Tasks;

namespace Re_RunApp.Core
{
    // Platform-specific folder picker abstraction
    public interface IFolderPicker
    {
        // Returns the selected folder full path, or null if user cancelled
        Task<string?> PickFolderAsync();
    }
}
namespace Re_RunApp.Core;

using System.Text.Json;

internal static class ConfigurationManager
{
    private static readonly string ConfigFilePath = Path.Combine(Runtime.GetAppFolder(), "strava-config.json");

    public static StravaSettings LoadStravaSettings()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var settings = JsonSerializer.Deserialize<StravaSettings>(json);
                return settings ?? new StravaSettings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading Strava settings: {ex.Message}");
        }
        
        return new StravaSettings();
    }

    public static async Task SaveStravaSettingsAsync(StravaSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving Strava settings: {ex.Message}");
            throw;
        }
    }

    public static bool StravaSettingsExist()
    {
        return File.Exists(ConfigFilePath);
    }
}
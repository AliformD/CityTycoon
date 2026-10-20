using System.Text.Json;
using Microsoft.JSInterop;

namespace CityTycoon.Web.Services;

public enum AnimationSpeed { Normal, Fast, Off, Slow }

/// <summary>What a person can change in Settings (Sections 24.2 and 27). Saved in the browser, not in a game.</summary>
public sealed class UserSettings
{
    /// <summary>"atkinson" (default), "opendyslexic" or "system".</summary>
    public string Font { get; set; } = "atkinson";

    /// <summary>Text size as a percentage: 100 is normal.</summary>
    public int TextSize { get; set; } = 100;

    /// <summary>Line height: 1.5 is normal; bigger is airier.</summary>
    public double LineSpacing { get; set; } = 1.5;

    /// <summary>Turns animation off even if the device didn't ask for reduced motion.</summary>
    public bool ReduceMotion { get; set; }

    public AnimationSpeed Animation { get; set; } = AnimationSpeed.Normal;

    /// <summary>Skip the short pause between bot moves.</summary>
    public bool FastBots { get; set; }

    /// <summary>New games start with everyone's "auto-pass reactions" ticked.</summary>
    public bool AutoPassReactions { get; set; }
}

/// <summary>
/// Keeps the settings, saves them in the browser (localStorage) and tells the page to restyle itself.
/// The JavaScript side is wwwroot/js/app.js.
/// </summary>
public sealed class SettingsService(IJSRuntime js)
{
    private const string Key = "citytycoon.settings";

    public UserSettings Current { get; private set; } = new();

    /// <summary>Reads saved settings (if any) and applies them. Safe to call more than once.</summary>
    public async Task LoadAsync()
    {
        try
        {
            string? json = await js.InvokeAsync<string?>("ct.get", Key);
            if (!string.IsNullOrEmpty(json))
                Current = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
        catch
        {
            Current = new UserSettings();   // a broken or missing saved setting just means the defaults
        }
        await ApplyAsync();
    }

    public async Task SaveAsync()
    {
        await js.InvokeVoidAsync("ct.set", Key, JsonSerializer.Serialize(Current));
        await ApplyAsync();
    }

    private ValueTask ApplyAsync()
    {
        double animationScale = Current.Animation switch { AnimationSpeed.Fast => 0.4, AnimationSpeed.Off => 0.0001, AnimationSpeed.Slow => 1.6, _ => 1.0 };
        return js.InvokeVoidAsync("ct.applySettings", Current.Font, Current.TextSize / 100.0, Current.LineSpacing,
            Current.ReduceMotion || Current.Animation == AnimationSpeed.Off, animationScale);
    }

    /// <summary>How long to pause between bot moves, in milliseconds.</summary>
    public int BotDelayMs => Current.FastBots ? 0 : Current.Animation switch { AnimationSpeed.Fast => 60, AnimationSpeed.Off => 0, AnimationSpeed.Slow => 300, _ => 200 };
}

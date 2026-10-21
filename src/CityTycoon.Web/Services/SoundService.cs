using Microsoft.JSInterop;

namespace CityTycoon.Web.Services;

/// <summary>
/// Short sound effects (dice, coins, cards, jail...). The sounds are made by the browser on the spot
/// (wwwroot/js/app.js, "ct.sound"), so there are no sound files to download.
/// The names it knows: dice, step, coin-in, coin-out, card, jail, build, gavel, turn, win, bad, click.
/// </summary>
public sealed class SoundService(SettingsService settings, IJSRuntime js)
{
    public void Play(string name)
    {
        if (!settings.Current.Sound || settings.Current.Volume <= 0 || string.IsNullOrEmpty(name))
            return;
        // Fire and forget: a sound that fails to play (for example before the first click on the page) never stops the game.
        _ = PlayAsync(name, settings.Current.Volume / 100.0);
    }

    private async Task PlayAsync(string name, double volume)
    {
        try
        {
            await js.InvokeVoidAsync("ct.sound", name, volume);
        }
        catch
        {
            // no sound is fine
        }
    }
}

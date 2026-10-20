using Microsoft.JSInterop;

namespace CityTycoon.Web.Services;

/// <summary>Read-aloud (Section 27) using the browser's built-in speech. Does nothing where speech isn't available.</summary>
public sealed class SpeechService(IJSRuntime js)
{
    private bool? available;

    public async Task<bool> IsAvailableAsync()
    {
        try { available ??= await js.InvokeAsync<bool>("ct.speechAvailable"); }
        catch { available = false; }
        return available.Value;
    }

    public async Task SpeakAsync(string text)
    {
        try { await js.InvokeVoidAsync("ct.speak", text); }
        catch { /* no speech here: nothing to do */ }
    }

    public async Task StopAsync()
    {
        try { await js.InvokeVoidAsync("ct.stopSpeaking"); }
        catch { }
    }
}

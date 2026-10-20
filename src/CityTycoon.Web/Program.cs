using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CityTycoon.Web;
using CityTycoon.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// One GameSession for the whole browser tab, shared by every page.
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<SpeechService>();
builder.Services.AddSingleton<GameSession>();

await builder.Build().RunAsync();

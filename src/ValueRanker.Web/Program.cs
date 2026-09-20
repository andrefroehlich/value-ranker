using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;
using ValueRanker.Core.Application;
using ValueRanker.Core.Ports;
using ValueRanker.Web;
using ValueRanker.Web.Adapters;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();
builder.Services.AddLocalization();

builder.Services.AddScoped<IRunRepository, LocalStorageRunRepository>();
builder.Services.AddScoped<IValueListProvider, HttpValueListProvider>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<RankingService>();

var host = builder.Build();

var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
await using var cultureModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/blazorCulture.js");
var storedCulture = await cultureModule.InvokeAsync<string?>("getCulture");
var culture = CultureInfo.GetCultureInfo(storedCulture ?? "de");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();

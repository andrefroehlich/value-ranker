using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
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

builder.Services.AddScoped<IRunRepository, LocalStorageRunRepository>();
builder.Services.AddScoped<IValueListProvider, HttpValueListProvider>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<RankingService>();

await builder.Build().RunAsync();

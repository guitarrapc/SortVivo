using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SortAlgorithm.VisualizationWeb.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<SortAlgorithm.VisualizationWeb.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// サービス登録
builder.Services.AddSingleton<PlaybackService>();
builder.Services.AddSingleton<SortExecutor>();
builder.Services.AddSingleton<AlgorithmRegistry>();
builder.Services.AddSingleton<ArrayPatternRegistry>();
builder.Services.AddSingleton<ComparisonModeService>();
builder.Services.AddSingleton<DebugSettings>();
builder.Services.AddSingleton<RenderSettings>();
builder.Services.AddSingleton<PictureImageService>();

await builder.Build().RunAsync();

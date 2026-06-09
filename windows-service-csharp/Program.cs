using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using APICallerService;
// dotnet new worker -n windows-service-csharp to create new c-sharp .Net project
// Once done, run dotnet build to build it
//You must cd cd windows-service-csharp to build
// Build compiles it, to run the app use dotnet run
//CTRL + C in terminal to stop app
//Make sure python is running first via uvicorn main:app --reload --port 5000 in a separate cmd prompt terminal

//This file is the main switch. Its responsible for starting DI container, register services, etc. It launches both background worker and tray icon.
namespace APICallerService
{
    public class Program
    {
        public static void Main(string[] args)
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);

    var builder = Host.CreateApplicationBuilder(args);
    builder.Configuration
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

    builder.Services.Configure<WorkerSettings>(
        builder.Configuration.GetSection("WorkerSettings"));
    builder.Services.AddHttpClient();
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    // Start the background host on a separate thread
    var hostThread = new Thread(() => host.Run()) { IsBackground = true };
    hostThread.Start();

    // Run the WinForms message loop on the main thread (required for tray icons)
    Application.Run(new TrayApp(host));
}
    }
}

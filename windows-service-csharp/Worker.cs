using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;


//This file is the brain, runs constantly. This handles changes and updates of info such as file watching
namespace APICallerService
{
    public class Worker : BackgroundService
    {
        private readonly HttpClient _http;
        private readonly ILogger<Worker> _logger;
        private readonly WorkerSettings _settings;
        private FileSystemWatcher? _watcher;

        public Worker(
            ILogger<Worker> logger,
            IOptions<WorkerSettings> settings,
            IHttpClientFactory httpFactory)
        {
            _logger = logger;
            _settings = settings.Value;
            _http = httpFactory.CreateClient();
        }

        private async Task SendToPythonAsync(string path, string eventType)
        {
            var payload = new
            {
                path = path,
                event_type = eventType
            };

            var response = await _http.PostAsJsonAsync("http://localhost:5000/analyze", payload);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Python response: {result}", result);
            }
            else
            {
                _logger.LogError("Python call failed: {status}", response.StatusCode);
            }
        }

        private void InitializeWatcher()
        {
            if (string.IsNullOrWhiteSpace(_settings.WatchPath))
            {
                _logger.LogWarning("WatchPath is not set in configuration.");
                return;
            }

            if (!Directory.Exists(_settings.WatchPath))
            {
                _logger.LogError("WatchPath does not exist: {path}", _settings.WatchPath);
                return;
            }

            _watcher = new FileSystemWatcher(_settings.WatchPath);
            _watcher.Created += OnCreated;
            _watcher.Changed += OnChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.Deleted += OnDeleted;

            _watcher.EnableRaisingEvents = true;

            _logger.LogInformation("Watching folder: {path}", _settings.WatchPath);
        }

        private async void OnCreated(object sender, FileSystemEventArgs e)
{
    _logger.LogInformation("File created: {file}", e.FullPath);
    await SendToPythonAsync(e.FullPath, "created");
}
// Do the same for OnChanged, OnRenamed, OnDeleted

        private async void OnChanged(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation("File changed: {file}", e.FullPath);
            await SendToPythonAsync(e.FullPath, "changed");
        }

        private async void OnRenamed(object sender, RenamedEventArgs e)
        {
            _logger.LogInformation("File renamed: {old} → {new}", e.OldFullPath, e.FullPath);
            await SendToPythonAsync(e.FullPath, "renamed");
        }

        private async void OnDeleted(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation("File deleted: {file}", e.FullPath);
            await SendToPythonAsync(e.FullPath, "deleted");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            InitializeWatcher();

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(_settings.DelayMilliseconds, stoppingToken);
            }
        }
    }
}

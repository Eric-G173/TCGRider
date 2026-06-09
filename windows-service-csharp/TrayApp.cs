

// The face of the app. Allows you to see it and right click it in tray/taskbar. 
namespace APICallerService
{
    public class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly IHost _host;

        public TrayApp(IHost host)
        {
            _host = host;

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application, // swap for your own .ico file
                Text = "File Watcher",
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };

        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, OnExit);
            return menu;
        }


        private async void OnExit(object? sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            await _host.StopAsync();
            Application.Exit();
        }
    }
}
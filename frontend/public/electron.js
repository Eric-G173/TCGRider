const { app, BrowserWindow, ipcMain, screen } = require('electron')
const path = require('path')

function createWindow() {
  const win = new BrowserWindow({
    width: 1000,
    height: 700,
    minWidth: 900,
    minHeight: 600,
    icon: path.join(__dirname, 'icon.png'),
    resizable: true,
    frame: false,
    show: false, // avoid flash before maximize
    webPreferences: {
      nodeIntegration: true,
      contextIsolation: false
    }
  })

  win.once('ready-to-show', () => {
    win.maximize(); // native OS maximize — no resize cursor at edges
    win.show();
  });

  ipcMain.on('window-minimize', () => win.minimize());

  ipcMain.on('window-maximize', () => {
    if (win.isMaximized()) {
      win.unmaximize();
    } else {
      win.maximize();
    }
  });

  ipcMain.on('window-close', () => win.close());

  // app.isPackaged is Electron's own built-in check — true when running as
  // a real packaged app, false when running via `npm run electron` in dev.
  if (app.isPackaged) {
    win.loadFile(path.join(__dirname, '..', 'build', 'index.html'));
  } else {
    win.loadURL('http://localhost:3000');
  }

  win.setMenuBarVisibility(false)
}

app.whenReady().then(createWindow)

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})
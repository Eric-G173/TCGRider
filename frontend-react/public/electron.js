const { app, BrowserWindow, ipcMain, screen } = require('electron')
const path = require('path')

function createWindow() {
  const { workArea } = screen.getPrimaryDisplay();

  const win = new BrowserWindow({
    x: workArea.x,
    y: workArea.y,
    width: workArea.width,
    height: workArea.height,
    useContentSize: true,
    minWidth: 900,
    minHeight: 600,
    icon: path.join(__dirname, 'icon.png'),
    frame: false,
    webPreferences: {
      nodeIntegration: true,
      contextIsolation: false
    }
  })

  let isMaximized = true; // starts true since we're opening at full workArea size
  let previousBounds = null;

  ipcMain.on('window-minimize', () => win.minimize());

  ipcMain.on('window-maximize', () => {
    if (isMaximized) {
      win.setBounds(previousBounds || { x: 100, y: 100, width: 1000, height: 700 });
      isMaximized = false;
    } else {
      previousBounds = win.getBounds();
      const { workArea } = screen.getPrimaryDisplay();
      win.setBounds(workArea);
      isMaximized = true;
    }
  });

  ipcMain.on('window-close', () => win.close());

  win.loadURL('http://localhost:3000')
  win.setMenuBarVisibility(false)
}

app.whenReady().then(createWindow)

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})
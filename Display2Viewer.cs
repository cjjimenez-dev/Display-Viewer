using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Display2Viewer
{
    public class MainForm : Form
    {
        // --- Win32 API Constants & Imports ---
        private const uint WDA_NONE = 0x00000000;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;

        private const int SW_RESTORE = 9;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, int dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        // --- UI Controls ---
        private Panel topToolbar;
        private ComboBox cmbDisplays;
        private Button btnRefreshDisplays;
        private CheckBox chkStealth;
        private CheckBox chkInteractive;
        private CheckBox chkReturnCursor;
        private CheckBox chkTopMost;
        private ComboBox cmbWindowList;
        private Button btnPullToMain;
        private Button btnSendToDisplay2;
        private Button btnHideToTray;
        private Label lblStatus;
        private DoubleBufferedPanel canvasPanel;
        private Timer captureTimer;

        // --- System Tray ---
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem trayItemStealth;
        private ToolStripMenuItem trayItemTopMost;
        private bool reallyExit = false;
        private bool trayBalloonShown = false;

        // --- Capture State (Reused buffer for low RAM) ---
        private Screen targetScreen;
        private Bitmap frameBuffer;
        private Graphics frameGraphics;
        private Rectangle lastDrawRect = Rectangle.Empty;
        private bool isDraggingOnDisplay2 = false;

        private class WindowItem
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; }
            public override string ToString() { return Title; }
        }

        public MainForm()
        {
            this.Text = "Display 2 Live Viewer (Stealth Edition)";
            this.Size = new Size(1020, 640);
            this.MinimumSize = new Size(640, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 18, 22);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.KeyPreview = true;
            this.ShowInTaskbar = false; // Completely hidden from the Windows Taskbar

            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            InitializeToolbar();
            InitializeCanvas();
            InitializeTrayIcon();
            PopulateDisplays();
            PopulateWindowList();

            captureTimer = new Timer();
            captureTimer.Interval = 16; // ~60 FPS
            captureTimer.Tick += CaptureTimer_Tick;
            captureTimer.Start();

            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;
            this.KeyDown += MainForm_KeyDown;
            this.Resize += (s, e) => {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.Hide();
                }
            };
            this.VisibleChanged += (s, e) => {
                if (this.Visible)
                {
                    ApplyStealthAffinity();
                }
            };
        }

        private void InitializeToolbar()
        {
            topToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.FromArgb(28, 28, 34),
                Padding = new Padding(8, 6, 8, 6)
            };

            // Row 1: Display Selection & Modes
            Label lblDisp = new Label
            {
                Text = "Target:",
                AutoSize = true,
                Location = new Point(10, 10),
                ForeColor = Color.LightGray
            };

            cmbDisplays = new ComboBox
            {
                Location = new Point(58, 7),
                Width = 210,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(42, 42, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cmbDisplays.SelectedIndexChanged += (s, e) => SelectDisplay(cmbDisplays.SelectedIndex);

            btnRefreshDisplays = CreateStyledButton("Refresh", new Point(274, 6), new Size(64, 24));
            btnRefreshDisplays.Click += (s, e) => { PopulateDisplays(); PopulateWindowList(); };

            chkStealth = new CheckBox
            {
                Text = "Stealth Mode (Hide from AnyDesk)",
                Checked = true,
                AutoSize = true,
                Location = new Point(352, 9),
                ForeColor = Color.FromArgb(110, 231, 183),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            chkStealth.CheckedChanged += (s, e) => {
                ApplyStealthAffinity();
                if (trayItemStealth != null) trayItemStealth.Checked = chkStealth.Checked;
            };

            chkInteractive = new CheckBox
            {
                Text = "Click Inside Viewer to Control Display 2",
                Checked = true,
                AutoSize = true,
                Location = new Point(595, 9),
                ForeColor = Color.FromArgb(147, 197, 253)
            };

            chkReturnCursor = new CheckBox
            {
                Text = "Snap Cursor Back",
                Checked = true,
                AutoSize = true,
                Location = new Point(845, 9),
                ForeColor = Color.LightGray
            };

            // Row 2: Window Mover & Pin Controls
            Label lblWin = new Label
            {
                Text = "App Mover:",
                AutoSize = true,
                Location = new Point(10, 41),
                ForeColor = Color.LightGray
            };

            cmbWindowList = new ComboBox
            {
                Location = new Point(82, 37),
                Width = 256,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(42, 42, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cmbWindowList.DropDown += (s, e) => PopulateWindowList();

            btnSendToDisplay2 = CreateStyledButton("Send App -> Display 2", new Point(344, 36), new Size(148, 25));
            btnSendToDisplay2.Click += (s, e) => MoveSelectedWindowToScreen(targetScreen);

            btnPullToMain = CreateStyledButton("<- Pull App to Display 1", new Point(498, 36), new Size(152, 25));
            btnPullToMain.Click += (s, e) => MoveSelectedWindowToScreen(Screen.PrimaryScreen);

            chkTopMost = new CheckBox
            {
                Text = "Always on Top",
                Checked = false,
                AutoSize = true,
                Location = new Point(664, 39),
                ForeColor = Color.LightGray
            };
            chkTopMost.CheckedChanged += (s, e) => {
                this.TopMost = chkTopMost.Checked;
                if (trayItemTopMost != null) trayItemTopMost.Checked = chkTopMost.Checked;
            };

            btnHideToTray = CreateStyledButton("Hide to Tray", new Point(774, 36), new Size(95, 25));
            btnHideToTray.Click += (s, e) => ToggleWindowVisibility();

            lblStatus = new Label
            {
                Text = "Ready",
                AutoSize = true,
                Location = new Point(880, 40),
                ForeColor = Color.FromArgb(180, 180, 195)
            };

            topToolbar.Controls.AddRange(new Control[] {
                lblDisp, cmbDisplays, btnRefreshDisplays, chkStealth, chkInteractive, chkReturnCursor,
                lblWin, cmbWindowList, btnSendToDisplay2, btnPullToMain, chkTopMost, btnHideToTray, lblStatus
            });

            this.Controls.Add(topToolbar);
        }

        private Button CreateStyledButton(string text, Point location, Size size)
        {
            Button btn = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 52, 64),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 95);
            return btn;
        }

        private void InitializeCanvas()
        {
            canvasPanel = new DoubleBufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 12, 15),
                Cursor = Cursors.Cross
            };

            canvasPanel.Paint += CanvasPanel_Paint;
            canvasPanel.MouseDown += CanvasPanel_MouseDown;
            canvasPanel.MouseMove += CanvasPanel_MouseMove;
            canvasPanel.MouseUp += CanvasPanel_MouseUp;
            canvasPanel.MouseWheel += CanvasPanel_MouseWheel;

            this.Controls.Add(canvasPanel);
            canvasPanel.BringToFront();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ApplyStealthAffinity();
        }

        private void ApplyStealthAffinity()
        {
            if (this.IsHandleCreated)
            {
                uint flag = chkStealth.Checked ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE;
                bool ok = SetWindowDisplayAffinity(this.Handle, flag);
                chkStealth.ForeColor = chkStealth.Checked
                    ? Color.FromArgb(110, 231, 183)
                    : Color.FromArgb(252, 165, 165);
                lblStatus.Text = ok
                    ? (chkStealth.Checked ? "Stealth Active (Hidden from AnyDesk)" : "Stealth OFF (Visible to AnyDesk)")
                    : "Stealth API warning";
            }
        }

        private void PopulateDisplays()
        {
            cmbDisplays.Items.Clear();
            Screen[] screens = Screen.AllScreens;
            int defaultIndex = 0;

            for (int i = 0; i < screens.Length; i++)
            {
                Screen s = screens[i];
                string label = string.Format("Display {0}: {1}x{2} {3}",
                    i + 1, s.Bounds.Width, s.Bounds.Height, s.Primary ? "(Main)" : "(Secondary)");
                cmbDisplays.Items.Add(label);

                if (!s.Primary && defaultIndex == 0)
                {
                    defaultIndex = i;
                }
            }

            if (cmbDisplays.Items.Count > 0)
            {
                cmbDisplays.SelectedIndex = defaultIndex;
            }
        }

        private void SelectDisplay(int index)
        {
            Screen[] screens = Screen.AllScreens;
            if (index >= 0 && index < screens.Length)
            {
                targetScreen = screens[index];
                EnsureFrameBuffer(targetScreen.Bounds.Width, targetScreen.Bounds.Height);
                lblStatus.Text = string.Format("Streaming {0} ({1}x{2})",
                    targetScreen.DeviceName, targetScreen.Bounds.Width, targetScreen.Bounds.Height);
            }
        }

        private void EnsureFrameBuffer(int width, int height)
        {
            if (width <= 0 || height <= 0) return;

            if (frameBuffer == null || frameBuffer.Width != width || frameBuffer.Height != height)
            {
                if (frameGraphics != null) frameGraphics.Dispose();
                if (frameBuffer != null) frameBuffer.Dispose();

                // 24bppRgb uses 25% less RAM than 32bppArgb and renders fast
                frameBuffer = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                frameGraphics = Graphics.FromImage(frameBuffer);
            }
        }

        private void CaptureTimer_Tick(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized || targetScreen == null)
                return;

            try
            {
                Rectangle bounds = targetScreen.Bounds;
                EnsureFrameBuffer(bounds.Width, bounds.Height);

                // Copy Display 2 pixels directly into our reusable bitmap buffer
                frameGraphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);

                // Draw live cursor tracker when cursor is on target screen
                POINT pt;
                if (GetCursorPos(out pt))
                {
                    int relX = pt.X - bounds.Left;
                    int relY = pt.Y - bounds.Top;
                    if (relX >= 0 && relX < bounds.Width && relY >= 0 && relY < bounds.Height)
                    {
                        frameGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                        using (Pen whitePen = new Pen(Color.White, 2.5f))
                        using (Brush redBrush = new SolidBrush(Color.FromArgb(239, 68, 68)))
                        {
                            frameGraphics.FillEllipse(redBrush, relX - 7, relY - 7, 14, 14);
                            frameGraphics.DrawEllipse(whitePen, relX - 9, relY - 9, 18, 18);
                        }
                    }
                }

                canvasPanel.Invalidate();
            }
            catch
            {
                // Ignore transient desktop switch/lock exceptions
            }
        }

        private void CanvasPanel_Paint(object sender, PaintEventArgs e)
        {
            if (frameBuffer == null)
                return;

            int cw = canvasPanel.ClientSize.Width;
            int ch = canvasPanel.ClientSize.Height;
            if (cw <= 0 || ch <= 0)
                return;

            float scale = Math.Min((float)cw / frameBuffer.Width, (float)ch / frameBuffer.Height);
            int drawW = (int)(frameBuffer.Width * scale);
            int drawH = (int)(frameBuffer.Height * scale);
            int drawX = (cw - drawW) / 2;
            int drawY = (ch - drawH) / 2;

            lastDrawRect = new Rectangle(drawX, drawY, drawW, drawH);

            e.Graphics.InterpolationMode = InterpolationMode.Bilinear;
            e.Graphics.DrawImage(frameBuffer, lastDrawRect);
        }

        // --- Interactive Remote Control on Display 2 ---
        private bool TryMapCanvasToDisplay2(Point canvasPt, out int screenX, out int screenY)
        {
            screenX = 0;
            screenY = 0;

            if (targetScreen == null || lastDrawRect.Width <= 0 || lastDrawRect.Height <= 0)
                return false;

            if (!lastDrawRect.Contains(canvasPt))
                return false;

            double normX = (double)(canvasPt.X - lastDrawRect.Left) / lastDrawRect.Width;
            double normY = (double)(canvasPt.Y - lastDrawRect.Top) / lastDrawRect.Height;

            screenX = targetScreen.Bounds.Left + (int)(normX * targetScreen.Bounds.Width);
            screenY = targetScreen.Bounds.Top + (int)(normY * targetScreen.Bounds.Height);
            return true;
        }

        private void CanvasPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (!chkInteractive.Checked) return;

            int targetX, targetY;
            if (!TryMapCanvasToDisplay2(e.Location, out targetX, out targetY))
                return;

            POINT originalPos;
            GetCursorPos(out originalPos);

            SetCursorPos(targetX, targetY);

            if (e.Button == MouseButtons.Left)
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                isDraggingOnDisplay2 = true;
            }
            else if (e.Button == MouseButtons.Right)
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
                if (chkReturnCursor.Checked)
                    SetCursorPos(originalPos.X, originalPos.Y);
            }
            else if (e.Button == MouseButtons.Middle)
            {
                mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, UIntPtr.Zero);
                if (chkReturnCursor.Checked)
                    SetCursorPos(originalPos.X, originalPos.Y);
            }
        }

        private void CanvasPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!chkInteractive.Checked || !isDraggingOnDisplay2)
                return;

            int targetX, targetY;
            if (TryMapCanvasToDisplay2(e.Location, out targetX, out targetY))
            {
                SetCursorPos(targetX, targetY);
            }
        }

        private void CanvasPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (!chkInteractive.Checked) return;

            if (e.Button == MouseButtons.Left && isDraggingOnDisplay2)
            {
                isDraggingOnDisplay2 = false;
                int targetX, targetY;
                Point restorePt = canvasPanel.PointToScreen(e.Location);

                if (TryMapCanvasToDisplay2(e.Location, out targetX, out targetY))
                {
                    SetCursorPos(targetX, targetY);
                }

                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

                if (chkReturnCursor.Checked)
                {
                    SetCursorPos(restorePt.X, restorePt.Y);
                }
            }
        }

        private void CanvasPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!chkInteractive.Checked) return;

            int targetX, targetY;
            if (!TryMapCanvasToDisplay2(e.Location, out targetX, out targetY))
                return;

            POINT originalPos;
            GetCursorPos(out originalPos);

            SetCursorPos(targetX, targetY);
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, e.Delta, UIntPtr.Zero);

            if (chkReturnCursor.Checked)
            {
                SetCursorPos(originalPos.X, originalPos.Y);
            }
        }

        // --- Window Manager (Send / Pull Open Windows Between Displays) ---
        private void PopulateWindowList()
        {
            IntPtr selectedHandle = IntPtr.Zero;
            if (cmbWindowList.SelectedItem is WindowItem)
            {
                selectedHandle = ((WindowItem)cmbWindowList.SelectedItem).Handle;
            }

            List<WindowItem> items = new List<WindowItem>();
            IntPtr shellWindow = GetShellWindow();
            IntPtr myHandle = this.IsHandleCreated ? this.Handle : IntPtr.Zero;

            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                if (hWnd == shellWindow || hWnd == myHandle) return true;
                if (!IsWindowVisible(hWnd)) return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;

                StringBuilder sb = new StringBuilder(length + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString().Trim();
                if (string.IsNullOrEmpty(title) || title == "Program Manager") return true;

                items.Add(new WindowItem { Handle = hWnd, Title = title });
                return true;
            }, IntPtr.Zero);

            cmbWindowList.Items.Clear();
            int restoreIdx = 0;
            for (int i = 0; i < items.Count; i++)
            {
                cmbWindowList.Items.Add(items[i]);
                if (items[i].Handle == selectedHandle)
                    restoreIdx = i;
            }

            if (cmbWindowList.Items.Count > 0)
                cmbWindowList.SelectedIndex = restoreIdx;
        }

        private void MoveSelectedWindowToScreen(Screen destination)
        {
            if (destination == null) return;
            WindowItem item = cmbWindowList.SelectedItem as WindowItem;
            if (item == null) return;

            if (IsIconic(item.Handle))
            {
                ShowWindow(item.Handle, SW_RESTORE);
            }

            RECT rect;
            if (GetWindowRect(item.Handle, out rect))
            {
                int w = Math.Min(rect.Right - rect.Left, destination.WorkingArea.Width);
                int h = Math.Min(rect.Bottom - rect.Top, destination.WorkingArea.Height);
                int newX = destination.WorkingArea.Left + (destination.WorkingArea.Width - w) / 2;
                int newY = destination.WorkingArea.Top + (destination.WorkingArea.Height - h) / 2;

                SetWindowPos(item.Handle, IntPtr.Zero, newX, newY, w, h, 0x0040 /* SWP_SHOWWINDOW */);
                lblStatus.Text = "Moved: " + item.Title;
            }
        }

        private void InitializeTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.BackColor = Color.FromArgb(32, 32, 40);
            trayMenu.ForeColor = Color.White;
            trayMenu.RenderMode = ToolStripRenderMode.System;

            ToolStripMenuItem itemShowHide = new ToolStripMenuItem("Show / Hide Viewer", null, (s, e) => ToggleWindowVisibility());
            itemShowHide.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            trayItemStealth = new ToolStripMenuItem("Stealth Mode (AnyDesk Shield)", null, (s, e) => {
                chkStealth.Checked = !chkStealth.Checked;
            });
            trayItemStealth.Checked = chkStealth.Checked;

            trayItemTopMost = new ToolStripMenuItem("Always on Top", null, (s, e) => {
                chkTopMost.Checked = !chkTopMost.Checked;
            });
            trayItemTopMost.Checked = chkTopMost.Checked;

            ToolStripMenuItem itemSend = new ToolStripMenuItem("Send App -> Display 2", null, (s, e) => MoveSelectedWindowToScreen(targetScreen));
            ToolStripMenuItem itemPull = new ToolStripMenuItem("Pull App -> Display 1", null, (s, e) => MoveSelectedWindowToScreen(Screen.PrimaryScreen));

            ToolStripMenuItem itemExit = new ToolStripMenuItem("Exit", null, (s, e) => {
                reallyExit = true;
                this.Close();
            });

            trayMenu.Items.Add(itemShowHide);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(trayItemStealth);
            trayMenu.Items.Add(trayItemTopMost);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(itemSend);
            trayMenu.Items.Add(itemPull);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(itemExit);

            trayIcon = new NotifyIcon
            {
                Text = "Display 2 Viewer (Hidden from Taskbar)",
                Icon = this.Icon,
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            trayIcon.DoubleClick += (s, e) => ToggleWindowVisibility();
        }

        private void ToggleWindowVisibility()
        {
            if (this.Visible && this.WindowState != FormWindowState.Minimized)
            {
                this.Hide();
            }
            else
            {
                this.Show();
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.WindowState = FormWindowState.Normal;
                }
                this.BringToFront();
                this.Activate();
                ApplyStealthAffinity();
            }
        }

        private FormWindowState prevWindowState = FormWindowState.Normal;
        private FormBorderStyle prevBorderStyle = FormBorderStyle.Sizable;
        private bool isFullscreen = false;

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                chkStealth.Checked = !chkStealth.Checked;
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.T)
            {
                chkTopMost.Checked = !chkTopMost.Checked;
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.H)
            {
                ToggleWindowVisibility();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                if (isFullscreen)
                {
                    ToggleFullscreen();
                    e.Handled = true;
                }
                else
                {
                    // Esc hides to tray instead of quitting
                    this.Hide();
                }
            }
        }

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                prevWindowState = this.WindowState;
                prevBorderStyle = this.FormBorderStyle;
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
                isFullscreen = true;
            }
            else
            {
                this.FormBorderStyle = prevBorderStyle;
                this.WindowState = prevWindowState;
                isFullscreen = false;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // If closed via X button or Alt+F4, minimize/hide to System Tray
            if (!reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();

                if (!trayBalloonShown)
                {
                    try
                    {
                        trayIcon.ShowBalloonTip(1500, "Display 2 Viewer", "Running hidden in the System Tray. Double-click to restore.", ToolTipIcon.Info);
                    }
                    catch { }
                    trayBalloonShown = true;
                }
                return;
            }

            captureTimer.Stop();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            if (frameGraphics != null) frameGraphics.Dispose();
            if (frameBuffer != null) frameBuffer.Dispose();
        }

        [STAThread]
        public static void Main()
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer |
                          ControlStyles.ResizeRedraw, true);
            this.UpdateStyles();
        }
    }
}

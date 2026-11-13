using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class Tray : ApplicationContext
{
    internal const string AppName = "Number Wheel";
    internal const string FontName = "Segoe UI";

    private readonly Wheel wheel = new Wheel();
    private readonly NotifyIcon icon;
    private readonly ContextMenuStrip menu;
    private readonly ToolStripMenuItem toggle;
    private readonly Icon activeIcon = Style.MakeIcon(true);
    private readonly Icon pausedIcon = Style.MakeIcon(false);
    private readonly Bitmap activeMark = Style.Mark(20, true);
    private readonly Bitmap pausedMark = Style.Mark(20, false);
    private Sample sample;
    private bool active = true;

    internal Tray()
    {
        menu = new ContextMenuStrip
        {
            ShowImageMargin = false, ShowCheckMargin = true, Padding = new Padding(4),
            ForeColor = Style.Text, Font = new Font(FontName, 9),
            Renderer = new ToolStripProfessionalRenderer(new MenuColors()) { RoundedEdges = false }
        };
        toggle = Item("Enabled", Toggle);
        menu.Items.AddRange(new ToolStripItem[]
        {
            Item("Open test window", OpenSample), toggle,
            new ToolStripSeparator(), Item("Quit", ExitThread)
        });
        icon = new NotifyIcon { ContextMenuStrip = menu };
        icon.MouseClick += (sender, args) => { if (args.Button == MouseButtons.Left) OpenSample(); };
        RefreshStatus();
        icon.Visible = true;
    }

    private static ToolStripMenuItem Item(string label, Action action)
    {
        return new ToolStripMenuItem(label, null, (sender, args) => action()) { Padding = new Padding(2, 4, 12, 4) };
    }

    private void Toggle()
    {
        active = !active;
        wheel.Enabled = active;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        string caption = active ? "Active across apps" : "Paused";
        toggle.Checked = active;
        icon.Text = AppName + " · " + caption;
        icon.Icon = active ? activeIcon : pausedIcon;
        string action = active ? "Pause" : "Resume";
        if (sample != null) sample.SetStatus(caption, active ? activeMark : pausedMark, action);
    }

    private void OpenSample()
    {
        if (sample == null)
        {
            sample = new Sample(activeIcon, Toggle);
            sample.FormClosed += (sender, args) => sample = null;
            RefreshStatus();
        }
        if (sample.WindowState == FormWindowState.Minimized) sample.WindowState = FormWindowState.Normal;
        if (!sample.Visible) sample.Show();
        sample.Activate();
        SetForegroundWindow(sample.Handle);
    }

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (sample != null) sample.Close();
            icon.Dispose();
            menu.Dispose();
            activeIcon.Dispose();
            pausedIcon.Dispose();
            activeMark.Dispose();
            pausedMark.Dispose();
            wheel.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class Sample : Form
{
    private readonly Label status;
    private readonly PictureBox mark;
    private readonly Button toggle;

    internal Sample(Icon icon, Action toggleAction)
    {
        Text = Tray.AppName;
        Icon = icon;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(430, 259);
        BackColor = Style.Surface;
        ForeColor = Style.Text;
        Font = new Font(Tray.FontName, 10);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        KeyPreview = true;
        KeyDown += (sender, args) => { if (args.KeyCode == Keys.Escape) Close(); };

        var header = new Label
        {
            Text = Tray.AppName, AutoSize = true, Location = new Point(25, 23),
            Font = new Font(Font.FontFamily, 20, FontStyle.Bold)
        };
        mark = new PictureBox { Bounds = new Rectangle(27, 69, 20, 20), SizeMode = PictureBoxSizeMode.Zoom };
        status = new Label { AutoSize = true, Location = new Point(52, 68), ForeColor = Style.Secondary };
        var instruction = new Label
        {
            Text = "Hover over a number and scroll up or down.",
            AutoSize = true, Location = new Point(26, 112)
        };
        var example = new TextBox
        {
            AccessibleName = "Test text", Location = new Point(26, 144), Width = 378,
            Font = new Font(Tray.FontName, 15), ForeColor = Style.Text,
            Text = "Exposure -0.25 EV, frame 120"
        };
        toggle = new Button
        {
            AutoSize = true, Location = new Point(26, 201), Padding = new Padding(10, 4, 10, 4),
            FlatStyle = FlatStyle.Flat, BackColor = Style.Highlight, ForeColor = Style.Text
        };
        toggle.FlatAppearance.BorderColor = Style.Border;
        toggle.Click += (sender, args) => { toggleAction(); example.Focus(); };
        Controls.AddRange(new Control[] { header, mark, status, instruction, example, toggle });
        ActiveControl = example;
        Shown += (sender, args) => example.Select(0, 0);
    }

    internal void SetStatus(string caption, Bitmap image, string action)
    {
        status.Text = caption;
        mark.Image = image;
        toggle.Text = action;
    }
}

internal static class Style
{
    internal static readonly Color Surface = SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(249, 250, 254);
    internal static readonly Color Text = SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(28, 33, 52);
    internal static readonly Color Secondary = SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(78, 86, 109);
    internal static readonly Color Highlight = SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(234, 234, 252);
    internal static readonly Color Border = SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(203, 205, 227);

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);

    internal static Icon MakeIcon(bool active)
    {
        using (var bitmap = Mark(32, active))
        {
            var handle = bitmap.GetHicon();
            try { using (var source = Icon.FromHandle(handle)) return (Icon)source.Clone(); }
            finally { DestroyIcon(handle); }
        }
    }

    internal static Bitmap Mark(int size, bool active)
    {
        var image = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(image))
        using (var background = new SolidBrush(active ? Color.FromArgb(79, 67, 204) : Color.FromArgb(88, 97, 115)))
        using (var line = new Pen(Color.White, 3.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.ScaleTransform(size / 32f, size / 32f);
            using (var tile = new GraphicsPath())
            {
                tile.AddArc(1, 1, 14, 14, 180, 90);
                tile.AddArc(17, 1, 14, 14, 270, 90);
                tile.AddArc(17, 17, 14, 14, 0, 90);
                tile.AddArc(1, 17, 14, 14, 90, 90);
                tile.CloseFigure();
                graphics.FillPath(background, tile);
            }
            if (active)
            {
                graphics.DrawLines(line, new[] { new Point(9, 13), new Point(16, 7), new Point(23, 13) });
                graphics.DrawLines(line, new[] { new Point(9, 19), new Point(16, 25), new Point(23, 19) });
            }
            else
            {
                graphics.DrawLine(line, 13, 10, 13, 22);
                graphics.DrawLine(line, 19, 10, 19, 22);
            }
        }
        return image;
    }
}

internal sealed class MenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground { get { return Style.Surface; } }
    public override Color MenuBorder { get { return Style.Border; } }
    public override Color MenuItemBorder { get { return Style.Border; } }
    public override Color MenuItemSelected { get { return Style.Highlight; } }
    public override Color SeparatorDark { get { return Style.Border; } }
    public override Color SeparatorLight { get { return Style.Surface; } }
    public override Color ImageMarginGradientBegin { get { return Style.Surface; } }
    public override Color ImageMarginGradientMiddle { get { return Style.Surface; } }
    public override Color ImageMarginGradientEnd { get { return Style.Surface; } }
}

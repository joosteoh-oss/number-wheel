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
        if (sample != null) sample.SetStatus(caption, active, action);
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
        ShowWindow(sample.Handle, 5);
        sample.Activate();
        SetForegroundWindow(sample.Handle);
    }

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (sample != null) sample.Close();
            icon.Dispose();
            menu.Dispose();
            activeIcon.Dispose();
            pausedIcon.Dispose();
            wheel.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class Sample : Form
{
    private readonly Label heading = new Label { Text = "Try the scroll gesture", AutoSize = true };
    private readonly Label instruction = new Label
    {
        Text = "Point at a number below and roll the wheel.", AutoSize = true, ForeColor = Style.Secondary
    };
    private readonly Panel editorCard = new Panel
    {
        BackColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly TextBox example = new TextBox
    {
        AccessibleName = "Test text", BorderStyle = BorderStyle.None, BackColor = SystemColors.Window,
        ForeColor = Style.Text, Text = "Exposure -0.25 EV  /  Frame 120"
    };
    private readonly Panel divider = new Panel { BackColor = Style.Border };
    private readonly Label status = new Label { AutoSize = true, ForeColor = Style.Secondary };
    private readonly Button toggle = new Button { FlatStyle = FlatStyle.Flat, Text = "Pause" };
    private Font bodyFont, headingFont, editorFont;

    internal Sample(Icon icon, Action toggleAction)
    {
        Text = Tray.AppName;
        Icon = icon;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Style.Surface;
        ForeColor = Style.Text;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        KeyPreview = true;
        KeyDown += (sender, args) => { if (args.KeyCode == Keys.Escape) Close(); };

        toggle.FlatAppearance.BorderColor = Style.Border;
        toggle.FlatAppearance.MouseOverBackColor = Style.Highlight;
        toggle.BackColor = SystemColors.Window;
        toggle.Click += (sender, args) => { toggleAction(); example.Focus(); };
        editorCard.Controls.Add(example);
        Controls.AddRange(new Control[] { heading, instruction, editorCard, divider, status, toggle });
        ActiveControl = example;
        Shown += (sender, args) => example.Select(0, 0);
    }

    protected override void OnLoad(EventArgs args)
    {
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = workArea.Location;
        LayoutForDpi((int)GetDpiForWindow(Handle));
        Location = new Point(workArea.Left + (workArea.Width - Width) / 2,
            workArea.Top + (workArea.Height - Height) / 2);
        base.OnLoad(args);
    }

    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);

    protected override void WndProc(ref Message message)
    {
        const int dpiChanged = 0x02E0;
        if (message.Msg == dpiChanged && bodyFont != null)
        {
            var suggested = (NativeRect)Marshal.PtrToStructure(message.LParam, typeof(NativeRect));
            Location = new Point(suggested.Left, suggested.Top);
            LayoutForDpi((int)(message.WParam.ToInt64() & 0xffff));
            return;
        }
        base.WndProc(ref message);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left, Top, Right, Bottom;
    }

    private void LayoutForDpi(int dpi)
    {
        SuspendLayout();
        var oldBody = bodyFont;
        var oldHeading = headingFont;
        var oldEditor = editorFont;
        float scale = dpi / 96f;
        Func<int, int> px = value => (int)Math.Round(value * scale);
        bodyFont = new Font(Tray.FontName, 14 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        headingFont = new Font(Tray.FontName, 25 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        editorFont = new Font(Tray.FontName, 19 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        Font = bodyFont;
        heading.Font = headingFont;
        example.Font = editorFont;

        int left = px(26);
        int content = Math.Max(px(352), Math.Max(instruction.PreferredWidth,
            TextRenderer.MeasureText(example.Text, editorFont).Width + px(32)));
        int y = px(22);
        heading.Location = new Point(left, y);
        y += heading.PreferredHeight + px(5);
        instruction.Location = new Point(left, y);
        y += instruction.PreferredHeight + px(18);
        editorCard.SetBounds(left, y, content, px(48));
        example.SetBounds(px(13), (editorCard.ClientSize.Height - example.PreferredHeight) / 2,
            editorCard.ClientSize.Width - px(26), example.PreferredHeight);
        y += editorCard.Height + px(19);
        divider.SetBounds(left, y, content, px(1));
        y += px(13);
        toggle.SetBounds(left + content - px(88), y, px(88), px(32));
        status.Location = new Point(left, y + (toggle.Height - status.PreferredHeight) / 2);
        ClientSize = new Size(content + left * 2, toggle.Bottom + px(18));
        ResumeLayout(false);
        if (oldBody != null) oldBody.Dispose();
        if (oldHeading != null) oldHeading.Dispose();
        if (oldEditor != null) oldEditor.Dispose();
    }

    internal void SetStatus(string caption, bool active, string action)
    {
        status.Text = caption;
        status.ForeColor = active && !SystemInformation.HighContrast
            ? Color.FromArgb(26, 101, 73) : Style.Secondary;
        toggle.Text = action;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        if (bodyFont != null) bodyFont.Dispose();
        if (headingFont != null) headingFont.Dispose();
        if (editorFont != null) editorFont.Dispose();
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

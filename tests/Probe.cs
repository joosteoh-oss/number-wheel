using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

internal static class Probe
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, int data, UIntPtr extra);

    [STAThread]
    private static int Main()
    {
        Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
        var oldWindow = Native.GetForegroundWindow();
        Native.Pt oldPoint;
        Native.GetCursorPos(out oldPoint);
        var result = 1;
        var form = new Form { Width = 490, Height = 220, StartPosition = FormStartPosition.CenterScreen, TopMost = true };
        var field = new TextBox
        {
            Font = new Font("Consolas", 24, FontStyle.Regular, GraphicsUnit.Pixel), Text = "gain -0.25 at 20",
            Location = new Point(12, 12), Width = 420
        };
        var sibling = new TextBox
        {
            Font = field.Font, Text = "200", Location = new Point(12, 65), Width = 420
        };
        int ordinary = 0;
        field.MouseWheel += (sender, args) => ordinary++;
        form.Controls.Add(field);
        form.Controls.Add(sibling);
        form.Shown += (sender, args) =>
        {
            form.Activate();
            field.Focus();
            new Thread(() =>
            {
                try
                {
                    Check(Numbers.Adjust("-0.25", 1) == "-0.24", "decimal precision");
                    Check(Numbers.Adjust("009", 1) == "010", "leading zero");
                    Check(Numbers.Adjust("+4", -1) == "+3", "explicit plus");
                    Check(Numbers.Adjust("0", -1) == "-1", "negative integer");
                    var digit = Position(field, 8);
                    Check(Native.GetForegroundWindow() == form.Handle, "fixture focused");
                    var focused = AutomationElement.FocusedElement;
                    Console.WriteLine("Provider: {0} {1}", focused.Current.ClassName,
                        string.Join(", ", Array.ConvertAll(focused.GetSupportedPatterns(), pattern => pattern.ProgrammaticName)));
                    var candidate = Editor.AtPoint(digit, Native.WindowFromPoint(digit), form.Handle);
                    Check(candidate != null && candidate.Text == "-0.25", "hit-test negative decimal");
                    Scroll(digit, 120);
                    Check(Read(field) == "gain -0.24 at 20" && ordinary == 0, "wheel up changes only hovered decimal");
                    Scroll(digit, -120);
                    Check(Read(field) == "gain -0.25 at 20" && ordinary == 0, "wheel down");
                    var second = Position(field, 14);
                    Scroll(second, 120);
                    Check(Read(field) == "gain -0.25 at 21" && ordinary == 0,
                        "second number is independently editable (" + Read(field) + ", wheel " + ordinary + ")");
                    form.Invoke(new Action(field.Undo));
                    Check(Read(field) == "gain -0.25 at 20", "standard editor undo");
                    Scroll(Position(field, 1), 120);
                    Check(Read(field) == "gain -0.25 at 20" && ordinary == 1, "letters pass wheel through");
                    Scroll(Position(field, 14, 90), 120);
                    Check(Read(field) == "gain -0.25 at 20" && ordinary == 2, "trailing blank passes wheel through");
                    Scroll(Position(sibling, 0), 120);
                    Check(Read(sibling) == "200", "unfocused field remains unchanged");
                    form.Invoke(new Action(() => { field.Text = "300"; field.ReadOnly = true; }));
                    Scroll(Position(field, 1), 120);
                    Check(Read(field) == "300", "read-only remains unchanged");
                    result = 0;
                }
                catch (Exception error) { Console.Error.WriteLine(error); }
                finally { form.BeginInvoke(new Action(form.Close)); }
            }) { IsBackground = true }.Start();
        };
        try { Application.Run(form); }
        finally
        {
            if (oldWindow != IntPtr.Zero) SetForegroundWindow(oldWindow);
            SetCursorPos(oldPoint.X, oldPoint.Y);
        }
        return result;
    }

    private static Native.Pt Position(TextBox field, int index, int offset = 5)
    {
        return (Native.Pt)field.Invoke(new Func<Native.Pt>(() =>
        {
            var p = field.PointToScreen(field.GetPositionFromCharIndex(index));
            return new Native.Pt { X = p.X + offset, Y = p.Y + field.Font.Height / 2 };
        }));
    }

    private static void Scroll(Native.Pt point, int delta)
    {
        SetCursorPos(point.X, point.Y);
        Thread.Sleep(300);
        if (Native.WindowFromPoint(point) == IntPtr.Zero) throw new InvalidOperationException("fixture vanished");
        mouse_event(0x800, 0, 0, delta, UIntPtr.Zero);
        Thread.Sleep(300);
    }

    private static string Read(TextBox field)
    {
        return (string)field.Invoke(new Func<string>(() => field.Text));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Automation.Text;

internal static class Probe
{
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, int data, UIntPtr extra);
    private static int Main()
    {
        Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
        Native.Pt oldPoint;
        Native.GetCursorPos(out oldPoint);
        try
        {
            var field = AutomationElement.FocusedElement;
            Console.WriteLine("{0}: {1}", field.Current.Name, field.Current.ControlType.ProgrammaticName);
            if (field.Current.Name != "Number Wheel integration fixture") return 1;
            var pattern = (TextPattern)field.GetCurrentPattern(TextPattern.Pattern);
            var token = pattern.DocumentRange.FindText("-0.25", false, false);
            if (token == null)
            {
                Console.WriteLine("Initial field value: " + pattern.DocumentRange.GetText(100));
                return 1;
            }
            var rectangles = token.GetBoundingRectangles();
            var rect = rectangles.Length > 0 ? rectangles[0] : field.Current.BoundingRectangle;
            var point = new Native.Pt { X = (int)(rect.X + rect.Width / 2), Y = (int)(rect.Y + rect.Height / 2) };
            Editor editor = null;
            if (rectangles.Length == 0)
            {
                for (point.X = (int)rect.Left + 10; point.X < rect.Right && editor == null; point.X += 4)
                {
                    var candidate = Editor.AtPoint(point, Native.WindowFromPoint(point), Native.GetForegroundWindow());
                    if (candidate != null && candidate.Text == "-0.25") { editor = candidate; break; }
                }
            }
            else editor = Editor.AtPoint(point, Native.WindowFromPoint(point), Native.GetForegroundWindow());
            Console.WriteLine("Hit: " + (editor == null ? "none" : editor.Text));
            if (editor == null) return 1;
            SetCursorPos(point.X, point.Y);
            Thread.Sleep(380);
            mouse_event(0x800, 0, 0, 120, UIntPtr.Zero);
            Thread.Sleep(350);
            string text = pattern.DocumentRange.GetText(100);
            Console.WriteLine("Result: " + text);
            return text.TrimEnd() == "gain -0.24 at 20" ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("{0} 0x{1:X8}: {2}\n{3}", error.GetType(), error.HResult, error.Message, error.StackTrace);
            return 1;
        }
        finally { SetCursorPos(oldPoint.X, oldPoint.Y); }
    }
}

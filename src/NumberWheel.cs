using System;
using System.ComponentModel;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Forms;
using Point = System.Windows.Point;

internal static class NumberWheel
{
    [STAThread]
    private static void Main()
    {
        Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
        bool first;
        using (var instance = new Mutex(true, @"Local\Codex.NumberWheel", out first))
        {
            if (!first) return;
            Application.EnableVisualStyles();
            try
            {
                using (var tray = new Tray()) Application.Run(tray);
            }
            catch (Win32Exception error)
            {
                MessageBox.Show(error.Message, Tray.AppName);
            }
        }
    }
}

internal sealed class Wheel : IDisposable
{
    private sealed class Hover
    {
        internal Native.Pt Point;
        internal IntPtr Window;
        internal IntPtr Foreground;
        internal bool Numeric;
        internal long Time;
    }

    private sealed class Scroll
    {
        internal Hover Hover;
        internal int Delta;
    }

    private readonly object sync = new object();
    private readonly AutoResetEvent wake = new AutoResetEvent(false);
    private readonly Native.MouseHook callback;
    private readonly IntPtr hook;
    private volatile bool running = true;
    private volatile bool enabled = true;
    private volatile Hover hover;
    private Scroll pending;
    private int remainder;
    private Hover previous;

    internal Wheel()
    {
        callback = OnMouse;
        hook = Native.SetWindowsHookEx(14, callback, Native.GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) throw new Win32Exception();
        new Thread(Work) { IsBackground = true, Name = "Number Wheel UI Automation" }.Start();
    }

    internal bool Enabled
    {
        set
        {
            enabled = value;
            hover = null;
            lock (sync) pending = null;
            wake.Set();
        }
    }

    private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && enabled && message.ToInt64() == 0x020A)
        {
            var state = hover;
            var mouse = (Native.MouseData)Marshal.PtrToStructure(data, typeof(Native.MouseData));
            if (state != null && state.Numeric && (mouse.Flags & 2) == 0 && !Native.ModifierDown()
                && DateTime.UtcNow.Ticks - state.Time < TimeSpan.TicksPerMillisecond * 220
                && SameLocation(state, mouse.Point))
            {
                lock (sync)
                {
                    if (pending == null || !Equivalent(pending.Hover, state))
                        pending = new Scroll { Hover = state };
                    pending.Delta += (short)(mouse.MouseDataValue >> 16);
                }
                wake.Set();
                return new IntPtr(1);
            }
        }
        return Native.CallNextHookEx(hook, code, message, data);
    }

    private static bool Equivalent(Hover a, Hover b)
    {
        return a.Window == b.Window && a.Foreground == b.Foreground && a.Point.Equals(b.Point);
    }

    private static bool SameLocation(Hover state, Native.Pt point)
    {
        return state.Point.Equals(point) && state.Foreground == Native.GetForegroundWindow()
            && state.Window == Native.WindowFromPoint(point);
    }

    private void Work()
    {
        while (running)
        {
            Scroll scroll;
            lock (sync)
            {
                scroll = pending;
                pending = null;
            }
            if (enabled)
            {
                if (scroll != null) Apply(scroll);
                UpdateHover();
            }
            wake.WaitOne(enabled ? 42 : Timeout.Infinite);
        }
        wake.Dispose();
    }

    private void UpdateHover()
    {
        Native.Pt point;
        if (!Native.GetCursorPos(out point)) return;
        var state = new Hover
        {
            Point = point, Window = Native.WindowFromPoint(point),
            Foreground = Native.GetForegroundWindow(), Time = DateTime.UtcNow.Ticks
        };
        var last = hover;
        if (last != null && !last.Numeric && Equivalent(state, last)
            && state.Time - last.Time < TimeSpan.TicksPerMillisecond * 125) return;
        state.Numeric = Find(state) != null;
        if (enabled) hover = state;
    }

    private void Apply(Scroll scroll)
    {
        Native.Pt point;
        if (!Native.GetCursorPos(out point) || !SameLocation(scroll.Hover, point)
            || Native.ModifierDown()) return;
        if (previous == null || !Equivalent(previous, scroll.Hover)
            || scroll.Hover.Time - previous.Time > TimeSpan.TicksPerSecond)
            remainder = 0;
        previous = scroll.Hover;
        remainder += scroll.Delta;
        int steps = remainder / 120;
        remainder %= 120;
        if (steps == 0) return;
        try
        {
            var target = Find(scroll.Hover);
            if (target == null || !enabled || !Native.GetCursorPos(out point)
                || !SameLocation(scroll.Hover, point)) return;
            var replacement = Numbers.Adjust(target.Text, steps);
            if (replacement != null) target.Replace(replacement, () => enabled
                && Native.GetCursorPos(out point) && SameLocation(scroll.Hover, point));
        }
        catch (Exception error)
        {
            if (!Editor.IsProviderError(error)) throw;
        }
    }

    private static Editor Find(Hover state)
    {
        if (state.Window == IntPtr.Zero || state.Foreground == IntPtr.Zero
            || Native.GetAncestor(state.Window, 2) != state.Foreground) return null;
        try
        {
            return Editor.AtPoint(state.Point, state.Window, state.Foreground);
        }
        catch (Exception error)
        {
            if (!Editor.IsProviderError(error)) throw;
            return null;
        }
    }

    public void Dispose()
    {
        Native.UnhookWindowsHookEx(hook);
        running = false;
        wake.Set();
    }
}

internal static class Numbers
{
    internal static readonly Regex Token = new Regex(
        @"(?<![\p{L}\p{N}_.)\]])[+-]?(?:[0-9]+(?:\.[0-9]+)?|\.[0-9]+)|[0-9]+(?:\.[0-9]+)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static string Adjust(string text, int steps)
    {
        decimal value;
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value)) return null;
        int dot = text.IndexOf('.');
        int scale = dot < 0 ? 0 : text.Length - dot - 1;
        if (scale > 28 || text.TrimStart('+', '-').Replace(".", "").TrimStart('0').Length > 28) return null;
        decimal increment = 1m;
        for (int index = 0; index < scale; index++) increment /= 10m;
        try { value += steps * increment; }
        catch (OverflowException) { return null; }
        var digits = text.TrimStart('+', '-');
        int width = digits.Split('.')[0].Length;
        string integerFormat = width > 1 && digits[0] == '0' ? new string('0', width) : "0";
        if (width == 0 && value > -1m && value < 1m) integerFormat = "#";
        string format = scale == 0 ? integerFormat : integerFormat + "." + new string('0', scale);
        string result = value.ToString(format, CultureInfo.InvariantCulture);
        return text[0] == '+' && value >= 0 ? "+" + result : result;
    }
}

internal sealed class Editor
{
    private readonly AutomationElement element;
    private readonly TextPattern pattern;
    private readonly TextPatternRange range;
    private readonly IntPtr foreground;
    internal readonly string Text;

    private Editor(AutomationElement element, TextPattern pattern, TextPatternRange range,
        IntPtr foreground, string text)
    {
        this.element = element;
        this.pattern = pattern;
        this.range = range;
        this.foreground = foreground;
        Text = text;
    }

    internal static bool IsProviderError(Exception error)
    {
        return error is ElementNotAvailableException || error is COMException
            || error is InvalidOperationException || error is ArgumentException
            || error is TimeoutException || error is UnauthorizedAccessException
            || error is NotSupportedException || error is NullReferenceException;
    }

    internal static Editor AtPoint(Native.Pt cursor, IntPtr window, IntPtr foreground)
    {
        var element = AutomationElement.FocusedElement;
        if (element == null) return null;
        int pid;
        Native.GetWindowThreadProcessId(window, out pid);
        var point = new Point(cursor.X, cursor.Y);
        var current = element.Current;
        if (current.ProcessId != pid || !current.IsEnabled || current.IsPassword
            || (current.ControlType != ControlType.Edit && current.ControlType != ControlType.Document)
            || !current.BoundingRectangle.Contains(point)) return null;
        object provider;
        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out provider)) return null;
        var pattern = provider as TextPattern;
        if (pattern == null || pattern.SupportedTextSelection == SupportedTextSelection.None) return null;

        // Providers can return no range for padding or a disappearing text element.
        var at = pattern.RangeFromPoint(point);
        if (at == null) return null;
        var left = at.Clone();
        var right = at.Clone();
        if (left == null || right == null) return null;
        int leftUnits = -left.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -96);
        right.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 96);
        string before = left.GetText(512);
        string after = right.GetText(512);
        string fragment = before + after;
        foreach (Match match in Numbers.Token.Matches(fragment))
        {
            int start = match.Index - before.Length;
            int end = start + match.Length;
            if (start > 1 || end < -1) continue;
            if ((match.Index == 0 && leftUnits == 96) ||
                (match.Index + match.Length == fragment.Length && after.Length >= 96)) continue;
            var token = at.Clone();
            if (token == null) return null;
            if (start < 0)
                token.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, start);
            if (end > 0)
                token.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, end);
            if (start > 0)
                token.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, start);
            if (end < 0)
                token.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, end);
            if (token.GetText(match.Length + 1) != match.Value) continue;
            var bounds = token.GetBoundingRectangles();
            if (bounds == null || !Array.Exists(bounds, rect => rect.Contains(point))
                || Numbers.Adjust(match.Value, 0) == null) continue;

            object readOnly = token.GetAttributeValue(TextPattern.IsReadOnlyAttribute);
            if (!object.Equals(readOnly, false))
            {
                object value;
                if (object.Equals(readOnly, true)
                    || !element.TryGetCurrentPattern(ValuePattern.Pattern, out value)) return null;
                var valuePattern = value as ValuePattern;
                if (valuePattern == null || valuePattern.Current.IsReadOnly) return null;
            }
            return new Editor(element, pattern, token, foreground, match.Value);
        }
        return null;
    }

    internal void Replace(string replacement, Func<bool> stillAtTarget)
    {
        if (replacement == Text || Native.GetForegroundWindow() != foreground
            || !element.Equals(AutomationElement.FocusedElement) || Native.ModifierDown()
            || range.GetText(Text.Length + 1) != Text) return;
        var original = pattern.GetSelection();
        if (!stillAtTarget()) return;
        range.Select();
        // Some providers post the selection to their editor asynchronously.
        for (int attempt = 0; attempt < 6; attempt++)
        {
            if (!stillAtTarget() || !element.Equals(AutomationElement.FocusedElement)
                || Native.ModifierDown()) return;
            var selected = pattern.GetSelection();
            if (SingleRange(selected) && selected[0].Compare(range)
                && selected[0].GetText(Text.Length + 1) == Text)
            {
                if (Native.GetForegroundWindow() == foreground && Native.Type(replacement)) return;
                break;
            }
            Thread.Sleep(10);
        }
        var current = pattern.GetSelection();
        if (SingleRange(original) && SingleRange(current) && current[0].Compare(range)
            && stillAtTarget() && element.Equals(AutomationElement.FocusedElement)) original[0].Select();
    }

    private static bool SingleRange(TextPatternRange[] ranges)
    {
        return ranges != null && ranges.Length == 1 && ranges[0] != null;
    }
}

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Pt
    {
        internal int X, Y;
        internal bool Equals(Pt other) { return X == other.X && Y == other.Y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseData
    {
        internal Pt Point;
        internal uint MouseDataValue, Flags, Time;
        internal UIntPtr Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Key
    {
        internal ushort VirtualKey, Scan;
        internal uint Flags, Time;
        internal UIntPtr Extra;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)] internal Key Key;
        [FieldOffset(0)] internal MouseData Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        internal uint Type;
        internal InputData Data;
    }

    internal delegate IntPtr MouseHook(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int id, MouseHook hook, IntPtr module, uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Pt point);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(Pt point);
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out int pid);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);

    internal static bool ModifierDown()
    {
        return GetAsyncKeyState(0x10) < 0 || GetAsyncKeyState(0x11) < 0
            || GetAsyncKeyState(0x12) < 0 || GetAsyncKeyState(0x5B) < 0
            || GetAsyncKeyState(0x5C) < 0 || GetAsyncKeyState(0x01) < 0
            || GetAsyncKeyState(0x02) < 0 || GetAsyncKeyState(0x04) < 0;
    }

    internal static bool Type(string text)
    {
        var inputs = new Input[text.Length * 2];
        for (int index = 0; index < text.Length; index++)
        {
            inputs[index * 2] = new Input { Type = 1, Data = new InputData { Key = new Key { Scan = text[index], Flags = 4 } } };
            inputs[index * 2 + 1] = inputs[index * 2];
            inputs[index * 2 + 1].Data.Key.Flags |= 2;
        }
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input))) == inputs.Length;
    }
}

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace SeleccionTraductor
{
    internal sealed class DocumentTarget
    {
        internal readonly IntPtr Window;
        internal readonly string Text;
        private readonly uint process;
        private readonly IntPtr focus;
        private readonly string title;
        private readonly bool nativeEdit, richEdit;
        private readonly int start, end;
        private AutomationElement owner;
        private TextPatternRange originalRange;
        private int[] focusId;
        private static int probeBusy;

        [StructLayout(LayoutKind.Sequential)]
        private struct GuiInfo
        {
            internal uint Size, Flags;
            internal IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
            internal Native.Rect CaretRect;
        }
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int limit);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int limit);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr w, IntPtr l, uint flags, uint timeout, out UIntPtr result);

        private DocumentTarget(IntPtr window, string text, IntPtr focused, uint pid, string caption, bool edit, bool rich, int first, int last)
        {
            Window = window; Text = text; focus = focused; process = pid; title = caption;
            nativeEdit = edit; richEdit = rich; start = first; end = last;
        }
        internal static bool SameText(string first, string second)
        {
            if (first == null || second == null) return false;
            return first.Replace("\r\n", "\n").Replace('\r', '\n') == second.Replace("\r\n", "\n").Replace('\r', '\n');
        }
        private static string Caption(IntPtr window)
        {
            var text = new StringBuilder(2048); GetWindowText(window, text, text.Capacity); return text.ToString();
        }
        private static IntPtr Focused(IntPtr window)
        {
            uint pid; uint thread = Native.GetWindowThreadProcessId(window, out pid);
            var info = new GuiInfo { Size = (uint)Marshal.SizeOf(typeof(GuiInfo)) };
            return thread != 0 && GetGUIThreadInfo(thread, ref info) ? info.Focus : IntPtr.Zero;
        }
        private static bool ReadSelection(IntPtr control, out int first, out int last)
        {
            first = last = -1; IntPtr buffer = Marshal.AllocHGlobal(8);
            try
            {
                Marshal.WriteInt32(buffer, -1); Marshal.WriteInt32(buffer, 4, -1);
                UIntPtr result;
                if (SendMessageTimeout(control, 0xB0, buffer, IntPtr.Add(buffer, 4), 3, 300, out result) == IntPtr.Zero) return false;
                first = Marshal.ReadInt32(buffer); last = Marshal.ReadInt32(buffer, 4);
                return first >= 0 && last > first;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        internal bool IsCurrent()
        {
            uint pid;
            return IsWindow(Window) && IsWindow(focus) && IsWindowEnabled(focus)
                && Native.GetForegroundWindow() == Window
                && Native.GetWindowThreadProcessId(Window, out pid) != 0 && pid == process
                && Focused(Window) == focus && Caption(Window) == title;
        }
        private static async Task<T> Probe<T>(Func<T> operation) where T : class
        {
            if (Interlocked.CompareExchange(ref probeBusy, 1, 0) != 0) return null;
            var task = Task.Run(() => {
                try { return operation(); }
                catch (Exception) { return null; }
                finally { Interlocked.Exchange(ref probeBusy, 0); }
            });
            return await Task.WhenAny(task, Task.Delay(800)) == task ? await task : null;
        }
        internal static async Task<DocumentTarget> Capture(IntPtr window, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || Native.GetForegroundWindow() != window) return null;
            uint pid; Native.GetWindowThreadProcessId(window, out pid);
            IntPtr focus = Focused(window);
            if (focus == IntPtr.Zero || !IsWindowEnabled(focus)) return null;
            var name = new StringBuilder(256); GetClassName(focus, name, name.Capacity);
            string className = name.ToString();
            bool edit = className.Equals("Edit", StringComparison.OrdinalIgnoreCase)
                || className.StartsWith("WindowsForms10.EDIT.", StringComparison.OrdinalIgnoreCase)
                || className.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase)
                || className.StartsWith("WindowsForms10.RichEdit", StringComparison.OrdinalIgnoreCase);
            int first = -1, last = -1;
            if (edit && ((GetWindowLong(focus, -16) & 0x800) != 0 || !ReadSelection(focus, out first, out last))) return null;
            var target = new DocumentTarget(window, text, focus, pid, Caption(window), edit, className.IndexOf("RichEdit", StringComparison.OrdinalIgnoreCase) >= 0, first, last);
            if (edit) return target.IsCurrent() ? target : null;
            return await Probe(() => {
                var focused = AutomationElement.FocusedElement;
                if (focused == null || focused.Current.IsPassword) return null;
                target.focusId = focused.GetRuntimeId();
                var element = focused;
                for (int depth = 0; element != null && depth < 6; depth++)
                {
                    if (element.Current.ProcessId != pid || element.Current.IsPassword) return null;
                    object value;
                    if (element.TryGetCurrentPattern(ValuePattern.Pattern, out value) && ((ValuePattern)value).Current.IsReadOnly) return null;
                    object pattern;
                    if (element.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
                    {
                        var ranges = ((TextPattern)pattern).GetSelection();
                        if (ranges.Length == 1 && SameText(ranges[0].GetText(GoogleTranslator.MaxLength + 1), text))
                        {
                            target.owner = element; target.originalRange = ranges[0].Clone();
                            return target.IsCurrent() ? target : null;
                        }
                    }
                    element = TreeWalker.ControlViewWalker.GetParent(element);
                }
                return null;
            });
        }
        private bool SameAutomationFocus()
        {
            var element = AutomationElement.FocusedElement;
            if (element == null || element.Current.IsPassword) return false;
            int[] current = element.GetRuntimeId();
            if (current.Length != focusId.Length) return false;
            for (int i = 0; i < current.Length; i++) if (current[i] != focusId[i]) return false;
            object value;
            return !owner.TryGetCurrentPattern(ValuePattern.Pattern, out value) || !((ValuePattern)value).Current.IsReadOnly;
        }
        internal async Task<bool> VerifySelection()
        {
            if (!IsCurrent()) return false;
            if (nativeEdit)
            {
                int first, last;
                return (GetWindowLong(focus, -16) & 0x800) == 0 && ReadSelection(focus, out first, out last) && first == start && last == end;
            }
            var valid = await Probe(() => {
                if (!SameAutomationFocus()) return null;
                var ranges = ((TextPattern)owner.GetCurrentPattern(TextPattern.Pattern)).GetSelection();
                if (ranges.Length != 1 || !SameText(ranges[0].GetText(GoogleTranslator.MaxLength + 1), Text)) return null;
                return ranges[0].CompareEndpoints(TextPatternRangeEndpoint.Start, originalRange, TextPatternRangeEndpoint.Start) == 0
                    && ranges[0].CompareEndpoints(TextPatternRangeEndpoint.End, originalRange, TextPatternRangeEndpoint.End) == 0 ? "valid" : null;
            });
            return valid != null && IsCurrent();
        }
        internal bool Paste()
        {
            if (!IsCurrent() || Native.Ctrl || Native.ShiftOrAlt) return false;
            if (nativeEdit)
            {
                int first, last; UIntPtr result;
                if ((GetWindowLong(focus, -16) & 0x800) != 0 || !ReadSelection(focus, out first, out last) || first != start || last != end) return false;
                return SendMessageTimeout(focus, 0x302, IntPtr.Zero, IntPtr.Zero, 3, 700, out result) != IntPtr.Zero;
            }
            Native.Paste(); return true;
        }
        internal async Task<bool> ConfirmReplacement(string text)
        {
            if (!IsCurrent()) return false;
            if (nativeEdit)
            {
                IntPtr buffer = Marshal.AllocHGlobal(8);
                try
                {
                    Marshal.WriteInt32(buffer, -1); Marshal.WriteInt32(buffer, 4, -1);
                    UIntPtr result;
                    if (SendMessageTimeout(focus, 0xB0, buffer, IntPtr.Add(buffer, 4), 3, 300, out result) == IntPtr.Zero) return false;
                    int caret = Marshal.ReadInt32(buffer), last = Marshal.ReadInt32(buffer, 4);
                    if (caret != last || caret <= start || caret > 500000) return false;
                    int capacity = richEdit ? caret * 2 + 2 : caret + 1;
                    IntPtr content = Marshal.AllocHGlobal(capacity * 2);
                    try
                    {
                        if (SendMessageTimeout(focus, 0xD, new IntPtr(capacity), content, 3, 300, out result) == IntPtr.Zero) return false;
                        ulong count = result.ToUInt64(); if (count >= (ulong)capacity) return false;
                        string actual = Marshal.PtrToStringUni(content, (int)count);
                        if (richEdit) actual = actual.Replace("\r\n", "\r");
                        return actual.Length >= caret && SameText(actual.Substring(start, caret - start), text);
                    }
                    finally { Marshal.FreeHGlobal(content); }
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            return await Probe(() => {
                if (!SameAutomationFocus()) return null;
                var ranges = ((TextPattern)owner.GetCurrentPattern(TextPattern.Pattern)).GetSelection();
                if (ranges.Length != 1) return null;
                var range = ranges[0].Clone();
                if (SameText(range.GetText(GoogleTranslator.MaxLength + 1), text)) return "confirmed";
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -text.Length);
                return SameText(range.GetText(GoogleTranslator.MaxLength + 1), text) ? "confirmed" : null;
            }) != null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Windows.Automation;

[assembly: System.Reflection.AssemblyTitle("Léxora")]
[assembly: System.Reflection.AssemblyProduct("Léxora")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.4.0.0")]

namespace SeleccionTraductor
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool startInTray = Array.Exists(args, value => string.Equals(value, "--tray", StringComparison.OrdinalIgnoreCase));
            bool created;
            using (var mutex = new Mutex(true, "Local\\SeleccionTraductor.App", out created))
            {
                if (!created) { if (!startInTray) MessageBox.Show("Léxora ya está abierto. Busca su icono en la bandeja, junto al reloj.", "Léxora"); return; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                Application.Run(new TranslatorForm(startInTray: startInTray));
            }
        }
    }

    public sealed class Language
    {
        public string Code; public string Name;
        public Language(string code, string name) { Code = code; Name = name; }
        public override string ToString() { return Name; }
        public static readonly Language[] All = {
            new Language("es", "Español"), new Language("en", "Inglés"), new Language("fr", "Francés"),
            new Language("de", "Alemán"), new Language("it", "Italiano"), new Language("pt", "Portugués"),
            new Language("ca", "Catalán"), new Language("eu", "Euskera"), new Language("gl", "Gallego"),
            new Language("ar", "Árabe"), new Language("bg", "Búlgaro"), new Language("zh-CN", "Chino simplificado"),
            new Language("zh-TW", "Chino tradicional"), new Language("ko", "Coreano"), new Language("hr", "Croata"),
            new Language("da", "Danés"), new Language("sk", "Eslovaco"), new Language("sl", "Esloveno"),
            new Language("fi", "Finés"), new Language("el", "Griego"), new Language("he", "Hebreo"),
            new Language("hi", "Hindi"), new Language("hu", "Húngaro"), new Language("id", "Indonesio"),
            new Language("ja", "Japonés"), new Language("nl", "Neerlandés"), new Language("no", "Noruego"),
            new Language("pl", "Polaco"), new Language("ro", "Rumano"), new Language("ru", "Ruso"),
            new Language("sv", "Sueco"), new Language("th", "Tailandés"), new Language("tr", "Turco"),
            new Language("uk", "Ucraniano"), new Language("vi", "Vietnamita")
        };
    }

    public static class Preferences
    {
        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeleccionTraductor");
        public static string Load(string file)
        {
            try { string code = File.ReadAllText(file).Trim(); foreach (var l in Language.All) if (l.Code == code) return code; }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            return "es";
        }
        public static void Save(string file, string code)
        {
            bool valid = false; foreach (var l in Language.All) if (l.Code == code) valid = true;
            if (!valid) throw new ArgumentException("Idioma desconocido.");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temp = file + ".tmp";
            File.WriteAllText(temp, code, Encoding.UTF8);
            if (File.Exists(file)) File.Replace(temp, file, null); else File.Move(temp, file);
        }
    }

    public static class GoogleTranslator
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        public const int MaxLength = 20000;
        public static IEnumerable<string> Split(string text, int limit)
        {
            if (limit < 2) throw new ArgumentOutOfRangeException("limit");
            int offset = 0;
            while (offset < text.Length)
            {
                int size = Math.Min(limit, text.Length - offset);
                if (offset + size < text.Length)
                {
                    if (char.IsHighSurrogate(text[offset + size - 1])) size--;
                    int boundary = text.LastIndexOfAny(new[] { ' ', '\n', '\r', '.', '!', '?' }, offset + size - 1, size);
                    if (boundary >= offset + size / 2) size = boundary - offset + 1;
                }
                yield return text.Substring(offset, size); offset += size;
            }
        }
        public static string Parse(string json)
        {
            var root = new JavaScriptSerializer().DeserializeObject(json) as object[];
            if (root == null || root.Length == 0) throw new FormatException();
            var segments = root[0] as object[];
            if (segments == null) throw new FormatException();
            var result = new StringBuilder();
            foreach (object item in segments)
            {
                var segment = item as object[];
                if (segment != null && segment.Length > 0 && segment[0] is string) result.Append((string)segment[0]);
            }
            if (result.Length == 0) throw new FormatException();
            return result.ToString();
        }
        public static async Task<string> Translate(string text, string target, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Selecciona o escribe un texto.");
            if (text.Length > MaxLength) throw new ArgumentException("El límite es de 20.000 caracteres por traducción.");
            var result = new StringBuilder();
            foreach (string part in Split(text, 1200))
            {
                token.ThrowIfCancellationRequested();
                string url = "https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&sl=auto&dt=t&tl=" + Uri.EscapeDataString(target) + "&q=" + Uri.EscapeDataString(part);
                using (var response = await Http.GetAsync(url, token).ConfigureAwait(false))
                {
                    if ((int)response.StatusCode == 429) throw new InvalidOperationException("Google ha limitado las peticiones. Espera antes de volver a intentarlo o usa «Abrir en Google».");
                    if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Google no está disponible. Puedes usar «Abrir en Google».");
                    try
                    {
                        string translated = Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        int end = part.Length;
                        while (end > 0 && char.IsWhiteSpace(part[end - 1])) end--;
                        // Google puede omitir el espacio final entre dos fragmentos.
                        if (end < part.Length) translated = translated.TrimEnd() + part.Substring(end);
                        result.Append(translated);
                    }
                    catch (FormatException) { throw new InvalidOperationException("Google ha devuelto un formato no compatible. Usa «Abrir en Google»."); }
                }
            }
            return result.ToString();
        }
    }

    internal static class Native
    {
        internal delegate IntPtr MouseProc(int code, IntPtr message, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct MouseData { public Point Position; public uint Mouse, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct KeyboardData { public uint Key, Scan, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint Type; public InputUnion Data; }
        [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)] public KeyInput Key; [FieldOffset(0)] public MouseInput Mouse; }
        [StructLayout(LayoutKind.Sequential)] internal struct KeyInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int id, MouseProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("user32.dll")] internal static extern uint GetDoubleClickTime();
        internal static bool Ctrl { get { return GetAsyncKeyState(0x11) < 0; } }
        internal static bool ShiftOrAlt { get { return GetAsyncKeyState(0x10) < 0 || GetAsyncKeyState(0x12) < 0; } }
        internal static void Copy()
        {
            Input[] keys = new Input[4]; ushort[] codes = { 0x11, 0x43, 0x43, 0x11 };
            for (int i = 0; i < 4; i++) { keys[i].Type = 1; keys[i].Data.Key.Key = codes[i]; keys[i].Data.Key.Flags = i >= 2 ? 2u : 0u; }
            if (SendInput(4, keys, Marshal.SizeOf(typeof(Input))) != 4) throw new InvalidOperationException("Windows no permite copiar desde esta aplicación.");
        }
        internal static void Paste()
        {
            Input[] keys = new Input[4]; ushort[] codes = { 0x11, 0x56, 0x56, 0x11 };
            for (int i = 0; i < 4; i++) { keys[i].Type = 1; keys[i].Data.Key.Key = codes[i]; keys[i].Data.Key.Flags = i >= 2 ? 2u : 0u; }
            if (SendInput(4, keys, Marshal.SizeOf(typeof(Input))) != 4) throw new InvalidOperationException("Windows no permite pegar en este documento.");
        }
    }

    internal sealed class SelectionButton : Form
    {
        private readonly ToolTip hint = new ToolTip();
        internal readonly ContextMenuStrip TranslateMenu = new ContextMenuStrip();
        internal bool MenuContains(System.Drawing.Point point) { return TranslateMenu.Visible && ContainsScreenPoint(TranslateMenu.Handle, point); }
        internal bool ContainsPoint(System.Drawing.Point point) { return Visible && ContainsScreenPoint(Handle, point); }
        private static bool ContainsScreenPoint(IntPtr handle, System.Drawing.Point point)
        {
            Native.Rect rect;
            return Native.GetWindowRect(handle, out rect) && point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom;
        }
        internal event EventHandler Translate;
        public SelectionButton()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual; Size = new Size(44, 38); BackColor = Color.FromArgb(32, 79, 203);
            Cursor = Cursors.Hand; DoubleBuffered = true;
            AccessibleName = "Traducir texto seleccionado";
            TranslateMenu.Items.Add("Traducir con Léxora", null, delegate { RequestTranslation(); });
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x08000088; return cp; }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
            base.WndProc(ref m);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var titleFont = new Font("Segoe UI", 17, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, "T", titleFont, new Rectangle(3, 0, 25, 36), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, "a", Font, new Rectangle(25, 12, 17, 22), Color.White, TextFormatFlags.HorizontalCenter);
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.FromArgb(18, 52, 145), ButtonBorderStyle.Solid);
        }
        private void RequestTranslation() { var handler = Translate; if (handler != null) handler(this, EventArgs.Empty); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!ClientRectangle.Contains(e.Location)) return;
            if (e.Button == MouseButtons.Left) RequestTranslation();
            else if (e.Button == MouseButtons.Right) TranslateMenu.Show(this, e.Location);
        }
        internal void Near(System.Drawing.Point point, string target)
        {
            Rectangle area = Screen.FromPoint(point).WorkingArea;
            int x = Math.Max(area.Left, Math.Min(point.X + 12, area.Right - Width));
            int y = point.Y + 18 + Height <= area.Bottom ? point.Y + 18 : point.Y - Height - 12;
            Location = new System.Drawing.Point(x, Math.Max(area.Top, y));
            hint.SetToolTip(this, "Traducir con Léxora a " + target); Show();
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x53);
        }
        protected override void Dispose(bool disposing) { if (disposing) { hint.Dispose(); TranslateMenu.Dispose(); } base.Dispose(disposing); }
    }

    internal sealed class SelectionProbe
    {
        internal bool Supported;
        internal string Text;
        internal static SelectionProbe Read(IntPtr window, System.Drawing.Point point)
        {
            var result = new SelectionProbe();
            uint expected; Native.GetWindowThreadProcessId(window, out expected);
            AutomationElement focused = null, underMouse = null;
            try { focused = AutomationElement.FocusedElement; } catch (ElementNotAvailableException) { }
            try { underMouse = AutomationElement.FromPoint(new System.Windows.Point(point.X, point.Y)); } catch (ElementNotAvailableException) { }
            foreach (var start in new[] { focused, underMouse })
            {
                var element = start;
                for (int depth = 0; element != null && depth < 6; depth++)
                {
                    if ((uint)element.Current.ProcessId != expected || element.Current.IsPassword) break;
                    object pattern;
                    if (element.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
                    {
                        result.Supported = true;
                        var selected = ((TextPattern)pattern).GetSelection(); var text = new StringBuilder();
                        foreach (var range in selected) text.Append(range.GetText(GoogleTranslator.MaxLength + 1));
                        if (!string.IsNullOrWhiteSpace(text.ToString())) { result.Text = text.ToString(); return result; }
                    }
                    element = TreeWalker.ControlViewWalker.GetParent(element);
                }
            }
            return result;
        }
    }

    internal sealed class CloseChoiceDialog : Form
    {
        internal CloseChoiceDialog()
        {
            Text = "Cerrar Léxora"; FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 10);
            ClientSize = new Size(540, 165); AutoScaleMode = AutoScaleMode.Dpi;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), RowCount = 2, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = "¿Qué quieres hacer?\nEn la bandeja seguirá disponible el icono flotante y Ctrl + C + C.", Dock = DockStyle.Fill }, 0, 0);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var background = new Button { Text = "Dejar en la bandeja", AutoSize = true, MinimumSize = new Size(170, 36), DialogResult = DialogResult.Yes };
            var exit = new Button { Text = "Cerrar aplicación", AutoSize = true, MinimumSize = new Size(155, 36), DialogResult = DialogResult.No };
            var cancelClose = new Button { Text = "Cancelar", AutoSize = true, MinimumSize = new Size(100, 36), DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(background); buttons.Controls.Add(exit); buttons.Controls.Add(cancelClose);
            layout.Controls.Add(buttons, 0, 1); Controls.Add(layout); AcceptButton = background; CancelButton = cancelClose;
        }
    }

    public sealed class TranslatorForm : Form
    {
        private readonly ComboBox language = new ComboBox();
        private readonly TextBox source = new TextBox(), translated = new TextBox();
        private readonly Label status = new Label();
        private readonly Button translate = new Button(), cancel = new Button(), clear = new Button(), replace = new Button();
        private readonly ToolTip replacementHelp = new ToolTip();
        private DocumentTarget documentTarget;
        private string translatedInput;
        private bool replacing;
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly string settings;
        private Native.MouseProc mouseProc;
        private Native.MouseProc keyboardProc;
        private IntPtr hook, keyboardHook;
        private bool exiting, capturing, mouseDown, iconEnabled = true, cPressed;
        private long lastCopy;
        private IntPtr copyWindow;
        private uint copySequence;
        private long lastClick;
        private Native.Point lastPoint, downPoint;
        private readonly SelectionButton selectionButton = new SelectionButton();
        private readonly System.Windows.Forms.Timer selectionTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer visibilityTimer = new System.Windows.Forms.Timer();
        private IntPtr selectionWindow;
        private System.Drawing.Point selectionPoint;
        private string selectionText;
        private bool fallbackCandidate;
        private int revision, probeBusy, contentRevision;
        private CancellationTokenSource request;
        private readonly Func<string, string, CancellationToken, Task<string>> translationService;
        private bool hideOnFirstShow, hooksInitialized;

        public TranslatorForm(string preferencesFile = null, Func<string, string, CancellationToken, Task<string>> translator = null, bool startInTray = false)
        {
            hideOnFirstShow = startInTray;
            translationService = translator ?? GoogleTranslator.Translate;
            settings = preferencesFile ?? Path.Combine(Preferences.Folder, "language.txt");
            Text = "Léxora · Traductor"; Size = new Size(900, 610); MinimumSize = new Size(620, 440);
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(246, 248, 252);
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; AutoScaleMode = AutoScaleMode.Dpi;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 6 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.Controls.Add(new Label { Text = "Traduce tu selección", Font = new Font("Segoe UI", 19, FontStyle.Bold), AutoSize = true }, 0, 0);
            var top = new FlowLayoutPanel { Dock = DockStyle.Fill };
            top.Controls.Add(new Label { Text = "Traducir siempre a", AutoSize = true, Margin = new Padding(0, 7, 12, 0) });
            language.DropDownStyle = ComboBoxStyle.DropDownList; language.Width = 210;
            language.Items.AddRange(Language.All); string saved = Preferences.Load(settings);
            foreach (Language l in language.Items) if (l.Code == saved) language.SelectedItem = l;
            language.SelectedIndexChanged += delegate {
                if (request != null) request.Cancel(); translated.Clear();
                try { Preferences.Save(settings, Target); SetStatus("Idioma guardado: " + language.Text); }
                catch (Exception ex) { if (!(ex is IOException) && !(ex is UnauthorizedAccessException)) throw; SetStatus("No se ha podido guardar el idioma. Comprueba los permisos."); }
            };
            top.Controls.Add(language); layout.Controls.Add(top, 0, 1);
            var editors = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            editors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); editors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            editors.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); editors.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            editors.Controls.Add(new Label { Text = "ORIGINAL · detección automática", AutoSize = true }, 0, 0);
            editors.Controls.Add(new Label { Text = "TRADUCCIÓN", AutoSize = true }, 1, 0);
            foreach (TextBox box in new[] { source, translated }) { box.Multiline = true; box.ScrollBars = ScrollBars.Vertical; box.Dock = DockStyle.Fill; box.Font = new Font("Segoe UI", 12); box.BorderStyle = BorderStyle.FixedSingle; }
            source.MaxLength = GoogleTranslator.MaxLength; translated.ReadOnly = true; translated.BackColor = Color.White;
            source.TextChanged += delegate { documentTarget = null; UpdateReplacement(); };
            translated.TextChanged += delegate { UpdateReplacement(); };
            editors.Controls.Add(source, 0, 1); editors.Controls.Add(translated, 1, 1); layout.Controls.Add(editors, 0, 2);
            source.ContextMenuStrip = EditorMenu(source); translated.ContextMenuStrip = EditorMenu(translated);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 7, 0, 7) };
            translate.Text = "Traducir"; translate.AutoSize = true; translate.MinimumSize = new Size(110, 36); translate.TextAlign = ContentAlignment.MiddleCenter; translate.BackColor = Color.FromArgb(32, 79, 203); translate.ForeColor = Color.White; translate.FlatStyle = FlatStyle.Flat;
            translate.Click += async delegate { await TranslateText(); }; actions.Controls.Add(translate);
            clear.Text = "Borrar"; clear.AutoSize = true; clear.MinimumSize = new Size(80, 32);
            clear.Click += delegate { ClearTexts(); }; actions.Controls.Add(clear);
            var copy = new Button { Text = "Copiar resultado", AutoSize = true, MinimumSize = new Size(120, 32) }; copy.Click += delegate { try { if (translated.TextLength > 0) Clipboard.SetText(translated.Text); } catch (ExternalException) { SetStatus("Portapapeles ocupado. Vuelve a intentarlo."); } }; actions.Controls.Add(copy);
            replace.Text = "Reemplazar en documento"; replace.AutoSize = true; replace.MinimumSize = new Size(190, 32); replace.Enabled = false;
            replace.Click += async delegate { await ReplaceInDocument(); }; actions.Controls.Add(replace);
            replacementHelp.SetToolTip(replace, "Sustituye la selección de origen si sigue siendo la misma. La traducción queda copiada; puedes deshacer en el documento con Ctrl+Z.");
            var web = new Button { Text = "Abrir en Google", AutoSize = true, MinimumSize = new Size(115, 32) }; web.Click += delegate { OpenGoogle(); }; actions.Controls.Add(web);
            cancel.Text = "Cancelar"; cancel.Size = new Size(85, 32); cancel.Enabled = false; cancel.Click += delegate { if (request != null) request.Cancel(); }; actions.Controls.Add(cancel);
            layout.Controls.Add(actions, 0, 3);
            layout.Controls.Add(new Label { Text = "Selecciona texto y pulsa el icono azul que aparece junto a la selección.\nAtajo: mantén Ctrl y pulsa C dos veces. Al cerrar puedes elegir bandeja o salir.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(65, 77, 98) }, 0, 4);
            status.Dock = DockStyle.Fill; status.ForeColor = Color.FromArgb(32, 79, 160); layout.Controls.Add(status, 0, 5);
            Controls.Add(layout);
            var menu = new ContextMenuStrip(); menu.Items.Add("Abrir Léxora", null, delegate { Reveal(); });
            var toggle = new ToolStripMenuItem("Mostrar icono al seleccionar") { Checked = true, CheckOnClick = true };
            toggle.CheckedChanged += delegate { iconEnabled = toggle.Checked; DismissSelection(); }; menu.Items.Add(toggle);
            var startup = new ToolStripMenuItem("Iniciar con Windows (en la bandeja)") { CheckOnClick = true };
            bool updatingStartup = false;
            menu.Opening += delegate {
                updatingStartup = true;
                try { startup.Checked = StartupSettings.IsEnabled(Application.ExecutablePath); }
                catch (Exception ex) { if (!(ex is UnauthorizedAccessException) && !(ex is System.Security.SecurityException) && !(ex is IOException)) throw; SetStatus("No se pudo consultar el inicio con Windows."); }
                finally { updatingStartup = false; }
            };
            startup.CheckedChanged += delegate {
                if (updatingStartup) return;
                try { StartupSettings.SetEnabled(Application.ExecutablePath, startup.Checked); }
                catch (Exception ex) {
                    if (!(ex is UnauthorizedAccessException) && !(ex is System.Security.SecurityException) && !(ex is IOException)) throw;
                    updatingStartup = true; startup.Checked = !startup.Checked; updatingStartup = false;
                    SetStatus("No se pudo cambiar el inicio con Windows. Comprueba los permisos de tu usuario.");
                }
            };
            menu.Items.Add(startup);
            menu.Items.Add("Salir", null, delegate { exiting = true; Close(); });
            tray.Text = "Léxora · Selecciona y pulsa el icono"; tray.Icon = Icon; tray.ContextMenuStrip = menu; tray.Visible = true; tray.DoubleClick += delegate { Reveal(); };
            selectionTimer.Interval = 180; selectionTimer.Tick += async delegate { selectionTimer.Stop(); await InspectSelection(); };
            visibilityTimer.Interval = 200; visibilityTimer.Tick += delegate { if (selectionButton.Visible && !selectionButton.TranslateMenu.Visible && Native.GetForegroundWindow() != selectionWindow) DismissSelection(); }; visibilityTimer.Start();
            selectionButton.Translate += delegate {
                IntPtr window = selectionWindow; string text = selectionText;
                DismissSelection();
                // El menú termina de restaurar el foco antes de volver al documento seleccionado.
                BeginInvoke((Action)(() => {
                    if (exiting || window == IntPtr.Zero) return;
                    if (Native.GetForegroundWindow() != window) Native.SetForegroundWindow(window);
                    if (Native.GetForegroundWindow() == window) CaptureFrom(window, text);
                }));
            };
            SetStatus("Google web · El texto se envía a Google al traducir. Sin historial local.");
        }
        private string Target { get { return ((Language)language.SelectedItem).Code; } }
        private ContextMenuStrip EditorMenu(TextBox editor)
        {
            var menu = new ContextMenuStrip();
            var item = menu.Items.Add("Traducir con Léxora", null, async delegate {
                string text = editor.SelectionLength > 0 ? editor.SelectedText : editor.Text;
                await TranslateValue(text);
            });
            menu.Items.Add(new ToolStripSeparator());
            if (!editor.ReadOnly)
            {
                menu.Items.Add("Deshacer", null, delegate { editor.Undo(); });
                menu.Items.Add("Cortar", null, delegate { try { editor.Cut(); } catch (ExternalException) { SetStatus("Portapapeles ocupado."); } });
            }
            menu.Items.Add("Copiar", null, delegate { try { editor.Copy(); } catch (ExternalException) { SetStatus("Portapapeles ocupado."); } });
            if (!editor.ReadOnly) menu.Items.Add("Pegar", null, delegate { try { editor.Paste(); } catch (ExternalException) { SetStatus("Portapapeles ocupado."); } });
            if (!editor.ReadOnly) menu.Items.Add("Eliminar selección", null, delegate { editor.SelectedText = ""; });
            menu.Items.Add("Seleccionar todo", null, delegate { editor.SelectAll(); });
            menu.Opening += delegate { item.Enabled = request == null && !string.IsNullOrWhiteSpace(editor.Text); };
            return menu;
        }
        private void SetStatus(string text) { status.Text = text; }
        private void Reveal() { hideOnFirstShow = false; Show(); WindowState = FormWindowState.Normal; Activate(); }
        protected override void SetVisibleCore(bool value)
        {
            if (value && hideOnFirstShow)
            {
                hideOnFirstShow = false; value = false;
                if (!IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (hooksInitialized) return;
            hooksInitialized = true; mouseProc = OnMouse;
            hook = Native.SetWindowsHookEx(14, mouseProc, Native.GetModuleHandle(null), 0);
            keyboardProc = OnKeyboard; keyboardHook = Native.SetWindowsHookEx(13, keyboardProc, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero) SetStatus("No se ha podido activar el icono flotante. Usa Ctrl + C + C o pega el texto.");
            else if (keyboardHook == IntPtr.Zero) SetStatus("No se ha podido activar Ctrl + C + C. Puedes usar el icono flotante.");
        }
        private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                int msg = message.ToInt32();
                var info = (Native.MouseData)Marshal.PtrToStructure(data, typeof(Native.MouseData));
                var point = new System.Drawing.Point(info.Position.X, info.Position.Y);
                if (selectionButton.MenuContains(point) || selectionButton.ContainsPoint(point)) return Native.CallNextHookEx(hook, code, message, data);
                if (msg == 0x201)
                {
                    mouseDown = true; downPoint = info.Position; DismissSelection();
                }
                else if (msg == 0x202 && mouseDown)
                {
                    mouseDown = false;
                    long now = Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;
                    bool twice = lastClick != 0 && now - lastClick <= Native.GetDoubleClickTime() && Math.Abs(info.Position.X - lastPoint.X) <= SystemInformation.DoubleClickSize.Width / 2 && Math.Abs(info.Position.Y - lastPoint.Y) <= SystemInformation.DoubleClickSize.Height / 2;
                    bool drag = Math.Abs(info.Position.X - downPoint.X) > SystemInformation.DragSize.Width || Math.Abs(info.Position.Y - downPoint.Y) > SystemInformation.DragSize.Height;
                    lastClick = now; lastPoint = info.Position;
                    QueueSelection(point, drag || twice);
                }
                else if (msg == 0x20A || msg == 0x204) DismissSelection();
            }
            return Native.CallNextHookEx(hook, code, message, data);
        }
        private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                var info = (Native.KeyboardData)Marshal.PtrToStructure(data, typeof(Native.KeyboardData));
                if ((info.Flags & 0x10) == 0)
                {
                    int msg = message.ToInt32();
                    if (msg == 0x101 && info.Key == 0x43) cPressed = false;
                    if (msg == 0x100 && info.Key == 0x43 && Native.Ctrl && !Native.ShiftOrAlt && !cPressed)
                    {
                        cPressed = true;
                        long now = Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;
                        IntPtr window = Native.GetForegroundWindow();
                        uint process; Native.GetWindowThreadProcessId(window, out process);
                        if (process != (uint)Process.GetCurrentProcess().Id && window != IntPtr.Zero)
                        {
                            if (lastCopy != 0 && now - lastCopy <= 850 && window == copyWindow)
                            {
                                uint sequence = copySequence; lastCopy = 0;
                                BeginInvoke((Action)(() => TranslateCopied(window, sequence)));
                            }
                            else { lastCopy = now; copyWindow = window; copySequence = Native.GetClipboardSequenceNumber(); }
                        }
                    }
                    else if (msg == 0x100 && info.Key != 0x43) lastCopy = 0;
                    if (msg == 0x100 || msg == 0x104) DismissSelection();
                    else if (msg == 0x101 && (info.Key == 0x10 || info.Key == 0xA0 || info.Key == 0xA1 || (info.Key == 0x41 && Native.Ctrl))) QueueSelection(Cursor.Position, false);
                }
            }
            return Native.CallNextHookEx(keyboardHook, code, message, data);
        }
        private async void TranslateCopied(IntPtr window, uint before)
        {
            if (capturing || request != null || replacing) return;
            await Task.Delay(180);
            if (exiting || Native.GetForegroundWindow() != window) return;
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    if (Native.GetClipboardSequenceNumber() != before && Clipboard.ContainsText())
                    {
                        string text = Clipboard.GetText(); DismissSelection(); CaptureFrom(window, text); return;
                    }
                }
                catch (ExternalException) { }
                await Task.Delay(40);
                if (exiting || Native.GetForegroundWindow() != window) return;
            }
            SetStatus("No se pudo copiar la selección. Selecciona texto copiable o pégalo en la ventana.");
        }
        private void DismissSelection()
        {
            revision++; selectionTimer.Stop(); selectionButton.TranslateMenu.Close(); selectionButton.Hide(); selectionText = null;
        }
        private void QueueSelection(System.Drawing.Point point, bool candidate)
        {
            if (!iconEnabled || capturing || request != null || replacing) return;
            IntPtr window = Native.GetForegroundWindow(); uint process; Native.GetWindowThreadProcessId(window, out process);
            if (process == (uint)Process.GetCurrentProcess().Id || window == IntPtr.Zero) return;
            revision++; selectionWindow = window; selectionPoint = point; fallbackCandidate = candidate;
            selectionTimer.Stop(); selectionTimer.Start();
        }
        private async Task InspectSelection()
        {
            int version = revision; IntPtr window = selectionWindow; var point = selectionPoint; bool candidate = fallbackCandidate;
            if (!iconEnabled || mouseDown || Native.GetForegroundWindow() != window) return;
            // Un arrastre o doble clic ofrece el icono aunque el proveedor de accesibilidad no exponga su selección.
            if (candidate) selectionButton.Near(point, language.Text);
            SelectionProbe found = null;
            // Un único proveedor de accesibilidad en curso; uno bloqueado no bloquea la interfaz ni acumula hilos.
            if (Interlocked.CompareExchange(ref probeBusy, 1, 0) == 0)
            {
                var probe = Task.Run(() => {
                    try { return SelectionProbe.Read(window, point); }
                    catch (Exception) { return new SelectionProbe(); }
                    finally { Interlocked.Exchange(ref probeBusy, 0); }
                });
                if (await Task.WhenAny(probe, Task.Delay(650)) == probe) found = await probe;
            }
            if (exiting || version != revision || mouseDown || !iconEnabled || Native.GetForegroundWindow() != window) return;
            if (found != null && !string.IsNullOrWhiteSpace(found.Text)) selectionText = found.Text;
            else if (!candidate) return;
            // Si la aplicación no expone accesibilidad, el icono permite intentar copiar tras un arrastre/doble clic.
            selectionButton.Near(point, language.Text);
        }
        private void ClearTexts()
        {
            contentRevision++;
            documentTarget = null; translatedInput = null;
            if (request != null) request.Cancel();
            DismissSelection(); source.Clear(); translated.Clear();
            SetStatus("Textos borrados."); source.Focus();
        }
        private async void CaptureFrom(IntPtr window, string knownText)
        {
            if (capturing || request != null || replacing) return;
            uint process; Native.GetWindowThreadProcessId(window, out process);
            if (process == (uint)Process.GetCurrentProcess().Id) return;
            capturing = true;
            try
            {
                var watch = Stopwatch.StartNew();
                while (Native.Ctrl || Native.ShiftOrAlt || Native.GetAsyncKeyState(1) < 0)
                {
                    if (watch.ElapsedMilliseconds > 4000) { SetStatus("Suelta Ctrl y el botón del ratón para traducir."); return; }
                    await Task.Delay(25);
                }
                if (Native.GetForegroundWindow() != window) return;
                string text = knownText ?? await CaptureSelection(window);
                if (string.IsNullOrWhiteSpace(text)) { Reveal(); SetStatus("No se pudo copiar la selección. Selecciona texto copiable y vuelve a intentarlo, o pégalo aquí."); return; }
                var target = await DocumentTarget.Capture(window, text);
                if (exiting || Native.GetForegroundWindow() != window) return;
                source.Text = text; documentTarget = target; translatedInput = null; UpdateReplacement();
                Reveal(); await TranslateText();
            }
            catch (Exception ex)
            {
                if (!(ex is ExternalException) && !(ex is InvalidOperationException) && !(ex is System.Runtime.Serialization.SerializationException)) throw;
                Reveal(); SetStatus("No se ha podido capturar el texto. Puedes copiarlo y pegarlo aquí.");
            }
            finally { capturing = false; UpdateReplacement(); }
        }
        private void UpdateReplacement()
        {
            replace.Enabled = !replacing && !capturing && request == null && documentTarget != null
                && DocumentTarget.SameText(source.Text, documentTarget.Text)
                && DocumentTarget.SameText(translatedInput, documentTarget.Text) && !string.IsNullOrWhiteSpace(translated.Text);
        }
        private async Task ReplaceInDocument()
        {
            if (!replace.Enabled || replacing || request != null) return;
            var target = documentTarget; string result = translated.Text;
            replacing = true; UpdateReplacement(); language.Enabled = false; translate.Enabled = false; clear.Enabled = false; source.ReadOnly = true;
            DismissSelection();
            Action<string> fail = message => { documentTarget = null; if (!exiting) { Reveal(); SetStatus(message); } };
            try
            {
                var watch = Stopwatch.StartNew();
                while (Native.Ctrl || Native.ShiftOrAlt || Native.GetAsyncKeyState(1) < 0)
                {
                    if (watch.ElapsedMilliseconds > 1500) { fail("Suelta las teclas y el botón del ratón antes de reemplazar."); return; }
                    await Task.Delay(25);
                }
                Native.SetForegroundWindow(target.Window); await Task.Delay(100);
                if (exiting) return;
                if (!await target.VerifySelection()) { fail("El documento o la selección han cambiado. Vuelve a seleccionar y traducir el texto."); return; }
                string current = await CaptureSelection(target.Window);
                if (exiting) return;
                if (!DocumentTarget.SameText(current, target.Text) || !await target.VerifySelection())
                {
                    fail("La selección ya no coincide con el original. No se ha reemplazado el texto."); return;
                }
                if (exiting || !target.IsCurrent() || Native.Ctrl || Native.ShiftOrAlt || Native.GetAsyncKeyState(1) < 0)
                {
                    fail("El documento ha perdido el foco. Vuelve a seleccionar y traducir el texto."); return;
                }
                Clipboard.SetText(result);
                if (!target.Paste()) { fail("No se pudo pegar en el documento. La traducción queda copiada para pegarla manualmente."); return; }
                documentTarget = null;
                bool confirmed = false;
                for (int i = 0; i < 3 && !exiting; i++)
                {
                    await Task.Delay(100);
                    if (await target.ConfirmReplacement(result)) { confirmed = true; break; }
                    if (!target.IsCurrent()) break;
                }
                if (!exiting)
                {
                    if (confirmed) SetStatus("Selección reemplazada en el documento. Puedes deshacer con Ctrl+Z.");
                    else fail("No se pudo confirmar el reemplazo. Comprueba el documento; la traducción queda copiada.");
                }
            }
            catch (Exception ex)
            {
                if (!(ex is ExternalException) && !(ex is InvalidOperationException) && !(ex is System.Runtime.Serialization.SerializationException)) throw;
                fail("No se pudo verificar la selección. El documento debe permitir editar y pegar texto.");
            }
            finally
            {
                replacing = false;
                if (!IsDisposed) { language.Enabled = true; translate.Enabled = request == null; clear.Enabled = true; source.ReadOnly = request != null; UpdateReplacement(); }
            }
        }
        internal static async Task<string> CaptureSelection(IntPtr window)
        {
            // Materializa los formatos antes de Ctrl+C para no conservar referencias de copia diferida.
            var backup = new DataObject(); IDataObject old = Clipboard.GetDataObject();
            if (old != null) foreach (string format in old.GetFormats(false))
            {
                object value = old.GetData(format, false);
                if (value != null) backup.SetData(format, false, value);
            }
            uint before = Native.GetClipboardSequenceNumber(); uint copied = before;
            try
            {
                if (Native.GetForegroundWindow() != window) return null;
                Native.Copy();
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(40);
                    if (Native.GetForegroundWindow() != window) return null;
                    copied = Native.GetClipboardSequenceNumber();
                    if (copied != before)
                    {
                        try { if (Clipboard.ContainsText()) { string text = Clipboard.GetText(); copied = Native.GetClipboardSequenceNumber(); return text; } }
                        catch (ExternalException) { continue; }
                    }
                }
                return null;
            }
            finally
            {
                // No pisa un nuevo contenido que el usuario haya copiado durante la captura.
                if (copied != before && Native.GetClipboardSequenceNumber() == copied)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        try { if (old == null) Clipboard.Clear(); else Clipboard.SetDataObject(backup, true); break; }
                        catch (ExternalException) { Thread.Sleep(30); if (Native.GetClipboardSequenceNumber() != copied) break; }
                    }
                }
            }
        }
        private Task TranslateText() { return TranslateValue(source.Text); }
        private async Task TranslateValue(string text)
        {
            if (request != null || replacing) return;
            translatedInput = null;
            if (string.IsNullOrWhiteSpace(text)) { SetStatus("Selecciona o escribe un texto."); return; }
            if (text.Length > GoogleTranslator.MaxLength) { SetStatus("El límite es de 20.000 caracteres. Selecciona un fragmento más corto."); return; }
            var current = new CancellationTokenSource(); request = current;
            int version = contentRevision;
            Action<string> report = message => { if (!exiting && version == contentRevision) SetStatus(message); };
            translate.Enabled = false; cancel.Enabled = true; source.ReadOnly = true; translated.Clear(); SetStatus("Traduciendo con Google…");
            try { string result = await translationService(text, Target, current.Token); current.Token.ThrowIfCancellationRequested(); if (!exiting && version == contentRevision) { translatedInput = text; translated.Text = result; report("Traducción completada · " + language.Text); } }
            catch (OperationCanceledException) { report("Traducción cancelada."); }
            catch (HttpRequestException) { report("No se pudo conectar con Google. Comprueba Internet o usa «Abrir en Google»."); }
            catch (ArgumentException ex) { report(ex.Message); }
            catch (InvalidOperationException ex) { report(ex.Message); }
            finally { request = null; current.Dispose(); translate.Enabled = true; cancel.Enabled = false; source.ReadOnly = false; UpdateReplacement(); }
        }
        private void OpenGoogle()
        {
            if (string.IsNullOrWhiteSpace(source.Text)) { SetStatus("Selecciona o escribe un texto."); return; }
            if (source.TextLength > 4000) { SetStatus("Para abrir Google en el navegador, reduce el texto a 4.000 caracteres."); return; }
            try { Process.Start(new ProcessStartInfo("https://translate.google.com/?sl=auto&tl=" + Uri.EscapeDataString(Target) + "&text=" + Uri.EscapeDataString(source.Text) + "&op=translate") { UseShellExecute = true }); }
            catch (Exception ex) { if (!(ex is System.ComponentModel.Win32Exception) && !(ex is InvalidOperationException)) throw; SetStatus("No se ha podido abrir el navegador predeterminado."); }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                DismissSelection();
                using (var dialog = new CloseChoiceDialog())
                {
                    DialogResult choice = dialog.ShowDialog(this);
                    if (choice != DialogResult.No)
                    {
                        e.Cancel = true;
                        if (choice == DialogResult.Yes) Hide();
                        return;
                    }
                }
            }
            base.OnFormClosing(e);
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (request != null) request.Cancel();
            exiting = true; DismissSelection(); selectionTimer.Dispose(); visibilityTimer.Dispose(); selectionButton.Dispose();
            if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
            if (keyboardHook != IntPtr.Zero) Native.UnhookWindowsHookEx(keyboardHook);
            tray.Visible = false; tray.Dispose(); base.OnFormClosed(e);
            replacementHelp.Dispose();
        }
    }
}

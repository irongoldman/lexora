using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SeleccionTraductor;

internal static class Tests
{
    sealed class TestEditorForm : Form
    {
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 8; return cp; } }
    }
    static string output;
    static bool interactive;
    static bool offline;
    static int passed;
    static int resultCode;
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from, uint to, bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr w, IntPtr l);
    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)] static extern IntPtr ReadText(IntPtr window, int message, IntPtr w, StringBuilder text);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Native.Point point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int length);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] static extern int SetWindowLong(IntPtr window, int index, int value);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr window);
    static void MouseAt(int x, int y, uint flags)
    {
        var screen = SystemInformation.VirtualScreen;
        var input = new Native.Input(); input.Data.Mouse.Flags = flags | 0xC001;
        input.Data.Mouse.X = (int)((x - screen.Left) * 65535L / Math.Max(1, screen.Width - 1));
        input.Data.Mouse.Y = (int)((y - screen.Top) * 65535L / Math.Max(1, screen.Height - 1));
        Assert(Native.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Native.Input))) == 1, "Entrada real de ratón");
    }
    static void ChooseClose(TranslatorForm app, DialogResult choice)
    {
        using (var timer = new System.Windows.Forms.Timer { Interval = 80 })
        {
            timer.Tick += delegate {
                var dialog = Application.OpenForms.OfType<CloseChoiceDialog>().FirstOrDefault();
                if (dialog == null) return;
                timer.Stop();
                using (var bitmap = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, "close-dialog.png")); }
                var buttons = dialog.Controls[0].Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>();
                buttons.Single(b => b.DialogResult == choice).PerformClick();
            };
            timer.Start(); app.Close();
        }
    }
    static void Foreground(IntPtr window)
    {
        uint process; uint thread = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out process);
        uint own = GetCurrentThreadId(); bool attached = own != thread && AttachThreadInput(own, thread, true);
        try { SetForegroundWindow(window); }
        finally { if (attached) AttachThreadInput(own, thread, false); }
    }
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) {
            Console.WriteLine("FAIL UI: " + e.Exception); resultCode = 1;
            if (output != null) File.WriteAllText(Path.Combine(output, "ui-error.txt"), e.Exception.ToString());
            Application.ExitThread();
        };
        if (args.Length == 2 && (args[0] == "--editor" || args[0] == "--replace-editor"))
        {
            var editor = new TestEditorForm { Text = "Documento de prueba del traductor", Size = new Size(500, 200), TopMost = true };
            var text = new TextBox { Multiline = true, Dock = DockStyle.Fill, Text = "Hello world", Font = new Font("Segoe UI", 18) };
            if (args[0] == "--replace-editor") text.Text = "Before Hello world After";
            editor.Controls.Add(text);
            editor.Shown += delegate { text.Focus(); text.SelectAll(); File.WriteAllText(args[1] + ".tmp", editor.Handle.ToInt64() + "," + text.Handle.ToInt64()); File.Move(args[1] + ".tmp", args[1]); };
            Application.Run(editor); return 0;
        }
        output = Path.GetFullPath(args[0]); interactive = args.Contains("--interactive"); offline = args.Contains("--offline"); Directory.CreateDirectory(output);
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        if (args.Contains("--startup")) return RunStartup();
        var app = new TranslatorForm(Path.Combine(output, "test-language.txt"), SimulateTranslation);
        app.Shown += async delegate
        {
            try { await Task.Delay(200); if (args.Contains("--replace")) await RunReplacement(app); else await Run(app); Console.WriteLine("PASS: " + passed + " comprobaciones"); }
            catch (Exception ex) { Console.WriteLine("FAIL: " + ex); resultCode = 1; }
            finally { Application.Exit(); }
        };
        Application.Run(app);
        return resultCode;
    }
    static void CheckStartupRegistration()
    {
        string keyPath = @"Software\Lexora-Tests-" + Guid.NewGuid().ToString("N");
        string executable = Path.Combine(output, "Ruta con espacios", "Lexora.exe");
        using (var existing = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath))
            if (existing != null) throw new Exception("Ya existe la clave de prueba.");
        try
        {
            Assert(StartupSettings.Command(executable) == "\"" + Path.GetFullPath(executable) + "\" --tray", "Inicio con Windows: ruta entre comillas y modo bandeja");
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath)) key.SetValue("OtherApp", "preservar");
            StartupSettings.SetEnabled(executable, true, keyPath);
            Assert(StartupSettings.IsEnabled(executable, keyPath), "Activar inicio automático en clave temporal aislada");
            StartupSettings.SetEnabled(executable, false, keyPath);
            Assert(!StartupSettings.IsEnabled(executable, keyPath), "Desactivar inicio automático retira solo su entrada");
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, true))
            {
                Assert((string)key.GetValue("OtherApp") == "preservar", "Otras entradas del registro conservadas");
                key.SetValue(StartupSettings.ValueName, "otro-programa.exe");
            }
            StartupSettings.SetEnabled(executable, false, keyPath);
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath))
                Assert((string)key.GetValue(StartupSettings.ValueName) == "otro-programa.exe", "No elimina entradas que pertenezcan a otro ejecutable");
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(keyPath, false); }
    }
    static int RunStartup()
    {
        CheckStartupRegistration();
        var app = new TranslatorForm(Path.Combine(output, "startup-language.txt"), SimulateTranslation, true);
        bool shown = false; app.Shown += delegate { shown = true; };
        using (var timer = new System.Windows.Forms.Timer { Interval = 300 })
        {
            timer.Tick += delegate {
                timer.Stop();
                try
                {
                    Assert(!app.Visible && !shown && app.IsHandleCreated, "Arranque en bandeja sin mostrar la ventana");
                    Assert(Field<NotifyIcon>(app, "tray").Visible, "Icono de bandeja activo en arranque oculto");
                    IntPtr mouse = Field<IntPtr>(app, "hook"), keyboard = Field<IntPtr>(app, "keyboardHook");
                    Assert(mouse != IntPtr.Zero && keyboard != IntPtr.Zero, "Atajo y detector de selección activos sin abrir ventana");
                    typeof(TranslatorForm).GetMethod("Reveal", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, null);
                    Application.DoEvents();
                    Assert(app.Visible && shown, "Abrir desde bandeja muestra la ventana");
                    Assert(Field<IntPtr>(app, "hook") == mouse && Field<IntPtr>(app, "keyboardHook") == keyboard, "Reabrir no duplica los hooks globales");
                    typeof(TranslatorForm).GetField("exiting", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(app, true);
                    app.Close();
                    Assert(app.IsDisposed && !Field<NotifyIcon>(app, "tray").Visible, "Salir del arranque en bandeja libera la aplicación");
                    Console.WriteLine("PASS: " + passed + " comprobaciones de inicio opcional y bandeja");
                }
                catch (Exception ex) { Console.WriteLine("FAIL: " + ex); resultCode = 1; Application.Exit(); }
            };
            timer.Start(); Application.Run(app);
        }
        return resultCode;
    }
    static async Task<string> SimulateTranslation(string text, string target, CancellationToken token)
    {
        await Task.Delay(80, token);
        if (text.Trim() == "Hello world") return "Hola Mundo";
        if (text.Trim() == "Goodbye") return "Adiós";
        if (text.Trim() == "Hello world\r\nGoodbye") return "Hola Mundo\r\nAdiós";
        throw new InvalidOperationException("Texto no previsto en la simulación de pruebas: " + text);
    }
    static void Assert(bool condition, string label)
    {
        if (!condition) throw new Exception(label); passed++; Console.WriteLine("OK " + label);
    }
    static T Field<T>(object instance, string name) { return (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance); }
    static async Task Until(Func<bool> condition, int ms, string error)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.ElapsedMilliseconds > ms) throw new Exception(error); await Task.Delay(50); }
    }
    static void Key(ushort key, bool up)
    {
        var input = new Native.Input { Type = 1 };
        input.Data.Key.Key = key; input.Data.Key.Flags = up ? 2u : 0u;
        Assert(Native.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Native.Input))) == 1, "Enviar tecla " + key + (up ? " arriba" : " abajo"));
    }
    static string EditorText(IntPtr editor)
    {
        var text = new StringBuilder(4096); ReadText(editor, 0xD, new IntPtr(text.Capacity), text); return text.ToString();
    }
    static async Task CaptureReplacement(TranslatorForm app, IntPtr window, IntPtr editor)
    {
        app.Hide();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Foreground(window); await Task.Delay(200);
            Rect bounds; GetWindowRect(editor, out bounds);
            MouseAt(bounds.Left + 30, bounds.Top + 30, 2); MouseAt(bounds.Left + 30, bounds.Top + 30, 4);
            await Task.Delay(200);
            if (Native.GetForegroundWindow() == window) break;
        }
        SendMessage(editor, 0xB1, new IntPtr(7), new IntPtr(18));
        IntPtr foreground = Native.GetForegroundWindow(); uint foregroundProcess; Native.GetWindowThreadProcessId(foreground, out foregroundProcess);
        Console.WriteLine("INFO Foco esperado=" + window + " actual=" + foreground + " proceso=" + foregroundProcess);
        Assert(foreground == window, "Documento temporal en primer plano");
        Field<TextBox>(app, "translated").Clear();
        typeof(TranslatorForm).GetMethod("CaptureFrom", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, new object[] { window, null });
        await Until(() => !Field<bool>(app, "capturing") && Field<CancellationTokenSource>(app, "request") == null && Field<TextBox>(app, "translated").TextLength > 0, 6000, "No se capturó la selección para reemplazar");
    }
    static Task ReplaceDocument(TranslatorForm app)
    {
        return (Task)typeof(TranslatorForm).GetMethod("ReplaceInDocument", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, null);
    }
    static async Task RunReplacement(TranslatorForm app)
    {
        IDataObject previous = Clipboard.GetDataObject(); var backup = new DataObject();
        if (previous != null) foreach (string format in previous.GetFormats(false)) { object data = previous.GetData(format, false); if (data != null) backup.SetData(format, false, data); }
        Process helper = null;
        string handshake = Path.Combine(output, "replace-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            helper = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--replace-editor \"" + handshake + "\"") { UseShellExecute = false });
            await Until(() => File.Exists(handshake), 5000, "No se abrió el editor temporal");
            string[] handles = File.ReadAllText(handshake).Split(',');
            IntPtr window = new IntPtr(long.Parse(handles[0])), editor = new IntPtr(long.Parse(handles[1]));
            await CaptureReplacement(app, window, editor);
            Assert(Field<Button>(app, "replace").Enabled, "Reemplazo habilitado para selección externa editable");
            await ReplaceDocument(app);
            Console.WriteLine("Documento temporal: " + EditorText(editor) + " / Estado: " + Field<Label>(app, "status").Text);
            Assert(EditorText(editor) == "Before Hola Mundo After", "Reemplaza únicamente la selección y conserva el resto");
            Assert(Field<Label>(app, "status").Text.Contains("reemplazada"), "Confirma el contenido real insertado");
            Assert(Clipboard.GetText() == "Hola Mundo" && !Field<Button>(app, "replace").Enabled, "Deja la traducción copiada y evita repetir el reemplazo");
            SendMessage(editor, 0x304, IntPtr.Zero, IntPtr.Zero);
            Assert(EditorText(editor) == "Before Hello world After", "Deshacer recupera el texto original");
            await CaptureReplacement(app, window, editor);
            SendMessage(editor, 0xB1, IntPtr.Zero, new IntPtr(6));
            await ReplaceDocument(app);
            Assert(EditorText(editor) == "Before Hello world After" && !Field<Button>(app, "replace").Enabled, "Selección cambiada cancela sin modificar el documento");
            SendMessage(editor, 0xCF, new IntPtr(1), IntPtr.Zero);
            await CaptureReplacement(app, window, editor);
            Assert(!Field<Button>(app, "replace").Enabled, "Documento de solo lectura no permite reemplazar");
            SendMessage(editor, 0xCF, IntPtr.Zero, IntPtr.Zero);
            await CaptureReplacement(app, window, editor);
            Field<TextBox>(app, "source").Text = "Texto editado";
            Assert(!Field<Button>(app, "replace").Enabled, "Editar el original invalida el destino");
            await CaptureReplacement(app, window, editor);
            int style = GetWindowLong(editor, -16); SetWindowLong(editor, -16, style | 8);
            await ReplaceDocument(app);
            Assert(EditorText(editor) == "Before HOLA MUNDO After" && Field<Label>(app, "status").Text.Contains("No se pudo confirmar"), "No confirma un pegado cuyo contenido transforma el editor");
            SetWindowLong(editor, -16, style); SendMessage(editor, 0x304, IntPtr.Zero, IntPtr.Zero);
            await CaptureReplacement(app, window, editor);
            helper.CloseMainWindow(); await Until(() => helper.HasExited, 3000, "El editor no se cerró");
            await ReplaceDocument(app);
            Assert(!Field<Button>(app, "replace").Enabled && Field<Label>(app, "status").Text.Contains("selección"), "Documento cerrado cancela el reemplazo");
            using (var bitmap = new Bitmap(app.Width, app.Height)) { app.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, "replacement.png")); }
        }
        finally
        {
            if (helper != null) { if (!helper.HasExited) helper.CloseMainWindow(); helper.Dispose(); }
            if (previous != null) Clipboard.SetDataObject(backup, true); else Clipboard.Clear();
            if (File.Exists(handshake)) File.Delete(handshake);
        }
    }
    static async Task Run(TranslatorForm app)
    {
        Assert(GoogleTranslator.Parse("[[[\"Hola \",\"Hello\"],[\"mundo\",\"world\"]],null,\"en\"]") == "Hola mundo", "Unión de segmentos de Google");
        bool malformed = false; try { GoogleTranslator.Parse("{}"); } catch (FormatException) { malformed = true; }
        Assert(malformed, "Rechazo de respuesta inesperada");
        string original = new string('a', 1199) + "😀 fin\n" + new string('b', 2300);
        string[] parts = GoogleTranslator.Split(original, 1200).ToArray();
        Assert(string.Concat(parts) == original && parts.All(p => p.Length <= 1200 && !char.IsHighSurrogate(p[p.Length - 1])), "Fragmentación sin perder caracteres ni dividir emoji");
        string settings = Path.Combine(output, "preferences.txt");
        Preferences.Save(settings, "fr"); Assert(Preferences.Load(settings) == "fr", "Idioma persistente en disco");
        Preferences.Save(settings, "es"); Assert(Preferences.Load(settings) == "es", "Actualización atómica del idioma");
        File.WriteAllText(settings, "invalid"); Assert(Preferences.Load(settings) == "es", "Preferencia inválida usa español");
        var combo = Field<ComboBox>(app, "language"); combo.SelectedIndex = 0;
        Assert(Field<IntPtr>(app, "hook") != IntPtr.Zero, "Hook global de ratón registrado");
        Assert(Field<IntPtr>(app, "keyboardHook") != IntPtr.Zero, "Hook global Ctrl + C + C registrado");
        if (!offline)
        {
            string live = await GoogleTranslator.Translate("Hello world", "es", CancellationToken.None);
            Assert(live.ToLowerInvariant().Contains("hola"), "Traducción real por el cliente de la aplicación: " + live);
        }
        else Console.WriteLine("INFO Conexión Google omitida (-Offline). Los flujos de interfaz usan traducción simulada.");
        bool cancelled = false;
        using (var token = new CancellationTokenSource()) { token.Cancel(); try { await GoogleTranslator.Translate("Hello", "es", token.Token); } catch (OperationCanceledException) { cancelled = true; } }
        Assert(cancelled, "Cancelación de traducción antes de transmitir");
        var inputBox = Field<TextBox>(app, "source"); var resultBox = Field<TextBox>(app, "translated");
        Assert(app.Text.StartsWith("Léxora"), "Nombre Léxora en la aplicación");
        Assert(inputBox.ContextMenuStrip.Items[0].Text == "Traducir con Léxora" && resultBox.ContextMenuStrip.Items[0].Text == "Traducir con Léxora", "Opción de traducción en el menú de ambos paneles");
        inputBox.Text = "Hello world\r\nGoodbye"; inputBox.Select(0, 11);
        inputBox.ContextMenuStrip.Items[0].PerformClick();
        await Until(() => Field<CancellationTokenSource>(app, "request") == null, 30000, "No terminó la traducción desde el menú");
        Assert(resultBox.Text.ToLowerInvariant().Contains("hola") && !resultBox.Text.ToLowerInvariant().Contains("adiós") && inputBox.Text == "Hello world\r\nGoodbye", "Menú traduce solo la selección y conserva el original");
        inputBox.Text = "Texto de prueba"; resultBox.Text = "Resultado"; string beforeLanguage = combo.Text;
        Field<Button>(app, "clear").PerformClick();
        Assert(inputBox.TextLength == 0 && resultBox.TextLength == 0 && combo.Text == beforeLanguage, "Borrar vacía ambos paneles y conserva idioma");
        inputBox.Text = "Hello world";
        var inFlight = (Task)typeof(TranslatorForm).GetMethod("TranslateText", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, null);
        Field<Button>(app, "clear").PerformClick(); await inFlight;
        Assert(inputBox.TextLength == 0 && resultBox.TextLength == 0 && Field<Label>(app, "status").Text == "Textos borrados.", "Borrar cancela la traducción y evita resultados tardíos");
        ChooseClose(app, DialogResult.Cancel);
        Assert(app.Visible && !app.IsDisposed, "Cancelar cierre mantiene la ventana abierta");
        ChooseClose(app, DialogResult.Yes);
        Assert(!app.Visible && !app.IsDisposed && Field<NotifyIcon>(app, "tray").Visible, "Cerrar a bandeja mantiene aplicación e icono activos");
        typeof(TranslatorForm).GetMethod("Reveal", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, null);
        Assert(app.Visible, "Reabrir desde bandeja recupera la ventana");
        using (var bitmap = new Bitmap(app.Width, app.Height)) { app.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, "app.png")); }
        if (!interactive) { ChooseClose(app, DialogResult.No); Assert(app.IsDisposed && !Field<NotifyIcon>(app, "tray").Visible, "Cerrar aplicación sale y retira el icono de bandeja"); Console.WriteLine("PENDIENTE: prueba interactiva de selección entre ventanas, icono y portapapeles (test.ps1 -Interactive)."); return; }
        IDataObject previous = Clipboard.GetDataObject(); var backup = new DataObject();
        if (previous != null) foreach (string format in previous.GetFormats(false)) { object data = previous.GetData(format, false); if (data != null) backup.SetData(format, false, data); }
        Process helper = null;
        try
        {
            string handshake = Path.Combine(output, "editor-handles.txt"); if (File.Exists(handshake)) File.Delete(handshake);
            helper = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--editor \"" + handshake + "\"") { UseShellExecute = false });
            await Until(() => File.Exists(handshake), 5000, "No arrancó el editor de pruebas");
            string[] handles = File.ReadAllText(handshake).Split(',');
            var window = new IntPtr(long.Parse(handles[0])); var textbox = new IntPtr(long.Parse(handles[1]));
            Clipboard.SetText("PORTAPAPELES ANTERIOR");
            Native.SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, 0x53);
            Foreground(window);
            app.Hide();
            Rect rectangle; Assert(GetWindowRect(textbox, out rectangle), "Coordenadas del documento de prueba");
            MouseAt(rectangle.Left + 5, rectangle.Top + 12, 2); await Task.Delay(80);
            MouseAt(rectangle.Left + 350, rectangle.Top + 12, 0); await Task.Delay(80);
            MouseAt(rectangle.Left + 350, rectangle.Top + 12, 4);
            await Task.Delay(220);
            Assert(Native.GetForegroundWindow() == window, "Editor externo en primer plano (esperado " + window + ", actual " + Native.GetForegroundWindow() + ")");
            var floating = Field<SelectionButton>(app, "selectionButton");
            await Until(() => floating.Visible, 2500, "No apareció el icono después de seleccionar con el ratón");
            Assert(floating.Visible, "Arrastre real → hook global → icono flotante visible");
            await Task.Delay(750);
            await Until(() => floating.Visible, 3000, "No apareció el icono flotante");
            Assert(Native.GetForegroundWindow() == window, "Icono visible sin robar foco al documento");
            Assert(Clipboard.GetText() == "PORTAPAPELES ANTERIOR", "Detectar selección no modifica el portapapeles");
            Assert(floating.TranslateMenu.Items[0].Text == "Traducir con Léxora", "Menú del icono identificado como Léxora");
            Rect iconRect; Assert(GetWindowRect(floating.Handle, out iconRect), "Posición nativa del icono");
            var iconCenter = new Point(iconRect.Left + (iconRect.Right - iconRect.Left) / 2, iconRect.Top + (iconRect.Bottom - iconRect.Top) / 2);
            Console.WriteLine("INFO Icono: Windows=" + iconRect.Left + "," + iconRect.Top + " Form=" + floating.Left + "," + floating.Top + " HWND=" + floating.Handle + " Hit=" + WindowFromPoint(new Native.Point { X = iconCenter.X, Y = iconCenter.Y }));
            IntPtr hit = WindowFromPoint(new Native.Point { X = iconCenter.X, Y = iconCenter.Y }); uint hitProcess;
            Native.GetWindowThreadProcessId(hit, out hitProcess); var className = new StringBuilder(256); GetClassName(hit, className, className.Capacity);
            Console.WriteLine("INFO Ventana encima: clase=" + className + " proceso=" + Process.GetProcessById((int)hitProcess).ProcessName + " IconoEnabled=" + floating.Enabled);
            Console.WriteLine("INFO Icono nativo visible=" + IsWindowVisible(floating.Handle) + " enabled=" + IsWindowEnabled(floating.Handle) + " exstyle=" + GetWindowLong(floating.Handle, -20).ToString("X") + " documentoEx=" + GetWindowLong(window, -20).ToString("X"));
            Assert(WindowFromPoint(new Native.Point { X = iconCenter.X, Y = iconCenter.Y }) == floating.Handle, "Icono por encima del documento y accesible al ratón");
            MouseAt(iconCenter.X, iconCenter.Y, 2); await Task.Delay(60);
            MouseAt(iconCenter.X, iconCenter.Y, 4);
            var translated = Field<TextBox>(app, "translated");
            await Until(() => translated.TextLength > 0 || Field<Label>(app, "status").Text.Contains("Google ha"), 35000, "No terminó la traducción por gesto: " + Field<Label>(app, "status").Text);
            Assert(translated.Text.ToLowerInvariant().Contains("hola"), "Icono → traducción simulada al español (" + Field<Label>(app, "status").Text + ")");
            Assert(Field<TextBox>(app, "source").Text.Trim() == "Hello world", "Texto original completo conservado (" + Field<TextBox>(app, "source").Text.Replace("\r", "\\r").Replace("\n", "\\n") + ")");
            await Until(() => Clipboard.GetText() == "PORTAPAPELES ANTERIOR", 1500, "Portapapeles no conservado en flujo completo");
            Assert(Clipboard.GetText() == "PORTAPAPELES ANTERIOR", "Portapapeles conservado en flujo completo");
            using (var bitmap = new Bitmap(app.Width, app.Height)) { app.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, "app.png")); }

            // Comprueba también el menú real del botón derecho del icono.
            Foreground(window); await Task.Delay(200);
            typeof(TranslatorForm).GetMethod("QueueSelection", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, new object[] { new Point(rectangle.Left + 350, rectangle.Top + 12), true });
            await Until(() => floating.Visible, 2500, "No reapareció el icono para probar su menú");
            await Task.Delay(750);
            GetWindowRect(floating.Handle, out iconRect);
            iconCenter = new Point(iconRect.Left + (iconRect.Right - iconRect.Left) / 2, iconRect.Top + (iconRect.Bottom - iconRect.Top) / 2);
            MouseAt(iconCenter.X, iconCenter.Y, 8); await Task.Delay(60); MouseAt(iconCenter.X, iconCenter.Y, 16);
            await Until(() => floating.TranslateMenu.Visible, 1500, "No se abrió el menú del icono");
            Assert(floating.TranslateMenu.Visible, "Botón derecho abre el menú Traducir con Léxora");
            var menuItem = floating.TranslateMenu.Items[0].Bounds;
            var menuPoint = new Point(floating.TranslateMenu.Left + menuItem.Left + 20, floating.TranslateMenu.Top + menuItem.Top + menuItem.Height / 2);
            translated.Clear();
            MouseAt(menuPoint.X, menuPoint.Y, 2); await Task.Delay(60); MouseAt(menuPoint.X, menuPoint.Y, 4);
            await Until(() => translated.TextLength > 0, 30000, "No terminó la traducción desde el menú del icono");
            Assert(translated.Text.ToLowerInvariant().Contains("hola"), "Menú del icono → traducción simulada");

            // Simula las notificaciones del hook físico; Ctrl+C se entrega realmente al editor.
            SendMessage(textbox, 0xB1, IntPtr.Zero, new IntPtr(-1)); Foreground(window); await Task.Delay(200);
            translated.Clear();
            var callback = typeof(TranslatorForm).GetMethod("OnKeyboard", BindingFlags.NonPublic | BindingFlags.Instance);
            IntPtr data = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.KeyboardData)));
            try
            {
                Marshal.StructureToPtr(new Native.KeyboardData { Key = 0x43 }, data, false);
                Key(0x11, false);
                for (int i = 0; i < 2; i++)
                {
                    callback.Invoke(app, new object[] { 0, new IntPtr(0x100), data });
                    Key(0x43, false); Key(0x43, true);
                    callback.Invoke(app, new object[] { 0, new IntPtr(0x101), data });
                    await Task.Delay(80);
                }
            }
            finally { Key(0x11, true); Marshal.FreeHGlobal(data); }
            await Until(() => translated.TextLength > 0, 35000, "No terminó la traducción por Ctrl+C+C: " + Field<Label>(app, "status").Text);
            Assert(translated.Text.ToLowerInvariant().Contains("hola"), "Ctrl+C+C simulado → copia real → traducción simulada");
            Clipboard.SetText("PORTAPAPELES ANTERIOR");
            Foreground(window); await Task.Delay(200);
            string selected = await TranslatorForm.CaptureSelection(window);
            Assert(selected == "Hello world", "Captura real de selección de otra aplicación con Ctrl+C");
            await Until(() => Clipboard.GetText() == "PORTAPAPELES ANTERIOR", 1500, "El portapapeles no se restauró tras capturar");
            Assert(Clipboard.GetText() == "PORTAPAPELES ANTERIOR", "Restauración del portapapeles tras capturar");
            SendMessage(textbox, 0xB1, IntPtr.Zero, IntPtr.Zero); Foreground(window); await Task.Delay(200);
            Assert(await TranslatorForm.CaptureSelection(window) == null, "Sin selección no traduce contenido previo del portapapeles");
            Assert(Clipboard.GetText() == "PORTAPAPELES ANTERIOR", "Portapapeles conservado cuando no hay selección");
            app.Show(); ChooseClose(app, DialogResult.No);
            Assert(app.IsDisposed, "Salir completamente tras usar el icono y el atajo");
        }
        finally
        {
            if (helper != null) { helper.CloseMainWindow(); helper.Dispose(); }
            if (previous == null) Clipboard.Clear(); else Clipboard.SetDataObject(backup, true);
        }
    }
}

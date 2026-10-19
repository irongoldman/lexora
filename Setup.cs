using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using SeleccionTraductor;

internal static class Setup
{
    const string AppName = "Léxora";
    const string AppFile = "SeleccionTraductor.exe";
    const string UninstallFile = "Desinstalar.exe";
    const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SeleccionTraductor";
    static readonly string InstallDir = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeleccionTraductor"));
    static string AppPath { get { return Path.Combine(InstallDir, AppFile); } }
    static string Uninstaller { get { return Path.Combine(InstallDir, UninstallFile); } }
    static string IconPath { get { return Path.Combine(InstallDir, "Lexora.ico"); } }
    internal static readonly string[] LegalFiles = { "LICENSE", "TERMS.txt", "SourceCode.zip" };
    internal static string LegalText(string resource)
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
        {
            if (stream == null) throw new IOException("Falta el documento legal del instalador: " + resource);
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8)) return reader.ReadToEnd();
        }
    }
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            if (args.Length == 0 && Path.GetFileNameWithoutExtension(Application.ExecutablePath).StartsWith("Desinstalar", StringComparison.OrdinalIgnoreCase))
                args = new[] { "/uninstall" };
            if (args.Length == 2 && args[0] == "/uninstall-worker")
            {
                int parent;
                if (!Int32.TryParse(args[1], out parent)) throw new InvalidOperationException("Argumentos incorrectos.");
                string own = Path.GetFullPath(Application.ExecutablePath);
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!own.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(own).StartsWith("SeleccionTraductor-uninstall-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Ruta del desinstalador incorrecta.");
                try { Process.GetProcessById(parent).WaitForExit(10000); } catch (ArgumentException) { }
                Uninstall();
                return;
            }
            if (args.Length == 1 && args[0] == "/uninstall")
            {
                RequestUninstall();
                return;
            }
            if (args.Length != 0) throw new InvalidOperationException("Argumentos incorrectos.");
            Application.Run(new InstallerForm());
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); Environment.ExitCode = 1; }
    }

    internal static void RequestUninstall()
    {
        if (MessageBox.Show("¿Desinstalar " + AppName + "? Se conservará tu configuración.", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        if (String.Equals(Path.GetFullPath(Application.ExecutablePath), Uninstaller, StringComparison.OrdinalIgnoreCase))
        {
            string worker = Path.Combine(Path.GetTempPath(), "SeleccionTraductor-uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Application.ExecutablePath, worker, false);
            Process.Start(new ProcessStartInfo(worker, "/uninstall-worker " + Process.GetCurrentProcess().Id) { UseShellExecute = true });
        }
        else Uninstall();
    }

    static void CheckPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (!String.Equals(full, InstallDir, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(InstallDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("La ruta no pertenece a la instalación.");
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("No se admite una instalación mediante enlaces de archivos o carpetas.");
    }

    static void CheckAppClosed()
    {
        CheckPath(InstallDir);
        CheckPath(AppPath);
        if (File.Exists(AppPath))
            try { using (new FileStream(AppPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } }
            catch (IOException) { throw new IOException("Léxora o uno de sus archivos sigue en uso. Cierra la aplicación y vuelve a intentarlo. No se puede continuar mientras Windows mantenga el archivo bloqueado."); }
    }

    internal static bool StopInstalledApplication(string executable, Func<int, bool> confirm)
    {
        string expected = Path.GetFullPath(executable);
        var matches = new List<Process>();
        try
        {
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(expected)))
            {
                bool matched = false;
                try
                {
                    // Conserva el identificador nativo mientras se comprueba la ruta y se finaliza.
                    IntPtr handle = process.Handle;
                    matched = process.Id != Process.GetCurrentProcess().Id && !process.HasExited
                        && process.SessionId == Process.GetCurrentProcess().SessionId
                        && String.Equals(Path.GetFullPath(process.MainModule.FileName), expected, StringComparison.OrdinalIgnoreCase);
                }
                catch (System.ComponentModel.Win32Exception) { }
                catch (InvalidOperationException) { }
                if (matched) matches.Add(process); else process.Dispose();
            }
            if (matches.Count == 0) return true;
            if (!confirm(matches.Count)) return false;
            foreach (var process in matches)
            {
                try
                {
                    if (process.HasExited) continue;
                    process.Kill();
                    if (!process.WaitForExit(5000)) throw new IOException("No se ha podido cerrar Léxora. No se ha iniciado la eliminación de archivos.");
                }
                catch (InvalidOperationException) { if (!process.HasExited) throw; }
                catch (System.ComponentModel.Win32Exception) { throw new IOException("Windows no ha permitido cerrar Léxora. Ciérrala desde su menú de bandeja y vuelve a intentar la desinstalación."); }
            }
            return true;
        }
        finally { foreach (var process in matches) process.Dispose(); }
    }

    internal static bool StartsWithWindows { get { return StartupSettings.IsEnabled(AppPath); } }

    internal static void Install(bool startWithWindows, bool accepted)
    {
        if (!accepted) throw new InvalidOperationException("Debes aceptar la licencia, los términos y el descargo antes de instalar.");
        CheckAppClosed();
        CheckPath(Uninstaller);
        CheckPath(IconPath);
        foreach (string file in LegalFiles) CheckPath(Path.Combine(InstallDir, file));
        Directory.CreateDirectory(InstallDir);
        string staged = Path.Combine(InstallDir, "SeleccionTraductor.installing");
        CheckPath(staged);
        try
        {
            using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(AppFile))
            {
                if (source == null) throw new IOException("Falta la aplicación dentro del instalador.");
                using (FileStream target = new FileStream(staged, FileMode.Create, FileAccess.Write, FileShare.None)) source.CopyTo(target);
            }
            File.Copy(staged, AppPath, true);
            foreach (string file in LegalFiles)
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(file))
                {
                    if (resource == null) throw new IOException("Falta un archivo incluido en el instalador: " + file);
                    using (var target = new FileStream(Path.Combine(InstallDir, file), FileMode.Create, FileAccess.Write, FileShare.None)) resource.CopyTo(target);
                }
            using (Stream icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("Lexora.ico"))
            {
                if (icon == null) throw new IOException("Falta el icono dentro del instalador.");
                using (var target = new FileStream(IconPath, FileMode.Create, FileAccess.Write, FileShare.None)) icon.CopyTo(target);
            }
            if (!String.Equals(Path.GetFullPath(Application.ExecutablePath), Uninstaller, StringComparison.OrdinalIgnoreCase)) File.Copy(Application.ExecutablePath, Uninstaller, true);
            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"), false);
            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"), false);
            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Selección Traductor.lnk"), true);
            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Selección Traductor.lnk"), true);
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", "1.4.0");
                key.SetValue("InstallLocation", InstallDir);
                key.SetValue("DisplayIcon", IconPath);
                key.SetValue("UninstallString", "\"" + Uninstaller + "\" /uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
            StartupSettings.SetEnabled(AppPath, startWithWindows);
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally { if (File.Exists(staged)) File.Delete(staged); }
    }

    static void Shortcut(string path, bool remove)
    {
        if (remove && !File.Exists(path)) return;
        object shell = null, link = null;
        try
        {
            Type type = Type.GetTypeFromProgID("WScript.Shell", true);
            shell = Activator.CreateInstance(type);
            link = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            Type lt = link.GetType();
            if (remove)
            {
                string target = Convert.ToString(lt.InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null));
                if (!String.IsNullOrEmpty(target) && String.Equals(Path.GetFullPath(target), AppPath, StringComparison.OrdinalIgnoreCase)) File.Delete(path);
            }
            else
            {
                if (File.Exists(path))
                {
                    string existing = Convert.ToString(lt.InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null));
                    if (String.IsNullOrEmpty(existing) || !String.Equals(Path.GetFullPath(existing), AppPath, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Ya existe un acceso directo con este nombre que pertenece a otra aplicación: " + Path.GetFileName(path));
                }
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { AppPath });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { InstallDir });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { AppName });
                lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { IconPath + ",0" });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            }
        }
        finally
        {
            if (link != null) Marshal.FinalReleaseComObject(link);
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }

    static void Uninstall()
    {
        CheckPath(InstallDir);
        CheckPath(AppPath);
        CheckPath(Uninstaller);
        CheckPath(IconPath);
        foreach (string file in LegalFiles) CheckPath(Path.Combine(InstallDir, file));
        if (!StopInstalledApplication(AppPath, count => MessageBox.Show(
            "Léxora sigue abierta, posiblemente en la bandeja del sistema.\r\n\r\n¿Finalizar sus procesos y continuar con la desinstalación?\r\nSe perderán los textos que no hayas copiado o guardado. Tus documentos externos no se cerrarán.\r\n\r\nSi eliges No, se cancelará la desinstalación.",
            AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)) return;
        CheckAppClosed();
        StartupSettings.SetEnabled(AppPath, false);
        Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"), true);
        Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"), true);
        Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Selección Traductor.lnk"), true);
        Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Selección Traductor.lnk"), true);
        if (File.Exists(AppPath)) File.Delete(AppPath);
        if (File.Exists(Uninstaller)) File.Delete(Uninstaller);
        if (File.Exists(IconPath)) File.Delete(IconPath);
        foreach (string file in LegalFiles) { string path = Path.Combine(InstallDir, file); if (File.Exists(path)) File.Delete(path); }
        bool owned = false;
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
            if (key != null) owned = String.Equals(Convert.ToString(key.GetValue("InstallLocation")), InstallDir, StringComparison.OrdinalIgnoreCase);
        if (owned) Registry.CurrentUser.DeleteSubKey(RegistryPath, false);
        if (Directory.Exists(InstallDir) && Directory.GetFileSystemEntries(InstallDir).Length == 0) Directory.Delete(InstallDir, false);
        MessageBox.Show("Aplicación desinstalada. Se ha conservado la configuración.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

internal sealed class InstallerForm : Form
{
    public InstallerForm()
    {
        Text = "Instalar Léxora";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        ClientSize = new Size(720, 660);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        Controls.Add(new Label { Text = "Léxora", Font = new Font("Segoe UI", 19, FontStyle.Bold), AutoSize = true, Location = new Point(24, 22) });
        Controls.Add(new Label { Text = "Incluye la aplicación, el icono y el desinstalador.\r\nSe instalará para tu usuario, sin permisos de administrador.\r\nAccesos directos en el escritorio y el menú Inicio.\r\nCierra Léxora antes de actualizar.", AutoSize = false, Size = new Size(480, 100), Location = new Point(26, 76) });
        var legal = new TabControl { Name = "LegalTabs", Location = new Point(26, 180), Size = new Size(668, 310) };
        foreach (string file in new[] { "LICENSE", "TERMS.txt" })
        {
            var page = new TabPage(file == "LICENSE" ? "Licencia MIT" : "Términos y descargo");
            page.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Text = Setup.LegalText(file).Replace("\r\n", "\n").Replace("\n", "\r\n"), BackColor = Color.White });
            legal.TabPages.Add(page);
        }
        Controls.Add(legal);
        var acceptance = new CheckBox { Name = "AcceptLegal", Text = "He leído y acepto la licencia MIT, los términos de uso\r\ny el descargo de responsabilidad.", AutoSize = true, Location = new Point(26, 502), Checked = false };
        Controls.Add(acceptance);
        var startup = new CheckBox { Text = "Iniciar Léxora con Windows (en la bandeja)", AutoSize = true, Location = new Point(26, 555), Checked = Setup.StartsWithWindows };
        Controls.Add(startup);
        Button uninstall = new Button { Text = "Desinstalar", Location = new Point(26, 605), Size = new Size(120, 36) };
        uninstall.Click += delegate {
            try { Setup.RequestUninstall(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        Controls.Add(uninstall);
        Button reject = new Button { Name = "RejectLegal", Text = "No acepto y salir", Location = new Point(314, 605), Size = new Size(170, 36) };
        reject.Click += delegate { Close(); }; Controls.Add(reject); CancelButton = reject;
        Button install = new Button { Name = "Install", Text = "Instalar", Location = new Point(574, 605), Size = new Size(120, 36), Enabled = false };
        acceptance.CheckedChanged += delegate { install.Enabled = acceptance.Checked; };
        install.Click += delegate {
            install.Enabled = false;
            try { Setup.Install(startup.Checked, acceptance.Checked); MessageBox.Show("Instalación completada. Abre Léxora desde el escritorio o el menú Inicio." + (startup.Checked ? "\r\nEn el próximo inicio de sesión arrancará en la bandeja." : ""), Text, MessageBoxButtons.OK, MessageBoxIcon.Information); Close(); }
            catch (IOException ex) { MessageBox.Show("No se ha podido completar la instalación. Cierra el traductor si está abierto y vuelve a intentarlo.\r\n\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); install.Enabled = true; }
            catch (Exception ex) { MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); install.Enabled = true; }
        };
        Controls.Add(install);
        AcceptButton = install;
    }
}

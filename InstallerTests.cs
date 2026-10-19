using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;

internal static class InstallerTests
{
    static int passed;
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        passed++; Console.WriteLine("OK " + label);
    }
    [STAThread] static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--hold") { Thread.Sleep(30000); return 0; }
        Process target = null, other = null;
        string root = Path.Combine(Path.GetDirectoryName(typeof(InstallerTests).Assembly.Location), "process-" + Guid.NewGuid().ToString("N"));
        string targetPath = Path.Combine(root, "installed", "Fixture.exe"), otherPath = Path.Combine(root, "other", "Fixture.exe");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)); Directory.CreateDirectory(Path.GetDirectoryName(otherPath));
            File.Copy(typeof(InstallerTests).Assembly.Location, targetPath); File.Copy(typeof(InstallerTests).Assembly.Location, otherPath);
            target = Process.Start(new ProcessStartInfo(targetPath, "--hold") { UseShellExecute = false, CreateNoWindow = true });
            other = Process.Start(new ProcessStartInfo(otherPath, "--hold") { UseShellExecute = false, CreateNoWindow = true });
            Thread.Sleep(250);
            Check(!target.HasExited && !other.HasExited, "Dos procesos temporales con el mismo nombre y rutas diferentes");
            int prompts = 0;
            Check(!Setup.StopInstalledApplication(targetPath, count => { prompts++; Check(count == 1, "Solo identifica la ruta de instalación indicada"); return false; }), "Rechazar el cierre cancela la operación");
            Check(prompts == 1 && !target.HasExited && !other.HasExited, "Cancelar conserva ambos procesos activos");
            Check(Setup.StopInstalledApplication(targetPath, count => { prompts++; return true; }), "Confirmar permite finalizar el proceso de la instalación");
            Check(target.HasExited && !other.HasExited, "El proceso de otra ruta con el mismo nombre sigue activo");
            Check(File.Exists(targetPath) && File.Exists(otherPath), "El cierre de procesos no elimina archivos");
            Check(Setup.StopInstalledApplication(targetPath, count => { throw new Exception("No debe preguntar sin procesos"); }), "Sin procesos activos continúa sin preguntar");
            Check(prompts == 2, "Solo pregunta cuando hay un proceso de la instalación activo");
            Application.EnableVisualStyles();
            using (var form = new InstallerForm())
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.Show(); Application.DoEvents();
                bool uninstall = false;
                foreach (Control control in form.Controls)
                    if (control is Button && control.Text == "Desinstalar") uninstall = form.ClientRectangle.Contains(control.Bounds);
                Check(uninstall, "Botón Desinstalar disponible dentro de la ventana del instalador");
                var acceptance = (CheckBox)form.Controls["AcceptLegal"];
                var install = (Button)form.Controls["Install"];
                Check(!acceptance.Checked && !install.Enabled, "Aceptación desmarcada e instalación bloqueada por defecto");
                var tabs = (TabControl)form.Controls["LegalTabs"];
                Check(tabs.TabPages.Count == 2 && ((TextBox)tabs.TabPages[0].Controls[0]).ReadOnly && ((TextBox)tabs.TabPages[1].Controls[0]).Text.Contains("DESCARGO"), "Licencia y términos íntegros consultables dentro del instalador");
                acceptance.Checked = true;
                Check(install.Enabled, "Aceptar habilita Instalar");
                acceptance.Checked = false;
                Check(!install.Enabled, "Retirar aceptación vuelve a bloquear Instalar");
                bool blocked = false;
                try { Setup.Install(false, false); } catch (InvalidOperationException) { blocked = true; }
                Check(blocked, "La rutina de instalación rechaza la falta de aceptación antes de modificar el sistema");
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(Path.GetDirectoryName(typeof(InstallerTests).Assembly.Location), "installer.png"));
                ((Button)form.Controls["RejectLegal"]).PerformClick();
                Check(form.IsDisposed, "No acepto y salir cierra el instalador");
            }
            Console.WriteLine("PASS: " + passed + " comprobaciones del cierre para desinstalación"); return 0;
        }
        catch (Exception ex) { Console.WriteLine("FAIL: " + ex); return 1; }
        finally
        {
            foreach (var process in new[] { target, other })
                if (process != null) { if (!process.HasExited) { process.Kill(); process.WaitForExit(5000); } process.Dispose(); }
            // Elimina únicamente los dos ejecutables temporales conocidos y carpetas vacías.
            foreach (string file in new[] { targetPath, otherPath })
            {
                if (File.Exists(file)) File.Delete(file);
                if (Directory.Exists(Path.GetDirectoryName(file))) Directory.Delete(Path.GetDirectoryName(file), false);
            }
            if (Directory.Exists(root)) Directory.Delete(root, false);
        }
    }
}

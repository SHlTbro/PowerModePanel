using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

// Loads the real MainForm and its normal read-only Shown/metrics path.
// Never clicks a button, writes a fan/power setting, trims memory, or closes another process.
class ShowcaseSmoke
{
    static string Value(PowerModePanel.MainForm form, string name)
    {
        FieldInfo field = typeof(PowerModePanel.MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Control control = field == null ? null : field.GetValue(form) as Control;
        if (control == null) throw new InvalidOperationException("Missing actual UI control: " + name);
        return control.Text;
    }
    static string Quote(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            string directory = args.Length == 1 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "artifacts");
            Directory.CreateDirectory(directory);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (PowerModePanel.MainForm form = new PowerModePanel.MainForm())
            {
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-30000, -30000);
                form.Show();
                DateTime stop = DateTime.UtcNow.AddSeconds(12);
                while (DateTime.UtcNow < stop) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
                if (form.Controls["fanQuiet"] == null || form.Controls["fanAutomatic"] == null)
                    throw new InvalidOperationException("The production fan controls are missing.");
                foreach (Control control in form.Controls)
                    if (control.Visible && (control.Right > form.ClientSize.Width || control.Bottom > form.ClientSize.Height))
                        throw new InvalidOperationException("Clipped production control: " + control.Text);
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(Path.Combine(directory, "smoke.png"), ImageFormat.Png);
                }
                List<string> fields = new List<string>();
                fields.Add("\"captured_at\":" + Quote(DateTimeOffset.Now.ToString("o")));
                fields.Add("\"action\":\"read-only MainForm startup; no buttons clicked\"");
                foreach (string name in new string[] { "fanModeValue", "currentModeValue", "cpuTempValue", "gpuTempValue", "cpuClockValue", "gpuUtilValue" })
                    fields.Add(Quote(name) + ":" + Quote(Value(form, name)));
                File.WriteAllText(Path.Combine(directory, "smoke.json"), "{\n  " + string.Join(",\n  ", fields.ToArray()) + "\n}\n", new UTF8Encoding(false));
                form.Close();
            }
            Console.WriteLine("PASS real MainForm startup and image capture; read-only; artifacts\\smoke.png + smoke.json");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL " + error.Message); return 1; }
    }
}

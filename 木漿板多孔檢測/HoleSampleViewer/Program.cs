using System;
using System.IO;
using System.Windows.Forms;

namespace HoleSampleViewer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            MainForm form = new MainForm();
            if (args.Length > 0 && File.Exists(args[0]))
                form.Shown += (s, e) => form.OpenFile(args[0]);
            Application.Run(form);
        }
    }
}

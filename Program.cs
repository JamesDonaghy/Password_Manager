using System;
using System.Windows.Forms;

namespace PasswordManager
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Load theme preference before any form paints (avoids a wrong-mode flash later).
            AppTheme.Initialize();

            Application.Run(new LoginForm());
        }
    }
}
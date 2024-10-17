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

            // Show the login form
            using (var loginForm = new LoginForm())
            {
                if (loginForm.ShowDialog() == DialogResult.OK)
                {
                    // If login is successful, run the main form
                    Application.Run(new MainForm());
                }
            }
        }
    }
}
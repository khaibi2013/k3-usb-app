using System;
using System.Threading;
using System.Windows.Forms;

namespace AnToanUSB
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            LoginForm login;
            using (var splash = new SplashForm())
            {
                splash.Show();
                splash.Refresh();
                login = new LoginForm();
                Thread.Sleep(900);
                splash.Close();
            }

            if (login.ShowDialog() == DialogResult.OK)
            {
                Application.Run(new MainForm());
            }
            login.Dispose();
        }
    }
}

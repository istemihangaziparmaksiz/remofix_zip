using System.Windows;
using System.Windows.Input;

namespace RemofixZip.Dialogs
{
    public partial class PasswordPromptDialog : Window
    {
        public string EnteredPassword { get; private set; } = string.Empty;

        public PasswordPromptDialog()
        {
            InitializeComponent();
            Loaded += (s, e) => txtPassword.Focus();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            EnteredPassword = txtPassword.Password;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnApply_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                BtnCancel_Click(sender, e);
            }
        }
    }
}

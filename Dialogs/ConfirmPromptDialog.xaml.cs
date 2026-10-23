using System.Windows;

namespace RemofixZip.Dialogs
{
    public partial class ConfirmPromptDialog : Window
    {
        public ConfirmPromptDialog(string fileName, string url)
        {
            InitializeComponent();
            lblMessage.Text = $"\"{fileName}\" arşiv dosyası tespit edildi.\n\nZIP Gezgini sekmesine aktarılıp dosya yapısı incelensin mi?";
            lblUrl.Text = url;
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

using System.Windows;

namespace DDBManager
{
    public partial class RestoreProgressWindow : Window
    {
        public bool IsCancelled { get; private set; }

        public RestoreProgressWindow()
        {
            InitializeComponent();
        }

        public void UpdateProgress(int current, int total, string fileName)
        {
            Dispatcher.Invoke(() =>
            {
                TxtCurrentFile.Text = $"Restoring: {fileName}";
                PrgTotal.Maximum = total;
                PrgTotal.Value = current;
                TxtStats.Text = $"{current} of {total} files restored";
            });
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsCancelled = true;
            this.Close();
        }
    }
}
using System.Reflection;
using System.Windows;

namespace DDBManager
{
    public partial class AboutWindow : Window
    {
        public string DisplayVersion { get; }

        public AboutWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            // Get the "Informational Version" (the <Version> tag in .csproj)
            var version = Assembly.GetExecutingAssembly()
                                  .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                                  .InformationalVersion;

            // If it includes git hash info (standard in .NET 8), we split it to just get the numbers
            if (version != null && version.Contains("+"))
            {
                version = version.Split('+')[0];
            }

            DisplayVersion = $"Version {version ?? "0.0.0"}";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
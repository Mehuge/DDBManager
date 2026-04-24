using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DDBManager.Models
{
    public class BackupProfile : INotifyPropertyChanged
    {
        private string _sourcePath = string.Empty;
        private string _setName = "NEW_SET";

        // Explicit constructor for JSON deserialization
        public BackupProfile()
        {
            Excludes = new ObservableCollection<string>();
            Includes = new ObservableCollection<string>();
        }

        public string SetName
        {
            get => _setName;
            set { _setName = value; OnPropertyChanged(); }
        }

        public string SourcePath
        {
            get => _sourcePath;
            set { _sourcePath = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> Excludes { get; set; }
        public ObservableCollection<string> Includes { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
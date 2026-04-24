using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DDBManager.Models
{
    public class BackupSetNode
    {
        public string Name { get; set; } = string.Empty;
        public ObservableCollection<BackupInstanceNode> Instances { get; } = new();
    }

    public class BackupInstanceNode
    {
        public string Timestamp { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string SetName { get; set; } = string.Empty;
    }

    public class FileSystemNode : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public string FullPath { get; set; } = string.Empty;
        public bool HasLoadedChildren { get; set; }
        public BackupEntry? Entry { get; set; }
        public ObservableCollection<FileSystemNode> Children { get; } = new();

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public string Icon => IsFolder ? "📁" : "📄";

        public string SizeDisplay
        {
            get
            {
                if (IsFolder || Entry == null) return "";
                if (Entry.Size < 1024) return $"{Entry.Size} B";
                if (Entry.Size < 1024 * 1024) return $"{Entry.Size / 1024.0:F1} KB";
                return $"{Entry.Size / (1024.0 * 1024.0):F1} MB";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
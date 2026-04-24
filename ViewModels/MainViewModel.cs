using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DDBManager.Models;

namespace DDBManager.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _storePath = string.Empty;
        private string _status = "Ready";
        private string _consoleOutput = string.Empty;
        private string _fileFilter = string.Empty;
        private BackupProfile? _selectedProfile;
        private List<BackupEntry> _rawManifest = new();
        private Process? _currentProcess;
        private bool _isProcessRunning;

        public ObservableCollection<BackupSetNode> BackupTree { get; } = new();
        public ObservableCollection<FileSystemNode> FileTree { get; } = new();
        public ObservableCollection<BackupProfile> Profiles { get; } = new();
        public ObservableCollection<string> GlobalExcludes { get; } = new();
        public ObservableCollection<string> GlobalIncludes { get; } = new();

        public string StorePath
        {
            get => _storePath;
            set { _storePath = value; OnPropertyChanged(); SaveConfig(); _ = LoadSets(); }
        }

        public string FileFilter
        {
            get => _fileFilter;
            set { _fileFilter = value; OnPropertyChanged(); RefreshFileTree(); }
        }

        public BackupProfile? SelectedProfile
        {
            get => _selectedProfile;
            set { _selectedProfile = value; OnPropertyChanged(); SaveConfig(); }
        }

        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
        public string ConsoleOutput { get => _consoleOutput; set { _consoleOutput = value; OnPropertyChanged(); } }
        public bool IsProcessRunning { get => _isProcessRunning; set { _isProcessRunning = value; OnPropertyChanged(); } }

        public MainViewModel()
        {
            LoadConfig();
            if (Profiles.Count == 0) Profiles.Add(new BackupProfile { SetName = "DEFAULT" });
            if (SelectedProfile == null) SelectedProfile = Profiles[0];
        }

        private void LoadConfig()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DDBManager");
                string path = Path.Combine(folder, "config.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Profiles", out var pElem))
                    {
                        var profiles = JsonSerializer.Deserialize<List<BackupProfile>>(pElem.GetRawText());
                        if (profiles != null) foreach (var p in profiles) Profiles.Add(p);
                    }
                    if (doc.RootElement.TryGetProperty("GlobalExcludes", out var geElem))
                    {
                        var ges = JsonSerializer.Deserialize<List<string>>(geElem.GetRawText());
                        if (ges != null) foreach (var s in ges) GlobalExcludes.Add(s);
                    }
                    if (doc.RootElement.TryGetProperty("GlobalIncludes", out var giElem))
                    {
                        var gis = JsonSerializer.Deserialize<List<string>>(giElem.GetRawText());
                        if (gis != null) foreach (var s in gis) GlobalIncludes.Add(s);
                    }
                }
                _storePath = Properties.Settings.Default.LastStorePath;

                if (!string.IsNullOrEmpty(_storePath))
                {
                    _ = LoadSets();
                }
            }
            catch { }
        }

        public void SaveConfig()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DDBManager");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "config.json");
                var configData = new { Profiles = Profiles, GlobalExcludes = GlobalExcludes, GlobalIncludes = GlobalIncludes };
                File.WriteAllText(path, JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true }));
                Properties.Settings.Default.LastStorePath = StorePath;
                Properties.Settings.Default.Save();
            }
            catch { }
        }

        public async Task LoadSets()
        {
            var groups = await Task.Run(() => {
                if (string.IsNullOrEmpty(StorePath)) return null;
                string backupsDir = Path.Combine(StorePath, "backups");
                if (!Directory.Exists(backupsDir)) return null;

                return Directory.GetFiles(backupsDir, "*.*")
                                .Select(Path.GetFileName)
                                .Where(name => name != null && name.Contains("."))
                                .GroupBy(name => name!.Substring(0, name!.LastIndexOf('.')))
                                .ToList();
            });

            App.Current.Dispatcher.Invoke(() => {
                BackupTree.Clear();
                if (groups == null) return;

                foreach (var g in groups)
                {
                    var setNode = new BackupSetNode { Name = g.Key };
                    foreach (var fileName in g.OrderByDescending(x => x))
                    {
                        setNode.Instances.Add(new BackupInstanceNode
                        {
                            Timestamp = fileName!.Substring(g.Key.Length + 1),
                            FullPath = Path.Combine(StorePath, "backups", fileName!),
                            SetName = g.Key
                        });
                    }
                    BackupTree.Add(setNode);
                }
            });
        }

        // Helper for readable sizes
        private string FormatByteSize(long bytes)
        {
            string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblSByte = bytes / 1024.0;
            }
            return $"{dblSByte:0.##} {Suffix[i]}";
        }

        public void DeleteBackupInstance(BackupInstanceNode instance)
        {
            try
            {
                if (File.Exists(instance.FullPath)) { File.Delete(instance.FullPath); _ = LoadSets(); Status = $"Deleted instance: {instance.Timestamp}"; }
            }
            catch (Exception ex) { Status = $"Error deleting: {ex.Message}"; }
        }

        public async Task BuildFileTree(BackupInstanceNode instance)
        {
            FileTree.Clear(); _rawManifest.Clear();
            Status = "Loading Manifest...";
            await Task.Run(() => {
                if (!File.Exists(instance.FullPath)) return;
                using var reader = new StreamReader(instance.FullPath);
                while (reader.ReadLine() is { } line)
                {
                    if (line.StartsWith("F ") || line.StartsWith("D "))
                    {
                        var entry = BackupEntry.FromManifestLine(line);
                        if (entry != null) _rawManifest.Add(entry);
                    }
                }
            });
            RefreshFileTree();
        }

        private void RefreshFileTree()
        {
            FileTree.Clear();
            LoadSubLevel(FileTree, string.Empty);
            Status = $"Showing {FileTree.Count} top-level items.";
        }

        public void LoadSubLevel(ObservableCollection<FileSystemNode> collection, string parentPath)
        {
            IEnumerable<BackupEntry> items;
            if (!string.IsNullOrWhiteSpace(FileFilter))
            {
                items = _rawManifest.Where(e => e.Path.Contains(FileFilter, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                if (string.IsNullOrEmpty(parentPath)) items = _rawManifest.Where(e => !e.Path.Contains("\\"));
                else
                {
                    string prefix = parentPath + "\\";
                    items = _rawManifest.Where(e => e.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !e.Path.Substring(prefix.Length).Contains("\\"));
                }
            }

            foreach (var entry in items.OrderByDescending(e => e.Type).ThenBy(e => e.Path))
            {
                var newNode = new FileSystemNode { Name = Path.GetFileName(entry.Path), IsFolder = entry.Type == 'D', FullPath = entry.Path, Entry = entry };
                if (newNode.IsFolder && string.IsNullOrWhiteSpace(FileFilter)) newNode.Children.Add(new FileSystemNode { Name = "Loading..." });
                collection.Add(newNode);
            }
        }

        public List<BackupEntry> GetFilesToRestore()
        {
            var results = new HashSet<BackupEntry>();
            void Walk(IEnumerable<FileSystemNode> nodes)
            {
                foreach (var node in nodes)
                {
                    if (node.IsSelected)
                    {
                        if (!node.IsFolder && node.Entry != null) results.Add(node.Entry);
                        else if (node.IsFolder)
                        {
                            var prefix = node.FullPath + "\\";
                            var matches = _rawManifest.Where(e => e.Type == 'F' && e.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                            foreach (var m in matches) results.Add(m);
                        }
                    }
                    else if (node.IsFolder) Walk(node.Children);
                }
            }
            Walk(FileTree);
            return results.ToList();
        }

        public async Task RunDdbCommand(string args)
        {
            var sw = Stopwatch.StartNew();

            IsProcessRunning = true;
            ConsoleOutput += $"\n--- {DateTime.Now:HH:mm:ss} | Running: ddb {args} ---\n";
            ProcessStartInfo psi = new() { FileName = "ddb.exe", Arguments = args, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            _currentProcess = new Process { StartInfo = psi };
            _currentProcess.OutputDataReceived += (s, e) => {
                if (e.Data != null) {
                    Debug.Print($"[{sw.ElapsedMilliseconds}ms] Data: " + e.Data);
                     App.Current.Dispatcher.Invoke(() => ConsoleOutput += e.Data + "\n"); 
                } else {
                    Debug.Print($"[{sw.ElapsedMilliseconds}ms] STDOUT CLOSED");
                }
            };
            _currentProcess.ErrorDataReceived += (s, e) => { 
                Debug.Print("Error Data Received" + e.Data);
                if (e.Data != null) App.Current.Dispatcher.Invoke(() => ConsoleOutput += "ERR: " + e.Data + "\n"); 
            };
            try {
                _currentProcess.Start();
                _currentProcess.BeginOutputReadLine();
                _currentProcess.BeginErrorReadLine();
                Debug.Print($"[{sw.ElapsedMilliseconds}ms] STARTING WAIT");
                await _currentProcess.WaitForExitAsync();
                Debug.Print($"[{sw.ElapsedMilliseconds}ms] WAIT FINISHED");
            }
            catch (Exception ex) {
                ConsoleOutput += $"Error: {ex.Message}\n";
            }
            finally { 
                _currentProcess = null; 
                IsProcessRunning = false; 
                Status = "Command Finished."; 

                Debug.Print($"[{sw.ElapsedMilliseconds}ms] STARTING LOADING SETS");
                _ = LoadSets();
                Debug.Print($"[{sw.ElapsedMilliseconds}ms] FINISH LOADING SETS");
            }
        }

        public void StopDdbProcess()
        {
            try { if (_currentProcess != null && !_currentProcess.HasExited) { _currentProcess.Kill(true); ConsoleOutput += "\n!!! Process Aborted by User !!!\n"; } } catch { }
        }

        public string GenerateBackupArgs()
        {
            if (SelectedProfile == null) return "";
            StringBuilder sb = new StringBuilder();
            sb.Append($"backup \"{StorePath}\" --verbose --set-name \"{SelectedProfile.SetName}\" --from \"{SelectedProfile.SourcePath}\" ");
            foreach (var ex in GlobalExcludes) sb.Append($"--exclude \"{ex}\" ");
            foreach (var inc in GlobalIncludes) sb.Append($"--include \"{inc}\" ");
            foreach (var ex in SelectedProfile.Excludes) sb.Append($"--exclude \"{ex}\" ");
            foreach (var inc in SelectedProfile.Includes) sb.Append($"--include \"{inc}\" ");
            return sb.ToString().Trim();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using DDBManager.Models;
using DDBManager.ViewModels;

namespace DDBManager
{
    public partial class MainWindow : Window
    {
        private MainViewModel vm = new MainViewModel();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = vm;
        }

        private void BtnSelectStore_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Select ddb Store Root" };
            if (dialog.ShowDialog() == true) vm.StorePath = dialog.FolderName;
        }

        private void BtnBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Select Source Folder" };
            if (dialog.ShowDialog() == true && vm.SelectedProfile != null)
            {
                vm.SelectedProfile.SourcePath = dialog.FolderName;
                vm.SaveConfig();
            }
        }

        private void MenuDeleteInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is BackupInstanceNode instance)
            {
                var result = MessageBox.Show($"Are you sure you want to delete the backup instance from {instance.Timestamp}?\n\nThis only deletes the manifest file. Run 'clean' later to reclaim space.", "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes) vm.DeleteBackupInstance(instance);
            }
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            var about = new AboutWindow();
            about.Owner = this; // Makes it center over the main app
            about.ShowDialog(); // ShowDialog makes it a "modal" (must close it to go back)
        }

        #region Profile Management
        private void BtnAddProfile_Click(object sender, RoutedEventArgs e)
        {
            var p = new BackupProfile { SetName = "NEW_SET" };
            vm.Profiles.Add(p); vm.SelectedProfile = p; vm.SaveConfig();
        }
        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (vm.SelectedProfile != null && vm.Profiles.Count > 1)
            {
                vm.Profiles.Remove(vm.SelectedProfile); vm.SelectedProfile = vm.Profiles.FirstOrDefault(); vm.SaveConfig();
            }
        }
        #endregion

        #region Pattern Handlers
        private void BtnAddGlobalExclude_Click(object sender, RoutedEventArgs e) => AddPattern(TxtGlobalExcludeInput, vm.GlobalExcludes, ListGlobalExcludes);
        private void BtnRemoveGlobalExclude_Click(object sender, RoutedEventArgs e) => RemovePattern(vm.GlobalExcludes, ListGlobalExcludes);
        private void TxtGlobalExcludeInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddPattern(TxtGlobalExcludeInput, vm.GlobalExcludes, ListGlobalExcludes); }

        private void BtnAddGlobalInclude_Click(object sender, RoutedEventArgs e) => AddPattern(TxtGlobalIncludeInput, vm.GlobalIncludes, ListGlobalIncludes);
        private void BtnRemoveGlobalInclude_Click(object sender, RoutedEventArgs e) => RemovePattern(vm.GlobalIncludes, ListGlobalIncludes);
        private void TxtGlobalIncludeInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddPattern(TxtGlobalIncludeInput, vm.GlobalIncludes, ListGlobalIncludes); }

        private void BtnAddExclude_Click(object sender, RoutedEventArgs e) => AddPattern(TxtExcludeInput, vm.SelectedProfile?.Excludes, ListExcludes);
        private void BtnRemoveExclude_Click(object sender, RoutedEventArgs e) => RemovePattern(vm.SelectedProfile?.Excludes, ListExcludes);
        private void TxtExcludeInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddPattern(TxtExcludeInput, vm.SelectedProfile?.Excludes, ListExcludes); }

        private void BtnAddInclude_Click(object sender, RoutedEventArgs e) => AddPattern(TxtIncludeInput, vm.SelectedProfile?.Includes, ListIncludes);
        private void BtnRemoveInclude_Click(object sender, RoutedEventArgs e) => RemovePattern(vm.SelectedProfile?.Includes, ListIncludes);
        private void TxtIncludeInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddPattern(TxtIncludeInput, vm.SelectedProfile?.Includes, ListIncludes); }

        private void AddPattern(TextBox input, ObservableCollection<string>? list, ListBox lb)
        {
            if (list != null && !string.IsNullOrWhiteSpace(input.Text))
            {
                list.Add(input.Text); input.Clear(); vm.SaveConfig(); lb.ScrollIntoView(list.Last());
            }
        }
        private void RemovePattern(ObservableCollection<string>? list, ListBox lb)
        {
            if (list != null && lb.SelectedItem is string selected) { list.Remove(selected); vm.SaveConfig(); }
        }
        #endregion

        private async void LeftTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is BackupInstanceNode instance) await vm.BuildFileTree(instance);
        }

        private void RightTree_Expanded(object sender, RoutedEventArgs e)
        {
            var item = e.OriginalSource as TreeViewItem;
            if (item?.Header is FileSystemNode node && node.IsFolder && !node.HasLoadedChildren)
            {
                node.Children.Clear(); vm.LoadSubLevel(node.Children, node.FullPath); node.HasLoadedChildren = true;
            }
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(vm.StorePath)) return;
            var targetDialog = new OpenFolderDialog { Title = "Restore to..." };
            if (targetDialog.ShowDialog() != true) return;
            var toRestore = vm.GetFilesToRestore();
            if (toRestore.Count == 0) return;
            var progressWin = new RestoreProgressWindow { Owner = this };
            progressWin.Show();
            await Task.Run(() => {
                for (int i = 0; i < toRestore.Count; i++)
                {
                    if (progressWin.IsCancelled) break;
                    var file = toRestore[i];
                    progressWin.UpdateProgress(i + 1, toRestore.Count, file.Path);
                    try
                    {
                        string source = file.GetPhysicalStorePath(vm.StorePath);
                        string target = Path.Combine(targetDialog.FolderName, file.Path);
                        if (!File.Exists(source)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        using FileStream fsIn = new(source, FileMode.Open, FileAccess.Read);
                        using GZipStream decompressor = new(fsIn, CompressionMode.Decompress);
                        using FileStream fsOut = new(target, FileMode.Create, FileAccess.Write);
                        decompressor.CopyTo(fsOut, 81920);
                        File.SetLastWriteTime(target, file.MTime);
                    }
                    catch { }
                }
            });
            progressWin.Close();
        }

        private async void BtnRunCheck_Click(object sender, RoutedEventArgs e) => await vm.RunDdbCommand($"verify \"{vm.StorePath}\" --verbose");
        private async void BtnRunCleanup_Click(object sender, RoutedEventArgs e) => await vm.RunDdbCommand($"clean \"{vm.StorePath}\" --verbose");
        private async void BtnRunBackup_Click(object sender, RoutedEventArgs e) { vm.SaveConfig(); await vm.RunDdbCommand(vm.GenerateBackupArgs()); }
        private void BtnStop_Click(object sender, RoutedEventArgs e) => vm.StopDdbProcess();
        private void Console_TextChanged(object sender, TextChangedEventArgs e) { if (sender is TextBox tb) tb.ScrollToEnd(); }
    }
}
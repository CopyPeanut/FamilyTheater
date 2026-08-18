using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace LoginWindow.Views
{
    public partial class ExecutableSelectionWindow : Window
    {
        public ObservableCollection<ExecutableOption> Executables { get; } = new();

        public string? SelectedPath { get; private set; }

        public ExecutableSelectionWindow(string gameFolder, IReadOnlyList<string> candidates)
        {
            InitializeComponent();
            DataContext = this;

            foreach (var path in candidates)
            {
                Executables.Add(new ExecutableOption(gameFolder, path));
            }

            ExecutableList.SelectedIndex = Executables.Count > 0 ? 0 : -1;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ExecutableList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        private void ConfirmSelection()
        {
            if (ExecutableList.SelectedItem is not ExecutableOption option)
            {
                return;
            }

            SelectedPath = option.FullPath;
            DialogResult = true;
            Close();
        }
    }

    public sealed class ExecutableOption
    {
        public string FullPath { get; }

        public string FileName { get; }

        public string RelativePath { get; }

        public ExecutableOption(string gameFolder, string fullPath)
        {
            FullPath = fullPath;
            FileName = Path.GetFileName(fullPath);
            RelativePath = GetRelativePath(gameFolder, fullPath);
        }

        private static string GetRelativePath(string gameFolder, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(gameFolder))
            {
                return fullPath;
            }

            try
            {
                return Path.GetRelativePath(gameFolder, fullPath);
            }
            catch
            {
                return fullPath;
            }
        }
    }
}

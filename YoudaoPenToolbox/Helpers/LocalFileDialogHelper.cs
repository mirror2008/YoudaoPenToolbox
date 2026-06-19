using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace YoudaoPenToolbox.Helpers
{
    public static class LocalFileDialogHelper
    {
        private static string _lastDirectory;

        public static string DefaultDirectory
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_lastDirectory) && Directory.Exists(_lastDirectory))
                {
                    return _lastDirectory;
                }

                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        public static bool TryPickSaveFile(string title, string suggestedFileName, out string filePath, string filter = null)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                FileName = suggestedFileName,
                Filter = filter ?? BuildSaveFilter(suggestedFileName),
                InitialDirectory = DefaultDirectory,
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dlg.ShowDialog() == true)
            {
                filePath = dlg.FileName;
                RememberDirectory(filePath);
                return true;
            }

            filePath = null;
            return false;
        }

        public static bool TryPickSaveDirectory(string description, out string directoryPath)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = description;
                dlg.SelectedPath = DefaultDirectory;
                dlg.ShowNewFolderButton = true;

                if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
                {
                    directoryPath = dlg.SelectedPath;
                    RememberDirectory(directoryPath);
                    return true;
                }
            }

            directoryPath = null;
            return false;
        }

        public static string BuildSaveFilter(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return "所有文件|*.*";
            }

            var pattern = "*" + extension;
            var label = extension.TrimStart('.').ToUpperInvariant() + " 文件";
            return $"{label}|{pattern}|所有文件|*.*";
        }

        public static string BuildUniqueLocalPath(string directory, string fileName)
        {
            var safeName = string.IsNullOrWhiteSpace(fileName) ? "remote_file" : fileName;
            var target = Path.Combine(directory, safeName);
            if (!File.Exists(target))
            {
                return target;
            }

            var nameWithoutExt = Path.GetFileNameWithoutExtension(safeName);
            var extension = Path.GetExtension(safeName);
            for (var i = 1; i < 1000; i++)
            {
                var candidate = Path.Combine(directory, $"{nameWithoutExt} ({i}){extension}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(directory, $"{nameWithoutExt}_{DateTime.Now:yyyyMMddHHmmss}{extension}");
        }

        private static void RememberDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                _lastDirectory = directory;
            }
        }
    }
}

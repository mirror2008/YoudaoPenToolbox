namespace YoudaoPenToolbox.Models
{
    public class RemoteFileItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsSymlink { get; set; }
        public long SizeBytes { get; set; }
        public string SizeDisplay { get; set; }
        public string Permissions { get; set; }
        public string ModifiedDisplay { get; set; }
        public string SymlinkTarget { get; set; }

        public string TypeDisplay
        {
            get
            {
                if (IsDirectory)
                {
                    return "文件夹";
                }

                return IsSymlink ? "链接" : "文件";
            }
        }

        public bool CanEnter => IsDirectory || IsSymlink;
    }
}

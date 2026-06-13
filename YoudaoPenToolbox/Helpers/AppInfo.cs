using System.Reflection;

namespace YoudaoPenToolbox.Helpers
{
    public static class AppInfo
    {
        public const string GithubUrl = "https://github.com/mirror2008/YoudaoPenToolbox";

        public static string VersionText
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                var build = version.Build < 0 ? 0 : version.Build;
                var revision = version.Revision < 0 ? 0 : version.Revision;

                if (revision > 0)
                {
                    return $"{version.Major}.{version.Minor}.{build}.{revision}";
                }

                return $"{version.Major}.{version.Minor}.{build}";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{

    public static class ProtectedSystemAppPolicy
    {
        private static readonly HashSet<string> ProtectedAppIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "8001699004677776",
            "8001712908385557",
            "8001736586947101",
            "8001654057944134",
            "8001678699362882",
            "8001650599023931",
            "8080252464522508",
            "8001684209830578",
            "8080212246010681",
            "8080282263329158",
            "8080212693101341",
            "8001659430761211",
            "8001671616562847",
            "8080292157485624",
            "8001670668055425",
            "8001735023580768",
            "8080262605498742",
            "8080272425914438",
            "8001661999525016",
            "8001679380845889",
            "8080232418330628",
            "8001693795735455",
            "8080222501178405",
            "8001718963156066",
            "8001707294117702",
            "8001657101235091",
            "8001673244388308",
            "8080282888534774",
            "8001657592345846",
            "8080212335092787",
            "8080212680903142",
            "8080232030310583",
            "8001666679481944",
            "8001656491465980",
            "8001667273038889",
            "8001660789649766",
            "8001733817189797",
            "8080222437664451"
        };

        public static bool IsProtected(string appId)
        {
            return !string.IsNullOrWhiteSpace(appId) && ProtectedAppIds.Contains(appId.Trim());
        }

        public static void Apply(InstalledApp app)
        {
            if (app == null)
            {
                return;
            }

            app.IsProtectedSystemApp = IsProtected(app.AppId);
        }

        public static string GetDefaultBackupDirectory()
        {
            return AppBackupService.GetSystemAppBackupDirectory();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UpdateCatalog.Core
{
    public class Link
    {
        public string KBNumber;
        public string WindowsVersion;
        public string Version;
        public string Architecture;
        public string Url;

        public Link(string link)
        {
            Regex rgx = new Regex(@"http:\/\/([a-z0-9]+[.])*(windowsupdate|microsoft)\.com\/([a-z]{1}\/msdownload\/update\/software\/[a-z]+\/\d{4}\/\d{2}|download\/[a-z0-9]{1}\/[a-z0-9]{1}\/[a-z0-9]{1}\/[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})\/windows(?<windowsVersion>\d+\.\d|8-RT|Blue)-(?<kbNumber>KB\d+)(?<version>-v\d+)?-(?<architecture>x64|x86|ia64|arm)(-express)?([_a-z0-9]+)?.(?<filetype>msu|cab|exe)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (rgx.IsMatch(link))
            {
                Match match = rgx.Match(link);

                KBNumber = match.Groups["kbNumber"].Value.ToLower();
                WindowsVersion = match.Groups["windowsVersion"].Value.ToUpper().Replace("BLUE", "8.1");
                Version = match.Groups["version"].Value.ToLower().Replace("-v", "") == "" ? "1" : match.Groups["version"].Value.ToLower().Replace("-v", "");
                Architecture = match.Groups["architecture"].Value;
                Url = match.Value.ToLower();
            }
            
        }
    }
}

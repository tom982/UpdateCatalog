using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UpdateCatalog.Core
{
    public class Update
    {
        [JsonProperty(PropertyName = "FooBar")]

        public string kbNumber { get; set; }
        public string windowsVersion { get; set; }
        public string downloadLink { get; set; }
        public string architecture { get; set; }
        public Cabfile[] cabFiles { get; set; }
    }

    public class Cabfile
    {
        public string filename { get; set; }
        public string hash { get; set; }
        public Manifest[] manifests { get; set; }
        public Package[] packages { get; set; }
        public Payload[] payloads { get; set; }
    }

    public class Manifest
    {
        public string name { get; set; }
        public string hash { get; set; }
    }

    public class Package
    {
        public string name { get; set; }
        public string hash { get; set; }
    }

    public class Payload
    {
        public string name { get; set; }
        public string hash { get; set; }
    }

}

using Newtonsoft.Json;

namespace UpdateCatalog.Core
{
    public class Update
    {
        [JsonProperty(PropertyName = "kbNumber")]
        public string KBNumber { get; set; }
        [JsonProperty(PropertyName = "windowsVersion")]
        public string WindowsVersion { get; set; }
        [JsonProperty(PropertyName = "downloadLink")]
        public string DownloadLink { get; set; }
        [JsonProperty(PropertyName = "architecture")]
        public string Architecture { get; set; }
        [JsonProperty(PropertyName = "cabFiles")]
        public Cabfile[] CabFiles { get; set; }
    }

    public class Cabfile
    {
        [JsonProperty(PropertyName = "filename")]
        public string Filename { get; set; }
        [JsonProperty(PropertyName = "hash")]
        public string Hash { get; set; }
        [JsonProperty(PropertyName = "manifests")]
        public Manifest[] Manifests { get; set; }
        [JsonProperty(PropertyName = "packages")]
        public Package[] Packages { get; set; }
        [JsonProperty(PropertyName = "payloads")]
        public Payload[] Payloads { get; set; }
    }

    public class Manifest : UpdateFile
    {
        
    }

    public class Package : UpdateFile
    {

    }

    public class Payload : UpdateFile
    {

    }

    public class UpdateFile
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "hash")]
        public string Hash { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml;

namespace UpdateCatalog.Core
{
    static class Extract
    {
        private const string TempDirectory = "D:\\UC\\Temp";
        private static readonly string ExpandPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\\system32\\expand.exe";
        private static readonly List<string> Hashes = LoadHashes();

        public static Update File(string path)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            if (!Directory.Exists(TempDirectory))
                Directory.CreateDirectory(TempDirectory);

            if (Hashes.Contains(Tools.SHA256b64(path)))
                return null;

            Update output = null;

            switch (Path.GetExtension(path))
            {
                case ".msu":
                    output = ExtractMsu(path);
                    break;
                case ".cab":
                    output = new Update
                    {
                        CabFiles = new [] {ExtractCab(path)}
                    };
                    break;
                default:
                    throw new FileLoadException("Invalid file extension");
            }
            Hashes.Add(Tools.SHA256b64(path));

            sw.Stop();
            MessageBox.Show(sw.ElapsedMilliseconds.ToString());

            DirectoryInfo myDirInfo = new DirectoryInfo(TempDirectory);
            foreach (FileInfo file in myDirInfo.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in myDirInfo.GetDirectories())
            {
                dir.Delete(true);
            }
            
            return output;
        }

        private static Update ExtractMsu(string path)
        {
            string outputDirectory = Expand(path);
            string architecture = path.ToLower().Contains("-x64") ? "x64" : "x86";
            string version = Regex.Match(path, "(\\d+\\.\\d+)").Value;
            string kb = GetKBNumber(path);

            Update output = new Update
            {
                DownloadLink = "",
                Architecture = architecture,
                CabFiles = null,
                KBNumber = kb,
                WindowsVersion = version
            };

            List<Cabfile> cabfiles = new List<Cabfile>();

            foreach (string cabfile in Directory.GetFiles(outputDirectory, "*.cab", SearchOption.TopDirectoryOnly))
            {
                if (cabfile.ToLower().Contains("wsusscan.cab")) continue;
                cabfiles.Add(ExtractCab(cabfile, outputDirectory));
            }

            output.CabFiles = cabfiles.ToArray();

            return output;
        }

        private static Cabfile ExtractCab(string path, string directory = TempDirectory)
        {
            string outputDirectory = Expand(path, directory);
            RenameDefaults(outputDirectory);

            Cabfile output = new Cabfile
            {
                Filename = Path.GetFileName(path),
                Hash = Tools.SHA256b64(path)
            };


            List<Package> packages = new List<Package>();
            List<Manifest> manifests = new List<Manifest>();
            List<Payload> payloads = new List<Payload>();


            foreach (string file in Directory.GetFiles(outputDirectory, "*.*", SearchOption.AllDirectories))
            {
                switch (Path.GetExtension(file).ToLower())
                {
                    case ".cat":
                        packages.Add(new Package
                        {
                            Name = Path.GetFileName(file),
                            Hash = Tools.SHA256b64(file)
                        });
                        break;
                    case ".mum":
                        packages.Add(new Package
                        {
                            Name = Path.GetFileName(file),
                            Hash = Tools.SHA256b64(file)
                        });
                        break;
                    case ".manifest":
                        manifests.Add(new Manifest
                        {
                            Name = Path.GetFileName(file),
                            Hash = Tools.SHA256b64(file)
                        });
                        break;
                    default:
                        payloads.Add(new Payload { 
                            Name = Path.GetFileName(Path.GetDirectoryName(file)) + "\\" + Path.GetFileName(file),
                            Hash = Tools.SHA256b64(file)
                        });
                        break;
                }
            }

            output.Packages = packages.ToArray();
            output.Manifests = manifests.ToArray();
            output.Payloads = payloads.ToArray();

            return output;
        }

        private static string Expand(string file, string directory = TempDirectory)
        {
            string outputDirectory = Path.Combine(directory, Path.GetFileNameWithoutExtension(file));
            Directory.CreateDirectory(outputDirectory);

            using (Process p = new Process())
            {
                p.StartInfo = new ProcessStartInfo
                {
                    FileName = ExpandPath,
                    Arguments = $" -f:* \"{file}\" \"{outputDirectory}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }; ;

                p.Start();
                p.WaitForExit();
            }

            return outputDirectory;
        }

        private static List<string> LoadHashes()
        {
            return new List<string>();
        }

        private static string GetKBNumber(string path)
        {
            Regex rgx = new Regex(@"KB\d+", RegexOptions.IgnoreCase);
            return rgx.Matches(path).Count > 0
                ? rgx.Matches(path)[0].ToString().ToUpper()
                : null;
        }

        private static void RenameDefaults(string path)
        {
            List<string> defaultFiles = Directory.GetFiles(path)
                .Where(n => Path.GetFileName(n).ToLower() == "update.mum" || Path.GetFileName(n).ToLower() == "update-bf.mum")
                .ToList();

            foreach (string file in defaultFiles)
            {
                string assemblyIdentity = string.Empty;

                using (System.IO.StreamReader reader = new System.IO.StreamReader(file))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains("<assemblyIdentity"))
                        {
                            assemblyIdentity = line;
                            break;
                        }
                    }
                    reader.Close();
                }

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(assemblyIdentity);

                // Check for nulls
                string name = xmlDoc.SelectSingleNode("//assemblyIdentity/@name").Value;
                string processorArchitecture = xmlDoc.SelectSingleNode("//assemblyIdentity/@processorArchitecture").Value;
                string publicKeyToken = xmlDoc.SelectSingleNode("//assemblyIdentity/@publicKeyToken").Value;
                string version = xmlDoc.SelectSingleNode("//assemblyIdentity/@version").Value;
                string defaultPackage = name + "~" + publicKeyToken + "~" + processorArchitecture + "~~" + version;

                if (defaultPackage.ToLower().StartsWith("microsoft-windows-ie") && defaultPackage.ToLower().Contains("~neutral~~"))
                    defaultPackage = defaultPackage.ToLower().Replace("~neutral~~", "~~~");

                string newPath = Directory.GetParent(file) + "\\" + defaultPackage + ".mum";

                System.IO.File.Move(file, newPath);
                System.IO.File.Move(Path.ChangeExtension(file, ".cat"), Path.ChangeExtension(newPath, ".cat"));
            }
        }
    }
}

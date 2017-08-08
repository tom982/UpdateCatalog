﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Delimon.Win32.IO;

namespace UpdateCatalog.Core
{
    internal static class Extract
    {
        /// <summary>Directory where updates will be extracted to</summary>
        private const string TempDirectory = @"D:\Libraries\Software\wsusoffline\client\w61-x64\glb\temp"; // TODO: Variable

        private const string rgxFilename = @"windows(?<windowsVersion>\\d+\\.\\d|8-RT|Blue)-(?<kbNumber>KB\\d+)(?<version>-v\\d+)?-(?<architecture>x64|x86|ia64|arm).*\\.(?<filetype>msu|cab)";

        /// <summary>Filepath of expand.exe</summary>
        private static readonly string ExpandPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\\system32\\expand.exe";

        /// <summary>List of already processed file hashes</summary>
        private static readonly List<string> Hashes = LoadHashes(); // TODO: Save hashes

        /// <summary> Processes an update file (.msu/.cab) into an Update object </summary>
        /// <param name="path">Path of update file to process</param>
        public static Update File(string path)
        {
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
                    output = ExtractCab(path);
                    break;
                    // TODO: Add exe support
            }

            Hashes.Add(Tools.SHA256b64(path));

            DirectoryInfo myDirInfo = new DirectoryInfo(Path.Combine(TempDirectory, Path.GetFileNameWithoutExtension(path)));
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
            // Extract file to folder
            string outputDirectory = Expand(path);
            
            // Detect update details
            string filename = Path.GetFileName(path);
            Regex rgx = new Regex(rgxFilename, RegexOptions.IgnoreCase);

            GroupCollection groups = rgx.Match(filename).Groups;

            string architecture = groups["architecture"].Value;
            string windowsVersion = groups["windowsVersion"].Value.ToUpper().Replace("BLUE", "8.1");
            string kb = groups["kbNumber"].Value;
            string version = groups["version"].Value.ToLower().Replace("-v", "") == "" ? "1" : groups["version"].Value.ToLower().Replace("-v", "");

            if (kb == null)
                throw new NullReferenceException("KB number is null. My code sucks");

            Update output = new Update
            {
                DownloadLink = "",
                Architecture = architecture,
                CabFiles = null,
                KBNumber = kb,
                Version = version,
                WindowsVersion = windowsVersion
            };

            List<Cabfile> cabfiles = new List<Cabfile>();

            foreach (string cabfile in Directory.GetFiles(outputDirectory, "*.cab", SearchOption.TopDirectoryOnly))
            {
                if (cabfile.ToLower().Contains("wsusscan.cab")) continue;
                cabfiles.Add(ExpandCab(cabfile, outputDirectory));
            }

            output.CabFiles = cabfiles.ToArray();

            return output;
        }

        private static Update ExtractCab(string path)
        {
            string filename = Path.GetFileName(path);
            Regex rgx = new Regex(rgxFilename, RegexOptions.IgnoreCase);

            GroupCollection groups = rgx.Match(filename).Groups;

            string architecture = groups["architecture"].Value;
            string windowsVersion = groups["windowsVersion"].Value.ToUpper().Replace("BLUE", "8.1");
            string kb = groups["kbNumber"].Value;
            string version = groups["version"].Value.ToLower().Replace("-v", "") == "" ? "1" : groups["version"].Value.ToLower().Replace("-v", "");

            if (kb == null)
                throw new NullReferenceException("KB number is null. My code sucks");

            Update output = new Update
            {
                DownloadLink = "",
                Architecture = architecture,
                KBNumber = kb,
                Version = version,
                WindowsVersion = windowsVersion,
                CabFiles = new[] {ExpandCab(path)}
            };

            return output;
        }

        private static Cabfile ExpandCab(string path, string directory = TempDirectory)
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
                        payloads.Add(new Payload
                        { 
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

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            using (Process p = new Process())
            {
                p.StartInfo = new ProcessStartInfo
                {
                    FileName = ExpandPath,
                    Arguments = $" -f:* \"{file}\" \"{outputDirectory}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                p.Start();
                p.WaitForExit();
            }

            return outputDirectory;
        }

        private static List<string> LoadHashes()
        {
            // TODO: Load hashes from settings
            return new List<string>();
        }

        private static void RenameDefaults(string path)
        {
            List<string> defaultFiles = Directory.GetFiles(path)
                .Where(n => Path.GetFileName(n).ToLower() == "update.mum" || Path.GetFileName(n).ToLower() == "update-bf.mum")
                .ToList();

            foreach (string file in defaultFiles)
            {
                string assemblyIdentity = string.Empty;

                string[] lines = Delimon.Win32.IO.File.ReadAllLines(file);

                foreach (string line in lines)
                {
                    if (line.Contains("<assemblyIdentity"))
                    {
                        assemblyIdentity = line;
                        break;
                    }
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

                string newmum = Directory.GetParent(file) + "\\" + defaultPackage + ".mum";
                string newcat = Directory.GetParent(file) + "\\" + defaultPackage + ".cat";

                if (Delimon.Win32.IO.File.Exists(newmum))
                    Delimon.Win32.IO.File.Delete(newmum);
                if (Delimon.Win32.IO.File.Exists(newcat))
                    Delimon.Win32.IO.File.Delete(newcat);

                Delimon.Win32.IO.File.Move(file, newmum);
                Delimon.Win32.IO.File.Move(file.Substring(0, file.Length - 4) + ".cat", newcat);
            }
        }
    }
}

using System;
using System.Security.Cryptography;
using Delimon.Win32.IO;

namespace UpdateCatalog.Core
{
    internal static class Tools
    {
        public static string SHA256b64(string path)
        {
            if (path == null)
                throw new NullReferenceException();
            if (Path.GetFileName(path).ToLower() == "wsusscan.cab")
                return "";

            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = File.ReadAllBytes(path);
                bytes = sha.ComputeHash(bytes);
                return Convert.ToBase64String(bytes);
            }
        }

        public static string SHA1(string path)
        {
            using (var cryptoProvider = new SHA1CryptoServiceProvider())
            {
                return BitConverter.ToString(cryptoProvider.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
            }
        }

        public static void EmptyDirectory(string path)
        {
            DirectoryInfo myDirInfo = new DirectoryInfo(path);
            foreach (FileInfo file in myDirInfo.GetFiles())
            {
                file.Delete();
            }

            foreach (DirectoryInfo dir in myDirInfo.GetDirectories())
            {
                dir.Delete(true);
            }
        }
    }
}

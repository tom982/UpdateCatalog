using System;
using System.Net;
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
    }
}

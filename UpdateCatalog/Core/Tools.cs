using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace UpdateCatalog.Core
{
    static class Tools
    {
        public static string SHA256b64(string path)
        {
            if (path == null)
                throw new NullReferenceException();

            byte[] bytes = SHA256.Create().ComputeHash(File.ReadAllBytes(path));
            return Convert.ToBase64String(bytes);
        }
    }
}

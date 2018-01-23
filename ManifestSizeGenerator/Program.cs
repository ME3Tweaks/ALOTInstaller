using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ManifestSizeGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Required input is not available. Takes 2 args: filedir and manifestfilepath");
                Environment.Exit(1);
            }
            var files = Directory.GetFiles(args[0]);
            var manifestfile = args[1];

            XmlDocument doc = new XmlDocument();
            doc.Load(manifestfile);
            XmlNode root = doc.DocumentElement;

            foreach (var file in files)
            {
                FileInfo info = new FileInfo(file);
                string xpathstr = "/alotaddonmanifest/addonfile/file[@filename='" + Path.GetFileName(file) + "']";
                //Console.WriteLine(xpathstr);
                XmlNode node = root.SelectSingleNode(xpathstr);
                if (node != null)
                {
                    Console.WriteLine("Calculating info for " + Path.GetFileName(file));

                    XmlAttribute attr = doc.CreateAttribute("size");
                    attr.Value = info.Length.ToString();
                    SetAttrSafe(node, attr);

                    string hash = CalculateMD5(file);
                    attr = doc.CreateAttribute("md5");
                    attr.Value = hash;
                    SetAttrSafe(node, attr);
                    Console.WriteLine(Path.GetFileName(file) + " " + info.Length + " " + hash);
                }
            }
            doc.Save(manifestfile);
            Console.WriteLine("Press any key to close");
            Console.ReadKey();
        }

        private static void SetAttrSafe(XmlNode node, params XmlAttribute[] attrList)
        {
            foreach (var attr in attrList)
            {
                if (node.Attributes[attr.Name] != null)
                {
                    node.Attributes[attr.Name].Value = attr.Value;
                }
                else
                {
                    node.Attributes.Append(attr);
                }
            }
        }

        private static string CalculateMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
    }
}

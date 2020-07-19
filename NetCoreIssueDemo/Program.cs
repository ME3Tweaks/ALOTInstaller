using System;
using System.IO;

namespace NetCoreIssueDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var folder = AppDataFolder();
            Console.WriteLine("The folder is: " + folder);
        }

        public static string AppDataFolder() => Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.ModuleName))).FullName;

    }
}

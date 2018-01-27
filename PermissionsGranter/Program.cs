using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace PermissionsGranter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Count() < 2)
            {
                Console.WriteLine("Requires username and list of folders as arguments: <Username> <Folderpath> [<Folderpath2>] [<Folderpath3>]...");
                Environment.Exit(1);
            }
            bool successful = true;

            string username = args[0];
            bool isFolderCreate = false;
            foreach (string folder in args.Where((source, index) => index != 0).ToArray())
            {
                if (folder.ToLower() == "-create-directory")
                {
                    isFolderCreate = true;
                    continue;
                }
                if (isFolderCreate)
                {
                    Console.WriteLine("Creating directory " + folder);
                    Directory.CreateDirectory(folder);
                    isFolderCreate = false;
                }
                if (Directory.Exists(folder))
                {
                    Console.WriteLine("Granting write permissions to " + username + " on: " + folder);
                    if (!GrantAccess(username, folder))
                    {
                        Console.WriteLine("Failed to grant write permissions to Everyone on: " + folder);
                        successful = false;
                    }
                }
                else
                {
                    Console.WriteLine("Directory doesn't exist: " + folder);
                    successful = false;
                }
            }
            //Console.WriteLine("Press any key to close");
            //Console.ReadKey();
            Environment.Exit(successful ? 0 : 1);
        }

        public static bool GrantAccess(string username, string fullPath)
        {
            try
            {
                NTAccount f = new NTAccount(username);
                SecurityIdentifier userSID = (SecurityIdentifier)f.Translate(typeof(SecurityIdentifier));

                DirectoryInfo dInfo = new DirectoryInfo(fullPath);
                DirectorySecurity dSecurity = dInfo.GetAccessControl();
                dSecurity.AddAccessRule(new FileSystemAccessRule(userSID, FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow));
                dInfo.SetAccessControl(dSecurity);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            return true;
        }
    }
}

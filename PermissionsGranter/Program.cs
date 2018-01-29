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
                PrintHelp();
                Environment.Exit(1);
            }
            string msg = "PermissionsGranter.exe";
            foreach (string arg in args)
            {
                msg += " [" + arg+"]";
            }
            Console.WriteLine(msg);
            bool successful = true;

            string username = args[0];
            bool isFolderCreate = false;
            bool isRegistryKeyCreate = false;
            foreach (string folder in args.Where((source, index) => index != 0).ToArray())
            {
                if (folder.ToLower() == "-create-directory")
                {
                    isFolderCreate = true;
                    continue;
                }
                if (folder.ToLower() == "-create-hklm-reg-key")
                {
                    isRegistryKeyCreate = true;
                    continue;
                }

                if (isRegistryKeyCreate)
                {
                    Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(folder); //"Software\\Wow6432Node\\AGEIA Technologies"
                    RegistrySecurity rs = new RegistrySecurity();
                    rs = key.GetAccessControl();
                    CanonicalizeDacl(rs);
                    rs.AddAccessRule(new RegistryAccessRule(username, RegistryRights.WriteKey | RegistryRights.ReadKey | RegistryRights.Delete | RegistryRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                    CanonicalizeDacl(rs);
                    key.SetAccessControl(rs);
                    isRegistryKeyCreate = false;
                }
                else
                {

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
            }
            //Console.WriteLine("Press any key to close");
            //Console.ReadKey();
            Environment.Exit(successful ? 0 : 1);
        }

        private static void PrintHelp()
        {
            Console.WriteLine("PermissionsGranter.exe");
            Console.WriteLine("Written by Mgamerz (ME3Tweaks)");
            Console.WriteLine("");
            Console.WriteLine("Usage:");
            Console.WriteLine("PermissionsGranter.exe \"domain\\username\" [-create-directory directory] [-create-hklm-reg-key subkey] [FolderToGivePermissionsTo ] ...");
            Console.WriteLine("\t-create-directory <directory>");
            Console.WriteLine("\t\tCreate specified directory and give the passed in user permissions to that folder.");
            Console.WriteLine("\t-create-hklm-reg-key <subkeypath>");
            Console.WriteLine("\t\tCreates a specified registry key under HKLM and assigns user permissions to it for editing.");
            Console.WriteLine("");
            Console.WriteLine("\tHaving no parameter before a path will default to granting permissions to a folder.");
            Console.WriteLine("\tYou can chain commands together into a list to do all actions in one elevation run.");
        }

        public static bool GrantAccess(string username, string fullPath)
        {
            try
            {
                NTAccount f = new NTAccount(username);
                SecurityIdentifier userSID = (SecurityIdentifier)f.Translate(typeof(SecurityIdentifier));

                DirectoryInfo dInfo = new DirectoryInfo(fullPath);
                DirectorySecurity dSecurity = dInfo.GetAccessControl();
                CanonicalizeDacl(dSecurity);
                dSecurity.AddAccessRule(new FileSystemAccessRule(userSID, FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow));
                CanonicalizeDacl(dSecurity);

                dInfo.SetAccessControl(dSecurity);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        static void CanonicalizeDacl(NativeObjectSecurity objectSecurity)
        {
            if (objectSecurity == null) { throw new ArgumentNullException("objectSecurity"); }
            if (objectSecurity.AreAccessRulesCanonical) { return; }

            // A canonical ACL must have ACES sorted according to the following order:
            //   1. Access-denied on the object
            //   2. Access-denied on a child or property
            //   3. Access-allowed on the object
            //   4. Access-allowed on a child or property
            //   5. All inherited ACEs 
            RawSecurityDescriptor descriptor = new RawSecurityDescriptor(objectSecurity.GetSecurityDescriptorSddlForm(AccessControlSections.Access));

            List<CommonAce> implicitDenyDacl = new List<CommonAce>();
            List<CommonAce> implicitDenyObjectDacl = new List<CommonAce>();
            List<CommonAce> inheritedDacl = new List<CommonAce>();
            List<CommonAce> implicitAllowDacl = new List<CommonAce>();
            List<CommonAce> implicitAllowObjectDacl = new List<CommonAce>();

            foreach (CommonAce ace in descriptor.DiscretionaryAcl)
            {
                if ((ace.AceFlags & AceFlags.Inherited) == AceFlags.Inherited) { inheritedDacl.Add(ace); }
                else
                {
                    switch (ace.AceType)
                    {
                        case AceType.AccessAllowed:
                            implicitAllowDacl.Add(ace);
                            break;

                        case AceType.AccessDenied:
                            implicitDenyDacl.Add(ace);
                            break;

                        case AceType.AccessAllowedObject:
                            implicitAllowObjectDacl.Add(ace);
                            break;

                        case AceType.AccessDeniedObject:
                            implicitDenyObjectDacl.Add(ace);
                            break;
                    }
                }
            }

            Int32 aceIndex = 0;
            RawAcl newDacl = new RawAcl(descriptor.DiscretionaryAcl.Revision, descriptor.DiscretionaryAcl.Count);
            implicitDenyDacl.ForEach(x => newDacl.InsertAce(aceIndex++, x));
            implicitDenyObjectDacl.ForEach(x => newDacl.InsertAce(aceIndex++, x));
            implicitAllowDacl.ForEach(x => newDacl.InsertAce(aceIndex++, x));
            implicitAllowObjectDacl.ForEach(x => newDacl.InsertAce(aceIndex++, x));
            inheritedDacl.ForEach(x => newDacl.InsertAce(aceIndex++, x));

            if (aceIndex != descriptor.DiscretionaryAcl.Count)
            {
                Console.WriteLine("The DACL cannot be canonicalized since it would potentially result in a loss of information");
                return;
            }

            descriptor.DiscretionaryAcl = newDacl;
            objectSecurity.SetSecurityDescriptorSddlForm(descriptor.GetSddlForm(AccessControlSections.Access), AccessControlSections.Access);
        }
    }
}

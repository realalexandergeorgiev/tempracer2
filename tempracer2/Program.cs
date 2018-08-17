using System;
using System.Collections;
using System.IO;
using System.Security;
using System.Security.Permissions;
using System.Threading;

namespace TempRacer
{
    class Program
    {
        public static FileStream fsHandle = null;
        public static ArrayList handleList = null;
        public static ArrayList blackListedFiles = null;
        public static int debugLevel = 1;   // 0 = only files not owned by us, writeable and bat/vbs/exe/ps1/py
                                            // 1 = only files not owned by us, writeable
                                            // 2 = writeable files, any owner
                                            // 3 = only files not owned by us
                                            // 4 = every single file


        public static void Main()
        {
            try
            {
                // init
                handleList = new ArrayList();
                blackListedFiles = new ArrayList();

                Console.WriteLine(Environment.NewLine);
                Console.WriteLine("TempRacer v2.0 by alexander.georgiev@daloo.de" + Environment.NewLine);
                Run();
            }
            catch (Exception e) { WriteRed("[-] Error:" + Environment.NewLine + e.ToString()); }
        }

        //[PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void Run()
        {
            string[] args = System.Environment.GetCommandLineArgs();

            // If a directory is not specified, exit program. 
            if (args.Length <= 2)
            {
                // Display the proper way to call the program.
                Console.WriteLine("Usage: tempracer2.exe <directory> <file/filter>" + Environment.NewLine);
                Console.WriteLine("Example 1 (watching everything in C:\\): tempracer2.exe C:\\ *" + Environment.NewLine);
                Console.WriteLine("Example 2 (watch only in C:\\temp\\ and subfolders for .bat files): tempracer2.exe C:\\Temp\\ *.bat");
                return;
            }
            if (args.Length == 4) debugLevel = int.Parse(args[3]);

                // Create a new FileSystemWatcher and set its properties.
                FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.IncludeSubdirectories = true;
            watcher.Path = args[1];
            /* Watch for changes in LastAccess and LastWrite times, and
               the renaming of files or directories. */
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
               | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            // Only watch text files.
            watcher.Filter = args[2];

            // Add event handlers.
            //watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.Created += new FileSystemEventHandler(OnChanged);
            //watcher.Deleted += new FileSystemEventHandler(OnChanged);
            watcher.Renamed += new RenamedEventHandler(OnRenamed);

            // Begin watching.
            watcher.EnableRaisingEvents = true;

            // Wait for the user to quit the program.
            Console.WriteLine("[+] Watching " + args[1] + " " + args[2] + Environment.NewLine);
            Console.WriteLine("[+] Press 'q' to quit");
            while (Console.Read() != 'q') ;
        }

        // Define the event handlers. 
        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            // skip blacklisted files
            if (blackListedFiles.Contains(e.FullPath)) return;

            // Specify what is done when a file is changed, created, or deleted.
            if (debugLevel == 4) Console.WriteLine("[+] File {0} {1}", e.FullPath, e.ChangeType);
            CheckFile(e.FullPath);
        }

        private static void OnRenamed(object source, RenamedEventArgs e)
        {
            // skip blacklisted files
            if (blackListedFiles.Contains(e.FullPath)) return;
            // Specify what is done when a file is renamed.
            if (debugLevel == 4) Console.WriteLine("[+] File: {0} renamed to {1}", e.OldFullPath, e.FullPath);
            CheckFile(e.FullPath);
        }



        public static Boolean CheckFile(String target)
        {
            // skip if blacklisted
            if (blackListedFiles.Contains(target)) return false;

            Boolean readWrite = CanReadWrite(target);

            Boolean notOwnedByUs = false;
            string currentUserName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            string owner = "Unknown Owner";
            try { owner = File.GetAccessControl(target).GetOwner(typeof(System.Security.Principal.NTAccount)).ToString(); }
            catch (Exception) { }
            if (owner != currentUserName && owner != "Unknown Owner") notOwnedByUs = true;

            Boolean coolExtension = false;
            if (target.EndsWith(".bat") || target.EndsWith(".exe") || target.EndsWith(".ps1") || target.EndsWith(".vbs")) coolExtension = true;

            if (debugLevel >= 0 && notOwnedByUs && readWrite && coolExtension) {
                WriteYellow("[+] Nice! Owner is not us (" + owner + ") and we might have write access! - " + target);
                blockFile(target);
            }
            else if (debugLevel >= 1 && notOwnedByUs && readWrite) { WriteYellow("[+] Owner is not us (" + owner + ") and we might have write access! Interesting. - " + target); blockFile(target); }
            else if (debugLevel >= 2 && readWrite) Console.Write("[+] We can read and write (but we are also the owner) - " + target + Environment.NewLine);
            else if (debugLevel >= 3 && notOwnedByUs) Console.Write("[+] Not our file (" + owner + ") and we cannot modify it - " + target + Environment.NewLine);

            Console.Beep();
            return false;
        }
        public static Boolean blockFile(String target)
        {
            int retryCount = 20;
            int i = 0;
            String exceptionMsg = "";

            Console.Write("[+] Blocking file for further changes");
            while (i <= retryCount)
            {
                try
                {
                    // Block file for writing, but allow read access (sharemode=read) to allow execution
                    //Thread.Sleep(500); // wait a moment until file is free for us  
                    //fsHandle = File.Open(target, FileMode.Open, FileAccess.Write, FileShare.Read);
                    fsHandle = File.Open(target, FileMode.Open, FileAccess.Read, FileShare.Read);
                    handleList.Add(fsHandle); // keep our lock
                    blackListedFiles.Add(target); // add file to blacklist to ignore further changes
                    WriteGreen(" successful!");
                    Console.Beep();
                    return true;
                }
                catch (Exception e)
                {
                    Console.Write(".");
                    i++;
                    Thread.Sleep(50);
                    exceptionMsg = e.Message;
                }
            }
            WriteRed(" failed! The process may have locked it for exclusive usage.");
            return false;
        }


        public static void WriteRed(String s)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(s);
            Console.ResetColor();
        }
        public static void WriteGreen(String s)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(s);
            Console.ResetColor();
        }
        public static void WriteYellow(String s)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(s);
            Console.ResetColor();
        }

        private static Boolean CanReadWrite(String path)
        {
            // https://msdn.microsoft.com/de-de/library/system.security.permissions.fileiopermission(v=vs.110).aspx
            FileIOPermission f = new FileIOPermission(FileIOPermissionAccess.Read, path);
            f.AddPathList(FileIOPermissionAccess.Write | FileIOPermissionAccess.Read, path);
            try
            {
                f.Demand();
                return true;
            }
            catch (SecurityException s)
            {
                //Console.WriteLine(s.Message);
                return false;
            }
        }
        private static Boolean CanRead(String path)
        {
            FileIOPermission f = new FileIOPermission(FileIOPermissionAccess.Read, path);
            try
            {
                f.Demand();
                return true;
            }
            catch (SecurityException)
            {
                //Console.WriteLine(s.Message);
                return false;
            }
        }

    }
}

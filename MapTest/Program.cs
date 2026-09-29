using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace MapTest
{
    class Program
    {

        public static string workingDir = Directory.GetCurrentDirectory();
        public static string confFile = Path.Combine(workingDir, "openjk.conf");

        public const string defaultGameDataPath = @"C:\Program Files (x86)\Steam\steamapps\common\Jedi Academy\GameData";
        public const string defaultLauncherEXE = @"C:\Program Files (x86)\MBII Launcher\MBIILauncher.exe";

        static void Main(string[] args)
        {

            string choice;
            int mapChoice = 0;
            string map;
            int i = 1;
            List<string> maps = new List<string>();

            string gameDataPath;
            string mbiiPath;
            string dedicatedEXE;
            string clientEXE;
            string launcherEXE;
            string serverConfig;

            string serverConfigData;

            Console.WriteLine("MBII Map Tester");
            Console.WriteLine("----------------------------");
            Console.WriteLine("MapTest.exe must be in the same folder as your maps.");
            Console.WriteLine("Each map needs its own folder containing the uncompressed map files (not a .pk3),");
            Console.WriteLine("named the same as the map, with the .bsp inside a maps folder, e.g.");
            Console.WriteLine(@"    mb2_mymap\maps\mb2_mymap.bsp");
            Console.WriteLine($"Looking for maps in: {workingDir}");
            Console.WriteLine("----------------------------");
            Console.WriteLine(" ");

            /* Conf file saves the GameData folder (line 1) and MBII Launcher exe (line 2) */
            string[] savedPaths = File.Exists(confFile) ? File.ReadAllLines(confFile) : new string[0];

            gameDataPath = ResolvePath(
                savedPaths.Length > 0 ? savedPaths[0] : null,
                defaultGameDataPath,
                IsGameDataFolder,
                "Please enter your Jedi Academy GameData folder",
                "That folder is not a Jedi Academy GameData folder with MBII installed (needs mbiided.x86.exe, mbii.x86.exe and an MBII folder)");

            launcherEXE = ResolvePath(
                savedPaths.Length > 1 ? savedPaths[1] : null,
                defaultLauncherEXE,
                path => File.Exists(path) && Path.GetFileName(path).Equals("MBIILauncher.exe", StringComparison.OrdinalIgnoreCase),
                "Please enter the full path to MBIILauncher.exe",
                "Unable to find MBIILauncher.exe at that path");

            File.WriteAllLines(confFile, new[] { gameDataPath, launcherEXE });

            /* Now some Checking */


            dedicatedEXE = Path.Combine(gameDataPath, "mbiided.x86.exe");
            clientEXE = Path.Combine(gameDataPath, "mbii.x86.exe");
            mbiiPath = Path.Combine(gameDataPath, "MBII");
            serverConfig = Path.Combine(mbiiPath, "server_config_default.cfg");


            if (!File.Exists(serverConfig))
            {
                Console.WriteLine($"Unable to find {serverConfig}");
                Console.ReadLine();
                Environment.Exit(0);
            }
            

            // Force MBMODE 2
            serverConfigData = File.ReadAllText(serverConfig);
            serverConfigData = serverConfigData.Replace("g_Authenticity \"0\"", "g_Authenticity \"2\"");
            File.WriteAllText(serverConfig, serverConfigData);

            Console.WriteLine("----------------------------");
            Console.WriteLine($"Jedi Academy GameData Folder: {gameDataPath}");
            Console.WriteLine($"MBII Launcher: {launcherEXE}");
            Console.WriteLine($"Dedicated Server Config File: {serverConfig}");
            Console.WriteLine($"Dedicated Server Exe: {dedicatedEXE}");
            Console.WriteLine($"Client Application: {clientEXE}");
            Console.WriteLine("----------------------------");
            Console.WriteLine(" ");
            Console.WriteLine("Available Custom Maps");
            Console.WriteLine("----------------------------");
            Console.WriteLine(" ");

            foreach (string folder in Directory.GetDirectories(workingDir))
            {


                if (!Path.GetFileName(folder).StartsWith("."))
                {
                    maps.Add(folder);
                    Console.WriteLine($"{i}. {Path.GetFileName(folder)}");
                    i++;
                }
                
            }

            if (maps.Count == 0)
            {
                Console.WriteLine($"No map folders found in {workingDir}");
                Console.WriteLine("Put MapTest.exe in the folder that contains your map folders and run it again");
                Console.ReadLine();
                Environment.Exit(0);
            }

            do
            {
                Console.Write("Please Choose Map: ");
                choice = Console.ReadLine();

                int.TryParse(choice, out mapChoice);

                if (mapChoice == 0  || mapChoice  > maps.Count)
                {
                    Console.WriteLine($"Invalid Selection");
                }

            }
            while (mapChoice == 0 || mapChoice > maps.Count);

            map = maps[mapChoice-1];

            Console.WriteLine($"Launching {Path.GetFileName(map)}");

            Console.WriteLine($"Creating PK3 For {Path.GetFileName(map)}");

            var finalDestination = Path.Combine(gameDataPath, "MBII", Path.GetFileName(map) + ".pk3");

            if (File.Exists(finalDestination))
            {
                File.Delete(finalDestination);
            }

            ZipFile.CreateFromDirectory(map, finalDestination) ;

            if (Directory.Exists(Path.GetDirectoryName(finalDestination)))
            {

        
                var clientCommand = clientEXE + " + set fs_game \"MBII\" +connect 127.0.0.1:29071";
                var serverCommand = dedicatedEXE + " +set dedicated 2 +set net_port 29071 +set fs_game \"MBII\" + exec \"server_config_default.cfg\" + set fs_direbeforepak \"1\" +set mbmode 2 +mbmode \"2\" +devmap \"{Path.GetFileName(map)}\"";

                Console.WriteLine("Following Commands will be run");
                Console.WriteLine($"Client Command: {clientCommand}");
                Console.WriteLine($"Server Command: {serverCommand}");
                Console.WriteLine("----------------------------");

                Thread.Sleep(2);

                EnsureLauncherRunning(launcherEXE);

                Console.WriteLine($"Launching Client");

                var clientThread = new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    System.Diagnostics.Process.Start(clientEXE, "+ set fs_game \"MBII\" +connect 127.0.0.1:29071");
                });

                clientThread.Start();

                Console.WriteLine($"Launching Dedicated Server");
             
                var startinfo = new ProcessStartInfo();
                startinfo.FileName = dedicatedEXE;
                startinfo.Arguments = $"+set dedicated 2 +set net_port 29071 +set fs_game \"MBII\" + exec \"server_config_default.cfg\" + set fs_direbeforepak \"1\" +set mbmode 2 +mbmode \"2\" +map \"{Path.GetFileName(map)}\"";

                var process = new Process();
                process.StartInfo = startinfo;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.UseShellExecute = false;

                process.Start();

                Thread.Sleep(20);

                Console.ReadLine();

            }
            else
            {
                Console.WriteLine($"Directory Not found: {finalDestination}");

            }

            Console.ReadLine();

        }

        static bool IsGameDataFolder(string path)
        {
            return File.Exists(Path.Combine(path, "mbiided.x86.exe"))
                && File.Exists(Path.Combine(path, "mbii.x86.exe"))
                && Directory.Exists(Path.Combine(path, "MBII"));
        }

        /* Use the saved path, then the default, otherwise keep asking until a valid path is entered */
        static string ResolvePath(string savedPath, string defaultPath, Func<string, bool> isValid, string prompt, string error)
        {
            if (!string.IsNullOrWhiteSpace(savedPath) && isValid(savedPath.Trim()))
            {
                return savedPath.Trim();
            }

            if (isValid(defaultPath))
            {
                return defaultPath;
            }

            while (true)
            {
                Console.WriteLine(prompt);
                Console.WriteLine("----------------------------");
                string path = (Console.ReadLine() ?? "").Trim().Trim('"');

                if (path.Length > 0 && isValid(path))
                {
                    return path;
                }

                Console.WriteLine(error);
            }
        }

        /* MBII anti-cheat requires the MBII Launcher to be open while the client runs */
        static void EnsureLauncherRunning(string launcherEXE)
        {
            if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(launcherEXE)).Length > 0)
            {
                Console.WriteLine("MBII Launcher is already running");
                return;
            }

            Console.WriteLine("Starting MBII Launcher");

            var startinfo = new ProcessStartInfo();
            startinfo.FileName = launcherEXE;
            startinfo.WorkingDirectory = Path.GetDirectoryName(launcherEXE);
            startinfo.UseShellExecute = true;

            Process.Start(startinfo);

            Console.WriteLine("Wait for the launcher to finish loading/updating, leave it open, then press Enter to continue");
            Console.ReadLine();
        }

        



    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace MapTest
{
    class Program
    {

        public static string workingDir = Directory.GetCurrentDirectory();
        public static string confFile = Path.Combine(workingDir, "openjk.conf");
        public static string logFile = Path.Combine(workingDir, "MapTest.log");

        public const string defaultGameDataPath = @"C:\Program Files (x86)\Steam\steamapps\common\Jedi Academy\GameData";
        public const string serverAddress = "127.0.0.1:29071";
        public const string defaultLauncherEXE = @"C:\Program Files (x86)\MBII Launcher\MBIILauncher.exe";

        static void Main(string[] args)
        {
            Log.Open(logFile);

            try
            {
                Run(args);
            }
            catch (Exception ex)
            {
                Log.Error("Unhandled error, MapTest has stopped", ex);
                Console.WriteLine();
                Console.WriteLine("Something went wrong:");
                Console.WriteLine($"    {ex.Message}");
                Console.WriteLine($"Full details have been written to {Log.FilePath}");
                Console.WriteLine("Please send that file to whoever is helping you. Press Enter to exit.");
                Console.ReadLine();
            }
            finally
            {
                Log.Info("MapTest exiting");
                Log.Close();
            }
        }

        static void Run(string[] args)
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
            Console.WriteLine($"Writing log to: {Log.FilePath}");
            Console.WriteLine("----------------------------");
            Console.WriteLine(" ");

            LogEnvironment(args);

            /* Conf file saves the GameData folder (line 1) and MBII Launcher exe (line 2) */
            string[] savedPaths = File.Exists(confFile) ? File.ReadAllLines(confFile) : new string[0];
            Log.Info($"Conf file {confFile} {(File.Exists(confFile) ? $"found with {savedPaths.Length} line(s)" : "not found")}");
            for (int line = 0; line < savedPaths.Length; line++)
            {
                Log.Info($"    conf line {line + 1}: {savedPaths[line]}");
            }

            gameDataPath = ResolvePath(
                "GameData folder",
                savedPaths.Length > 0 ? savedPaths[0] : null,
                defaultGameDataPath,
                GameDataProblem,
                "Please enter your Jedi Academy GameData folder",
                "That folder is not a Jedi Academy GameData folder with MBII installed (needs mbiided.x86.exe, mbii.x86.exe and an MBII folder)");

            launcherEXE = ResolvePath(
                "MBII Launcher",
                savedPaths.Length > 1 ? savedPaths[1] : null,
                defaultLauncherEXE,
                LauncherProblem,
                "Please enter the full path to MBIILauncher.exe",
                "Unable to find MBIILauncher.exe at that path");

            File.WriteAllLines(confFile, new[] { gameDataPath, launcherEXE });
            Log.Info($"Saved paths to {confFile}");

            /* Now some Checking */


            dedicatedEXE = Path.Combine(gameDataPath, "mbiided.x86.exe");
            clientEXE = Path.Combine(gameDataPath, "mbii.x86.exe");
            mbiiPath = Path.Combine(gameDataPath, "MBII");
            serverConfig = Path.Combine(mbiiPath, "server_config_default.cfg");

            LogFile("Dedicated server exe", dedicatedEXE);
            LogFile("Client exe", clientEXE);
            LogFile("MBII Launcher exe", launcherEXE);
            LogFile("Server config", serverConfig);

            if (!File.Exists(serverConfig))
            {
                Log.Error($"Unable to find server config {serverConfig}. Contents of {mbiiPath}: {ListFolder(mbiiPath)}");
                Console.WriteLine($"Unable to find {serverConfig}");
                Console.WriteLine($"See {Log.FilePath} for details");
                Console.ReadLine();
                Environment.Exit(0);
            }


            // Force MBMODE 2
            serverConfigData = File.ReadAllText(serverConfig);
            if (serverConfigData.Contains("g_Authenticity \"0\""))
            {
                serverConfigData = serverConfigData.Replace("g_Authenticity \"0\"", "g_Authenticity \"2\"");
                File.WriteAllText(serverConfig, serverConfigData);
                Log.Info("Changed g_Authenticity \"0\" to \"2\" in server config");
            }
            else
            {
                Log.Info($"g_Authenticity \"0\" not found in server config, left unchanged (g_Authenticity lines: {FindLines(serverConfigData, "g_Authenticity")})");
            }

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

            Log.Info($"Scanning {workingDir} for map folders");

            foreach (string folder in Directory.GetDirectories(workingDir))
            {


                if (!Path.GetFileName(folder).StartsWith("."))
                {
                    maps.Add(folder);
                    Console.WriteLine($"{i}. {Path.GetFileName(folder)}");
                    Log.Info($"    {i}. {Path.GetFileName(folder)}");
                    i++;
                }
                else
                {
                    Log.Info($"    skipped hidden folder {Path.GetFileName(folder)}");
                }

            }

            if (maps.Count == 0)
            {
                Log.Error($"No map folders found in {workingDir}. Contents: {ListFolder(workingDir)}");
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
                    Log.Info($"Invalid map selection entered: '{choice}'");
                }

            }
            while (mapChoice == 0 || mapChoice > maps.Count);

            map = maps[mapChoice-1];
            string mapName = Path.GetFileName(map);

            Log.Info($"Selected map {mapChoice}: {mapName} ({map})");
            CheckMapLayout(map, mapName);

            Console.WriteLine($"Launching {mapName}");

            Console.WriteLine($"Creating PK3 For {mapName}");

            var finalDestination = Path.Combine(gameDataPath, "MBII", mapName + ".pk3");

            if (File.Exists(finalDestination))
            {
                Log.Info($"Deleting existing {finalDestination}");
                File.Delete(finalDestination);
            }

            Log.Info($"Zipping {map} to {finalDestination}");
            ZipFile.CreateFromDirectory(map, finalDestination) ;
            LogPk3(finalDestination);

            if (Directory.Exists(Path.GetDirectoryName(finalDestination)))
            {


                var serverArgs = $"+set dedicated 2 +set net_port 29071 +set fs_game \"MBII\" + exec \"server_config_default.cfg\" + set fs_direbeforepak \"1\" +set mbmode 2 +mbmode \"2\" +map \"{mapName}\"";

                Console.WriteLine("Following Command will be run");
                Console.WriteLine($"Server Command: \"{dedicatedEXE}\" {serverArgs}");
                Console.WriteLine("----------------------------");

                Console.WriteLine($"Launching Dedicated Server");

                var startinfo = new ProcessStartInfo();
                startinfo.FileName = dedicatedEXE;
                startinfo.Arguments = serverArgs;
                startinfo.RedirectStandardOutput = false;
                startinfo.RedirectStandardInput = true;
                startinfo.UseShellExecute = false;

                Process server = null;
                try
                {
                    server = StartProcess("Dedicated server", startinfo);
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to start the dedicated server", ex);
                    Console.WriteLine($"Failed to start the dedicated server: {ex.Message}");
                }

                /* Give it a few seconds, then record whether it has already died */
                Thread.Sleep(5000);
                CheckStillRunning("Dedicated server", server);

                Console.WriteLine(" ");
                Console.WriteLine("----------------------------");

                if (server == null || server.HasExited)
                {
                    Console.WriteLine("The dedicated server is not running, check the log for details");
                    Console.WriteLine($"Log written to {Log.FilePath}");
                    Console.ReadLine();
                    Environment.Exit(0);
                }

                Console.WriteLine($"Server is running {mapName}");
                Console.WriteLine("----------------------------");

                /* MBII anti-cheat rejects a client started by anything other than the launcher,
                   so the launcher is opened with +connect and the user presses Play */
                bool autoConnect = EnsureLauncherRunning(launcherEXE, $"+connect {serverAddress}");

                Console.WriteLine(" ");
                Console.WriteLine("----------------------------");
                Console.WriteLine("To join:");
                Console.WriteLine("  1. Press Play in the MBII Launcher");

                if (autoConnect)
                {
                    Console.WriteLine("  2. The game should join automatically. If it doesn't, open the console (Shift + ~) and enter:");
                }
                else
                {
                    Console.WriteLine("  2. Open the console (Shift + ~) and enter:");
                }

                Console.WriteLine($"       /connect {serverAddress}");
                Console.WriteLine("Keep this window and the launcher open while testing.");
                Console.WriteLine("----------------------------");
                Console.WriteLine($"Log written to {Log.FilePath}");

                Console.ReadLine();

                CheckStillRunning("Dedicated server", server);

            }
            else
            {
                Console.WriteLine($"Directory Not found: {finalDestination}");
                Log.Error($"Directory Not found: {Path.GetDirectoryName(finalDestination)}");

            }

            Console.ReadLine();

        }

        /* Returns null when the folder is a usable GameData folder, otherwise why it isn't */
        static string GameDataProblem(string path)
        {
            if (!Directory.Exists(path))
            {
                return "folder does not exist";
            }

            var missing = new List<string>();
            if (!File.Exists(Path.Combine(path, "mbiided.x86.exe"))) missing.Add("mbiided.x86.exe");
            if (!File.Exists(Path.Combine(path, "mbii.x86.exe"))) missing.Add("mbii.x86.exe");
            if (!Directory.Exists(Path.Combine(path, "MBII"))) missing.Add(@"MBII\ folder");

            return missing.Count == 0 ? null : $"missing {string.Join(", ", missing)} (folder contains: {ListFolder(path)})";
        }

        static string LauncherProblem(string path)
        {
            if (!Path.GetFileName(path).Equals("MBIILauncher.exe", StringComparison.OrdinalIgnoreCase))
            {
                return "file name is not MBIILauncher.exe";
            }

            return File.Exists(path) ? null : "file does not exist";
        }

        /* Use the saved path, then the default, otherwise keep asking until a valid path is entered */
        static string ResolvePath(string name, string savedPath, string defaultPath, Func<string, string> problem, string prompt, string error)
        {
            string why;

            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                why = problem(savedPath.Trim());
                if (why == null)
                {
                    Log.Info($"{name}: using saved path {savedPath.Trim()}");
                    return savedPath.Trim();
                }
                Log.Warn($"{name}: saved path {savedPath.Trim()} rejected, {why}");
            }
            else
            {
                Log.Info($"{name}: no saved path");
            }

            why = problem(defaultPath);
            if (why == null)
            {
                Log.Info($"{name}: using default path {defaultPath}");
                return defaultPath;
            }
            Log.Warn($"{name}: default path {defaultPath} rejected, {why}");

            while (true)
            {
                Console.WriteLine(prompt);
                Console.WriteLine("----------------------------");
                string path = (Console.ReadLine() ?? "").Trim().Trim('"');

                why = path.Length == 0 ? "nothing entered" : problem(path);
                if (why == null)
                {
                    Log.Info($"{name}: using entered path {path}");
                    return path;
                }

                Log.Warn($"{name}: entered path '{path}' rejected, {why}");
                Console.WriteLine(error);
            }
        }

        /* MBII anti-cheat requires the MBII Launcher to be open while the client runs */
        /* Returns true if the launcher was started by us with launcherArgs */
        static bool EnsureLauncherRunning(string launcherEXE, string launcherArgs)
        {
            string launcherName = Path.GetFileNameWithoutExtension(launcherEXE);
            var running = Process.GetProcessesByName(launcherName);

            if (running.Length > 0)
            {
                Log.Info($"MBII Launcher already running (PID {string.Join(", ", running.Select(p => p.Id))})");
                Console.WriteLine("MBII Launcher is already running, so it can't be told to join the test server automatically.");
                Console.WriteLine("Close the launcher and press Enter to have MapTest reopen it with auto-connect,");
                Console.WriteLine("or just press Enter to keep it open and connect manually");
                Console.ReadLine();

                running = Process.GetProcessesByName(launcherName);
                if (running.Length > 0)
                {
                    Log.Info("MBII Launcher left open, user will connect manually");
                    return false;
                }
            }

            Console.WriteLine($"Starting MBII Launcher with {launcherArgs}");

            var startinfo = new ProcessStartInfo();
            startinfo.FileName = launcherEXE;
            startinfo.Arguments = launcherArgs;
            startinfo.WorkingDirectory = Path.GetDirectoryName(launcherEXE);
            startinfo.UseShellExecute = true;

            try
            {
                StartProcess("MBII Launcher", startinfo);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to start the MBII Launcher", ex);
                Console.WriteLine($"Failed to start the MBII Launcher: {ex.Message}");
                Console.WriteLine("Start it yourself, leave it open, then press Enter to continue");
                Console.ReadLine();
                return false;
            }

            Console.WriteLine("Wait for the launcher to finish loading/updating, leave it open, then press Enter to continue");
            Console.ReadLine();

            running = Process.GetProcessesByName(launcherName);
            if (running.Length > 0)
            {
                Log.Info($"MBII Launcher is running (PID {string.Join(", ", running.Select(p => p.Id))})");
            }
            else
            {
                Log.Warn("MBII Launcher is not running after the user pressed Enter, the client may fail anti-cheat");
            }

            return true;
        }

        static Process StartProcess(string name, ProcessStartInfo startinfo)
        {
            Log.Info($"Starting {name}");
            Log.Info($"    FileName:         {startinfo.FileName}");
            Log.Info($"    Arguments:        {startinfo.Arguments}");
            Log.Info($"    WorkingDirectory: {(string.IsNullOrEmpty(startinfo.WorkingDirectory) ? $"(not set, inherits {Directory.GetCurrentDirectory()})" : startinfo.WorkingDirectory)}");
            Log.Info($"    UseShellExecute:  {startinfo.UseShellExecute}");

            var process = Process.Start(startinfo);

            if (process == null)
            {
                Log.Warn($"{name}: Process.Start returned no process (it may have handed off to an existing instance)");
                return null;
            }

            Log.Info($"{name} started, PID {process.Id}");
            return process;
        }

        static void CheckStillRunning(string name, Process process)
        {
            if (process == null)
            {
                Log.Warn($"{name}: no process to check (it did not start)");
                return;
            }

            try
            {
                if (process.HasExited)
                {
                    Log.Error($"{name} (PID {process.Id}) has exited with code {process.ExitCode} (0x{process.ExitCode:X8}) at {process.ExitTime:HH:mm:ss}, it ran for {(process.ExitTime - process.StartTime).TotalSeconds:0.0}s");
                    Console.WriteLine($"{name} has closed (exit code {process.ExitCode}), see {Log.FilePath}");
                }
                else
                {
                    Log.Info($"{name} (PID {process.Id}) is still running");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"{name}: unable to check process state, {ex.Message}");
            }
        }

        /* Warn about the common layout mistakes, the game won't find the map if the bsp isn't at maps\<name>.bsp */
        static void CheckMapLayout(string map, string mapName)
        {
            try
            {
                var files = Directory.GetFiles(map, "*", SearchOption.AllDirectories);
                Log.Info($"Map folder contains {files.Length} file(s), {files.Sum(f => new FileInfo(f).Length):N0} bytes");
                foreach (var f in files.Take(200))
                {
                    Log.Info($"    {Path.GetRelativePath(map, f)}");
                }
                if (files.Length > 200)
                {
                    Log.Info($"    ... and {files.Length - 200} more");
                }

                var expectedBsp = Path.Combine(map, "maps", mapName + ".bsp");
                if (File.Exists(expectedBsp))
                {
                    Log.Info($"Found expected bsp {expectedBsp}");
                    return;
                }

                var bsps = files.Where(f => f.EndsWith(".bsp", StringComparison.OrdinalIgnoreCase)).ToList();
                var pk3s = files.Where(f => f.EndsWith(".pk3", StringComparison.OrdinalIgnoreCase)).ToList();

                Log.Warn($"Expected bsp not found at {expectedBsp}");
                if (bsps.Count > 0)
                {
                    Log.Warn($"bsp file(s) found instead: {string.Join(", ", bsps.Select(f => Path.GetRelativePath(map, f)))}");
                }
                if (pk3s.Count > 0)
                {
                    Log.Warn($"pk3 file(s) found inside the map folder, these must be extracted: {string.Join(", ", pk3s.Select(f => Path.GetRelativePath(map, f)))}");
                }

                Console.WriteLine($"WARNING: {Path.Combine(mapName, "maps", mapName + ".bsp")} not found, the map will probably fail to load");
            }
            catch (Exception ex)
            {
                Log.Warn($"Unable to inspect map folder {map}, {ex.Message}");
            }
        }

        static void LogPk3(string pk3)
        {
            try
            {
                using var zip = ZipFile.OpenRead(pk3);
                Log.Info($"Created {pk3}, {new FileInfo(pk3).Length:N0} bytes, {zip.Entries.Count} entries");
            }
            catch (Exception ex)
            {
                Log.Warn($"Unable to read back {pk3}, {ex.Message}");
            }
        }

        static void LogEnvironment(string[] args)
        {
            Log.Info($"MapTest version {Assembly.GetExecutingAssembly().GetName().Version}");
            Log.Info($"Started {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
            Log.Info($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture}), process {RuntimeInformation.ProcessArchitecture}");
            Log.Info($".NET: {RuntimeInformation.FrameworkDescription}");
            Log.Info($"Exe path: {Environment.ProcessPath}");
            Log.Info($"Working directory: {workingDir}");
            Log.Info($"Arguments: {(args.Length == 0 ? "(none)" : string.Join(" ", args))}");
        }

        static void LogFile(string name, string path)
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                var version = FileVersionInfo.GetVersionInfo(path).FileVersion;
                Log.Info($"{name}: {path} ({info.Length:N0} bytes, modified {info.LastWriteTime:yyyy-MM-dd HH:mm}{(string.IsNullOrEmpty(version) ? "" : $", version {version}")})");
            }
            else
            {
                Log.Warn($"{name}: {path} does not exist");
            }
        }

        static string ListFolder(string path)
        {
            try
            {
                var entries = Directory.GetFileSystemEntries(path).Select(Path.GetFileName).ToList();
                if (entries.Count == 0) return "(empty)";
                var shown = string.Join(", ", entries.Take(50));
                return entries.Count > 50 ? $"{shown}, ... and {entries.Count - 50} more" : shown;
            }
            catch (Exception ex)
            {
                return $"(unable to list: {ex.Message})";
            }
        }

        static string FindLines(string text, string search)
        {
            var lines = text.Split('\n').Where(l => l.Contains(search, StringComparison.OrdinalIgnoreCase)).Select(l => l.Trim()).ToList();
            return lines.Count == 0 ? "(none)" : string.Join(" | ", lines);
        }

    }

    /* Writes a timestamped MapTest.log next to the maps so failures can be diagnosed afterwards */
    static class Log
    {
        static StreamWriter writer;
        static readonly object sync = new object();

        public static string FilePath { get; private set; }

        public static void Open(string path)
        {
            try
            {
                writer = new StreamWriter(path, false) { AutoFlush = true };
                FilePath = path;
            }
            catch
            {
                /* Folder not writable, fall back to the temp folder */
                FilePath = Path.Combine(Path.GetTempPath(), "MapTest.log");
                try
                {
                    writer = new StreamWriter(FilePath, false) { AutoFlush = true };
                }
                catch
                {
                    FilePath = "(unable to create log file)";
                }
            }
        }

        public static void Close()
        {
            lock (sync)
            {
                writer?.Dispose();
                writer = null;
            }
        }

        public static void Info(string message) => Write("INFO ", message);
        public static void Warn(string message) => Write("WARN ", message);
        public static void Error(string message) => Write("ERROR", message);
        public static void Error(string message, Exception ex) => Write("ERROR", $"{message}{Environment.NewLine}{ex}");

        static void Write(string level, string message)
        {
            lock (sync)
            {
                writer?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {level} {message}");
            }
        }
    }
}

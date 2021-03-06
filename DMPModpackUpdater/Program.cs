﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace DMPModpackUpdater
{
    class MainClass
    {
        private static Dictionary<string, string> serverFiles = new Dictionary<string, string>();
        private static List<string> excludeList = new List<string>();
        private static List<string> containsExcludeList = new List<string>();
        private static List<string> kspList = new List<string>();
        private static string kspPath;
        private static string serverIndex;
        private static string cachePath;
        private static string gamedataPath;
        private static int checkedFiles;
        private static bool enableDelete = false;
        private static bool enableRun = true;
        private static bool enableStock = false;
        private static string kspArgs = "";
        private static string runFile = null;

        public static void Main(string[] args)
        {
            foreach (string arg in args)
            {
                if (arg == "/?")
                {
                    ShowHelp();
                    return;
                }
                if (arg == "--help")
                {
                    ShowHelp();
                    return;
                }
                if (arg == "--delete")
                {
                    enableDelete = true;
                }
                if (arg == "--stock")
                {
                    enableStock = true;
                    enableDelete = true;
                }
                if (arg == "--no-run")
                {
                    enableRun = false;
                }
                if (arg.StartsWith("--run=", StringComparison.Ordinal))
                {
                    runFile = arg.Substring("--run=".Length);
                }
                if (arg.StartsWith("--ksp-path=", StringComparison.Ordinal))
                {
                    kspPath = arg.Substring("--ksp-path=".Length);
                }
                if (arg.StartsWith("--ksp-args=", StringComparison.Ordinal))
                {
                    kspArgs = arg.Substring("--ksp-args=".Length);
                }
            }
            if (kspPath == null)
            {
                kspPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
            cachePath = Path.Combine(kspPath, "DarkMultiPlayer-ModCache");
            gamedataPath = Path.Combine(kspPath, "GameData");
            serverIndex = Path.Combine(kspPath, "DarkMultiPlayer-Server-GameData.txt");
            excludeList.Add("darkmultiplayer/");
            excludeList.Add("squad/");
            excludeList.Add("squadexpansion/");
            containsExcludeList.Add(".log");
            containsExcludeList.Add("modulemanager.configcache");
            containsExcludeList.Add("modulemanager.configsha");
            containsExcludeList.Add("modulemanager.physics");
            containsExcludeList.Add("modulemanager.techtree");
            kspList.Add("KSP.x86_64");
            kspList.Add("KSP.x86");
            kspList.Add("KSP_x64.exe");
            kspList.Add("KSP.exe");

            int syncError = LoadServerCache();
            if (syncError == 0)
            {
                Console.WriteLine("Checking files...");
                if (enableDelete)
                {
                    DeleteFromGameData();
                }
                AddMissingToGameData();
                if (enableRun)
                {
                    RunKSP();
                }
            }
            else
            {
                switch (syncError)
                {
                    case 1:
                        Console.WriteLine("Please place DMPModpackUpdater in the KSP folder");
                        break;
                    case 2:
                        Console.WriteLine("Please connect to a DMP Server with a modpackMode=GAMEDATA setting.");
                        break;
                    case 3:
                        Console.WriteLine("Missing files needed for DMP Server mod pack. Please tell your server admin.");
                        break;
                }
                Console.WriteLine();
                ShowHelp();
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Command line arguments:");
            Console.WriteLine("--stock: Reverts GameData to stock, implies --delete");
            Console.WriteLine("--delete: Delete mods that are not on the server, instead of only updating or adding");
            Console.WriteLine("--no-run: Do not attempt to start KSP");
            Console.WriteLine("--run=[ProgramName.exe]: Runs this program instead of trying to find and start KSP.");
            Console.WriteLine("--ksp-path=[path]: Run in specified folder, rather than the program location");
            Console.WriteLine("--ksp-args=\"args\": Run KSP with these arguments, example: --ksp-args=\"-force-d3d11 -popupwindow -dmp dmp://localhost:6702\"");
            Console.WriteLine("--help: Displays this message");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }


        private static int LoadServerCache()
        {
            if (enableStock)
            {
                return 0;
            }
            if (!Directory.Exists(gamedataPath))
            {
                return 1;
            }
            if (!File.Exists(serverIndex))
            {
                return 2;
            }

            bool missingFiles = false;
            using (StreamReader sr = new StreamReader(serverIndex))
            {
                string currentLine = null;
                while ((currentLine = sr.ReadLine()) != null)
                {
                    int splitPos = currentLine.LastIndexOf("=", StringComparison.Ordinal);
                    string filePath = currentLine.Substring(0, splitPos);
                    bool skipFile = false;
                    foreach (string ignoreString in excludeList)
                    {
                        if (filePath.ToLower().StartsWith(ignoreString, StringComparison.Ordinal))
                        {
                            //Console.WriteLine("Skipping ignored file: " + filePath);
                            skipFile = true;
                        }
                    }
                    foreach (string ignoreString in containsExcludeList)
                    {
                        if (filePath.ToLower().Contains(ignoreString))
                        {
                            //Console.WriteLine("Skipping ignored file: " + filePath);
                            skipFile = true;
                        }
                    }
                    if (skipFile)
                    {
                        continue;
                    }
                    string sha256 = currentLine.Substring(splitPos + 1);
                    string fileCachePath = Path.Combine(cachePath, sha256 + ".bin");
                    if (!File.Exists(fileCachePath))
                    {
                        Console.WriteLine("Missing file: " + filePath);
                        missingFiles = true;
                    }
                    serverFiles.Add(filePath, sha256);
                }
            }
            if (missingFiles)
            {
                return 3;
            }
            return 0;
        }

        private static void DeleteFromGameData()
        {
            string[] modFiles = Directory.GetFiles(gamedataPath, "*", SearchOption.AllDirectories);
            foreach (string filePath in modFiles)
            {
                string trimmedPath = filePath.Substring(gamedataPath.Length + 1).Replace('\\', '/');
                bool skipFile = false;
                foreach (string ignoreString in excludeList)
                {
                    if (trimmedPath.ToLower().StartsWith(ignoreString, StringComparison.Ordinal))
                    {
                        //Console.WriteLine("Skipping ignored file: " + filePath);
                        skipFile = true;
                    }
                }
                foreach (string ignoreString in containsExcludeList)
                {
                    if (trimmedPath.ToLower().Contains(ignoreString))
                    {
                        //Console.WriteLine("Skipping ignored file: " + filePath);
                        skipFile = true;
                    }
                }
                if (skipFile)
                {
                    continue;
                }
                if (!filePath.ToLower().StartsWith(gamedataPath.ToLower(), StringComparison.Ordinal))
                {
                    //Console.WriteLine("Not touching " + filePath + " as it is outside of GameData (symlink?)");
                    continue;
                }
                if (!serverFiles.ContainsKey(trimmedPath))
                {
                    Console.WriteLine("Deleting " + trimmedPath + ", not in modpack");
                    File.Delete(filePath);
                }
            }
            string[] modDirs = Directory.GetDirectories(gamedataPath, "*", SearchOption.AllDirectories);
            foreach (string dirPath in modDirs)
            {
                string trimmedPath = dirPath.Substring(gamedataPath.Length + 1).Replace('\\', '/');
                bool skipDir = false;
                foreach (string ignoreString in excludeList)
                {
                    if (trimmedPath.ToLower().StartsWith(ignoreString, StringComparison.Ordinal))
                    {
                        //Console.WriteLine("Skipping ignored file: " + trimmedPath);
                        skipDir = true;
                    }
                }
                foreach (string ignoreString in containsExcludeList)
                {
                    if (trimmedPath.ToLower().Contains(ignoreString))
                    {
                        //Console.WriteLine("Skipping ignored file: " + trimmedPath);
                        skipDir = true;
                    }
                }
                if (skipDir)
                {
                    continue;
                }
                if (Directory.Exists(dirPath))
                {
                    if (Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories).Length == 0)
                    {
                        Directory.Delete(dirPath, true);
                    }
                }
            }
        }

        private static void AddMissingToGameData()
        {
            foreach (KeyValuePair<string, string> kvp in serverFiles)
            {
                string fileCachePath = Path.Combine(cachePath, kvp.Value + ".bin");
                string filePath = Path.Combine(gamedataPath, kvp.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                bool isUpdate = false;
                if (File.Exists(filePath))
                {
                    isUpdate = true;
                    File.Delete(filePath);
                }
                File.Copy(fileCachePath, filePath);
                if (isUpdate)
                {
                    //Console.WriteLine("Updated/Checked " + kvp.Key);
                    checkedFiles++;
                }
                else
                {
                    Console.WriteLine("Added " + kvp.Key);
                }
            }
            Console.WriteLine("Updated/Checked " + checkedFiles + " existing files.");
        }

        private static void RunKSP()
        {
            if (runFile != null)
            {
                string filePath = Path.Combine(kspPath, runFile);
                if (File.Exists(filePath))
                {
                    StartProcess(filePath, kspArgs);
                }
                else
                {
                    Console.WriteLine("File does not exist:" + filePath);
                }
            }
            else
            {
                foreach (string testFile in kspList)
                {
                    string filePath = Path.Combine(kspPath, testFile);
                    if (File.Exists(filePath))
                    {
                        Console.WriteLine("Starting:" + testFile);
                        StartProcess(filePath, kspArgs);
                        return;
                    }
                    Console.WriteLine("KSP executable not found");
                }
            }
        }

        private static void StartProcess(string fileName, string args)
        {
            Process.Start(fileName, args);
        }
    }
}

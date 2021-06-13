﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oculus.API;
using ComputerUtils.GraphQL;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Net;
using System.IO.Compression;

namespace RIFT_Downgrader
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the Rift downgrader. Navigate the program by typing the number corresponding to your action and hitting enter. You can always cancle a action by closing the program.");
            DowngradeManager m = new DowngradeManager();
            m.Menu();
        }
    }

    public class DowngradeManager
    {
        public static string exe = AppDomain.CurrentDomain.BaseDirectory;
        public static string RiftBSAppId = "1304877726278670";
        public static string RiftPolygonNightmareAppId = "1333056616777885";
        public static string access_token = "";
        public static string version = "1.0";
        public static Config config = Config.LoadConfig();
        public void Menu()
        {
            SetupProgram();
            while (true)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("[1] Downgrade Beat Saber");
                Console.WriteLine("[2] Downgrade another Rift app");
                Console.WriteLine("[3] Launch App");
                Console.WriteLine("[4] Update access_token");
                Console.WriteLine("[5] Exit");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("Choice: ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                switch (Console.ReadLine())
                {
                    case "1":
                        ShowVersions(RiftBSAppId);
                        //ShowVersions(RiftPolygonNightmareAppId);
                        break;
                    case "2":
                        StoreSearch();
                        break;
                    case "3":
                        LaunchApp();
                        break;
                    case "4":
                        UpdateAccessToken();
                        break;
                    case "5":
                        System.Environment.Exit(0);
                        break;
                }
            }
        }

        public void LaunchApp()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Console.WriteLine("Downloaded apps");
            Dictionary<string, App> nameApp = new Dictionary<string, App>();
            foreach(App a in config.apps)
            {
                nameApp.Add(a.name.ToLower(), a);
                Console.WriteLine(a.name);
            }
            Console.WriteLine();
            bool choosen = false;
            string sel = "";
            while (!choosen)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("Which app do you want to launch: ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                sel = Console.ReadLine();
                if (nameApp.ContainsKey(sel.ToLower()))
                {
                    choosen = true;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("That app is not downloaded. Please type the full name displayed above.");
                }
            }
            App selected = nameApp[sel.ToLower()];
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Downloaded versions for " + selected.name);

            Dictionary<string, ReleaseChannelReleaseBinary> versionBinary = new Dictionary<string, ReleaseChannelReleaseBinary>();
            foreach (ReleaseChannelReleaseBinary b in selected.versions)
            {
                bool exists = false;
                foreach (ReleaseChannelReleaseBinary e in selected.versions)
                {
                    if (e.version == b.version && e.version_code != b.version_code)
                    {
                        exists = true;
                        break;
                    }
                }
                string displayName = b.version + (exists ? " " + b.version_code : "");
                versionBinary.Add(displayName, b);
                DateTime t = UnixTimeStampToDateTime(b.created_date);
                Console.WriteLine(t.Day.ToString("D2") + "." + t.Month.ToString("D2") + "." + t.Year + "     " + displayName);
            }
            choosen = false;
            string ver = "";
            while (!choosen)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine();
                Console.Write("Which version do you want?: ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                ver = Console.ReadLine();
                if (!versionBinary.ContainsKey(ver))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("This version does not exist.");
                }
                else
                {
                    choosen = true;
                }
            }
            ReleaseChannelReleaseBinary selectedVersion = versionBinary[ver];

            Console.WriteLine("Loading manifest");
            string baseDirectory = exe + "apps\\" + selected.id + "\\" + selectedVersion.id + "\\";
            Manifest manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(baseDirectory + "manifest.json"));
            Console.WriteLine("Launching");
            Process.Start(baseDirectory + manifest.launchFile);
        }

        public void StoreSearch()
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine();
            Console.Write("Search term: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            string term = Console.ReadLine();
            GraphQLClient cl = GraphQLClient.StoreSearch(term);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Requesting results");
            StoreSearchResultSkeleton s = JsonSerializer.Deserialize<StoreSearchResultSkeleton>(cl.Request());
            Console.WriteLine();
            Console.WriteLine("Results: ");
            Console.WriteLine();
            Dictionary<string, string> nameId = new Dictionary<string, string>();
            foreach(StoreSearchResultCategory c in s.data.viewer.contextual_search.all_category_results)
            {
                if(c.name == "APPS")
                {
                    foreach(StoreSearchSearchResult r in c.search_results.nodes)
                    {
                        Console.WriteLine(r.target_object.display_name);
                        if(r.target_object.display_name.ToLower() == term.ToLower())
                        {
                            Console.WriteLine("Result is exact match. Auto selecting");
                            ShowVersions(r.target_object.id);
                            return;
                        }
                        nameId.Add(r.target_object.display_name.ToLower(), r.target_object.id);
                    }
                }
            }
            Console.WriteLine();
            bool choosen = false;
            string sel = "";
            while(!choosen)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("App Name: ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                sel = Console.ReadLine();
                if(nameId.ContainsKey(sel.ToLower()))
                {
                    choosen = true;
                } else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("That app does not exist in the results. Please type the full name displayed above.");
                }
            }
            ShowVersions(nameId[sel.ToLower()]);
        }

        public void ShowVersions(string appId)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Console.WriteLine("Fetching release channels");
            GraphQLClient client = GraphQLClient.ReleaseChannels(appId);
            ReleaseChannelSkeleton channels = JsonSerializer.Deserialize<ReleaseChannelSkeleton>(client.Request());
            List<ReleaseChannelReleaseBinary> versions = new List<ReleaseChannelReleaseBinary>();
            string appName = "";
            foreach(ReleaseChannel c in channels.data.node.release_channels.nodes)
            {
                Console.WriteLine("Fetching versions in " + c.channel_name);
                client = GraphQLClient.ReleaseChannelReleases(c.id);
                ReleaseChannelReleasesSkeleton s = JsonSerializer.Deserialize<ReleaseChannelReleasesSkeleton>(client.Request());
                appName = s.data.node.latest_supported_binary.binary_application.display_name;
                foreach (ReleaseChannelReleaseBinaryNode b in s.data.node.binaries.edges)
                {
                    bool exists = false;
                    foreach (ReleaseChannelReleaseBinary e in versions)
                    {
                        if (e.id == b.node.id)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists) versions.Add(b.node);
                }
            }
            Console.WriteLine("Date is in format DD-MM-YYYY");
            Console.WriteLine("Versions for " + appName);
            Console.WriteLine();
            versions = versions.OrderBy(b => b.created_date).ToList<ReleaseChannelReleaseBinary>();
            Dictionary<string, ReleaseChannelReleaseBinary> versionBinary = new Dictionary<string, ReleaseChannelReleaseBinary>();
            foreach(ReleaseChannelReleaseBinary b in versions)
            {
                bool exists = false;
                foreach (ReleaseChannelReleaseBinary e in versions)
                {
                    if(e.version == b.version && e.version_code != b.version_code)
                    {
                        exists = true;
                        break;
                    }
                }
                string displayName = b.version + (exists ? " " + b.version_code : "");
                versionBinary.Add(displayName, b);
                DateTime t = UnixTimeStampToDateTime(b.created_date);
                Console.WriteLine(t.Day.ToString("D2") + "." + t.Month.ToString("D2") + "." + t.Year + "     " + displayName);
            }
            bool choosen = false;
            string ver = "";
            while(!choosen)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine();
                Console.Write("Which version do you want?: ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                ver = Console.ReadLine();
                if(!versionBinary.ContainsKey(ver))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("This version does not exist.");
                } else
                {
                    choosen = true;
                }
            }
            ReleaseChannelReleaseBinary selected = versionBinary[ver];
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Console.WriteLine(selected.ToString());
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("Do you want to download this version? (Y/n): ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            string choice = Console.ReadLine();
            if (choice.ToLower() == "y" || choice == "")
            {
                if(Directory.Exists(exe + "apps\\" + appId + "\\" + selected.id))
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write("Seems like you already have the version " + selected.version + " installed. Do you want to download it again? (y/N): ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    choice = Console.ReadLine();
                    if(choice.ToLower() != "y")
                    {
                        return;
                    }
                    RecreateDirectoryIfExisting(exe + "apps\\" + appId + "\\" + selected.id);
                }
                StartDownload(selected, appId, appName);
            } else
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Downgrading aborted");
            }
        }

        public string ByteSizeToString(long input, int decimals = 2)
        {
            // TB
            if (input > 1099511627776)
            {
                return (input / 1099511627776).ToString("D" + decimals) + " TB";
            }
            // GB
            else if (input > 1073741824)
            {
                return (input / 1073741824).ToString("D" + decimals) + " GB";
            }
            // MB
            else if (input > 1048576)
            {
                return (input / 1048576).ToString("D" + decimals) + " MB";
            }
            // KB
            else if (input > 1024)
            {
                return (input / 1024).ToString("D" + decimals) + " KB";
            }
            // Bytes
            else
            {
                return input + " Bytes";
            }
        }

        public void StartDownload(ReleaseChannelReleaseBinary binary, string appId, string appName)
        {
            Console.ForegroundColor = ConsoleColor.White;
            if (!UpdateAccessToken(true))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Valid acces token is needed to proceed. Aborting.");
                return;
            }
            WebClient downloader = new WebClient();
            if (!File.Exists(exe + "ovr-platform-util.exe"))
            {
                
                Console.WriteLine("Downloading ovr-platform-util.exe from Oculus");
                downloader.DownloadFile("https://securecdn.oculus.com/binaries/download/?id=3606802009426978&access_token=OC|1196467420370658|", exe + "ovr-platform-util.exe");
                Console.WriteLine("Downloaded");
            }
            
            Console.WriteLine();
            Console.WriteLine("Downloading manifest");
            string baseDirectory = exe + "apps\\" + appId + "\\" + binary.id + "\\";
            string baseDownloadLink = "https://securecdn.oculus.com/binaries/download/?id=" + binary.id + "&access_token=" + config.access_token + "&get_";
            Directory.CreateDirectory(baseDirectory);
            try
            {
                downloader.DownloadFile(baseDownloadLink + "manifest=1", baseDirectory + "manifest.zip");
            } catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine("Download of manifest failed. Do you own this game? If you do then please update your access token in case it's expired");
                return;
            }
            ZipFile.ExtractToDirectory(baseDirectory + "manifest.zip", baseDirectory);
            Manifest manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(baseDirectory + "manifest.json"));
            Console.WriteLine();
            Console.WriteLine("Starting download of " + appName + " via ovr-platform-util");
            Process p = Process.Start(exe + "ovr-platform-util.exe", "download-rift-build -b " + binary.id + " -d \"" + baseDirectory.Substring(0, baseDirectory.Length - 1) + "\" -t " + config.access_token);
            p.WaitForExit();
            int i = 0;
            int suceeded = 0;
            Console.WriteLine("Validating if all files exist");
            Console.WriteLine(baseDirectory);
            foreach (KeyValuePair<string, ManifestFile> f in manifest.files)
            {
                i++;
                Console.WriteLine(f.Key);
                if (File.Exists(baseDirectory + f.Key))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(f.Key + " exists (" + i + " / " + manifest.files.Count + ")");
                    suceeded++;
                } else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(f.Key + " is missing (" + i + " / " + manifest.files.Count + ")");
                }
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Saving version info");
            bool found = false;
            for(int a = 0; a < config.apps.Count; a++)
            {
                if(config.apps[a].id == appId)
                {
                    found = true;
                    bool exists = false;
                    foreach(ReleaseChannelReleaseBinary b in config.apps[a].versions)
                    {
                        if(b.id == binary.id)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if(!exists) config.apps[a].versions.Add(binary);
                }
            }
            if(!found)
            {
                App a = new App();
                a.name = appName;
                a.id = appId;
                a.versions.Add(binary);
                config.apps.Add(a);
            }
            config.Save();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Finished");
            Console.ForegroundColor = ConsoleColor.Red;
            if(suceeded == 0)
            {
                Console.WriteLine("No files could be downloaded. " + i + " files had to be downloaded. Please contact ComputerElite so this issue gets fixed.");
            } else if(suceeded != i)
            {
                Console.WriteLine("Only " + suceeded + " out of " + i + " files could be downloaded. Please try it again to make sure no files are missing.");
            }
            
        }

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        public bool UpdateAccessToken(bool onlyIfNeeded = false)
        {
            Console.ForegroundColor = ConsoleColor.White;
            if (IsTokenValid(config.access_token) && onlyIfNeeded) return true;
            Console.WriteLine();
            if (onlyIfNeeded) Console.WriteLine("Your access_token is needed to authenticate downloads.");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("Do you need a guide on how to get the access token? (Y/n): ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            string choice = Console.ReadLine();
            Console.ForegroundColor = ConsoleColor.White;
            if (choice.ToLower() == "y" || choice == "")
            {
                //Console.WriteLine("Guide does not exist atm.");
                Process.Start("https://computerelite.github.io/tools/Oculus/ObtainToken.html");
            }
            Console.WriteLine();
            Console.WriteLine("Please enter your access_token (it'll be saved locally and is used to authenticate downloads)");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("access_token: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            string at = Console.ReadLine();
            String[] parts = at.Split(':');
            if(parts.Length >= 2)
            {
                at = parts[2];
            }
            at = at.Replace(" ", "");
            Console.ForegroundColor = ConsoleColor.White;
            if (IsTokenValid(at))
            {
                Console.WriteLine("Saving token");
                config.access_token = at;
                config.Save();
                return true;
            } else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Token is not valid. Please try getting you access_token with another request described in the guide.");
                return false;
            }
        }

        public bool IsTokenValid(string token)
        {
            //yes this is basic
            if (token.StartsWith("OC") && !token.Contains("|")) return true;
            return false;
        }

        public void SetupProgram()
        {
            Console.WriteLine();
            Console.WriteLine("Setting up Program directory");
            CreateDirectoryIfNotExisting(exe + "apps");
            Console.WriteLine("Finished");
            Console.WriteLine("Checking for updates");
            try
            {
                WebClient c = new WebClient();
                c.Headers.Add("user-agent", "RiftDowngrader/" + version);
                String tags = c.DownloadString("https://api.github.com/repos/ComputerElite/Rift-downgrader/tags");
                List<GitHubTag> ts = JsonSerializer.Deserialize<List<GitHubTag>>(tags);
                if (ts.Count > 0 && ts[0].name != version)
                {
                    Console.WriteLine("\nA new update is available (Current: " + version + "; New: " + ts[0].name + ")! Download it from https://github.com/ComputerElite/Rift-downgrader/releases/latest");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("no Updates available");
                }
            } catch
            {
                Console.WriteLine("Couldn't check for updates");
            }
        }

        public void CreateDirectoryIfNotExisting(string path)
        {
            if (Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        public void RecreateDirectoryIfExisting(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
            Directory.CreateDirectory(path);
        }
    }

    public class GitHubTag
    {
        public string name { get; set; } = "";
    }
}
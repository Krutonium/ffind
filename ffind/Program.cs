using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace ffind
{
    internal class Program
    {
        private static Config _cfg;
        private static List<string> _fileList;

        private static void Main(string[] args)
        {
            string appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ffind/");
            string configPath = Path.Combine(appdata, "config.json");
            string dbPath = Path.Combine(appdata, "db.json");
            
            string toFind = "";
            foreach (string thing in args)
            {
                toFind += thing + " ";
            }
            toFind = toFind.Trim();
            if (String.IsNullOrWhiteSpace(toFind) && File.Exists(dbPath))
            {
                Console.WriteLine("Please put a Query at the end of your command.");
                return;
            }
            
            if (!Directory.Exists(appdata))
            {
                Directory.CreateDirectory(appdata);
            }
            if(File.Exists(configPath))
            {
                _cfg = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));
            }
            else
            {
                _cfg = new Config();
                CreateConfig();
                File.WriteAllText(configPath, JsonConvert.SerializeObject(_cfg, Formatting.Indented));
            }

            if (toFind.ToLower() == "-help")
            {
                Console.WriteLine("Welcome to ffind Help!");
                Console.WriteLine("==========================================");
                Console.WriteLine("");
                Console.WriteLine("-help     - Brings up this info");
                Console.WriteLine("-updatedb - Updates the file database.");
                Console.WriteLine("");
                Console.WriteLine("==========================================");
                return;
            }

            if (toFind.ToLower() == "-about")
            {
                Console.WriteLine("What is ffind?");
                Console.WriteLine("ffind is Fast Find, a C# Application that indexes your Linux disk and saves");
                Console.WriteLine("a compressed file database that enables it to quickly return all matching");
                Console.WriteLine("files to any query, at least in terms of file names or paths.");
                Console.WriteLine();
                Console.WriteLine("When searching it searches the ENTIRE path of the file, so keep that in mind.");
                Console.WriteLine();
                Console.WriteLine("Created by Krutonium - https://github.com/Krutonium");
		Console.WriteLine("Revised by xero-lib - https://github.com/xero-lib");
                Console.WriteLine("I borrowed some code for traversing the filesystem cleanly from Microsoft");
                Console.WriteLine("https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/file-system/how-to-iterate-through-a-directory-tree");
            }
            
            if (toFind.ToLower() == "-updatedb" | File.Exists(dbPath) == false)
            {
                 Console.WriteLine("Please Wait, Updating File Database...");
                 Console.WriteLine("This may take a while.");
                 Console.WriteLine("Errors during this process are expected, you can safely ignore them.");
                 UpdateDb();
                 File.WriteAllText(dbPath, Compress(JsonConvert.SerializeObject(_fileList)));
                 Console.WriteLine("Update Complete.");
                 return;
            }
            
            //fileList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(dbPath));
            _fileList = JsonConvert.DeserializeObject<List<string>>(Decompress(File.ReadAllText(dbPath)));
            DoSearch(toFind);
        }

        public static void DoSearch(string toFind)
        {
            if (_cfg.caseSensitive == false)
            {
                var toFindLower = toFind.ToLower();
                foreach (var item in _fileList)
                {
                    if (item.ToLower().Contains(toFindLower))
                    {
                        Console.WriteLine(item);
                    }
                } 
            }
            else
            {
                foreach (var item in _fileList)
                {
                    if (item.Contains(toFind))
                    {
                        Console.WriteLine(item);
                    }
                }
            }
        }
        
        public static void CreateConfig()
        {
	        //Currently only supported by Arch based operating systems
            _cfg.PruneNames.Add(".git");
            _cfg.PruneNames.Add(".hg");
            _cfg.PruneNames.Add(".svn");
            //#########################// 
            _cfg.PrunePaths.Add("/afs");
            _cfg.PrunePaths.Add("/dev");
            _cfg.PrunePaths.Add("/media");
            _cfg.PrunePaths.Add("/mnt");
            _cfg.PrunePaths.Add("/net");
            _cfg.PrunePaths.Add("/sfs");
            _cfg.PrunePaths.Add("/tmp");
            _cfg.PrunePaths.Add("/udev");
            _cfg.PrunePaths.Add("/var/cache");
            _cfg.PrunePaths.Add("/var/lock");
            _cfg.PrunePaths.Add("/var/run");
            _cfg.PrunePaths.Add("/var/spool");
            _cfg.PrunePaths.Add("/var/lib/pacman/local");
		    _cfg.PrunePaths.Add("/var/tmp");
	        _cfg.PrunePaths.Add("/proc");
        }

        public static void UpdateDb()
        {
            //Start Indexing from root.
            string root = Path.GetPathRoot("");
            DirectoryInfo info = new DirectoryInfo(root ?? "/");
            _fileList = new List<string>();
            var spinner = new ConsoleSpiner();
            WalkDirectoryTree(info, spinner);
        }

        private static void WalkDirectoryTree(DirectoryInfo root, ConsoleSpiner spinner)
        {
            FileInfo[] files = null;
            DirectoryInfo[] subDirs = null;
            spinner.Turn();
            try
            {
                files = root.GetFiles("*.*");
            }catch (UnauthorizedAccessException e){
                //We're not authorized, and that's fine.
                //Console.WriteLine("Cannot access {0}, Permission Denied, but this is expected.", e);
                //Disabled because it will cause *excessive* spam and isn't useful for the user.
            }
            catch (DirectoryNotFoundException e)
            {
                Console.WriteLine(e.Message);
            }

            if (files != null)
            {
                foreach (FileInfo fi in files)
                {
                    _fileList.Add(fi.FullName);
                }

                // Now find all the subdirectories under this directory.
                subDirs = root.GetDirectories();

                foreach (DirectoryInfo dirInfo in subDirs)
                {
                    if (!_cfg.PrunePaths.Contains(dirInfo.FullName))
                    {
                        if (!_cfg.PruneNames.Contains(dirInfo.Name))
                        {
                            try
                            {
                                var att = File.GetAttributes(dirInfo.FullName);
                                if (!att.HasFlag(FileAttributes.ReparsePoint))
                                {
                                    //Console.WriteLine("Entering " + dirInfo.FullName);
                                    WalkDirectoryTree(dirInfo, spinner);
                                }
                                else
                                {
                                    //Console.WriteLine("Skipping " + dirInfo.FullName);
                                }
                            } catch (Exception e)
                            {
                                Console.WriteLine("Errored on " + dirInfo.FullName);
                                Console.WriteLine(e.Message);
                                Console.WriteLine("Continuing...");
                            }
                        }
                    }
                }
            }
        }

        public class ConsoleSpiner
        {
            int counter;
            public ConsoleSpiner()
            {
                counter = 0;
            }
            public void Turn()
            {
                counter++;        
                switch (counter % 4)
                {
                    case 0: Console.Write("/"); break;
                    case 1: Console.Write("-"); break;
                    case 2: Console.Write("\\"); break;
                    case 3: Console.Write("|"); break;
                }
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
            }
        }
        
        public static string Compress(string uncompressedString) 
        {
            using var uncompressedStream = new MemoryStream(Encoding.UTF8.GetBytes(uncompressedString));
        using var compressedStream = new MemoryStream(); 
        if (_cfg.shouldCompressDB)
        {
            using var compressorStream = new DeflateStream(compressedStream, CompressionLevel.Optimal, true);
            uncompressedStream.CopyTo(compressorStream);
        }
        else
        {
            using var compressorStream = new DeflateStream(compressedStream, CompressionLevel.NoCompression, true);
            uncompressedStream.CopyTo(compressorStream);
        }

        var compressedBytes = compressedStream.ToArray();
        return Convert.ToBase64String(compressedBytes);
    }

        private static string Decompress(string compressedString)
        {
        byte[] decompressedBytes;

        var compressedStream = new MemoryStream(Convert.FromBase64String(compressedString));

        using (var decompressorStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
        {
            using (var decompressedStream = new MemoryStream())
            {
                decompressorStream.CopyTo(decompressedStream);

                decompressedBytes = decompressedStream.ToArray();
            }
        }

        return Encoding.UTF8.GetString(decompressedBytes);
    }
        public class Config
        {
            public List<string> PruneNames = new List<string>();
            public List<string> PrunePaths = new List<string>();
            public bool caseSensitive = false;
            public bool shouldCompressDB = true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace ffind
{
    class Program
    {
        private static config cfg;
        private static List<string> fileList;
        static void Main(string[] args)
        {
            string Appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ffind/");
            string ConfigPath = Path.Combine(Appdata, "config.json");
            string dbPath = Path.Combine(Appdata, "db.json");
            
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
            
            if (!Directory.Exists(Appdata))
            {
                Directory.CreateDirectory(Appdata);
            }
            if(File.Exists(ConfigPath))
            {
                cfg = JsonConvert.DeserializeObject<config>(File.ReadAllText(ConfigPath));
            }
            else
            {
                cfg = new config();
                createConfig();
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(cfg, Formatting.Indented));
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
                 UpdateDB();
                 File.WriteAllText(dbPath, Compress(JsonConvert.SerializeObject(fileList)));
                 Console.WriteLine("Update Complete.");
                 return;
            }
            
            //fileList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(dbPath));
            fileList = JsonConvert.DeserializeObject<List<string>>(Decompress(File.ReadAllText(dbPath)));
            doSearch(toFind);
        }

        public static void doSearch(string toFind)
        {
            if (cfg.caseSensitive == false)
            {
                var toFindLower = toFind.ToLower();
                foreach (var item in fileList)
                {
                    if (item.ToLower().Contains(toFindLower))
                    {
                        Console.WriteLine(item);
                    }
                } 
            }
            else
            {
                foreach (var item in fileList)
                {
                    if (item.Contains(toFind))
                    {
                        Console.WriteLine(item);
                    }
                }
            }
        }
        
        public static void createConfig()
        {
	//Currently only supported by Arch based operating systems
            cfg.PruneNames.Add(".git");
            cfg.PruneNames.Add(".hg");
            cfg.PruneNames.Add(".svn");
           //#########################// 
            cfg.PrunePaths.Add("/afs");
            cfg.PrunePaths.Add("/dev");
            cfg.PrunePaths.Add("/media");
            cfg.PrunePaths.Add("/mnt");
            cfg.PrunePaths.Add("/net");
            cfg.PrunePaths.Add("/sfs");
            cfg.PrunePaths.Add("/tmp");
            cfg.PrunePaths.Add("/udev");
            cfg.PrunePaths.Add("/var/cache");
            cfg.PrunePaths.Add("/var/lock");
            cfg.PrunePaths.Add("/var/run");
            cfg.PrunePaths.Add("/var/spool");
            cfg.PrunePaths.Add("/var/lib/pacman/local"),
		    cfg.PrunePaths.Add("/var/tmp");
	    cfg.PrunePaths.Add("/proc");
        }

        public static void UpdateDB()
        {
            //Start Indexing from root.
            string root = Path.GetPathRoot("");
            DirectoryInfo info = new DirectoryInfo(root ?? "/");
            fileList = new List<string>();
            WalkDirectoryTree(info);
        }

        static void WalkDirectoryTree(System.IO.DirectoryInfo root)
        {
            System.IO.FileInfo[] files = null;
            System.IO.DirectoryInfo[] subDirs = null;
            
            try
            {
                files = root.GetFiles("*.*");
             catch (UnauthorizedAccessException e)
           	 {
                //We're not authorized, and that's fine.
                Console.WriteLine("Cannot access {0}, Permission Denied, but this is expected.", e);
		 }
            catch (System.IO.DirectoryNotFoundException e)
            {
                Console.WriteLine(e.Message);
            }

            if (files != null)
            {
                foreach (System.IO.FileInfo fi in files)
                {
                    fileList.Add(fi.FullName);
                }

                // Now find all the subdirectories under this directory.
                subDirs = root.GetDirectories();

                foreach (System.IO.DirectoryInfo dirInfo in subDirs)
                {
                    if (!cfg.PrunePaths.Contains(dirInfo.FullName))
                    {
                        if (!cfg.PruneNames.Contains(dirInfo.Name))
                        {
                            try
                            {
                                var att = File.GetAttributes(dirInfo.FullName);
                                if (!att.HasFlag(FileAttributes.ReparsePoint))
                                {
                                    //Console.WriteLine("Entering " + dirInfo.FullName);
                                    WalkDirectoryTree(dirInfo);
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
        
        public static string Compress(string uncompressedString) 
        {
        byte[] compressedBytes;

        using (var uncompressedStream = new MemoryStream(Encoding.UTF8.GetBytes(uncompressedString)))
        {
            using (var compressedStream = new MemoryStream())
            { 
                // setting the leaveOpen parameter to true to ensure that compressedStream will not be closed when compressorStream is disposed
                // this allows compressorStream to close and flush its buffers to compressedStream and guarantees that compressedStream.ToArray() can be called afterward
                // although MSDN documentation states that ToArray() can be called on a closed MemoryStream, I don't want to rely on that very odd behavior should it ever change
                if (cfg.shouldCompressDB)
                {
                    using (var compressorStream = new DeflateStream(compressedStream, CompressionLevel.Optimal, true))
                    {
                        uncompressedStream.CopyTo(compressorStream);
                    }
                }
                else
                {
                    using (var compressorStream = new DeflateStream(compressedStream, CompressionLevel.NoCompression, true))
                    {
                        uncompressedStream.CopyTo(compressorStream);
                    }
                }


                // call compressedStream.ToArray() after the enclosing DeflateStream has closed and flushed its buffer to compressedStream
                compressedBytes = compressedStream.ToArray();
            }
        }

        return Convert.ToBase64String(compressedBytes);
    }

    /// <summary>
    /// Decompresses a deflate compressed, Base64 encoded string and returns an uncompressed string.
    /// </summary>
    /// <param name="compressedString">String to decompress.</param>
    public static string Decompress(string compressedString)
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
        public class config
        {
            public List<string> PruneNames = new List<string>();
            public List<string> PrunePaths = new List<string>();
            public bool caseSensitive = false;
            public bool shouldCompressDB = true;
        }
    }
}

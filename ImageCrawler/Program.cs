using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MetadataExtractor;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Directory = System.IO.Directory;

namespace ImageCrawler
{
    class Program
    {
        private static IMongoCollection<Image> mongoCollection;

        // https://maps.googleapis.com/maps/api/geocode/json?latlng=55.70083333,12.53944444&key=AIzaSyB0GmXjYKtLeFFHx7G-z95dEpCSuUihgSs
        static void Main(string[] args)
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var db = client.GetDatabase("ImageOrganizer");
            mongoCollection = db.GetCollection<Image>("Images");
            DirectoryInfo d = new DirectoryInfo("c:/temp/imageTest");
            WalkDirectoryTree(d);
            Console.ReadKey();
        }

        static void Log(string msg)
        {
            Console.WriteLine(msg);
        }

        static void WalkDirectoryTree(System.IO.DirectoryInfo root)
        {
            System.IO.FileInfo[] files = null;
            System.IO.DirectoryInfo[] subDirs = null;

            // First, process all the files directly under this folder
            try
            {
                files = root.GetFiles("*.jpg");
            }

            // This is thrown if even one of the files requires permissions greater
            // than the application provides.
            catch (UnauthorizedAccessException e)
            {
                // This code just writes out the message and continues to recurse.
                // You may decide to do something different here. For example, you
                // can try to elevate your privileges and access the file again.
                Log(e.Message);
            }

            catch (System.IO.DirectoryNotFoundException e)
            {
                Log(e.Message);
            }

            if (files != null)
            {
                foreach (System.IO.FileInfo fi in files)
                {
                    Task.Run(() => HandleFile(fi.FullName));
                }

                // Now find all the subdirectories under this directory.
                subDirs = root.GetDirectories();

                foreach (System.IO.DirectoryInfo dirInfo in subDirs)
                {
                    // Resursive call for each subdirectory.
                    WalkDirectoryTree(dirInfo);
                }
            }
        }

        private static void HandleFile(string filePath)
        {
            Log("*** " + filePath + " ***");
            IReadOnlyList<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(filePath);
            var dictionary = new Dictionary<string, string>();
            foreach (var directory in directories)
            {
                foreach (var tag in directory.Tags)
                {
                    dictionary.Add(directory.Name + "_" + tag.Name, tag.Description);
                }
            }
            mongoCollection.InsertOneAsync(new Image {Metadata = dictionary, Path = filePath});
        }
    }

    public class Image
    {
        [BsonId]
        public string Path { get; set; }
        public Dictionary<string,string> Metadata { get; set; }

    }
}

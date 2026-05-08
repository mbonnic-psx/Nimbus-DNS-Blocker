using Nimbus_Internet_Blocker.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nimbus_Internet_Blocker.Services
{ 
    public class PresetService
    {
        /*
         * declaring each JSON file to the variable name
         * "seed" = the starter file we ship with the app
         * "live" = the file that gets edited the one that get stored in AppData
         */
        public const string liveFileName = "presets.json";
        public const string seedFileName = "presets.seed.json";  

        public string GetLivePath()
        {
            /*
             * Links the path of AppData and presets.json
             * (AppDataDirectory + "/" + filename)
             */
            return Path.Combine(FileSystem.AppDataDirectory, liveFileName);
        }

        /*
         * Task<T> = this work will finish later and produce a T or promise a string later in this case
         * async = this means in this method I am going to await
         * await means pause this method here and let the app keep running and then continue when the Task finishes
         */
        public async Task<string> EnsureLiveFileExistsAsync()
        {
                string livePath = GetLivePath(); // Get the json file path in the AppData folder

                if (File.Exists(livePath))
                {
                    return livePath; // If the file already exists return the path
                }

            string seedJson = await ReadSeedTextAsync(seedFileName); // Read the seed Json file from the packaged app assets if there is not one in AppData
             
            if (string.IsNullOrWhiteSpace(seedJson)) 
            {
                seedJson = "{ \"categories\": {} }"; // If the file could not be read default to an empty preset file
            }
            
            var folder  = Path.GetDirectoryName(livePath); 
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder); // Make sure the folder exists already
            }

            await File.WriteAllTextAsync(livePath, seedJson); // Create prsets.json in AppData using the seed content

                return livePath;
        }

        public async Task<PresetsRoot> LoadAsync() // PATH: "C:\Users\danie\AppData\Local\User Name\com.companyname.nimbusinternetblocker\Data\presets.json"
        {
            try
            {
                await EnsureLiveFileExistsAsync(); // Make sure there is a usable presets file in AppData

                string livePath = GetLivePath();
                string json = await File.ReadAllTextAsync(livePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return new PresetsRoot();
                }

                // Convert the Json text into the model
                var root = JsonSerializer.Deserialize<PresetsRoot>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if(root == null)
                {
                    return new PresetsRoot();
                }

                NormalizePresets(root); // Clean up bad or missing vaules so the UI and Apply step are safer

                return root ?? new PresetsRoot();
            }

            catch (Exception ex)
            {
                Debug.WriteLine($"PresetService.LoadAsync failed: {ex}");
                return new PresetsRoot(); 
            }
        }

        public async Task SaveAsync(PresetsRoot root)
        {

            if (root == null)
            {
                return;
            }
            try
            {
                string livePath = GetLivePath();

                NormalizePresets(root); // Clean the file befire saving

                var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true}); // Turn the model back into Json and save
                await File.WriteAllTextAsync(livePath, json);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(message: $"PresetService.SaveAsync has failed: {ex}");
            }
        }

        public void NormalizePresets(PresetsRoot root)
        {
            if (root.Categories == null)
            {
                root.Categories = new Dictionary<string, PresetCategory>();
            }

            foreach (var category in root.Categories.Values)
            {
                // 1) make sure Entries list exists
                if (category.Entries == null)
                    category.Entries = new List<PresetEntry>();

                // 2) normalize each entry
                foreach (var entry in category.Entries)
                {
                    entry.Host = NormalizeHost(entry.Host); // Clean the host text

                    if (entry.Ipv4 == null)
                    {
                        entry.Ipv4 = "0.0.0.0"; // If missing Ipv4 use the default
                    }
                    if (entry.Ipv6 == null)
                    {
                        entry.Ipv6 = "::"; // If missing Ipv6 use the default
                    }
                }

                // Remove Blanks
                category.Entries = category.Entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.Host))
                    .ToList();

                // Remove Dupes
                category.Entries = category.Entries
                    .GroupBy(e => e.Host, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
            }
        }

        private async Task<string> ReadSeedTextAsync(string seedFileName)
        {
            //string seedPath = Path.Combine(FileSystem.AppDataDirectory, seedFileName);
            //return await File.ReadAllTextAsync(seedPath);

            /*
             * Put presets.seed.json into Resources/Raw/ 
             * This will read it from the app package
             */
            using var stream = await FileSystem.OpenAppPackageFileAsync(seedFileName);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        private static string NormalizeHost(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            var s = input.Trim();

            s = Regex.Replace(s, @"^\s*https?://", "", RegexOptions.IgnoreCase); // strip scheme if pasted as a URL

            var slash = s.IndexOf('/');
            if (slash >= 0) s = s[..slash]; //cut off path/query/fragment

            var colon = s.IndexOf(':');
            if (colon >= 0) s = s[..colon]; // remove port if present (example.com:443)

            s = s.Trim().TrimEnd('.').ToLowerInvariant();

            return s;
        }
    }
}

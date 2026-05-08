using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nimbus_Internet_Blocker.Models;

namespace Nimbus_Internet_Blocker.Services
{
    public class CustomSitesService
    {
        /*
         * declaring each JSON file to the variable name
         * "seed" = the starter file we ship with the app
         * "live" = the file that gets edited the one that get stored in AppData
         */
        public const string liveFileName = "custom.json";
        public const string seedFileName = "custom.seed.json";

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

            var folder = Path.GetDirectoryName(livePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder); // Make sure the folder exists already
            }

            await File.WriteAllTextAsync(livePath, seedJson); // Create prsets.json in AppData using the seed content

            return livePath;
        }

        public async Task<CustomsRoot> LoadAsync() // PATH: "C:\Users\danie\AppData\Local\User Name\com.companyname.nimbusinternetblocker\Data\presets.json"
        {
            try
            {
                await EnsureLiveFileExistsAsync(); // Make sure there is a usable presets file in AppData

                string livePath = GetLivePath();
                string json = await File.ReadAllTextAsync(livePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return new CustomsRoot();
                }

                // Convert the Json text into the model
                var root = JsonSerializer.Deserialize<CustomsRoot>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (root == null)
                {
                    return new CustomsRoot();
                }

                NormalizeCustoms(root); // Clean up bad or missing vaules so the UI and Apply step are safer

                return root ?? new CustomsRoot();
            }

            catch (Exception ex)
            {
                Debug.WriteLine($"PresetService.LoadAsync failed: {ex}");
                return new CustomsRoot();
            }
        }

        public async Task SaveAsync(CustomsRoot root)
        {

            if (root == null)
            {
                return;
            }
            try
            {
                string livePath = GetLivePath();

                NormalizeCustoms(root); // Clean the file before saving

                var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }); // Turn the model back into Json and save
                await File.WriteAllTextAsync(livePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(message: $"PresetService.SaveAsync has failed: {ex}");
            }
        }

        public void NormalizeCustoms(CustomsRoot root)
        {
            if (root.Sites == null)
            {
                root.Sites = new List<CustomEntry>();
            }

            foreach (var site in root.Sites)
            {
                site.Host = NormalizeHost(site.Host); // Clean the host text

                site.Enabled ??= true; // Default to enabled

                if (string.IsNullOrWhiteSpace(site.Ipv4))
                    site.Ipv4 = "0.0.0.0";

                if (string.IsNullOrWhiteSpace(site.Ipv6))
                    site.Ipv6 = "::";
            }

            // remove blanks
            root.Sites = root.Sites
                .Where(s => !string.IsNullOrWhiteSpace(s.Host))
                .ToList();

            // dedupe by host (case-insensitive)
            root.Sites = root.Sites
                .GroupBy(s => s.Host, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        public async Task<(bool success, string message, CustomsRoot updatedRoot)> AddCustomSite(string inputHost)
        {
            CustomsRoot root = await LoadAsync(); // Load the existing custom sites

            var normalizedHost = NormalizeHost(inputHost); // Normalize the input host

            if (string.IsNullOrWhiteSpace(normalizedHost) || !normalizedHost.Contains("."))
            {
                return (false, "The host cannot be empty or contain any whitespace and must include a \".\"", root); // Basic Validation if there is no host entered and if there is whitespace only
            }
            if (root.Sites.Any(s => string.Equals(s.Host, normalizedHost, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, "This host already exists in your custom sites.", root); // Check for duplicates
            }

            var newSite = new CustomEntry { Host = normalizedHost, Enabled = true, Ipv4 = "0.0.0.0", Ipv6 = "::" }; // Create a new custom site entry

            root.Sites.Add(newSite); // Add the new site to the list

            NormalizeCustoms(root);

            await SaveAsync (root); // Save the updated root back to the file

            return (true, "Custom site added successfully.", root);
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nimbus_Internet_Blocker.Models;
using Nimbus_Internet_Blocker.Utilities;

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
                seedJson = "{ \"sites\": [] }"; // If the file could not be read default to an empty custom sites file
            }

            var folder = Path.GetDirectoryName(livePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder); // Make sure the folder exists already
            }

            await File.WriteAllTextAsync(livePath, seedJson); // Create prsets.json in AppData using the seed content

            return livePath;
        }

        /// <summary>
        /// Loads the live custom sites file. Returns <see langword="null"/> when the file
        /// exists but cannot be read or parsed — callers must treat null as "do not
        /// touch disk", never as an empty config (that would be a data-loss path).
        /// </summary>
        public async Task<CustomsRoot?> LoadAsync()
        {
            try
            {
                await EnsureLiveFileExistsAsync();

                string json = await File.ReadAllTextAsync(GetLivePath());
                if (string.IsNullOrWhiteSpace(json)) return null;

                var root = JsonSerializer.Deserialize<CustomsRoot>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (root is null) return null;

                NormalizeCustoms(root);
                return root;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CustomSitesService.LoadAsync failed: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Normalizes and saves the custom sites atomically. Returns <see langword="false"/>
        /// on failure; the previous file is left intact.
        /// </summary>
        public async Task<bool> SaveAsync(CustomsRoot root)
        {
            if (root is null) return false;

            try
            {
                NormalizeCustoms(root);
                var json = JsonSerializer.Serialize(root,
                    new JsonSerializerOptions { WriteIndented = true });
                return await AtomicFile.WriteAllTextAtomicAsync(GetLivePath(), json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CustomSitesService.SaveAsync failed: {ex}");
                return false;
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

        /// <summary>
        /// Validates and adds a host to <paramref name="root"/> in memory. Nothing is
        /// saved — pending changes persist at apply time (see Blocking.razor).
        /// </summary>
        public (bool success, string message) AddSite(CustomsRoot root, string inputHost)
        {
            var normalizedHost = NormalizeHost(inputHost);

            if (string.IsNullOrWhiteSpace(normalizedHost) || !normalizedHost.Contains('.'))
                return (false, "Enter a valid host like example.com.");

            if (root.Sites.Any(s => string.Equals(s.Host, normalizedHost, StringComparison.OrdinalIgnoreCase)))
                return (false, "This host already exists in your custom sites.");

            root.Sites.Add(new CustomEntry { Host = normalizedHost, Enabled = true, Ipv4 = "0.0.0.0", Ipv6 = "::" });
            NormalizeCustoms(root);

            return (true, $"{normalizedHost} added — click Apply Blocking Rules to activate.");
        }

        /// <summary>
        /// Removes a host from <paramref name="root"/> in memory. Nothing is saved —
        /// pending changes persist at apply time.
        /// </summary>
        public (bool success, string message) RemoveSite(CustomsRoot root, string host)
        {
            var normalizedHost = NormalizeHost(host);

            if (string.IsNullOrWhiteSpace(normalizedHost))
                return (false, "Invalid host provided.");

            var before = root.Sites.Count;
            root.Sites = root.Sites
                .Where(s => !string.Equals(s.Host, normalizedHost, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return before == root.Sites.Count
                ? (false, $"{normalizedHost} was not found in your custom sites.")
                : (true, $"{normalizedHost} removed — apply to update your blocking rules.");
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

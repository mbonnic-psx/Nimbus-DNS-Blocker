using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nimbus_Internet_Blocker.Models;
using Nimbus_Internet_Blocker.Utilities;

namespace Nimbus_Internet_Blocker.Services
{
    public class CustomSitesService : SeedBackedConfigService, ICustomSitesService
    {
        protected override string LiveFileName      => "custom.json";
        protected override string SeedFileName      => "custom.seed.json";
        protected override string EmptyFallbackJson => "{ \"sites\": [] }";

        /// <summary>
        /// Loads the live custom sites file. Returns <see langword="null"/> when the file
        /// exists but cannot be read or parsed — callers must treat null as "do not
        /// touch disk", never as an empty config.
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
                site.Host = HostValidation.NormalizeHost(site.Host); // Clean the host text

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
            var normalizedHost = HostValidation.NormalizeHost(inputHost);

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
            var normalizedHost = HostValidation.NormalizeHost(host);

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
    }
}

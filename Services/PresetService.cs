using Nimbus_Internet_Blocker.Models;
using Nimbus_Internet_Blocker.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nimbus_Internet_Blocker.Services
{
    public class PresetService : SeedBackedConfigService, IPresetService
    {
        protected override string LiveFileName      => "presets.json";
        protected override string SeedFileName      => "presets.seed.json";
        protected override string EmptyFallbackJson => "{ \"categories\": {} }";

        /// <summary>
        /// Loads the live presets file. Returns <see langword="null"/> when the file
        /// exists but cannot be read or parsed — callers must treat null as "do not
        /// touch disk", never as an empty config (that would be a data-loss path).
        /// </summary>
        public async Task<PresetsRoot?> LoadAsync()
        {
            try
            {
                await EnsureLiveFileExistsAsync();

                string json = await File.ReadAllTextAsync(GetLivePath());
                if (string.IsNullOrWhiteSpace(json)) return null;

                var root = JsonSerializer.Deserialize<PresetsRoot>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (root is null) return null;

                NormalizePresets(root);
                return root;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PresetService.LoadAsync failed: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Normalizes and saves the presets atomically. Returns <see langword="false"/>
        /// on failure; the previous file is left intact.
        /// </summary>
        public async Task<bool> SaveAsync(PresetsRoot root)
        {
            if (root is null) return false;

            try
            {
                NormalizePresets(root);
                var json = JsonSerializer.Serialize(root,
                    new JsonSerializerOptions { WriteIndented = true });
                return await AtomicFile.WriteAllTextAtomicAsync(GetLivePath(), json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PresetService.SaveAsync failed: {ex}");
                return false;
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
                    entry.Host = HostValidation.NormalizeHost(entry.Host); // Clean the host text

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
    }
}

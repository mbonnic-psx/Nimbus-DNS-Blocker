using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Nimbus_Internet_Blocker.Models
{
    public sealed class PresetsRoot
    {
        [JsonPropertyName("categories")]
        public Dictionary<string, PresetCategory> Categories { get; set; } = new();
    }

    public sealed class PresetCategory
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("entries")]
        public List<PresetEntry> Entries { get; set; } = new();
    }

    public sealed class PresetEntry
    {
        [JsonPropertyName("host")]
        public string Host { get; set; } = "";

        [JsonPropertyName("ipv4")]
        public string Ipv4 { get; set; } = "0.0.0.0";

        [JsonPropertyName("ipv6")]
        public string Ipv6 { get; set; } = "::";
    }
}

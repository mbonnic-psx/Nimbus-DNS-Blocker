using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Nimbus_Internet_Blocker.Models
{
    public sealed class CustomsRoot
    {
        [JsonPropertyName("sites")]
        public List<CustomEntry> Sites { get; set; } = new();
    }

    public sealed class CustomEntry
    {
        [JsonPropertyName("enabled")]
        public bool? Enabled { get; set; }

        [JsonPropertyName("host")]
        public string Host { get; set; } = "";

        [JsonPropertyName("ipv4")]
        public string Ipv4 { get; set; } = "0.0.0.0";

        [JsonPropertyName("ipv6")]
        public string Ipv6 { get; set; } = "::";
    }
}

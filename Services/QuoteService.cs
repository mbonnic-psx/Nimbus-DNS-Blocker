using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimbus_Internet_Blocker.Services
{
    public class QuoteService
    {
        List<string> quotes = new List<string>
        {
            "\"If you are going through hell, Keep Going\" - \"Winston Churchill\"",
            "\"The fastest way to succeed is slowly - Anonymous\"",
            "\"You find out by doing, not thinking - Anonymous\"",
            "\"If you are lost in the forest keep walking - Anonymous\"",
            "\"It does not matter how slowly you go as long as you do not stop - Confucius\"",
            "\"A person who thinks all the time has nothing to think about except thoughts - Alan Watts\"",
            "\"The seeds I plant yesterday shapes the plant that grows today - Anonymous\"",
            "\"People may spend their whole lives climbing the ladder of success only to find once they reach the top, that the ladder is leaning against the wrong wall - Thomas Merton\"",
            "\"If you feel like you are losing everything remember, trees lose their leaves every year, yet they stand tall and wait for better days to come - Anonymous\"",
            "\"It is not good that man should be alone... We were made for relationships not just with God, but with each other - Genesis 2:18\""
        };

        private string? currentQuote;

        private readonly Random rand = new();
        
        public string CurrentQuote => currentQuote ??= quotes[rand.Next(quotes.Count)];

        public IReadOnlyList<string> GetQuotes => quotes;
    }
}

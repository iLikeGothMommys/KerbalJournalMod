using System.Collections.Generic;

namespace KerbalJournal.Models
{
    /// <summary>
    /// Container for a single Kerbal's journal data.
    /// </summary>
    public class KerbalJournalData
    {
        public string KerbalName { get; set; }
        public List<KerbalJournalEntry> Entries { get; set; } = new List<KerbalJournalEntry>();
    }
}

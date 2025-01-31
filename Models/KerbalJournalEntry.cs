using System;

namespace KerbalJournal.Models
{
    /// <summary>
    /// A class that holds one journal entry.
    /// </summary>
    public class KerbalJournalEntry
    {
        public string Date { get; set; }   // e.g. "Y1 D100" or "Year 1, Day 100"
        public int Index { get; set; }     // e.g. 001, 002, ...
        public string Title { get; set; }  // Journal name/title
        public string Body { get; set; }   // Journal contents
        public bool IsLocked { get; set; } = false; // If locked, no further edits or re-locks
    }
}

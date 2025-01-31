using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP;
using KerbalJournal.Models;

namespace KerbalJournal.Scenarios
{
    /// <summary>
    /// ScenarioModule for storing the journal data in persistent files.
    /// </summary>
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[]
    {
        GameScenes.FLIGHT,
        GameScenes.SPACECENTER,
        GameScenes.TRACKSTATION
    })]
    public class JournalScenario : ScenarioModule
    {
        public static JournalScenario Instance;
        public Dictionary<string, KerbalJournalData> KerbalJournals { get; set; }
            = new Dictionary<string, KerbalJournalData>();

        public override void OnAwake()
        {
            base.OnAwake();
            Instance = this;
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            ConfigNode journalsNode = node.AddNode("KERBAL_JOURNAL_DATA");
            foreach (var kvp in KerbalJournals)
            {
                KerbalJournalData kerbalData = kvp.Value;
                ConfigNode kerbalNode = journalsNode.AddNode("KERBAL_DATA");
                kerbalNode.AddValue("Name", kerbalData.KerbalName);

                foreach (var entry in kerbalData.Entries)
                {
                    ConfigNode entryNode = kerbalNode.AddNode("ENTRY");
                    entryNode.AddValue("Date", entry.Date);
                    entryNode.AddValue("Index", entry.Index);
                    entryNode.AddValue("Title", entry.Title);
                    entryNode.AddValue("Body", entry.Body);
                    entryNode.AddValue("IsLocked", entry.IsLocked);
                }
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            KerbalJournals.Clear();

            ConfigNode journalsNode = node.GetNode("KERBAL_JOURNAL_DATA");
            if (journalsNode == null) return;

            foreach (var kerbalNode in journalsNode.GetNodes("KERBAL_DATA"))
            {
                string kerbalName = kerbalNode.GetValue("Name");
                if (string.IsNullOrEmpty(kerbalName))
                {
                    Debug.LogWarning("[KerbalJournal] Encountered Kerbal data without a name.");
                    continue;
                }

                var data = new KerbalJournalData
                {
                    KerbalName = kerbalName,
                    Entries = new List<KerbalJournalEntry>()
                };

                foreach (var entryNode in kerbalNode.GetNodes("ENTRY"))
                {
                    var entry = new KerbalJournalEntry
                    {
                        Date = entryNode.GetValue("Date"),
                        Title = entryNode.GetValue("Title"),
                        Body = entryNode.GetValue("Body")
                    };

                    // Safely parse Index
                    if (entryNode.HasValue("Index"))
                    {
                        if (int.TryParse(entryNode.GetValue("Index"), out int parsedIndex))
                        {
                            entry.Index = parsedIndex;
                        }
                        else
                        {
                            Debug.LogWarning($"[KerbalJournal] Invalid Index value for Kerbal {kerbalName}. Setting to 1.");
                            entry.Index = 1;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[KerbalJournal] Missing Index value for Kerbal {kerbalName}. Setting to 1.");
                        entry.Index = 1;
                    }

                    // Safely parse IsLocked
                    if (entryNode.HasValue("IsLocked"))
                    {
                        if (bool.TryParse(entryNode.GetValue("IsLocked"), out bool locked))
                        {
                            entry.IsLocked = locked;
                        }
                        else
                        {
                            Debug.LogWarning($"[KerbalJournal] Invalid IsLocked value for Kerbal {kerbalName}, Journal #{entry.Index}. Setting to false.");
                            entry.IsLocked = false;
                        }
                    }

                    data.Entries.Add(entry);
                }

                // Ensure unique Kerbal names
                if (!KerbalJournals.ContainsKey(kerbalName))
                {
                    KerbalJournals[kerbalName] = data;
                }
                else
                {
                    Debug.LogWarning($"[KerbalJournal] Duplicate Kerbal name found: {kerbalName}. Skipping duplicate entries.");
                }
            }
        }

        /// <summary>
        /// Adds a new journal entry for a specified Kerbal.
        /// </summary>
        public void AddJournalEntry(string kerbalName, string title, string body, string date)
        {
            if (string.IsNullOrEmpty(kerbalName))
            {
                Debug.LogError("[KerbalJournal] Attempted to add a journal entry with an empty Kerbal name.");
                return;
            }

            if (!KerbalJournals.ContainsKey(kerbalName))
            {
                KerbalJournals[kerbalName] = new KerbalJournalData
                {
                    KerbalName = kerbalName,
                    Entries = new List<KerbalJournalEntry>()
                };
            }

            int nextIndex = KerbalJournals[kerbalName].Entries.Count + 1;
            var entry = new KerbalJournalEntry
            {
                Date = date,
                Index = nextIndex,
                Title = title,
                Body = body
            };
            KerbalJournals[kerbalName].Entries.Add(entry);
            Debug.Log($"[KerbalJournal] Added Journal #{entry.Index} for Kerbal {kerbalName}.");
        }

        /// <summary>
        /// Retrieves all journal entries for a specified Kerbal.
        /// </summary>
        public List<KerbalJournalEntry> GetKerbalEntries(string kerbalName)
        {
            if (KerbalJournals.ContainsKey(kerbalName))
                return KerbalJournals[kerbalName].Entries;
            return null;
        }

        /// <summary>
        /// Deletes a specific journal entry for a specified Kerbal.
        /// </summary>
        public void DeleteJournalEntry(string kerbalName, int index)
        {
            if (!KerbalJournals.ContainsKey(kerbalName))
            {
                Debug.LogWarning($"[KerbalJournal] Attempted to delete a journal entry for non-existent Kerbal: {kerbalName}.");
                return;
            }

            var list = KerbalJournals[kerbalName].Entries;
            var toRemove = list.FirstOrDefault(e => e.Index == index);
            if (toRemove != null)
            {
                list.Remove(toRemove);
                // Reindex remaining entries
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].Index = i + 1;
                }
                Debug.Log($"[KerbalJournal] Deleted Journal #{index} for Kerbal {kerbalName}.");
            }
            else
            {
                Debug.LogWarning($"[KerbalJournal] Attempted to delete non-existent Journal #{index} for Kerbal {kerbalName}.");
            }
        }
    }
}

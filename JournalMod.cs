using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP;
using KSP.UI.Screens;

namespace KerbalJournal
{
    /// <summary>
    /// A class that holds one journal entry.
    /// </summary>
    public class KerbalJournalEntry
    {
        public string Date;   // e.g. "Y1 D100" or "Year 1, Day 100"
        public int Index;     // e.g. 001, 002, ...
        public string Title;  // Journal name/title
        public string Body;   // Journal contents
        public bool IsLocked = false; // If locked, no further edits or re-locks
    }

    /// <summary>
    /// Container for a single Kerbal's journal data.
    /// </summary>
    public class KerbalJournalData
    {
        public string KerbalName;
        public List<KerbalJournalEntry> Entries = new List<KerbalJournalEntry>();
    }

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
        public Dictionary<string, KerbalJournalData> KerbalJournals
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
                        Index = int.Parse(entryNode.GetValue("Index")),
                        Title = entryNode.GetValue("Title"),
                        Body = entryNode.GetValue("Body")
                    };

                    // Check "IsLocked"
                    if (entryNode.HasValue("IsLocked"))
                    {
                        bool locked = false;
                        bool.TryParse(entryNode.GetValue("IsLocked"), out locked);
                        entry.IsLocked = locked;
                    }
                    data.Entries.Add(entry);
                }

                KerbalJournals[kerbalName] = data;
            }
        }

        public void AddJournalEntry(string kerbalName, string title, string body, string date)
        {
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
        }

        public List<KerbalJournalEntry> GetKerbalEntries(string kerbalName)
        {
            if (KerbalJournals.ContainsKey(kerbalName))
                return KerbalJournals[kerbalName].Entries;
            return null;
        }

        public void DeleteJournalEntry(string kerbalName, int index)
        {
            if (!KerbalJournals.ContainsKey(kerbalName)) return;
            var list = KerbalJournals[kerbalName].Entries;
            var toRemove = list.FirstOrDefault(e => e.Index == index);
            if (toRemove != null)
            {
                list.Remove(toRemove);
                list = list.OrderBy(e => e.Index).ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].Index = i + 1;
                }
                KerbalJournals[kerbalName].Entries = list;
            }
        }
    }

    /// <summary>
    /// Main UI code with hooking into SpaceCenter on load:
    /// The KSPAddon attribute ensures it runs in both Flight and SpaceCenter,
    /// and "true" means it persists across scene changes.
    /// We attach a handler to onGUIApplicationLauncherReady so the button
    /// appears as soon as the SpaceCenter is loaded.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight | KSPAddon.Startup.SpaceCentre, true)]
    public class JournalUI : MonoBehaviour
    {
        public static JournalUI Instance;
        private bool showGUI;

        // Window properties
        private Rect windowRect = new Rect(300, 100, 600, 650);
        private int windowID = 123456;

        private ApplicationLauncherButton appLauncherButton;
        private static Texture2D btnTexture;

        private enum JournalUIState
        {
            Main,
            ListJournals,
            View,
            Edit,
            Create
        }
        private JournalUIState currentState = JournalUIState.Main;

        private string selectedKerbal = null;
        private KerbalJournalEntry selectedEntry = null;

        private string newJournalKerbal = "";
        private string newJournalTitle = "";
        private string newJournalBody = "";

        private string editJournalTitle = "";
        private string editJournalBody = "";

        private Rect confirmRect = new Rect(0, 0, 300, 150);
        private bool showConfirmDialog = false;
        private bool showLockConfirmDialog = false;
        private int confirmDeleteIndex = -1;

        // Scroll vectors
        private Vector2 scrollKerbalList = Vector2.zero;
        private Vector2 scrollJournalList = Vector2.zero;
        private Vector2 scrollBodyView = Vector2.zero;
        private Vector2 scrollEditBody = Vector2.zero;
        private Vector2 scrollCreateKerbal = Vector2.zero;
        private Vector2 scrollCreateJournal = Vector2.zero;

        // Sorting
        private bool sortAscending = true;
        private int sortOption = 0;
        private string[] sortOptions = { "A to Z", "Z to A", "Journals (Low-High)", "Journals (High-Low)" };

        // Table widths
        private const float tableWidthMain = 560f;
        private const float tableWidthJournals = 560f;

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle columnHeaderStyle;
        private GUIStyle rowLabelStyle;
        private GUIStyle noJournalStyle;

        public void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this);

            // Hook into onGUIApplicationLauncherReady so the button is ready in SpaceCenter
            GameEvents.onGUIApplicationLauncherReady.Add(InitializeAppLauncherButton);
        }

        public void OnDestroy()
        {
            // Unhook the event
            GameEvents.onGUIApplicationLauncherReady.Remove(InitializeAppLauncherButton);

            if (appLauncherButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                appLauncherButton = null;
            }
            Instance = null;
        }

        /// <summary>
        /// This is called once the game has an AppLauncher ready (SpaceCenter/Flight).
        /// </summary>
        private void InitializeAppLauncherButton()
        {
            if (appLauncherButton == null && ApplicationLauncher.Instance != null)
            {
                // Ensure we have a valid texture
                if (btnTexture == null)
                {
                    btnTexture = GameDatabase.Instance.GetTexture(
                        "KerbalJournalMod/Icons/Journal", false);
                }

                appLauncherButton = ApplicationLauncher.Instance.AddModApplication(
                    onTrue: ShowWindow,
                    onFalse: HideWindow,
                    onHover: null,
                    onHoverOut: null,
                    onEnable: null,
                    onDisable: null,
                    visibleInScenes: ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.SPACECENTER,
                    texture: btnTexture
                );
            }
        }

        private void ShowWindow() { showGUI = true; }
        private void HideWindow() { showGUI = false; }

        private void SetupStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(HighLogic.Skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }
            if (columnHeaderStyle == null)
            {
                columnHeaderStyle = new GUIStyle(HighLogic.Skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
            }
            if (rowLabelStyle == null)
            {
                rowLabelStyle = new GUIStyle(HighLogic.Skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
            }
            if (noJournalStyle == null)
            {
                noJournalStyle = new GUIStyle(HighLogic.Skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }
        }

        public void OnGUI()
        {
            if (!showGUI) return;
            GUI.skin = HighLogic.Skin;

            SetupStyles();

            windowRect = GUILayout.Window(
                windowID,
                windowRect,
                MainWindow,
                "Kerbal Journal Manager",
                GUILayout.Width(600),
                GUILayout.Height(650)
            );

            // Confirm Delete
            if (showConfirmDialog)
            {
                confirmRect.x = windowRect.x + (windowRect.width / 2) - (confirmRect.width / 2);
                confirmRect.y = windowRect.y + (windowRect.height / 2) - (confirmRect.height / 2);
                confirmRect = GUILayout.Window(
                    999999,
                    confirmRect,
                    ConfirmDeleteWindow,
                    "Confirm Deletion",
                    GUILayout.Width(300),
                    GUILayout.Height(150)
                );
            }

            // Confirm Lock
            if (showLockConfirmDialog)
            {
                confirmRect.x = windowRect.x + (windowRect.width / 2) - (confirmRect.width / 2);
                confirmRect.y = windowRect.y + (windowRect.height / 2) - (confirmRect.height / 2);
                confirmRect = GUILayout.Window(
                    999998,
                    confirmRect,
                    ConfirmLockWindow,
                    "Confirm Lock",
                    GUILayout.Width(300),
                    GUILayout.Height(150)
                );
            }
        }

        private void MainWindow(int id)
        {
            GUILayout.BeginVertical();

            switch (currentState)
            {
                case JournalUIState.Main:
                    DrawMainPage();
                    break;
                case JournalUIState.ListJournals:
                    DrawListJournalsPage();
                    break;
                case JournalUIState.View:
                    DrawViewPage();
                    break;
                case JournalUIState.Edit:
                    DrawEditPage();
                    break;
                case JournalUIState.Create:
                    DrawCreatePage();
                    break;
            }

            GUILayout.EndVertical();

            DrawBottomButtons();

            GUI.DragWindow(new Rect(0, 0, windowRect.width, 30));
        }

        /// <summary>
        /// Single row of buttons at bottom, all centered, consistent height
        /// </summary>
        private void DrawBottomButtons()
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            const float buttonHeight = 35;

            switch (currentState)
            {
                case JournalUIState.Main:
                    {
                        if (GUILayout.Button("Create Journal", HighLogic.Skin.button, GUILayout.Width(120), GUILayout.Height(buttonHeight)))
                        {
                            currentState = JournalUIState.Create;
                            newJournalKerbal = "";
                            newJournalTitle = "";
                            newJournalBody = "";
                        }
                        GUILayout.Space(10);

                        if (GUILayout.Button($"SORT: {sortOptions[sortOption]}", HighLogic.Skin.button, GUILayout.Width(200), GUILayout.Height(buttonHeight)))
                        {
                            sortOption = (sortOption + 1) % sortOptions.Length;
                        }
                        GUILayout.Space(10);

                        if (GUILayout.Button("Close", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(buttonHeight)))
                        {
                            HideWindow();
                            if (appLauncherButton != null)
                            {
                                appLauncherButton.SetFalse(false);
                            }
                        }
                        break;
                    }

                case JournalUIState.ListJournals:
                    {
                        if (GUILayout.Button("Create Journal", HighLogic.Skin.button, GUILayout.Width(120), GUILayout.Height(buttonHeight)))
                        {
                            currentState = JournalUIState.Create;
                            newJournalKerbal = "";
                            newJournalTitle = "";
                            newJournalBody = "";
                        }
                        GUILayout.Space(10);

                        string dateSortLabel = sortAscending ? "SORT: Oldest First" : "SORT: Newest First";
                        if (GUILayout.Button(dateSortLabel, HighLogic.Skin.button, GUILayout.Width(200), GUILayout.Height(buttonHeight)))
                        {
                            sortAscending = !sortAscending;
                        }
                        GUILayout.Space(10);

                        if (GUILayout.Button("Return", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(buttonHeight)))
                        {
                            currentState = JournalUIState.Main;
                            selectedKerbal = null;
                        }
                        break;
                    }

                case JournalUIState.View:
                    {
                        if (selectedEntry != null && !selectedEntry.IsLocked)
                        {
                            if (GUILayout.Button("EDIT", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(buttonHeight)))
                            {
                                editJournalTitle = selectedEntry.Title;
                                editJournalBody = selectedEntry.Body;
                                currentState = JournalUIState.Edit;
                            }
                            GUILayout.Space(10);

                            if (GUILayout.Button("LOCK JOURNAL", HighLogic.Skin.button, GUILayout.Width(120), GUILayout.Height(buttonHeight)))
                            {
                                showLockConfirmDialog = true;
                            }
                            GUILayout.Space(10);
                        }

                        if (GUILayout.Button("DELETE", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(buttonHeight)))
                        {
                            showConfirmDialog = true;
                            confirmDeleteIndex = (selectedEntry != null) ? selectedEntry.Index : -1;
                        }
                        GUILayout.Space(10);

                        if (GUILayout.Button("RETURN", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(buttonHeight)))
                        {
                            currentState = JournalUIState.ListJournals;
                        }
                        break;
                    }

                case JournalUIState.Edit:
                    {
                        if (GUILayout.Button("Return", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(buttonHeight)))
                        {
                            currentState = JournalUIState.View;
                        }
                        break;
                    }

                case JournalUIState.Create:
                    {
                        if (GUILayout.Button("Save Journal", HighLogic.Skin.button, GUILayout.Width(150), GUILayout.Height(buttonHeight)))
                        {
                            if (!string.IsNullOrEmpty(newJournalTitle) && !string.IsNullOrEmpty(newJournalBody))
                            {
                                if (JournalScenario.Instance != null)
                                {
                                    string dateStr = GetCurrentKSPDate();
                                    JournalScenario.Instance.AddJournalEntry(
                                        newJournalKerbal,
                                        newJournalTitle,
                                        newJournalBody,
                                        dateStr
                                    );
                                }
                                currentState = JournalUIState.Main;
                                newJournalKerbal = "";
                                newJournalTitle = "";
                                newJournalBody = "";
                            }
                            else
                            {
                                Debug.LogWarning("Subject and Body cannot be empty.");
                            }
                        }
                        GUILayout.Space(10);

                        if (GUILayout.Button("Return", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(buttonHeight)))
                        {
                            currentState = JournalUIState.Main;
                            selectedKerbal = null;
                        }
                        break;
                    }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        #region Page Drawing

        private void DrawMainPage()
        {
            bool anyJournalExists = JournalScenario.Instance != null &&
                                    JournalScenario.Instance.KerbalJournals.Values.Any(kd => kd.Entries.Count > 0);

            if (!anyJournalExists)
            {
                GUILayout.BeginVertical(HighLogic.Skin.box, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("No journals have been found, please create a journal", noJournalStyle, GUILayout.Width(500));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            List<KerbalJournalData> sortedKerbals = JournalScenario.Instance.KerbalJournals.Values.ToList();
            switch (sortOption)
            {
                case 0: sortedKerbals = sortedKerbals.OrderBy(k => k.KerbalName).ToList(); break;
                case 1: sortedKerbals = sortedKerbals.OrderByDescending(k => k.KerbalName).ToList(); break;
                case 2: sortedKerbals = sortedKerbals.OrderBy(k => k.Entries.Count).ToList(); break;
                case 3: sortedKerbals = sortedKerbals.OrderByDescending(k => k.Entries.Count).ToList(); break;
            }

            scrollKerbalList = GUILayout.BeginScrollView(scrollKerbalList, GUILayout.Height(400));
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Name", columnHeaderStyle, GUILayout.Width(250));
            GUILayout.Label("Journals", columnHeaderStyle, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            foreach (var kd in sortedKerbals)
            {
                if (kd.Entries.Count > 0)
                {
                    GUILayout.BeginHorizontal(HighLogic.Skin.box, GUILayout.ExpandWidth(true));
                    GUILayout.Label(kd.KerbalName, rowLabelStyle, GUILayout.Width(250));
                    GUILayout.Label(kd.Entries.Count.ToString(), rowLabelStyle, GUILayout.Width(100));

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("View Journals", HighLogic.Skin.button, GUILayout.Width(120), GUILayout.Height(25)))
                    {
                        selectedKerbal = kd.KerbalName;
                        currentState = JournalUIState.ListJournals;
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.Space(5);
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawListJournalsPage()
        {
            if (string.IsNullOrEmpty(selectedKerbal))
            {
                GUILayout.Label("No Kerbal selected.", HighLogic.Skin.label);
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{selectedKerbal}'s Journal Entries", headerStyle, GUILayout.Height(40));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            var entries = JournalScenario.Instance.GetKerbalEntries(selectedKerbal);
            if (entries == null || entries.Count == 0)
            {
                GUILayout.Label("No entries for this Kerbal yet.", HighLogic.Skin.label);
                return;
            }

            List<KerbalJournalEntry> sortedEntries =
                sortAscending
                ? entries.OrderBy(e => e.Index).ToList()
                : entries.OrderByDescending(e => e.Index).ToList();

            scrollJournalList = GUILayout.BeginScrollView(scrollJournalList, GUILayout.Height(400));
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Date", columnHeaderStyle, GUILayout.Width(140));
            GUILayout.Label("Journal #", columnHeaderStyle, GUILayout.Width(80));
            GUILayout.Label("Title", columnHeaderStyle, GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            foreach (var entry in sortedEntries)
            {
                GUILayout.BeginHorizontal(HighLogic.Skin.box, GUILayout.ExpandWidth(true));
                string displayDate = ConvertLegacyDate(entry.Date);
                GUILayout.Label(displayDate, rowLabelStyle, GUILayout.Width(140));
                GUILayout.Label(entry.Index.ToString("D3"), rowLabelStyle, GUILayout.Width(80));
                GUILayout.Label(entry.Title, rowLabelStyle, GUILayout.Width(200));

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("VIEW", HighLogic.Skin.button, GUILayout.Width(80), GUILayout.Height(25)))
                {
                    selectedEntry = entry;
                    currentState = JournalUIState.View;
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(5);
            }
            GUILayout.EndScrollView();
        }

        private void DrawViewPage()
        {
            if (selectedEntry == null)
            {
                GUILayout.Label("No entry selected.", HighLogic.Skin.label);
                return;
            }

            // e.g. "Valentina Kerman LOG 003"
            string headerText = $"{selectedKerbal} LOG {selectedEntry.Index:D3}";
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(headerText, headerStyle, GUILayout.Height(40));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // "Crew Member", "Date", "Subject"
            GUILayout.Label($"Crew Member: {selectedKerbal}", columnHeaderStyle);
            string displayDate = ConvertLegacyDate(selectedEntry.Date);
            GUILayout.Label($"Date: {displayDate}", columnHeaderStyle);
            GUILayout.Label($"Subject: {selectedEntry.Title}", columnHeaderStyle);

            GUILayout.Space(10);

            scrollBodyView = GUILayout.BeginScrollView(scrollBodyView, GUILayout.Height(300));
            GUILayout.Label(selectedEntry.Body, rowLabelStyle);
            GUILayout.EndScrollView();

            GUILayout.Space(10);
        }

        private void DrawEditPage()
        {
            if (selectedEntry == null)
            {
                GUILayout.Label("No entry selected to edit.", HighLogic.Skin.label);
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Editing Journal", headerStyle, GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.Label("Subject:", columnHeaderStyle);
            editJournalTitle = GUILayout.TextField(editJournalTitle, HighLogic.Skin.textField, GUILayout.Height(30));
            GUILayout.Space(10);

            GUILayout.Label("Body:", columnHeaderStyle);
            scrollEditBody = GUILayout.BeginScrollView(scrollEditBody, GUILayout.Height(200));
            editJournalBody = GUILayout.TextArea(editJournalBody, HighLogic.Skin.textArea, GUILayout.Height(200));
            GUILayout.EndScrollView();

            GUILayout.Space(10);
        }

        private void DrawCreatePage()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Create Journal Entry", headerStyle, GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.BeginVertical(HighLogic.Skin.box, GUILayout.Width(580f), GUILayout.Height(150f));
            GUILayout.Label("Select Kerbal:", columnHeaderStyle);
            GUILayout.Space(5);

            if (FlightGlobals.ActiveVessel != null)
            {
                var crew = FlightGlobals.ActiveVessel.GetVesselCrew();
                if (crew.Count == 0)
                {
                    GUILayout.Label("No Kerbals on this vessel!", rowLabelStyle);
                }
                else
                {
                    // Left-aligned row, wrapping after 4
                    int buttonsPerRow = 4;
                    int count = 0;
                    GUILayout.BeginHorizontal();
                    foreach (var c in crew)
                    {
                        if (GUILayout.Button(c.name, HighLogic.Skin.button, GUILayout.Width(135), GUILayout.Height(30)))
                        {
                            newJournalKerbal = c.name;
                        }
                        count++;
                        if (count % buttonsPerRow == 0)
                        {
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                        }
                        else
                        {
                            GUILayout.Space(3);
                        }
                    }
                    if (count % buttonsPerRow != 0)
                    {
                        GUILayout.EndHorizontal();
                    }
                }
            }
            else
            {
                GUILayout.Label("No active vessel or no crew found.", rowLabelStyle);
            }

            GUILayout.EndVertical();
            GUILayout.Space(10);

            if (!string.IsNullOrEmpty(newJournalKerbal))
            {
                GUILayout.BeginVertical();
                GUILayout.Label("Selected Kerbal: " + newJournalKerbal, rowLabelStyle);
                GUILayout.Space(10);

                // "Subject"
                GUILayout.Label("Subject:", columnHeaderStyle);
                newJournalTitle = GUILayout.TextField(newJournalTitle, HighLogic.Skin.textField, GUILayout.Height(30));
                GUILayout.Space(10);

                GUILayout.Label("Journal Body:", columnHeaderStyle);
                scrollCreateJournal = GUILayout.BeginScrollView(scrollCreateJournal, GUILayout.Height(150));
                newJournalBody = GUILayout.TextArea(newJournalBody, HighLogic.Skin.textArea, GUILayout.Height(150));
                GUILayout.EndScrollView();
                GUILayout.Space(10);

                GUILayout.EndVertical();
            }
        }

        #endregion

        #region Confirmation Popups

        private void ConfirmDeleteWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Are you sure you want to delete this journal entry?", rowLabelStyle);
            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Yes", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(30)))
            {
                if (JournalScenario.Instance != null && selectedKerbal != null && confirmDeleteIndex > 0)
                {
                    JournalScenario.Instance.DeleteJournalEntry(selectedKerbal, confirmDeleteIndex);
                }
                showConfirmDialog = false;
                confirmDeleteIndex = -1;
                currentState = JournalUIState.ListJournals;
            }
            if (GUILayout.Button("No", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(30)))
            {
                showConfirmDialog = false;
                confirmDeleteIndex = -1;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void ConfirmLockWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Are you sure you want to LOCK this journal?\nThis removes the ability to Edit or Lock again.", rowLabelStyle);
            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Yes", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(30)))
            {
                if (selectedEntry != null)
                {
                    selectedEntry.IsLocked = true;
                }
                showLockConfirmDialog = false;
            }
            if (GUILayout.Button("No", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(30)))
            {
                showLockConfirmDialog = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        #endregion

        #region Utility

        private string ConvertLegacyDate(string storedDate)
        {
            if (storedDate.StartsWith("Year"))
                return storedDate;

            if (storedDate.StartsWith("Y"))
            {
                string[] parts = storedDate.Split(' ');
                if (parts.Length >= 2)
                {
                    string y = parts[0].Substring(1); // remove 'Y'
                    string d = parts[1].Substring(1); // remove 'D'
                    return $"Year {y}, Day {d}";
                }
            }
            return storedDate;
        }

        private string GetCurrentKSPDate()
        {
            double UT = Planetarium.GetUniversalTime();
            string fullDate = KSPUtil.PrintDate(UT, false);
            string dateString = "";

            if (!string.IsNullOrEmpty(fullDate))
            {
                // Typically "Year 1, Day 20"
                string[] parts = fullDate.Split(',');
                if (parts.Length >= 2)
                {
                    string yearPart = parts[0].Trim();
                    string dayPart = parts[1].Trim();
                    string yearNumber = yearPart.Split(' ')[1];
                    string dayNumber = dayPart.Split(' ')[1];
                    dateString = $"Year {yearNumber}, Day {dayNumber}";
                }
                else
                {
                    dateString = fullDate;
                }
            }
            return dateString;
        }

        #endregion
    }
}

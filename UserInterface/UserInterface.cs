using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP;
using KSP.UI.Screens;
using KerbalJournal.Models;
using KerbalJournal.Scenarios;
using KerbalJournal.Utilities;

namespace KerbalJournal.UI
{
    /// <summary>
    /// Main UI code with hooking into SpaceCenter on load.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class JournalUI : MonoBehaviour
    {
        public static JournalUI Instance;
        private bool showGUI;

        // Window properties
        private Rect windowRect = new Rect(300, 100, 600, 700);
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
        private Vector2 scrollCreateJournal = Vector2.zero;

        // Sorting for Main Page
        private int mainSortOption = 0;
        private readonly string[] mainSortOptions = { "A to Z", "Z to A", "Lowest First", "Highest First" };

        // Sorting for Journal List Page
        private int listSortOption = 0;
        private readonly string[] listSortOptions = { "Newest First", "Oldest First" };

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle columnHeaderStyle;
        private GUIStyle rowLabelStyle;
        private GUIStyle noJournalStyle;
        private GUIStyle bodyLabelStyle;

        // Additional styles for center alignment
        private GUIStyle centerHeaderStyle;      // used for "Number of Journals" header
        private GUIStyle centerRowLabelStyle;    // used for "Number of Journals" contents

        public void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this);

            // Hook into onGUIApplicationLauncherReady
            GameEvents.onGUIApplicationLauncherReady.Add(InitializeAppLauncherButton);

            // Also hook into scene changes to ensure button persists and handle GUI closure
            GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequested);
        }

        public void Start()
        {
            // Try to initialize button immediately if launcher exists
            if (ApplicationLauncher.Ready)
            {
                InitializeAppLauncherButton();
            }
        }

        public void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(InitializeAppLauncherButton);
            GameEvents.onGameSceneLoadRequested.Remove(OnGameSceneLoadRequested);

            if (appLauncherButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                appLauncherButton = null;
            }
            Instance = null;
        }

        private void OnGameSceneLoadRequested(GameScenes scene)
        {
            // Close the GUI when changing scenes
            if (showGUI)
            {
                HideWindow();
            }

            // Reset to Main state to ensure GUI opens at main page next time
            currentState = JournalUIState.Main;
        }

        /// <summary>
        /// This is called once the game has an AppLauncher ready (SpaceCenter/Flight).
        /// </summary>
        private void InitializeAppLauncherButton()
        {
            if (appLauncherButton != null || ApplicationLauncher.Instance == null)
                return;

            // Ensure we have a valid texture
            if (btnTexture == null)
            {
                btnTexture = GameDatabase.Instance.GetTexture("KerbalJournalMod/Icons/Journal", false);
                if (btnTexture == null)
                {
                    Debug.LogError("[KerbalJournal] Failed to load button texture at 'KerbalJournalMod/Icons/Journal'. Please ensure the texture exists.");
                    return;
                }
            }

            appLauncherButton = ApplicationLauncher.Instance.AddModApplication(
                onTrue: ShowWindow,
                onFalse: HideWindow,
                onHover: null,
                onHoverOut: null,
                onEnable: null,
                onDisable: null,
                visibleInScenes: ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.FLIGHT,
                texture: btnTexture
            );
        }

        private void ShowWindow()
        {
            showGUI = true;
            // Reset to Main state whenever the GUI is opened
            currentState = JournalUIState.Main;
        }

        private void HideWindow()
        {
            showGUI = false;
            // Reset the toolbar button state to not pressed
            if (appLauncherButton != null)
            {
                appLauncherButton.SetFalse(false);
            }
        }

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
                    alignment = TextAnchor.MiddleLeft, // Left-aligned
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
                    wordWrap = true,
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
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
            }
            if (bodyLabelStyle == null)
            {
                bodyLabelStyle = new GUIStyle(HighLogic.Skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
            }

            // Additional style for center alignment (header & row)
            if (centerHeaderStyle == null)
            {
                centerHeaderStyle = new GUIStyle(columnHeaderStyle)
                {
                    alignment = TextAnchor.MiddleCenter
                };
            }
            if (centerRowLabelStyle == null)
            {
                centerRowLabelStyle = new GUIStyle(rowLabelStyle)
                {
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }

        public void OnGUI()
        {
            if (!showGUI) return;

            // Ensure the window stays within screen bounds
            windowRect.x = Mathf.Clamp(windowRect.x, 0, Screen.width - windowRect.width);
            windowRect.y = Mathf.Clamp(windowRect.y, 0, Screen.height - windowRect.height);

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
                Rect centeredRect = new Rect(
                    windowRect.x + (windowRect.width / 2) - (confirmRect.width / 2),
                    windowRect.y + (windowRect.height / 2) - (confirmRect.height / 2),
                    confirmRect.width,
                    confirmRect.height
                );

                GUI.ModalWindow(
                    999999,
                    centeredRect,
                    ConfirmDeleteWindow,
                    "Confirm Deletion",
                    HighLogic.Skin.window
                );
            }

            // Confirm Lock
            if (showLockConfirmDialog)
            {
                Rect centeredRect = new Rect(
                    windowRect.x + (windowRect.width / 2) - (confirmRect.width / 2),
                    windowRect.y + (windowRect.height / 2) - (confirmRect.height / 2),
                    confirmRect.width,
                    confirmRect.height
                );

                GUI.ModalWindow(
                    999998,
                    centeredRect,
                    ConfirmLockWindow,
                    "Confirm Lock",
                    HighLogic.Skin.window
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
                        if (GUILayout.Button("Create Journal", HighLogic.Skin.button, GUILayout.Width(150), GUILayout.Height(buttonHeight)))
                        {
                            currentState = JournalUIState.Create;
                            newJournalKerbal = "";
                            newJournalTitle = "";
                            newJournalBody = "";
                        }
                        GUILayout.Space(10);

                        string mainSortLabel = $"SORT: {mainSortOptions[mainSortOption]}";
                        if (GUILayout.Button(mainSortLabel, HighLogic.Skin.button, GUILayout.Width(200), GUILayout.Height(buttonHeight)))
                        {
                            mainSortOption = (mainSortOption + 1) % mainSortOptions.Length;
                        }
                        GUILayout.Space(10);

                        if (GUILayout.Button("Close", HighLogic.Skin.button, GUILayout.Width(120), GUILayout.Height(buttonHeight)))
                        {
                            HideWindow();
                        }
                        break;
                    }

                case JournalUIState.ListJournals:
                    {
                        if (GUILayout.Button("Create Journal", HighLogic.Skin.button, GUILayout.Width(150), GUILayout.Height(buttonHeight)))
                        {
                            currentState = JournalUIState.Create;
                            newJournalKerbal = "";
                            newJournalTitle = "";
                            newJournalBody = "";
                        }
                        GUILayout.Space(10);

                        string listSortLabel = $"SORT: {listSortOptions[listSortOption]}";
                        if (GUILayout.Button(listSortLabel, HighLogic.Skin.button, GUILayout.Width(200), GUILayout.Height(buttonHeight)))
                        {
                            listSortOption = (listSortOption + 1) % listSortOptions.Length;
                        }
                        GUILayout.Space(10);

                        if (GUILayout.Button("Return", HighLogic.Skin.button, GUILayout.Width(120), GUILayout.Height(buttonHeight)))
                        {
                            currentState = JournalUIState.Main;
                        }
                        break;
                    }

                case JournalUIState.View:
                    {
                        if (selectedEntry != null && !selectedEntry.IsLocked)
                        {
                            if (GUILayout.Button("Edit", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(buttonHeight)))
                            {
                                editJournalTitle = selectedEntry.Title;
                                editJournalBody = selectedEntry.Body;
                                currentState = JournalUIState.Edit;
                            }
                            GUILayout.Space(10);

                            if (GUILayout.Button("Lock Journal", HighLogic.Skin.button, GUILayout.Width(150), GUILayout.Height(buttonHeight)))
                            {
                                showLockConfirmDialog = true;
                            }
                            GUILayout.Space(10);
                        }

                        if (GUILayout.Button("Delete", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(buttonHeight)))
                        {
                            showConfirmDialog = true;
                            confirmDeleteIndex = (selectedEntry != null) ? selectedEntry.Index : -1;
                        }
                        GUILayout.Space(10);

                        if (GUILayout.Button("Return", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(buttonHeight)))
                        {
                            currentState = JournalUIState.ListJournals;
                        }
                        break;
                    }

                case JournalUIState.Edit:
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Save", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(30)))
                        {
                            SaveEditedJournal();
                        }
                        GUILayout.Space(10);

                        if (GUILayout.Button("Return", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(30)))
                        {
                            currentState = JournalUIState.View;
                        }

                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        break;
                    }

                case JournalUIState.Create:
                    {
                        if (GUILayout.Button("Save Journal", HighLogic.Skin.button, GUILayout.Width(150), GUILayout.Height(buttonHeight)))
                        {
                            if (!string.IsNullOrEmpty(newJournalTitle) && !string.IsNullOrEmpty(newJournalBody) && !string.IsNullOrEmpty(newJournalKerbal))
                            {
                                if (JournalScenario.Instance != null)
                                {
                                    string dateStr = DateUtility.GetCurrentKSPDate();
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
                                Debug.LogWarning("[KerbalJournal] Subject, Body, and Kerbal must not be empty.");
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
                GUILayout.Label("No journals have been found, please create a journal.", noJournalStyle, GUILayout.Width(500));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            // Header
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Kerbal Journal Management System", headerStyle, GUILayout.Height(40));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // Sort
            List<KerbalJournalData> sortedKerbals = JournalScenario.Instance.KerbalJournals.Values.ToList();
            switch (mainSortOption)
            {
                case 0: // A to Z
                    sortedKerbals = sortedKerbals.OrderBy(k => k.KerbalName).ToList();
                    break;
                case 1: // Z to A
                    sortedKerbals = sortedKerbals.OrderByDescending(k => k.KerbalName).ToList();
                    break;
                case 2: // Journals (Lowest First)
                    sortedKerbals = sortedKerbals.OrderBy(k => k.Entries.Count).ToList();
                    break;
                case 3: // Journals (Highest First)
                    sortedKerbals = sortedKerbals.OrderByDescending(k => k.Entries.Count).ToList();
                    break;
            }

            // Scroll Container
            scrollKerbalList = GUILayout.BeginScrollView(scrollKerbalList, GUILayout.Width(580), GUILayout.Height(500));

            // Column Headers
            GUILayout.BeginHorizontal();
            GUILayout.Label("Kerbal Name", columnHeaderStyle, GUILayout.Width(150));
            GUILayout.Label("Number of Journals", columnHeaderStyle, GUILayout.Width(150));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            // Rows
            foreach (var kd in sortedKerbals)
            {
                if (kd.Entries.Count > 0)
                {
                    GUILayout.BeginHorizontal(HighLogic.Skin.box, GUILayout.ExpandWidth(true));
                    GUILayout.Label(kd.KerbalName, rowLabelStyle, GUILayout.Width(150));
                    GUILayout.Label(kd.Entries.Count.ToString(), rowLabelStyle, GUILayout.Width(150));

                    GUILayout.Space(3);
                    GUILayout.FlexibleSpace();

                    // "View Journals" button
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
                GUILayout.BeginVertical();
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("No entries for this Kerbal yet.", rowLabelStyle, GUILayout.Width(500));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(20);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Return", HighLogic.Skin.button, GUILayout.Width(100), GUILayout.Height(30)))
                {
                    currentState = JournalUIState.Main;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            // Sort
            List<KerbalJournalEntry> sortedEntries =
                (listSortOption == 0)
                ? entries.OrderByDescending(e => e.Index).ToList() // Newest First
                : entries.OrderBy(e => e.Index).ToList();          // Oldest First

            // Scroll Container
            scrollJournalList = GUILayout.BeginScrollView(scrollJournalList, GUILayout.Width(580), GUILayout.Height(500));

            // Column Headers
            GUILayout.BeginHorizontal();
            GUILayout.Label("Date", columnHeaderStyle, GUILayout.Width(140));
            GUILayout.Label("Journal #", columnHeaderStyle, GUILayout.Width(100));
            GUILayout.Label("Title", columnHeaderStyle, GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            // Rows
            foreach (var entry in sortedEntries)
            {
                GUILayout.BeginHorizontal(HighLogic.Skin.box, GUILayout.ExpandWidth(true));

                string displayDate = DateUtility.ConvertLegacyDate(entry.Date);
                GUILayout.Label(displayDate, rowLabelStyle, GUILayout.Width(140));

                GUILayout.Label(entry.Index.ToString("D3"), rowLabelStyle, GUILayout.Width(100));
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

            string headerText = $"{selectedKerbal} LOG {selectedEntry.Index:D3}";
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(headerText, headerStyle, GUILayout.Height(40));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.Label($"Crew Member: {selectedKerbal}", columnHeaderStyle);
            string displayDate = DateUtility.ConvertLegacyDate(selectedEntry.Date);
            GUILayout.Label($"Date: {displayDate}", columnHeaderStyle);
            GUILayout.Label($"Subject: {selectedEntry.Title}", columnHeaderStyle);

            GUILayout.Space(10);

            scrollBodyView = GUILayout.BeginScrollView(scrollBodyView, GUILayout.Height(320));
            GUILayout.Label(selectedEntry.Body, bodyLabelStyle);
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

            string tempEditTitle = GUILayout.TextField(editJournalTitle, HighLogic.Skin.textField, GUILayout.Height(30));
            if (tempEditTitle.Length <= 65)
            {
                editJournalTitle = tempEditTitle;
            }
            else
            {
                editJournalTitle = tempEditTitle.Substring(0, 65);
            }
            GUILayout.Label($"Characters remaining: {65 - editJournalTitle.Length}", rowLabelStyle);

            GUILayout.Space(10);

            GUILayout.Label("Body:", columnHeaderStyle);

            scrollEditBody = GUILayout.BeginScrollView(scrollEditBody, GUILayout.Height(320));
            editJournalBody = GUILayout.TextArea(editJournalBody, HighLogic.Skin.textArea, GUILayout.ExpandHeight(true));
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

                GUILayout.Label("Subject:", columnHeaderStyle);
                string tempTitle = GUILayout.TextField(newJournalTitle, HighLogic.Skin.textField, GUILayout.Height(30));
                if (tempTitle.Length <= 65)
                {
                    newJournalTitle = tempTitle;
                }
                else
                {
                    newJournalTitle = tempTitle.Substring(0, 65);
                }
                GUILayout.Label($"Characters remaining: {65 - newJournalTitle.Length}", rowLabelStyle);

                GUILayout.Space(10);

                GUILayout.Label("Journal Body:", columnHeaderStyle);

                scrollCreateJournal = GUILayout.BeginScrollView(scrollCreateJournal, GUILayout.Height(180));
                newJournalBody = GUILayout.TextArea(newJournalBody, HighLogic.Skin.textArea, GUILayout.ExpandHeight(true));
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
                if (JournalScenario.Instance != null && !string.IsNullOrEmpty(selectedKerbal) && confirmDeleteIndex > 0)
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
                    Debug.Log($"[KerbalJournal] Journal #{selectedEntry.Index} for Kerbal {selectedKerbal} has been locked.");
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

        private void SaveEditedJournal()
        {
            if (selectedEntry != null)
            {
                selectedEntry.Title = editJournalTitle;
                selectedEntry.Body = editJournalBody;
                Debug.Log($"[KerbalJournal] Journal #{selectedEntry.Index} for Kerbal {selectedKerbal} has been edited.");
            }
            currentState = JournalUIState.View;
        }

        #endregion
    }
}

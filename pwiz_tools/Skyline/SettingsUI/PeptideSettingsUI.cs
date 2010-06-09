﻿/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class PeptideSettingsUI : Form
    {
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
        private enum TABS { Digest, Prediction, Filter, Library, Modifications }
// ReSharper restore UnusedMember.Local
// ReSharper restore InconsistentNaming

        private readonly SkylineWindow _parent;
        private readonly LibraryManager _libraryManager;
        private PeptideSettings _peptideSettings;
        private IEnumerable<LibrarySpec> _eventChosenLibraries;
        private PeptideRankId _lastRankId;
        private string _lastPeptideCount;

        private readonly SettingsListComboDriver<Enzyme> _driverEnzyme;
        private readonly SettingsListComboDriver<RetentionTimeRegression> _driverRT;
        private readonly SettingsListBoxDriver<PeptideExcludeRegex> _driverExlusion;
        private readonly SettingsListBoxDriver<LibrarySpec> _driverLibrary;
        private readonly SettingsListComboDriver<BackgroundProteomeSpec> _driverBackgroundProteome;
        private readonly SettingsListBoxDriver<StaticMod> _driverStaticMod;
        private readonly SettingsListBoxDriver<StaticMod> _driverHeavyMod;
        private readonly LabelTypeComboDriver _driverLabelType;
        private readonly MessageBoxHelper _helper;

        public PeptideSettingsUI(SkylineWindow parent, LibraryManager libraryManager)
        {
            InitializeComponent();

            _helper = new MessageBoxHelper(this);

            _parent = parent;
            _libraryManager = libraryManager;
            _peptideSettings = parent.DocumentUI.Settings.PeptideSettings;

            // Initialize digestion settings
            _driverEnzyme = new SettingsListComboDriver<Enzyme>(comboEnzyme, Settings.Default.EnzymeList);
            _driverEnzyme.LoadList(_peptideSettings.Enzyme.GetKey());
            cbMissedCleavages.SelectedItem = Digest.MaxMissedCleavages.ToString();
            if (cbMissedCleavages.SelectedIndex < 0)
                cbMissedCleavages.SelectedIndex = 0;
            cbRaggedEnds.Checked = Digest.ExcludeRaggedEnds;

            // Initialize prediction settings
            _driverRT = new SettingsListComboDriver<RetentionTimeRegression>(comboRetentionTime, Settings.Default.RetentionTimeList);
            string sel = (Prediction.RetentionTime == null ? null : Prediction.RetentionTime.Name);
            _driverRT.LoadList(sel);
            cbUseMeasuredRT.Checked = textMeasureRTWindow.Enabled = Prediction.UseMeasuredRTs;
            if (Prediction.MeasuredRTWindow.HasValue)
                textMeasureRTWindow.Text = Prediction.MeasuredRTWindow.ToString();

            // Initialize filter settings
            _driverExlusion = new SettingsListBoxDriver<PeptideExcludeRegex>(listboxExclusions, Settings.Default.PeptideExcludeList);
            _driverExlusion.LoadList(null, Filter.Exclusions);

            textExcludeAAs.Text = Filter.ExcludeNTermAAs.ToString();
            textMaxLength.Text = Filter.MaxPeptideLength.ToString();
            textMinLength.Text = Filter.MinPeptideLength.ToString();
            cbAutoSelect.Checked = Filter.AutoSelect;

            // Initialize spectral library settings
            _driverLibrary = new SettingsListBoxDriver<LibrarySpec>(listLibraries, Settings.Default.SpectralLibraryList);
            IList<LibrarySpec> listLibrarySpecs = Libraries.LibrarySpecs;

            _driverLibrary.LoadList(null, listLibrarySpecs);
            _driverBackgroundProteome = new SettingsListComboDriver<BackgroundProteomeSpec>(comboBackgroundProteome, Settings.Default.BackgroundProteomeList);
            _driverBackgroundProteome.LoadList(_peptideSettings.BackgroundProteome.Name);

            panelPick.Visible = listLibrarySpecs.Count > 0;
            btnExplore.Enabled = listLibraries.Items.Count > 0;

            comboMatching.SelectedIndex = (int)Libraries.Pick;

            _lastRankId = Libraries.RankId;
            _lastPeptideCount = Libraries.PeptideCount.ToString();

            UpdateRanks(null);

            // Initialize modification settings
            _driverStaticMod = new SettingsListBoxDriver<StaticMod>(listStaticMods, Settings.Default.StaticModList);
            _driverStaticMod.LoadList(null, Modifications.StaticModifications);
            _driverHeavyMod = new SettingsListBoxDriver<StaticMod>(listHeavyMods, Settings.Default.HeavyModList);
            _driverLabelType = new LabelTypeComboDriver(comboLabelType, Modifications, _driverHeavyMod, 
                labelStandardType, comboStandardType, listStandardTypes);
        }

        public DigestSettings Digest { get { return _peptideSettings.DigestSettings; } }
        public PeptidePrediction Prediction { get { return _peptideSettings.Prediction; } }
        public PeptideFilter Filter { get { return _peptideSettings.Filter; } }
        public PeptideLibraries Libraries { get { return _peptideSettings.Libraries; } }
        public PeptideModifications Modifications { get { return _peptideSettings.Modifications; } }

        protected override void OnShown(EventArgs e)
        {
            tabControl1.FocusFirstTabStop();
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();

            // Validate and hold digestion settings
            Enzyme enzyme = Settings.Default.GetEnzymeByName(comboEnzyme.SelectedItem.ToString());
            Helpers.AssignIfEquals(ref enzyme, _peptideSettings.Enzyme);

            int maxMissedCleavages =
                int.Parse(cbMissedCleavages.SelectedItem.ToString());
            bool excludeRaggedEnds = cbRaggedEnds.Checked;
            DigestSettings digest = new DigestSettings(maxMissedCleavages, excludeRaggedEnds);
            Helpers.AssignIfEquals(ref digest, Digest);
            
            // Validate and hold prediction settings
            string nameRT = comboRetentionTime.SelectedItem.ToString();
            RetentionTimeRegression retentionTime =
                Settings.Default.GetRetentionTimeByName(nameRT);
            bool useMeasuredRT = cbUseMeasuredRT.Checked;
            double? measuredRTWindow = null;
            if (!string.IsNullOrEmpty(textMeasureRTWindow.Text))
            {
                double measuredRTWindowOut;
                const double minWindow = PeptidePrediction.MIN_MEASURED_RT_WINDOW;
                const double maxWindow = PeptidePrediction.MAX_MEASURED_RT_WINDOW;
                if (!_helper.ValidateDecimalTextBox(e, tabControl1, (int) TABS.Prediction,
                        textMeasureRTWindow, minWindow, maxWindow, out measuredRTWindowOut))
                    return;
                measuredRTWindow = measuredRTWindowOut;
            }
            PeptidePrediction prediction = new PeptidePrediction(retentionTime, useMeasuredRT, measuredRTWindow);
            Helpers.AssignIfEquals(ref prediction, Prediction);

            // Validate and hold filter settings
            int excludeNTermAAs;
            if (!_helper.ValidateNumberTextBox(e, tabControl1, (int) TABS.Filter, textExcludeAAs,
                    PeptideFilter.MIN_EXCLUDE_NTERM_AA, PeptideFilter.MAX_EXCLUDE_NTERM_AA, out excludeNTermAAs))
                return;
            int minPeptideLength;
            if (!_helper.ValidateNumberTextBox(e, tabControl1, (int) TABS.Filter, textMinLength,
                    PeptideFilter.MIN_MIN_LENGTH, PeptideFilter.MAX_MIN_LENGTH, out minPeptideLength))
                return;
            int maxPeptideLength;
            if (!_helper.ValidateNumberTextBox(e, tabControl1, (int)TABS.Filter, textMaxLength,
                    Math.Max(PeptideFilter.MIN_MAX_LENGTH, minPeptideLength), PeptideFilter.MAX_MAX_LENGTH, out maxPeptideLength))
                return;

            PeptideExcludeRegex[] exclusions = _driverExlusion.Chosen;

            bool autoSelect = cbAutoSelect.Checked;
            PeptideFilter filter = new PeptideFilter(excludeNTermAAs,
                                                        minPeptideLength,
                                                        maxPeptideLength,
                                                        exclusions,
                                                        autoSelect
                                                        );
            Helpers.AssignIfEquals(ref filter, Filter);

            // Validate and hold libraries
            PeptideLibraries libraries;
            IList<LibrarySpec> librarySpecs = _driverLibrary.Chosen;
            if (librarySpecs.Count == 0)
                libraries = new PeptideLibraries(PeptidePick.library, null, null, librarySpecs, new Library[0]);
            else
            {
                int? peptideCount = null;
                if (cbLimitPeptides.Checked)
                {
                    int peptideCountVal;
                    if (!_helper.ValidateNumberTextBox(e, textPeptideCount, PeptideLibraries.MIN_PEPTIDE_COUNT,
                            PeptideLibraries.MAX_PEPTIDE_COUNT, out peptideCountVal))
                        return;
                    peptideCount = peptideCountVal;
                }
                PeptidePick pick = (PeptidePick) comboMatching.SelectedIndex;

                IList<Library> librariesLoaded = new Library[librarySpecs.Count];
                if (Libraries != null)
                {
                    // Use existing library spec's, if nothing was changed.
                    // Avoid changing the libraries, just because the the picking
                    // algorithm changed.
                    if (ArrayUtil.EqualsDeep(librarySpecs, Libraries.LibrarySpecs))
                    {
                        librarySpecs = Libraries.LibrarySpecs;
                        librariesLoaded = Libraries.Libraries;
                    }
                    // Otherwise, leave the list of loaded libraries empty,
                    // and let the LibraryManager refill it.  This ensures a
                    // clean save of library specs only in the user config, rather
                    // than a mix of library specs and libraries.
                }

                PeptideRankId rankId = (PeptideRankId) comboRank.SelectedItem;
                if (comboRank.SelectedIndex == 0)
                    rankId = null;

                libraries = new PeptideLibraries(pick, rankId, peptideCount, librarySpecs, librariesLoaded);
            }
            Helpers.AssignIfEquals(ref libraries, Libraries);

            // Validate and hold modifications
            var standardTypes = _driverLabelType.InternalStandardTypes;
            if (standardTypes.Count < 1)
            {
                MessageDlg.Show(this, "Choose at least one internal standard type.");
                e.Cancel = true;
                return;
            }
            PeptideModifications modifications = new PeptideModifications(
                _driverStaticMod.Chosen, _driverLabelType.GetHeavyModifications(),
                standardTypes);
            // Should not be possible to change explicit modifications in the background,
            // so this should be safe.  CONSIDER: Document structure because of a library load?
            modifications = modifications.DeclareExplicitMods(_parent.DocumentUI,
                Settings.Default.StaticModList, Settings.Default.HeavyModList);
            Helpers.AssignIfEquals(ref modifications, _peptideSettings.Modifications);
            var backgroundProteomeSpec =
                Settings.Default.BackgroundProteomeList.GetBackgroundProteomeSpec(
                    (string) _driverBackgroundProteome.Combo.SelectedItem);           
            BackgroundProteome backgroundProteome = BackgroundProteome.NONE;
            if (!backgroundProteomeSpec.IsNone)
            {
                backgroundProteome = new BackgroundProteome(backgroundProteomeSpec, true);
                if (backgroundProteome.DatabaseInvalid)
                {
                    MessageDlg.Show(this, string.Format("Failed to load background proteome {0}.\nThe file {1} may not be a valid proteome file.",
                        backgroundProteomeSpec.Name, backgroundProteomeSpec.DatabasePath));
                    tabControl1.SelectedIndex = 0;
                    _driverBackgroundProteome.Combo.Focus();
                    e.Cancel = true;
                    return;
                }
            }
                
            Helpers.AssignIfEquals(ref backgroundProteome, _peptideSettings.BackgroundProteome);
            PeptideSettings settings = new PeptideSettings(enzyme, digest, prediction,
                    filter, libraries, modifications, backgroundProteome);

            // Only update, if anything changed
            if (!Equals(settings, _peptideSettings))
            {
                SrmSettings newSettings = _parent.DocumentUI.Settings.ChangePeptideSettings(settings);
                if (!_parent.ChangeSettings(newSettings, true))
                {
                    return;
                }
                _peptideSettings = settings;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void enzyme_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverEnzyme.SelectedIndexChangedEvent(sender, e);
        }

        private void comboRetentionTime_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverRT.SelectedIndexChangedEvent(sender, e);
        }
        private void comboBackgroundProteome_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverBackgroundProteome.SelectedIndexChangedEvent(sender, e);
        }

        private void cbUseMeasuredRT_CheckedChanged(object sender, EventArgs e)
        {
            bool enable = cbUseMeasuredRT.Checked;
            textMeasureRTWindow.Enabled = enable;
            // If disabling the text box, and it has content, make sure it is
            // valid content.  Otherwise, clear the current content, which
            // is always valid, if the measured RT values will not be used.
            if (!enable && !string.IsNullOrEmpty(textMeasureRTWindow.Text))
            {
                double measuredRTWindow;
                if (!double.TryParse(textMeasureRTWindow.Text, out measuredRTWindow) ||
                        PeptidePrediction.MIN_MEASURED_RT_WINDOW > measuredRTWindow ||
                        measuredRTWindow > PeptidePrediction.MAX_MEASURED_RT_WINDOW)
                {
                    textMeasureRTWindow.Text = "";
                }                
            }
        }

        private void btnEditExlusions_Click(object sender, EventArgs e)
        {
            _driverExlusion.EditList();
        }

        private void editLibraries_Click(object sender, EventArgs e)
        {
            EditLibraryList();
        }

        public void EditLibraryList()
        {
            _driverLibrary.EditList();

            panelPick.Visible = listLibraries.CheckedIndices.Count > 0;
            btnExplore.Enabled = listLibraries.Items.Count > 0;
        }

        private void btnBuildLibrary_Click(object sender, EventArgs e)
        {
            ShowBuildLibraryDlg();
        }

        public void ShowBuildLibraryDlg()
        {
            var dlg = new BuildLibraryDlg();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _libraryManager.BuildLibrary(_parent, dlg.Builder, _parent.LibraryBuildCompleteCallback, dlg.Builder.LibrarySpec.Name);

                Settings.Default.SpectralLibraryList.Add(dlg.Builder.LibrarySpec);
                _driverLibrary.LoadList();
                LaunchBuildNotficationThread(dlg.Builder.LibrarySpec.Name);
            }
        }

        private void listLibraries_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.NewValue == CheckState.Checked && listLibraries.CheckedItems.Count == 0)
                panelPick.Visible = true;
            else if (e.NewValue == CheckState.Unchecked && listLibraries.CheckedItems.Count == 1)
                panelPick.Visible = false;

            // Only update ranks, if they are enabled
            int match = comboMatching.SelectedIndex;
            if (match == (int) PeptidePick.library || match == (int) PeptidePick.both)
                UpdateRanks(e);
        }

        private void comboMatching_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboMatching.SelectedIndex)
            {
                case (int)PeptidePick.library:
                case (int)PeptidePick.both:
                    UpdateRanks(null);
                    if (IsValidRankId(_lastRankId, _driverLibrary.Chosen))
                        comboRank.SelectedItem = _lastRankId;
                    comboRank.Enabled = true;
                    break;

                case (int)PeptidePick.filter:
                case (int)PeptidePick.either:
                    if (comboRank.SelectedIndex != -1)
                        _lastRankId = (PeptideRankId)comboRank.SelectedItem;
                    comboRank.SelectedIndex = -1;
                    comboRank.Enabled = false;
                    break;
            }
        }

        private void UpdateRanks(ItemCheckEventArgs e)
        {
            // Store selection, if there is one.
            if (comboRank.SelectedIndex != -1)
                _lastRankId = (PeptideRankId) comboRank.SelectedItem;

            PeptideRankId rankId = _lastRankId;

            _eventChosenLibraries = _driverLibrary.GetChosen(e);
            try
            {
                // Recalculate possible ranks from selected libraries
                comboRank.Items.Clear();
                comboRank.Items.Add(PeptideRankId.PEPTIDE_RANK_NONE);

                HashSet<PeptideRankId> rankIdSet = new HashSet<PeptideRankId>();
                foreach (LibrarySpec spec in _eventChosenLibraries)
                {
                    // If not all libraries contain the most recently selected
                    // rank ID, then leave the default of no rank ID.
                    if (!spec.PeptideRankIds.Contains(rankId))
                        rankId = null;

                    rankIdSet.UnionWith(spec.PeptideRankIds);
                }
                PeptideRankId[] rankIds = rankIdSet.ToArray();
                Array.Sort(rankIds, (id1, id2) => Comparer<string>.Default.Compare(id1.Label, id2.Label));
                comboRank.Items.AddRange(rankIds);

                // Restore selection
                if (rankId != null)
                    comboRank.SelectedItem = rankId;
                comboRank_SelectedIndexChanged(this, new EventArgs());
            }
            finally
            {
                _eventChosenLibraries = null;
            }
        }

        private void comboRank_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboRank.SelectedIndex < 1)
            {
                cbLimitPeptides.Enabled = false;
                if (cbLimitPeptides.Checked)
                    cbLimitPeptides.Checked = false;
                else
                    cbLimitPeptides_CheckedChanged(this, new EventArgs());
            }
            else
            {
                // Make sure all libraries have this rank ID.
                PeptideRankId rankId = (PeptideRankId)comboRank.SelectedItem;
                IEnumerable<LibrarySpec> chosen = _eventChosenLibraries ?? _driverLibrary.Chosen;
                if (!IsValidRankId(rankId, chosen))
                {
                    if (MessageBox.Show(string.Format("Not all libraries chosen support the '{0}' ranking for peptides.\nDo you want to uncheck the ones that do not?", rankId),
                            Program.Name, MessageBoxButtons.OKCancel) == DialogResult.OK)
                    {
                        foreach (int i in listLibraries.CheckedIndices)
                        {
                            if (!_driverLibrary.List[i].PeptideRankIds.Contains(rankId))
                                listLibraries.SetItemChecked(i, false);
                        }
                    }
                    else
                    {
                        comboRank.SelectedIndex = -1;
                        return;
                    }
                }
                cbLimitPeptides.Enabled = true;
                if (!string.IsNullOrEmpty(_lastPeptideCount))
                    cbLimitPeptides.Checked = true;
            }
        }

        private static bool IsValidRankId(PeptideRankId rankId, IEnumerable<LibrarySpec> chosen)
        {
            foreach (LibrarySpec spec in chosen)
            {
                if (!spec.PeptideRankIds.Contains(rankId))
                    return false;
            }
            return true;            
        }

        private void cbLimitPeptides_CheckedChanged(object sender, EventArgs e)
        {
            if (cbLimitPeptides.Checked)
            {
                if (!string.IsNullOrEmpty(_lastPeptideCount))
                    textPeptideCount.Text = _lastPeptideCount;

                textPeptideCount.Enabled = true;
                labelPeptides.Enabled = true;
            }
            else
            {
                if (!string.IsNullOrEmpty(textPeptideCount.Text))
                    _lastPeptideCount = textPeptideCount.Text;

                labelPeptides.Enabled = false;
                textPeptideCount.Enabled = false;
                textPeptideCount.Text = "";
            }
        }

        private void textPeptideCount_TextChanged(object sender, EventArgs e)
        {
            // If the control is enabled, then the user is changing this value,
            // so the saved value is cleared.
            if (textPeptideCount.Enabled)
                _lastPeptideCount = null;
        }

        private void btnExplore_Click(object sender, EventArgs e)
        {
            ShowViewLibraryDlg();
        }

        public void ShowViewLibraryDlg()
        {
            ShowViewLibraryDlg(String.Empty);
        }

        public void ShowViewLibraryDlg(String libName)
        {
            var dlg = new ViewLibraryDlg(_libraryManager, _driverLibrary, libName);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
            }
        }

        private void comboLabelType_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Handle label type selection events, like <Edit list...>
            if (_driverLabelType != null)
                _driverLabelType.SelectedIndexChangedEvent();
        }

        public void EditLabelTypeList ()
        {
            _driverLabelType.EditList();
        }

        private void btnEditStaticMods_Click(object sender, EventArgs e)
        {
            EditStaticMods();
        }

        public void EditStaticMods()
        {
            _driverStaticMod.EditList();
        }

        private void btnEditHeavyMods_Click(object sender, EventArgs e)
        {
            EditHeavyMods();
        }

        public void EditHeavyMods()
        {
            _driverHeavyMod.EditList();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void LaunchBuildNotficationThread(String libraryName)
        {
            Thread th = new Thread(new ThreadStart(delegate
            {
                BuildLibraryNotification frm = new BuildLibraryNotification(_parent, libraryName);
                frm.BuildNotificationThread();
            }));
            th.Name = "BuildLibraryNotification";
            th.SetApartmentState(ApartmentState.STA);
            th.IsBackground = true;
            th.Start();
        }
        
        #region Functional testing support

        public void ShowBuildBackgroundProteomeDlg()
        {
            var dlg = new BuildBackgroundProteomeDlg(_driverBackgroundProteome.List);
            if (dlg.ShowDialog(this) == DialogResult.Cancel)
            {
                return;
            }
            Settings.Default.BackgroundProteomeList.Add(dlg.BackgroundProteomeSpec);
            _driverBackgroundProteome.LoadList(dlg.BackgroundProteomeSpec.Name);
        }

        public int MissedCleavages
        { 
            get { return Convert.ToInt32(cbMissedCleavages.SelectedItem); }
            set { cbMissedCleavages.SelectedItem = value.ToString(); }
        }

        public string[] AvailableLibraries
        {
            get
            {
                var availableLibraries = new List<string>();
                for (int i = 0; i < listLibraries.Items.Count; i++)
                    availableLibraries.Add(listLibraries.Items[i].ToString());
                return availableLibraries.ToArray();
            }
        }

        public string[] PickedLibraries
        {
            get { return _driverLibrary.CheckedNames; }
            set { _driverLibrary.CheckedNames = value; }
        }

        public string[] PickedStaticMods
        {
            get { return _driverStaticMod.CheckedNames; }
            set { _driverStaticMod.CheckedNames = value;}
        }

        public string[] PickedHeavyMods
        {
            get { return _driverHeavyMod.CheckedNames; }
            set { _driverHeavyMod.CheckedNames = value; }
        }

        public string SelectedLabelTypeName
        {
            get { return _driverLabelType.SelectedName; }
            set { _driverLabelType.SelectedName = value; }
        }

        #endregion

        private sealed class LabelTypeComboDriver
        {
            private const string EDIT_LIST_ITEM = "<Edit list...>";

            private readonly SettingsListBoxDriver<StaticMod> _driverHeavyMod;

            private int _selectedIndexLast;
            private bool _singleStandard;

            public LabelTypeComboDriver(ComboBox combo,
                                        PeptideModifications modifications,
                                        SettingsListBoxDriver<StaticMod> driverHeavyMod,
                                        Label labelIS,
                                        ComboBox comboIS,
                                        CheckedListBox listBoxIS)
            {
                _driverHeavyMod = driverHeavyMod;

                LabelIS = labelIS;
                Combo = combo;
                Combo.DisplayMember = "LabelType";
                ComboIS = comboIS;
                ListBoxIS = listBoxIS;
                LoadList(null, modifications.InternalStandardTypes,
                    modifications.GetHeavyModifications());
                ShowModifications();
            }

            private ComboBox Combo { get; set; }
            private ComboBox ComboIS { get; set; }
            private CheckedListBox ListBoxIS { get; set; }
            private Label LabelIS { get; set; }

            public IList<IsotopeLabelType> InternalStandardTypes
            {
                get
                {
                    if (_singleStandard)
                        return new[] {(IsotopeLabelType) ComboIS.SelectedItem};
                    else
                    {
                        var listStandardTypes = new List<IsotopeLabelType>();
                        foreach (IsotopeLabelType labelType in ListBoxIS.CheckedItems)
                            listStandardTypes.Add(labelType);
                        return listStandardTypes.ToArray();
                    }
                }
            }

            private void LoadList(string selectedItemLast, ICollection<IsotopeLabelType> internalStandardTypes,
                IEnumerable<TypedModifications> heavyMods)
            {
                try
                {
                    Combo.BeginUpdate();
                    ComboIS.BeginUpdate();
                    Combo.Items.Clear();

                    _singleStandard = (heavyMods.Count() <= 1);
                    if (_singleStandard)
                    {
                        LabelIS.Text = "I&nternal standard type:";
                        ComboIS.Items.Clear();
                        ComboIS.Items.Add(IsotopeLabelType.light);
                        if (internalStandardTypes.Contains(IsotopeLabelType.light))
                            ComboIS.SelectedIndex = 0;
                        ComboIS.Visible = true;
                        ListBoxIS.Visible = false;
                        ComboIS.Parent.Parent.Parent.Height +=
                            ComboIS.Bottom + 10 - ComboIS.Parent.Height;
                    }
                    else
                    {
                        LabelIS.Text = "I&nternal standard types:";
                        ListBoxIS.Items.Clear();
                        ListBoxIS.Items.Add(IsotopeLabelType.light);
                        if (internalStandardTypes.Contains(IsotopeLabelType.light))
                            ListBoxIS.SetItemChecked(0, true);
                        ComboIS.Visible = false;
                        ListBoxIS.Visible = true;
                        ListBoxIS.Parent.Parent.Parent.Height +=
                            ListBoxIS.Bottom + 12 - ComboIS.Parent.Height;
                    }

                    foreach (var typedMods in heavyMods)
                    {
                        string labelName = typedMods.LabelType.Name;

                        int i = Combo.Items.Add(typedMods);
                        if (Equals(typedMods.LabelType.Name, selectedItemLast))
                            Combo.SelectedIndex = i;

                        if (_singleStandard)
                        {
                            i = ComboIS.Items.Add(typedMods.LabelType);
                            if (internalStandardTypes.Contains(lt => Equals(lt.Name, labelName)))
                                ComboIS.SelectedIndex = i;
                        }
                        else
                        {
                            i = ListBoxIS.Items.Add(typedMods.LabelType);
                            if (internalStandardTypes.Contains(lt => Equals(lt.Name, labelName)))
                                ListBoxIS.SetItemChecked(i, true);
                        }
                    }

                    Combo.Items.Add(EDIT_LIST_ITEM);
                    if (Combo.SelectedIndex < 0)
                        Combo.SelectedIndex = 0;
                    // If no internal standard selected yet, use the first heavy mod type
                    if (_singleStandard && ComboIS.SelectedIndex == -1)
                        ComboIS.SelectedIndex = (ComboIS.Items.Count > 1 ? 1 : 0);
                    if (!_singleStandard && ListBoxIS.CheckedItems.Count == 0)
                        ListBoxIS.SetItemChecked(ListBoxIS.Items.Count > 1 ? 1 : 0, true);
                }
                finally
                {
                    ComboIS.EndUpdate();
                    Combo.EndUpdate();
                }                
            }

            public string SelectedName
            {
                get { return SelectedMods.LabelType.Name; }
                set
                {
                    for (int i = 0; i < Combo.Items.Count; i++)
                    {
                        if (Equals(value, ((TypedModifications)Combo.Items[i]).LabelType.Name))
                        {
                            Combo.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }

            private TypedModifications SelectedMods
            {
                get { return (TypedModifications) Combo.SelectedItem; }
            }

            private TypedModifications SelectedModsLast
            {
                get { return (TypedModifications)Combo.Items[_selectedIndexLast]; }
            }

            public IEnumerable<TypedModifications> GetHeavyModifications()
            {
                UpdateLastSelected();

                foreach (object item in Combo.Items)
                {
                    if (item is TypedModifications)
                        yield return (TypedModifications) item;                    
                }
            }

            private bool EditListSelected()
            {
                return (EDIT_LIST_ITEM == Combo.SelectedItem.ToString());
            }

            public void SelectedIndexChangedEvent()
            {
                if (_selectedIndexLast != -1)
                {
                    UpdateLastSelected();

                    if (EditListSelected())
                    {
                        EditList();
                    }

                    ShowModifications();
                }

                _selectedIndexLast = Combo.SelectedIndex;
            }

            private void UpdateLastSelected()
            {
                var lastSelectedMods = SelectedModsLast;
                var currentMods = _driverHeavyMod.Chosen;
                if (!ArrayUtil.EqualsDeep(currentMods, lastSelectedMods.Modifications))
                {
                    Combo.Items[_selectedIndexLast] =
                        new TypedModifications(lastSelectedMods.LabelType, currentMods);
                }
            }

            private void ShowModifications()
            {
                // Update the heavy modifications check-list to show the new selection
                _driverHeavyMod.LoadList(SelectedMods.Modifications);
            }

            public void EditList()
            {
                var heavyMods = GetHeavyModifications();
                var dlg = new EditLabelTypeListDlg
                              {
                                  LabelTypes = from typedMods in heavyMods
                                               select typedMods.LabelType
                              };
                if (dlg.ShowDialog(Combo.TopLevelControl) == DialogResult.OK)
                {
                    // Store existing values in dictionary by lowercase name.
                    string selectedItemLast = SelectedModsLast.LabelType.Name;
                    var internalStandardTypes = InternalStandardTypes;
                    var dictHeavyMods = new Dictionary<string, TypedModifications>();
                    foreach (var typedMods in heavyMods)
                        dictHeavyMods.Add(typedMods.LabelType.Name.ToLower(), typedMods);

                    Combo.Items.Clear();

                    // Add new values based on the content of the dialog.
                    // Names that already existed keep same modifications, and
                    // names that have not changed order stay reference-equal.
                    var listHeavyMods = new List<TypedModifications>();
                    foreach (var labelType in dlg.LabelTypes)
                    {
                        TypedModifications typedMods;
                        if (!dictHeavyMods.TryGetValue(labelType.Name.ToLower(), out typedMods))
                            typedMods = new TypedModifications(labelType, new StaticMod[0]);
                        else if (!Equals(labelType, typedMods.LabelType))
                            typedMods = new TypedModifications(labelType, typedMods.Modifications);
                        listHeavyMods.Add(typedMods);
                    }

                    _selectedIndexLast = -1;
                    LoadList(selectedItemLast, internalStandardTypes, listHeavyMods);
                }
                else
                {
                    // Reset the selected index before edit was chosen.
                    Combo.SelectedIndex = _selectedIndexLast;
                }
            }
        }
    }
}
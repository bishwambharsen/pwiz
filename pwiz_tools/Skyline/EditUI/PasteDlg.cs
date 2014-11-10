﻿/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.EditUI
{
    // CONSIDER bspratt: Checkbox for hiding and showing new protein columns
    public partial class PasteDlg : FormEx, IMultipleViewProvider
    {
        private readonly StatementCompletionTextBox _statementCompletionEditBox;
        private bool _noErrors;

        public PasteDlg(IDocumentUIContainer documentUiContainer)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            DocumentUiContainer = documentUiContainer;

            _statementCompletionEditBox = new StatementCompletionTextBox(DocumentUiContainer)
                                              {
                                                  MatchTypes = ProteinMatchType.name | ProteinMatchType.description
                                              };
            _statementCompletionEditBox.SelectionMade += statementCompletionEditBox_SelectionMade;
            gridViewProteins.DataGridViewKey += OnDataGridViewKey;
            gridViewPeptides.DataGridViewKey += OnDataGridViewKey;
            gridViewTransitionList.DataGridViewKey += OnDataGridViewKey;
        }

        void OnDataGridViewKey(object sender, KeyEventArgs e)
        {
            _statementCompletionEditBox.OnKeyPreview(sender, e);
        }

        void statementCompletionEditBox_SelectionMade(StatementCompletionItem statementCompletionItem)
        {
            if (tabControl1.SelectedTab == tabPageProteinList)
            {
                _statementCompletionEditBox.TextBox.Text = statementCompletionItem.ProteinInfo.Name;
                gridViewProteins.EndEdit();
            }
            else if (tabControl1.SelectedTab == tabPagePeptideList)
            {
                _statementCompletionEditBox.TextBox.Text = statementCompletionItem.Peptide;
                if (gridViewPeptides.CurrentRow != null)
                {
                    gridViewPeptides.CurrentRow.Cells[colPeptideProtein.Index].Value 
                        = statementCompletionItem.ProteinInfo.Name;
                }
                gridViewPeptides.EndEdit();    
            }
            else if (tabControl1.SelectedTab == tabPageTransitionList)
            {
                _statementCompletionEditBox.TextBox.Text = statementCompletionItem.Peptide;
                if (gridViewTransitionList.CurrentRow != null)
                {
                    gridViewTransitionList.CurrentRow.Cells[colTransitionProteinName.Index].Value =
                        statementCompletionItem.ProteinInfo.Name;
                }
                gridViewTransitionList.EndEdit();
            }
        }

        public IDocumentUIContainer DocumentUiContainer { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DocumentUiContainer.ListenUI(OnDocumentUIChanged);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            DocumentUiContainer.UnlistenUI(OnDocumentUIChanged);
        }

        private IdentityPath _selectedPath;
        public IdentityPath SelectedPath
        {
            get { return _selectedPath; }
            set
            {
                _selectedPath = value;

                // Handle insert node path
                if (_selectedPath != null &&
                    _selectedPath.Depth == (int)SrmDocument.Level.MoleculeGroups &&
                    ReferenceEquals(_selectedPath.GetIdentity((int)SrmDocument.Level.MoleculeGroups), SequenceTree.NODE_INSERT_ID))
                {
                    _selectedPath = null;
                }
            }
        }

        public string ErrorText
        {
            get { return panelError.Visible ? tbxError.Text : null; }
        }

        public int SelectedGridRow
        {
            get
            {
                var cell = ActiveGridView.CurrentCell;
                return cell != null ? cell.RowIndex : -1;
            }
        }

        public int SelectedGridColumn
        {
            get
            {
                var cell = ActiveGridView.CurrentCell;
                return cell != null ? cell.ColumnIndex : -1;
            }
        }

        private DataGridView ActiveGridView
        {
            get
            {
                return gridViewProteins.Visible
                   ? gridViewProteins
                   : (gridViewPeptides.Visible
                          ? gridViewPeptides
                          : gridViewTransitionList);
            }
        }

        public void ShowError(PasteError pasteError)
        {
            _noErrors = false;
            panelError.Visible = true;
            if (pasteError == null)
            {
                tbxError.Text = string.Empty;
                tbxError.Visible = false;
                return;
            }
            tbxError.BackColor = Color.Red;
            tbxError.Text = pasteError.Message;
        }

        public void ShowNoErrors()
        {
            _noErrors = true;
            panelError.Visible = true;
            tbxError.Text = Resources.PasteDlg_ShowNoErrors_No_errors;
            tbxError.BackColor = Color.LightGreen;
        }

        public void HideNoErrors()
        {
            if (!_noErrors)
            {
                return;
            }
            panelError.Visible = false;
        }

        private void btnValidate_Click(object sender, EventArgs e)
        {
            ValidateCells();
        }

        public void ValidateCells()
        {
            IdentityPath selectedPath = null;
            var document = GetNewDocument(DocumentUiContainer.Document, true, ref selectedPath);
            if (document != null)
                ShowNoErrors();
        }

        private SrmDocument GetNewDocument(SrmDocument document, bool validating, ref IdentityPath selectedPath)
        {
            int emptyPeptideGroups;
            return GetNewDocument(document, validating, ref selectedPath, out emptyPeptideGroups);
        }

        private SrmDocument GetNewDocument(SrmDocument document, bool validating, ref IdentityPath selectedPath, out int emptyPeptideGroups)
        {
            var fastaHelper = new ImportFastaHelper(tbxFasta, tbxError, panelError);
            if ((document = fastaHelper.AddFasta(document, ref selectedPath, out emptyPeptideGroups)) == null)
            {
                tabControl1.SelectedTab = tabPageFasta;  // To show fasta errors
                return null;
            }
            if ((document = AddProteins(document, ref selectedPath)) == null)
            {
                return null;
            }
            if ((document = AddPeptides(document, validating, ref selectedPath)) == null)
            {
                return null;
            }
            if ((document = AddTransitionList(document, ref selectedPath)) == null)
            {
                return null;
            }
            return document;
        }

        private void ShowProteinError(PasteError pasteError)
        {
            tabControl1.SelectedTab = tabPageProteinList;
            ShowError(pasteError);
            gridViewProteins.CurrentCell = gridViewProteins.Rows[pasteError.Line].Cells[colProteinName.Index];
        }

        private void ShowPeptideError(PasteError pasteError)
        {
            tabControl1.SelectedTab = tabPagePeptideList;
            ShowError(pasteError);
            gridViewPeptides.CurrentCell = gridViewPeptides.Rows[pasteError.Line].Cells[pasteError.Column];
        }

        private void ShowTransitionError(PasteError pasteError)
        {
            tabControl1.SelectedTab = tabPageTransitionList;
            ShowError(pasteError);
            gridViewTransitionList.CurrentCell = gridViewTransitionList.Rows[pasteError.Line].Cells[pasteError.Column];
        }

        private SrmDocument AddPeptides(SrmDocument document, bool validating, ref IdentityPath selectedPath)
        {
            if (tabControl1.SelectedTab != tabPagePeptideList)
                return document;

            var matcher = new ModificationMatcher();
            var listPeptideSequences = ListPeptideSequences();
            if (listPeptideSequences == null)
                return null;
            try
            {
                matcher.CreateMatches(document.Settings, listPeptideSequences, Settings.Default.StaticModList,
                                      Settings.Default.HeavyModList);
            }
            catch (FormatException e)
            {
                MessageDlg.Show(this, e.Message);
                ShowPeptideError(new PasteError
                                     {
                                         Column = colPeptideSequence.Index,
                                         Message = Resources.PasteDlg_AddPeptides_Unable_to_interpret_peptide_modifications
                                     });
                return null;
            }
            var strNameMatches = matcher.FoundMatches;
            if (!validating && !string.IsNullOrEmpty(strNameMatches))
            {
                string message = TextUtil.LineSeparate(Resources.PasteDlg_AddPeptides_Would_you_like_to_use_the_Unimod_definitions_for_the_following_modifications,
                                                        string.Empty, strNameMatches);
                if (MultiButtonMsgDlg.Show(this, message, Resources.PasteDlg_AddPeptides_OK) == DialogResult.Cancel)
                    return null;
            }
            var backgroundProteome = GetBackgroundProteome(document);
            for (int i = gridViewPeptides.Rows.Count - 1; i >= 0; i--)
            {
                PeptideGroupDocNode peptideGroupDocNode;
                var row = gridViewPeptides.Rows[i];
                var pepModSequence = Convert.ToString(row.Cells[colPeptideSequence.Index].Value);
                var proteinName = Convert.ToString(row.Cells[colPeptideProtein.Index].Value);
                if (string.IsNullOrEmpty(pepModSequence) && string.IsNullOrEmpty(proteinName))
                    continue;
                if (string.IsNullOrEmpty(proteinName))
                {
                    peptideGroupDocNode = GetSelectedPeptideGroupDocNode(document, selectedPath);
                    if (!IsPeptideListDocNode(peptideGroupDocNode))
                    {
                        peptideGroupDocNode = null;
                    }
                }
                else
                {
                    peptideGroupDocNode = FindPeptideGroupDocNode(document, proteinName);
                }
                if (peptideGroupDocNode == null)
                {
                    if (string.IsNullOrEmpty(proteinName))
                    {
                        peptideGroupDocNode = new PeptideGroupDocNode(new PeptideGroup(),
                                                                      document.GetPeptideGroupId(true), null,
                                                                      new PeptideDocNode[0]);
                    }
                    else
                    {
                        ProteinMetadata metadata = null;
                        PeptideGroup peptideGroup = backgroundProteome.IsNone ? new PeptideGroup()
                            : (backgroundProteome.GetFastaSequence(proteinName, out metadata) ??
                                                    new PeptideGroup());
                        if (metadata != null)
                            peptideGroupDocNode = new PeptideGroupDocNode(peptideGroup, metadata, new PeptideDocNode[0]);
                        else
                            peptideGroupDocNode = new PeptideGroupDocNode(peptideGroup, proteinName,
                                                                      peptideGroup.Description, new PeptideDocNode[0]);
                    }
                    // Add to the end, if no insert node
                    var to = selectedPath;
                    if (to == null || to.Depth < (int)SrmDocument.Level.MoleculeGroups)
                        document = (SrmDocument)document.Add(peptideGroupDocNode);
                    else
                    {
                        Identity toId = selectedPath.GetIdentity((int) SrmDocument.Level.MoleculeGroups);
                        document = (SrmDocument) document.Insert(toId, peptideGroupDocNode);
                    }
                    selectedPath = new IdentityPath(peptideGroupDocNode.Id);
                }
                var peptides = new List<PeptideDocNode>();
                foreach (PeptideDocNode peptideDocNode in peptideGroupDocNode.Children)
                {
                    peptides.Add(peptideDocNode);
                }

                var fastaSequence = peptideGroupDocNode.PeptideGroup as FastaSequence;
                PeptideDocNode nodePepNew;
                if (fastaSequence != null)
                {
                    // Attempt to create node for error checking.
                    nodePepNew = fastaSequence.CreateFullPeptideDocNode(document.Settings,
                                                                        FastaSequence.StripModifications(pepModSequence));
                    if (nodePepNew == null)
                    {
                        ShowPeptideError(new PasteError
                                             {
                                                 Column = colPeptideSequence.Index,
                                                 Line = i,
                                                 Message = Resources.PasteDlg_AddPeptides_This_peptide_sequence_was_not_found_in_the_protein_sequence
                                             });
                        return null;
                    }
                }
                // Create node using ModificationMatcher.
                nodePepNew = matcher.GetModifiedNode(pepModSequence, fastaSequence).ChangeSettings(document.Settings,
                                                                                                  SrmSettingsDiff.ALL);
                // Avoid adding an existing peptide a second time.
                if (!peptides.Contains(nodePep => Equals(nodePep.Key, nodePepNew.Key)))
                {
                    peptides.Add(nodePepNew);
                    if (nodePepNew.Peptide.FastaSequence != null)
                        peptides.Sort(FastaSequence.ComparePeptides);
                    var newPeptideGroupDocNode = new PeptideGroupDocNode(peptideGroupDocNode.PeptideGroup, peptideGroupDocNode.Annotations, peptideGroupDocNode.Name, peptideGroupDocNode.Description, peptides.ToArray(), false);
                    document = (SrmDocument)document.ReplaceChild(newPeptideGroupDocNode);
                }
            }
            if (!validating && listPeptideSequences.Count > 0)
            {
                var pepModsNew = matcher.GetDocModifications(document);
                document = document.ChangeSettings(document.Settings.ChangePeptideModifications(mods => pepModsNew));
                document.Settings.UpdateDefaultModifications(false);
            }
            return document;
        }

        private List<string> ListPeptideSequences()
        {
            List<string> listSequences = new List<string>();
            for (int i = gridViewPeptides.Rows.Count - 1; i >= 0; i--)
            {
                var row = gridViewPeptides.Rows[i];
                var peptideSequence = Convert.ToString(row.Cells[colPeptideSequence.Index].Value);
                var proteinName = Convert.ToString(row.Cells[colPeptideProtein.Index].Value);
                if (string.IsNullOrEmpty(peptideSequence) && string.IsNullOrEmpty(proteinName))
                {
                    continue;
                }
                if (string.IsNullOrEmpty(peptideSequence))
                {
                    ShowPeptideError(new PasteError
                    {
                        Column = colPeptideSequence.Index,
                        Line = i,
                        Message = Resources.PasteDlg_ListPeptideSequences_The_peptide_sequence_cannot_be_blank
                    });
                    return null;
                }
                if (!FastaSequence.IsExSequence(peptideSequence))
                {
                    ShowPeptideError(new PasteError
                    {
                        Column = colPeptideSequence.Index,
                        Line = i,
                        Message = Resources.PasteDlg_ListPeptideSequences_This_peptide_sequence_contains_invalid_characters
                    });
                    return null;
                }
                listSequences.Add(peptideSequence);
            }
            return listSequences;
        }

        private static bool IsPeptideListDocNode(PeptideGroupDocNode peptideGroupDocNode)
        {
            return peptideGroupDocNode != null && peptideGroupDocNode.IsPeptideList;
        }

        private static string NullForEmpty(string str)
        {
            if (str == null)
                return null;
            return (str.Length == 0) ? null : str;
        }

        private SrmDocument AddProteins(SrmDocument document, ref IdentityPath selectedPath)
        {
            if (tabControl1.SelectedTab != tabPageProteinList)
                return document;

            var backgroundProteome = GetBackgroundProteome(document);
            for (int i = gridViewProteins.Rows.Count - 1; i >= 0; i--)
            {
                var row = gridViewProteins.Rows[i];
                var proteinName = Convert.ToString(row.Cells[colProteinName.Index].Value);
                if (String.IsNullOrEmpty(proteinName))
                {
                    continue;
                }
                var pastedMetadata = new ProteinMetadata(proteinName,
                    Convert.ToString(row.Cells[colProteinDescription.Index].Value),
                    NullForEmpty(Convert.ToString(row.Cells[colProteinPreferredName.Index].Value)),
                    NullForEmpty(Convert.ToString(row.Cells[colProteinAccession.Index].Value)),
                    NullForEmpty(Convert.ToString(row.Cells[colProteinGene.Index].Value)),
                    NullForEmpty(Convert.ToString(row.Cells[colProteinSpecies.Index].Value)));
                FastaSequence fastaSequence = null;
                if (!backgroundProteome.IsNone)
                {
                    ProteinMetadata protdbMetadata;
                    fastaSequence = backgroundProteome.GetFastaSequence(proteinName, out protdbMetadata);
                    // Fill in any gaps in pasted metadata with that in protdb
                    pastedMetadata = pastedMetadata.Merge(protdbMetadata);
                }
                // Strip any whitespace (tab, newline etc) In case it was copied out of a FASTA file
                var fastaSequenceString = new string(Convert.ToString(row.Cells[colProteinSequence.Index].Value).Where(c => !Char.IsWhiteSpace(c)).ToArray()); 
                if (!string.IsNullOrEmpty(fastaSequenceString))
                {
                        try
                        {
                            if (fastaSequence == null) // Didn't match anything in protdb
                            {
                                fastaSequence = new FastaSequence(pastedMetadata.Name, pastedMetadata.Description,
                                                                  new ProteinMetadata[0], fastaSequenceString);
                            }
                            else
                            {
                                if (fastaSequence.Sequence != fastaSequenceString)
                                {
                                    fastaSequence = new FastaSequence(pastedMetadata.Name, pastedMetadata.Description,
                                                                      fastaSequence.Alternatives, fastaSequenceString);
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            ShowProteinError(new PasteError
                                                 {
                                                     Line = i,
                                                     Column = colProteinDescription.Index,
                                                     Message = string.Format(Resources.PasteDlg_AddProteins_Invalid_protein_sequence__0__, exception.Message)
                                                 });
                            return null;
                        }
                }
                if (fastaSequence == null)
                {
                    ShowProteinError(
                        new PasteError
                        {
                                             Line = i,
                                Message = backgroundProteome.IsNone
                                        ? Resources.PasteDlg_AddProteins_Missing_protein_sequence
                                        : Resources.PasteDlg_AddProteins_This_protein_was_not_found_in_the_background_proteome_database
                        });
                    return null;
                }
                var description = pastedMetadata.Description;
                if (!string.IsNullOrEmpty(description) && description != fastaSequence.Description)
                {
                    fastaSequence = new FastaSequence(fastaSequence.Name, description, fastaSequence.Alternatives, fastaSequence.Sequence);
                }
                pastedMetadata = pastedMetadata.ChangeName(fastaSequence.Name).ChangeDescription(fastaSequence.Description);  // Make sure these agree
                var nodeGroupPep = new PeptideGroupDocNode(fastaSequence, pastedMetadata, new PeptideDocNode[0]);
                nodeGroupPep = nodeGroupPep.ChangeSettings(document.Settings, SrmSettingsDiff.ALL);
                var to = selectedPath;
                if (to == null || to.Depth < (int)SrmDocument.Level.MoleculeGroups)
                    document = (SrmDocument)document.Add(nodeGroupPep);
                else
                {
                    Identity toId = selectedPath.GetIdentity((int)SrmDocument.Level.MoleculeGroups);
                    document = (SrmDocument)document.Insert(toId, nodeGroupPep);
                }
                selectedPath = new IdentityPath(nodeGroupPep.Id);
            }
            return document;
        }

        private const char TRANSITION_LIST_SEPARATOR = TextUtil.SEPARATOR_TSV;
        private static readonly ColumnIndices TRANSITION_LIST_COL_INDICES = new ColumnIndices(
            0, 1, 2, 3);

        private const int INDEX_MOLECULE_GROUP = 0;
        private const int INDEX_MOLECULE = 1;
        private const int INDEX_MOLECULE_FORMULA = 2;
        private const int INDEX_PRODUCT_FORMULA = 3;
        private const int INDEX_MOLECULE_MZ = 4;
        private const int INDEX_PRODUCT_MZ = 5;
        private const int INDEX_MOLECULE_CHARGE = 6;
        private const int INDEX_PRODUCT_CHARGE = 7;

        private int ValidateFormulaWithMz(SrmDocument document, ref string moleculeFormula, double mz, out double monoMass, out double averageMass)
        {
            // Is the ion's formula the old style where user expected us to add a hydrogen?
            var tolerance = document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            int massShift;
            var ion = new DocNodeCustomIon(moleculeFormula);
            monoMass = ion.GetMass(MassType.Monoisotopic);
            averageMass = ion.GetMass(MassType.Average);
            double mass = (document.Settings.TransitionSettings.Prediction.FragmentMassType == MassType.Monoisotopic)
                ? monoMass
                : averageMass;
            var charge = TransitionCalc.CalcCharge(mass, mz, tolerance, true, TransitionGroup.MIN_PRECURSOR_CHARGE,
                TransitionGroup.MAX_PRECURSOR_CHARGE, new int[0],
                TransitionCalc.MassShiftType.none, out massShift);
            if (charge < 0)
            {
                // That formula and this mz don't yield a reasonable charge state - try adding an H
                var ion2 = new DocNodeCustomIon(BioMassCalc.AddH(ion.Formula));
                monoMass = ion2.GetMass(MassType.Monoisotopic);
                averageMass = ion2.GetMass(MassType.Average);
                mass = (document.Settings.TransitionSettings.Prediction.FragmentMassType == MassType.Monoisotopic)
                    ? monoMass
                    : averageMass;
                charge = TransitionCalc.CalcCharge(mass, mz, tolerance, true, TransitionGroup.MIN_PRECURSOR_CHARGE,
                    TransitionGroup.MAX_PRECURSOR_CHARGE, new int[0], TransitionCalc.MassShiftType.none, out massShift);
                if (charge > 0)
                {
                    moleculeFormula = ion2.Formula;
                }
                else
                {
                    monoMass = 0;
                    averageMass = 0;
                }
            }
            return charge;
        }

        private double ValidateFormulaWithCharge(SrmDocument document, string moleculeFormula, int charge, out double monoMass, out double averageMass)
        {
            var massType = document.Settings.TransitionSettings.Prediction.PrecursorMassType;
            var ion = new DocNodeCustomIon(moleculeFormula);
            double mass = ion.GetMass(massType);
            monoMass = ion.GetMass(MassType.Monoisotopic);
            averageMass = ion.GetMass(MassType.Average);
            return BioMassCalc.CalculateMz(mass, charge);
        }

        private class MoleculeInfo
        {
            public string Formula { get; private set; }
            public double Mz { get; private set; }
            public int Charge { get; private set; }
            public double MonoMass { get; private set; }
            public double AverageMass { get; private set; }

            public MoleculeInfo(string formula, int charge, double mz, double monoMass, double averageMass)
            {
                Formula = formula;
                Charge = charge;
                Mz = mz;
                MonoMass = monoMass;
                AverageMass = averageMass;
            }
        }

        // We need some combination of:
        //  Formula and mz
        //  Formula and charge
        //  mz and charge
        private MoleculeInfo ReadPrecursorOrProductColumns(SrmDocument document, 
            DataGridViewRow row, 
            bool getPrecursorColumns)
        {
            int indexFormula = getPrecursorColumns ? INDEX_MOLECULE_FORMULA : INDEX_PRODUCT_FORMULA;
            int indexMz = getPrecursorColumns ? INDEX_MOLECULE_MZ : INDEX_PRODUCT_MZ;
            int indexCharge = getPrecursorColumns ? INDEX_MOLECULE_CHARGE : INDEX_PRODUCT_CHARGE;
            var formula = Convert.ToString(row.Cells[indexFormula].Value);
            double mz;
            if (!double.TryParse(Convert.ToString(row.Cells[indexMz].Value), out mz))
                mz = 0;
            int charge;
            if (!int.TryParse(Convert.ToString(row.Cells[indexCharge].Value), out charge))
                charge =  0;
            double monoMass;
            double averageMmass;
            string errMessage = String.Format(getPrecursorColumns
                ? Resources.PasteDlg_ValidateEntry_Error_on_line__0___Precursor_needs_values_for_any_two_of__Formula__m_z_or_Charge_
                : Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_, row.Index+1);
            int errColumn = indexFormula;
            if (NullForEmpty(formula) != null)
            {
                // We have a formula
                if (mz > 0)
                {
                    // Is the ion's formula the old style where user expected us to add a hydrogen? 
                    charge = ValidateFormulaWithMz(document, ref formula, mz, out monoMass, out averageMmass);
                    row.Cells[indexFormula].Value = formula;
                    if (charge > 0)
                    {
                        row.Cells[indexCharge].Value = charge;
                        return new MoleculeInfo(formula, charge, mz, monoMass, averageMmass);
                    }
                    errMessage = String.Format(getPrecursorColumns
                        ? Resources.PasteDlg_ValidateEntry_Error_on_line__0___Precursor_formula_and_m_z_value_do_not_agree_for_any_charge_state_
                        : Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_formula_and_m_z_value_do_not_agree_for_any_charge_state_, row.Index+1);
                }
                else if (charge != 0)
                {
                    // Get the mass from the formula, and mz from that and charge
                    mz = ValidateFormulaWithCharge(document, formula, charge, out monoMass, out averageMmass);
                    row.Cells[indexMz].Value = mz;
                    return new MoleculeInfo(formula, charge, mz, monoMass, averageMmass);
                }
                errColumn = indexMz;
            } 
            else if (mz != 0 && charge != 0)
            {
                // No formula, just use charge and m/z
                monoMass = averageMmass = BioMassCalc.CalculateMassWithElectronLoss(mz, charge);
                return new MoleculeInfo(formula, charge, mz, monoMass, averageMmass);
            }
            ShowTransitionError(new PasteError
            {
                Column = errColumn,
                Line = row.Index,
                Message = errMessage
            });
            return null;
        }

        private SrmDocument AddTransitionList(SrmDocument document, ref IdentityPath selectedPath)
        {
            if (tabControl1.SelectedTab != tabPageTransitionList)
                return document;
            if (IsMolecule)
            {
                for(int i = 0; i < gridViewTransitionList.RowCount - 1; i ++)
                {
                    DataGridViewRow row = gridViewTransitionList.Rows[i];
                    var precursor = ReadPrecursorOrProductColumns(document, row, true); // Get moecule values
                    if (precursor == null)
                        return null;
                    var product = ReadPrecursorOrProductColumns(document, row, false); // get product values
                    if (product == null)
                    {
                        return null;
                    }

                    bool pepGroupFound = false;
                    foreach (var pepGroup in document.MoleculeGroups)
                    {
                        var pathPepGroup = new IdentityPath(pepGroup.Id);
                        if (pepGroup.Name == Convert.ToString(row.Cells[INDEX_MOLECULE_GROUP].Value))
                        {
                            pepGroupFound = true;
                            bool pepFound = false;
                            foreach (var pep in pepGroup.SmallMolecules)
                            {
                                var pepPath = new IdentityPath(pathPepGroup, pep.Id);
                                if (!string.IsNullOrEmpty(pep.Peptide.CustomIon.Formula) && Equals(pep.Peptide.CustomIon.Formula, Convert.ToString(row.Cells[INDEX_MOLECULE].Value)))
                                {
                                    pepFound = true;
                                    bool tranGroupFound = false;
                                    foreach (var tranGroup in pep.TransitionGroups)
                                    {
                                        var pathGroup = new IdentityPath(pepPath, tranGroup.Id);
                                        if (Math.Abs(tranGroup.PrecursorMz - precursor.Mz) <= document.Settings.TransitionSettings.Instrument.MzMatchTolerance)
                                        {
                                            tranGroupFound = true;
                                            var tranFound = false;
                                            var tranNode = GetMoleculeTransition(document, row, pep.Peptide, tranGroup.TransitionGroup);
                                            if (tranNode == null)
                                                return null;
                                            foreach (var tran in tranGroup.Transitions)
                                            {
                                                if (Equals(tranNode.Transition.CustomIon,tran.Transition.CustomIon))
                                                {
                                                    tranFound = true;
                                                    break;
                                                }
                                            }
                                            if (!tranFound)
                                            {
                                                document = (SrmDocument) document.Add(pathGroup, tranNode);
                                            }
                                            break;
                                        }
                                    }
                                    if (!tranGroupFound)
                                    {
                                        var node = GetMoleculeTransitionGroup(document, row, pep.Peptide);
                                        if (node == null)
                                            return null;
                                        document =
                                            (SrmDocument)
                                                document.Add(pepPath, node);
                                    }
                                    break;
                                }   
                            }
                            if (!pepFound)
                            {
                                var node = GetMoleculePeptide(document, row, pepGroup.PeptideGroup);
                                if (node == null)
                                    return null;
                                document =
                                    (SrmDocument)
                                        document.Add(pathPepGroup,node);
                            }
                            break;
                        }
                    }
                    if (!pepGroupFound)
                    {
                        var node = GetMoleculePeptideGroup(document, row);
                        if (node == null)
                            return null;
                        IdentityPath first;
                        IdentityPath next;
                        document =
                                document.AddPeptideGroups(new[] {node}, false,null
                                    , out first,out next);
                    }
                }
            }
            else
            {
                var backgroundProteome = GetBackgroundProteome(document);
                var sbTransitionList = new StringBuilder();
                var dictNameSeq = new Dictionary<string, FastaSequence>();
                // Add all existing FASTA sequences in the document to the name to seq dictionary
                // Including named peptide lists would cause the import code to give matching names
                // in this list new names (e.g. with 1, 2, 3 appended).  In this code, the names
                // are intended to be merged.
                foreach (var nodePepGroup in document.Children.Cast<PeptideGroupDocNode>().Where(n => !n.IsPeptideList))
                {
                    if (!dictNameSeq.ContainsKey(nodePepGroup.Name))
                        dictNameSeq.Add(nodePepGroup.Name, (FastaSequence) nodePepGroup.PeptideGroup);
                }

                // Check for simple errors and build strings for import
                for (int i = 0; i < gridViewTransitionList.Rows.Count; i++)
                {
                    var row = gridViewTransitionList.Rows[i];
                    var peptideSequence = Convert.ToString(row.Cells[colTransitionPeptide.Index].Value);
                    var proteinName = Convert.ToString(row.Cells[colTransitionProteinName.Index].Value);
                    var precursorMzText = Convert.ToString(row.Cells[colTransitionPrecursorMz.Index].Value);
                    var productMzText = Convert.ToString(row.Cells[colTransitionProductMz.Index].Value);
                    if (string.IsNullOrEmpty(peptideSequence) && string.IsNullOrEmpty(proteinName))
                    {
                        continue;
                    }
                    if (string.IsNullOrEmpty(peptideSequence))
                    {
                        ShowTransitionError(new PasteError
                        {
                            Column = colTransitionPeptide.Index,
                            Line = i,
                            Message = Resources.PasteDlg_ListPeptideSequences_The_peptide_sequence_cannot_be_blank
                        });
                        return null;
                    }
                    if (!FastaSequence.IsExSequence(peptideSequence))
                    {
                        ShowTransitionError(new PasteError
                        {
                            Column = colTransitionPeptide.Index,
                            Line = i,
                            Message = Resources.PasteDlg_ListPeptideSequences_This_peptide_sequence_contains_invalid_characters
                        });
                        return null;
                    }
                    double mz;
                    if (!double.TryParse(precursorMzText, out mz))
                    {
                        ShowTransitionError(new PasteError
                        {
                            Column = colTransitionPrecursorMz.Index,
                            Line = i,
                            Message = Resources.PasteDlg_AddTransitionList_The_precursor_m_z_must_be_a_number_
                        });
                        return null;
                    }
                    if (!double.TryParse(productMzText, out mz))
                    {
                        ShowTransitionError(new PasteError
                        {
                            Column = colTransitionProductMz.Index,
                            Line = i,
                            Message = Resources.PasteDlg_AddTransitionList_The_product_m_z_must_be_a_number_
                        });
                        return null;
                    }

                    const char sep = TRANSITION_LIST_SEPARATOR;
                    // Add columns in order specified by TRANSITION_LIST_COL_INDICES
                    sbTransitionList
                        .Append(proteinName).Append(sep)
                        .Append(peptideSequence).Append(sep)
                        .Append(precursorMzText).Append(sep)
                        .Append(productMzText).AppendLine();
                    // Build FASTA sequence text in cases where it is known
                    if (!dictNameSeq.ContainsKey(proteinName))
                    {
                        var fastaSeq = backgroundProteome.GetFastaSequence(proteinName);
                        if (fastaSeq != null)
                            dictNameSeq.Add(proteinName, fastaSeq);
                    }
                }

                if (sbTransitionList.Length == 0)
                    return document;

                // Do the actual import into PeptideGroupDocNodes
                IEnumerable<PeptideGroupDocNode> peptideGroupDocNodes;
                try
                {
                    List<TransitionImportErrorInfo> errorList;
                    List<KeyValuePair<string, double>> irtPeptides;
                    List<SpectrumMzInfo> librarySpectra;
                    var importer = new MassListImporter(document, LocalizationHelper.CurrentCulture, TRANSITION_LIST_SEPARATOR);
                    // TODO: support long-wait broker
                    peptideGroupDocNodes = importer.Import(new StringReader(sbTransitionList.ToString()),
                        null,
                        -1,
                        TRANSITION_LIST_COL_INDICES,
                        dictNameSeq,
                        out irtPeptides,
                        out librarySpectra,
                        out errorList);
                    if (errorList.Any())
                    {
                        var firstError = errorList[0];
                        if (firstError.Row.HasValue)
                        {
                            throw new LineColNumberedIoException(firstError.ErrorMessage, firstError.Row.Value, firstError.Column ?? -1);
                        }
                        else
                        {
                            throw new InvalidDataException(firstError.ErrorMessage);
                        }
                    }
                }
                catch (LineColNumberedIoException x)
                {
                    var columns = new[]
                    {
                        colTransitionProteinName,
                        colPeptideSequence,
                        colTransitionPrecursorMz,
                        colTransitionProductMz
                    };

                    ShowTransitionError(new PasteError
                    {
                        Column = x.ColumnIndex >= 0 ? columns[x.ColumnIndex].Index : 0,
                        Line = (int) x.LineNumber - 1,
                        Message = x.PlainMessage
                    });
                    return null;
                }
                catch (InvalidDataException x)
                {
                    ShowTransitionError(new PasteError
                    {
                        Message = x.Message
                    });
                    return null;
                }

                // Insert the resulting nodes into the document tree, merging when possible
                bool after = false;
                foreach (var nodePepGroup in peptideGroupDocNodes)
                {
                    PeptideGroupDocNode nodePepGroupExist = FindPeptideGroupDocNode(document, nodePepGroup);
                    if (nodePepGroupExist != null)
                    {
                        var nodePepGroupNew = nodePepGroupExist.Merge(nodePepGroup);
                        if (!ReferenceEquals(nodePepGroupExist, nodePepGroupNew))
                            document = (SrmDocument) document.ReplaceChild(nodePepGroupNew);

                    }
                    else
                    {
                        // Add to the end, if no insert node
                        var to = selectedPath;
                        if (to == null || to.Depth < (int) SrmDocument.Level.MoleculeGroups)
                            document = (SrmDocument) document.Add(nodePepGroup);
                        else
                        {
                            Identity toId = selectedPath.GetIdentity((int) SrmDocument.Level.MoleculeGroups);
                            document = (SrmDocument) document.Insert(toId, nodePepGroup, after);
                        }
                        selectedPath = new IdentityPath(nodePepGroup.Id);
                        // All future insertions should be after, to avoid reversing the list
                        after = true;
                    }
                }
            }
            return document;
        }

        private PeptideGroupDocNode GetMoleculePeptideGroup(SrmDocument document, DataGridViewRow row)
        {
            var pepGroup = new PeptideGroup();
            var pep = GetMoleculePeptide(document, row, pepGroup);
            if (pep == null)
                return null;
            var name = Convert.ToString(row.Cells[INDEX_MOLECULE_GROUP].Value);
            if (string.IsNullOrEmpty(name))
                name = document.GetPeptideGroupId(true);
            var metadata = new ProteinMetadata(name, string.Empty).SetWebSearchCompleted();  // FUTURE: some kind of lookup for small molecules
            return new PeptideGroupDocNode(pepGroup, metadata, new[] {pep});
        }

        private PeptideDocNode GetMoleculePeptide(SrmDocument document, DataGridViewRow row, PeptideGroup group)
        {

            DocNodeCustomIon ion;
            try
            {
                var moleculeInfo = ReadPrecursorOrProductColumns(document, row, true); // Re-read the precursor columns
                ion = new DocNodeCustomIon(moleculeInfo.Formula, moleculeInfo.MonoMass, moleculeInfo.AverageMass,
                    Convert.ToString(row.Cells[INDEX_MOLECULE].Value)); // Short name
            }
            catch (ArgumentException e)
            {
                ShowTransitionError(new PasteError
                {
                    Column    = INDEX_MOLECULE_FORMULA,
                    Line = row.Index, 
                    Message = e.Message
                });
                return null;
            }
            

            var pep = new Peptide(ion);
            var tranGroup = GetMoleculeTransitionGroup(document, row, pep);
            if (tranGroup == null)
                return null;
            return new PeptideDocNode(pep,document.Settings,null,null,new[]{tranGroup},true);
        }

        private TransitionGroupDocNode GetMoleculeTransitionGroup(SrmDocument document, DataGridViewRow row, Peptide pep)
        {
            var moleculeInfo = ReadPrecursorOrProductColumns(document, row, true); // Re-read the precursor columns
            if (!document.Settings.TransitionSettings.IsMeasurablePrecursor(moleculeInfo.Mz))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_MOLECULE_MZ,
                    Line = row.Index,
                    Message = string.Format(Resources.PasteDlg_GetMoleculeTransitionGroup_The_precursor_m_z__0__is_not_measureable_with_your_current_instrument_settings_, moleculeInfo.Mz)
                });
                return null;
            }
            var group = new TransitionGroup(pep, moleculeInfo.Charge, IsotopeLabelType.light);
            var tran = GetMoleculeTransition(document, row, pep, group);
            if (tran == null)
                return null;
            return new TransitionGroupDocNode(group, document.Annotations, document.Settings, null,
                null, null, new[] {tran}, true);
        }

        private TransitionDocNode GetMoleculeTransition(SrmDocument document, DataGridViewRow row, Peptide pep, TransitionGroup group)
        {
            var massType =
                document.Settings.TransitionSettings.Prediction.FragmentMassType;

            var molecule = ReadPrecursorOrProductColumns(document, row, false); // Re-read the product columns
            if (molecule == null)
            {
                return null;
            }
            DocNodeCustomIon ion = new DocNodeCustomIon(molecule.Formula, molecule.MonoMass, molecule.AverageMass);
            var ionType = ion.Equals(pep.CustomIon)
                ? IonType.precursor
                : IonType.custom;
            double mass = ion.GetMass(massType);

            var transition = new Transition(group, molecule.Charge, null, ion, ionType);
            return new TransitionDocNode(transition, document.Annotations, null, mass, null, null, null);
        }

        private static PeptideGroupDocNode FindPeptideGroupDocNode(SrmDocument document, PeptideGroupDocNode nodePepGroup)
        {
            if (!nodePepGroup.IsPeptideList)
                return (PeptideGroupDocNode) document.FindNode(nodePepGroup.PeptideGroup);

            // Find peptide lists by name
            return FindPeptideGroupDocNode(document, nodePepGroup.Name);
        }

        private static PeptideGroupDocNode FindPeptideGroupDocNode(SrmDocument document, String name)
        {
            return document.MoleculeGroups.FirstOrDefault(n => Equals(name, n.Name));
        }

        private PeptideGroupDocNode GetSelectedPeptideGroupDocNode(SrmDocument document, IdentityPath selectedPath)
        {
            var to = selectedPath;
            if (to != null && to.Depth >= (int)SrmDocument.Level.MoleculeGroups)
                return (PeptideGroupDocNode) document.FindNode(to.GetIdentity((int) SrmDocument.Level.MoleculeGroups));

            PeptideGroupDocNode lastPeptideGroupDocuNode = null;
            foreach (PeptideGroupDocNode peptideGroupDocNode in document.MoleculeGroups)
            {
                lastPeptideGroupDocuNode = peptideGroupDocNode;
            }
            return lastPeptideGroupDocuNode;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
            DialogResult = DialogResult.Cancel;
        }

        private static void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            
        }

        public PasteFormat PasteFormat
        {
            get
            {
                return GetPasteFormat(tabControl1.SelectedTab);
            }
            set
            {
                var tab = GetTabPage(value);
                radioMolecule.Visible = radioPeptide.Visible = radioPeptide.Checked = (value  == PasteFormat.transition_list);
                for (int i = tabControl1.Controls.Count - 1; i >= 0; i--)
                {
                    if (tabControl1.Controls[i] != tab)
                    {
                        tabControl1.Controls.RemoveAt(i);
                    }
                }
                if (tab.Parent == null)
                {
                    tabControl1.Controls.Add(tab);
                }
                tabControl1.SelectedTab = tab;
                AcceptButton = tabControl1.SelectedTab != tabPageFasta ? btnInsert : null;
            }
        }

        public string Description
        {
            get
            {
                switch (PasteFormat)
                {
                    case PasteFormat.fasta: return Resources.PasteDlg_Description_Insert_FASTA;
                    case PasteFormat.protein_list: return Resources.PasteDlg_Description_Insert_protein_list;
                    case PasteFormat.peptide_list: return Resources.PasteDlg_Description_Insert_peptide_list;
                    case PasteFormat.transition_list: return Resources.PasteDlg_Description_Insert_transition_list;
                }
                return Resources.PasteDlg_Description_Insert;
            }
        }

// ReSharper disable MemberCanBeMadeStatic.Local
        private void tabControl1_Selecting(object sender, TabControlCancelEventArgs e)
        {
            // Should no longer be possible to change tabs
        }
// ReSharper restore MemberCanBeMadeStatic.Local

        private PasteFormat GetPasteFormat(TabPage tabPage)
        {
            if (tabPage == tabPageFasta)
            {
                return PasteFormat.fasta;
            }
            if (tabPage == tabPageProteinList)
            {
                return PasteFormat.protein_list;
            }
            if (tabPage == tabPagePeptideList)
            {
                return PasteFormat.peptide_list;
            }
            if (tabPage == tabPageTransitionList)
            {
                return PasteFormat.transition_list;
            }
            return PasteFormat.none;
        }

        private TabPage GetTabPage(PasteFormat pasteFormat)
        {
            switch (pasteFormat)
            {
                case PasteFormat.fasta:
                    return tabPageFasta;
                case PasteFormat.protein_list:
                    return tabPageProteinList;
                case PasteFormat.peptide_list:
                    return tabPagePeptideList;
                case PasteFormat.transition_list:
                    return tabPageTransitionList;
            }
            return null;
        }

        private static BackgroundProteome GetBackgroundProteome(SrmDocument srmDocument)
        {
            return srmDocument.Settings.PeptideSettings.BackgroundProteome;
        }

        private void gridViewProteins_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            HideNoErrors();
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
            {
                return;
            }
            var column = gridViewProteins.Columns[e.ColumnIndex];
            if (column != colProteinName)
            {
                return;
            }
            var row = gridViewProteins.Rows[e.RowIndex];
            var proteinName = Convert.ToString(row.Cells[e.ColumnIndex].Value);
            if (string.IsNullOrEmpty(proteinName))
            {
                gridViewProteins.Rows.Remove(row);
            }

            ProteinMetadata metadata;
            FastaSequence fastaSequence = GetFastaSequence(row, proteinName, out metadata);
            if (fastaSequence == null)
            {
                row.Cells[colProteinDescription.Index].Value = null;
                row.Cells[colProteinSequence.Index].Value = null;
                row.Cells[colProteinPreferredName.Index].Value = null;
                row.Cells[colProteinAccession.Index].Value = null;
                row.Cells[colProteinGene.Index].Value = null;
                row.Cells[colProteinSpecies.Index].Value = null;
            }
            else
            {
                row.Cells[colProteinName.Index].Value = fastaSequence.Name; // Possibly the search was actually on accession, gene etc
                row.Cells[colProteinDescription.Index].Value = fastaSequence.Description;
                row.Cells[colProteinSequence.Index].Value = fastaSequence.Sequence;
                row.Cells[colProteinPreferredName.Index].Value = (metadata == null) ? null : metadata.PreferredName;
                row.Cells[colProteinAccession.Index].Value = (metadata == null) ? null : metadata.Accession;
                row.Cells[colProteinGene.Index].Value = (metadata == null) ? null : metadata.Gene;
                row.Cells[colProteinSpecies.Index].Value = (metadata == null) ? null : metadata.Species;
            }
        }
       private void gridViewPeptides_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }
            var row = gridViewPeptides.Rows[e.RowIndex];
            var column = gridViewPeptides.Columns[e.ColumnIndex];
            if (column != colPeptideProtein)
            {
                return;
            }
            var proteinName = Convert.ToString(row.Cells[colPeptideProtein.Index].Value);
            ProteinMetadata metadata;
            FastaSequence fastaSequence = GetFastaSequence(row, proteinName, out metadata);
            row.Cells[colPeptideProteinDescription.Index].Value = fastaSequence == null ? null : fastaSequence.Description;
        }

        /// <summary>
        /// Enumerates table entries for all proteins matching a pasted peptide.
        /// This can't be done on gridViewPeptides_CellValueChanged because we are creating new cells.
        /// </summary>
        private void EnumerateProteins(DataGridView dataGridView, int rowIndex, bool keepAllPeptides, 
            ref int numUnmatched, ref int numMultipleMatches, ref int numFiltered, HashSet<string> seenPepSeq)
        {

            HideNoErrors();      
            var row = dataGridView.Rows[rowIndex];
            int sequenceIndex = Equals(dataGridView, gridViewPeptides)
                                ? colPeptideSequence.Index
                                : (Equals(dataGridView, gridViewTransitionList) ? colTransitionPeptide.Index : -1);
            int proteinIndex = Equals(dataGridView, gridViewPeptides)
                                ? colPeptideProtein.Index
                                : (Equals(dataGridView, gridViewTransitionList) ? colTransitionProteinName.Index : -1);
            
            var proteinName = Convert.ToString(row.Cells[proteinIndex].Value);
            var pepModSequence = Convert.ToString(row.Cells[sequenceIndex].Value);

            // Only enumerate the proteins if the user has not specified a protein.
            if (!string.IsNullOrEmpty(proteinName))
                return;
            
            // If there is no peptide sequence and no protein, remove this entry.
            if (string.IsNullOrEmpty(pepModSequence))
            {
                dataGridView.Rows.Remove(row);
                return;
            }

            string peptideSequence = FastaSequence.StripModifications(pepModSequence);

            // Check to see if this is a new sequence because we don't want to count peptides more than once for
            // the FilterMatchedPeptidesDlg.
            bool newSequence = !seenPepSeq.Contains(peptideSequence);
            if(newSequence)
            {
                // If we are not keeping filtered peptides, and this peptide does not match current filter
                // settings, remove this peptide.
                if (!FastaSequence.IsExSequence(peptideSequence))
                {
                    dataGridView.CurrentCell = row.Cells[sequenceIndex];
                    throw new InvalidDataException(Resources.PasteDlg_ListPeptideSequences_This_peptide_sequence_contains_invalid_characters);
                }
                seenPepSeq.Add(peptideSequence);
            }

            var proteinNames = GetProteinNamesForPeptideSequence(peptideSequence);

            bool isUnmatched = proteinNames == null || proteinNames.Count == 0;
            bool hasMultipleMatches = proteinNames != null && proteinNames.Count > 1;
            bool isFiltered = !DocumentUiContainer.Document.Settings.Accept(peptideSequence);

            if(newSequence)
            {
                numUnmatched += isUnmatched ? 1 : 0;
                numMultipleMatches += hasMultipleMatches ? 1 : 0;
                numFiltered += isFiltered ? 1 : 0;
            }
          
            // No protein matches found, so we do not need to enumerate this peptide. 
            if (isUnmatched)
            {
                // If we are not keeping unmatched peptides, then remove this peptide.
                if (!keepAllPeptides && !Settings.Default.LibraryPeptidesAddUnmatched)
                    dataGridView.Rows.Remove(row);
                // Even if we are keeping this peptide, it has no matches so we don't enumerate it.
                return;
            }

            // If there are multiple protein matches, and we are filtering such peptides, remove this peptide.
            if (!keepAllPeptides &&
                (hasMultipleMatches && FilterMultipleProteinMatches == BackgroundProteome.DuplicateProteinsFilter.NoDuplicates)
                || (isFiltered && !Settings.Default.LibraryPeptidesKeepFiltered))
            {
                dataGridView.Rows.Remove(row);
                return;
            }
            
            row.Cells[proteinIndex].Value = proteinNames[0];
            // Only using the first occurence.
            if(!keepAllPeptides && FilterMultipleProteinMatches == BackgroundProteome.DuplicateProteinsFilter.FirstOccurence)
                return;
            // Finally, enumerate all proteins for this peptide.
            for (int i = 1; i < proteinNames.Count; i ++)
            {
                var newRow = dataGridView.Rows[dataGridView.Rows.Add()];
                // Copy all cells, except for the protein name as well as any cells that are not null, 
                // meaning that they have already been filled out by CellValueChanged.
                for(int x = 0; x < row.Cells.Count; x++)
                {
                    if (newRow.Cells[proteinIndex].Value != null)
                        continue;
                    if (x == proteinIndex)
                        newRow.Cells[proteinIndex].Value = proteinNames[i];
                    else
                        newRow.Cells[x].Value = row.Cells[x].Value;
                }
            }
        }

        private void gridViewTransitionList_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            HideNoErrors();
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
            {
                return;
            }
            var row = gridViewTransitionList.Rows[e.RowIndex];
            var proteinName = Convert.ToString(row.Cells[colTransitionProteinName.Index].Value);
            var column = gridViewTransitionList.Columns[e.ColumnIndex];
            if (column != colTransitionProteinName)
            {
                return;
            }
            ProteinMetadata metadata;
            FastaSequence fastaSequence = GetFastaSequence(row, proteinName, out metadata);
            if (fastaSequence != null)
            {
                row.Cells[colTransitionProteinDescription.Index].Value = fastaSequence.Description;
                // CONSIDER (bspratt) show other parts of protein metadata here as well - gene, accession etc
            }
        }

        private FastaSequence GetFastaSequence(DataGridViewRow row, string proteinName, out ProteinMetadata metadata)
        {
            metadata = null;
            var backgroundProteome = GetBackgroundProteome(DocumentUiContainer.DocumentUI);
            if (backgroundProteome.IsNone)
                return null;

            var fastaSequence = backgroundProteome.GetFastaSequence(proteinName, out metadata);
            if (fastaSequence == null)
            {
                // Sometimes the protein name in the background proteome will have an extra "|" on the end.
                // In that case, update the name of the protein to match the one in the database.
                fastaSequence = backgroundProteome.GetFastaSequence(proteinName + "|"); // Not L10N
                if (fastaSequence != null)
                {
                    row.Cells[colPeptideProtein.Index].Value = fastaSequence.Name;
                }
            }

            return fastaSequence;
        }

        private void OnEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            _statementCompletionEditBox.Attach(((DataGridView) sender).EditingControl as TextBox);
        }

        private void btnInsert_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            bool error = false;
            IdentityPath newSelectedPath = SelectedPath;
            Program.MainWindow.ModifyDocument(Description, 
                                              document =>
                                                  {
                                                      newSelectedPath = SelectedPath;
                                                      int emptyPeptideGroups;
                                                      var newDocument = GetNewDocument(document, false, ref newSelectedPath, out emptyPeptideGroups);
                                                      if (newDocument == null)
                                                      {
                                                          error = true;
                                                          return document;
                                                      }
                                                      // CONSIDER: This can show message boxes requesting user input
                                                      //           Should it really be in the ModifyDocument function?
                                                      newDocument = ImportFastaHelper.HandleEmptyPeptideGroups(this, emptyPeptideGroups, newDocument);
                                                      if (newDocument == null)
                                                      {
                                                          error = true;
                                                          return document;
                                                      }
                                                      return newDocument;
                                                  });
            if (error)
            {
                return;
            }
            SelectedPath = newSelectedPath;
            DialogResult = DialogResult.OK;
        }

        private void tbxFasta_TextChanged(object sender, EventArgs e)
        {
            HideNoErrors();
        }

        private void gridViewPeptides_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            _statementCompletionEditBox.MatchTypes = e.ColumnIndex == colPeptideSequence.Index
                ? ProteinMatchType.sequence : 0;
        }

        private void gridViewProteins_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            _statementCompletionEditBox.MatchTypes = e.ColumnIndex == colProteinName.Index
                ? (ProteinMatchType.all & ~ProteinMatchType.sequence) : 0;  // name, description, accession, etc
        }

        private void gridViewTransitionList_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            _statementCompletionEditBox.MatchTypes = e.ColumnIndex == colTransitionPeptide.Index
                ? ProteinMatchType.sequence : 0;
        }

        private void gridViewProteins_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.V && e.Modifiers == Keys.Control)
            {
                if (!gridViewProteins.IsCurrentCellInEditMode)
                {
                    PasteProteins();
                    e.Handled = true;
                }
            }
        }

        public void PasteFasta()  // For functional test use
        {
            tbxFasta.Text = ClipboardEx.GetText();
        }

        public void PasteProteins()
        {
            Paste(gridViewProteins, false);
        }

        public void PasteTransitions()
        {
            var document = DocumentUiContainer.Document;
            var backgroundProteome = document.Settings.PeptideSettings.BackgroundProteome;
            Paste(gridViewTransitionList, !backgroundProteome.IsNone);
        }

        private void gridViewPeptides_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.V && e.Modifiers == Keys.Control)
            {
                if (!gridViewPeptides.IsCurrentCellInEditMode)
                {
                    PastePeptides();
                    e.Handled = true;
                }
            }
        }

        public void PastePeptides()
        {       
            var document = DocumentUiContainer.Document;
            var backgroundProteome = document.Settings.PeptideSettings.BackgroundProteome;
            Paste(gridViewPeptides, !backgroundProteome.IsNone);
        }

        /// <summary>
        /// Removes the given number of last rows in the given DataGridView.
        /// </summary>
        private static void RemoveLastRows(DataGridView dataGridView, int numToRemove)
        {
            int rowCount = dataGridView.Rows.Count;
            for (int i = rowCount - numToRemove; i < rowCount; i++)
            {
                dataGridView.Rows.Remove(dataGridView.Rows[dataGridView.Rows.Count - 2]);
            }
        }    
              
        public static BackgroundProteome.DuplicateProteinsFilter FilterMultipleProteinMatches
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.LibraryPeptidesAddDuplicatesEnum,
                                         BackgroundProteome.DuplicateProteinsFilter.AddToAll);
            }
        }

        private void Paste(DataGridView dataGridView, bool enumerateProteins)
        {
            string text;

            try
            {
                text = ClipboardEx.GetText();
            }
            catch (ExternalException)
            {
                MessageDlg.Show(this, ClipboardHelper.GetOpenClipboardMessage(Resources.PasteDlg_Paste_Failed_getting_data_from_the_clipboard));
                return;
            }

            int numUnmatched;
            int numMultipleMatches;
            int numFiltered;
            int prevRowCount = dataGridView.RowCount;
            try
            {
                Paste(dataGridView, text, enumerateProteins, enumerateProteins, out numUnmatched, 
                    out numMultipleMatches, out numFiltered);
            }
            // User pasted invalid text.
            catch(InvalidDataException e)
            {
                dataGridView.Show();
                // Show the invalid text, then remove all newly added rows.
                MessageDlg.Show(this, e.Message);
                RemoveLastRows(dataGridView, dataGridView.RowCount - prevRowCount);
                return;
            }
            // If we have no unmatched, no multiple matches, and no filtered, we do not need to show 
            // the FilterMatchedPeptidesDlg.
            if (numUnmatched + numMultipleMatches + numFiltered == 0)
                return;
            using (var filterPeptidesDlg =
                new FilterMatchedPeptidesDlg(numMultipleMatches, numUnmatched, numFiltered,
                                             dataGridView.RowCount - prevRowCount == 1))
            {
                var result = filterPeptidesDlg.ShowDialog(this);
                // If the user is keeping all peptide matches, we don't need to redo the paste.
                bool keepAllPeptides = ((FilterMultipleProteinMatches ==
                                         BackgroundProteome.DuplicateProteinsFilter.AddToAll || numMultipleMatches == 0)
                                        && (Settings.Default.LibraryPeptidesAddUnmatched || numUnmatched == 0)
                                        && (Settings.Default.LibraryPeptidesKeepFiltered || numFiltered == 0));
                // If the user is filtering some peptides, or if the user clicked cancel, remove all rows added as
                // a result of the paste.
                if (result == DialogResult.Cancel || !keepAllPeptides)
                    RemoveLastRows(dataGridView, dataGridView.RowCount - prevRowCount);
                // Redo the paste with the new filter settings.
                if (result != DialogResult.Cancel && !keepAllPeptides)
                    Paste(dataGridView, text, enumerateProteins, !enumerateProteins, out numUnmatched,
                          out numMultipleMatches, out numFiltered);
            }
        }

        /// <summary>
        /// Paste the clipboard text into the specified DataGridView.
        /// The clipboard text is assumed to be tab separated values.
        /// The values are matched up to the columns in the order they are displayed.
        /// </summary>
        private void Paste(DataGridView dataGridView, string text, bool enumerateProteins, bool keepAllPeptides,
            out int numUnmatched, out int numMulitpleMatches, out int numFiltered)
        {
            numUnmatched = numMulitpleMatches = numFiltered = 0;
            var columns = new DataGridViewColumn[dataGridView.Columns.Count];
            dataGridView.Columns.CopyTo(columns, 0);
            Array.Sort(columns, (a,b)=>a.DisplayIndex - b.DisplayIndex);
            HashSet<string> listPepSeqs = new HashSet<string>();

            foreach (var values in ParseColumnarData(text))
            {
                var row = dataGridView.Rows[dataGridView.Rows.Add()];
                var valueEnumerator = values.GetEnumerator();
                foreach (DataGridViewColumn column in columns)
                {
                    if (column.ReadOnly || !column.Visible)
                    {
                        continue;
                    }
                    if (!valueEnumerator.MoveNext())
                    {
                        break;
                    }
                    row.Cells[column.Index].Value = valueEnumerator.Current;
                }
                if (enumerateProteins)
                {
                    EnumerateProteins(dataGridView, row.Index, keepAllPeptides, ref numUnmatched, ref numMulitpleMatches, 
                        ref numFiltered, listPepSeqs);
                }
            }
        }

        static IEnumerable<IList<string>> ParseColumnarData(String text)
        {
            IFormatProvider formatProvider;
            char separator;
            Type[] types;

            if (!MassListImporter.IsColumnar(text, out formatProvider, out separator, out types))
            {
                string line;
                var reader = new StringReader(text);
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    yield return new[] {line};
                }
            }
            else
            {
                string line;
                var reader = new StringReader(text);
                while ((line = reader.ReadLine()) != null)
                {
                    // Avoid trimming off tabs, which will shift columns
                    line = line.Trim('\r', '\n', TextUtil.SEPARATOR_SPACE); // Not L10N
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    yield return line.Split(new[] { separator });
                }
            }
        }


        private List<String> GetProteinNamesForPeptideSequence(String peptideSequence)
        {
            var document = DocumentUiContainer.Document;
            var backgroundProteome = document.Settings.PeptideSettings.BackgroundProteome;
            if (backgroundProteome.IsNone)
            {
                return null;
            }
            using (var proteomeDb = backgroundProteome.OpenProteomeDb())
            {
                var digestion = backgroundProteome.GetDigestion(proteomeDb, document.Settings.PeptideSettings);
                if (digestion == null)
                {
                    return null;
                }
                var proteins = digestion.GetProteinsWithSequence(peptideSequence);
                return proteins.ConvertAll(protein => protein.Name);
            }
        }

        private void OnCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            _statementCompletionEditBox.HideStatementCompletionForm();
        }

        private void gridViewTransitionList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.V && e.Modifiers == Keys.Control)
            {
                if (!gridViewTransitionList.IsCurrentCellInEditMode)
                {
                    PasteTransitions();
                    e.Handled = true;
                }
            }
        }

        private void OnLoad(object sender, EventArgs e)
        {
            // If you set this in the Designer, DataGridView has a defect that causes it to throw an
            // exception if the the cursor is positioned over the record selector column during loading.
            gridViewPeptides.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridViewProteins.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridViewTransitionList.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        #region Testing

        public class FastaTab : IFormView {}
        public class ProteinListTab : IFormView { }
        public class PeptideListTab : IFormView { }
        public class TransitionListTab : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new FastaTab(), new ProteinListTab(), new PeptideListTab(), new TransitionListTab()
        };

        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = GetSelectedTabIndex()));
                return TAB_PAGES[selectedIndex];
            }
        }

        private int GetSelectedTabIndex()
        {
            if (tabControl1.SelectedTab == tabPageFasta)
                return 0;
            else if (tabControl1.SelectedTab == tabPageProteinList)
                return 1;
            else if (tabControl1.SelectedTab == tabPagePeptideList)
                return 2;
            return 3;
        }

        public int PeptideRowCount
        {
            get { return gridViewPeptides.RowCount; }
        }

        public int TransitionRowCount
        {
            get { return gridViewTransitionList.RowCount; }
        }

        public bool PeptideRowsContainProtein(Predicate<string> found)
        {
            var peptideRows = new DataGridViewRow[gridViewPeptides.RowCount];
            gridViewPeptides.Rows.CopyTo(peptideRows, 0);
            return peptideRows.Take(gridViewPeptides.RowCount-1).Contains(row =>
            {
                var protein = row.Cells[colPeptideProtein.Index].Value;
                return found(protein != null ? protein.ToString() : null);
            });
        }

        public bool PeptideRowsContainPeptide(Predicate<string> found)
        {
            var peptideRows = new DataGridViewRow[gridViewPeptides.RowCount];
            gridViewPeptides.Rows.CopyTo(peptideRows, 0);
            return peptideRows.Take(gridViewPeptides.RowCount-1).Contains(row =>
            {
                var peptide = row.Cells[colPeptideSequence.Index].Value;
                return found(peptide != null ? peptide.ToString() : null);
            });
        }

        public bool TransitionListRowsContainProtein(Predicate<string> found)
        {
            var transitionListRows = new DataGridViewRow[gridViewTransitionList.RowCount];
            gridViewPeptides.Rows.CopyTo(transitionListRows, 0);
            return transitionListRows.Take(gridViewTransitionList.RowCount-1).Contains(row =>
            {
                var protein = row.Cells[colTransitionProteinName.Index].Value;
                return found(protein != null ? protein.ToString() : null);
            });
        }

        public void ClearRows()
        {
           if(PasteFormat == PasteFormat.peptide_list)
               gridViewPeptides.Rows.Clear();
            if(PasteFormat == PasteFormat.transition_list)
                gridViewTransitionList.Rows.Clear();
        }

        #endregion

        private void radioPeptide_CheckedChanged(object sender, EventArgs e)
        {
            UpdateMoleculeType();
        }

        private void UpdateMoleculeType()
        {
            bool isPeptide = radioPeptide.Checked;

            //Skip updating if nothing needs to be changed
            if ((isPeptide && gridViewTransitionList.ColumnCount == 5) || (!isPeptide && gridViewTransitionList.ColumnCount == 6))
                return;

            int rowCount = gridViewTransitionList.RowCount - 1;

            if (rowCount > 0)
            {
                if (
                    MultiButtonMsgDlg.Show(this,
                        string.Format(
                            Resources.PasteDlg_UpdateMoleculeType_Possible_loss_of_data_could_occur_if_you_switch_to__0___Do_you_want_to_continue_,
                            isPeptide ? radioPeptide.Text : radioMolecule.Text), MultiButtonMsgDlg.BUTTON_YES) ==
                    DialogResult.Cancel)
                {
                    radioPeptide.Checked = !isPeptide;
                    return;
                }
            }

            var peptideGroupNames = new string[rowCount];
            var peptideNames = new string[rowCount];
            var precursorMzs = new string[rowCount];
            var productMzs = new string[rowCount];

            for (int i = 0; i < rowCount; i ++)
            {
                peptideGroupNames[i] = Convert.ToString(gridViewTransitionList.Rows[i].Cells[(isPeptide ? 0 : 3)].Value);
                peptideNames[i] = Convert.ToString(gridViewTransitionList.Rows[i].Cells[(isPeptide ? 1 : 0)].Value);
                precursorMzs[i] = Convert.ToString(gridViewTransitionList.Rows[i].Cells[(isPeptide ? 4 : 1)].Value);
                productMzs[i] = Convert.ToString(gridViewTransitionList.Rows[i].Cells[(isPeptide ? 5 : 2)].Value);
            }

            gridViewTransitionList.Columns.Clear();
                        
            if (isPeptide)
            {
                gridViewTransitionList.Columns.Add("Peptide", Resources.PasteDlg_UpdateMoleculeType_Peptide); // Not L10N
                gridViewTransitionList.Columns.Add("Precursor", Resources.PasteDlg_UpdateMoleculeType_Precursor_m_z);  // Not L10N
                gridViewTransitionList.Columns.Add("Product", Resources.PasteDlg_UpdateMoleculeType_Product_m_z); // Not L10N
                gridViewTransitionList.Columns.Add("Protein", Resources.PasteDlg_UpdateMoleculeType_Protein_name); // Not L10N
                gridViewTransitionList.Columns.Add("Description", Resources.PasteDlg_UpdateMoleculeType_Protein_description); // Not L10N
            }
            else
            {
                gridViewTransitionList.Columns.Add("MoleculeGroup", Resources.PasteDlg_UpdateMoleculeType_Molecule_Class); // Not L10N
                gridViewTransitionList.Columns.Add("Name", Resources.PasteDlg_UpdateMoleculeType_Short_Name); // Not L10N
                gridViewTransitionList.Columns.Add("PreFormula", Resources.PasteDlg_UpdateMoleculeType_Precursor_Formula); // Not L10N
                gridViewTransitionList.Columns.Add("ProdFormula", Resources.PasteDlg_UpdateMoleculeType_Product_Formula); // Not L10N
                gridViewTransitionList.Columns.Add("MzPre", Resources.PasteDlg_UpdateMoleculeType_Precursor_m_z); // Not L10N
                gridViewTransitionList.Columns.Add("MzProd", Resources.PasteDlg_UpdateMoleculeType_Product_m_z); // Not L10N
                gridViewTransitionList.Columns.Add("ChargePre", Resources.PasteDlg_UpdateMoleculeType_Precursor_Charge); // Not L10N
                gridViewTransitionList.Columns.Add("ChargeProd", Resources.PasteDlg_UpdateMoleculeType_Product_Charge); // Not L10N
            }

            for (int i = 0; i < rowCount; i ++)
            {
                if (isPeptide)
                {
                    gridViewTransitionList.Rows.Add(peptideNames[i], precursorMzs[i], productMzs[i],
                        peptideGroupNames[i], string.Empty);
                }
                else
                {
                    gridViewTransitionList.Rows.Add(peptideGroupNames[i], peptideNames[i], string.Empty,
                        string.Empty, precursorMzs[i], productMzs[i]);
                }
            }
        }

        public bool IsMolecule
        {
            get { return radioMolecule.Checked; } 
            set { radioMolecule.Checked = value; }
        }
    }

    public enum PasteFormat
    {
        none,
        fasta,
        protein_list,
        peptide_list,
        transition_list,
    }

    public class PasteError
    {
        public String Message { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public int Length { get; set; }
    }

    public class ImportFastaHelper
    {
        public ImportFastaHelper(TextBox tbxFasta, TextBox tbxError, Panel panelError)
        {
            _tbxFasta = tbxFasta;
            _tbxError = tbxError;
            _panelError = panelError;
        }

        public IdentityPath SelectedPath { get; set; }

        private readonly TextBox _tbxFasta;
        private TextBox TbxFasta { get { return _tbxFasta; } }

        private readonly TextBox _tbxError;
        private TextBox TbxError { get { return _tbxError; } }

        private readonly Panel _panelError;
        private Panel PanelError { get { return _panelError; } }

        public SrmDocument AddFasta(SrmDocument document, ref IdentityPath selectedPath, out int emptyPeptideGroups)
        {
            emptyPeptideGroups = 0;
            var text = TbxFasta.Text;
            if (text.Length == 0)
            {
                return document;
            }
            if (!text.StartsWith(">")) // Not L10N
            {
                ShowFastaError(new PasteError
                {
                    Message = Resources.ImportFastaHelper_AddFasta_This_must_start_with____,
                    Column = 0,
                    Length = 1,
                    Line = 0,
                });
                return null;
            }
            string[] lines = text.Split('\n');
            int lastNameLine = -1;
            int aa = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith(">")) // Not L10N
                {
                    if (line.Trim().Length == 1)
                    {
                        ShowFastaError(new PasteError
                        {
                            Message = Resources.ImportFastaHelper_AddFasta_There_is_no_name_for_this_protein,
                            Column = 0,
                            Line = i,
                            Length = 1
                        });
                        return null;
                    }
                    if (!CheckSequence(aa, lastNameLine, lines))
                        return null;
                    lastNameLine = i;
                    aa = 0;
                    continue;
                }

                for (int column = 0; column < line.Length; column++)
                {
                    char c = line[column];
                    if (AminoAcid.IsExAA(c))
                        aa++;
                    else if (!Char.IsWhiteSpace(c) && c != '*')
                    {
                        ShowFastaError(new PasteError
                        {
                            Message =
                                String.Format(Resources.ImportFastaHelper_AddFasta___0___is_not_a_capital_letter_that_corresponds_to_an_amino_acid_, c),
                            Column = column,
                            Line = i,
                            Length = 1,
                        });
                        return null;
                    }
                }
            }

            if (!CheckSequence(aa, lastNameLine, lines))
                return null;

            var importer = new FastaImporter(document, false);
            try
            {
                var reader = new StringReader(TbxFasta.Text);
                IdentityPath to = selectedPath;
                IdentityPath firstAdded, nextAdd;
                // TODO: support long-wait broker
                document = document.AddPeptideGroups(importer.Import(reader, null, -1), false,
                    to, out firstAdded, out nextAdd);
                emptyPeptideGroups = importer.EmptyPeptideGroupCount;
                selectedPath = firstAdded;
            }
            catch (Exception exception)
            {
                ShowFastaError(new PasteError
                {
                    Message = Resources.ImportFastaHelper_AddFasta_An_unexpected_error_occurred__ + exception.Message + " (" + exception.GetType() + ")" // Not L10N
                });
                return null;
            }
            return document;
        }

        private void ShowFastaError(PasteError pasteError)
        {
            PanelError.Visible = true;
            if (pasteError == null)
            {
                TbxError.Text = string.Empty;
                TbxError.Visible = false;
                return;
            }
            TbxError.BackColor = Color.Red;
            TbxError.Text = pasteError.Message;
            TbxError.Visible = true;

            TbxFasta.SelectionStart = Math.Max(0, TbxFasta.GetFirstCharIndexFromLine(pasteError.Line) + pasteError.Column);
            TbxFasta.SelectionLength = Math.Min(pasteError.Length, TbxFasta.Text.Length - TbxFasta.SelectionStart);
            TbxFasta.Focus();
        }

        public void ClearFastaError()
        {
            TbxError.Text = string.Empty;
            TbxError.Visible = false;
            PanelError.Visible = false;
        }

        private bool CheckSequence(int aa, int lastNameLine, string[] lines)
        {
            if (aa == 0 && lastNameLine >= 0)
            {
                ShowFastaError(new PasteError
                {
                    Message = Resources.ImportFastaHelper_CheckSequence_There_is_no_sequence_for_this_protein,
                    Column = 0,
                    Line = lastNameLine,
                    Length = lines[lastNameLine].Length
                });
                return false;
            }
            return true;
        }

        public static SrmDocument HandleEmptyPeptideGroups(IWin32Window parent, int emptyPeptideGroups, SrmDocument docCurrent)
        {
            SrmDocument docNew = docCurrent;
            if (emptyPeptideGroups > FastaImporter.MaxEmptyPeptideGroupCount)
            {
                MessageDlg.Show(parent, String.Format(Resources.SkylineWindow_ImportFasta_This_operation_discarded__0__proteins_with_no_peptides_matching_the_current_filter_settings_, emptyPeptideGroups));
            }
            else if (emptyPeptideGroups > 0)
            {
                using (var dlg = new EmptyProteinsDlg(emptyPeptideGroups))
                {
                    if (dlg.ShowDialog(parent) == DialogResult.Cancel)
                        return null;
                    // Remove all empty proteins, if requested by the user.
                    if (!dlg.IsKeepEmptyProteins)
                        docNew = new RefinementSettings { MinPeptidesPerProtein = 1 }.Refine(docNew);
                }
            }
            return docNew;
        }
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;
using SymptomCheckerApp.Models;
using SymptomCheckerApp.UI.Controls;

namespace SymptomCheckerApp.UI
{
    public partial class MainForm
    {
        private void InitializeModeChrome()
        {
            if (_modeChrome != null) return;

            _modeChrome = new Panel
            {
                Dock = DockStyle.Top,
                Height = ScaleY(52),
                Padding = new Padding(10, 6, 10, 6),
                AccessibleName = "Mode chrome"
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var leftStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                AutoSize = true
            };
            leftStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _modeStatusLabel = new Label
            {
                AutoSize = true,
                Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
                Margin = new Padding(0),
                AccessibleName = "Current mode"
            };

            _modeHintLabel = new Label
            {
                AutoSize = true,
                Font = new Font(Font.FontFamily, 8.5f, FontStyle.Regular),
                Margin = new Padding(0, 2, 0, 0),
                AccessibleName = "Mode hint"
            };

            _modeSwitchButton = new Button
            {
                AutoSize = true,
                Margin = new Padding(8, 4, 0, 4),
                Padding = new Padding(12, 6, 12, 6),
                FlatStyle = FlatStyle.Flat,
                AccessibleName = "Switch mode"
            };
            _modeSwitchButton.Click += (s, e) =>
            {
                if (_settingsService?.Settings.UiMode == null)
                {
                    ShowModeSelector();
                }
                else
                {
                    ToggleUiMode();
                }
            };

            leftStack.Controls.Add(_modeStatusLabel, 0, 0);
            leftStack.Controls.Add(_modeHintLabel, 0, 1);
            layout.Controls.Add(leftStack, 0, 0);
            layout.Controls.Add(_modeSwitchButton, 1, 0);
            _modeChrome.Controls.Add(layout);

            Controls.Add(_modeChrome);
            EnsureModeChromeDockOrder();
            UpdateModePresentation();
        }

        private void EnsureModeChromeDockOrder()
        {
            if (_modeChrome == null) return;

            _modeChrome.SendToBack();
            PerformLayout();
        }

        private void EnsureModeSelectorOverlay()
        {
            if (_modeSelectorOverlay != null) return;

            _modeSelectorOverlay = new ModeSelector(_translationService, _darkModeToggle.Checked)
            {
                Dock = DockStyle.Fill,
                Visible = false
            };
            _modeSelectorOverlay.ModeSelected += mode => ApplyUiMode(mode, persist: true);
            Controls.Add(_modeSelectorOverlay);
            _modeSelectorOverlay.BringToFront();
        }

        private void RestoreOrPromptForUiMode()
        {
            if (_settingsService == null)
            {
                UpdateModePresentation();
                return;
            }

            if (_settingsService.Settings.UiMode is UiMode savedMode)
            {
                ApplyUiMode(savedMode, persist: false);
                return;
            }

            _activeUiMode = null;
            UpdateModePresentation();
            ShowModeSelector();
        }

        private void ShowModeSelector()
        {
            EnsureModeSelectorOverlay();
            if (_modeSelectorOverlay == null) return;

            _modeSelectorOverlay.UpdatePresentation(_translationService, _darkModeToggle.Checked);
            _modeSelectorOverlay.Visible = true;
            _modeSelectorOverlay.BringToFront();
            if (_modeChrome != null) _modeChrome.Visible = false;
            _modeSelectorOverlay.Focus();
        }

        private void HideModeSelector()
        {
            if (_modeSelectorOverlay != null)
            {
                _modeSelectorOverlay.Visible = false;
            }
            if (_modeChrome != null)
            {
                _modeChrome.Visible = true;
                EnsureModeChromeDockOrder();
            }
        }

        private void ToggleUiMode()
        {
            if (_settingsService == null) return;

            if (_settingsService.Settings.UiMode is not UiMode currentMode)
            {
                ShowModeSelector();
                return;
            }

            var nextMode = currentMode == UiMode.Patient
                ? UiMode.Professional
                : UiMode.Patient;

            ApplyUiMode(nextMode, persist: true);
        }

        private void ApplyUiMode(UiMode mode, bool persist)
        {
            _activeUiMode = mode;
            bool professional = mode == UiMode.Professional;
            bool createdPatientShell = _patientShell == null;

            SetVisible(true,
                _lblModel,
                _modelSelector,
                _lblThresh,
                _threshold,
                _lblMinMatch,
                _minMatch,
                _lblTopK,
                _topK,
                _weightCatSelector,
                _weightValue,
                _applyWeightButton,
                _clearWeightsButton,
                _nbTempEnable,
                _nbTempValue,
                _syncButton,
                _missingTransButton,
                _openLogsButton,
                _saveSessionButton,
                _loadSessionButton,
                _resetSettingsButton,
                _saveSettingsProfileButton,
                _loadSettingsProfileButton,
                _selectVisibleButton,
                _clearVisibleButton,
                _selectAllButton,
                _deselectAllButton,
                _selectCategoryButton,
                _clearCategoryButton,
                _showOnlyCategory,
                _lblPerf,
                _showLegacyList);

            var rulesRow = FindControl<FlowLayoutPanel>(this, "_rulesRow");
            if (rulesRow != null)
            {
                rulesRow.Visible = true;
            }

            try
            {
                EnsurePatientShell();
                if (_professionalShell != null)
                {
                    _professionalShell.Visible = professional;
                    if (professional)
                    {
                        _professionalShell.BringToFront();
                    }
                }
                if (_patientShell != null)
                {
                    SyncPatientShellFromState(restoreStep: createdPatientShell);
                    _patientShell.Visible = !professional;
                    if (!professional)
                    {
                        _patientShell.BringToFront();
                    }
                }
                if (_mainVerticalSplitHost != null)
                {
                    _mainVerticalSplitHost.Panel2Collapsed = !professional;
                    if (professional)
                    {
                        SyncProfessionalShellFromState();
                        EnsureProfessionalCoreControlsVisible();
                    }
                }
                if (_professionalAiSection != null)
                {
                    _professionalAiSection.Visible = professional;
                }
            }
            catch { }

            if (_collapseBtn != null)
            {
                _collapseBtn.Visible = professional;
            }

            if (persist && _settingsService != null)
            {
                try
                {
                    _settingsService.Settings.UiMode = mode;
                    _settingsService.Save();
                }
                catch { }
            }

            HideModeSelector();
            UpdateModePresentation();

            BeginInvoke(new Action(() =>
            {
                if (professional)
                {
                    _filterBox.Focus();
                }
                else if (_patientShell != null)
                {
                    _patientShell.FocusCurrentStepControl();
                }
                else
                {
                    _checkButton.Focus();
                }
            }));
        }

        private void HandleModeResetAfterSettingsReset()
        {
            _activeUiMode = null;
            _patientSelectedCategory = null;
            if (_patientShell != null)
            {
                _patientShell.SetStep(1, raiseEvent: false);
            }
            SyncProfessionalShellFromState();
            UpdateModePresentation();
            ShowModeSelector();
        }

        private void UpdateModePresentation()
        {
            string modeText = _activeUiMode switch
            {
                UiMode.Patient => TMode("Mode_Patient", "Patient (guided)"),
                UiMode.Professional => TMode("Mode_Professional", "Professional (advanced)"),
                _ => TMode("Mode_Title", "Choose how you'd like to use Symptom Checker")
            };

            if (_modeStatusLabel != null)
            {
                _modeStatusLabel.Text = modeText;
            }

            if (_modeHintLabel != null)
            {
                _modeHintLabel.Text = TMode("Mode_Switch_Hint", "You can switch mode anytime (Ctrl+M).");
            }

            if (_modeSwitchButton != null)
            {
                _modeSwitchButton.Text = TMode("Mode_Switch_Button", "Switch mode");
            }

            if (_modeChrome != null)
            {
                bool dark = _darkModeToggle.Checked;
                _modeChrome.BackColor = dark ? Color.FromArgb(34, 38, 42) : Color.FromArgb(245, 247, 250);
                if (_modeStatusLabel != null)
                {
                    _modeStatusLabel.ForeColor = dark ? Color.WhiteSmoke : Color.FromArgb(28, 28, 28);
                }
                if (_modeHintLabel != null)
                {
                    _modeHintLabel.ForeColor = dark ? Color.Gainsboro : Color.DimGray;
                }
                if (_modeSwitchButton != null)
                {
                    _modeSwitchButton.BackColor = dark ? Color.FromArgb(54, 62, 72) : Color.White;
                    _modeSwitchButton.ForeColor = dark ? Color.WhiteSmoke : Color.FromArgb(28, 28, 28);
                }
            }

            _modeSelectorOverlay?.UpdatePresentation(_translationService, _darkModeToggle.Checked);
            _patientShell?.UpdatePresentation(_translationService, _darkModeToggle.Checked);
            UpdateProfessionalShellPresentation();
        }

        private void SetVisible(bool visible, params Control[] controls)
        {
            foreach (var control in controls)
            {
                control.Visible = visible;
            }
        }

        private void EnsureProfessionalCoreControlsVisible()
        {
            EnsureControlSectionExpanded("_topControls");
            EnsureControlSectionExpanded("_vitalsRow");
            EnsureControlSectionExpanded("_rulesRow");
        }

        private void EnsureControlSectionExpanded(string controlName)
        {
            var control = FindControl<Control>(this, controlName);
            if (control == null) return;

            control.Visible = true;

            var current = control.Parent;
            while (current != null)
            {
                current.Visible = true;
                if (current is CollapsibleSection section)
                {
                    section.SetCollapsed(false, raiseEvent: false);
                    break;
                }
                current = current.Parent;
            }
        }

        private string TMode(string key, string fallback) => _translationService?.T(key) ?? fallback;
    }
}
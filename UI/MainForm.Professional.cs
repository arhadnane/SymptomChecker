using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SymptomCheckerApp.UI.Controls;

namespace SymptomCheckerApp.UI
{
    public partial class MainForm
    {
        private static readonly string[] CoreProfessionalSections =
        {
            "pro.model",
            "pro.vitals",
            "pro.rules"
        };

        private ProfessionalShell? _professionalShell;
        private CollapsibleSection? _professionalAiSection;
        private int _professionalAiExpandedHeight = 220;

        private void InitializeProfessionalShell(
            SplitContainer mainLayout,
            SplitContainer split,
            Control leftPanel,
            Control topControls,
            Control vitalsRow,
            Control rulesRow,
            Control resultsHost,
            Control aiTabControl)
        {
            _professionalShell = new ProfessionalShell(
                split,
                leftPanel,
                topControls,
                vitalsRow,
                rulesRow,
                resultsHost,
                _triageBanner,
                _disclaimer,
                _translationService,
                _darkModeToggle.Checked)
            {
                Dock = DockStyle.Fill,
                Visible = true
            };
            _professionalShell.SectionCollapsedChanged += OnProfessionalSectionCollapsedChanged;
            mainLayout.Panel1.Controls.Add(_professionalShell);

            _professionalAiSection = new CollapsibleSection("pro.ai", fillContent: true)
            {
                Dock = DockStyle.Fill
            };
            _professionalAiSection.SetContent(aiTabControl);
            _professionalAiSection.CollapsedChanged += collapsed =>
            {
                PersistProfessionalSectionState("pro.ai", collapsed);
                ApplyAiSectionLayout(collapsed, rememberExpandedHeight: true);
            };
            mainLayout.Panel2.Controls.Add(_professionalAiSection);

            _mainSplitHost = split;
            _mainVerticalSplitHost = mainLayout;
            UpdateProfessionalShellPresentation();
        }

        private void UpdateProfessionalShellPresentation()
        {
            _professionalShell?.UpdatePresentation(_translationService, _darkModeToggle.Checked);

            if (_professionalAiSection != null)
            {
                bool rtl = string.Equals(_translationService?.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
                _professionalAiSection.UpdatePresentation(
                    _translationService?.T("Pro_Section_Ai") ?? "Professional section: AI tools",
                    _darkModeToggle.Checked,
                    rtl);
            }
        }

        private void SyncProfessionalShellFromState()
        {
            EnsureCoreProfessionalSectionsVisible();
            _professionalShell?.ApplyCollapsedState(_settingsService?.Settings.CollapsedSections);
            _professionalShell?.EnsureExpanded(CoreProfessionalSections);

            if (_professionalAiSection != null)
            {
                bool aiCollapsed = IsProfessionalSectionCollapsed("pro.ai");
                _professionalAiSection.SetCollapsed(aiCollapsed, raiseEvent: false);
                ApplyAiSectionLayout(aiCollapsed, rememberExpandedHeight: false);
            }
        }

        // Keep essential controls always visible in Professional mode so users
        // can always access model, vitals and Check actions.
        private void EnsureCoreProfessionalSectionsVisible()
        {
            if (_settingsService == null) return;

            bool changed = false;
            _settingsService.Settings.CollapsedSections ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in CoreProfessionalSections)
            {
                if (!_settingsService.Settings.CollapsedSections.TryGetValue(key, out var collapsed) || collapsed)
                {
                    _settingsService.Settings.CollapsedSections[key] = false;
                    changed = true;
                }
            }

            if (changed)
            {
                _settingsService.Save();
            }
        }

        private void OnProfessionalSectionCollapsedChanged(string key, bool collapsed)
        {
            PersistProfessionalSectionState(key, collapsed);
        }

        private bool IsProfessionalSectionCollapsed(string key)
        {
            return _settingsService?.Settings.CollapsedSections != null &&
                   _settingsService.Settings.CollapsedSections.TryGetValue(key, out var collapsed) &&
                   collapsed;
        }

        private void PersistProfessionalSectionState(string key, bool collapsed)
        {
            if (_settingsService == null) return;

            _settingsService.Settings.CollapsedSections ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            _settingsService.Settings.CollapsedSections[key] = collapsed;
            _settingsService.Save();
        }

        private void ApplyAiSectionLayout(bool collapsed, bool rememberExpandedHeight)
        {
            if (_mainVerticalSplitHost == null || _professionalAiSection == null) return;

            int headerHeight = Math.Max(_professionalAiSection.HeaderHeight + 8, ScaleY(48));
            if (_mainVerticalSplitHost.Height <= 0) return;

            int currentPanel2Height = _mainVerticalSplitHost.Height - _mainVerticalSplitHost.SplitterDistance - _mainVerticalSplitHost.SplitterWidth;
            if (rememberExpandedHeight && !collapsed && currentPanel2Height > headerHeight)
            {
                _professionalAiExpandedHeight = currentPanel2Height;
            }

            if (collapsed)
            {
                if (currentPanel2Height > headerHeight)
                {
                    _professionalAiExpandedHeight = currentPanel2Height;
                }

                _mainVerticalSplitHost.Panel2MinSize = headerHeight;
                int targetDistance = Math.Max(
                    _mainVerticalSplitHost.Panel1MinSize,
                    _mainVerticalSplitHost.Height - headerHeight - _mainVerticalSplitHost.SplitterWidth);
                _mainVerticalSplitHost.SplitterDistance = targetDistance;
            }
            else
            {
                int minPanel2 = Math.Max(headerHeight, ScaleY(140));
                _mainVerticalSplitHost.Panel2MinSize = minPanel2;
                int maxPanel2 = Math.Max(minPanel2, _mainVerticalSplitHost.Height - _mainVerticalSplitHost.Panel1MinSize - _mainVerticalSplitHost.SplitterWidth);
                int desiredPanel2 = Math.Min(Math.Max(_professionalAiExpandedHeight, minPanel2), maxPanel2);
                int targetDistance = Math.Max(
                    _mainVerticalSplitHost.Panel1MinSize,
                    _mainVerticalSplitHost.Height - desiredPanel2 - _mainVerticalSplitHost.SplitterWidth);
                _mainVerticalSplitHost.SplitterDistance = targetDistance;
            }
        }

        private void ApplyProfessionalShellLayout()
        {
            if (_professionalAiSection?.Collapsed == true)
            {
                ApplyAiSectionLayout(collapsed: true, rememberExpandedHeight: false);
            }
        }
    }
}
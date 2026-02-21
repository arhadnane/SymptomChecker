using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Text.Json;

namespace SymptomCheckerApp.UI
{
    // Session save/load and settings profile management
    public partial class MainForm
    {
        private class SessionData
        {
            public List<string> SelectedSymptoms { get; set; } = new();
            public string? Model { get; set; }
            public int ThresholdPercent { get; set; }
            public int MinMatch { get; set; }
            public int TopK { get; set; }
            public string? Language { get; set; }
        }

        private class SettingsProfile
        {
            public string? Language { get; set; }
            public bool DarkMode { get; set; }
            public string? Model { get; set; }
            public int ThresholdPercent { get; set; }
            public int MinMatch { get; set; }
            public int TopK { get; set; }
            public bool ShowOnlyCategory { get; set; }
            public string? SelectedCategory { get; set; }
        }

        private void SaveSession()
        {
            try
            {
                var sfd = new SaveFileDialog { Filter = "Session JSON (*.json)|*.json", FileName = "session.json" };
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                var data = new SessionData
                {
                    SelectedSymptoms = _checkedSymptoms.ToList(),
                    Model = _modelSelector.SelectedItem?.ToString(),
                    ThresholdPercent = (int)_threshold.Value,
                    MinMatch = (int)_minMatch.Value,
                    TopK = (int)_topK.Value,
                    Language = (_languageSelector.SelectedItem as LangItem)?.Code
                };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(sfd.FileName, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSession()
        {
            try
            {
                var ofd = new OpenFileDialog { Filter = "Session JSON (*.json)|*.json" };
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                var json = File.ReadAllText(ofd.FileName);
                var data = JsonSerializer.Deserialize<SessionData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data == null) return;
                _checkedSymptoms = new HashSet<string>(data.SelectedSymptoms ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(data.Language))
                {
                    for (int i = 0; i < _languageSelector.Items.Count; i++)
                    {
                        if (_languageSelector.Items[i] is LangItem li &&
                            data.Language is string lang &&
                            string.Equals(li.Code, lang, StringComparison.OrdinalIgnoreCase))
                        { _languageSelector.SelectedIndex = i; break; }
                    }
                }
                if (!string.IsNullOrEmpty(data.Model))
                {
                    for (int i = 0; i < _modelSelector.Items.Count; i++)
                    {
                        if (_modelSelector.Items[i]?.ToString()?.Equals(data.Model, StringComparison.OrdinalIgnoreCase) == true)
                        { _modelSelector.SelectedIndex = i; break; }
                    }
                }
                _threshold.Value = Math.Max(_threshold.Minimum, Math.Min(_threshold.Maximum, data.ThresholdPercent));
                _minMatch.Value = Math.Max(_minMatch.Minimum, Math.Min(_minMatch.Maximum, data.MinMatch));
                _topK.Value = Math.Max(_topK.Minimum, Math.Min(_topK.Maximum, data.TopK));
                RefreshSymptomList();
                UpdateCheckButtonEnabled();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetSettings()
        {
            if (_settingsService == null) return;
            var confirm = MessageBox.Show(this, _translationService?.T("ConfirmResetSettings") ?? "Reset all settings to defaults?", _translationService?.T("ResetSettings") ?? "Reset Settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;
            _settingsService.Reset();
            _darkModeToggle.Checked = _settingsService.Settings.DarkMode;
            for (int i = 0; i < _modelSelector.Items.Count; i++)
            {
                var it = _modelSelector.Items[i]?.ToString();
                if (it != null && _settingsService.Settings.Model != null && string.Equals(it, _settingsService.Settings.Model, StringComparison.OrdinalIgnoreCase)) { _modelSelector.SelectedIndex = i; break; }
            }
            _threshold.Value = _settingsService.Settings.ThresholdPercent;
            _minMatch.Value = _settingsService.Settings.MinMatch;
            _topK.Value = _settingsService.Settings.TopK;
            _filterBox.Text = _settingsService.Settings.FilterText ?? string.Empty;
            _showOnlyCategory.Checked = _settingsService.Settings.ShowOnlyCategory;
            RefreshSymptomList();
            UpdateDecisionRules();
            UpdatePercRule();
        }

        private void SaveSettingsProfile()
        {
            if (_settingsService == null) return;
            try
            {
                var sfd = new SaveFileDialog { Filter = "Settings Profile (*.json)|*.json", FileName = "settings_profile.json" };
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                var profile = new SettingsProfile
                {
                    Language = _settingsService.Settings.Language,
                    DarkMode = _settingsService.Settings.DarkMode,
                    Model = _settingsService.Settings.Model,
                    ThresholdPercent = _settingsService.Settings.ThresholdPercent,
                    MinMatch = _settingsService.Settings.MinMatch,
                    TopK = _settingsService.Settings.TopK,
                    ShowOnlyCategory = _settingsService.Settings.ShowOnlyCategory,
                    SelectedCategory = _settingsService.Settings.SelectedCategory
                };
                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(sfd.FileName, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSettingsProfile()
        {
            if (_settingsService == null) return;
            try
            {
                var ofd = new OpenFileDialog { Filter = "Settings Profile (*.json)|*.json" };
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                var json = File.ReadAllText(ofd.FileName);
                var profile = JsonSerializer.Deserialize<SettingsProfile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (profile == null) return;
                _settingsService.Settings.Language = profile.Language;
                _settingsService.Settings.DarkMode = profile.DarkMode;
                _settingsService.Settings.Model = profile.Model;
                _settingsService.Settings.ThresholdPercent = profile.ThresholdPercent;
                _settingsService.Settings.MinMatch = profile.MinMatch;
                _settingsService.Settings.TopK = profile.TopK;
                _settingsService.Settings.ShowOnlyCategory = profile.ShowOnlyCategory;
                _settingsService.Settings.SelectedCategory = profile.SelectedCategory;
                _settingsService.Save();
                _darkModeToggle.Checked = profile.DarkMode;
                for (int i = 0; i < _modelSelector.Items.Count; i++)
                {
                    if (_modelSelector.Items[i]?.ToString()?.Equals(profile.Model, StringComparison.OrdinalIgnoreCase) == true) { _modelSelector.SelectedIndex = i; break; }
                }
                _threshold.Value = Math.Max(_threshold.Minimum, Math.Min(_threshold.Maximum, profile.ThresholdPercent));
                _minMatch.Value = Math.Max(_minMatch.Minimum, Math.Min(_minMatch.Maximum, profile.MinMatch));
                _topK.Value = Math.Max(_topK.Minimum, Math.Min(_topK.Maximum, profile.TopK));
                _showOnlyCategory.Checked = profile.ShowOnlyCategory;
                if (!string.IsNullOrEmpty(profile.SelectedCategory))
                {
                    for (int i = 0; i < _categorySelector.Items.Count; i++)
                    {
                        if (_categorySelector.Items[i] is CatItem ci && ci.Canonical.Equals(profile.SelectedCategory, StringComparison.OrdinalIgnoreCase)) { _categorySelector.SelectedIndex = i; break; }
                    }
                }
                if (!string.IsNullOrEmpty(profile.Language))
                {
                    for (int i = 0; i < _languageSelector.Items.Count; i++)
                    {
                        if (_languageSelector.Items[i] is LangItem li && li.Code.Equals(profile.Language, StringComparison.OrdinalIgnoreCase)) { _languageSelector.SelectedIndex = i; break; }
                    }
                }
                RefreshSymptomList();
                UpdateDecisionRules();
                UpdatePercRule();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

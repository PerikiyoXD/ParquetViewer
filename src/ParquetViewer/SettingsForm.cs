using ParquetViewer.Controls;
using ParquetViewer.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ParquetViewer
{
    /// <summary>
    /// A single place to see and change the application's settings.
    /// </summary>
    /// <remarks>
    /// Built in code rather than with the designer so the whole window lives in one file. The settings that
    /// also have menu items stay in sync: this form writes to AppSettings, and the main form refreshes its
    /// menu check marks when the dialog closes.
    /// </remarks>
    public class SettingsForm : FormBase
    {
        private readonly CheckBox _darkModeCheckBox = new();
        private readonly CheckBox _alwaysLoadAllRecordsCheckBox = new();
        private readonly CheckBox _alwaysSelectAllFieldsCheckBox = new();

        private readonly CheckBox _cellPreviewVisibleCheckBox = new();
        private readonly CheckBox _cellPreviewWordWrapCheckBox = new();
        private readonly CheckBox _cellPreviewPrettyPrintCheckBox = new();

        private readonly CheckBox _lineBreakMarkerEnabledCheckBox = new();
        private readonly ComboBox _lineBreakMarkerGlyphComboBox = new();
        private readonly ComboBox _lineBreakMarkerColorComboBox = new();
        private readonly Panel _colorPreview = new();

        private readonly Button _okButton = new();
        private readonly Button _cancelButton = new();

        private Color? _selectedMarkerColor;

        private sealed record ColorOption(string Name, Color? Color);

        //Null means "follow the theme's accent colour", which is the default
        private static readonly ColorOption[] ColorOptions =
        [
            new("Theme accent (default)", null),
            new("Grey", Color.Gray),
            new("Red", Color.FromArgb(220, 80, 80)),
            new("Orange", Color.FromArgb(230, 145, 55)),
            new("Green", Color.FromArgb(80, 175, 100)),
            new("Magenta", Color.FromArgb(200, 90, 190)),
        ];

        private static readonly (string Label, string Glyph)[] GlyphOptions =
        [
            ("¶  Pilcrow", "¶"),
            ("↵  Return arrow", "↵"),
            ("⏎  Return symbol", "⏎"),
            ("⤶  Turn arrow", "⤶"),
            ("\\n  Literal", "\\n"),
        ];

        public SettingsForm()
        {
            this.Text = Resources.Strings.SettingsFormTitle;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(430, 430);
            this.ShowInTaskbar = false;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(12),
                AutoScroll = true
            };

            layout.Controls.Add(BuildGeneralGroup());
            layout.Controls.Add(BuildCellPreviewGroup());
            layout.Controls.Add(BuildLineBreakGroup());

            this.Controls.Add(layout);
            this.Controls.Add(BuildButtonRow());

            LoadSettings();
        }

        private GroupBox BuildGeneralGroup()
        {
            _darkModeCheckBox.Text = Resources.Strings.SettingsDarkModeText;
            _alwaysLoadAllRecordsCheckBox.Text = Resources.Strings.SettingsAlwaysLoadAllRecordsText;
            _alwaysSelectAllFieldsCheckBox.Text = Resources.Strings.SettingsAlwaysSelectAllFieldsText;

            return BuildGroup(Resources.Strings.SettingsGeneralGroupText, 108,
                _darkModeCheckBox, _alwaysLoadAllRecordsCheckBox, _alwaysSelectAllFieldsCheckBox);
        }

        private GroupBox BuildCellPreviewGroup()
        {
            _cellPreviewVisibleCheckBox.Text = Resources.Strings.SettingsCellPreviewVisibleText;
            _cellPreviewWordWrapCheckBox.Text = Resources.Strings.SettingsCellPreviewWordWrapText;
            _cellPreviewPrettyPrintCheckBox.Text = Resources.Strings.SettingsCellPreviewPrettyPrintText;

            return BuildGroup(Resources.Strings.SettingsCellPreviewGroupText, 108,
                _cellPreviewVisibleCheckBox, _cellPreviewWordWrapCheckBox, _cellPreviewPrettyPrintCheckBox);
        }

        private GroupBox BuildLineBreakGroup()
        {
            var group = new GroupBox
            {
                Text = Resources.Strings.SettingsLineBreakGroupText,
                Width = 390,
                Height = 132,
                Margin = new Padding(0, 0, 0, 10)
            };

            _lineBreakMarkerEnabledCheckBox.Text = Resources.Strings.SettingsLineBreakMarkerEnabledText;
            _lineBreakMarkerEnabledCheckBox.Location = new Point(12, 22);
            _lineBreakMarkerEnabledCheckBox.AutoSize = true;
            _lineBreakMarkerEnabledCheckBox.CheckedChanged += (_, _) => UpdateLineBreakControlsEnabled();
            group.Controls.Add(_lineBreakMarkerEnabledCheckBox);

            group.Controls.Add(new Label
            {
                Text = Resources.Strings.SettingsLineBreakGlyphText,
                Location = new Point(12, 52),
                AutoSize = true
            });

            _lineBreakMarkerGlyphComboBox.Location = new Point(120, 48);
            _lineBreakMarkerGlyphComboBox.Width = 250;
            //Editable so any glyph can be typed, not just the presets
            _lineBreakMarkerGlyphComboBox.DropDownStyle = ComboBoxStyle.DropDown;
            foreach (var (label, _) in GlyphOptions)
                _lineBreakMarkerGlyphComboBox.Items.Add(label);
            group.Controls.Add(_lineBreakMarkerGlyphComboBox);

            group.Controls.Add(new Label
            {
                Text = Resources.Strings.SettingsLineBreakColorText,
                Location = new Point(12, 88),
                AutoSize = true
            });

            _lineBreakMarkerColorComboBox.Location = new Point(120, 84);
            _lineBreakMarkerColorComboBox.Width = 210;
            _lineBreakMarkerColorComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var option in ColorOptions)
                _lineBreakMarkerColorComboBox.Items.Add(option.Name);
            _lineBreakMarkerColorComboBox.Items.Add(Resources.Strings.SettingsCustomColorText);
            _lineBreakMarkerColorComboBox.SelectedIndexChanged += ColorComboBox_SelectedIndexChanged;
            group.Controls.Add(_lineBreakMarkerColorComboBox);

            _colorPreview.Location = new Point(340, 84);
            _colorPreview.Size = new Size(30, 22);
            _colorPreview.BorderStyle = BorderStyle.FixedSingle;
            group.Controls.Add(_colorPreview);

            return group;
        }

        private static GroupBox BuildGroup(string title, int height, params Control[] children)
        {
            var group = new GroupBox
            {
                Text = title,
                Width = 390,
                Height = height,
                Margin = new Padding(0, 0, 0, 10)
            };

            var top = 22;
            foreach (var child in children)
            {
                child.Location = new Point(12, top);
                child.AutoSize = true;
                group.Controls.Add(child);
                top += 26;
            }

            return group;
        }

        private Panel BuildButtonRow()
        {
            _okButton.Text = Resources.Strings.SettingsOkText;
            _okButton.DialogResult = DialogResult.OK;
            _okButton.Size = new Size(90, 28);
            _okButton.Click += (_, _) => SaveSettings();

            _cancelButton.Text = Resources.Strings.CancelButtonText;
            _cancelButton.DialogResult = DialogResult.Cancel;
            _cancelButton.Size = new Size(90, 28);

            var panel = new Panel { Dock = DockStyle.Bottom, Height = 46 };
            panel.Controls.Add(_okButton);
            panel.Controls.Add(_cancelButton);

            //Positioned from the panel's own width whenever it changes. Reading this.ClientSize in the
            //constructor gave a stale value, which put both buttons past the right edge of the form where
            //they couldn't be clicked at all.
            void LayoutButtons()
            {
                const int Margin = 12;
                const int Gap = 8;

                _cancelButton.Location = new Point(panel.ClientSize.Width - Margin - _cancelButton.Width, 8);
                _okButton.Location = new Point(_cancelButton.Left - Gap - _okButton.Width, 8);
            }

            panel.SizeChanged += (_, _) => LayoutButtons();
            LayoutButtons();

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;

            return panel;
        }

        private void LoadSettings()
        {
            _darkModeCheckBox.Checked = AppSettings.DarkMode;
            _alwaysLoadAllRecordsCheckBox.Checked = AppSettings.AlwaysLoadAllRecords;
            _alwaysSelectAllFieldsCheckBox.Checked = AppSettings.AlwaysSelectAllFields;

            _cellPreviewVisibleCheckBox.Checked = AppSettings.CellPreviewVisible;
            _cellPreviewWordWrapCheckBox.Checked = AppSettings.CellPreviewWordWrap;
            _cellPreviewPrettyPrintCheckBox.Checked = AppSettings.CellPreviewPrettyPrint;

            _lineBreakMarkerEnabledCheckBox.Checked = AppSettings.LineBreakMarkerEnabled;

            //Show the matching preset label if there is one, otherwise the raw glyph
            var glyph = AppSettings.LineBreakMarkerGlyph;
            var glyphLabel = Array.Find(GlyphOptions, option => option.Glyph == glyph).Label;
            _lineBreakMarkerGlyphComboBox.Text = glyphLabel ?? glyph;

            _selectedMarkerColor = AppSettings.LineBreakMarkerColor;
            var colorIndex = Array.FindIndex(ColorOptions, option => option.Color == _selectedMarkerColor);
            _lineBreakMarkerColorComboBox.SelectedIndex = colorIndex >= 0
                ? colorIndex
                : ColorOptions.Length; //The "Custom..." entry

            UpdateColorPreview();
            UpdateLineBreakControlsEnabled();
        }

        private void SaveSettings()
        {
            AppSettings.AlwaysLoadAllRecords = _alwaysLoadAllRecordsCheckBox.Checked;
            AppSettings.AlwaysSelectAllFields = _alwaysSelectAllFieldsCheckBox.Checked;

            AppSettings.CellPreviewVisible = _cellPreviewVisibleCheckBox.Checked;
            AppSettings.CellPreviewWordWrap = _cellPreviewWordWrapCheckBox.Checked;
            AppSettings.CellPreviewPrettyPrint = _cellPreviewPrettyPrintCheckBox.Checked;

            AppSettings.LineBreakMarkerEnabled = _lineBreakMarkerEnabledCheckBox.Checked;
            AppSettings.LineBreakMarkerGlyph = ResolveGlyphFromInput(_lineBreakMarkerGlyphComboBox.Text);
            AppSettings.LineBreakMarkerColor = _selectedMarkerColor;

            //Assigned last: its setter re-themes every open form, so let the rest land first
            AppSettings.DarkMode = _darkModeCheckBox.Checked;
        }

        /// <summary>
        /// Turns whatever is in the glyph box into the marker to store.
        /// </summary>
        private static string ResolveGlyphFromInput(string input)
        {
            foreach (var (label, glyph) in GlyphOptions)
            {
                if (label == input)
                    return glyph;
            }

            //Typed directly. Trim so a stray space doesn't become the marker, and fall back if it's empty.
            var trimmed = input.Trim();
            return trimmed.Length > 0 ? trimmed : AppSettings.DefaultLineBreakMarkerGlyph;
        }

        private void ColorComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var index = _lineBreakMarkerColorComboBox.SelectedIndex;
            if (index >= 0 && index < ColorOptions.Length)
            {
                _selectedMarkerColor = ColorOptions[index].Color;
            }
            else
            {
                using var dialog = new ColorDialog
                {
                    Color = _selectedMarkerColor ?? AppSettings.GetTheme().HyperlinkColor,
                    FullOpen = true
                };

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _selectedMarkerColor = dialog.Color;
                }
                else
                {
                    //Cancelled, so go back to whatever was selected before
                    var previous = Array.FindIndex(ColorOptions, option => option.Color == _selectedMarkerColor);
                    _lineBreakMarkerColorComboBox.SelectedIndex = previous >= 0 ? previous : 0;
                    return;
                }
            }

            UpdateColorPreview();
        }

        private void UpdateColorPreview()
            => _colorPreview.BackColor = _selectedMarkerColor ?? AppSettings.GetTheme().HyperlinkColor;

        private void UpdateLineBreakControlsEnabled()
        {
            var enabled = _lineBreakMarkerEnabledCheckBox.Checked;
            _lineBreakMarkerGlyphComboBox.Enabled = enabled;
            _lineBreakMarkerColorComboBox.Enabled = enabled;
            _colorPreview.Enabled = enabled;
        }

        public override void SetTheme(Theme theme)
        {
            base.SetTheme(theme);

            foreach (var control in EnumerateControls(this))
            {
                switch (control)
                {
                    case GroupBox or Label or CheckBox or Panel:
                        control.ForeColor = theme.TextColor;
                        break;
                    case Button button:
                        //Buttons keep a light background in both themes, matching the main form
                        button.BackColor = Color.White;
                        button.ForeColor = Color.Black;
                        break;
                    case ComboBox comboBox:
                        comboBox.BackColor = theme.CellBackgroundColor;
                        comboBox.ForeColor = theme.TextColor;
                        break;
                }
            }

            //Restore the swatch, which the loop above would otherwise repaint
            UpdateColorPreview();
        }

        private static IEnumerable<Control> EnumerateControls(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;

                foreach (var descendant in EnumerateControls(child))
                    yield return descendant;
            }
        }
    }
}

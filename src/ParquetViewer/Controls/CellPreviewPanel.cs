using ParquetViewer.Engine.Types;
using ParquetViewer.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace ParquetViewer.Controls
{
    /// <summary>
    /// A panel that shows the full, untruncated value of the currently selected grid cell.
    /// </summary>
    /// <remarks>
    /// The grid truncates cell text at <see cref="ParquetGridView.MAX_CHARACTERS_THAT_CAN_BE_RENDERED_IN_A_CELL"/>
    /// and has tooltips switched off for performance, so without this there is no way to read a long value.
    /// Updates are debounced: selection changes fire on every arrow key press, and formatting a large value
    /// on each one is exactly the cost that got tooltips disabled in the first place.
    /// </remarks>
    public class CellPreviewPanel : Panel
    {
        private const int DebounceMilliseconds = 150;

        //Above this we stop offering pretty printing, since indenting a huge document is what actually costs
        private const int MaxCharactersToPrettyPrint = 512 * 1024;

        private readonly TextBox _previewTextBox;
        private readonly Label _headerLabel;
        private readonly CheckBox _wordWrapCheckBox;
        private readonly CheckBox _prettyPrintCheckBox;
        private readonly Button _copyButton;
        private readonly Panel _toolbar;
        private readonly System.Windows.Forms.Timer _debounceTimer;

        private DataGridView? _grid;
        private string _rawValue = string.Empty;
        private bool _isFormattable;

        public CellPreviewPanel()
        {
            //Created first because the toolbar's event handlers below capture it
            this._previewTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = true,
                BorderStyle = BorderStyle.None,
                Font = new Font(FontFamily.GenericMonospace, 9F)
            };

            this._headerLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Left,
                Width = 260,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0),
                Text = Resources.Strings.CellPreviewNoSelectionText
            };

            this._wordWrapCheckBox = new CheckBox
            {
                Text = Resources.Strings.CellPreviewWordWrapText,
                AutoSize = true,
                Dock = DockStyle.Left,
                Checked = true,
                Padding = new Padding(8, 0, 0, 0)
            };
            this._wordWrapCheckBox.CheckedChanged += (_, _) =>
            {
                this._previewTextBox.WordWrap = this._wordWrapCheckBox.Checked;
                AppSettings.CellPreviewWordWrap = this._wordWrapCheckBox.Checked;
            };

            this._prettyPrintCheckBox = new CheckBox
            {
                Text = Resources.Strings.CellPreviewPrettyPrintText,
                AutoSize = true,
                Dock = DockStyle.Left,
                Checked = false,
                Padding = new Padding(8, 0, 0, 0)
            };
            this._prettyPrintCheckBox.CheckedChanged += (_, _) =>
            {
                AppSettings.CellPreviewPrettyPrint = this._prettyPrintCheckBox.Checked;
                RenderValue();
            };

            this._copyButton = new Button
            {
                Text = Resources.Strings.CellPreviewCopyText,
                AutoSize = true,
                Dock = DockStyle.Left,
                Padding = new Padding(4, 0, 4, 0)
            };
            this._copyButton.Click += CopyButton_Click;

            //Docked controls fill in reverse declaration order, so add right to left
            this._toolbar = new Panel { Dock = DockStyle.Top, Height = 26 };
            this._toolbar.Controls.Add(this._copyButton);
            this._toolbar.Controls.Add(this._prettyPrintCheckBox);
            this._toolbar.Controls.Add(this._wordWrapCheckBox);
            this._toolbar.Controls.Add(this._headerLabel);

            this.Controls.Add(this._previewTextBox);
            this.Controls.Add(this._toolbar);

            this._debounceTimer = new System.Windows.Forms.Timer { Interval = DebounceMilliseconds };
            this._debounceTimer.Tick += DebounceTimer_Tick;
        }

        /// <summary>
        /// Starts previewing the selected cell of the given grid.
        /// </summary>
        public void Attach(DataGridView grid)
        {
            ArgumentNullException.ThrowIfNull(grid);

            Detach();

            this._grid = grid;
            this._grid.SelectionChanged += Grid_SelectionChanged;
            this._grid.CurrentCellChanged += Grid_SelectionChanged;

            this._wordWrapCheckBox.Checked = AppSettings.CellPreviewWordWrap;
            this._prettyPrintCheckBox.Checked = AppSettings.CellPreviewPrettyPrint;

            ScheduleRefresh();
        }

        /// <summary>
        /// Re-reads the settings this panel mirrors, after they've been changed elsewhere.
        /// </summary>
        public void RefreshSettings()
        {
            this._wordWrapCheckBox.Checked = AppSettings.CellPreviewWordWrap;
            this._prettyPrintCheckBox.Checked = AppSettings.CellPreviewPrettyPrint;
            ScheduleRefresh();
        }

        public void Detach()
        {
            if (this._grid is null)
                return;

            this._grid.SelectionChanged -= Grid_SelectionChanged;
            this._grid.CurrentCellChanged -= Grid_SelectionChanged;
            this._grid = null;
        }

        public void SetTheme(Theme theme)
        {
            this.BackColor = theme.FormBackgroundColor;
            this.ForeColor = theme.TextColor;

            this._toolbar.BackColor = theme.FormBackgroundColor;
            this._toolbar.ForeColor = theme.TextColor;
            this._headerLabel.ForeColor = theme.TextColor;
            this._wordWrapCheckBox.ForeColor = theme.TextColor;
            this._prettyPrintCheckBox.ForeColor = theme.TextColor;

            this._previewTextBox.BackColor = theme.CellBackgroundColor;
            this._previewTextBox.ForeColor = theme.TextColor;

            //Buttons keep a light background in both themes, matching the query buttons on the main form
            this._copyButton.BackColor = Color.White;
            this._copyButton.ForeColor = Color.Black;
        }

        private void Grid_SelectionChanged(object? sender, EventArgs e) => ScheduleRefresh();

        private void ScheduleRefresh()
        {
            //Restart the timer so holding down an arrow key only formats the cell it lands on
            this._debounceTimer.Stop();
            this._debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            this._debounceTimer.Stop();
            RefreshFromGrid();
        }

        private void RefreshFromGrid()
        {
            if (!this.Visible)
                return; //Nothing to show, and no reason to pay for formatting

            var cell = this._grid?.CurrentCell;
            if (cell is null)
            {
                SetEmpty(Resources.Strings.CellPreviewNoSelectionText);
                return;
            }

            var columnName = this._grid!.Columns[cell.ColumnIndex].HeaderText;
            var value = cell.Value;

            if (value is null || value == DBNull.Value)
            {
                SetEmpty($"{columnName} [{cell.RowIndex}] — NULL");
                return;
            }

            (this._rawValue, this._isFormattable) = GetRawValue(value);

            var characterCount = this._rawValue.Length;
            this._headerLabel.Text = $"{columnName} [{cell.RowIndex}] — {value.GetType().Name}, {characterCount:N0} chars";
            this._prettyPrintCheckBox.Enabled = this._isFormattable && characterCount <= MaxCharactersToPrettyPrint;
            this._copyButton.Enabled = true;

            RenderValue();
        }

        /// <summary>
        /// Returns the untruncated text of a cell value, and whether it is worth offering to pretty print.
        /// </summary>
        private static (string Text, bool IsFormattable) GetRawValue(object value) => value switch
        {
            //These already serialize themselves as JSON, so they're the ones worth reformatting
            IStructValue structValue => (structValue.ToString() ?? string.Empty, true),
            IListValue listValue => (listValue.ToString() ?? string.Empty, true),
            IMapValue mapValue => (mapValue.ToString() ?? string.Empty, true),

            //Byte arrays render as their full hex rather than the grid's truncated form
            IByteArrayValue byteArrayValue => (byteArrayValue.ToString() ?? string.Empty, false),

            //A plain string might still be JSON that someone stored in a text column
            string text => (text, LooksLikeJson(text)),

            _ => (value.ToString() ?? string.Empty, false)
        };

        private static bool LooksLikeJson(string text)
        {
            var trimmed = text.AsSpan().Trim();
            return trimmed.Length > 1
                && ((trimmed[0] == '{' && trimmed[^1] == '}')
                    || (trimmed[0] == '[' && trimmed[^1] == ']'));
        }

        private void RenderValue()
        {
            var shouldPrettyPrint = this._prettyPrintCheckBox.Checked
                && this._prettyPrintCheckBox.Enabled
                && this._isFormattable;

            var text = shouldPrettyPrint
                ? TryPrettyPrint(this._rawValue)
                : this._rawValue;

            this._previewTextBox.Text = NormalizeLineEndings(text);

            this._previewTextBox.SelectionStart = 0;
            this._previewTextBox.SelectionLength = 0;
        }

        /// <summary>
        /// Converts lone LF and CR line breaks to CRLF for display.
        /// </summary>
        /// <remarks>
        /// A multiline TextBox only breaks lines on CRLF. Data written on non-Windows systems typically
        /// uses bare LF, which the control renders as nothing at all, so the text ran together on screen
        /// even though the newlines were really there and came back on copy.
        /// </remarks>
        private static string NormalizeLineEndings(string text)
        {
            if (text.Length == 0)
                return text;

            //Cheap check first: most values have no bare line breaks, and this runs on every selection
            var needsNormalizing = false;
            for (var i = 0; i < text.Length; i++)
            {
                var current = text[i];
                if (current == '\n')
                {
                    //A LF that isn't preceded by CR
                    if (i == 0 || text[i - 1] != '\r')
                    {
                        needsNormalizing = true;
                        break;
                    }
                }
                else if (current == '\r')
                {
                    //A CR that isn't followed by LF (old Mac style)
                    if (i == text.Length - 1 || text[i + 1] != '\n')
                    {
                        needsNormalizing = true;
                        break;
                    }
                }
            }

            if (!needsNormalizing)
                return text;

            var builder = new StringBuilder(text.Length + 16);
            for (var i = 0; i < text.Length; i++)
            {
                var current = text[i];
                if (current == '\r')
                {
                    builder.Append("\r\n");

                    //Skip the LF of an existing CRLF pair so it isn't doubled
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                }
                else if (current == '\n')
                {
                    builder.Append("\r\n");
                }
                else
                {
                    builder.Append(current);
                }
            }

            return builder.ToString();
        }

        private static string TryPrettyPrint(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (JsonException ex)
            {
                //Not actually JSON, or malformed. Showing the raw value is more useful than an error.
                System.Diagnostics.Trace.TraceInformation($"Cell value could not be pretty printed: {ex.Message}");
                return json;
            }
        }

        private void SetEmpty(string header)
        {
            this._rawValue = string.Empty;
            this._isFormattable = false;
            this._headerLabel.Text = header;
            this._previewTextBox.Text = string.Empty;
            this._prettyPrintCheckBox.Enabled = false;
            this._copyButton.Enabled = false;
        }

        private void CopyButton_Click(object? sender, EventArgs e)
        {
            if (this._rawValue.Length == 0)
                return;

            try
            {
                //If there's a selection, copy exactly that. Otherwise copy the value as it came from the
                //file rather than the displayed text, so the original line endings survive the round trip.
                var selection = this._previewTextBox.SelectedText;
                if (selection.Length > 0)
                {
                    Clipboard.SetText(selection);
                    return;
                }

                var shouldPrettyPrint = this._prettyPrintCheckBox.Checked
                    && this._prettyPrintCheckBox.Enabled
                    && this._isFormattable;

                Clipboard.SetText(shouldPrettyPrint ? TryPrettyPrint(this._rawValue) : this._rawValue);
            }
            catch (Exception ex)
            {
                //Clipboard access fails if another process is holding it open
                System.Diagnostics.Trace.TraceError($"Failed to copy the cell preview to the clipboard: {ex}");
                MessageBox.Show(this, ex.Message, Resources.Errors.CopyToClipboardErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);

            if (this.Visible)
                ScheduleRefresh(); //Catch up on whatever was selected while we were hidden
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Detach();
                this._debounceTimer.Stop();
                this._debounceTimer.Dispose();
                this._previewTextBox.Font?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

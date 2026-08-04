using ParquetViewer.Controls;
using System;
using System.Windows.Forms;

namespace ParquetViewer
{
    public partial class MainForm
    {
        private CellPreviewPanel? _cellPreviewPanel;
        private Splitter? _cellPreviewSplitter;
        private ToolStripMenuItem? _cellPreviewMenuItem;

        /// <summary>
        /// Adds the cell preview panel underneath the grid.
        /// </summary>
        /// <remarks>
        /// Hosted on the form rather than inside mainTableLayoutPanel. The grid lives in a table cell and is
        /// anchored, not docked, so docking siblings next to it inside that cell gives them no space. The
        /// table itself is Dock.Fill on the form, so docking the panel to the bottom of the form reserves
        /// height and lets the table shrink into what's left. This keeps the designer and its .resx
        /// untouched, which matters because the table's layout is driven from resources.
        /// </remarks>
        private void InitializeCellPreview()
        {
            this._cellPreviewPanel = new CellPreviewPanel
            {
                Dock = DockStyle.Bottom,
                Height = AppSettings.CellPreviewHeight,
                Visible = AppSettings.CellPreviewVisible
            };

            this._cellPreviewSplitter = new Splitter
            {
                Dock = DockStyle.Bottom,
                Height = 5,
                MinExtra = 120, //Keep a usable amount of grid visible
                MinSize = 60,
                Visible = AppSettings.CellPreviewVisible
            };
            this._cellPreviewSplitter.SplitterMoved += (_, _) =>
            {
                if (this._cellPreviewPanel is not null)
                    AppSettings.CellPreviewHeight = this._cellPreviewPanel.Height;
            };

            this.Controls.Add(this._cellPreviewPanel);
            this.Controls.Add(this._cellPreviewSplitter);

            //Docked siblings claim space from the back of the z-order forwards, so the Fill control has to
            //end up in front of everything that reserves an edge. Bring them forward in the order they
            //should claim space: status strip at the very bottom edge, then the panel, then the splitter,
            //and finally the table, which fills whatever is left.
            this.mainStatusStrip.BringToFront();
            this._cellPreviewPanel.BringToFront();
            this._cellPreviewSplitter.BringToFront();
            this.mainTableLayoutPanel.BringToFront();

            this._cellPreviewPanel.Attach(this.mainGridView);
            this._cellPreviewPanel.SetTheme(AppSettings.GetTheme());

            AddCellPreviewMenuItem();
        }

        private void AddCellPreviewMenuItem()
        {
            this._cellPreviewMenuItem = new ToolStripMenuItem(Resources.Strings.CellPreviewMenuItemText)
            {
                CheckOnClick = true,
                Checked = AppSettings.CellPreviewVisible
            };
            this._cellPreviewMenuItem.CheckedChanged += (_, _) => SetCellPreviewVisible(this._cellPreviewMenuItem!.Checked);

            //Sits next to the other view level toggles in the Edit menu rather than in a menu of its own
            var insertAt = this.editToolStripMenuItem.DropDownItems.IndexOf(this.darkModeToolStripMenuItem) + 1;
            this.editToolStripMenuItem.DropDownItems.Insert(insertAt, this._cellPreviewMenuItem);

            //Everything the settings window can change is also reachable from the menus, so give it a
            //home here too, separated from the toggles above it.
            var settingsMenuItem = new ToolStripMenuItem(Resources.Strings.SettingsMenuItemText);
            settingsMenuItem.Click += (_, _) => ShowSettingsDialog();

            this.editToolStripMenuItem.DropDownItems.Add(new ThemableToolStripSeperator());
            this.editToolStripMenuItem.DropDownItems.Add(settingsMenuItem);
        }

        private void ShowSettingsDialog()
        {
            using var settingsForm = new SettingsForm();
            if (settingsForm.ShowDialog(this) != DialogResult.OK)
                return;

            //The dialog writes straight to AppSettings, so pull everything that mirrors it back into sync
            RefreshSettingsDependentState();
        }

        /// <summary>
        /// Re-reads settings that are also surfaced elsewhere, so the menus and the grid agree with them.
        /// </summary>
        private void RefreshSettingsDependentState()
        {
            this.alwaysLoadAllRecordsToolStripMenuItem.Checked = AppSettings.AlwaysLoadAllRecords;
            this.darkModeToolStripMenuItem.Checked = AppSettings.DarkMode;

            if (this._cellPreviewMenuItem is not null)
                this._cellPreviewMenuItem.Checked = AppSettings.CellPreviewVisible;

            SetCellPreviewVisible(AppSettings.CellPreviewVisible);
            this._cellPreviewPanel?.RefreshSettings();
            this.mainGridView.RefreshLineBreakMarkerSettings();
        }

        private void SetCellPreviewVisible(bool visible)
        {
            if (this._cellPreviewPanel is null || this._cellPreviewSplitter is null)
                return;

            this._cellPreviewPanel.Visible = visible;
            this._cellPreviewSplitter.Visible = visible;
            AppSettings.CellPreviewVisible = visible;
        }

        private void SetCellPreviewTheme(Helpers.Theme theme) => this._cellPreviewPanel?.SetTheme(theme);
    }
}

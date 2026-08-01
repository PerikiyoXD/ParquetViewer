using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ParquetViewer
{
    public class LoadingIcon : IDisposable, IProgress<int>
    {
        private const int LoadingPanelWidth = 200;
        private const int LoadingPanelHeight = 200;

        private readonly Form _form;
        private readonly Panel _panel;
        private readonly Button _cancelButton;
        private readonly long _loadingBarMax = 0;
        private readonly CancellationTokenSource _cancellationToken = new();
        private readonly EventHandler _onFormSizeChanged;
        private long _progressSoFar = 0;
        private int _progressRatio = 0;

        public CancellationToken CancellationToken => this._cancellationToken.Token;

        public event EventHandler? OnShow;
        public event EventHandler? OnHide;

        public LoadingIcon(Form form, string message, long loadingBarMax = 0)
        {
            ArgumentNullException.ThrowIfNull(form);

            this._form = form;
            this._panel = new Panel();
            this._panel.BorderStyle = BorderStyle.FixedSingle;
            this._panel.Size = new Size(LoadingPanelWidth, LoadingPanelHeight);
            this._panel.Location = this.GetFormCenter();
            this._loadingBarMax = loadingBarMax;

            this._panel.Controls.Add(new Label()
            {
                Name = "loadingmessagelabel",
                Text = message,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Regular)
            });

            var pictureBox = new PictureBox()
            {
                Name = "loadingpicturebox",
                Image = Resources.Icons.hourglass,
                Size = new Size(200, 200)
            };
            this._panel.Controls.Add(pictureBox);

            this._cancelButton = new Button()
            {
                Name = "cancelloadingbutton",
                Text = Resources.Strings.CancelButtonText,
                Dock = DockStyle.Bottom,
                Enabled = this._cancellationToken.Token.CanBeCanceled,
                BackColor = Color.White,
                ForeColor = Color.Black
            };
            this._cancelButton.Click += (object? buttonSender, EventArgs buttonClickEventArgs) =>
            {
                this._cancellationToken.Cancel();

                if (buttonSender is Button button)
                {
                    button.Enabled = false;
                    button.Text = Resources.Strings.CancelInitiatedLabelText;
                }
            };
            this._panel.Controls.Add(this._cancelButton);
            this._cancelButton.BringToFront();

            //Center on form resize. Kept in a field so Dispose can detach it: the handler captures this
            //instance, so leaving it attached roots every LoadingIcon for the lifetime of the form.
            this._onFormSizeChanged = (object? sender, EventArgs e) =>
            {
                this._panel.Location = this.GetFormCenter();
            };
            this._form.SizeChanged += this._onFormSizeChanged;
        }

        public void Reset(string? newMessage = null)
        {
            if (newMessage is not null)
            {
                foreach (Control control in this._panel.Controls.Find("loadingmessagelabel", false))
                {
                    control.Text = newMessage;
                }
            }

            Interlocked.Exchange(ref this._progressSoFar, 0);

            lock (_lock)
            {
                this._progressRatio = 0;

                var previousImage = this._cancelButton.BackgroundImage;
                this._cancelButton.BackgroundImage = null;
                previousImage?.Dispose();
            }

            this._cancelButton.Invoke(this._cancelButton.Refresh);
        }

        public void Show()
        {
            this._form.Controls.Add(this._panel);
            this._panel.BringToFront();
            this._panel.Show();
            this._cancelButton.Focus();

            this._cancelButton.BackgroundImage = new Bitmap(_cancelButton.ClientSize.Width, _cancelButton.ClientSize.Height);
            this.OnShow?.Invoke(this, EventArgs.Empty);
        }

        private Point GetFormCenter()
            => new((this._form.Width / 2) - (LoadingPanelWidth / 2), (this._form.Height / 2) - (LoadingPanelHeight / 2));

        public void Dispose()
        {
            this.OnHide?.Invoke(this, EventArgs.Empty);

            this._form.SizeChanged -= this._onFormSizeChanged;

            //The button owns the last progress bitmap we handed it; disposing the panel won't release it.
            var backgroundImage = this._cancelButton.BackgroundImage;
            this._cancelButton.BackgroundImage = null;
            backgroundImage?.Dispose();

            this._panel.Dispose();
            this._cancellationToken.Dispose();
        }

        private readonly object _lock = new();
        public void Report(int progress)
        {
            if (this._loadingBarMax <= 0)
                return;

            var progressSoFar = Interlocked.Add(ref this._progressSoFar, progress);
            var progressRatio = (int)Math.Ceiling((progressSoFar * 100) / (double)this._loadingBarMax);

            //Progress is reported from background threads, so the compare-and-update of _progressRatio has
            //to happen under the lock too. Reading it outside let two threads both observe a stale value
            //and redraw concurrently, and a later tick could overwrite the bar with an earlier percentage.
            lock (_lock)
            {
                //Only redraw when the whole percentage point changes. Report is called once per cell, so
                //marshalling to the UI thread on every call would dominate the load time.
                if (progressRatio == this._progressRatio)
                    return;

                this._progressRatio = progressRatio;

                //Convert the cancel button into a progress bar
                var bitmap = new Bitmap(_cancelButton.ClientSize.Width, _cancelButton.ClientSize.Height);
                using (var solidBrush = new SolidBrush(Color.FromArgb(160, 40, 160, 60)))
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        float wid = bitmap.Width * progressRatio / 100;
                        float hgt = bitmap.Height;
                        RectangleF rect = new RectangleF(0, 0, wid, hgt);
                        graphics.FillRectangle(solidBrush, rect);
                    }
                }

                //Release the bitmap from the previous tick; this runs once per percentage point.
                var previousImage = this._cancelButton.BackgroundImage;
                this._cancelButton.BackgroundImage = bitmap;
                previousImage?.Dispose();
            }

            //Deliberately outside the lock. Invoke blocks until the UI thread runs the delegate, and the
            //UI thread itself calls Reset, which takes this same lock. Holding it here would deadlock.
            this._cancelButton.Invoke(this._cancelButton.Refresh);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using VisionLibrary;

namespace HoleSampleViewer
{
    public sealed class MainForm : Form
    {
        private static readonly string[] ImageExtensions = { ".bmp", ".png", ".jpg", ".jpeg", ".tif", ".tiff" };

        private readonly ImageCanvas _canvas = new ImageCanvas { Dock = DockStyle.Fill };
        private readonly ToolStripButton _openButton = new ToolStripButton("開啟影像...");
        private readonly ToolStripButton _prevButton = new ToolStripButton("◀ 上一張") { Enabled = false };
        private readonly ToolStripButton _nextButton = new ToolStripButton("下一張 ▶") { Enabled = false };
        private readonly ToolStripLabel _fileLabel = new ToolStripLabel("尚未開啟影像");
        private readonly ToolStripStatusLabel _hoverLabel = new ToolStripStatusLabel(" ");

        private readonly CheckBox _useScale = new CheckBox { Text = "開啟 (0.5 倍)", Checked = true, AutoSize = true };
        private readonly NumericUpDown _margin = Number(1, 500, 10, 1, 0.5m);
        private readonly NumericUpDown _spacing = Number(1, 500, 5);
        private readonly ComboBox _polarity = Combo("孔較亮", "孔較暗");
        private readonly CheckBox _otsu = new CheckBox { Text = "自動 (Otsu)", Checked = true, AutoSize = true };
        private readonly NumericUpDown _threshold = Number(0, 255, 127);
        private readonly NumericUpDown _blur = Number(0, 31, 3);
        private readonly NumericUpDown _morph = Number(0, 31, 3);
        private readonly NumericUpDown _minArea = Number(0, 1000000, 30);
        private readonly NumericUpDown _maxArea = Number(1, 100000000, 10000);
        private readonly NumericUpDown _separation = Number(0.1m, 3, 0.8m, 2, 0.05m);
        private readonly ComboBox _view = Combo("原圖", "二值圖");
        private readonly CheckBox _showOverlay = new CheckBox { Text = "安全區/孔邊", Checked = true, AutoSize = true };
        private readonly CheckBox _showCenters = new CheckBox { Text = "估計孔心", Checked = true, AutoSize = true };

        private readonly TextBox _summary = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, WordWrap = true,
        };
        private readonly DataGridView _grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false, AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
        };

        private Mat _gray;
        private Image<Gray, byte> _grayLookup;
        private HoleSampleResult _result;
        private string _path;
        private string[] _siblings = new string[0];

        public MainForm()
        {
            Text = "木漿板孔座標檢視";
            ClientSize = new Size(1180, 760);
            MinimumSize = new Size(820, 560);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;

            ToolStrip toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            toolStrip.Items.AddRange(new ToolStripItem[] { _openButton, _prevButton, _nextButton, new ToolStripSeparator(), _fileLabel });

            StatusStrip statusStrip = new StatusStrip();
            statusStrip.Items.Add(_hoverLabel);

            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2 };
            split.Panel1.Controls.Add(_canvas);
            split.Panel2.Controls.Add(BuildSidePanel());

            Controls.Add(split);
            Controls.Add(toolStrip);
            Controls.Add(statusStrip);
            Load += (s, e) =>
            {
                split.Panel2MinSize = 340;
                split.SplitterDistance = Math.Max(100, split.ClientSize.Width - 360);
            };

            BuildGridColumns();
            WireEvents();
        }

        private static NumericUpDown Number(decimal min, decimal max, decimal value, int decimals = 0, decimal step = 1)
        {
            return new NumericUpDown
            {
                Minimum = min, Maximum = max, Value = value, DecimalPlaces = decimals, Increment = step,
                Dock = DockStyle.Fill,
            };
        }

        private static ComboBox Combo(params object[] items)
        {
            ComboBox box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            box.Items.AddRange(items);
            box.SelectedIndex = 0;
            return box;
        }

        private Control BuildSidePanel()
        {
            TableLayoutPanel parameters = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2,
            };
            parameters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            parameters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            AddRow(parameters, "縮圖計算", _useScale);
            AddRow(parameters, "距孔邊緣 (px)", _margin);
            AddRow(parameters, "採樣間距 (px)", _spacing);
            AddRow(parameters, "孔的亮度", _polarity);
            AddRow(parameters, "二值化閾值", _otsu);
            AddRow(parameters, "手動閾值", _threshold);
            AddRow(parameters, "模糊 kernel (0=關)", _blur);
            AddRow(parameters, "開閉運算 kernel (0=關)", _morph);
            AddRow(parameters, "最小孔面積 (px)", _minArea);
            AddRow(parameters, "最大孔面積 (px)", _maxArea);
            AddRow(parameters, "孔心分離比", _separation);
            AddRow(parameters, "底圖", _view);
            AddRow(parameters, "疊圖", _showOverlay);
            AddRow(parameters, "", _showCenters);
            GroupBox parameterGroup = new GroupBox { Text = "參數", Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            parameterGroup.Controls.Add(parameters);

            Label legend = new Label
            {
                AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(3, 4, 3, 4),
                Text = "紅點=座標　黃點=未達邊距　橘十字=估計孔心\r\n綠=安全區　青=孔邊",
            };

            TableLayoutPanel side = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            side.Controls.Add(parameterGroup, 0, 0);
            side.Controls.Add(legend, 0, 1);
            side.Controls.Add(_summary, 0, 2);
            side.Controls.Add(_grid, 0, 3);
            return side;
        }

        private static void AddRow(TableLayoutPanel panel, string label, Control control)
        {
            int row = panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            panel.Controls.Add(control, 1, row);
        }

        private void BuildGridColumns()
        {
            AddColumn("區塊", "BlobId", 70);
            AddColumn("子孔", "HoleIndex", 70);
            AddColumn("X", "X", 50);
            AddColumn("Y", "Y", 50);
            AddColumn("距邊緣", "EdgeDistance", 70, "F2");
            AddColumn("狀態", "Status", 80);
        }

        private void AddColumn(string header, string property, int weight, string format = null)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
            {
                HeaderText = header, DataPropertyName = property, FillWeight = weight,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            if (format != null)
                column.DefaultCellStyle.Format = format;
            _grid.Columns.Add(column);
        }

        private void WireEvents()
        {
            _openButton.Click += (s, e) => ShowOpenDialog();
            _prevButton.Click += (s, e) => Step(-1);
            _nextButton.Click += (s, e) => Step(1);

            foreach (NumericUpDown number in new[] { _margin, _spacing, _threshold, _blur, _morph, _minArea, _maxArea, _separation })
                number.ValueChanged += (s, e) => Recompute(false);
            _useScale.CheckedChanged += (s, e) => Recompute(false);
            _polarity.SelectedIndexChanged += (s, e) => Recompute(false);
            _otsu.CheckedChanged += (s, e) =>
            {
                _threshold.Enabled = !_otsu.Checked;
                Recompute(false);
            };
            _threshold.Enabled = false;

            _view.SelectedIndexChanged += (s, e) => RefreshImage(false);
            _showOverlay.CheckedChanged += (s, e) => RefreshImage(false);
            _showCenters.CheckedChanged += (s, e) =>
            {
                _canvas.ShowCenters = _showCenters.Checked;
                _canvas.Invalidate();
            };

            _grid.SelectionChanged += (s, e) =>
            {
                _canvas.HighlightIndex = _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].Index : -1;
                _canvas.Invalidate();
            };

            _canvas.HoverPixelChanged += ShowHoverInfo;

            DragEnter += (s, e) =>
            {
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            };
            DragDrop += (s, e) =>
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                    OpenFile(files[0]);
            };
        }

        private void ShowOpenDialog()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "影像檔|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|所有檔案|*.*";
                dialog.InitialDirectory = _path != null ? Path.GetDirectoryName(_path) : FindImageFolder();
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    OpenFile(dialog.FileName);
            }
        }

        /// <summary>程式放在圖檔資料夾底下，往上找第一個含影像的資料夾當預設位置。</summary>
        private static string FindImageFolder()
        {
            for (DirectoryInfo folder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); folder != null; folder = folder.Parent)
            {
                if (folder.EnumerateFiles().Any(f => ImageExtensions.Contains(f.Extension.ToLowerInvariant())))
                    return folder.FullName;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private void Step(int direction)
        {
            int index = Array.FindIndex(_siblings, f => string.Equals(f, _path, StringComparison.OrdinalIgnoreCase));
            int next = index + direction;
            if (index >= 0 && next >= 0 && next < _siblings.Length)
                OpenFile(_siblings[next]);
        }

        public void OpenFile(string path)
        {
            Mat gray;
            try
            {
                gray = HoleSampler.LoadGray(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "無法開啟影像", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _gray?.Dispose();
            _grayLookup?.Dispose();
            _gray = gray;
            _grayLookup = gray.ToImage<Gray, byte>();
            _path = path;

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            _siblings = Directory.GetFiles(directory)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            int index = Array.FindIndex(_siblings, f => string.Equals(f, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
            _path = Path.GetFullPath(path);
            _prevButton.Enabled = index > 0;
            _nextButton.Enabled = index >= 0 && index < _siblings.Length - 1;
            _fileLabel.Text = string.Format("{0}  ({1}×{2})  [{3}/{4}]", Path.GetFileName(path), gray.Cols, gray.Rows, index + 1, _siblings.Length);

            Recompute(true);
        }

        private HoleSampleOptions ReadOptions()
        {
            return new HoleSampleOptions
            {
                Margin = (double)_margin.Value,
                Spacing = (int)_spacing.Value,
                HoleIsBright = _polarity.SelectedIndex == 0,
                UseOtsu = _otsu.Checked,
                Threshold = (int)_threshold.Value,
                BlurSize = (int)_blur.Value,
                MorphSize = (int)_morph.Value,
                MinArea = (int)_minArea.Value,
                MaxArea = (int)_maxArea.Value,
                SeparationRatio = (double)_separation.Value,
                IsScale = _useScale.Checked,
            };
        }

        private void Recompute(bool resetView)
        {
            if (_gray == null)
                return;

            try
            {
                _result = HoleSampler.Detect(_gray, ReadOptions());
            }
            catch (Exception ex)
            {
                _result = null;
                _summary.Text = "計算失敗：" + ex.Message;
                return;
            }

            _canvas.Points = _result.Points;
            _canvas.Blobs = _result.Blobs;
            _canvas.HighlightIndex = -1;
            _grid.DataSource = _result.Points.Select(p => new PointRow(p)).ToList();
            _grid.ClearSelection();
            _summary.Text = BuildSummary(_result);
            RefreshImage(resetView);
        }

        private void RefreshImage(bool resetView)
        {
            if (_result == null || _gray == null)
                return;
            _canvas.SetImage(OverlayRenderer.Render(_gray, _result, _view.SelectedIndex == 1, _showOverlay.Checked), resetView);
        }

        private string BuildSummary(HoleSampleResult result)
        {
            StringBuilder text = new StringBuilder();
            int holes = result.Blobs.Sum(b => b.EstimatedHoleCount);
            int weak = result.Points.Count(p => !p.MeetsMargin);
            text.AppendFormat("估計 {0} 孔（{1} 個連通區塊），座標 {2} 點", holes, result.Blobs.Count, result.Points.Count);
            if (weak > 0)
                text.AppendFormat("，其中 {0} 點未達邊距", weak);
            text.AppendLine();
            text.AppendFormat("閾值 {0:F0}，孔佔 {1:P1}，耗時 {2} ms", result.UsedThreshold, result.ForegroundRatio, result.ElapsedMs);
            text.AppendLine();
            if (result.ScaleUsed)
                text.AppendFormat("縮圖計算：{0} 個 ROI", result.RegionCount).AppendLine();
            else if (result.ScaleFellBack)
                text.AppendLine("縮圖計算：孔超出 ROI 或候選區過多，本次改算全幅");
            else
                text.AppendLine("全幅計算");

            foreach (HoleBlob blob in result.Blobs)
            {
                text.AppendFormat("區塊{0}：面積 {1}，最大距離 {2:F2}，估計 {3} 孔，{4} 點",
                    blob.Id, blob.Area, blob.MaxDistance, blob.EstimatedHoleCount, result.Points.Count(p => p.BlobId == blob.Id));
                if (blob.TouchesBorder)
                    text.Append("（碰到影像邊界）");
                text.AppendLine();
            }

            if (result.TooMuchForeground)
                text.AppendLine("⚠ 孔佔畫面超過一半，孔的亮度設定可能相反，因此不取座標");
            else if (result.Blobs.Count == 0)
                text.AppendLine("⚠ 找不到孔，請檢查孔的亮度設定與閾值");
            if (result.OversizedBlobCount > 0)
                text.AppendFormat("⚠ {0} 個區塊超過最大孔面積，已排除", result.OversizedBlobCount).AppendLine();
            return text.ToString();
        }

        private void ShowHoverInfo(Point? pixel)
        {
            if (pixel == null || _grayLookup == null || _result == null)
            {
                _hoverLabel.Text = " ";
                return;
            }
            Point p = pixel.Value;
            _hoverLabel.Text = string.Format("X={0}  Y={1}  灰階={2}  距孔邊緣={3:F2}", p.X, p.Y, _grayLookup.Data[p.Y, p.X, 0], _result.Distance[p.Y, p.X]);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _gray?.Dispose();
                _grayLookup?.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class PointRow
        {
            private readonly HoleSamplePoint _point;
            public PointRow(HoleSamplePoint point) { _point = point; }
            public int BlobId { get { return _point.BlobId; } }
            public int HoleIndex { get { return _point.HoleIndex; } }
            public int X { get { return _point.Location.X; } }
            public int Y { get { return _point.Location.Y; } }
            public double EdgeDistance { get { return _point.EdgeDistance; } }
            public string Status { get { return _point.MeetsMargin ? "OK" : "未達邊距"; } }
        }
    }
}

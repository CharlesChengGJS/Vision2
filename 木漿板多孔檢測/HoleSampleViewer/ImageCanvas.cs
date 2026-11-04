using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using VisionLibrary;

namespace HoleSampleViewer
{
    /// <summary>顯示影像與座標：滾輪縮放、左鍵拖曳平移、雙擊回到全圖。</summary>
    public sealed class ImageCanvas : Control
    {
        private Bitmap _image;
        private float _zoom = 1;
        private PointF _origin;
        private bool _autoFit = true;
        private Point _dragStart;
        private PointF _originAtDragStart;
        private bool _dragging;

        public IList<HoleSamplePoint> Points;
        public IList<HoleBlob> Blobs;
        public bool ShowCenters = true;
        public int HighlightIndex = -1;

        /// <summary>滑鼠所在的影像座標，離開影像時為 null。</summary>
        public event Action<Point?> HoverPixelChanged;

        public ImageCanvas()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(45, 45, 48);
        }

        /// <summary>傳入的影像由此控制項接管並負責釋放。</summary>
        public void SetImage(Bitmap image, bool resetView)
        {
            Bitmap old = _image;
            _image = image;
            old?.Dispose();
            if (resetView)
                _autoFit = true;
            if (_autoFit)
                FitToWindow();
            Invalidate();
        }

        private void FitToWindow()
        {
            if (_image == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return;
            _zoom = Math.Min((float)ClientSize.Width / _image.Width, (float)ClientSize.Height / _image.Height);
            _origin = new PointF((ClientSize.Width - _image.Width * _zoom) / 2, (ClientSize.Height - _image.Height * _zoom) / 2);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_autoFit)
                FitToWindow();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();    // 取得焦點才收得到滾輪事件
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_image == null)
                return;
            float factor = e.Delta > 0 ? 1.25f : 0.8f;
            float next = Math.Max(0.05f, Math.Min(64f, _zoom * factor));
            float imageX = (e.X - _origin.X) / _zoom, imageY = (e.Y - _origin.Y) / _zoom;
            _zoom = next;
            _origin = new PointF(e.X - imageX * _zoom, e.Y - imageY * _zoom);
            _autoFit = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragStart = e.Location;
                _originAtDragStart = _origin;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                _origin = new PointF(_originAtDragStart.X + e.X - _dragStart.X, _originAtDragStart.Y + e.Y - _dragStart.Y);
                _autoFit = false;
                Invalidate();
            }

            if (_image == null || HoverPixelChanged == null)
                return;
            int x = (int)Math.Floor((e.X - _origin.X) / _zoom), y = (int)Math.Floor((e.Y - _origin.Y) / _zoom);
            bool inside = x >= 0 && y >= 0 && x < _image.Width && y < _image.Height;
            HoverPixelChanged(inside ? new Point(x, y) : (Point?)null);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            HoverPixelChanged?.Invoke(null);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            _autoFit = true;
            FitToWindow();
            Invalidate();
        }

        private PointF ToScreen(Point pixel)
        {
            return new PointF(_origin.X + (pixel.X + 0.5f) * _zoom, _origin.Y + (pixel.Y + 0.5f) * _zoom);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            if (_image == null)
            {
                TextRenderer.DrawText(g, "請開啟影像，或把檔案拖到這裡", Font, ClientRectangle, Color.Silver,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(_image, _origin.X, _origin.Y, _image.Width * _zoom, _image.Height * _zoom);

            g.PixelOffsetMode = PixelOffsetMode.Default;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float radius = Math.Max(2.5f, Math.Min(_zoom * 0.45f, 7f));

            if (ShowCenters && Blobs != null)
            {
                using (Pen pen = new Pen(Color.Orange, 1.5f))
                {
                    float arm = radius + 5;
                    foreach (HoleBlob blob in Blobs)
                    {
                        foreach (Point center in blob.Centers)
                        {
                            PointF s = ToScreen(center);
                            g.DrawLine(pen, s.X - arm, s.Y, s.X + arm, s.Y);
                            g.DrawLine(pen, s.X, s.Y - arm, s.X, s.Y + arm);
                        }
                    }
                }
            }

            if (Points != null)
            {
                for (int i = 0; i < Points.Count; i++)
                {
                    HoleSamplePoint point = Points[i];
                    PointF s = ToScreen(point.Location);
                    Color color = point.MeetsMargin ? Color.Red : Color.Gold;
                    using (SolidBrush brush = new SolidBrush(color))
                        g.FillEllipse(brush, s.X - radius, s.Y - radius, radius * 2, radius * 2);
                    if (i == HighlightIndex)
                    {
                        using (Pen pen = new Pen(Color.Magenta, 2f))
                            g.DrawEllipse(pen, s.X - radius - 4, s.Y - radius - 4, (radius + 4) * 2, (radius + 4) * 2);
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _image?.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>把灰階圖或二值圖與安全區、孔邊合成為顯示用的影像。</summary>
    public static class OverlayRenderer
    {
        public static Bitmap Render(Mat gray, HoleSampleResult result, bool showBinary, bool showOverlay)
        {
            int width = result.Width, height = result.Height;
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                using (Image<Gray, byte> source = gray.ToImage<Gray, byte>())
                {
                    byte[] row = new byte[data.Stride];
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            byte value = showBinary ? result.Binary[y, x] : source.Data[y, x, 0];
                            int b = value, g = value, r = value;
                            float d = result.Distance[y, x];
                            if (showOverlay && d > 0)
                            {
                                if (d < 1.5f)           // 孔邊：青色
                                {
                                    b = 255; g = 200; r = 0;
                                }
                                else if (d >= result.Margin)   // 安全區：綠色
                                {
                                    b = value / 3 + 40; g = value / 3 + 170; r = value / 3 + 40;
                                }
                            }
                            row[x * 3] = (byte)b;
                            row[x * 3 + 1] = (byte)g;
                            row[x * 3 + 2] = (byte)r;
                        }
                        Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }
    }
}

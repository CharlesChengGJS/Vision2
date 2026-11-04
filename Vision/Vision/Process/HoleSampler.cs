using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace VisionLibrary
{
    public sealed class HoleSampleOptions
    {
        /// <summary>座標到孔邊緣的最小距離 (pixel，一律以原圖為準)。</summary>
        public double Margin = 10;
        /// <summary>採樣間距 (pixel，一律以原圖為準)。</summary>
        public int Spacing = 5;
        public bool HoleIsBright = true;
        public bool UseOtsu = true;
        public int Threshold = 127;
        /// <summary>高斯模糊 kernel，小於 3 表示不做。</summary>
        public int BlurSize = 3;
        /// <summary>開、閉運算 kernel，小於 3 表示不做。</summary>
        public int MorphSize = 3;
        /// <summary>小於此面積的區塊視為雜訊。</summary>
        public int MinArea = 30;
        /// <summary>大於此面積的區塊不視為孔，一律以原圖的面積判斷。</summary>
        public int MaxArea = 10000;
        /// <summary>距離達區塊最大距離此比例的像素才可當孔心。</summary>
        public double PeakLevel = 0.85;
        /// <summary>孔心之間至少相隔「區塊最大距離 × 此比例」，調小較容易把連在一起的孔分開。</summary>
        public double SeparationRatio = 0.8;
        /// <summary>true：先用 0.5 倍縮圖找孔的位置，再回原圖的 ROI 精算；false：全幅以原圖計算。</summary>
        public bool IsScale = true;
        /// <summary>孔佔影像超過此比例時視為亮暗設定相反，不取座標。ROI 貼近孔時要調高。</summary>
        public double MaxForegroundRatio = 0.5;

        internal HoleSampleOptions Clone()
        {
            return (HoleSampleOptions)MemberwiseClone();
        }
    }

    public sealed class HoleSamplePoint
    {
        public Point Location;
        /// <summary>到最近孔外像素中心的距離。</summary>
        public double EdgeDistance;
        public int BlobId;
        public int HoleIndex;
        /// <summary>false 表示此孔沒有符合邊距的位置，這是退而求其次的最深點。</summary>
        public bool MeetsMargin;
    }

    /// <summary>一個連通區塊，連在一起的多個孔屬於同一區塊。</summary>
    public sealed class HoleBlob
    {
        public int Id;
        public int Area;
        public Rectangle Bounds;
        public double MaxDistance;
        public bool TouchesBorder;
        /// <summary>估計孔心，數量即估計孔數。</summary>
        public List<Point> Centers = new List<Point>();
        public int EstimatedHoleCount { get { return Centers.Count; } }
    }

    public sealed class HoleSampleResult
    {
        public int Width;
        public int Height;
        /// <summary>孔的遮罩；縮圖模式下 ROI 以外一律為 0。</summary>
        public byte[,] Binary;
        /// <summary>到孔邊緣的距離；縮圖模式下 ROI 以外一律為 0。</summary>
        public float[,] Distance;
        public double Margin;
        public double UsedThreshold;
        public double ForegroundRatio;
        public long ElapsedMs;
        /// <summary>實際處理的原圖區域數，全幅計算時為 1。</summary>
        public int RegionCount;
        /// <summary>結果確實來自「縮圖定位 + ROI 精算」。</summary>
        public bool ScaleUsed;
        /// <summary>有開縮圖，但因孔超出 ROI 或候選區過多而改用全幅計算。</summary>
        public bool ScaleFellBack;
        /// <summary>孔佔影像超過 MaxForegroundRatio，多半是亮暗設定相反，因此不取座標。</summary>
        public bool TooMuchForeground;
        /// <summary>因超過 MaxArea 而被排除的區塊數。</summary>
        public int OversizedBlobCount;
        public List<HoleBlob> Blobs = new List<HoleBlob>();
        public List<HoleSamplePoint> Points = new List<HoleSamplePoint>();
    }

    public static class HoleSampler
    {
        private const int ScaleDivisor = 2;             // 縮圖比例 0.5
        private const int MinSizeForScale = 16;
        private const int MaxRegionsBeforeMerge = 1000;
        private const double MaxRegionAreaRatio = 0.5;

        /// <summary>經由位元組解碼，避免 Imread 無法開啟中文路徑。</summary>
        public static Mat LoadGray(string path)
        {
            Mat gray = new Mat();
            CvInvoke.Imdecode(File.ReadAllBytes(path), ImreadModes.Grayscale, gray);
            if (gray.IsEmpty)
            {
                gray.Dispose();
                throw new InvalidDataException("無法解碼影像：" + path);
            }
            return gray;
        }

        public static HoleSampleResult Detect(Mat gray, HoleSampleOptions options)
        {
            if (options.Spacing < 1)
                throw new ArgumentOutOfRangeException("options", "Spacing 必須 >= 1");

            Stopwatch watch = Stopwatch.StartNew();
            Rectangle whole = new Rectangle(0, 0, gray.Cols, gray.Rows);
            HoleSampleResult result = null;
            bool fellBack = false;

            if (options.IsScale && Math.Min(gray.Cols, gray.Rows) >= MinSizeForScale)
            {
                double threshold;
                List<Rectangle> regions = FindRegionsOnThumbnail(gray, options, out threshold);
                HoleSampleOptions roiOptions = options.Clone();
                roiOptions.UseOtsu = false;     // 同一張圖只決定一次閾值，各 ROI 的結果才一致
                roiOptions.Threshold = (int)threshold;

                if (regions != null)
                {
                    result = ProcessRegions(gray, regions, roiOptions);
                    if (result != null)
                        result.ScaleUsed = true;
                }
                fellBack = result == null;
            }

            if (result == null)
            {
                result = ProcessRegions(gray, new List<Rectangle> { whole }, options);
                result.ScaleFellBack = fellBack;
            }

            watch.Stop();
            result.ElapsedMs = watch.ElapsedMilliseconds;
            return result;
        }

        /// <summary>
        /// 在 0.5 倍縮圖上找出孔的大略位置，換算成原圖 ROI。
        /// 回傳 null 表示候選區太多或太大，縮圖沒有好處，應改用全幅計算。
        /// </summary>
        private static List<Rectangle> FindRegionsOnThumbnail(Mat gray, HoleSampleOptions options, out double threshold)
        {
            int width = gray.Cols, height = gray.Rows;
            List<Rectangle> regions = new List<Rectangle>();
            int padding = 4 + options.BlurSize / 2 + options.MorphSize;
            Rectangle whole = new Rectangle(0, 0, width, height);

            HoleSampleOptions thumbnailOptions = options.Clone();
            thumbnailOptions.MorphSize = 0;     // 這裡只求定位，開運算反而可能把小孔去掉

            // 指定 dsize，避免奇數尺寸四捨五入後與預期不符。
            using (Mat thumbnail = new Mat())
            using (Mat labels = new Mat())
            using (Mat stats = new Mat())
            using (Mat centroids = new Mat())
            {
                CvInvoke.Resize(gray, thumbnail, new Size(width / ScaleDivisor, height / ScaleDivisor), 0, 0, Inter.Area);
                using (Mat binary = Binarize(thumbnail, thumbnailOptions, out threshold))
                {
                    int count = CvInvoke.ConnectedComponentsWithStats(binary, labels, stats, centroids,
                        LineType.EightConnected, DepthType.Cv32S);
                    int[] values = ReadInts(stats);
                    int columns = stats.Cols;
                    for (int i = 1; i < count; i++)
                    {
                        // ConnectedComponentsWithStats 欄位：left, top, width, height, area
                        int area = values[i * columns + 4] * ScaleDivisor * ScaleDivisor;
                        if (area * 2 < options.MinArea)     // 放寬一半，精確面積留給原圖判斷
                            continue;

                        Rectangle box = new Rectangle(
                            values[i * columns] * ScaleDivisor - padding,
                            values[i * columns + 1] * ScaleDivisor - padding,
                            values[i * columns + 2] * ScaleDivisor + padding * 2,
                            values[i * columns + 3] * ScaleDivisor + padding * 2);
                        box.Intersect(whole);
                        regions.Add(box);
                        if (regions.Count > MaxRegionsBeforeMerge)
                            return null;
                    }
                }
            }

            MergeOverlapping(regions);

            long regionArea = regions.Sum(r => (long)r.Width * r.Height);
            if (regionArea > (long)width * height * MaxRegionAreaRatio)
                return null;
            return regions;
        }

        /// <summary>重疊的 ROI 要合併，否則同一個孔會在兩個 ROI 各被算一次。</summary>
        private static void MergeOverlapping(List<Rectangle> regions)
        {
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < regions.Count; i++)
                {
                    for (int j = regions.Count - 1; j > i; j--)
                    {
                        if (regions[i].IntersectsWith(regions[j]))
                        {
                            regions[i] = Rectangle.Union(regions[i], regions[j]);
                            regions.RemoveAt(j);
                            merged = true;
                        }
                    }
                }
            }
        }

        /// <summary>在原圖的指定區域計算。有孔超出 ROI（結果不可信）時回傳 null。</summary>
        private static HoleSampleResult ProcessRegions(Mat gray, List<Rectangle> regions, HoleSampleOptions options)
        {
            int width = gray.Cols, height = gray.Rows;
            HoleSampleResult result = new HoleSampleResult
            {
                Width = width,
                Height = height,
                Margin = Math.Max(options.Margin, 1),
                Binary = new byte[height, width],
                Distance = new float[height, width],
                UsedThreshold = options.Threshold,
                RegionCount = regions.Count,
            };

            long foreground = 0;
            List<List<Point>> blobPixels = new List<List<Point>>();
            foreach (Rectangle region in regions)
            {
                using (Mat crop = new Mat(gray, region))
                using (Mat binary = Binarize(crop, options, out result.UsedThreshold))
                {
                    if (!ExtractBlobs(binary, region, options, result, blobPixels, ref foreground))
                        return null;
                }
            }

            result.ForegroundRatio = (double)foreground / ((long)width * height);
            if (result.ForegroundRatio > options.MaxForegroundRatio)
            {
                result.TooMuchForeground = true;    // 大圖上會排序數百萬個像素而卡住畫面
                return result;
            }

            // 依掃描順序編號，不論是否縮圖、ROI 怎麼切，區塊編號都相同。
            blobPixels = blobPixels.OrderBy(b => b[0].Y).ThenBy(b => b[0].X).ToList();

            int[,] labels = new int[height, width];
            for (int i = 0; i < blobPixels.Count; i++)
            {
                foreach (Point p in blobPixels[i])
                    labels[p.Y, p.X] = i + 1;
                WriteDistance(blobPixels[i], result.Distance);
                result.Blobs.Add(BuildBlob(i + 1, blobPixels[i], result, options));
            }

            SamplePoints(result, blobPixels, labels, options);
            return result;
        }

        private static Mat Binarize(Mat gray, HoleSampleOptions options, out double usedThreshold)
        {
            Mat binary = new Mat();
            using (Mat blurred = new Mat())
            {
                Mat source = gray;
                if (options.BlurSize >= 3)
                {
                    int k = options.BlurSize | 1;
                    CvInvoke.GaussianBlur(gray, blurred, new Size(k, k), 0);
                    source = blurred;
                }

                ThresholdType type = options.HoleIsBright ? ThresholdType.Binary : ThresholdType.BinaryInv;
                if (options.UseOtsu)
                    type |= ThresholdType.Otsu;
                usedThreshold = CvInvoke.Threshold(source, binary, options.Threshold, 255, type);
            }

            if (options.MorphSize >= 3)
            {
                int k = options.MorphSize | 1;
                using (Mat element = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(k, k), new Point(-1, -1)))
                {
                    CvInvoke.MorphologyEx(binary, binary, MorphOp.Open, element, new Point(-1, -1), 1, BorderType.Replicate, new MCvScalar(0));
                    CvInvoke.MorphologyEx(binary, binary, MorphOp.Close, element, new Point(-1, -1), 1, BorderType.Replicate, new MCvScalar(0));
                }
            }
            return binary;
        }

        /// <summary>
        /// 取出區域內面積足夠的區塊，結果寫回全圖座標。
        /// 孔碰到 ROI 的內側邊界代表 ROI 切到孔，回傳 false。
        /// ROI 內已超過 MaxArea 的區塊，實際只會更大，所以即使被切到也可以直接排除。
        /// foreground 也計入超過 MaxArea 的區塊，否則亮暗設反時整片背景被排除，就判斷不出來。
        /// </summary>
        private static bool ExtractBlobs(Mat binary, Rectangle region, HoleSampleOptions options, HoleSampleResult result, List<List<Point>> blobPixels, ref long foreground)
        {
            int width = binary.Cols, height = binary.Rows;
            int count;
            int[] labels = LabelFlat(binary, out count);

            int[] areas = new int[count];
            foreach (int id in labels)
                areas[id]++;

            for (int id = 1; id < count; id++)
            {
                if (areas[id] < options.MinArea)
                    continue;
                foreground += areas[id];
                if (areas[id] > options.MaxArea)
                    result.OversizedBlobCount++;
            }

            bool cutLeft = region.X > 0, cutTop = region.Y > 0;
            bool cutRight = region.Right < result.Width, cutBottom = region.Bottom < result.Height;

            List<Point>[] pixels = new List<Point>[count];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int id = labels[y * width + x];
                    if (id == 0 || areas[id] < options.MinArea || areas[id] > options.MaxArea)
                        continue;

                    if ((x == 0 && cutLeft) || (y == 0 && cutTop) || (x == width - 1 && cutRight) || (y == height - 1 && cutBottom))
                        return false;

                    if (pixels[id] == null)
                        pixels[id] = new List<Point>(areas[id]);
                    pixels[id].Add(new Point(x + region.X, y + region.Y));
                    result.Binary[y + region.Y, x + region.X] = 255;
                }
            }

            blobPixels.AddRange(pixels.Where(p => p != null));
            return true;
        }

        /// <summary>
        /// 精確 L2 距離轉換，只算這個孔的外接框。
        /// 全幅寬度超過 4096 時 OpenCV 的精確模式會失準（連背景都出現非 0 距離），裁切後就不會。
        /// 補 1 px 黑邊：否則影像邊界不算孔邊，被切到的孔會被誤判為符合邊距。
        /// 外接框之外都不屬於此孔，其他孔也一定隔著背景，所以距離與全幅計算相同。
        /// </summary>
        private static void WriteDistance(List<Point> pixels, float[,] distance)
        {
            int minX = pixels.Min(p => p.X), minY = pixels.Min(p => p.Y);
            int width = pixels.Max(p => p.X) - minX + 1, height = pixels.Max(p => p.Y) - minY + 1;

            byte[] mask = new byte[width * height];
            foreach (Point p in pixels)
                mask[(p.Y - minY) * width + (p.X - minX)] = 255;

            using (Mat source = ToMat(mask, width, height))
            using (Mat padded = new Mat())
            using (Mat dist = new Mat())
            {
                CvInvoke.CopyMakeBorder(source, padded, 1, 1, 1, 1, BorderType.Constant, new MCvScalar(0));
                CvInvoke.DistanceTransform(padded, dist, null, DistType.L2, 0);

                int stride = dist.Cols;
                float[] values = new float[dist.Rows * stride];
                for (int y = 0; y < dist.Rows; y++)
                    Marshal.Copy(IntPtr.Add(dist.DataPointer, y * dist.Step), values, y * stride, stride);

                foreach (Point p in pixels)
                    distance[p.Y, p.X] = values[(p.Y - minY + 1) * stride + (p.X - minX + 1)];
            }
        }

        private static int[] LabelFlat(Mat binary, out int count)
        {
            using (Mat labels = new Mat())
            {
                count = CvInvoke.ConnectedComponents(binary, labels, LineType.EightConnected, DepthType.Cv32S);
                return ReadInts(labels);
            }
        }

        // 用整列記憶體複製讀取。逐像素存取 Image<Gray,int>.Data 在大圖上要超過 1 秒。
        private static int[] ReadInts(Mat mat)
        {
            int[] data = new int[mat.Rows * mat.Cols];
            for (int y = 0; y < mat.Rows; y++)
                Marshal.Copy(IntPtr.Add(mat.DataPointer, y * mat.Step), data, y * mat.Cols, mat.Cols);
            return data;
        }

        private static Mat ToMat(byte[] data, int width, int height)
        {
            Mat mat = new Mat(new Size(width, height), DepthType.Cv8U, 1);
            for (int y = 0; y < height; y++)
                Marshal.Copy(data, y * width, IntPtr.Add(mat.DataPointer, y * mat.Step), width);
            return mat;
        }

        private static HoleBlob BuildBlob(int id, List<Point> pixels, HoleSampleResult result, HoleSampleOptions options)
        {
            int minX = pixels.Min(p => p.X), maxX = pixels.Max(p => p.X);
            int minY = pixels.Min(p => p.Y), maxY = pixels.Max(p => p.Y);
            HoleBlob blob = new HoleBlob
            {
                Id = id,
                Area = pixels.Count,
                Bounds = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1),
                MaxDistance = pixels.Max(p => result.Distance[p.Y, p.X]),
                TouchesBorder = minX == 0 || minY == 0 || maxX == result.Width - 1 || maxY == result.Height - 1,
            };
            blob.Centers = FindCenters(pixels, result.Distance, blob.MaxDistance, options);
            return blob;
        }

        /// <summary>
        /// 估計孔心：從夠深的像素中由深到淺挑選，並保持互相遠離。
        /// 單一圓孔的夠深像素都集中在圓心附近，只會挑到一個；連在一起的孔會挑到多個。
        /// </summary>
        private static List<Point> FindCenters(List<Point> pixels, float[,] distance, double maxDistance, HoleSampleOptions options)
        {
            double level = maxDistance * options.PeakLevel;
            List<Point> candidates = pixels.Where(p => distance[p.Y, p.X] >= level).ToList();
            double cx = candidates.Average(p => p.X), cy = candidates.Average(p => p.Y);
            double separation = maxDistance * options.SeparationRatio;

            List<Point> centers = new List<Point>();
            foreach (Point p in candidates
                .OrderByDescending(p => distance[p.Y, p.X])
                .ThenBy(p => (p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)))
            {
                if (centers.All(c => Dist(c, p) >= separation))
                    centers.Add(p);
            }
            // 依位置排序，子孔編號才不會隨深度的微小差異對調。
            return centers.OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
        }

        private static void SamplePoints(HoleSampleResult result, List<List<Point>> blobPixels, int[,] labels, HoleSampleOptions options)
        {
            float[,] distance = result.Distance;
            SpacingIndex index = new SpacingIndex(options.Spacing);

            // 符合邊距的孔心優先，確保每個孔都先拿到自己的點。
            foreach (HoleBlob blob in result.Blobs)
                foreach (Point center in blob.Centers)
                    if (distance[center.Y, center.X] >= result.Margin)
                        index.TryAdd(center);

            List<Point> allSafe = new List<Point>();
            for (int i = 0; i < result.Blobs.Count; i++)
            {
                List<Point> safe = blobPixels[i].Where(p => distance[p.Y, p.X] >= result.Margin).ToList();
                if (safe.Count == 0)
                    continue;
                allSafe.AddRange(safe);
                AddAnchoredGrids(result.Blobs[i], safe, distance, options.Spacing, index);
            }

            // 補點：網格排不到的位置（例如斜向細長的安全區）由深到淺補上。
            foreach (Point p in allSafe.OrderByDescending(p => distance[p.Y, p.X]).ThenBy(p => p.Y).ThenBy(p => p.X))
                index.TryAdd(p);

            foreach (Point p in index.Points)
            {
                HoleBlob blob = result.Blobs[labels[p.Y, p.X] - 1];
                result.Points.Add(new HoleSamplePoint
                {
                    Location = p,
                    EdgeDistance = distance[p.Y, p.X],
                    BlobId = blob.Id,
                    HoleIndex = NearestCenter(blob, p) + 1,
                    MeetsMargin = true,
                });
            }

            // 沒有任何安全位置的孔不能無聲漏掉：輸出最深點並標記未達邊距。
            foreach (HoleBlob blob in result.Blobs)
            {
                for (int i = 0; i < blob.Centers.Count; i++)
                {
                    Point center = blob.Centers[i];
                    bool hasPoint = result.Points.Any(p => p.BlobId == blob.Id && p.HoleIndex == i + 1);
                    if (!hasPoint && distance[center.Y, center.X] < result.Margin)
                    {
                        result.Points.Add(new HoleSamplePoint
                        {
                            Location = center,
                            EdgeDistance = distance[center.Y, center.X],
                            BlobId = blob.Id,
                            HoleIndex = i + 1,
                            MeetsMargin = false,
                        });
                    }
                }
            }

            result.Points = result.Points
                .OrderBy(p => p.BlobId).ThenBy(p => p.HoleIndex)
                .ThenBy(p => p.Location.Y).ThenBy(p => p.Location.X)
                .ToList();
        }

        /// <summary>
        /// 每個安全區各排一組網格，網格原點取該區的孔心或最深點。
        /// 不用全圖固定網格：小孔的安全區只有幾個像素，固定網格幾乎一定會整孔漏掉。
        /// </summary>
        private static void AddAnchoredGrids(HoleBlob blob, List<Point> safe, float[,] distance, int spacing, SpacingIndex index)
        {
            int minX = safe.Min(p => p.X), minY = safe.Min(p => p.Y);
            int width = safe.Max(p => p.X) - minX + 1, height = safe.Max(p => p.Y) - minY + 1;

            byte[] mask = new byte[width * height];
            foreach (Point p in safe)
                mask[(p.Y - minY) * width + (p.X - minX)] = 255;

            int[] regions;
            int unused;
            using (Mat maskMat = ToMat(mask, width, height))
                regions = LabelFlat(maskMat, out unused);

            Func<Point, int> regionOf = p => regions[(p.Y - minY) * width + (p.X - minX)];

            Dictionary<int, Point> anchors = new Dictionary<int, Point>();
            foreach (Point center in blob.Centers)
            {
                if (center.X < minX || center.Y < minY || center.X >= minX + width || center.Y >= minY + height)
                    continue;
                int region = regionOf(center);
                if (region != 0 && !anchors.ContainsKey(region))
                    anchors[region] = center;
            }

            foreach (IGrouping<int, Point> group in safe.GroupBy(regionOf))
            {
                Point anchor;
                if (!anchors.TryGetValue(group.Key, out anchor))
                {
                    double cx = group.Average(p => p.X), cy = group.Average(p => p.Y);
                    anchor = group
                        .OrderByDescending(p => distance[p.Y, p.X])
                        .ThenBy(p => (p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy))
                        .First();
                    index.TryAdd(anchor);
                }

                foreach (Point p in group)
                {
                    if (Math.Abs(p.X - anchor.X) % spacing == 0 && Math.Abs(p.Y - anchor.Y) % spacing == 0)
                        index.TryAdd(p);
                }
            }
        }

        private static int NearestCenter(HoleBlob blob, Point p)
        {
            int best = 0;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < blob.Centers.Count; i++)
            {
                double d = Dist(blob.Centers[i], p);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        private static double Dist(Point a, Point b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>只接受與既有點距離不小於間距的點；以格子分桶，大孔時不必逐點比對。</summary>
        private sealed class SpacingIndex
        {
            private readonly int _spacing;
            private readonly long _spacingSquared;
            private readonly Dictionary<long, List<Point>> _cells = new Dictionary<long, List<Point>>();
            public readonly List<Point> Points = new List<Point>();

            public SpacingIndex(int spacing)
            {
                _spacing = spacing;
                _spacingSquared = (long)spacing * spacing;
            }

            private static long Key(int cellX, int cellY)
            {
                return ((long)cellX << 32) | (uint)cellY;
            }

            public bool TryAdd(Point p)
            {
                int cellX = p.X / _spacing, cellY = p.Y / _spacing;
                for (int y = cellY - 1; y <= cellY + 1; y++)
                {
                    for (int x = cellX - 1; x <= cellX + 1; x++)
                    {
                        List<Point> cell;
                        if (!_cells.TryGetValue(Key(x, y), out cell))
                            continue;
                        foreach (Point q in cell)
                        {
                            long dx = p.X - q.X, dy = p.Y - q.Y;
                            if (dx * dx + dy * dy < _spacingSquared)
                                return false;
                        }
                    }
                }

                long key = Key(cellX, cellY);
                List<Point> target;
                if (!_cells.TryGetValue(key, out target))
                {
                    target = new List<Point>();
                    _cells[key] = target;
                }
                target.Add(p);
                Points.Add(p);
                return true;
            }
        }
    }
}

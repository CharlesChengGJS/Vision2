using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Flann;
using Emgu.CV.Structure;
using System.Drawing;
using FileStreamLibrary;
using System.IO;
using Flee.PublicTypes;
using static VisionLibrary.Process;

namespace VisionLibrary
{
    public class FindEdge : InpectBase
    {
        /// <summary>灰階最大變化點/// </summary>
        private PointF[] _points;
        /// <summary>灰階平均值的方向</summary>
        private int _dir;
        /// <summary>找出灰階平均值最大斷差的比較間隔</summary>
        private int _interval;
        private string _formula;
        private bool _saveImage = false; // 是否儲存影像

        public FindEdge(string SystemPath, int Index) : base(SystemPath, Index)
        {
            _points = new PointF[0];
            _systemPath = SystemPath;
        }

        public override void ReadSetting()
        {
            IniFile ini = new IniFile(_systemPath + "\\FindEdge.ini", true);

            //string section = "System" + _index.ToString();
            string section = "System";
            _dir = ini.ReadInt(section, "Direction", 1);
            _interval = ini.ReadInt(section, "Interval", 1);
            _formula = ini.ReadStr(section, "Formula", "R");
            _saveImage = ini.ReadBool(section, "SaveImage", false);

            ini.FileClose();
            ini.Dispose();
        }

        public override void Dispose()
        {
        }

        /// <summary>
        /// 分析輸入影像，計算灰階值變化最大的行（或列），並記錄該位置的座標。
        /// 用於找出物件邊界、變化點、對位參考等。
        /// </summary>
        /// <param name="SrcImage">輸入彩色影像</param>
        public override void Inspect(Image<Bgr, byte> SrcImage)
        {
            string dir = _systemPath + "\\FindEdge";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            if (_saveImage)
                SrcImage.Save(dir + "\\Src.bmp");

            Image<Gray, byte> gray = ImageArithmetic(SrcImage, _formula);

            if (_saveImage)
                gray.Save(dir + "\\_formula.bmp");

            double[] grayAvg = CalculateAverageGray(gray, _dir);
            int maxIndex = GetMaxGrayDifferenceIndex(grayAvg, _interval);

            // 根據方向找座標
            PointF resultPoint = _dir == 1
                ? new PointF(SrcImage.Width / 2f, maxIndex)
                : new PointF(maxIndex, SrcImage.Height / 2f);

            _points = new PointF[] { resultPoint };

            gray.Dispose();
        }

        public override PointF[] GetResult()
        {
            return _points;
        }

        public int GetDir()
        {
            return _dir;
        }
        public int GetInterval()
        {
            return _interval;
        }
        public string GetFormula()
        {
            return _formula;
        }

        /// <summary>
        /// 計算灰階圖像在指定方向上的平均灰階值(列是直的行是橫的)。
        /// </summary>
        /// <param name="roiGray">要處理的灰階ROI圖像</param>
        /// <param name="direction">
        /// 計算方向：
        /// 0 表示水平方向（X 方向）每列的平均灰階值，
        /// 1 表示垂直方向（Y 方向）每行的平均灰階值。
        /// </param>
        /// <returns>
        /// 若 direction 為 0，返回圖像每一列（row）的平均灰階值陣列；
        /// 若 direction 為 1，返回圖像每一行（column）的平均灰階值陣列。</returns>
        /// <exception cref="ArgumentException">當 direction 不是 0 或 1 時，拋出例外。</exception>
        public double[] CalculateAverageGray(Image<Gray, byte> roiGray, int direction = 1)
        {
            switch (direction)
            {
                case 1:
                    {
                        double[] result = new double[roiGray.Height];
                        for (int y = 0; y < roiGray.Height; y++)
                        {
                            int sum = 0;
                            for (int x = 0; x < roiGray.Width; x++)
                                sum += roiGray.Data[y, x, 0];
                            result[y] = sum / (double)roiGray.Width;
                        }
                        return result;
                    }

                case 0:
                    {
                        double[] result = new double[roiGray.Width];
                        for (int x = 0; x < roiGray.Width; x++)
                        {
                            int sum = 0;
                            for (int y = 0; y < roiGray.Height; y++)
                                sum += roiGray.Data[y, x, 0];
                            result[x] = sum / (double)roiGray.Height;
                        }
                        return result;
                    }

                default:
                    throw new ArgumentException("方向必須是 'X' 或 'Y'");
            }
        }

        /// <summary>
        /// 在平均灰階值陣列中，尋找最大灰階變化的位置索引。
        /// </summary>
        /// <param name="grayAvgValues">
        /// 灰階平均值陣列，例如每列或每行的平均灰階值。
        /// </param>
        /// <param name="interval">
        /// 比較的間隔，預設為 1，表示與下一個項目比較。可調整比較跨度。
        /// </param>
        /// <returns>
        /// 回傳發生最大灰階變化的位置索引（即 grayAvgValues[i]）。
        /// </returns>
        public int GetMaxGrayDifferenceIndex(double[] grayAvgValues, int interval = 1)
        {
            if (interval < 1) interval = 1;

            double maxDiff = 0; //最大差值
            int maxDiffIndex = 0;

            for (int i = 0; i < grayAvgValues.Length - interval; i++)
            {
                double diff = Math.Abs(grayAvgValues[i + interval] - grayAvgValues[i]);
                if (diff > maxDiff)
                {
                    maxDiff = diff;
                    maxDiffIndex = i;
                }
            }
            return maxDiffIndex;
        }

        public static Image<Gray, byte> ImageArithmetic(Image<Bgr, byte> input, string formula)
        {
            if (formula == "" || input.NumberOfChannels < 3)
            {
                return null;
            }
            // Define the context of our expression
            ExpressionContext context = new ExpressionContext();
            // Allow the expression to use all static public methods of System.Math
            context.Imports.AddType(typeof(Math));
            Image<Gray, byte>[] bgr = input.Split();
            // Define an int variable
            context.Variables["B"] = context.Variables["b"] = bgr[0];
            context.Variables["G"] = context.Variables["g"] = bgr[1];
            context.Variables["R"] = context.Variables["r"] = bgr[2];

            // Create a dynamic expression that evaluates to an Object
            IDynamicExpression eDynamic = context.CompileDynamic(formula);
            // Evaluate the expressions
            Image<Gray, byte> result = (Image<Gray, byte>)eDynamic.Evaluate();
            ScratchProcessFile("ImageArithmetic.bmp", result.Mat);
            return result;

        }
    }
}

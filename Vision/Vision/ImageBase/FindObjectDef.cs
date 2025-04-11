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
using FileStreamLibrary;
using System.Drawing;
using System.IO;
using static VisionLibrary.Process;

namespace VisionLibrary
{
    public class FindObjectDef : InpectBase
    {

        private PointF[] _points;
        private Rectangle[] _rectangles;
        private int _threshold;
        private int _minArea;
        private int _maxArea;
        private string _formula;
        private bool _isCorner;
        /// <summary> 0 左上, 1 左下, 2 ,右下 3 右上 </summary>
        private int _cornerIndex; 
        private float _ratio = 0.5f;
        public FindObjectDef(string SystemPath, int Index) : base(SystemPath, Index)
        {
            _points = new PointF[0];
            _rectangles = new Rectangle[0];
        }

        public override void ReadSetting()
        {
            IniFile ini = new IniFile(_systemPath + "FindObject.ini", true);

            string section = "System" + _index.ToString();
            _threshold = ini.ReadInt(section, "Threshold", 60);
            _minArea = ini.ReadInt(section, "MinArea", 300);
            _maxArea = ini.ReadInt(section, "MaxArea", 3660);
            _formula = ini.ReadStr(section, "Formula", "B-G");
            _isCorner = ini.ReadBool(section, "IsCorner", false);
            _cornerIndex = ini.ReadInt(section, "CornerIndex", 1);
            _ratio = ini.ReadFloat(section, "Ratio", 0.5f);

            ini.FileClose();
            ini.Dispose();
        }

        public override PointF[] GetResult()
        {
            return _points;
        }

        public override void Inspect(Image<Bgr, byte> SrcImage)
        {
            Image<Gray, byte> imageResult = ImageArithmetic(SrcImage, _formula);
            _points = GetObjectCenter2(imageResult, _threshold, _minArea, _maxArea, _ratio, out _rectangles);
           
            if(_isCorner)
            {
                _points = new PointF[_rectangles.Length] ;
                if (_cornerIndex == 0)
                {
                    for (int i = 0; i < _rectangles.Length; i++)
                        _points[i] = new PointF(_rectangles[i].X, _rectangles[i].Y);
                }
                else if (_cornerIndex == 1)
                {
                    for (int i = 0; i < _rectangles.Length; i++)
                        _points[i] = new PointF(_rectangles[i].X, _rectangles[i].Y + _rectangles[i].Height);
                }
                else if (_cornerIndex == 2)
                {
                    for (int i = 0; i < _rectangles.Length; i++)
                        _points[i] = new PointF(_rectangles[i].X+ _rectangles[i].Width, _rectangles[i].Y + _rectangles[i].Height);
                }
                else if (_cornerIndex == 3)
                {
                    for (int i = 0; i < _rectangles.Length; i++)
                        _points[i] = new PointF(_rectangles[i].X + _rectangles[i].Width, _rectangles[i].Y);
                }
            }
        }

        public override void Dispose()
        {

        }
    }


}

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
    public class FindCornerDef : InpectBase
    {

        private CornerDetectorDef _cornerDetector;
        private double _xv;
        private double _yv;
        private CornerDetectorDef.DetectionDirection _direction;
        private int _houghLinesThrehold = 0;
        private int _houghLinesMinLen = 0;
        private int _minlineGap = 0;
        private int _antiBrightNoise = 0;
        private PointF[] _points;
        private string _formula;
        public FindCornerDef(string SystemPath, int Index) : base(SystemPath, Index)
        {
            _points = new PointF[1];
            //_model = null;
            _cornerDetector = new CornerDetectorDef();

        }

        public override void ReadSetting()
        {
            IniFile ini = new IniFile(_systemPath + "FindCorner.ini", true);

            string section = "System" + _index.ToString();
            _houghLinesThrehold = ini.ReadInt(section, "HoughLinesThrehold", 0);
            _houghLinesMinLen = ini.ReadInt(section, "HoughLinesMinLen", 50);
            _minlineGap = ini.ReadInt(section, "MinlineGap", 5);
            _antiBrightNoise = ini.ReadInt(section, "AntiBrightNoise", 210);
            _direction = (CornerDetectorDef.DetectionDirection)ini.ReadInt(section, "Direction", 0);
            _xv = ini.ReadFloat(section, "Xv", (float)0.3);
            _yv = ini.ReadFloat(section, "Yv", (float)0.3);
            _formula = ini.ReadStr(section, "Formula", "G");

            ini.FileClose();
            ini.Dispose();
        }

        public override PointF[] GetResult()
        {
            return _points;
        }

        public override void Dispose()
        {
            _cornerDetector.Dispose();
        }
       
      
        public override void Inspect(Image<Bgr, byte> SrcImage)
        {
            Image<Gray, byte> imageResult = ImageArithmetic(SrcImage, _formula);
            _points[0] = _cornerDetector.GetCorner(imageResult, _direction, _houghLinesThrehold,_houghLinesMinLen,_minlineGap,_xv,_yv,_antiBrightNoise);
        }
    }
}

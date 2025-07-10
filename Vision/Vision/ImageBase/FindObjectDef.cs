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
using System.Security.Cryptography;

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
        private bool _isScale = false;
        private int _openSize = 0; // 形態學操作的大小
        private bool _white_class = false; // 是否白色物件

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
            _isScale = ini.ReadBool(section, "Scale", true);
            _openSize = ini.ReadInt(section, "OpenSize", 0);
            _white_class = ini.ReadBool(section, "WhiteClass", false);

            ini.FileClose();
            ini.Dispose();
        }

        public override PointF[] GetResult()
        {
            return _points;
        }

        public override void Inspect(Image<Bgr, byte> SrcImage)
        {
            if (_isScale)
            {
                Image<Bgr, byte> imageScale = new Image<Bgr, byte>(SrcImage.Width/2, SrcImage.Height/2);
                Image<Bgr, byte> img = SrcImage.Clone(); 
                CvInvoke.Resize(SrcImage, imageScale, new Size(0, 0), 0.5, 0.5, Inter.Linear);
                
                Image<Gray, byte> imageResult = ImageArithmetic(imageScale, _formula);
                
                
                if(_openSize > 0)
                {
                    Mat element = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Cross,new Size(_openSize, _openSize), new Point(-1, -1));
                    CvInvoke.MorphologyEx(imageResult, imageResult, Emgu.CV.CvEnum.MorphOp.Open, element,new Point(-1, -1), 3, Emgu.CV.CvEnum.BorderType.Default, new MCvScalar(0, 0, 0));
                }
                
                GetObjectCenter2(imageResult, _threshold, _minArea/4, _maxArea/4, _ratio, out _rectangles, _white_class);
                imageResult.Dispose();
                List<PointF> listP = new List<PointF>();
                List<Rectangle> listR = new List<Rectangle>();
                img.ROI = Rectangle.Empty;
                for (int i = 0; i < _rectangles.Length; i++)
                {
                    _rectangles[i].X = (int)(_rectangles[i].X * 2) - 2 + SrcImage.ROI.X;
                    _rectangles[i].Y = (int)(_rectangles[i].Y * 2) - 2 + SrcImage.ROI.Y;

                  
                    _rectangles[i].Width = (int)(_rectangles[i].Width * 2) + 4;
                    _rectangles[i].Height = (int)(_rectangles[i].Height * 2 + 4);
                    if(_rectangles[i].X < 0)
                        _rectangles[i].X = 0;
                    if (_rectangles[i].Y < 0)
                        _rectangles[i].Y = 0;
                    if (_rectangles[i].X + _rectangles[i].Width > img.Width)
                        _rectangles[i].Width = img.Width - _rectangles[i].X;
                    if (_rectangles[i].Y + _rectangles[i].Height > img.Height)
                        _rectangles[i].Height = img.Height - _rectangles[i].Y;
                    if (_rectangles[i].Width < 0)
                        _rectangles[i].Width = 0;
                    if (_rectangles[i].Height < 0)
                        _rectangles[i].Height = 0;

                    Rectangle[] rectangles;
                    img.ROI = _rectangles[i];
                    imageResult = ImageArithmetic(img, _formula);
                    PointF[] ps = GetObjectCenter2(imageResult, _threshold, _minArea, _maxArea, _ratio, out rectangles, _white_class);
                    imageResult.Dispose();
                    for(int j = 0; j < ps.Length; j++)
                    {
                        ps[i].X = ps[i].X + img.ROI.X - SrcImage.ROI.X;
                        ps[i].Y = ps[i].Y + img.ROI.Y - SrcImage.ROI.Y;

                        rectangles[i].X = rectangles[i].X + img.ROI.X - SrcImage.ROI.X;
                        rectangles[i].Y = rectangles[i].Y + img.ROI.Y - SrcImage.ROI.Y;
                    }

                    
                    listR.AddRange(rectangles);
                    listP.AddRange(ps);
                }

                img.Dispose();
                imageScale.Dispose();
                
                _points = listP.ToArray();
                _rectangles = listR.ToArray();
            }
            else
            {
                Image<Gray, byte> imageResult = ImageArithmetic(SrcImage, _formula);
                _points = GetObjectCenter2(imageResult, _threshold, _minArea, _maxArea, _ratio, out _rectangles, _white_class);
            }
            
           
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

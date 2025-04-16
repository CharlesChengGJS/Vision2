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
    public class JudgeObjectDef : InpectBase
    {

        private int _whiteObject;
        private int _threshold;
        private string _formula;
        private bool _isCorrect;
        private float _scaleRatio;
        private int _doCanny;
        private float _cannyThreshold1;
        private float _cannyThreshold2;
        private double _judgeValue;
        private bool _saveImage;

        public JudgeObjectDef(string SystemPath, int Index) : base(SystemPath, Index)
        {
            _isCorrect = false;
            _judgeValue = 0;
        }

        public override void ReadSetting()
        {
            IniFile ini = new IniFile(_systemPath + "JudgeObject.ini", true);

            string section = "System" + _index.ToString();
            _threshold = ini.ReadInt(section, "Threshold", 40);
            _scaleRatio = ini.ReadFloat(section, "ScaleRatio", (float)0.125);
            _doCanny = ini.ReadInt(section, "DoCanny", 1);
            _cannyThreshold1 = ini.ReadFloat(section, "CannyThreshold1", (float)50);
            _cannyThreshold2 = ini.ReadFloat(section, "CannyThreshold2", (float)150);

            _formula = ini.ReadStr(section, "Formula", "G");
            _whiteObject = ini.ReadInt(section, "WhiteObject", 1);
            _saveImage = ini.ReadBool(section, "SaveImage", false);

            ini.FileClose();
            ini.Dispose();
        }

       
        public override void Inspect(Image<Bgr, byte> SrcImage)
        {
            if(_saveImage)
            {
                string savePath = Path.Combine(Application.StartupPath, "JudgeObject" + _index.ToString(), "SrcImage" + ".jpg");
                if (!Directory.Exists(Path.Combine(Application.StartupPath, "JudgeObject" + _index.ToString())))
                    Directory.CreateDirectory(Path.Combine(Application.StartupPath, "JudgeObject" + _index.ToString()));
                SrcImage.Save(savePath);
            }
            Image<Gray, byte> imageResult = ImageArithmetic(SrcImage, _formula);

            if (_saveImage)
            {
                string savePath = Path.Combine(Application.StartupPath, "JudgeObject" + _index.ToString(), "ImageArithmetic" + ".jpg");
                imageResult.Save(savePath);
            }

            if (_scaleRatio != 1 && _scaleRatio > 0)
            {
                imageResult = imageResult.Resize(_scaleRatio, Inter.Nearest);
                if (_saveImage)
                {
                    string savePath = Path.Combine(Application.StartupPath, "JudgeObject" + _index.ToString(), "imageResult_Resize" + ".jpg");
                    imageResult.Save(savePath);
                }
            }
            if (_doCanny > 0)
            {
                imageResult = imageResult.Canny(_cannyThreshold1, _cannyThreshold2);
                if (_saveImage)
                {
                    string savePath = Path.Combine(Application.StartupPath, "JudgeObject" + _index.ToString(), "imageResult_Canny" + ".jpg");
                    imageResult.Save(savePath);
                }
            }
            Gray value = imageResult.GetAverage();
            _judgeValue = value.Intensity;
            bool whiteFlag = value.Intensity > _threshold;
            _isCorrect = (whiteFlag == (_whiteObject > 0));
        }

        public override bool IsCorrect()
        {
            return _isCorrect;
        }

        public double GetJudgeValue()
        {
            return _judgeValue;
        }

        public override void Dispose()
        {

        }
    }


}

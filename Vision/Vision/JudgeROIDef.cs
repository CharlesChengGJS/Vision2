using System;
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
    public class JudgeROIDef : IDisposable
    {
        private string _systemPath;
        private EProcessType _processType;
        private Rectangle _limitROI;
        private Size _searchSize;
        private InpectBase _inspect;

        private Rectangle _roi;
        private CameraCollectionDef _cameraCollection;
        private ECamera _eCamera;
        private PointF _ccdCenterPoint;
        private PointF _searchPoint;
        private int _index;
        public string ErrorCode;
        public JudgeROIDef(string SystemPath, CameraCollectionDef Cameras, ECamera Camera, int Index)
        {
            _eCamera = Camera;
            _cameraCollection = Cameras;
            _systemPath = SystemPath;
            _index = Index;
            ReadSetting();
        }

        private void RefreshROI(Point SearchP)
        {
            if (_searchSize.Width > 0 && _searchSize.Height > 0)
            {
                int OrgX = SearchP.X - _searchSize.Width / 2;
                int OrgY = SearchP.Y - _searchSize.Height / 2;

                int W = _searchSize.Width;
                int H = _searchSize.Height;

                if (_limitROI != Rectangle.Empty)
                {
                    if (OrgX < _limitROI.X)
                        OrgX = _limitROI.X;
                    if (OrgY < _limitROI.Y)
                        OrgY = _limitROI.Y;
                    if (OrgX + W > _limitROI.Width + _limitROI.X)
                    { 
                        W = _limitROI.Width - OrgX + _limitROI.X;
                        if (W < 0)
                            W = 0;
                    }
                    if (OrgY + H > _limitROI.Height + _limitROI.Y)
                    { 
                        H = _limitROI.Height - OrgY + _limitROI.Y;
                        if (H < 0)
                            H = 0;
                    }
                }


                _roi = new Rectangle(OrgX, OrgY, W, H);
                return;
            }

            _roi = Rectangle.Empty;
        }
        public void SetSearchPoint(PointF MMP)
        {
            _searchPoint = MMP;
            Point p = GetImagePoint(MMP, _ccdCenterPoint);
            RefreshROI(p);
        }

        public void ShiftSearchPoint(PointF Shift)
        {
            PointF shiftP = new PointF(Shift.X + _searchPoint.X, Shift.Y + _searchPoint.Y);
            Point p = GetImagePoint(shiftP, _ccdCenterPoint);
            RefreshROI(p);
        }

        public InpectBase GetInspectBase()
        {
            return _inspect;
        }
        private Point GetImagePoint(
                   PointF DstPoint,
                   PointF CenterPoint)
        {
            int orgX = (int)Math.Round((DstPoint.X - CenterPoint.X) / _cameraCollection.GetResolution(_eCamera), 0, MidpointRounding.AwayFromZero);
            int orgY = (int)Math.Round((DstPoint.Y - CenterPoint.Y) / _cameraCollection.GetResolution(_eCamera), 0, MidpointRounding.AwayFromZero);

            if (_cameraCollection.GetXYSwap(_eCamera))
            {
                int temp = orgX;
                orgX = orgY;
                orgY = temp;
            }

            if (_cameraCollection.GetXInverse(_eCamera))
            {
                orgX = -(orgX);
            }

            if (_cameraCollection.GetYInverse(_eCamera))
            {
                orgY = -(orgY);
            }
            orgX += _cameraCollection.GetWidth(_eCamera) / 2;
            orgY += _cameraCollection.GetHeight(_eCamera) / 2;

            return new Point(orgX, orgY);
        }

        public void Inspect(Image<Bgr, byte> SrcImage)
        {
            SrcImage.ROI = _roi;
            _inspect.Inspect(SrcImage);
            SrcImage.ROI = Rectangle.Empty;
        }

        public bool IsCorrect()
        {
            return _inspect.IsCorrect();
        }

        public void ReadSetting()
        {
            string dir = _systemPath + "\\Vision\\JudgeROI" + _index.ToString("0") + "\\";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            IniFile ini = new IniFile(dir + "JudgeROI.ini", true);

            string section = "System";

            _limitROI.X = ini.ReadInt(section, "LimitROI_X", 0);
            _limitROI.Y = ini.ReadInt(section, "LimitROI_Y", 0);
            _limitROI.Width = ini.ReadInt(section, "LimitROI_Width", 1280);
            _limitROI.Height = ini.ReadInt(section, "LimitROI_Height", 960);
            _searchSize.Width = ini.ReadInt(section, "SearchSize_Width", 200);
            _searchSize.Height = ini.ReadInt(section, "SearchSize_Height", 500);


            _ccdCenterPoint.X = ini.ReadFloat(section, "CcdCenterPoint_X", 0);
            _ccdCenterPoint.Y = ini.ReadFloat(section, "CcdCenterPoint_Y", 0);
            _searchPoint.X = ini.ReadFloat(section, "SearchPoint_X", 0);
            _searchPoint.Y = ini.ReadFloat(section, "SearchPoint_Y", 0);
            _processType = (EProcessType)Enum.Parse(typeof(EProcessType), ini.ReadStr(section, "ProcessType", EProcessType.JudgeObject.ToString()));

            if (_processType == EProcessType.JudgeObject)
                _inspect = new JudgeObjectDef(dir, 0);
   

            Point p = GetImagePoint(_searchPoint, _ccdCenterPoint);
            RefreshROI(p);

            ini.FileClose();
            ini.Dispose();
        }

        public void Dispose()
        {
            if (_inspect != null)
                _inspect.Dispose();
        }

    }
}

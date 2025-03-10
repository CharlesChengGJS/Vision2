using System;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Flann;
using Emgu.CV.Structure;
using FileStreamLibrary;
using System.Drawing;
using System.IO;
using static VisionLibrary.Process;
using nsAlignAlgorithm;

namespace VisionLibrary
{
    public class PanelAlignDef : IDisposable
    {
        private enum Quadrant
        {
            Quadrant1,
            Quadrant2,
            Quadrant3,
            Quadrant4,
        }

        private enum AlignType
        {
            PanelOnTable,
            PanelOnHand,
        }

        public enum CornerIndex
        {
            Corner1,
            Corner2,
            Count
        }

        private string _systemPath;
        private EProcessType[] _processType;
        private Rectangle[] _limitROI;
        private Size[] _searchSize;
        private InpectBase[] _inspect;
        private PointF[] _ccdCenterPoint;
        private ECamera[] _eCamera;
        private Rectangle[] _roi;

        private cAlignAlgorithmDef _alignAlgorithm;
        private PointF _panelCenter;
        private PointF _panelSize;
        private Quadrant[] _cornerQuadrant;
        private PointF[] _corners;
        private PointF _rotateCenter;
        private CameraCollectionDef _cameraCollection;
        private double _shiftX;
        private double _shiftY;
        private double _shiftAngle;
        private int _index;
        private AlignType _alignType;
        private bool _success;
        private float _distanceTolerance;
        public string ErrorCode;

        public PanelAlignDef(string SystemPath, CameraCollectionDef Cameras, ECamera Camera1, ECamera Camera2, int Index)
        {
            _eCamera = new ECamera[(int)CornerIndex.Count];
            _eCamera[0] = Camera1;
            _eCamera[1] = Camera2;
            _index = Index;
            _systemPath = SystemPath;
            _cameraCollection = Cameras;
            _processType = new EProcessType[(int)CornerIndex.Count];
            _limitROI = new Rectangle[(int)CornerIndex.Count];
            _searchSize = new Size[2];
            _inspect = new InpectBase[(int)CornerIndex.Count];
            _roi = new Rectangle[2];
            _cornerQuadrant = new Quadrant[(int)CornerIndex.Count];
            _corners = new PointF[2];
            _ccdCenterPoint = new PointF[(int)CornerIndex.Count];

            _alignAlgorithm = new cAlignAlgorithmDef();

            ReadSetting();
        }

        public Rectangle GetROI(int CornerIndex)
        {
            return _roi[CornerIndex];
        }

        private void RefreshROI(int cornerIndex, Point ImgP)
        {
            if (_searchSize[cornerIndex].Width > 0 && _searchSize[cornerIndex].Height > 0)
            {
                int OrgX = ImgP.X - _searchSize[cornerIndex].Width / 2;
                int OrgY = ImgP.Y - _searchSize[cornerIndex].Height / 2;

                int W = _searchSize[cornerIndex].Width;
                int H = _searchSize[cornerIndex].Height;

                if (_limitROI[cornerIndex] != Rectangle.Empty)
                {
                    if (OrgX < _limitROI[cornerIndex].X)
                        OrgX = _limitROI[cornerIndex].X;
                    if (OrgY < _limitROI[cornerIndex].Y)
                        OrgY = _limitROI[cornerIndex].Y;
                    if (OrgX + W > _limitROI[cornerIndex].Width + _limitROI[cornerIndex].X)
                    { 
                        W = _limitROI[cornerIndex].Width - OrgX + _limitROI[cornerIndex].X;
                        if (W < 0)
                            W = 0;
                    }
                    if (OrgY + H > _limitROI[cornerIndex].Height + _limitROI[cornerIndex].Y)
                    { 
                        H = _limitROI[cornerIndex].Height - OrgY + _limitROI[cornerIndex].Y;
                        if (H < 0)
                            H = 0;
                    }
                }


                _roi[cornerIndex] = new Rectangle(OrgX, OrgY, W, H);
                return;
            }

            _roi[cornerIndex] = Rectangle.Empty;
        }

        private Point GetImagePoint(
            ECamera eCamera,
            PointF DstPoint,
            PointF CenterPoint)
        {
            int orgX = (int)Math.Round((DstPoint.X - CenterPoint.X) / _cameraCollection.GetResolution(eCamera), 0, MidpointRounding.AwayFromZero);
            int orgY = (int)Math.Round((DstPoint.Y - CenterPoint.Y) / _cameraCollection.GetResolution(eCamera), 0, MidpointRounding.AwayFromZero);

            if (_cameraCollection.GetXYSwap(eCamera))
            {
                int temp = orgX;
                orgX = orgY;
                orgY = temp;
            }

            if (_cameraCollection.GetXInverse(eCamera))
            {
                orgX = -(orgX);
            }

            if (_cameraCollection.GetYInverse(eCamera))
            {
                orgY = -(orgY);
            }
            orgX += _cameraCollection.GetWidth(eCamera) / 2;
            orgY += _cameraCollection.GetHeight(eCamera) / 2;

            return new Point(orgX, orgY);
        }
        public void SetPanelCenter(Point P)
        {
            _panelCenter = P;
            UpdateCorner();

        }

        public void SetPanelSize(float XLength, float YLength)
        {
            _panelSize.X = XLength;
            _panelSize.Y = YLength;
            UpdateCorner();

        }

        public void SetDistanceTolerance(float Tolerance)
        {
            _distanceTolerance = Tolerance;
        }

        public bool Success()
        {
            return _success;
        }

        public InpectBase GetInspectBase(CornerIndex Corner)
        {
            return _inspect[(int)Corner];
        }

        public PointF GetResult(CornerIndex Corner)
        {
            PointF result = _inspect[(int)Corner].GetResult()[0];
            result.X = result.X + _roi[(int)Corner].X;
            result.Y = result.Y + _roi[(int)Corner].Y;
            return result;
        }

        public void Align(Image<Bgr, byte> SrcImage1, Image<Bgr, byte> SrcImage2)
        {
            _success = false;

            SrcImage1.ROI = _roi[0];
            _inspect[0].Inspect(SrcImage1);
            SrcImage2.ROI = _roi[1];
            _inspect[1].Inspect(SrcImage2);

            SrcImage1.ROI = Rectangle.Empty;
            SrcImage2.ROI = Rectangle.Empty;

            if (_inspect[0].GetResult().Length == 0)
            {
                ErrorCode = "Align Corner1 not Found";
                return;
            }

            if (_inspect[1].GetResult().Length == 0)
            {
                ErrorCode = "Align Corner2 not Found";
                return;
            }

            PointF imageP1 = _inspect[0].GetResult()[0];
            PointF imageP2 = _inspect[1].GetResult()[0];

            imageP1.X += _roi[0].X;
            imageP1.Y += _roi[0].Y;

            imageP2.X += _roi[1].X;
            imageP2.Y += _roi[1].Y;

            PointF cartesianP1 = GetCartesianPoint(imageP1, _eCamera[0], _ccdCenterPoint[0]);
            PointF cartesianP2 = GetCartesianPoint(imageP2, _eCamera[1], _ccdCenterPoint[1]);

            double oriDistance = GetDistance(_corners[0], _corners[1]);
            double nowDistance = GetDistance(cartesianP1, cartesianP2);
            if (Math.Abs(nowDistance - oriDistance) > _distanceTolerance)
            {
                ErrorCode = "Size Error";
                return;
            }

            PointF shiftP1 = new PointF(cartesianP1.X - _corners[0].X, cartesianP1.Y - _corners[0].Y);
            PointF shiftP2 = new PointF(cartesianP2.X - _corners[1].X, cartesianP2.Y - _corners[1].Y);

            if (_alignType == AlignType.PanelOnTable)
            {
                _alignAlgorithm.vGetShift(
                   shiftP1.X,
                   shiftP1.Y,
                   shiftP2.X,
                   shiftP2.Y,
                   ref _shiftX,
                   ref _shiftY,
                   ref _shiftAngle);
            }
            else if (_alignType == AlignType.PanelOnHand)
            {
                _alignAlgorithm.vGetShiftByPoint(
                   shiftP1.X,
                   shiftP1.Y,
                   shiftP2.X,
                   shiftP2.Y,
                   ref _shiftX,
                   ref _shiftY,
                   ref _shiftAngle);
            }

            ErrorCode = "";
            _success = true;
        }

        public void LearnPanelMatchModel(int CornerIndex, Image<Bgr, byte> SrcImage, PointF ModelSize)
        {
            if (_processType[CornerIndex] != EProcessType.MatchObject)
                return;

            RectangleF CartesianROI = new RectangleF(
                _corners[CornerIndex].X - ModelSize.X / 2,
                _corners[CornerIndex].Y - ModelSize.Y / 2,
                ModelSize.X,
                ModelSize.Y);
            Rectangle ImgROI = GetImageRetangle(_eCamera[CornerIndex], CartesianROI, _ccdCenterPoint[CornerIndex]);
            SrcImage.ROI = ImgROI;
            ((MatchObjectDef)_inspect[CornerIndex]).LearnModel(SrcImage);
            ((MatchObjectDef)_inspect[CornerIndex]).SaveModel();
        }

        private double GetDistance(PointF P1, PointF P2)
        {
            return Math.Pow((P1.X - P2.X) * (P1.X - P2.X) + (P1.Y - P2.Y) * (P1.Y - P2.Y), 0.5);
        }

        public void GetShift(ref double ShiftX, ref double ShiftY, ref double ShiftAngle)
        {
            ShiftX = _shiftX;
            ShiftY = _shiftY;

            double BufShiftAngle = ShiftAngle % 360.0;
            if (BufShiftAngle > 180)
                BufShiftAngle = 360 - BufShiftAngle;
            ShiftAngle = BufShiftAngle;
        }

        private PointF GetCartesianPoint(PointF ImagePos, ECamera Camera, PointF CCDCenterP)
        {
            ImagePos.X -= _cameraCollection.GetWidth(Camera) / 2;
            ImagePos.Y -= _cameraCollection.GetHeight(Camera) / 2;

            if (_cameraCollection.GetXInverse(Camera))
                ImagePos.X *= -1;

            if (_cameraCollection.GetYInverse(Camera))
                ImagePos.Y *= -1;

            PointF pos = new PointF();
            pos.X = ImagePos.X;
            pos.Y = ImagePos.Y;

            if (_cameraCollection.GetXYSwap(Camera))
            {
                pos.X = ImagePos.Y;
                pos.Y = ImagePos.X;
            }

            pos.X = (float)(_cameraCollection.GetResolution(Camera) * pos.X) + CCDCenterP.X;
            pos.Y = (float)(_cameraCollection.GetResolution(Camera) * pos.Y) + CCDCenterP.Y;

            return pos;
        }

        private void UpdateCorner()
        {

            switch (_cornerQuadrant[0])
            {
                case Quadrant.Quadrant1:
                    _corners[0].X = _panelCenter.X + _panelSize.X / 2;
                    _corners[0].Y = _panelCenter.Y + _panelSize.Y / 2;
                    break;

                case Quadrant.Quadrant2:
                    _corners[0].X = _panelCenter.X - _panelSize.X / 2;
                    _corners[0].Y = _panelCenter.Y + _panelSize.Y / 2;
                    break;

                case Quadrant.Quadrant3:
                    _corners[0].X = _panelCenter.X - _panelSize.X / 2;
                    _corners[0].Y = _panelCenter.Y - _panelSize.Y / 2;
                    break;

                case Quadrant.Quadrant4:
                    _corners[0].X = _panelCenter.X + _panelSize.X / 2;
                    _corners[0].Y = _panelCenter.Y - _panelSize.Y / 2;
                    break;
            }

            switch (_cornerQuadrant[1])
            {
                case Quadrant.Quadrant1:
                    _corners[1].X = _panelCenter.X + _panelSize.X / 2;
                    _corners[1].Y = _panelCenter.Y + _panelSize.Y / 2;
                    break;

                case Quadrant.Quadrant2:
                    _corners[1].X = _panelCenter.X - _panelSize.X / 2;
                    _corners[1].Y = _panelCenter.Y + _panelSize.Y / 2;
                    break;

                case Quadrant.Quadrant3:
                    _corners[1].X = _panelCenter.X - _panelSize.X / 2;
                    _corners[1].Y = _panelCenter.Y - _panelSize.Y / 2;
                    break;

                case Quadrant.Quadrant4:
                    _corners[1].X = _panelCenter.X + _panelSize.X / 2;
                    _corners[1].Y = _panelCenter.Y - _panelSize.Y / 2;
                    break;
            }

            Point p = GetImagePoint(_eCamera[0], _corners[0], _ccdCenterPoint[0]);
            RefreshROI(0, p);

            p = GetImagePoint(_eCamera[1], _corners[1], _ccdCenterPoint[1]);
            RefreshROI(1, p);

            _alignAlgorithm.vSetAlignPos(_corners[0].X, _corners[0].Y, _corners[1].X, _corners[1].Y);
            _alignAlgorithm.vSetCenterPos(_rotateCenter.X, _rotateCenter.Y);
        }

        private RectangleF GetCartesianCCDRetangle(int CornerIndex)
        {
            float w = (float)_cameraCollection.GetResolution(_eCamera[CornerIndex]) * _cameraCollection.GetWidth(_eCamera[CornerIndex]);
            float h = (float)_cameraCollection.GetResolution(_eCamera[CornerIndex]) * _cameraCollection.GetHeight(_eCamera[CornerIndex]);

            if (_cameraCollection.GetXYSwap(_eCamera[CornerIndex]))
            {
                float temp = w;
                w = h;
                h = temp;
            }

            return new RectangleF((float)_ccdCenterPoint[CornerIndex].X - w / 2, (float)_ccdCenterPoint[CornerIndex].Y - h / 2, w, h);
        }

        private Rectangle GetImageRetangle(
           ECamera CAMERA,
           RectangleF Rectangle,
           PointF CenterPoint)
        {
            int w = (int)Math.Round(Rectangle.Width / _cameraCollection.GetResolution(CAMERA), 0, MidpointRounding.AwayFromZero);
            int h = (int)Math.Round(Rectangle.Height / _cameraCollection.GetResolution(CAMERA), 0, MidpointRounding.AwayFromZero);

            int orgX = (int)Math.Round((Rectangle.X - CenterPoint.X) / _cameraCollection.GetResolution(CAMERA), 0, MidpointRounding.AwayFromZero);
            int orgY = (int)Math.Round((Rectangle.Y - CenterPoint.Y) / _cameraCollection.GetResolution(CAMERA), 0, MidpointRounding.AwayFromZero);

            if (_cameraCollection.GetXYSwap(CAMERA))
            {
                int temp = w;
                w = h;
                h = temp;

                temp = orgX;
                orgX = orgY;
                orgY = temp;
            }

            if (_cameraCollection.GetXInverse(CAMERA))
            {
                orgX = -(orgX + w);
            }

            if (_cameraCollection.GetYInverse(CAMERA))
            {
                orgY = -(orgY + h);
            }
            orgX += _cameraCollection.GetWidth(CAMERA) / 2;
            orgY += _cameraCollection.GetHeight(CAMERA) / 2;

            return new Rectangle(orgX, orgY, w, h);
        }

        public Rectangle GetPanelOnImage(int CornerIndex)
        {
            RectangleF CCDRectangle = GetCartesianCCDRetangle(CornerIndex);

            RectangleF Panel = new RectangleF(_panelCenter.X - _panelSize.X / 2, _panelCenter.Y - _panelSize.Y / 2, (float)_panelSize.X, (float)_panelSize.Y);

            if (CCDRectangle.IntersectsWith(Panel))
            {
                RectangleF ccd1Intersect = RectangleF.Intersect(CCDRectangle, Panel);
                return GetImageRetangle(_eCamera[CornerIndex], ccd1Intersect, _ccdCenterPoint[CornerIndex]);
            }

            return Rectangle.Empty;
        }

        public void SetRotateCenter(PointF RotateCenter)
        {
            _rotateCenter = RotateCenter;
        }

        public void ReadSetting()
        {
            string dir = _systemPath + "\\Vision\\PanelAlign" + _index.ToString() + "\\";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            IniFile ini = new IniFile(dir + "PanelAlign.ini", true);

            string section = "System";

            _alignType = (AlignType)Enum.Parse(typeof(AlignType), ini.ReadStr(section, "AlignType", AlignType.PanelOnTable.ToString()));
          
            _panelCenter.X = ini.ReadFloat(section, "PanelCenter_X", 0);
            _panelCenter.Y = ini.ReadFloat(section, "PanelCenter_Y", 0);
            _panelSize.X = ini.ReadFloat(section, "PanelSize_XLenth", 0);
            _panelSize.Y = ini.ReadFloat(section, "PanelSize_YLenth", 0);
            _rotateCenter.X = ini.ReadFloat(section, "RotateCeneter_X", 0);
            _rotateCenter.Y = ini.ReadFloat(section, "RotateCeneter_Y", 0);
            _distanceTolerance = ini.ReadFloat(section, "DistanceTolerance", 0);

            for (int i = 0; i < 2; i++)
            {
                _limitROI[i].X = ini.ReadInt(section, "LimitROI_X"+ i.ToString(), 0);
                _limitROI[i].Y = ini.ReadInt(section, "LimitROI_Y" + i.ToString(), 0);
                _limitROI[i].Width = ini.ReadInt(section, "LimitROI_Width" + i.ToString(), 1280);
                _limitROI[i].Height = ini.ReadInt(section, "LimitROI_Height" + i.ToString(), 960);
                _searchSize[i].Width = ini.ReadInt(section, "SearchSize_Width" + i.ToString(), 200);
                _searchSize[i].Height = ini.ReadInt(section, "SearchSize_Height" + i.ToString(), 500);
                _ccdCenterPoint[i].X = ini.ReadFloat(section, "CCDCenterPoint_X" + i.ToString(), 550);
                _ccdCenterPoint[i].Y = ini.ReadFloat(section, "CCDCenterPoint_Y" + i.ToString(), 550);
                _cornerQuadrant[i] = (Quadrant)ini.ReadInt(section, "CornerQuadrant" + i.ToString(), 0);

                _processType[i] = (EProcessType)Enum.Parse(typeof(EProcessType), ini.ReadStr(section, "ProcessType" + i.ToString(), EProcessType.MatchObject.ToString()));

                if (_processType[i] == EProcessType.FindObject)
                    _inspect[i] = new FindObjectDef(dir, i);
                else if (_processType[i] == EProcessType.MatchObject)
                    _inspect[i] = new MatchObjectDef(dir, i);
                else if (_processType[i] == EProcessType.FindCorner)
                    _inspect[i] = new FindCornerDef(dir, i);
            }
            UpdateCorner();

            ini.FileClose();
            ini.Dispose();
        }

        public void Dispose()
        {
            for (int i = 0; i < _inspect.Length; i++)
            {
                if (_inspect[i] != null)
                    _inspect[i].Dispose();
            }
        }

    }



}

using System;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Flann;
using Emgu.CV.Structure;
using FileStreamLibrary;
using System.Drawing;
using System.IO;
using static VisionLibrary.Process;
using VisionLibrary.Camera;
using System.CodeDom.Compiler;
using static System.Collections.Specialized.BitVector32;
using System.Runtime.Remoting.Messaging;

namespace VisionLibrary
{
    public class SearchPointDef : IDisposable
    {
        private string _systemPath;
        private EProcessType _processType;
        private Rectangle[] _limitROI;
        private Size[] _search_size;
        private InspectBase[] _inspect;

        private Rectangle[] _roi;
        private CameraCollectionDef _cameras;
        private ECamera _eCamera;
        private Point[] _image_search_point;
        private PointF[] _cartesian_search_point;
        private PointF _ccd_center_point;
        private bool[] _is_cartesian;
        private int _index;
        public string ErrorCode;
        public SearchPointDef(string SystemPath, CameraCollectionDef Cameras, ECamera ECamera, int Index)
        {
            _cameras = Cameras;
            _eCamera = ECamera;
            _systemPath = SystemPath;
            _index = Index;
            ReadSetting();
        }

        private void RefreshROI(Point NewPos,int idx)
        {
            if (_search_size[idx].Width > 0 && _search_size[idx].Height > 0)
            {
                int OrgX = NewPos.X - _search_size[idx].Width / 2;
                int OrgY = NewPos.Y - _search_size[idx].Height / 2;

                int W = _search_size[idx].Width;
                int H = _search_size[idx].Height;

                if (_limitROI[idx] != Rectangle.Empty)
                {
                    if (OrgX < _limitROI[idx].X)
                        OrgX = _limitROI[idx].X;
                    if (OrgY < _limitROI[idx].Y)
                        OrgY = _limitROI[idx].Y;
                    if (OrgX + W > _limitROI[idx].Width + _limitROI[idx].X)
                    {
                        W = _limitROI[idx].Width - OrgX + _limitROI[idx].X;
                        if (W < 0)
                            W = 0;
                    }
                    if (OrgY + H > _limitROI[idx].Height + _limitROI[idx].Y)
                    {
                        H = _limitROI[idx].Height - OrgY + _limitROI[idx].Y;
                        if (H < 0)
                            H = 0;
                    }
                }


                _roi[idx] = new Rectangle(OrgX, OrgY, W, H);
                return;
            }

            _roi[idx] = Rectangle.Empty;
        }

        private void RefreshROI(int idx)
        {
            if (_search_size[idx].Width > 0 && _search_size[idx].Height > 0)
            {
                int OrgX = _image_search_point[idx].X - _search_size[idx].Width / 2;
                int OrgY = _image_search_point[idx].Y - _search_size[idx].Height / 2;

                int W = _search_size[idx].Width;
                int H = _search_size[idx].Height;

                if (_limitROI[idx] != Rectangle.Empty)
                {
                    if (OrgX < _limitROI[idx].X)
                        OrgX = _limitROI[idx].X;
                    if (OrgY < _limitROI[idx].Y)
                        OrgY = _limitROI[idx].Y;
                    if (OrgX + W > _limitROI[idx].Width + _limitROI[idx].X)
                    { 
                        W = _limitROI[idx].Width - OrgX + _limitROI[idx].X - 1;
                        if (W < 0)
                            W = 0;
                    }
                    if (OrgY + H > _limitROI[idx].Height + _limitROI[idx].Y)
                    { 
                        H = _limitROI[idx].Height - OrgY + _limitROI[idx].Y - 1;
                        if (H < 0)
                            H = 0;
                    }
                }


                _roi[idx] = new Rectangle(OrgX, OrgY, W, H);
                return;
            }

            _roi[idx] = Rectangle.Empty;
        }
        public void SetSearchPoint(PointF MMP, int idx)
        {
            _cartesian_search_point[idx] = MMP;
            _image_search_point[idx] = GetImagePoint(MMP, _ccd_center_point);
            RefreshROI(idx);
        }

        public void ShiftSearchPoint(PointF Shift, int idx)
        {
            //PointF shiftP = new PointF(Shift.X + _cartesian_search_point[idx].X, Shift.Y + _cartesian_search_point[idx].Y);
            //_image_search_point[idx] = GetImagePoint(shiftP, _ccd_center_point);

            int shiftX = (int)Math.Round(Shift.X / _cameras.GetResolution(_eCamera), 0, MidpointRounding.AwayFromZero);
            int shiftY = (int)Math.Round(Shift.Y / _cameras.GetResolution(_eCamera), 0, MidpointRounding.AwayFromZero);

            if (_cameras.GetXYSwap(_eCamera))
            {
                int temp = shiftX;
                shiftX = shiftY;
                shiftY = temp;
            }

            if (_cameras.GetXInverse(_eCamera))
            {
                shiftX = -(shiftX);
            }

            if (_cameras.GetYInverse(_eCamera))
            {
                shiftY = -(shiftY);
            }
            
            Point NewPos = new Point(_image_search_point[idx].X + shiftX, _image_search_point[idx].Y + shiftY);
         
            RefreshROI(NewPos, idx);
        }

        public InspectBase GetInspectBase(int idx)
        {
            return _inspect[idx];
        }
        private Point GetImagePoint(
                   PointF DstPoint,
                   PointF CenterPoint)
        {
            int orgX = (int)Math.Round((DstPoint.X - CenterPoint.X) / _cameras.GetResolution(_eCamera), 0, MidpointRounding.AwayFromZero);
            int orgY = (int)Math.Round((DstPoint.Y - CenterPoint.Y) / _cameras.GetResolution(_eCamera), 0, MidpointRounding.AwayFromZero);

            if (_cameras.GetXYSwap(_eCamera))
            {
                int temp = orgX;
                orgX = orgY;
                orgY = temp;
            }

            if (_cameras.GetXInverse(_eCamera))
            {
                orgX = -(orgX);
            }

            if (_cameras.GetYInverse(_eCamera))
            {
                orgY = -(orgY);
            }
            orgX += _cameras.GetWidth(_eCamera) / 2;
            orgY += _cameras.GetHeight(_eCamera) / 2;

            return new Point(orgX, orgY);
        }

        public void Inspect(Image<Bgr, byte> SrcImage, int Idx)
        {
            SrcImage.ROI = _roi[Idx];
            _inspect[Idx].Inspect(SrcImage);
            SrcImage.ROI = Rectangle.Empty;
        }

        public PointF[] GetResult(int Idx)
        {
            PointF[] data =(PointF[]) _inspect[Idx].GetResult().Clone();
            for (int i = 0; i < data.Length; i++)
            {
                data[i].X += _roi[Idx].X;
                data[i].Y += _roi[Idx].Y;
            }
            return data;
        }

        public PointF[] GetCartesianResult(int Idx )
        {
            PointF[] data = (PointF[])_inspect[Idx].GetResult().Clone();
            for (int i = 0; i < data.Length; i++)
            {
                data[i].X += _roi[Idx].X;
                data[i].Y += _roi[Idx].Y;

                data[i] = GetCartesianPoint(data[i], _eCamera, _ccd_center_point);
            }
            return data;
        }

        public PointF GetCartesianPoint(PointF ImagePos, ECamera Camera, PointF CCDCenterP)
        {
            ImagePos.X -= _cameras.GetWidth(Camera) / 2;
            ImagePos.Y -= _cameras.GetHeight(Camera) / 2;

            if (_cameras.GetXInverse(Camera))
                ImagePos.X *= -1;

            if (_cameras.GetYInverse(Camera))
                ImagePos.Y *= -1;

            PointF pos = new PointF();
            pos.X = ImagePos.X;
            pos.Y = ImagePos.Y;

            if (_cameras.GetXYSwap(Camera))
            {
                pos.X = ImagePos.Y;
                pos.Y = ImagePos.X;
            }

            pos.X = (float)(_cameras.GetResolution(Camera) * pos.X) + CCDCenterP.X;
            pos.Y = (float)(_cameras.GetResolution(Camera) * pos.Y) + CCDCenterP.Y;

            return pos;
        }

        public int GetInspectNum()
        {
            return _inspect.Length;
        }

        public void ReadSetting()
        {
            string dir = _systemPath + "\\Vision\\SearchPoint" + _index.ToString("0") + "\\";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            IniFile ini = new IniFile(dir + "SearchPoint.ini", true);


            string section = "System";
            int InspectCount = ini.ReadInt("System", "InspectCount", 1);
            _ccd_center_point.X = ini.ReadFloat(section, "CcdCenterPoint_X", 0);
            _ccd_center_point.Y = ini.ReadFloat(section, "CcdCenterPoint_Y", 0);

            _limitROI = new Rectangle[InspectCount];
            _search_size = new Size[InspectCount];
            _cartesian_search_point = new PointF[InspectCount];
            _image_search_point = new Point[InspectCount];
            _inspect = new InspectBase[InspectCount];
            _is_cartesian = new bool[InspectCount];
            _roi = new Rectangle[InspectCount];
            for (int i = 0; i < InspectCount; i++)
            {
                section = "Item" + i.ToString();
                _limitROI[i].X = ini.ReadInt(section, "LimitROI_X", 0);
                _limitROI[i].Y = ini.ReadInt(section, "LimitROI_Y", 0);
                _limitROI[i].Width = ini.ReadInt(section, "LimitROI_Width", 1280);
                _limitROI[i].Height = ini.ReadInt(section, "LimitROI_Height", 960);
                _search_size[i].Width = ini.ReadInt(section, "search_size_Width", 200);
                _search_size[i].Height = ini.ReadInt(section, "search_size_Height", 500);
                _cartesian_search_point[i].X = ini.ReadFloat(section, "cartesian_search_pointX", 0);
                _cartesian_search_point[i].Y = ini.ReadFloat(section, "cartesian_search_pointY", 0);
                _processType = (EProcessType)Enum.Parse(typeof(EProcessType), ini.ReadStr(section, "ProcessType", EProcessType.FindObject.ToString()));

                if (_processType == EProcessType.FindObject)
                    _inspect[i] = new FindObjectDef(dir, i);
                else if (_processType == EProcessType.FindEdge)
                    _inspect[i] = new FindEdge(dir, i);

                if (ini.ReadBool(section, "UseCartesian", false))
                {
                    _is_cartesian[i] = true;
                    _image_search_point[i] = GetImagePoint(_cartesian_search_point[i], _ccd_center_point);
                }
                else
                {
                    _is_cartesian[i] = false;
                    _image_search_point[i].X = ini.ReadInt(section, "image_search_pointX", 0);
                    _image_search_point[i].Y = ini.ReadInt(section, "image_search_pointY", 0);
                }

                RefreshROI(i);

            }
            ini.FileClose();
            ini.Dispose();
        }

        public void Dispose()
        {
            if (_inspect != null)
            {
                for(int i = 0; i < _inspect.Length; i++)
                _inspect[i].Dispose();
            }
        }

    }
}

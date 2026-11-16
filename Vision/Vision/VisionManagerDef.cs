using CommonLibrary;
using Emgu.CV;
using Emgu.CV.Structure;
using FileStreamLibrary;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace VisionLibrary
{
    public enum EProcessType
    {
        FindObject,
        MatchObject,
        JudgeObject,
        FindCorner,
        FindEdge,
        Count
    }

    public class VisionManagerDef : IDisposable
    {
        public bool Ready { private set; get; }
        private CameraCollectionDef _cameraCollection;

        private JudgeROIDef[] _judgeROI;
        Thread[] _taskJudgeROI;
        private bool[] _doTaskJudgeROI;
        private ECamera[] _eCamerasJudgeROI;


        private SearchPointDef[] _searchPoint;
        Thread[] _taskSearchPoint;
        private bool[] _doTaskSearchPoint;
        private ECamera[] _eCamerasSearchPoint;

        private PanelAlignDef[] _panelAlign;
        Thread[] _taskPanelAlign;
        private bool[] _doTaskPanelAlign;
        private ECamera[][] _eCamerasPanelAlign;

        private string _systemDirPath;
        private Image<Bgr, byte>[] _images;

        private VideoRecorderDef[] _videoRecorder;
        private ECamera[] _recCamera = { };
        private DateTime[] _recCam_dt;
        private string _viedoRecPath;
        private string _saveDir = string.Empty;
        public bool ComponentsReady = true;
        private bool _saveImage = false;
        public VisionManagerDef(string SystemDirPath, string ViedoRecPath)
        {
            _systemDirPath = SystemDirPath;
            _viedoRecPath = ViedoRecPath;
            try
            {
                _cameraCollection = new CameraCollectionDef(_systemDirPath);
                String errorCode = String.Empty;
                Ready = true;
                if (!_cameraCollection.CreateSucces(ref errorCode))
                {
                    Ready = false;
                    ComponentsReady = false;
                    MessageBox.Show(errorCode);
                }

                _images = new Image<Bgr, byte>[(int)ECamera.Count];
                for (int i = 0; i < (int)ECamera.Count; i++)
                {
                    _images[i] = new Image<Bgr, byte>(_cameraCollection.GetWidth((ECamera)i), _cameraCollection.GetHeight((ECamera)i));
                }

                _taskJudgeROI = new Thread[(int)EImageJudge.Count];
                _judgeROI = new JudgeROIDef[(int)EImageJudge.Count];
                _doTaskJudgeROI = new bool[(int)EImageJudge.Count];
                _eCamerasJudgeROI = new ECamera[(int)EImageJudge.Count];

                _taskSearchPoint = new Thread[(int)ESearchPoint.Count];
                _searchPoint = new SearchPointDef[(int)ESearchPoint.Count];
                _doTaskSearchPoint = new bool[(int)ESearchPoint.Count];
                _eCamerasSearchPoint = new ECamera[(int)ESearchPoint.Count];

                _taskPanelAlign = new Thread[(int)EAlign.Count];
                _panelAlign = new PanelAlignDef[(int)EAlign.Count];
                _doTaskPanelAlign = new bool[(int)EAlign.Count];
                _eCamerasPanelAlign = new ECamera[(int)EAlign.Count][];

                string dir = _systemDirPath + "\\Vision\\";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                IniFile ini = new IniFile(dir + "Vision.ini", true);

                for (int i = 0; i < _doTaskSearchPoint.Length; i++)
                {
                    string section = "SearchPoint_";
                    section += ((ESearchPoint)i).ToString();
                    _eCamerasSearchPoint[i] = (ECamera)Enum.Parse(typeof(ECamera), ini.ReadStr(section, "ECamera", ECamera.Count.ToString()));
                }

                for (int i = 0; i < _doTaskJudgeROI.Length; i++)
                {
                    string section = "JudgeROI_";
                    section += ((EImageJudge)i).ToString();
                    _eCamerasJudgeROI[i] = (ECamera)Enum.Parse(typeof(ECamera), ini.ReadStr(section, "ECamera", ECamera.Count.ToString()));
                }

                for (int i = 0; i < _doTaskPanelAlign.Length; i++)
                {
                    string section = "PanelAlign_";
                    section += ((EAlign)i).ToString();
                    _eCamerasPanelAlign[i] = new ECamera[2];
                    _eCamerasPanelAlign[i][0] = (ECamera)Enum.Parse(typeof(ECamera), ini.ReadStr(section, "ECamera0", ECamera.Count.ToString()));
                    _eCamerasPanelAlign[i][1] = (ECamera)Enum.Parse(typeof(ECamera), ini.ReadStr(section, "ECamera1", ECamera.Count.ToString()));
                }

                _saveImage = ini.ReadBool("System", "SaveImage", false);
                _saveDir = ini.ReadStr("System", "SaveDir", string.Empty);

                if(_saveImage)
                {
                    if (!Directory.Exists(_saveDir))
                        Directory.CreateDirectory(_saveDir);
                }

                ini.FileClose();
                ini.Dispose();

                for (int i = 0; i < _taskJudgeROI.Length; i++)
                {
                    _judgeROI[i] = new JudgeROIDef(_systemDirPath, _cameraCollection, _eCamerasJudgeROI[i], i);

                    _taskJudgeROI[i] = new Thread(RunTaskJudgeROI);
                    _taskJudgeROI[i].Priority = ThreadPriority.BelowNormal;
                    _taskJudgeROI[i].IsBackground = true;
                    _taskJudgeROI[i].Start(i);
                }

                for (int i = 0; i < _taskSearchPoint.Length; i++)
                {
                    _searchPoint[i] = new SearchPointDef(_systemDirPath, _cameraCollection, _eCamerasSearchPoint[i], i);

                    _taskSearchPoint[i] = new Thread(RunTaskSearchPoint);
                    _taskSearchPoint[i].Priority = ThreadPriority.BelowNormal;
                    _taskSearchPoint[i].IsBackground = true;
                    _taskSearchPoint[i].Start(i);
                }

                for (int i = 0; i < _taskPanelAlign.Length; i++)
                {
                    
                    _panelAlign[i] = new PanelAlignDef(_systemDirPath, _cameraCollection, _eCamerasPanelAlign[i][0], _eCamerasPanelAlign[i][1], i);

                    _taskPanelAlign[i] = new Thread(RunTaskPanelAlign);
                    _taskPanelAlign[i].Priority = ThreadPriority.BelowNormal;
                    _taskPanelAlign[i].IsBackground = true;
                    _taskPanelAlign[i].Start(i);
                }

                ReadSetting();


                _videoRecorder = new VideoRecorderDef[_recCamera.Length];
                _recCam_dt = new DateTime[_recCamera.Length];
            }
            catch (Exception e)
            {
                LogDef.Add(
                    ELogFileName.Vision,
                    this.GetType().Name,
                    System.Reflection.MethodBase.GetCurrentMethod().Name,
                    e.ToString());
                MessageBox.Show(e.ToString());
            }
        }

        private void YuanXin(double x1, double y1, double x2, double y2, double R, ref double x, ref double y)
        {
            double c1 = (x2 * x2 - x1 * x1 + y2 * y2 - y1 * y1) / (2 * (x2 - x1));
            double c2 = (y2 - y1) / (x2 - x1);
            double A = (c2 * c2 + 1);
            double B = (2 * x1 * c2 - 2 * c1 * c2 - 2 * y1);
            double C = x1 * x1 - 2 * x1 * c1 + c1 * c1 + y1 * y1 - R * R;

            y = (-B + Math.Sqrt(B * B - 4 * A * C)) / (2 * A);
            x = c1 - c2 * y;
        }

        public PointF GetRobotCenter(ECamera eCAMERA, PointF resultP1, PointF resultP2, double rotateAngle)
        {
            PointF P1 = GetCartesianPoint(resultP1, eCAMERA, new PointF(0, 0));
            PointF P2 = GetCartesianPoint(resultP2, eCAMERA, new PointF(0, 0));

            double P1X = P1.X;
            double P1Y = P1.Y;
            double P2X = P2.X;
            double P2Y = P2.Y;

            LogDef.Add(
                    ELogFileName.Engineer,
                    this.GetType().Name,
                    System.Reflection.MethodBase.GetCurrentMethod().Name,
                    P1X.ToString("0.0") + "," +
                    P1Y.ToString("0.0") + "," +
                    P2X.ToString("0.0") + "," +
                    P2Y.ToString("0.0") + ",");

            double shiftD = Math.Sqrt((P2Y - P1Y) * (P2Y - P1Y) + (P2X - P1X) * (P2X - P1X));
            double centerR = Math.Abs(shiftD / 2 / Math.Sin(rotateAngle / 2 * Math.PI / 180.0));

            double P2P1Theta = Math.Atan2((P2Y - P1Y), (P2X - P1X)) * 180 / Math.PI;

            double cX = 0;
            double cY = 0;
            YuanXin(P1X, P1Y, P2X, P2Y, centerR, ref cX, ref cY);

            double cX2 = (P2X + P1X) - cX;
            double cY2 = (P2Y + P1Y) - cY;

            if (rotateAngle > 0)
            {
                if (P2P1Theta < 90 && P2P1Theta > 0)
                {
                    if (cX > cX2)
                        return new PointF((float)(cX2), (float)(cY2));
                    else
                        return new PointF((float)(cX), (float)(cY));
                }
                else if (P2P1Theta < 180 && P2P1Theta > 90)
                {
                    if (cX > cX2)
                        return new PointF((float)(cX2), (float)(cY2));
                    else
                        return new PointF((float)(cX), (float)(cY));
                }
                else if (P2P1Theta < 0 && P2P1Theta > -90)
                {
                    if (cX > cX2)
                        return new PointF((float)(cX), (float)(cY));
                    else
                        return new PointF((float)(cX2), (float)(cY2));
                }
                else if (P2P1Theta < -90 && P2P1Theta > -180)
                {
                    if (cX > cX2)
                        return new PointF((float)(cX), (float)(cY));
                    else
                        return new PointF((float)(cX2), (float)(cY2));
                }
            }
            else
            {
                if (P2P1Theta < 90 && P2P1Theta > 0)
                {
                    if (cX > cX2)
                        return new PointF((float)(cX), (float)(cY));
                    else
                        return new PointF((float)(cX2), (float)(cY2));
                }
                else if (P2P1Theta < 180 && P2P1Theta > 90)
                {
                    if (cX > cX2)
                        return new PointF((float)(cX), (float)(cY));
                    else
                        return new PointF((float)(cX2), (float)(cY2));
                }
                else if (P2P1Theta < 0 && P2P1Theta > -90)
                {
                    if (cX > cX2)
                        return new PointF((float)(cX2), (float)(cY2));
                    else
                        return new PointF((float)(cX), (float)(cY));
                }
                else if (P2P1Theta < -90 && P2P1Theta > -180)
                {
                    if (cX > cX2)
                        return new PointF((float)(cX2), (float)(cY2));
                    else
                        return new PointF((float)(cX), (float)(cY));
                }
            }
            return new PointF((float)(cX), (float)(cY));
        }

        public int GetViedoCamNum()
        {
            return _recCamera.Length;
        }
        public void StartRecord_Viedo(int iViedo, PictureBox picBx, string ViedoName)
        {
            if (iViedo < 0 || iViedo >= _recCamera.Length)
                return;

            _videoRecorder[iViedo] = new VideoRecorderDef(_viedoRecPath, picBx, _cameraCollection.GetCamera(_recCamera[iViedo]), ViedoName, 10);
            _videoRecorder[iViedo].StartRecord();
        }
        public void Replay_Viedo(int iViedo, DateTime dt, bool bLongMode)
        {
            if (iViedo < 0 || iViedo > _recCamera.Length)
                return;

            _recCam_dt[iViedo] = dt;
            _videoRecorder[iViedo].StartReplay(_recCam_dt[iViedo], bLongMode);
        }

        public int GetWidth(ECamera CAMERA)
        {
            return _cameraCollection.GetWidth(CAMERA);
        }

        public int GetHeight(ECamera CAMERA)
        {
            return _cameraCollection.GetHeight(CAMERA);
        }

        public int GetExposure(ECamera CAMERA)
        {
            return _cameraCollection.GetExposure(CAMERA);
        }

        public int GetExposureTimeRangeMax(ECamera CAMERA)
        {
            return _cameraCollection.GetExposureTimeRangeMax(CAMERA);
        }

        public int GetExposureTimeRangeMin(ECamera CAMERA)
        {
            return _cameraCollection.GetExposureTimeRangeMin(CAMERA);
        }

        public int GetExposureTimeRangeStep(ECamera CAMERA)
        {
            return _cameraCollection.GetExposureTimeRangeStep(CAMERA);
        }

        public void SetExposure(ECamera CAMERA, double Exposure)
        {
            _cameraCollection.SetExposure(CAMERA, Exposure);
        }

        public void SaveExposure(ECamera CAMERA, double Exposure)
        {
            _cameraCollection.SaveExposure(CAMERA, Exposure);
        }

        public Camera.CameraBaseDef GetCamera(ECamera CAMERA)
        {
            return _cameraCollection.GetCamera(CAMERA);
        }

        public void SetExposureByDefault(ECamera CAMERA)
        {
            _cameraCollection.SetExposureByDefault(CAMERA);
        }

        private void SetSearchPoint(ESearchPoint Index, PointF MMP, int ROI_Index = 0)
        {
            _searchPoint[(int)Index].SetSearchPoint(MMP, ROI_Index);
        }

        public void ShiftSearchPoint(ESearchPoint Index, PointF Shift, int ROI_Index = 0)
        {
            _searchPoint[(int)Index].ShiftSearchPoint(Shift, ROI_Index);
        }

        public SearchPointDef GetSearchPoint(ESearchPoint Index)
        {
            if (Index == ESearchPoint.Count)
                return null;

            return _searchPoint[(int)Index];
        }
        public Rectangle GetPanelOnImage(EAlign AlignIndex, ECamera Camera)
        {
            if (_eCamerasPanelAlign == null || _eCamerasPanelAlign[(int)AlignIndex] == null || _panelAlign == null || _panelAlign[(int)AlignIndex] == null)
                return Rectangle.Empty;

            if (_eCamerasPanelAlign[(int)AlignIndex][0] == Camera)
            {
                return _panelAlign[(int)AlignIndex].GetPanelOnImage(0);
            }
            if (_eCamerasPanelAlign[(int)AlignIndex][1] == Camera)
            {
                return _panelAlign[(int)AlignIndex].GetPanelOnImage(1);
            }

            return Rectangle.Empty;
        }

        public Image<Bgr, byte> GetImage(ECamera CAMERA, bool Update)
        {
            if (!Update)
                return _images[(int)CAMERA].Clone();

            _cameraCollection.CopyImage(CAMERA, _images[(int)CAMERA]);
            return _images[(int)CAMERA].Clone();
        }

        public void SetImage(ECamera CAMERA, Image<Bgr, byte> ImageSrc)
        {
            if (_images[(int)CAMERA].Width != ImageSrc.Width || _images[(int)CAMERA].Height != ImageSrc.Height)
                return;

            ImageSrc.CopyTo(_images[(int)CAMERA]);
        }

        public PanelAlignDef GetPanelAlign(EAlign Index)
        {
            if (Index == EAlign.Count)
                return null;

            return _panelAlign[(int)Index];
        }

        public Rectangle GetROI(EAlign AlignIndex, PanelAlignDef.CornerIndex Corner, ECamera Camera)
        {
            if (AlignIndex == EAlign.Count || (int)AlignIndex < 0 || (int)Corner < 0 || (int)Camera < 0 || Corner == PanelAlignDef.CornerIndex.Count || Camera == ECamera.Count)
                return Rectangle.Empty; ;

            if (_eCamerasPanelAlign == null || _eCamerasPanelAlign[(int)AlignIndex] == null || _panelAlign == null || _panelAlign[(int)AlignIndex] == null)
                return Rectangle.Empty;

            if (_eCamerasPanelAlign[(int)AlignIndex][(int)Corner] == Camera)
            {
                return _panelAlign[(int)AlignIndex].GetROI((int)Corner);
            }

            return Rectangle.Empty;
        }

        public PointF GetCartesianPoint(PointF ImagePos, ECamera Camera, PointF CCDCenterP)
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

        public void ReadSetting()
        {
            for (int i = 0; i < _judgeROI.Length; i++)
                _judgeROI[i].ReadSetting();
            for (int i = 0; i < _searchPoint.Length; i++)
                _searchPoint[i].ReadSetting();
            for (int i = 0; i < _panelAlign.Length; i++)
                _panelAlign[i].ReadSetting();

            
        }

        public void SearchPointInsepct(ESearchPoint Index, int ROI_Index = 0)
        {
           // Image<Bgr, byte> img = new Image<Bgr, byte>(_cameraCollection.GetWidth(_eCamerasSearchPoint[(int)Index]), _cameraCollection.GetHeight(_eCamerasSearchPoint[(int)Index]));
           // _cameraCollection.CopyImage(_eCamerasSearchPoint[(int)Index], img);

            _searchPoint[(int)Index].Inspect(_images[(int)_eCamerasSearchPoint[(int)Index]], ROI_Index);
        }

        public void JudgeInsepct(EImageJudge Index)
        {
          //  Image<Bgr, byte> img = new Image<Bgr, byte>(_cameraCollection.GetWidth(_eCamerasJudgeROI[(int)Index]), _cameraCollection.GetHeight(_eCamerasJudgeROI[(int)Index]));
          //  _cameraCollection.CopyImage(_eCamerasJudgeROI[(int)Index], img);
            _judgeROI[(int)Index].Inspect(_images[(int)_eCamerasJudgeROI[(int)Index]]);
        }
        public void PanelAlign(EAlign Index)
        {
            if (_eCamerasPanelAlign[(int)Index][0] == _eCamerasPanelAlign[(int)Index][1])
            {
                //_cameraCollection.CopyImage(_eCamerasPanelAlign[(int)Index][0], _images[(int)_eCamerasPanelAlign[(int)Index][0]]);
                _panelAlign[(int)Index].Align(_images[(int)_eCamerasPanelAlign[(int)Index][0]], _images[(int)_eCamerasPanelAlign[(int)Index][0]]);
            }
            else
            {
                // _cameraCollection.CopyImage(_eCamerasPanelAlign[(int)Index][0], _images[(int)_eCamerasPanelAlign[(int)Index][0]]);
                // _cameraCollection.CopyImage(_eCamerasPanelAlign[(int)Index][1], _images[(int)_eCamerasPanelAlign[(int)Index][1]]);
                _panelAlign[(int)Index].Align(_images[(int)_eCamerasPanelAlign[(int)Index][0]], _images[(int)_eCamerasPanelAlign[(int)Index][1]]);
            }
        }
        public void DoTaskJudgeROI(EImageJudge Index) { _doTaskJudgeROI[(int)Index] = true; }

        public bool TaskDoneJudgeROI(EImageJudge Index) { return !_doTaskJudgeROI[(int)Index]; }
        public void DoTaskPanelAlign(EAlign Index) { _doTaskPanelAlign[(int)Index] = true; }
        public bool TaskDonePanelAlign(EAlign Index) { return !_doTaskPanelAlign[(int)Index]; }

        private void RunTaskPanelAlign(Object para)
        {
            while (true)
            {
                Thread.Sleep(1);
                int index = Convert.ToInt32(para);
                if (_doTaskPanelAlign[index])
                {
                    try
                    {
                        if (index < (int)EAlign.Count)
                        {
                            //_images[_eCamerasPanelAlign[index][0] = new Image<Bgr, byte>(CameraCollection.GetWidth(_eCamerasPanelAlign[index][0]), CameraCollection.GetHeight(_eCamerasPanelAlign[index][0]));

                            if (_eCamerasPanelAlign[index][0] == _eCamerasPanelAlign[index][1])
                            {
                                _cameraCollection.CopyImage(_eCamerasPanelAlign[index][0], _images[(int)_eCamerasPanelAlign[index][0]]);
                                _panelAlign[index].Align(_images[(int)_eCamerasPanelAlign[index][0]], _images[(int)_eCamerasPanelAlign[index][0]]);
                                if(_saveImage)
                                {
                                    string savePath = _saveDir + "\\PanelAlign" + index.ToString() + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
                                    _images[(int)_eCamerasPanelAlign[index][0]].Save(savePath);
                                }
                            }
                            else
                            {
                                _cameraCollection.CopyImage(_eCamerasPanelAlign[index][0], _images[(int)_eCamerasPanelAlign[index][0]]);
                                _cameraCollection.CopyImage(_eCamerasPanelAlign[index][1], _images[(int)_eCamerasPanelAlign[index][1]]);

                                _panelAlign[index].Align(_images[(int)_eCamerasPanelAlign[index][0]], _images[(int)_eCamerasPanelAlign[index][1]]);

                                if (_saveImage)
                                {
                                    string savePath = _saveDir + "\\PanelAlign" + index.ToString() + "_" + DateTime.Now.ToString("0_yyyyMMdd_HHmmss") + ".jpg";
                                    _images[(int)_eCamerasPanelAlign[index][0]].Save(savePath);
                                    savePath = _saveDir + "\\PanelAlign" + index.ToString() + "_" + DateTime.Now.ToString("1_yyyyMMdd_HHmmss") + ".jpg";
                                    _images[(int)_eCamerasPanelAlign[index][1]].Save(savePath);
                                }

                            }
                        }
                        _doTaskPanelAlign[index] = false;
                    }
                    catch (Exception ex)
                    {
                        _doTaskPanelAlign[index] = false;
                        CommonLibrary.AlarmTextDisplay.Add(CommonLibrary.AlarmCode.Alarm, CommonLibrary.AlarmType.Alarm, ex.ToString());
                    }
                }
            }
        }

        public void DoTaskSearchPoint(ESearchPoint Index) { _doTaskSearchPoint[(int)Index] = true; }
        public bool TaskDoneSearchPoint(ESearchPoint Index) { return !_doTaskSearchPoint[(int)Index]; }

        private void RunTaskJudgeROI(Object para)
        {
            while (true)
            {
                Thread.Sleep(1);
                int index = Convert.ToInt32(para);
                if (_doTaskJudgeROI[index])
                {
                    try
                    {
                        if (index < (int)EImageJudge.Count)
                        {
                            _cameraCollection.CopyImage(_eCamerasJudgeROI[index], _images[(int)_eCamerasJudgeROI[index]]);
                            _judgeROI[index].Inspect(_images[(int)_eCamerasJudgeROI[index]]);

                            if (_saveImage)
                            {
                                string savePath = _saveDir + "\\JudgeROI" + index.ToString() + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
                                _images[(int)_eCamerasPanelAlign[index][0]].Save(savePath);
                            }
                        }
                        _doTaskJudgeROI[index] = false;
                    }
                    catch (Exception ex)
                    {
                        _doTaskJudgeROI[index] = false;
                        CommonLibrary.AlarmTextDisplay.Add(CommonLibrary.AlarmCode.Alarm, CommonLibrary.AlarmType.Alarm, ex.ToString());
                    }
                }
            }
        }

        private void RunTaskSearchPoint(Object para)
        {
            while (true)
            {
                Thread.Sleep(1);
                int index = Convert.ToInt32(para);
                if (_doTaskSearchPoint[index])
                {
                    try
                    {
                        if (index < (int)ESearchPoint.Count)
                        {
                            _cameraCollection.CopyImage(_eCamerasSearchPoint[index], _images[(int)_eCamerasSearchPoint[index]]);
                            for(int i = 0; i < _searchPoint[index].GetInspectNum(); i++)
                            {
                                Thread.Sleep(5);
                                _searchPoint[index].Inspect(_images[(int)_eCamerasSearchPoint[index]], i);
                                
                            }

                            if (_saveImage)
                            {
                                string savePath = _saveDir + "\\SearchPoint" + index.ToString() + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
                                _images[(int)_eCamerasPanelAlign[index][0]].Save(savePath);
                            }

                        }
                        _doTaskSearchPoint[index] = false;
                    }
                    catch (Exception ex)
                    {
                        _doTaskSearchPoint[index] = false;
                        CommonLibrary.AlarmTextDisplay.Add(CommonLibrary.AlarmCode.Alarm, CommonLibrary.AlarmType.Alarm, ex.ToString());
                    }
                }
            }
        }

        public PointF[] GetResult(ESearchPoint Index, int ROI_Index = 0)
        {
            return _searchPoint[(int)Index].GetResult(ROI_Index);
        }

        public bool IsCorrect(EImageJudge Index)
        {
            return _judgeROI[(int)Index].IsCorrect();
        }


        public void Dispose()
        {

            for (int i = 0; i < _taskSearchPoint.Length; i++)
            {
                if(_taskSearchPoint[i] != null)
                    _taskSearchPoint[i].Abort();
            }

            for (int i = 0; i < _taskJudgeROI.Length; i++)
            {
                if (_taskJudgeROI[i] != null)
                    _taskJudgeROI[i].Abort();
            }

            for (int i = 0; i < _taskPanelAlign.Length; i++)
            {
                if (_taskPanelAlign[i] != null)
                    _taskPanelAlign[i].Abort();
            }

            if (_cameraCollection != null)
                _cameraCollection.Dispose();

            for (int i = 0; i < _recCamera.Length; i++)
            {
                if (_videoRecorder[i] != null)
                {
                    _videoRecorder[i].StopRecord();
                    _videoRecorder[i].StopReplay();
                    _videoRecorder[i].Dispose();
                }
            }
        }
    }
}
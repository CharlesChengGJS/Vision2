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

        public bool ComponentsReady = true;
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

                for (int i = 0; i < _taskJudgeROI.Length; i++)
                {
                    _judgeROI[i] = new JudgeROIDef(_systemDirPath, _cameraCollection, _eCamerasJudgeROI[i], i);

                    _taskJudgeROI[i] = new Thread(RunTaskJudgeROI);
                    _taskJudgeROI[i].Priority = ThreadPriority.Normal;
                    _taskJudgeROI[i].IsBackground = true;
                    _taskJudgeROI[i].Start(i);
                }

                for (int i = 0; i < _taskSearchPoint.Length; i++)
                {
                    _searchPoint[i] = new SearchPointDef(_systemDirPath, _cameraCollection, _eCamerasSearchPoint[i], i);

                    _taskSearchPoint[i] = new Thread(RunTaskSearchPoint);
                    _taskSearchPoint[i].Priority = ThreadPriority.Normal;
                    _taskSearchPoint[i].IsBackground = true;
                    _taskSearchPoint[i].Start(i);
                }

                for (int i = 0; i < _taskPanelAlign.Length; i++)
                {
                    _eCamerasPanelAlign[i] = new ECamera[2];
                    _panelAlign[i] = new PanelAlignDef(_systemDirPath, _cameraCollection, _eCamerasPanelAlign[i][0], _eCamerasPanelAlign[i][1], i);

                    _taskPanelAlign[i] = new Thread(RunTaskPanelAlign);
                    _taskPanelAlign[i].Priority = ThreadPriority.Normal;
                    _taskPanelAlign[i].IsBackground = true;
                    _taskPanelAlign[i].Start(i);
                }

                ReadSetting();


                _videoRecorder = new VideoRecorderDef[_recCamera.Length];
                _recCam_dt = new DateTime[_recCamera.Length];
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
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

        private void SetSearchPoint(ESearchPoint Index, PointF MMP)
        {
            _searchPoint[(int)Index].SetSearchPoint(MMP);
        }

        public void ShiftSearchPoint(ESearchPoint Index, PointF Shift)
        {
            _searchPoint[(int)Index].ShiftSearchPoint(Shift);
        }

        public SearchPointDef GetSearchPoint(ESearchPoint Index)
        {
            if (Index == ESearchPoint.Count)
                return null;

            return _searchPoint[(int)Index];
        }
        public Rectangle GetPanelOnImage(EAlign AlignIndex, ECamera Camera)
        {
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

            if (_eCamerasPanelAlign[(int)AlignIndex][(int)Corner] == Camera)
            {
                return _panelAlign[(int)AlignIndex].GetROI((int)Corner);
            }

            return Rectangle.Empty;
        }

        public void ReadSetting()
        {
            for (int i = 0; i < _judgeROI.Length; i++)
                _judgeROI[i].ReadSetting();
            for (int i = 0; i < _searchPoint.Length; i++)
                _searchPoint[i].ReadSetting();
            for (int i = 0; i < _panelAlign.Length; i++)
                _panelAlign[i].ReadSetting();

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

                _eCamerasPanelAlign[i][0] = (ECamera)Enum.Parse(typeof(ECamera), ini.ReadStr(section, "ECamera0", ECamera.Count.ToString()));
                _eCamerasPanelAlign[i][1] = (ECamera)Enum.Parse(typeof(ECamera), ini.ReadStr(section, "ECamera1", ECamera.Count.ToString()));
            }

            ini.FileClose();
            ini.Dispose();
        }

        public void SearchPointInsepct(ESearchPoint Index)
        {
           // Image<Bgr, byte> img = new Image<Bgr, byte>(_cameraCollection.GetWidth(_eCamerasSearchPoint[(int)Index]), _cameraCollection.GetHeight(_eCamerasSearchPoint[(int)Index]));
           // _cameraCollection.CopyImage(_eCamerasSearchPoint[(int)Index], img);
            _searchPoint[(int)Index].Inspect(_images[(int)_eCamerasSearchPoint[(int)Index]]);
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
                            }
                            else
                            {
                                _cameraCollection.CopyImage(_eCamerasPanelAlign[index][0], _images[(int)_eCamerasPanelAlign[index][0]]);
                                _cameraCollection.CopyImage(_eCamerasPanelAlign[index][1], _images[(int)_eCamerasPanelAlign[index][1]]);

                                _panelAlign[index].Align(_images[(int)_eCamerasPanelAlign[index][0]], _images[(int)_eCamerasPanelAlign[index][1]]);
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
                            _searchPoint[index].Inspect(_images[(int)_eCamerasSearchPoint[index]]);
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

        public PointF[] GetResult(ESearchPoint Index)
        {
            return _searchPoint[(int)Index].GetResult();
        }

        public bool IsCorrect(EImageJudge Index)
        {
            return _judgeROI[(int)Index].IsCorrect();
        }


        public void Dispose()
        {
            for (int i = 0; i < _taskSearchPoint.Length; i++)
            {
                _taskSearchPoint[i].Abort();
            }

            for (int i = 0; i < _taskJudgeROI.Length; i++)
            {
                _taskJudgeROI[i].Abort();
            }

            for (int i = 0; i < _taskPanelAlign.Length; i++)
            {
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
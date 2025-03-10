using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Threading;
using System.Collections;
using AForge.Video;
using AForge.Video.DirectShow;
using Microsoft.Win32;

namespace VisionLibrary.Camera
{
    public class LogitechDef : CameraBaseDef
    {
        DirectShowLib.IAMCameraControl _CamControl;
        DirectShowLib.IAMVideoProcAmp _CamParmControl;

        readonly FilterInfoCollection _VideoDevices;
        readonly VideoCaptureDevice _VideoSource;
        readonly VideoCapabilities[] _VideoCapabilities;


        private Image<Bgr, byte> _ImgFrame;
        private Mat _MatFrame;
        private AutoResetEvent _GrabDoneEvent;
        private object _CopyLocker = new object();
        private bool _IsLive;
        readonly int _CameraIndex;
        readonly string ErrorCode;
        readonly double Resolution;
        private int _AliveTick;
        Thread _UpdateFrameThread;
        private bool _GrabbingLastestImage;

        public LogitechDef(string MonikerString, double fResolution)
        {
            _IsLive = true;
            _CameraIndex = -1;
            _CopyLocker = new object();

            _GrabDoneEvent = new AutoResetEvent(false);
            try
            {
                ErrorCode = string.Empty;
                Resolution = fResolution;

                _VideoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                for (int index = 0; index < _VideoDevices.Count; index++)
                {
                    int comport = GetComportFromMonikerString(_VideoDevices[index].MonikerString);
                    string comportString = "COM" + comport.ToString();
                    if (MonikerString.ToUpper() == comportString)
                    {
                        _CameraIndex = index;
                        _VideoSource = new VideoCaptureDevice(_VideoDevices[index].MonikerString);
                    }
                }
                if (_CameraIndex < 0)
                {
                    ErrorCode = "Camera initial fail.";
                    return;
                }
                _VideoCapabilities = _VideoSource.VideoCapabilities;

                VideoCapabilities cap = _VideoCapabilities[0];
                foreach (var item in _VideoCapabilities)
                {   //選FPS30以上，最高解析度
                    if(item.AverageFrameRate >= 30)
                        if (item.FrameSize.Width * item.FrameSize.Height >= cap.FrameSize.Width * cap.FrameSize.Height)
                            cap = item;
                }
                _VideoSource.VideoResolution = cap;
                _VideoSource.NewFrame += new NewFrameEventHandler(NewFrame);


                var sourceObj = FilterInfo.CreateFilter(_VideoDevices[_CameraIndex].MonikerString);
                var sourceBase = (DirectShowLib.IBaseFilter)sourceObj;
                _CamControl = (DirectShowLib.IAMCameraControl)sourceBase;
                _CamParmControl = (DirectShowLib.IAMVideoProcAmp)sourceBase;

                _CamControl.Set(DirectShowLib.CameraControlProperty.Exposure, 0, DirectShowLib.CameraControlFlags.Manual);
                _CamParmControl.Set(DirectShowLib.VideoProcAmpProperty.WhiteBalance, 5000, DirectShowLib.VideoProcAmpFlags.Manual);


                _VideoSource.Start();
                Thread.Sleep(1000);

            }
            catch
            {
                ErrorCode = "Camera initial fail.";
                throw;
            }
        }


        public override bool IsAlive()
        {
            if (!_IsLive)
                return false;

            //if (Math.Abs(Environment.TickCount - _aliveTick) > 3000)
            //    return false;

            return true;

        }

        public override bool IsRunning()
        {
            lock (_CopyLocker)
            {
                if (_VideoSource != null)
                    return _VideoSource.IsRunning;
                else
                    return true;
            }
        }

        public override void Restart()
        {
            lock (_CopyLocker)
            {
                _VideoSource.Start();
            }
        }

        public override double GetResolution()
        {
            return Resolution;
        }
        public static int GetCameraQuantities()
        {
            return new FilterInfoCollection(FilterCategory.VideoInputDevice).Count;
        }

        public override bool IniSuccess(ref String sErrorCode)
        {
            if (ErrorCode.Length > 0)
            {
                sErrorCode = ErrorCode;
                return false;
            }

            return true;
        }

        private void NewFrame(object sender, NewFrameEventArgs e)
        {
            if (_GrabbingLastestImage)
            {
                if (_ImgFrame == null)
                    _ImgFrame = new Image<Bgr, byte>(e.Frame.Width, e.Frame.Height);

                _AliveTick = Environment.TickCount;
                _ImgFrame = e.Frame.ToImage<Bgr, byte>();
                _GrabDoneEvent.Set();
                _GrabbingLastestImage = false;
            }
            else
                e.Frame.Dispose();
        }

        public override void Dispose()
        {
            if (_VideoSource == null)
                return;
            _VideoSource.Stop();
            _VideoSource.WaitForStop();
            if (_ImgFrame != null)
                _ImgFrame.Dispose();

            if (_MatFrame != null)
                _MatFrame.Dispose();
        }


        public override void CopyImage(IInputOutputArray cOutputArray)
        {
            try
            {
                _GrabDoneEvent.Reset();
                _GrabbingLastestImage = true;
                if (_GrabDoneEvent.WaitOne(100))
                {
                    lock (_CopyLocker)
                    {
                        if (_ImgFrame != null)
                            _ImgFrame.CopyTo((CvArray<byte>)cOutputArray);
                    }
                }
                _GrabbingLastestImage = false;
            }
            catch
            {
                //throw;
            }
        }

        public override void UndistortImage(IInputOutputArray cOutputArray, IInputOutputArray CameraMatrix, IInputOutputArray DistCoeffs)
        {
            try
            {
                lock (_CopyLocker)
                {
                    CvInvoke.Undistort(_MatFrame, cOutputArray, CameraMatrix, DistCoeffs);
                }
            }
            catch
            {
                throw;
            }
        }

        public override void SetLive(bool bIsLive)
        {
            lock (_CopyLocker)
            {
                _IsLive = bIsLive;
                if (bIsLive)
                    _VideoSource.Start();
                else
                    _VideoSource.SignalToStop();
            }
        }



        public override int GetHeight()
        {
            lock (_CopyLocker)
            {
                if (_VideoSource == null)
                    return 0;
                else
                    return _VideoSource.VideoResolution.FrameSize.Height;
            }
        }

        public override int GetWidth()
        {
            lock (_CopyLocker)
            {
                if (_VideoSource == null)
                    return 0;
                else
                    return _VideoSource.VideoResolution.FrameSize.Width;
            }
        }
        public override PropertyInfo GetExposureTimeRange()
        {
            lock (_CopyLocker)
            {
                if (_VideoSource == null)
                    return new PropertyInfo();
                PropertyInfo stRange = new PropertyInfo();
                _CamControl.Get(DirectShowLib.CameraControlProperty.Exposure, out stRange.iValue, out DirectShowLib.CameraControlFlags eFlag);
                _CamControl.GetRange(DirectShowLib.CameraControlProperty.Exposure, out stRange.iMin, out stRange.iMax, out stRange.iStep, out stRange.iDefault, out eFlag);
                return stRange;
            }
        }

        public override void SetExposureTime(int iValue)
        {
            lock (_CopyLocker)
            {
                if (_VideoSource != null)
                    _CamControl.Set(DirectShowLib.CameraControlProperty.Exposure, iValue, DirectShowLib.CameraControlFlags.Manual);
            }
        }

        public override string GetSerialNumberOrMonikerString()
        {

            if (this == null)
                return string.Empty;

            string monikerString = _VideoDevices[_CameraIndex].MonikerString;

            int s = 0;
            string monikerStringAfterSplit = string.Empty;
            for (int i = 0; i < monikerString.Length; i++)
            {
                if (monikerString[i] == '&')
                    s++;
                if (s == 3 && monikerString[i] != '&')
                    monikerStringAfterSplit += monikerString[i];
            }
            return monikerStringAfterSplit;
        }

        public static string[] GetAllMonikerString()
        {
            return new string[]{ "" };
        }

        private int GetComportFromMonikerString(string completeMonikerString)
        {
            RegistryKey localKey;
            if (Environment.Is64BitOperatingSystem)
                localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            else
                localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            string[] keys = completeMonikerString.Split('#');
            if(keys.Length < 2)
                return -1;
            string path = keys[1] + @"\" + keys[2];

            var value = localKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB\" + path).GetValue("LocationInformation").ToString();
            int comport = Convert.ToInt32(value.Split('.')[3]);
            return comport;
        }

        public string GetCameraName()
        {
            if (this == null)
                return string.Empty;
            return _VideoDevices[_CameraIndex].Name;
        }

        public override void OpenPropertyPage()
        {
            //VideoSource.DisplayPropertyPage(IntPtr.Zero);
        }

       

    }
}

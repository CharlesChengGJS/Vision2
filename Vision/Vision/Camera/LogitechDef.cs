using System;
using System.Runtime.InteropServices;
using System.Threading;
using AForge.Video.DirectShow;
using Microsoft.Win32;
using OcvCapture = OpenCvSharp.VideoCapture;
using OcvCaptureApi = OpenCvSharp.VideoCaptureAPIs;
using OcvCaptureProp = OpenCvSharp.VideoCaptureProperties;
using OcvMat = OpenCvSharp.Mat;
using OcvMatType = OpenCvSharp.MatType;

namespace VisionLibrary.Camera
{
    public class LogitechDef : CameraBaseDef
    {
        // DSHOW 直接控制曝光/白平衡（與 capture graph 平行存在，不靠 OpenCvSharp）
        DirectShowLib.IAMCameraControl _CamControl;
        DirectShowLib.IAMVideoProcAmp _CamParmControl;
        object _CamControlSourceObj;

        // 列舉裝置：保留 AForge.FilterInfoCollection（純 DSHOW 列舉，與 Emgu.CV 無關）
        readonly FilterInfoCollection _VideoDevices;

        // OpenCvSharp + DSHOW 抓圖
        OcvCapture _Capture;
        OcvMat _LatestMat;
        Thread _GrabThread;
        volatile bool _RunGrabThread;

        readonly object _CopyLocker = new object();
        bool _IsLive;
        readonly int _CameraIndex;
        string ErrorCode;
        readonly double Resolution;
      
        int _FrameWidth;
        int _FrameHeight;

        public LogitechDef(string MonikerString, double fResolution)
        {
            _IsLive = true;
            _CameraIndex = -1;
            ErrorCode = string.Empty;
            Resolution = fResolution;

            try
            {
                _VideoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                // 以 USB 實體連接位置（COM port 號）排序選擇 — 與舊行為相同
                for (int index = 0; index < _VideoDevices.Count; index++)
                {
                    int comport = GetComportFromMonikerString(_VideoDevices[index].MonikerString);
                    string comportString = "COM" + comport.ToString();
                    if (MonikerString.ToUpper() == comportString)
                    {
                        _CameraIndex = index;
                        break;
                    }
                }

                if (_CameraIndex < 0)
                {
                    ErrorCode = "Camera initial fail.";
                    return;
                }

                // 取得 DSHOW 控制介面（IAMCameraControl/IAMVideoProcAmp 與 capture graph 共存）
                _CamControlSourceObj = FilterInfo.CreateFilter(_VideoDevices[_CameraIndex].MonikerString);
                var sourceBase = (DirectShowLib.IBaseFilter)_CamControlSourceObj;
                _CamControl = (DirectShowLib.IAMCameraControl)sourceBase;
                _CamParmControl = (DirectShowLib.IAMVideoProcAmp)sourceBase;

                _CamControl.Set(DirectShowLib.CameraControlProperty.Exposure, 0, DirectShowLib.CameraControlFlags.Manual);
                _CamParmControl.Set(DirectShowLib.VideoProcAmpProperty.WhiteBalance, 5000, DirectShowLib.VideoProcAmpFlags.Manual);

                // 用 OpenCvSharp + DSHOW 開啟相機；index 與 FilterInfoCollection 一致（同一份 DSHOW 列舉）
                _Capture = new OcvCapture(_CameraIndex, OcvCaptureApi.DSHOW);
                if (!_Capture.IsOpened())
                {
                    ErrorCode = "Camera initial fail.";
                    return;
                }

                ChooseBestResolution();
                _FrameWidth = (int)_Capture.Get(OcvCaptureProp.FrameWidth);
                _FrameHeight = (int)_Capture.Get(OcvCaptureProp.FrameHeight);

                _LatestMat = new OcvMat();

                _RunGrabThread = true;
                _GrabThread = new Thread(GrabLoop) { IsBackground = true, Name = "LogitechDef.GrabLoop" };
                _GrabThread.Start();

                Thread.Sleep(1000);
            }
            catch
            {
                ErrorCode = "Camera initial fail.";
                throw;
            }
        }

        // OpenCvSharp 沒有列舉裝置支援解析度的 API；用候選清單嘗試 Set，挑能達成且 FPS>=30 的最大畫素
        private void ChooseBestResolution()
        {
            int[][] candidates =
            {
                new[] { 3840, 2160 }, new[] { 2592, 1944 }, new[] { 2048, 1536 },
                new[] { 1920, 1080 }, new[] { 1600, 1200 }, new[] { 1280, 960 },
                new[] { 1280, 720 },  new[] { 1024, 768 },  new[] { 640, 480 }
            };

            int bestPx = 0;
            int bestW = 0, bestH = 0;
            foreach (var c in candidates)
            {
                _Capture.Set(OcvCaptureProp.FrameWidth, c[0]);
                _Capture.Set(OcvCaptureProp.FrameHeight, c[1]);
                int actualW = (int)_Capture.Get(OcvCaptureProp.FrameWidth);
                int actualH = (int)_Capture.Get(OcvCaptureProp.FrameHeight);
                int actualFps = (int)_Capture.Get(OcvCaptureProp.Fps);
                int px = actualW * actualH;
                if (actualFps >= 30 && px > bestPx)
                {
                    bestPx = px;
                    bestW = actualW;
                    bestH = actualH;
                }
            }
            if (bestW > 0)
            {
                _Capture.Set(OcvCaptureProp.FrameWidth, bestW);
                _Capture.Set(OcvCaptureProp.FrameHeight, bestH);
            }
        }

        // 背景緒：純粹持續 Grab() 推進 DSHOW 內部 frame buffer，不做 Retrieve
        private void GrabLoop()
        {
            while (_RunGrabThread)
            {
                if (!_IsLive || _Capture == null || !_Capture.IsOpened())
                {
                    Thread.Sleep(20);
                    continue;
                }
                try
                {
                    
                    lock (_CopyLocker)
                    {
                        _Capture.Grab();
                    }
                    Thread.Sleep(5);
                }
                catch
                {
                    Thread.Sleep(20);
                }
            }
        }

        public override void CopyImage(Emgu.CV.IInputOutputArray cOutputArray)
        {
            try
            {
                lock (_CopyLocker)
                {
                    if (_Capture == null || !_Capture.IsOpened()) return;
                    _Capture.Retrieve(_LatestMat);
                    CopyLatestTo(cOutputArray);
                }
            }
            catch
            {
            }
        }

        // 把 OpenCvSharp.Mat 透過原生 memcpy 寫進呼叫端的 buffer。
        // cOutputArray 的具體型別由基底類別決定（呼叫端目前是 Image<Bgr,byte> / Mat），這裡只取 DataPointer 不做任何影像處理。
        private void CopyLatestTo(Emgu.CV.IInputOutputArray cOutputArray)
        {
            CopyOcvMatTo(_LatestMat, cOutputArray);
        }

        private static void CopyOcvMatTo(OcvMat src, Emgu.CV.IInputOutputArray cOutputArray)
        {
            if (src == null || src.Empty()) return;

            IntPtr dstPtr;
            int dstW, dstH, dstCh;

            if (cOutputArray is Emgu.CV.Image<Emgu.CV.Structure.Bgr, byte> img)
            {
                dstPtr = img.Mat.DataPointer;
                dstW = img.Width;
                dstH = img.Height;
                dstCh = img.NumberOfChannels;
            }
            else if (cOutputArray is Emgu.CV.Mat outMat)
            {
                dstPtr = outMat.DataPointer;
                dstW = outMat.Cols;
                dstH = outMat.Rows;
                dstCh = outMat.NumberOfChannels;
            }
            else
            {
                return;
            }

            if (dstW != src.Cols || dstH != src.Rows || dstCh != src.Channels())
                return;

            long bytes = (long)src.Total() * src.ElemSize();
            if (bytes > 0)
                CopyMemory(dstPtr, src.Data, (UIntPtr)bytes);
        }

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, UIntPtr count);

        public override void UndistortImage(Emgu.CV.IInputOutputArray cOutputArray, Emgu.CV.IInputOutputArray CameraMatrix, Emgu.CV.IInputOutputArray DistCoeffs)
        {
            try
            {
                lock (_CopyLocker)
                {
                    if (_LatestMat == null || _LatestMat.Empty()) return;

                    using (OcvMat camMat = WrapEmguMatAsOcv(CameraMatrix))
                    using (OcvMat distMat = WrapEmguMatAsOcv(DistCoeffs))
                    using (OcvMat dst = new OcvMat())
                    {
                        if (camMat == null || distMat == null) return;
                        OpenCvSharp.Cv2.Undistort(_LatestMat, dst, camMat, distMat);
                        CopyOcvMatTo(dst, cOutputArray);
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        // 把 Emgu.CV.Mat 的 DataPointer 包成共享記憶體的 OpenCvSharp.Mat（不複製、不擁有資料）
        private static OcvMat WrapEmguMatAsOcv(Emgu.CV.IInputOutputArray arr)
        {
            Emgu.CV.Mat m = arr as Emgu.CV.Mat;
            if (m == null) return null;
            OcvMatType type = OcvMatType.MakeType(EmguDepthToOcv(m.Depth), m.NumberOfChannels);
            return OcvMat.FromPixelData(m.Rows, m.Cols, type, m.DataPointer);
        }

        private static int EmguDepthToOcv(Emgu.CV.CvEnum.DepthType depth)
        {
            switch (depth)
            {
                case Emgu.CV.CvEnum.DepthType.Cv8U:  return OcvMatType.CV_8U;
                case Emgu.CV.CvEnum.DepthType.Cv8S:  return OcvMatType.CV_8S;
                case Emgu.CV.CvEnum.DepthType.Cv16U: return OcvMatType.CV_16U;
                case Emgu.CV.CvEnum.DepthType.Cv16S: return OcvMatType.CV_16S;
                case Emgu.CV.CvEnum.DepthType.Cv32S: return OcvMatType.CV_32S;
                case Emgu.CV.CvEnum.DepthType.Cv32F: return OcvMatType.CV_32F;
                case Emgu.CV.CvEnum.DepthType.Cv64F: return OcvMatType.CV_64F;
                default: throw new NotSupportedException("Unsupported Emgu.CV depth: " + depth);
            }
        }

        public override void Dispose()
        {
            _RunGrabThread = false;
            try
            {
                if (_GrabThread != null && _GrabThread.IsAlive)
                    _GrabThread.Join(2000);
            }
            catch { }

            if (_Capture != null)
            {
                try { _Capture.Release(); } catch { }
                _Capture.Dispose();
                _Capture = null;
            }
            if (_LatestMat != null)
            {
                _LatestMat.Dispose();
                _LatestMat = null;
            }
            if (_CamControlSourceObj != null)
            {
                try { Marshal.ReleaseComObject(_CamControlSourceObj); } catch { }
                _CamControlSourceObj = null;
                _CamControl = null;
                _CamParmControl = null;
            }
        }

        public override bool IsAlive()
        {
            return _IsLive;
        }

        public override bool IsRunning()
        {
            lock (_CopyLocker)
            {
                return _Capture != null && _Capture.IsOpened() && _RunGrabThread;
            }
        }

        public override void Restart()
        {
            lock (_CopyLocker)
            {
                if (_Capture != null && !_Capture.IsOpened())
                {
                    _Capture.Open(_CameraIndex, OcvCaptureApi.DSHOW);
                }
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

        public override bool IniSuccess(ref string sErrorCode)
        {
            if (ErrorCode.Length > 0)
            {
                sErrorCode = ErrorCode;
                return false;
            }
            return true;
        }

        public override void SetLive(bool bIsLive)
        {
            lock (_CopyLocker)
            {
                _IsLive = bIsLive;
            }
        }

        public override int GetHeight()
        {
            lock (_CopyLocker)
            {
                if (_Capture == null) return 0;
                return _FrameHeight;
            }
        }

        public override int GetWidth()
        {
            lock (_CopyLocker)
            {
                if (_Capture == null) return 0;
                return _FrameWidth;
            }
        }

        public override PropertyInfo GetExposureTimeRange()
        {
            lock (_CopyLocker)
            {
                if (_Capture == null || _CamControl == null)
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
                if (_CamControl != null)
                    _CamControl.Set(DirectShowLib.CameraControlProperty.Exposure, iValue, DirectShowLib.CameraControlFlags.Manual);
            }
        }

        public override string GetSerialNumberOrMonikerString()
        {
            if (_VideoDevices == null || _CameraIndex < 0 || _CameraIndex >= _VideoDevices.Count)
                return string.Empty;

            string monikerString = _VideoDevices[_CameraIndex].MonikerString;

            int s = 0;
            string monikerStringAfterSplit = string.Empty;
            for (int i = 0; i < monikerString.Length; i++)
            {
                if (monikerString[i] == '&') s++;
                if (s == 3 && monikerString[i] != '&')
                    monikerStringAfterSplit += monikerString[i];
            }
            return monikerStringAfterSplit;
        }

        public static string[] GetAllMonikerString()
        {
            return new string[] { "" };
        }

        private int GetComportFromMonikerString(string completeMonikerString)
        {
            try
            {
                RegistryKey localKey = Environment.Is64BitOperatingSystem
                    ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    : RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

                string[] keys = completeMonikerString.Split('#');
                if (keys.Length < 3)
                    return -1;
                string path = keys[1] + @"\" + keys[2];

                using (RegistryKey subKey = localKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB\" + path))
                {
                    if (subKey == null)
                        return -1;
                    object raw = subKey.GetValue("LocationInformation");
                    if (raw == null)
                        return -1;
                    string[] parts = raw.ToString().Split('.');
                    if (parts.Length < 4)
                        return -1;
                    return Convert.ToInt32(parts[3]);
                }
            }
            catch
            {
                return -1;
            }
        }

        public string GetCameraName()
        {
            if (_VideoDevices == null || _CameraIndex < 0 || _CameraIndex >= _VideoDevices.Count)
                return string.Empty;
            return _VideoDevices[_CameraIndex].Name;
        }

        public override void OpenPropertyPage()
        {
            try
            {
                if (_Capture != null && _Capture.IsOpened())
                    _Capture.Set(OcvCaptureProp.Settings, 1);
            }
            catch { }
        }
    }
}

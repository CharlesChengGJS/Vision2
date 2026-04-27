using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.Win32;

namespace VisionLibrary.Camera
{
    public class LogitechDef : CameraBaseDef
    {
        // DSHOW 硬體參數控制（曝光/白平衡）
        DirectShowLib.IAMCameraControl _CamControl;
        DirectShowLib.IAMVideoProcAmp _CamParmControl;
        object _CamControlSourceObj;

        readonly FilterInfoCollection _VideoDevices;

        // 用 AForge VideoCaptureDevice 直接吃 MonikerString 開相機，
        // 避免 OpenCV/AForge 的 DSHOW 索引錯位（多顆相機時會抓到內建 webcam）
        VideoCaptureDevice _AForgeCap;

        // 內部影像存儲改成 Emgu.CV.Mat（與下游消費端統一）
        Mat _LatestMat;

        // On-demand 擷取：CopyImage 設旗標 → 下一張 NewFrame 寫入 _LatestMat 並 Set，其餘 frame 全丟掉
        volatile bool _CaptureRequested;
        readonly ManualResetEventSlim _FrameReady = new ManualResetEventSlim(false);
        const int CaptureTimeoutMs = 500;

        readonly object _CopyLocker = new object();
        bool _IsLive;
        readonly int _CameraIndex;
        readonly string _MonikerString;
        string ErrorCode;
        readonly double Resolution;
        readonly int _WhiteBalance;

        int _FrameWidth;
        int _FrameHeight;

        public LogitechDef(string MonikerString, double fResolution, int whiteBalance = 6000)
        {
            _IsLive = true;
            _CameraIndex = -1;
            ErrorCode = string.Empty;
            Resolution = fResolution;
            _WhiteBalance = whiteBalance;

            try
            {
                _VideoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                // 以 USB 實體連接位置（COM port 號）找到目標裝置 — 與舊行為相同
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

                _MonikerString = _VideoDevices[_CameraIndex].MonikerString;

                // 取得 DSHOW 控制介面（IAMCameraControl/IAMVideoProcAmp 走硬體層，與 capture graph 並行存在）
                _CamControlSourceObj = FilterInfo.CreateFilter(_MonikerString);
                var sourceBase = (DirectShowLib.IBaseFilter)_CamControlSourceObj;
                _CamControl = (DirectShowLib.IAMCameraControl)sourceBase;
                _CamParmControl = (DirectShowLib.IAMVideoProcAmp)sourceBase;

                _CamControl.Set(DirectShowLib.CameraControlProperty.Exposure, 0, DirectShowLib.CameraControlFlags.Manual);
                // 色溫從 Camera.ini 的 WhiteBalance 欄位讀取（每顆相機獨立），預設 6000K
                _CamParmControl.Set(DirectShowLib.VideoProcAmpProperty.WhiteBalance, _WhiteBalance, DirectShowLib.VideoProcAmpFlags.Manual);

                // 直接以 MonikerString 開相機，索引完全跳過
                _AForgeCap = new VideoCaptureDevice(_MonikerString);
                ChooseBestResolution(_AForgeCap);
                _AForgeCap.NewFrame += OnNewFrame;
                _AForgeCap.Start();

                Thread.Sleep(1000);

                if (!_AForgeCap.IsRunning)
                {
                    ErrorCode = "Camera initial fail.";
                    return;
                }

                // 強制抓一張 frame，把 _FrameWidth/_FrameHeight 與 _LatestMat 尺寸定下來。
                // VisionManagerDef 在初始化時用 GetWidth/GetHeight 配置 display image buffer，
                // 若這時尺寸還是 0，後續 CopyMatTo 會因尺寸不一致而靜默丟棄影像。
                _FrameReady.Reset();
                _CaptureRequested = true;
                if (!_FrameReady.Wait(2000))
                {
                    _CaptureRequested = false;
                    ErrorCode = "Camera initial fail.";
                    return;
                }
            }
            catch
            {
                ErrorCode = "Camera initial fail.";
                throw;
            }
        }

        // 從 AForge 的 VideoCapabilities 中挑能達成且 FPS>=30 的最大畫素
        private void ChooseBestResolution(VideoCaptureDevice dev)
        {
            VideoCapabilities best = null;
            int bestPx = 0;
            foreach (VideoCapabilities cap in dev.VideoCapabilities)
            {
                int px = cap.FrameSize.Width * cap.FrameSize.Height;
                if (cap.AverageFrameRate >= 30 && px > bestPx)
                {
                    bestPx = px;
                    best = cap;
                }
            }
            if (best != null)
                dev.VideoResolution = best;
        }

        // AForge 在 worker thread 上推送 Bitmap；轉成 Emgu.CV.Mat（24bppRgb 在 Windows memory layout 即 BGR）
        // 只有當 CopyImage 設定 _CaptureRequested 時才實際處理 frame；其餘 frame 立即丟棄。
        private void OnNewFrame(object sender, NewFrameEventArgs args)
        {
         //   if (!_IsLive) return;
            if (!_CaptureRequested) return;

            Bitmap src = args.Frame;
            if (src == null) return;

            Bitmap bmp = src;
            bool ownsBmp = false;
            if (src.PixelFormat != PixelFormat.Format24bppRgb)
            {
                bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(bmp))
                    g.DrawImage(src, 0, 0);
                ownsBmp = true;
            }

            int w = bmp.Width, h = bmp.Height;
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                lock (_CopyLocker)
                {
                    if (_LatestMat == null || _LatestMat.Cols != w || _LatestMat.Rows != h || _LatestMat.NumberOfChannels != 3)
                    {
                        _LatestMat?.Dispose();
                        _LatestMat = new Mat(new Size(w, h), DepthType.Cv8U, 3);
                    }

                    int rowBytes = w * 3;
                    int dstStep = _LatestMat.Step;
                    IntPtr dstBase = _LatestMat.DataPointer;
                    for (int y = 0; y < h; y++)
                    {
                        IntPtr srcRow = IntPtr.Add(bd.Scan0, y * bd.Stride);
                        IntPtr dstRow = IntPtr.Add(dstBase, y * dstStep);
                        CopyMemory(dstRow, srcRow, (UIntPtr)rowBytes);
                    }

                    _FrameWidth = w;
                    _FrameHeight = h;
                }
                _CaptureRequested = false;
                _FrameReady.Set();
            }
            finally
            {
                bmp.UnlockBits(bd);
                if (ownsBmp) bmp.Dispose();
            }
        }

        public override void CopyImage(IInputOutputArray cOutputArray)
        {
            try
            {
                if (_AForgeCap == null || !_AForgeCap.IsRunning) return;

                // 觸發下一張 NewFrame 的擷取；其餘 frame 仍會被 OnNewFrame 早早丟掉
                _FrameReady.Reset();
                _CaptureRequested = true;

                if (!_FrameReady.Wait(CaptureTimeoutMs))
                {
                    _CaptureRequested = false;
                    return;
                }

                lock (_CopyLocker)
                {
                    if (_LatestMat == null || _LatestMat.IsEmpty) return;
                    CopyMatTo(_LatestMat, cOutputArray);
                }
            }
            catch
            {
            }
        }

        // 把 Emgu.CV.Mat 透過原生 memcpy 寫進呼叫端的 buffer。
        // cOutputArray 由基底類別決定（呼叫端目前是 Image<Bgr,byte> / Mat），這裡只取 DataPointer 不做任何影像處理。
        private static void CopyMatTo(Mat src, IInputOutputArray cOutputArray)
        {
            if (src == null || src.IsEmpty) return;

            IntPtr dstPtr;
            int dstW, dstH, dstCh;

            if (cOutputArray is Image<Bgr, byte> img)
            {
                dstPtr = img.Mat.DataPointer;
                dstW = img.Width;
                dstH = img.Height;
                dstCh = img.NumberOfChannels;
            }
            else if (cOutputArray is Mat outMat)
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

            if (dstW != src.Cols || dstH != src.Rows || dstCh != src.NumberOfChannels)
                return;

            long bytes = (long)src.Step * src.Rows;
            if (bytes > 0)
                CopyMemory(dstPtr, src.DataPointer, (UIntPtr)bytes);
        }

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, UIntPtr count);

        public override void UndistortImage(IInputOutputArray cOutputArray, IInputOutputArray CameraMatrix, IInputOutputArray DistCoeffs)
        {
            try
            {
                if (_AForgeCap == null || !_AForgeCap.IsRunning) return;

                _FrameReady.Reset();
                _CaptureRequested = true;
                if (!_FrameReady.Wait(CaptureTimeoutMs))
                {
                    _CaptureRequested = false;
                    return;
                }

                lock (_CopyLocker)
                {
                    if (_LatestMat == null || _LatestMat.IsEmpty) return;

                    using (Mat dst = new Mat())
                    {
                        CvInvoke.Undistort(_LatestMat, dst, CameraMatrix, DistCoeffs);
                        CopyMatTo(dst, cOutputArray);
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        public override void Dispose()
        {
            // 喚醒可能正在等下一張 frame 的 CopyImage / UndistortImage
            _CaptureRequested = false;
            try { _FrameReady.Set(); } catch { }

            try
            {
                if (_AForgeCap != null)
                {
                    _AForgeCap.NewFrame -= OnNewFrame;
                    if (_AForgeCap.IsRunning)
                    {
                        _AForgeCap.SignalToStop();
                        _AForgeCap.WaitForStop();
                    }
                    _AForgeCap = null;
                }
            }
            catch { }

            try { _FrameReady.Dispose(); } catch { }

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
                return _AForgeCap != null && _AForgeCap.IsRunning;
            }
        }

        public override void Restart()
        {
            lock (_CopyLocker)
            {
                if (_AForgeCap == null) return;
                if (!_AForgeCap.IsRunning)
                    _AForgeCap.Start();
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
                if (_AForgeCap == null) return 0;
                return _FrameHeight;
            }
        }

        public override int GetWidth()
        {
            lock (_CopyLocker)
            {
                if (_AForgeCap == null) return 0;
                return _FrameWidth;
            }
        }

        public override PropertyInfo GetExposureTimeRange()
        {
            lock (_CopyLocker)
            {
                if (_AForgeCap == null || _CamControl == null)
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
                if (_AForgeCap != null)
                    _AForgeCap.DisplayPropertyPage(IntPtr.Zero);
            }
            catch { }
        }
    }
}

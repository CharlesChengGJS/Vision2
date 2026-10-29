using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Basler.Pylon;
using Emgu.CV;

namespace VisionLibrary.Camera
{
    public class BaslerDef : CameraBaseDef
    {
        /// <summary>
        /// If using the GigE Camera , follow the steps
        /// Open jumbo packet function in NIC(set to 9014 bytes)
        /// Try to set packet size in pylon viewer(set to 9012 bytes) , and save to userset 1.
        /// Select defualt startup set to user set 1.
        /// </summary>
        private readonly Basler.Pylon.Camera _Cam;
        private readonly object Lock = new object();
        private readonly AutoResetEvent GrabEvent = new AutoResetEvent(false);
        private readonly string _SerialNum;
        private readonly double _Resolution;
        private readonly string _ErrorCode;
        private readonly double _FrameRate;

        private Emgu.CV.CvEnum.ColorConversion _format;

        private Mat _GrabImage;

        public bool IsOpen { get; private set; }
        public double CameraGainPercentageValue { get; private set; }
        public double CameraGammaPercentageValue { get; private set; }
        public double CameraBlackLevelPercentageValue { get; private set; }
        public double CameraExposureTimePercentageValue { get; private set; }

        /// <param name="frameRate">連續取像幀率上限, 小於等於 0 表示不限制 (以相機最大速度取像)</param>
        public BaslerDef(string serialNum, double fResolution,  Emgu.CV.CvEnum.ColorConversion Format = Emgu.CV.CvEnum.ColorConversion.BayerGb2Rgb, double frameRate = 10)
        {
            try
            {
                _format = Format;
                _FrameRate = frameRate;

                //_GrabImage = null;
                _ErrorCode = string.Empty;
                _Resolution = fResolution;
                _Cam = new Basler.Pylon.Camera(serialNum);
                
                OpenCamera();
                
                CvInvoke.UseOpenCL = true;
            }
            catch (Exception ex)
            {
                _ErrorCode = ex.ToString();
                throw;
            }
            _SerialNum = serialNum;
        }
        
        public override bool IsAlive()
        {
            return true;
        }
        private void OpenCamera()
        {

            //事件只訂閱一次, 斷線重連時不重複訂閱
            _Cam.CameraOpened += Configuration.AcquireContinuous;
            _Cam.ConnectionLost += OnConnectionLost;
            _Cam.StreamGrabber.ImageGrabbed += OnImageGrabbed;
            _Cam.Open();
            _GrabImage = new Mat(new Size(GetWidth(), GetHeight()), Emgu.CV.CvEnum.DepthType.Cv8U, 1);

            StartGrab();

            if (_Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerUsb")
            {
                CameraGainPercentageValue = _Cam.Parameters[PLCamera.Gain].GetValuePercentOfRange();
                CameraGammaPercentageValue = _Cam.Parameters[PLCamera.Gamma].GetValuePercentOfRange();
                CameraBlackLevelPercentageValue = _Cam.Parameters[PLCamera.BlackLevel].GetValuePercentOfRange();
                CameraExposureTimePercentageValue = _Cam.Parameters[PLCamera.ExposureTime].GetValuePercentOfRange();
            }
            if (_Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerGigE")
            {
                CameraGainPercentageValue = _Cam.Parameters[PLCamera.GainRaw].GetValuePercentOfRange();
                CameraGammaPercentageValue = _Cam.Parameters[PLCamera.Gamma].GetValuePercentOfRange();
                CameraBlackLevelPercentageValue = _Cam.Parameters[PLCamera.BlackLevelRaw].GetValuePercentOfRange();
            }
            // if (DeviceType == "BaslerGigE") Cam.Parameters[PLCamera.ExposureTimeAbs].SetValue(35000, FloatValueCorrection.ClipToRange);
            // if (DeviceType == "BaslerUsb") Cam.Parameters[PLCamera.ExposureTime].SetValue(35000, FloatValueCorrection.ClipToRange);
            IsOpen = true;
        }

        /// <summary>設定取像參數並啟動 StreamGrabber (開啟與斷線重連共用)</summary>
        private void StartGrab()
        {
            // Load parameters
            _Cam.Parameters[PLCameraInstance.MaxNumBuffer].SetValue(5);
            _Cam.Parameters[PLCamera.ShutterMode].TrySetValue(PLCamera.ShutterMode.Rolling);  //部分機種(如 GigE acA2500-14gm)無 ShutterMode, 有才設定
            //Cam.Parameters[PLCameraInstance.OutputQueueSize].SetValue(3);

            if (_Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerGigE")
            {
                _Cam.Parameters[PLCamera.GammaEnable].TrySetValue(true);
                _Cam.Parameters[PLCamera.GevHeartbeatTimeout].SetValue(3000);
                //StreamGrabber 開啟時自動測試網路可用的最大封包並設到相機 (網卡需開 Jumbo Packet 9014, 否則維持 1500)
                _Cam.Parameters[PLGigEStream.AutoPacketSize].TrySetValue(true);
                //封包遺失時要求相機重送, 避免整張影像作廢 (0xE1000014 incompletely grabbed)
                _Cam.Parameters[PLGigEStream.EnableResend].TrySetValue(true);
            }

            //限制連續取像幀率, 避免滿速取像佔滿 GigE 頻寬造成掉封包
            SetFrameRate();

            //相機斷電重開後參數會回到 UserSet 預設, 重新套用曝光
            if (_ExposureTimeSet)
                SetExposureTime((int)(CameraExposureTimePercentageValue * 100));

            // Start the grabbing of images until grabbing is stopped.

            //不再以 CanWaitForFrameTriggerReady 判斷, 部分 GigE 機種(如 acA2500-14gm)為 false 時會完全沒有啟動取像
            _Cam.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByStreamGrabber);
        }

        private void SetFrameRate()
        {
            if (_FrameRate <= 0)
            {
                _Cam.Parameters[PLCamera.AcquisitionFrameRateEnable].TrySetValue(false);
                return;
            }

            _Cam.Parameters[PLCamera.AcquisitionFrameRateEnable].TrySetValue(true);
            if (_Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerUsb")
                _Cam.Parameters[PLCamera.AcquisitionFrameRate].TrySetValue(_FrameRate, FloatValueCorrection.ClipToRange);
            if (_Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerGigE")
                _Cam.Parameters[PLCamera.AcquisitionFrameRateAbs].TrySetValue(_FrameRate, FloatValueCorrection.ClipToRange);
        }

        private readonly object ReconnectLock = new object();
        private int _ReconnectTimes = 0;
        private bool _ExposureTimeSet = false;

        private void OnConnectionLost(Object sender, EventArgs e)
        {
            _LastGrabError = "Connection lost";
        }

        /// <summary>
        /// GigE 斷線(Heartbeat timeout / 網路線 / 供電)後 Control channel 會關閉, 需 Close 再 Open 才能恢復取像
        /// </summary>
        private void Reconnect()
        {
            lock (ReconnectLock)
            {
                if (_Cam.IsOpen && _Cam.IsConnected && _Cam.StreamGrabber.IsGrabbing)
                    return;

                _Cam.Close();
                if (!_Cam.Open(5000, TimeoutHandling.Return))
                    throw new Exception("Camera reconnect fail (" + _SerialNum + "). " + GetGrabFailInfo());

                StartGrab();
                _ReconnectTimes++;
            }
        }


        public override string GetSerialNumberOrMonikerString()
        {
            return _SerialNum;
        }

        public static int GetCameraQuantities()
        {
            return CameraFinder.Enumerate().Count;
        }
        public override void Restart() { }

        public override bool IsRunning() { return true; }
        public override void CopyImage(IInputOutputArray cOutputArray)
        {
            //if (_Cam.WaitForFrameTriggerReady(10000, TimeoutHandling.ThrowException))
            //{
            //    _Cam.ExecuteSoftwareTrigger();
            //}
            if (!_Cam.IsOpen || !_Cam.IsConnected || !_Cam.StreamGrabber.IsGrabbing)
                Reconnect();

            GrabEvent.Reset();
            if (!GrabEvent.WaitOne(10000))
                throw new Exception("Grab fail. " + GetGrabFailInfo());
            lock (Lock)
            {
                CvInvoke.CvtColor(_GrabImage, cOutputArray, _format);
            }
        }


        public override void UndistortImage(IInputOutputArray cOutputArray, IInputOutputArray CameraMatrix, IInputOutputArray DistCoeffs)
        {

        }
        private int _GrabFailTimes = 0;
        private string _LastGrabError = "";

        /// <summary>Grab fail 時的診斷資訊: 是否取像中、失敗次數、最後一次錯誤</summary>
        private string GetGrabFailInfo()
        {
            string info = "IsConnected=" + _Cam.IsConnected + ", IsGrabbing=" + _Cam.StreamGrabber.IsGrabbing + ", FailTimes=" + _GrabFailTimes + ", ReconnectTimes=" + _ReconnectTimes;
            if (_LastGrabError != "")
                info += ", LastError=" + _LastGrabError;
            //斷線時讀相機參數會丟 "Control channel not open", 不可蓋掉原本的錯誤訊息
            try
            {
                if (_Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerGigE")
                    info += ", PacketSize=" + _Cam.Parameters[PLCamera.GevSCPSPacketSize].GetValue() + ", InterPacketDelay=" + _Cam.Parameters[PLCamera.GevSCPD].GetValue() + ", FrameRate=" + _Cam.Parameters[PLCamera.ResultingFrameRateAbs].GetValue().ToString("0.0");
            }
            catch { }
            return info;
        }

        private void OnImageGrabbed(Object sender, ImageGrabbedEventArgs e)
        {
            //Mat mat;
            try
            {
                IGrabResult grabResult = e.GrabResult;
                if (grabResult.GrabSucceeded)
                {
                    //mat = new Mat(new Size(grabResult.Width, grabResult.Height), Emgu.CV.CvEnum.DepthType.Cv8U, 1);
                    //mat.SetTo<byte>((byte[])grabResult.PixelData);
                    lock (Lock)
                    {
                        //if (_GrabImage != null)
                        //    _GrabImage.Dispose();

                        _GrabImage.SetTo<byte>((byte[])grabResult.PixelData);
                        //_GrabImage = mat;
                        GrabEvent.Set();
                    }
                }
                else
                {
                    //記錄失敗原因, Grab fail 時帶出 (GigE 常見: 封包大小超過網卡 MTU / 頻寬不足 → 0xE1000014 incompletely grabbed)
                    _GrabFailTimes++;
                    _LastGrabError = "0x" + grabResult.ErrorCode.ToString("X") + " " + grabResult.ErrorDescription;
                }
            }
            catch (Exception ex)
            {
                _GrabFailTimes++;
                _LastGrabError = ex.Message;
                MessageBox.Show(ex.ToString(), "PylonNet5ToUMat", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
            finally
            {
                //Dispose the grab result if needed for returning it to the grab loop.
                e.DisposeGrabResultIfClone();
            }
            //GC.Collect();
        }

        /*  old function 
        public bool OpenCamera()
        {
            try
            {
                Cam.CameraOpened += Configuration.AcquireContinuous;
                Cam.Open();
                Cam.Parameters[PLCameraInstance.MaxNumBuffer].SetValue(5);
                Cam.Parameters[PLCameraInstance.OutputQueueSize].SetValue(1);
                Cam.StreamGrabber.Start();

                //pass parm
                CameraGainValue = Cam.Parameters[PLCamera.Gain].GetValuePercentOfRange();
                CameraGammaValue = Cam.Parameters[PLCamera.Gamma].GetValuePercentOfRange();
                CameraBlackLevelValue = Cam.Parameters[PLCamera.BlackLevel].GetValuePercentOfRange();
                IsOpen = Cam.IsOpen;
                return true;
            }
            catch(Exception)
            {
                throw;
            }
        }
        */

        /*
    public Mat Grab()
    {
        lock (Lock)
        {
            try
            {
                IGrabResult grabResult = Cam.StreamGrabber.RetrieveResult(5000, TimeoutHandling.ThrowException);
                // Image grabbed successfully?
                if (grabResult.GrabSucceeded)
                {
                    Mat mat = new Mat(new Size(grabResult.Width, grabResult.Height), Emgu.CV.CvEnum.DepthType.Cv8U, 1);
                    mat.SetTo<byte>((byte[])grabResult.PixelData);
                    return mat.Clone();
                }
                else
                {
                    throw new Exception("Camera disconnect (" + Cam.CameraInfo[CameraInfoKey.SerialNumber] + ")");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
    */
        private void Stop()
        {
            if (IsOpen)
            {
                _Cam.StreamGrabber.Stop();
                _Cam.Close();
                IsOpen = false;
            }

        }

        //public void vSetCameraGain(double value)
        //{
        //    CameraGainPercentageValue = value;
        //    try
        //    {
        //        if ( _Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerUsb")
        //             _Cam.Parameters[PLCamera.Gain].SetValuePercentOfRange(value);
        //        if ( _Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerGigE")
        //             _Cam.Parameters[PLCamera.GainRaw].SetValuePercentOfRange(value);
        //    }
        //    catch
        //    {
        //        throw;
        //    }
        //}

        //public void CameraFrameRate(double frameRate)
        //{

        //    try
        //    {
        //         _Cam.Parameters[PLCamera.AcquisitionFrameRateEnable].TrySetValue(true);
        //        if ( _Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerUsb")
        //             _Cam.Parameters[PLCamera.AcquisitionFrameRate].TrySetValue(frameRate);
        //        if ( _Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerGigE")
        //             _Cam.Parameters[PLCamera.AcquisitionFrameRateAbs].TrySetValue(frameRate);
        //    }
        //    catch
        //    {
        //        throw;
        //    }
        //}

        //public void vSetCameraGamma(double value)
        //{
        //    CameraGammaPercentageValue = value;
        //    try
        //    {
        //        //Cam.Parameters[PLCamera.Gamma].SetValuePercentOfRange(value);
        //        if ( _Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerUsb")
        //             _Cam.Parameters[PLCamera.Gamma].SetValuePercentOfRange(value);
        //        if ( _Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerGigE")
        //             _Cam.Parameters[PLCamera.Gamma].SetValuePercentOfRange(value);
        //    }
        //    catch
        //    {
        //        throw;
        //    }
        //}

        //public void vSetCameraBlackLevel(double value)
        //{
        //    CameraBlackLevelPercentageValue = value;
        //    try
        //    {
        //        if ( _Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerUsb")
        //             _Cam.Parameters[PLCamera.BlackLevel].SetValuePercentOfRange(value);
        //        if ( _Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerGigE")
        //             _Cam.Parameters[PLCamera.BlackLevelRaw].SetValuePercentOfRange(value);
        //    }
        //    catch
        //    {
        //        throw;
        //    }
        //}

        public override void SetExposureTime(int value)
        {
            CameraExposureTimePercentageValue = (double)value / 100;
            _ExposureTimeSet = true;
            try
            {
                if (_Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerUsb")
                    _Cam.Parameters[PLCamera.ExposureTime].SetValuePercentOfRange(CameraExposureTimePercentageValue);
                if (_Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerGigE")
                    _Cam.Parameters[PLCamera.ExposureTimeRaw].SetValuePercentOfRange(CameraExposureTimePercentageValue);
            }
            catch
            {
                throw;
            }
        }
        public override int GetWidth()
        {
            return (int)_Cam.Parameters[PLCamera.Width].GetValue();
        }
        public override int GetHeight()
        {
            return (int)_Cam.Parameters[PLCamera.Height].GetValue();
        }

        public override void SetLive(bool bIsLive)
        {
        }

        public override double GetResolution()
        {
            return _Resolution;
        }

        public override bool IniSuccess(ref String sErrorCode)
        {
            if (_ErrorCode.Length > 0)
            {
                sErrorCode = _ErrorCode;
                return false;
            }

            return true;
        }

        public override PropertyInfo GetExposureTimeRange()
        {
            PropertyInfo p = new PropertyInfo
            {
                iMax = 10000,
                iMin = 1,
                iStep = 1,
                iValue = (int)(CameraExposureTimePercentageValue * 100)
            };
            return p;
        }
        public override void OpenPropertyPage()
        {
        }

        public override void Dispose()
        {
            Stop();
        }

    }
}
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
      
        private Emgu.CV.CvEnum.ColorConversion _format;

        private Mat _GrabImage;

        public bool IsOpen { get; private set; }
        public double CameraGainPercentageValue { get; private set; }
        public double CameraGammaPercentageValue { get; private set; }
        public double CameraBlackLevelPercentageValue { get; private set; }
        public double CameraExposureTimePercentageValue { get; private set; }

        public BaslerDef(string serialNum, double fResolution,  Emgu.CV.CvEnum.ColorConversion Format = Emgu.CV.CvEnum.ColorConversion.BayerGb2Rgb)
        {
            try
            {
                _format = Format;
                
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

            _Cam.CameraOpened += Configuration.AcquireContinuous;
            _Cam.Open();
            _GrabImage = new Mat(new Size(GetWidth(), GetHeight()), Emgu.CV.CvEnum.DepthType.Cv8U, 1);

            // Load parameters
            _Cam.Parameters[PLCameraInstance.MaxNumBuffer].SetValue(5);
            _Cam.Parameters[PLCamera.ShutterMode].SetValue(PLCamera.ShutterMode.Rolling);
            //Cam.Parameters[PLCameraInstance.OutputQueueSize].SetValue(3);

            // Start the grabbing of images until grabbing is stopped.

            if (_Cam.CanWaitForFrameTriggerReady)
            {
                _Cam.StreamGrabber.ImageGrabbed += OnImageGrabbed;
                _Cam.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByStreamGrabber);
            }

            if (_Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerUsb")
            {
                CameraGainPercentageValue = _Cam.Parameters[PLCamera.Gain].GetValuePercentOfRange();
                CameraGammaPercentageValue = _Cam.Parameters[PLCamera.Gamma].GetValuePercentOfRange();
                CameraBlackLevelPercentageValue = _Cam.Parameters[PLCamera.BlackLevel].GetValuePercentOfRange();
                CameraExposureTimePercentageValue = _Cam.Parameters[PLCamera.ExposureTime].GetValuePercentOfRange();
            }
            if (_Cam.CameraInfo[CameraInfoKey.DeviceType] == "BaslerGigE")
            {
                _Cam.Parameters[PLCamera.GammaEnable].TrySetValue(true);
                _Cam.Parameters[PLCamera.GevHeartbeatTimeout].SetValue(3000);
                CameraGainPercentageValue = _Cam.Parameters[PLCamera.GainRaw].GetValuePercentOfRange();
                CameraGammaPercentageValue = _Cam.Parameters[PLCamera.Gamma].GetValuePercentOfRange();
                CameraBlackLevelPercentageValue = _Cam.Parameters[PLCamera.BlackLevelRaw].GetValuePercentOfRange();
            }
            // if (DeviceType == "BaslerGigE") Cam.Parameters[PLCamera.ExposureTimeAbs].SetValue(35000, FloatValueCorrection.ClipToRange);
            // if (DeviceType == "BaslerUsb") Cam.Parameters[PLCamera.ExposureTime].SetValue(35000, FloatValueCorrection.ClipToRange);
            IsOpen = true;
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
            GrabEvent.Reset();
            if (!GrabEvent.WaitOne(10000))
                throw new Exception("Grab fail.");
            lock (Lock)
            {
                CvInvoke.CvtColor(_GrabImage, cOutputArray, _format);
            }
        }


        public override void UndistortImage(IInputOutputArray cOutputArray, IInputOutputArray CameraMatrix, IInputOutputArray DistCoeffs)
        {

        }
        //int ImageGrabbedFailTimes = 0;
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

                    // _GrabFailTimes++;
                }
            }
            catch (Exception ex)
            {
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
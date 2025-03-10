using Emgu.CV;
using System;

namespace VisionLibrary.Camera
{
    public abstract class CameraBaseDef : IDisposable
    {

        public struct PropertyInfo
        {
            public int iMax, iMin, iStep, iValue, iDefault;
        }
        public abstract void Dispose();

        /// <summary>
        /// Copy image to input image after grab()
        /// </summary>
        /// <param name="cOutputArray"></param>
        public abstract void CopyImage(IInputOutputArray cOutputArray);

        public abstract void UndistortImage(IInputOutputArray cOutputArray, IInputOutputArray CameraMatrix, IInputOutputArray DistCoeffs);

        public abstract string GetSerialNumberOrMonikerString();

        /// <summary>
        /// Only effective when use cLogitechDef class
        /// </summary>
        /// <param name="bIsLive"></param>
        public abstract void SetLive(bool bIsLive);
        public abstract int GetHeight();
        public abstract int GetWidth();
        //public virtual int GetFucus();
        //public virtual int SetFucus();

        public abstract bool IniSuccess(ref String sErrorCode);

        /// <summary>
        /// Get exposuretime range
        /// </summary>
        /// <returns></returns>
        public abstract PropertyInfo GetExposureTimeRange();

        /// <summary>
        /// Only set according the info from stGetExposureTimeRange() !!
        /// </summary>
        /// <param name="iValue"></param>
        public abstract void SetExposureTime(int iValue);

        /// <summary>
        /// Get x value  (1 pixel = x mm)
        /// </summary>
        /// <returns></returns>
        public abstract double GetResolution();

        public abstract void Restart();

        public abstract bool IsRunning();

        public abstract bool IsAlive();

        public abstract void OpenPropertyPage();
    }
}

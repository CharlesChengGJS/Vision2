using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using VisionLibrary.Camera;

namespace VisionLibrary
{
    public class VideoRecorderDef : IDisposable
    {
        private readonly ManualResetEventSlim _writerReset = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _recordReset = new ManualResetEventSlim(false);

        private VideoCapture _recordCapture;
        private VideoWriter _videoWriter;
        private Thread _writerThread;
        private Thread _PlayThread;
        private PictureBox _PicBox;
        private string _NowRecorderName;
        private string _ViedoName;
        private DateTime _ReplayDateTime;
        private CameraBaseDef _Camera;
        private double _FPS;
        private string _FolderPath;
        private bool _LongTimeMode;

        public VideoRecorderDef(string FolderPath, PictureBox PicBox, CameraBaseDef Camera, string ViedoName, double FPS)
        {
            _FPS = FPS;
            _PicBox = PicBox;
            _Camera = Camera;
            _ViedoName = ViedoName;
            _FolderPath = FolderPath;
            _LongTimeMode = false;

            if (!Directory.Exists(_FolderPath))
                Directory.CreateDirectory(_FolderPath);
        }
        public void Dispose()
        {
            if (_Camera == null)
                return;

            if (_writerThread != null)
            {
                _writerReset.Set();
                _writerThread.Abort();
                _writerThread = null;
                _writerReset.Reset();
            }

            _videoWriter?.Dispose();
            _videoWriter = null;

            StopReplay();
        }

        public void StopReplay()
        {
            if (_Camera == null)
                return;

            if (_PlayThread != null)
            {
                _recordReset.Set();
                _PlayThread.Abort();
                _PlayThread = null;
                _recordReset.Set();
            }
            if (_recordCapture != null)
                _recordCapture.Dispose();
        }
        public void StartReplay(DateTime ReplayDateTime, bool bLongMode)
        {
            if (_Camera == null)
                return;

            _LongTimeMode = bLongMode;
            StopReplay();
            _ReplayDateTime = ReplayDateTime;
            _recordReset.Reset();
            _PlayThread = new Thread(ExcutePlay);
            _PlayThread.Priority = ThreadPriority.BelowNormal;
            _PlayThread.IsBackground = true;
            _PlayThread.Start();
        }
        private void ExcutePlay()
        {
            if (_Camera == null)
                return;

            int iFileNum = 3;
            DateTime before = _ReplayDateTime.AddSeconds(-(_ReplayDateTime.Second % 10) - 20);
            if (_LongTimeMode)
            {
                iFileNum = 6;
                before = _ReplayDateTime;
            }

            for (int i = 0; i < iFileNum; i++)
            {
                string myVideo = _FolderPath + before.ToString("yyyyMMddHHmmss") + "_" + _ViedoName + ".avi";
                _recordCapture = new VideoCapture(myVideo);
                var frame = new Mat(new System.Drawing.Size(_recordCapture.Width, _recordCapture.Height), Emgu.CV.CvEnum.DepthType.Cv8U, 3);
                Mat dst = new Mat();
                var waitTimeBetweenFrames = (int)(1000 / _FPS);
                before = before.AddSeconds(10);
                if (!File.Exists(myVideo) || _NowRecorderName == before.ToString("yyyyMMddHHmmss"))
                    continue;

                while (!_recordReset.Wait(waitTimeBetweenFrames))
                {
                    _recordCapture.Read(frame);

                    if (frame.GetData() != null)
                    {
                        if (_PicBox.Visible)
                        {
                            CvInvoke.Resize(frame, dst, new System.Drawing.Size(_PicBox.Width, _PicBox.Height));
                            _PicBox.Invoke(new Action(() => { _PicBox.CreateGraphics().DrawImageUnscaled(dst.ToBitmap(), 0, 0); }));
                        }
                    }
                    else
                        break;
                }
            }
        }

        public void StopRecord()
        {
            if (_Camera == null)
                return;

            if (_writerThread != null)
            {
                _writerReset.Set();
                _writerThread.Join();
                _writerThread = null;
                _writerReset.Reset();
            }

            _videoWriter?.Dispose();
            _videoWriter = null;
        }
        public void StartRecord()
        {
            if (_Camera == null)
                return;

            _writerThread = new Thread(AddCameraFrameToRecordingThread);
            _writerThread.Priority = ThreadPriority.BelowNormal;
            _writerThread.IsBackground = true;
            _writerThread.Start();
        }
        private void AddCameraFrameToRecordingThread()
        {
            _NowRecorderName = DateTime.Now.ToString("yyyyMMddHHmmss");
            _videoWriter = new VideoWriter(_FolderPath + _NowRecorderName + "_" + _ViedoName + ".avi", VideoWriter.Fourcc('X', 'V', 'I', 'D'), (int)_FPS, new System.Drawing.Size(_Camera.GetWidth(), _Camera.GetHeight()), true);
            _writerReset.Reset();
            var waitTimeBetweenFrames = (int)(1000 / _FPS);
            var frame = new Image<Bgr, byte>(_Camera.GetWidth(), _Camera.GetHeight());
            int minute = DateTime.Now.Minute;
            int second = 0;
            while (!_writerReset.Wait(waitTimeBetweenFrames))
            {
                _Camera.CopyImage(frame);
                _videoWriter.Write(frame.Mat);

                if (Math.Abs(DateTime.Now.Second - second) >= 10 && DateTime.Now.Second % 10 == 0)
                {
                    second = Math.Abs(DateTime.Now.Second);

                    _videoWriter?.Dispose();
                    _videoWriter = null;

                    _videoWriter = new VideoWriter(_FolderPath + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + _ViedoName + ".avi", VideoWriter.Fourcc('X', 'V', 'I', 'D'), (int)_FPS, new System.Drawing.Size(_Camera.GetWidth(), _Camera.GetHeight()), true);
                    _writerReset.Reset();
                }
            }
        }
    }
}
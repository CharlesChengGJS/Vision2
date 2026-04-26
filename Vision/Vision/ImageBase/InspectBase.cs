using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Flann;
using Emgu.CV.Structure;

namespace VisionLibrary
{
    public abstract class InspectBase : IDisposable
    {
        protected string _systemPath;
        protected int _index;
        protected InspectBase(string SystemPath, int Index)
        {
            _systemPath = SystemPath;
            _index = Index;
            ReadSetting();
        }

        public abstract void ReadSetting();
        public virtual PointF[] GetResult() { return null; }

        public virtual bool IsCorrect() { return true; }
        public abstract void Inspect(Image<Bgr, byte> SrcImage);
        public abstract void Dispose();
    }
}

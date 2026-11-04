using System;
using System.Linq;
using Emgu.CV;
using Emgu.CV.Structure;
using FileStreamLibrary;
using System.Drawing;
using System.IO;
using static VisionLibrary.Process;

namespace VisionLibrary
{
    /// <summary>
    /// 找出每個孔內離孔邊緣夠遠的多個座標。
    /// GetResult 與 GetHoleResult 的座標都相對於 SrcImage.ROI，與 FindObjectDef 相同，由呼叫端加回 ROI 位移。
    /// </summary>
    public class FindHoleDef : InspectBase
    {
        /// <summary>取灰階的公式，修改後可用 WriteSetting 存檔。</summary>
        public string Formula { get; set; }
        /// <summary>檢測參數，修改後下次 Inspect 生效，可用 WriteSetting 存檔。</summary>
        public HoleSampleOptions Options { get; set; }
        private PointF[] _points;
        private HoleSampleResult _result;
        private bool _isCorrect;

        public FindHoleDef(string SystemPath, int Index) : base(SystemPath, Index)
        {
            _points = new PointF[0];
            _isCorrect = false;
        }

        public override void ReadSetting()
        {
            IniFile ini = new IniFile(_systemPath + "FindHole.ini", true);

            string section = "System" + _index.ToString();
            Formula = ini.ReadStr(section, "Formula", "G");
            Options = new HoleSampleOptions
            {
                Margin = ini.ReadDouble(section, "Margin", 25),
                Spacing = ini.ReadInt(section, "Spacing", 10),
                HoleIsBright = ini.ReadBool(section, "WhiteClass", true),
                UseOtsu = ini.ReadBool(section, "UseOtsu", false),
                Threshold = ini.ReadInt(section, "Threshold", 180),
                BlurSize = ini.ReadInt(section, "BlurSize", 3),
                MorphSize = ini.ReadInt(section, "MorphSize", 3),
                MinArea = ini.ReadInt(section, "MinArea", 300),
                MaxArea = ini.ReadInt(section, "MaxArea", 10000),
                PeakLevel = ini.ReadDouble(section, "PeakLevel", 0.85),
                SeparationRatio = ini.ReadDouble(section, "SeparationRatio", 0.8),
                MaxForegroundRatio = ini.ReadDouble(section, "MaxForegroundRatio", 0.5),
                IsScale = ini.ReadBool(section, "Scale", true),
            };
            ini.FileClose();
            ini.Dispose();
        }

        /// <summary>把目前的設定寫回 FindHole.ini，同一檔案內其他 Index 的區段會保留。</summary>
        public void WriteSetting()
        {
            // IniFile 在目錄不存在時不會寫入，也不會丟出例外。
            if (!Directory.Exists(_systemPath))
                Directory.CreateDirectory(_systemPath);

            IniFile ini = new IniFile(_systemPath + "FindHole.ini", false);

            string section = "System" + _index.ToString();
            ini.WriteStr(section, "Formula", Formula);
            ini.WriteDouble(section, "Margin", Options.Margin);
            ini.WriteInt(section, "Spacing", Options.Spacing);
            ini.WriteBool(section, "WhiteClass", Options.HoleIsBright);
            ini.WriteBool(section, "UseOtsu", Options.UseOtsu);
            ini.WriteInt(section, "Threshold", Options.Threshold);
            ini.WriteInt(section, "BlurSize", Options.BlurSize);
            ini.WriteInt(section, "MorphSize", Options.MorphSize);
            ini.WriteInt(section, "MinArea", Options.MinArea);
            ini.WriteInt(section, "MaxArea", Options.MaxArea);
            ini.WriteDouble(section, "PeakLevel", Options.PeakLevel);
            ini.WriteDouble(section, "SeparationRatio", Options.SeparationRatio);
            ini.WriteDouble(section, "MaxForegroundRatio", Options.MaxForegroundRatio);
            ini.WriteBool(section, "Scale", Options.IsScale);
            ini.FileClose();
            ini.Dispose();
        }

        /// <summary>只回傳符合邊距的座標；未達邊距的最深點請由 GetHoleResult 取得。</summary>
        public override PointF[] GetResult()
        {
            return _points;
        }

        /// <summary>至少有一個符合邊距的座標。</summary>
        public override bool IsCorrect()
        {
            return _isCorrect;
        }

        /// <summary>完整結果，含區塊、孔心與各座標的距離。尚未檢測或檢測失敗時為 null。</summary>
        public HoleSampleResult GetHoleResult()
        {
            return _result;
        }

        public override void Inspect(Image<Bgr, byte> SrcImage)
        {
            // 先清除，檢測失敗時才不會留下前一張影像的座標。
            _points = new PointF[0];
            _result = null;
            _isCorrect = false;

            using (Image<Gray, byte> imageResult = ImageArithmetic(SrcImage, Formula))
            {
                _result = HoleSampler.Detect(imageResult.Mat, Options);
            }

            _points = _result.Points.Where(p => p.MeetsMargin).Select(p => new PointF(p.Location.X, p.Location.Y)).ToArray();
            _isCorrect = _points.Length > 0;
        }

        public override void Dispose()
        {

        }
    }


}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Flann;
using Emgu.CV.Structure;
using FileStreamLibrary;
using System.Drawing;
using System.IO;
using static VisionLibrary.Process;

namespace VisionLibrary
{
    public class MatchObjectDef : InpectBase
    {

        private Image<Gray, byte> _model;
        private double _score;
        private string _formula;
        private PointF[] _points;
        private float _scale = 1;
        public MatchObjectDef(string SystemPath, int Index) : base(SystemPath, Index)
        {
            _points = new PointF[0];
            //_model = null;


        }

        public override void ReadSetting()
        {
            IniFile ini = new IniFile(_systemPath + "Match.ini", true);

            string section = "System" + _index.ToString();
            _formula = ini.ReadStr(section, "Formula", "B-G");
            _score = ini.ReadFloat(section, "Score", (float)0.8);
            _scale = ini.ReadFloat(section, "Scale", 1);
            string fileName = _systemPath + "MatchModel" + _index.ToString() + ".bmp";
            if (File.Exists(fileName))
            {
                _model = new Image<Gray, byte>(fileName);
            }
            else
                _model = null;

            ini.FileClose();
            ini.Dispose();
        }

        public override PointF[] GetResult()
        {
            return (PointF[])_points.Clone();
        }

        public override void Dispose()
        {
            if (_model != null)
            {
                _model.Dispose();
                _model = null;
            }
        }
        public Image<Gray, byte> GetModel()
        {
            return _model;
        }

        public void LearnModel(Image<Bgr, byte> Src)
        {
            
            Image<Gray, byte> imageResult = ImageArithmetic(Src, _formula);
            //_model.Dispose();
            _model = new Image<Gray, byte>(imageResult.Width, imageResult.Height);
            imageResult.CopyTo(_model);
            if (_scale != 1)
            {
                _model = _model.Resize(_scale, Inter.Linear);
            }
            _model._SmoothGaussian(5);
            
        }

        public void SaveModel()
        {
            string fileName = _systemPath + "MatchModel" + _index.ToString() + ".bmp";
            _model.Save(fileName);
        }

        private double MinMaxLocSubPix(Point LocationIn, Mat HeatMap, ref PointF SubPixLocation)
        {

            // set default result in case we bail
            SubPixLocation.X = (float)LocationIn.X;
            SubPixLocation.Y = (float)LocationIn.Y;


            // At this time we don't have anything other than Parabola math so we can ignore "Method".

            { // Parabola math	

                /*
                    The values returned from MatchTemplate are not linear past the point where it just starts to drop. 
                    The reason is that we can not assume that the template is the same on all sides. Imagine a sloped edge on one side and a sharp edge on the other.			

                    We can also get several values at the top that are all the same since the source and the template are integers.

                    We also have to protect against the situation where the source is a solid white or solid black. The result is a constant value heat map.
                */

                // pick some limiting values
                // It's not expected that the template peak values will span more than 1/16 of the source size. This also limits the processing time used when looking at a blank image.
                Size MaxScan = new Size
                {
                    Width = HeatMap.Cols >> 4,
                    Height = HeatMap.Rows >> 4
                };


                Point scanRectMin = new Point();  // I used two Points instead of a Rect to prevent having Rect compute right/left values in each loop below
                Point scanRectMax = new Point();
                scanRectMin.X = LocationIn.X - MaxScan.Width; if (scanRectMin.X < 0) scanRectMin.X = 0;
                scanRectMin.Y = LocationIn.Y - MaxScan.Height; if (scanRectMin.Y < 0) scanRectMin.Y = 0;
                scanRectMax.X = LocationIn.X + MaxScan.Width; if (scanRectMax.X >= HeatMap.Cols) scanRectMax.X = HeatMap.Cols - 1;
                scanRectMax.Y = LocationIn.Y + MaxScan.Height; if (scanRectMax.Y >= HeatMap.Rows) scanRectMax.Y = HeatMap.Rows - 1;

                float FLT_EPSILON = 1.192092896e-07F;       /* smallest such that 1.0+FLT_EPSILON != 1.0 */
                // were we are starting at
                float FloatValueChange = FLT_EPSILON * 10.0f; // smallest change that we can do math on with some meaningful result.

                // scan to find area to use. this can get complicated since we may be given a point near any of the edges of the blob we want to use.		
                float srcStartingPoint = HeatMap.GetFloatValue(LocationIn.Y, LocationIn.X);

                Point Center = LocationIn;

                // results
                Point ScanRight;
                Point ScanLeft;
                Point ScanUp;
                Point ScanDown;

                //for (int rescan = 0; rescan < 2; ++rescan){

                ScanRight = Center;
                while (true)
                {
                    ++ScanRight.X; // no point checking the passed location. so inc first
                    if (ScanRight.X > scanRectMax.X)
                    {
                        //					_ASSERT(0);
                        return 1; // ran out of room to scan
                    };
                    float Val = HeatMap.GetFloatValue(ScanRight.Y, ScanRight.X);
                    if (Math.Abs(Val - srcStartingPoint) > FloatValueChange)
                    {
                        break;
                    };
                };

                ScanLeft = Center;
                while (true)
                {
                    --ScanLeft.X; // no point checking the passed location. so inc first
                    if (ScanLeft.X < scanRectMin.X)
                    {
                        //					_ASSERT(0);
                        return 1; // ran out of room to scan
                    };
                    if (Math.Abs(HeatMap.GetFloatValue(ScanLeft.Y, ScanLeft.X) - srcStartingPoint) > FloatValueChange)
                    {
                        break;
                    };
                };

                ScanUp = Center;
                while (true)
                {
                    ++ScanUp.Y; // assume G cords. The actual direction of Up in the image is not important since the math is symmetrical
                    if (ScanUp.Y > scanRectMax.Y)
                    {
                        //					_ASSERT(0);
                        return 1; // ran out of room to scan
                    };
                    if (Math.Abs(HeatMap.GetFloatValue(ScanUp.Y, ScanUp.X) - srcStartingPoint) > FloatValueChange)
                    {
                        break;
                    };
                };

                ScanDown = Center;
                while (true)
                {
                    --ScanDown.Y; // assume G cords. The actual direction of Up in the image is not important since the math is symmetrical
                    if (ScanDown.Y < scanRectMin.Y)
                    {
                        //					_ASSERT(0);
                        return 1; // ran out of room to scan
                    };
                    if (Math.Abs(HeatMap.GetFloatValue(ScanDown.Y, ScanDown.X) - srcStartingPoint) > FloatValueChange)
                    {
                        break;
                    };
                };

                // At this point we have a good starting point on the blob area, but our initial scan may be stuck on one side so center and rescan once more

                //Center.x =  ((ScanRight.x - ScanLeft.x) >> 1) + ScanLeft.x;
                //Center.y =  ((ScanUp.y    - ScanDown.y) >> 1) + ScanDown.y;

                // did center change?
                //if ((Center.x == LocIn->x) && (Center.y == LocIn->y)) break; // done early

                //}; // for rescan

                // measure polarity if needed



                // At this point we have a center of a blob with some extents to use

                // for each axis we now do a triangulation math.


                // imagine the match numbers as height and the pixel numbers as horizontal.

                //B is highest, A and C are on the sides


                double ErrorVal = 0;

                {// X axis

                    PointF A = new PointF
                    {
                        X = ScanLeft.X, // The pixel cords
                        Y = HeatMap.GetFloatValue(ScanLeft.Y, ScanLeft.X) // the Heat map value
                    };

                    PointF B = new PointF
                    {
                        X = Center.X, // The pixel cords
                        Y = HeatMap.GetFloatValue(Center.Y, Center.X) // the Heat map value
                    }; // center

                    PointF C = new PointF
                    {
                        X = ScanRight.X, // The pixel cords
                        Y = HeatMap.GetFloatValue(ScanRight.Y, ScanRight.X) // the Heat map value
                    };

                    PointF Result = new PointF();
                    SubPixFitParabola(ref Result, A, B, C);
                    // we throw away the y and use the x

                    // clip and set error
                    if (Result.X < ScanLeft.X)
                    {

                        Result.X = ScanLeft.X;
                        ErrorVal = 1;
                    };
                    if (Result.X > ScanRight.X)
                    {

                        Result.X = ScanRight.X;
                        ErrorVal = 1;
                    };
                    SubPixLocation.X = Result.X;
                }; // X axis



                {// Y axis

                    // this time we swap x and y since the parabola is always found in the x
                    PointF A = new PointF
                    {
                        X = ScanDown.Y, // The pixel cords
                        Y = HeatMap.GetFloatValue(ScanDown.Y, ScanDown.X)// the Heat map value
                    };

                    PointF B = new PointF
                    {
                        X = Center.Y, // The pixel cords
                        Y = HeatMap.GetFloatValue(Center.Y, Center.X) // the Heat map value
                    }; // center

                    PointF C = new PointF
                    {
                        X = ScanUp.Y, // The pixel cords
                        Y = HeatMap.GetFloatValue(ScanUp.Y, ScanUp.X) // the Heat map value
                    };

                    PointF Result = new PointF();
                    SubPixFitParabola(ref Result, A, B, C);
                    // we throw away the y and use the x
                    Result.Y = Result.X;

                    // clip and set error
                    if (Result.Y < ScanDown.Y)
                    {

                        Result.Y = ScanDown.Y;
                        ErrorVal = 1;
                    };
                    if (Result.Y > ScanUp.Y)
                    {
                        Result.Y = ScanUp.Y;
                        ErrorVal = 1;
                    };
                    SubPixLocation.Y = Result.Y;
                }; // X axis


                return ErrorVal;


            }; // Bill's Tilt math



        }

        // Parabolic fit
        private void SubPixFitParabola(ref PointF Result, PointF P1, PointF P2, PointF P3)
        {/*
            Parabola fit and resulting peak

            The parabola is aligned along the X axis with the peak being in the Y.

        in
	        P1 = a point on one side
	        P2 = the center point
	        P3 = a point on the other side
        out
	        Result = the peak point in the center of the parabola
        */

            Result.X = P2.X; // default in case of an error
            Result.Y = P2.Y;


            /* from http://stackoverflow.com/questions/717762/how-to-calculate-the-vertex-of-a-parabola-given-three-points
                This is really just a simple linear algebra problem, so you can do the calculation symbolically. When you substitute in the x and y values of your three points, you'll get three linear equations in three unknowns.

                A x1^2 + B x1 + C = y1
                A x2^2 + B x2 + C = y2
                A x3^2 + B x3 + C = y3

                The straightforward way to solve this is to invert the matrix

                x1^2  x1  1
                x2^2  x2  1
                x3^2  x2  1

                and multiply it by the vector

                y1
                y2
                y3

                The result of this is... okay, not exactly all that simple ;-) I did it in Mathematica, and here are the formulas in pseudocode:
            */

            float denom = (P1.X - P2.X) * (P1.X - P3.X) * (P2.X - P3.X); // can't be zero since X is from piXel locations.
            float A = (P3.X * (P2.Y - P1.Y) + P2.X * (P1.Y - P3.Y) + P1.X * (P3.Y - P2.Y)) / denom;
            float B = ((P3.X * P3.X) * (P1.Y - P2.Y) + (P2.X * P2.X) * (P3.Y - P1.Y) + (P1.X * P1.X) * (P2.Y - P3.Y)) / denom;
            float C = (P2.X * P3.X * (P2.X - P3.X) * P1.Y + P3.X * P1.X * (P3.X - P1.X) * P2.Y + P1.X * P2.X * (P1.X - P2.X) * P3.Y) / denom;



            // Y = A * X^2 + B * x + C 

            //now find the center

            float xv = -B / (2 * A);
            float yv = C - (B * B) / (4 * A);


            Result.X = xv;
            Result.Y = yv;
        }


        public override void Inspect(Image<Bgr, byte> SrcImage)
        {
            Image<Gray, byte> imageResult = ImageArithmetic(SrcImage, _formula);
            _points = Match(imageResult);
        }


        public PointF[] Match(Image<Gray, byte> cSrcImage)
        {
            Image<Gray, float> ret;
            if (_scale == 1)
            { 
                ret = cSrcImage.MatchTemplate(_model, TemplateMatchingType.CcoeffNormed);
            }
            else
            {
                ret = cSrcImage.Resize(_scale, Inter.Linear).MatchTemplate(_model, TemplateMatchingType.CcoeffNormed);
            }
            double fMin = 0, fMax = 0;
            Point stMaxp = new Point(0, 0);
            Point stMinp = new Point(0, 0);
            CvInvoke.MinMaxLoc(ret, ref fMin, ref fMax, ref stMinp, ref stMaxp);
            PointF[] result;
            if (fMax < _score)
            { 
                return new PointF[0];
            }
            else
            {
                result = new PointF[1];
                MinMaxLocSubPix(stMaxp, ret.Mat, ref result[0]);
                result[0].X += _model.Width / 2;
                result[0].Y += _model.Height / 2;

                result[0].X = result[0].X / _scale;
                result[0].Y = result[0].Y / _scale;

                ret.Dispose();
            }

            return result;
        }
    }
}

using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Flee.PublicTypes;
using System.Runtime.InteropServices;
using Emgu.CV.CvEnum;
using System.IO;
using Emgu.CV.Util;

namespace VisionLibrary
{
    public static class Process
    {
        public enum CornerSide
        {
            LeftTop,
            LeftBottom,
            RightTop,
            RightBottom
        }
        public static bool EnableScratchProcessFile = false;
        public static PointF[] GetObjectCenter2(Image<Gray, byte> Src, int Threshold, int MinArea, int MaxArea, float ratio, out Rectangle[] Rectangles)
        {
            List<double[]> centers = new List<double[]>();
            List<Rectangle> rectangles = new List<Rectangle>();
            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                // 在這版本請使用FindContours，早期版本有cvFindContours等等，在這版都無法使用，
                // 由於這邊是要取得最外層的輪廓，所以第三個參數給 null，第四個參數則用 RetrType.External。
                Mat threshold = Src.ThresholdBinary(new Gray(Threshold), new Gray(255)).Mat;
                CvInvoke.FindContours(threshold, contours, null, RetrType.List, ChainApproxMethod.ChainApproxNone);

                int count = contours.Size;
                VectorOfPoint GetContour = new VectorOfPoint();
                for (int i = 0; i < count; i++)
                {
                    using (VectorOfPoint contour = contours[i])
                    {
                        double area = CvInvoke.ContourArea(contour);
                        if (area < MaxArea && area > MinArea)
                        {
                            area = CvInvoke.ContourArea(contour);
                            Moments m = CvInvoke.Moments(contour, true);

                            PointF center = new PointF((float)(m.M10 / m.M00), (float)(m.M01 / m.M00));
                      //      if ((Src.Data[contour[0].Y, contour[0].X, 0] > Threshold) == WhiteClass)
                            {
                                double[] data = { area, center.X, center.Y };
                                centers.Add(data);
                                rectangles.Add(CvInvoke.BoundingRectangle(contour));
                            }
                        }
                    }
                }
            }

            for(int i = 0; i < centers.Count; i++)
            {
                float w = rectangles[i].Width;
                float h = rectangles[i].Height;
                float r = w / h;
                if (w > h)
                    r = h / w;

                if(ratio > r)
                {
                    centers.RemoveAt(i);
                    rectangles.RemoveAt(i);
                    i--;
                }
            }

           // double[][] orderData = centers.OrderBy(x => x[0]).ToArray();
            PointF[] result = new PointF[centers.Count];

            for (int i = 0; i < result.Length; i++)
            {
                result[i].X = (float)centers[i][1];
                result[i].Y = (float)centers[i][2];
            }

            Rectangles = rectangles.ToArray();
            return result;
        }

        public static PointF[] GetObjectCenter2(Image<Gray, byte> Src, int Threshold, int MinArea, int MaxArea, float W_H_Ratio, out Rectangle[] Rectangles, bool WhiteClass)
        {
            List<double[]> centers = new List<double[]>();
            List<Rectangle> rectangles = new List<Rectangle>();
            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                // 在這版本請使用FindContours，早期版本有cvFindContours等等，在這版都無法使用，
                // 由於這邊是要取得最外層的輪廓，所以第三個參數給 null，第四個參數則用 RetrType.External。
                Mat threshold = Src.ThresholdBinary(new Gray(Threshold), new Gray(255)).Mat;
                CvInvoke.FindContours(threshold, contours, null, RetrType.List, ChainApproxMethod.ChainApproxNone);

                int count = contours.Size;
                VectorOfPoint GetContour = new VectorOfPoint();
                for (int i = 0; i < count; i++)
                {
                    using (VectorOfPoint contour = contours[i])
                    {

                        

                        // 1. 濾除面積過小或過大的輪廓
                        double area = CvInvoke.ContourArea(contour);
                        if (area < MaxArea && area > MinArea)
                        {
                            //  判斷是否為白色物件
                            //Mat mask = new Mat(threshold.Size, DepthType.Cv8U, 1);
                            //mask.SetTo(new MCvScalar(0));

                            //// 將對應輪廓填成白色
                            //CvInvoke.DrawContours(mask, contours, i, new MCvScalar(255), -1);  // -1 表示填滿

                            //// 3. 計算該輪廓區域的平均灰階
                            //MCvScalar mean = CvInvoke.Mean(threshold, mask);
                            //if (mean.V0 > 128 != WhiteClass)
                            //    continue;

                            // 2. 計算輪廓的質心
                            Moments m = CvInvoke.Moments(contour, true);

                            PointF center = new PointF((float)(m.M10 / m.M00), (float)(m.M01 / m.M00));
                            {
                                double[] data = { area, center.X, center.Y };
                                centers.Add(data);
                                rectangles.Add(CvInvoke.BoundingRectangle(contour));
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < centers.Count; i++)
            {
                float w = rectangles[i].Width;
                float h = rectangles[i].Height;
                float r = w / h;
                if (w > h)
                    r = h / w;

                if (W_H_Ratio > r)
                {
                    centers.RemoveAt(i);
                    rectangles.RemoveAt(i);
                    i--;
                }
            }

            // double[][] orderData = centers.OrderBy(x => x[0]).ToArray();
            PointF[] result = new PointF[centers.Count];

            for (int i = 0; i < result.Length; i++)
            {
                result[i].X = (float)centers[i][1];
                result[i].Y = (float)centers[i][2];
            }

            Rectangles = rectangles.ToArray();
            return result;
        }
        public static PointF[] GetObjectCenter(Image<Gray, byte> Src, int Threshold, int MinArea, int MaxArea, bool WhiteClass)
        {
            List<double[]> centers = new List<double[]>();
            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                // 在這版本請使用FindContours，早期版本有cvFindContours等等，在這版都無法使用，
                // 由於這邊是要取得最外層的輪廓，所以第三個參數給 null，第四個參數則用 RetrType.External。
                Mat threshold = Src.ThresholdBinary(new Gray(Threshold), new Gray(255)).Mat;
                CvInvoke.FindContours(threshold, contours, null, RetrType.List, ChainApproxMethod.ChainApproxNone);

                int count = contours.Size;
                VectorOfPoint GetContour = new VectorOfPoint();
                for (int i = 0; i < count; i++)
                {
                    using (VectorOfPoint contour = contours[i])
                    {
                        double area = CvInvoke.ContourArea(contour);
                        if (area < MaxArea && area > MinArea)
                        {
                            Moments m = CvInvoke.Moments(contour, true);

                            PointF center = new PointF((float)(m.M10 / m.M00), (float)(m.M01 / m.M00));
                            double[] data = { area, center.X, center.Y };
                            centers.Add(data);

                            //area = CvInvoke.ContourArea(contour);
                            //Moments m = CvInvoke.Moments(contour, true);

                            //PointF center = new PointF((float)(m.M10 / m.M00), (float)(m.M01 / m.M00));

                            //Mat mask = new Mat(Src.Size, DepthType.Cv8U, 1);
                            //CvInvoke.FillPoly(mask, contours[i], new MCvScalar(255));
                            //double average_value = CvInvoke.Mean(threshold, mask).V0;
                            //if(WhiteClass && average_value >= 254)
                            //{
                            //    double[] data = { area, center.X, center.Y };
                            //    centers.Add(data);
                            //}
                            //if(!WhiteClass && average_value < 254)
                            //{
                            //    double[] data = { area, center.X, center.Y };
                            //    centers.Add(data);
                            //}
                        }
                    }
                }
            }
            
            double[][] orderData = centers.OrderBy(x => x[0]).ToArray();
            PointF[] result = new PointF[orderData.Length];

            for (int i = 0; i < result.Length; i++)
            {
                result[i].X = (float)orderData[i][1];
                result[i].Y = (float)orderData[i][2];
            }

            return result;
        }

        /// <summary>
        /// src = 來源影像, Threshold = 灰階門檻, MinArea = 物件最小面積, MaxArea = 物件最大面積,  WhiteClass = 是否為白色物件 回傳物件中心
        /// </summary>
        public static PointF[] GetObjectCenter(Image<Gray, byte> Src, int Threshold, int MinArea, int MaxArea, bool WhiteClass, out Rectangle[] Rectangles)
        {
            List<PointF> centers = new List<PointF>();
            List<Rectangle> rectangles = new List<Rectangle>();
            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                // 在這版本請使用FindContours，早期版本有cvFindContours等等，在這版都無法使用，
                // 由於這邊是要取得最外層的輪廓，所以第三個參數給 null，第四個參數則用 RetrType.External。
                Mat threshold = Src.ThresholdBinary(new Gray(Threshold), new Gray(255)).Mat;
                CvInvoke.FindContours(threshold, contours, null, RetrType.List, ChainApproxMethod.ChainApproxNone);

                int count = contours.Size;
                VectorOfPoint GetContour = new VectorOfPoint();
                for (int i = 0; i < count; i++)
                {
                    using (VectorOfPoint contour = contours[i])
                    {
                        double area = CvInvoke.ContourArea(contour);
                        if (area < MaxArea && area > MinArea)
                        {
                            area = CvInvoke.ContourArea(contour);
                            Moments m = CvInvoke.Moments(contour, true);

                            PointF center = new PointF((float)(m.M10 / m.M00), (float)(m.M01 / m.M00));

                            Mat mask = new Mat(Src.Size, DepthType.Cv8U, 1);
                            CvInvoke.FillPoly(mask, contours[i], new MCvScalar(255));
                            double average_value = CvInvoke.Mean(threshold, mask).V0;
                            if (WhiteClass && average_value >= 254)
                            {
                                centers.Add(center);
                                rectangles.Add(CvInvoke.BoundingRectangle(contour));
                            }
                            if (!WhiteClass && average_value < 254)
                            {
                                centers.Add(center);
                                rectangles.Add(CvInvoke.BoundingRectangle(contour));
                            }
                        }
                    }
                }
            }
            Rectangles = rectangles.ToArray();
            return centers.ToArray();
        }

        public static void BlobSearch(Image<Gray, byte> sample, int minThreshold, int minArea, int maxArea, float minCircularity, out PointF[] position, out float[] diameter)
        {
            SimpleBlobDetectorParams blobparams = new SimpleBlobDetectorParams
            {
                FilterByArea = true,
                MinArea = minArea,
                MaxArea = maxArea,
                MinThreshold = minThreshold,
                MaxThreshold = 255,
                FilterByCircularity = true,        //default
                MinCircularity = minCircularity,
                MaxCircularity = 1,
                FilterByConvexity = false,
                MinConvexity = (float)0.2,
                MaxConvexity = 1,
                FilterByInertia = true,
                MinInertiaRatio = (float)0.4,
                MaxInertiaRatio = 1,
                FilterByColor = false,
                ThresholdStep = 2,
                MinRepeatability = new IntPtr(2)
            };
            SimpleBlobDetector detector = new SimpleBlobDetector(blobparams);
            MKeyPoint[] keypoints = detector.Detect(sample);
            detector.Dispose();
            blobparams.Dispose();
            diameter = new float[keypoints.Length];
            position = new PointF[keypoints.Length];

            for (int i = 0; i < keypoints.Length; i++)
            {
                diameter[i] = keypoints[i].Size;
                position[i] = keypoints[i].Point;
            }
            GC.Collect();
        }

        public static float GetFloatValue(this Mat mat, int row, int col)
        {
            var value = new float[1];
            Marshal.Copy(mat.DataPointer + (row * mat.Cols + col) * mat.ElementSize, value, 0, 1);
            return value[0];
        }

        public static bool GetAutoMatchModel(Image<Gray, byte> cImageSrc, ref Rectangle stROI1)
        {
            ScratchProcessFile("AutoMatch.bmp", cImageSrc.Mat);
            //cImageSrc = 255 - cImageSrc;
            //_saveImg.SaveImg(EImgDirPath.AutoMatch, cImageSrc, 60, "CImageSrcInv");

            PointF[] pos;
            float[] size;

            VisionLibrary.Process.BlobSearch(cImageSrc, 150, 15, 10000, (float)0.1, out pos, out size);

            //HarrisDetectorDef cHarrisDetector = new HarrisDetectorDef();
            //cHarrisDetector.Detect(cImageSrc);

            //List<Point> featurePoints = new List<Point>();
            //cHarrisDetector.GetCorners(featurePoints, 0.01);
            //cHarrisDetector.Dispose();
            List<Point> featurePoints = new List<Point>();
            for (int i = 0; i < pos.Length; i++)
            {
                Point lTop = new Point((int)(pos[i].X - size[i] / 2), (int)(pos[i].Y - size[i] / 2));
                Point lDown = new Point((int)(pos[i].X - size[i] / 2), (int)(pos[i].Y + size[i] / 2));
                Point rTop = new Point((int)(pos[i].X + size[i] / 2), (int)(pos[i].Y - size[i] / 2));
                Point rDown = new Point((int)(pos[i].X + size[i] / 2), (int)(pos[i].Y + size[i] / 2));

                if (lTop.X > 0 && lTop.Y > 0 && lTop.X < cImageSrc.Width && lTop.Y < cImageSrc.Height)
                    featurePoints.Add(lTop);
                if (lDown.X > 0 && lDown.Y > 0 && lDown.X < cImageSrc.Width && lDown.Y < cImageSrc.Height)
                    featurePoints.Add(lDown);
                if (rTop.X > 0 && rTop.Y > 0 && rTop.X < cImageSrc.Width && rTop.Y < cImageSrc.Height)
                    featurePoints.Add(rTop);
                if (rDown.X > 0 && rDown.Y > 0 && rDown.X < cImageSrc.Width && rDown.Y < cImageSrc.Height)
                    featurePoints.Add(rDown);
            }

            List<Rectangle> stResultRect = new List<Rectangle>();
            GetGroupingRectangle(featurePoints, stResultRect, 200, 200);
            List<Rectangle> stResultRect2 = new List<Rectangle>();
            GetGroupingRectangle(featurePoints, stResultRect2, 350, 350);

            //List<Rectangle> stResultRect = new List<Rectangle>();
            //cHarrisDetector.GetGroupingRectangle(featurePoints, stResultRect, 60, 60);
            //List<Rectangle> stResultRect2 = new List<Rectangle>();
            //cHarrisDetector.GetGroupingRectangle(featurePoints, stResultRect2, 120, 120);

            double fResultScore = 0;

            double fMinScore = 1;
            int nMinIndex = -1;
            List<Rectangle> stRectResultList = new List<Rectangle>();
            for (int i = 0; i < stResultRect.Count; i++)
            {
                if (stResultRect[i].Width < 15 || stResultRect[i].Height < 15)
                    continue;

                MatchDef cMatch = new MatchDef();
                Rectangle temp = cImageSrc.ROI;
                cImageSrc.ROI = stResultRect[i];

                cMatch.SetMatchModel(cImageSrc, stResultRect[i].Location);

                cImageSrc.ROI = temp;

                if (cMatch.MatchTest(cImageSrc, 0.65, ref fResultScore))
                {
                    if (fResultScore < fMinScore)
                    {
                        fMinScore = fResultScore;
                        nMinIndex = i;
                    }
                    stRectResultList.Add(stResultRect[i]);
                }

                cMatch.Dispose();
            }

            stROI1 = Rectangle.Empty;

            if (nMinIndex >= 0 && stRectResultList.Count > 0)
            {
                int nCX = stResultRect[nMinIndex].X + stResultRect[nMinIndex].Width / 2;
                int nCY = stResultRect[nMinIndex].Y + stResultRect[nMinIndex].Height / 2;

                stROI1 = stResultRect[nMinIndex];
                return true;

            }

            nMinIndex = -1;
            fMinScore = 1;
            List<Rectangle> stRectResultList2 = new List<Rectangle>();
            for (int i = 0; i < stResultRect2.Count; i++)
            {
                if (stResultRect2[i].Width < 30 || stResultRect2[i].Height < 30)
                    continue;

                MatchDef cMatch = new MatchDef();
                Rectangle temp = cImageSrc.ROI;
                cImageSrc.ROI = stResultRect2[i];

                cMatch.SetMatchModel(cImageSrc, stResultRect2[i].Location);

                cImageSrc.ROI = temp;
                if (cMatch.MatchTest(cImageSrc, 0.75, ref fResultScore))
                {
                    if (fResultScore < fMinScore)
                    {
                        fMinScore = fResultScore;
                        nMinIndex = i;
                    }
                    stRectResultList2.Add(stResultRect2[i]);
                }

                cMatch.Dispose();
            }

            if (stROI1.IsEmpty)
            {
                if (nMinIndex >= 0)
                {
                    stROI1 = stResultRect2[nMinIndex];
                    return true;
                }
                else
                    return false;
            }

            return false;
        }

        public static void GetGroupingRectangle(List<Point> cornerPoints, List<Rectangle> stRectList, int nMaxW, int nMaxH)
        {
            stRectList.Clear();
            for (int i = 0; i < cornerPoints.Count; i++)
            {
                bool bInside = false; ;
                for (int j = 0; j < stRectList.Count; j++)
                {
                    if (PointInside(cornerPoints[i], stRectList[j], 3))
                    {
                        Rectangle stNewRect = new Rectangle();
                        if (ResizeRect(cornerPoints[i], stRectList[j], 3, ref stNewRect, nMaxW, nMaxH))
                        {
                            bInside = true;
                            stRectList[j] = stNewRect;
                            break;
                        }
                    }
                }
                if (!bInside)
                {
                    stRectList.Add(new Rectangle(cornerPoints[i].X - 3, cornerPoints[i].Y - 3, 6, 6));
                }
            }
        }

        private static bool PointInside(Point stPoint, Rectangle stRect, int nRange)
        {
            if (stPoint.X > stRect.X - nRange
                && stPoint.X < stRect.X + stRect.Width + nRange
                && stPoint.Y > stRect.Y - nRange
                && stPoint.Y < stRect.Y + stRect.Height + nRange)
                return true;

            return false;
        }

        private static bool ResizeRect(Point stPoint, Rectangle stRect, int nHalfWidth, ref Rectangle stNewRect, int nMaxW, int nMaxH)
        {
            stNewRect = stRect;
            if (stNewRect.X > stPoint.X - nHalfWidth)
                stNewRect.X = stPoint.X - nHalfWidth;
            if (stNewRect.Y > stPoint.Y - nHalfWidth)
                stNewRect.Y = stPoint.Y - nHalfWidth;
            if (stNewRect.X + stNewRect.Width < stPoint.X + nHalfWidth)
                stNewRect.Width = stPoint.X + nHalfWidth - stNewRect.X;
            if (stNewRect.Y + stNewRect.Height < stPoint.Y + nHalfWidth)
                stNewRect.Height = stPoint.Y + nHalfWidth - stNewRect.Y;

            if (stNewRect.Width > nMaxW || stNewRect.Height > nMaxH)
                return false;
            return true;
        }

        public static Image<Gray, byte> ImageArithmetic(Image<Bgr, byte> input, string formula)
        {
            if (formula == "" || input.NumberOfChannels < 3)
            {
                return null;
            }
            // Define the context of our expression
            ExpressionContext context = new ExpressionContext();
            // Allow the expression to use all static public methods of System.Math
            context.Imports.AddType(typeof(Math));
            Image<Gray, byte>[] bgr = input.Split();
            // Define an int variable
            context.Variables["B"] = context.Variables["b"] = bgr[0];
            context.Variables["G"] = context.Variables["g"] = bgr[1];
            context.Variables["R"] = context.Variables["r"] = bgr[2];

            // Create a dynamic expression that evaluates to an Object
            IDynamicExpression eDynamic = context.CompileDynamic(formula);
            // Evaluate the expressions
            Image<Gray, byte> result = (Image<Gray, byte>)eDynamic.Evaluate();
            ScratchProcessFile("ImageArithmetic.bmp", result.Mat);
            return result;

        }
        public static void ImageArithmetic(Mat input, string formula, out Mat result)
        {
            if (formula == "" || input.NumberOfChannels < 3)
            {
                result = input;
                return;
            }
            // Define the context of our expression
            ExpressionContext context = new ExpressionContext();
            // Allow the expression to use all static public methods of System.Math
            context.Imports.AddType(typeof(Math));

            Mat[] bgr = ((Mat)input).Split();
            // Define an int variable
            context.Variables["B"] = context.Variables["b"] = bgr[0];
            context.Variables["G"] = context.Variables["g"] = bgr[1];
            context.Variables["R"] = context.Variables["r"] = bgr[2];

            // Create a dynamic expression that evaluates to an Object
            IDynamicExpression eDynamic = context.CompileDynamic(formula);

            // Evaluate the expressions
            result = (Mat)eDynamic.Evaluate();
            ScratchProcessFile("ImageArithmetic.bmp", result);
        }


        public static void GetRotatedCornerByTriangle(Mat src, CornerSide cs, double lineLengthThroshold, int cornerMaskSize, out PointF resultP, out double cornerDegree, out LineSegment2D[] line)
        {
            Rectangle rect = new Rectangle(0, 0, 0, 0);
            GetMaxSizeBlob(src, ref rect);
            bool xInverse = false;
            bool yInverse = false;

            switch (cs)
            {
                case CornerSide.LeftTop:
                    xInverse = yInverse = true;
                    break;
                case CornerSide.LeftBottom:
                    xInverse = true;
                    break;
                case CornerSide.RightTop:
                    yInverse = true;
                    break;
            }

            Point[] vertices = new Point[]
            {
                new Point(rect.X, rect.Y),
                new Point(rect.X + rect.Width, rect.Y),
                new Point(rect.X, rect.Y + rect.Height),
                new Point(rect.X + rect.Width, rect.Y + rect.Height)};


            FindNearestAndFarestPointFromOrg(src.Size, vertices, xInverse, yInverse, out int nearestIndex, out int farestIndex);

            List<Point> maskPoints = new List<Point>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (i != farestIndex && i != nearestIndex)
                    maskPoints.Add(vertices[i]);
            }

            //填充遮罩
            maskPoints.Insert(0, vertices[nearestIndex]);
            VectorOfPoint vp = new VectorOfPoint(maskPoints.ToArray());
            CvInvoke.FillConvexPoly(src, vp, new MCvScalar(0));
            //找最大三角形，最遠的頂點為角點
            VectorOfPointF triangleVertices;
            GetMaxTriangle(src, lineLengthThroshold, out triangleVertices);
            FindNearestAndFarestPointFromOrg(src.Size, triangleVertices.ToArray(), xInverse, yInverse, out nearestIndex, out farestIndex);
            resultP = triangleVertices[farestIndex];


            if (cornerMaskSize > 0)
            {
                //填充角點遮罩
                maskPoints.Clear();
                Rectangle cornerMaskRect = new Rectangle(
                    (int)triangleVertices[farestIndex].X - cornerMaskSize,
                    (int)triangleVertices[farestIndex].Y - cornerMaskSize,
                    cornerMaskSize * 2,
                    cornerMaskSize * 2);
                cornerMaskRect.Intersect(new Rectangle(0, 0, src.Width, src.Height));
                maskPoints.Clear();
                maskPoints.Add(new Point(cornerMaskRect.Left, cornerMaskRect.Top));
                maskPoints.Add(new Point(cornerMaskRect.Left, cornerMaskRect.Bottom));
                maskPoints.Add(new Point(cornerMaskRect.Right, cornerMaskRect.Bottom));
                maskPoints.Add(new Point(cornerMaskRect.Right, cornerMaskRect.Top));
                vp = new VectorOfPoint(maskPoints.ToArray());
                CvInvoke.FillConvexPoly(src, vp, new MCvScalar(0));
                //找最大三角形，最遠的頂點為角點，使用遮罩去除角點誤差後再算一次
                GetMaxTriangle(src, lineLengthThroshold, out triangleVertices);
                FindNearestAndFarestPointFromOrg(src.Size, triangleVertices.ToArray(), xInverse, yInverse, out nearestIndex, out farestIndex);
                resultP = triangleVertices[farestIndex];
                //Mat canvas = new Mat();
                //CvInvoke.Resize(src, canvas, new Size(0, 0), 0.2, 0.2);
                //CvInvoke.Imshow("mat", canvas);
                //CvInvoke.WaitKey(0);
                //CvInvoke.DestroyWindow("mat");
            }






            List<PointF> triangle = new List<PointF>();
            for (int i = 0; i < triangleVertices.Size; i++)
            {
                if (i != farestIndex)
                    triangle.Add(triangleVertices[i]);
            }

            //角點的夾角線
            line = new LineSegment2D[2];
            line[0] = new LineSegment2D(Point.Round(triangle[0]), Point.Round(triangleVertices[farestIndex]));
            line[1] = new LineSegment2D(Point.Round(triangle[1]), Point.Round(triangleVertices[farestIndex]));

            //計算角度
            //Formula : cosTheta = (a^2 + b^2 - c^2) / 2ab 
            double a = Math.Sqrt(Math.Pow(triangle[1].Y - triangleVertices[farestIndex].Y, 2) + Math.Pow(triangle[1].X - triangleVertices[farestIndex].X, 2));
            double b = Math.Sqrt(Math.Pow(triangle[0].Y - triangleVertices[farestIndex].Y, 2) + Math.Pow(triangle[0].X - triangleVertices[farestIndex].X, 2));
            double c = Math.Sqrt(Math.Pow(triangle[1].Y - triangle[0].Y, 2) + Math.Pow(triangle[1].X - triangle[0].X, 2));

            cornerDegree = Math.Acos((a * a + b * b - c * c) / (2 * a * b)) * 180 / Math.PI;
        }

        public static void GetMaxSizeBlob(Mat Sample, ref Rectangle Box)
        {
            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                // 在這版本請使用FindContours，早期版本有cvFindContours等等，在這版都無法使用，
                // 由於這邊是要取得最外層的輪廓，所以第三個參數給 null，第四個參數則用 RetrType.External。
                CvInvoke.FindContours(Sample, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                int area = 0;
                int count = contours.Size;
                for (int i = 0; i < count; i++)
                {
                    using (VectorOfPoint contour = contours[i])
                    {
                        // 使用 BoundingRectangle 取得框選矩形
                        Rectangle BoundingBox = CvInvoke.BoundingRectangle(contour);
                        if (BoundingBox.Width * BoundingBox.Height > area)
                        {
                            Box = BoundingBox;
                            area = BoundingBox.Width * BoundingBox.Height;
                        }
                    }
                }
            }
        }
        public static void GetMaxTriangle(Mat src, double lineLengthThreshold, out VectorOfPointF triangle)
        {
            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {

                CvInvoke.FindContours(src, contours, null, RetrType.External, ChainApproxMethod.ChainApproxNone);

                int count = contours.Size;
                double area = 0;
                triangle = new VectorOfPointF();
                VectorOfPoint maxContour = new VectorOfPoint();
                for (int i = 0; i < contours.Size; i++)
                {
                    using (VectorOfPoint contour = contours[i])
                    {
                        if (CvInvoke.ContourArea(contour) > area)
                        {
                            area = CvInvoke.ContourArea(contour);
                            maxContour = contour;
                        }
                    }
                }

                Mat canvas = src.Clone();
                canvas.SetTo(new MCvScalar(0));
                CvInvoke.CvtColor(canvas, canvas, ColorConversion.Gray2Bgr);
                if (EnableScratchProcessFile)
                {
                    for (int i = 0; i < maxContour.Size; i++)
                        CvInvoke.Circle(canvas, maxContour[i], 0, new MCvScalar(255,255,255));
                    CvInvoke.Imwrite("max_contour.bmp", canvas);
                }

                VectorOfPoint approx_cotour = new VectorOfPoint();
                VectorOfPoint result_contour = new VectorOfPoint();
                CvInvoke.ApproxPolyDP(maxContour, approx_cotour, 1, true);
                List<LineSegment2D> lines = new List<LineSegment2D>();
                for (int i = 0; i < approx_cotour.Size - 1; i++)
                {
                    LineSegment2D line = new LineSegment2D();
                    line = new LineSegment2D(approx_cotour[i], approx_cotour[i + 1]);
                    lines.Add(line);
                }
                lines.Add(new LineSegment2D(approx_cotour[0], approx_cotour[approx_cotour.Size - 1]));

                if (EnableScratchProcessFile)
                {
                    MCvScalar[] rgb = new MCvScalar[] { new MCvScalar(255, 0, 0), new MCvScalar(0, 255, 0), new MCvScalar(0, 0, 255) };
                    for (int i = 0; i < lines.Count; i++)
                        CvInvoke.Line(canvas, lines[i].P1, lines[i].P2, rgb[i%3]);
                    CvInvoke.Imwrite("approx_lines.bmp", canvas);
                }

                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Length >= lineLengthThreshold)
                    {
                        result_contour.Push(new Point[] { lines[i].P1 });
                        result_contour.Push(new Point[] { lines[i].P2 });
                    }
                }

                CvInvoke.MinEnclosingTriangle(result_contour, triangle);

                if (EnableScratchProcessFile)
                {
                    CvInvoke.Line(canvas, Point.Round(triangle[0]), Point.Round(triangle[1]), new MCvScalar(125,125,125));
                    CvInvoke.Line(canvas, Point.Round(triangle[1]), Point.Round(triangle[2]), new MCvScalar(125, 125, 125));
                    CvInvoke.Line(canvas, Point.Round(triangle[2]), Point.Round(triangle[0]), new MCvScalar(125, 125, 125));
                    CvInvoke.Imwrite("triangle.bmp", canvas);
                }
            }
        }

        public static void FindNearestAndFarestPointFromOrg(Size size, PointF[] pointsF, bool xInverse, bool yInverse, out int nearestIndex, out int farestIndex)
        {
            for (int i = 0; i < pointsF.Length; i++)
            {
                if (xInverse)
                    pointsF[i].X = size.Width - pointsF[i].X;
                if (yInverse)
                    pointsF[i].Y = size.Height - pointsF[i].Y;
            }
            nearestIndex = 0;
            farestIndex = 0;
            double max = 0;
            double min = Distance_2P(new Point(0, 0), pointsF[0]);
            for (int i = 0; i < pointsF.Length; i++)
            {
                if (Distance_2P(new Point(0, 0), pointsF[i]) > max)
                {
                    max = Distance_2P(new Point(0, 0), pointsF[i]);
                    farestIndex = i;
                }
                if (min > Distance_2P(new Point(0, 0), pointsF[i]))
                {
                    min = Distance_2P(new Point(0, 0), pointsF[i]);
                    nearestIndex = i;
                }
            }
        }

        public static void FindNearestAndFarestPointFromOrg(Size size, Point[] points, bool xInverse, bool yInverse, out int nearestIndex, out int farestIndex)
        {
            PointF[] pointsF = new PointF[points.Length];
            for (int i = 0; i < pointsF.Length; i++)
                pointsF[i] = new PointF(points[i].X, points[i].Y);
            FindNearestAndFarestPointFromOrg(size, pointsF, xInverse, yInverse, out nearestIndex, out farestIndex);
        }
        public static double Distance_2P(Point p1, Point p2)
        {
            double value = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            return value;
        }
        public static double Distance_2P(PointF p1, PointF p2)
        {
            double value = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            return value;
        }

        public static void ScratchProcessFile(string filename, Mat Img)
        {
            if (EnableScratchProcessFile)
            {
                CvInvoke.Imwrite(filename, Img);
            }
        }
    }







    public class CornerDetectorDef : IDisposable
    {
        public enum DetectionDirection
        {
            RightTopToLeftBottom,
            LeftTopToRightBottom,
            LeftBottomToRightTop,
            RightBottomToLeftTop
        }



        private Mat _Temp16S;
        private Mat _Temp8U;
        private Mat _EdgeX;
        private Mat _EdgeY;
        private Mat _LightThresholdImage;

        public CornerDetectorDef()
        {
            _Temp16S = null;
            _Temp8U = null;
            _EdgeX = null;
            _EdgeY = null;
        }

        public void Dispose()
        {
            if (_Temp16S != null)
            {
                _Temp16S.Dispose();
                _Temp8U.Dispose();
                _EdgeX.Dispose();
                _EdgeY.Dispose();
                _LightThresholdImage.Dispose();
            }
        }
        public Point GetCorner(IInputOutputArray inputImage, DetectionDirection direction, int houghLinesThrehold = 0, int houghLinesMinLen = 50, int minlineGap = 5, double Xv = 0.3, double Yv = 0.3, int antiBrightNoise = 210)
        {
            #region 檢測方向選擇
            int directionX = 1;
            int directionY = 1;
            switch (direction)
            {
                case DetectionDirection.RightTopToLeftBottom:
                    {
                        directionX = -1;
                        directionY = 1;
                    }
                    break;
                case DetectionDirection.LeftTopToRightBottom:
                    {
                        directionX = 1;
                        directionY = 1;
                    }
                    break;
                case DetectionDirection.LeftBottomToRightTop:
                    {
                        directionX = 1;
                        directionY = -1;
                    }
                    break;
                case DetectionDirection.RightBottomToLeftTop:
                    {
                        directionX = -1;
                        directionY = -1;
                    }
                    break;
            }
            #endregion

            Mat inputMat = inputImage.GetInputArray().GetMat();
            Mat src = inputMat.Clone();

            Process.ScratchProcessFile("_Src.bmp", src);

            if (_Temp16S == null || _Temp16S.Width != inputMat.Width || _Temp16S.Height != inputMat.Height)
                _Temp16S = new Mat(new Size(inputMat.Width, inputMat.Height), Emgu.CV.CvEnum.DepthType.Cv16S, 1);

            if (_Temp8U == null || _Temp8U.Width != inputMat.Width || _Temp8U.Height != inputMat.Height)
                _Temp8U = new Mat(new Size(inputMat.Width, inputMat.Height), Emgu.CV.CvEnum.DepthType.Cv8U, 1);

            if (_EdgeX == null || _EdgeX.Width != inputMat.Width || _EdgeX.Height != inputMat.Height)
                _EdgeX = new Mat(new Size(inputMat.Width, inputMat.Height), Emgu.CV.CvEnum.DepthType.Cv8U, 1);

            if (_EdgeY == null || _EdgeY.Width != inputMat.Width || _EdgeY.Height != inputMat.Height)
                _EdgeY = new Mat(new Size(inputMat.Width, inputMat.Height), Emgu.CV.CvEnum.DepthType.Cv8U, 1);

            if (_LightThresholdImage == null || _LightThresholdImage.Width != inputMat.Width || _LightThresholdImage.Height != inputMat.Height)
                _LightThresholdImage = new Mat(new Size(inputMat.Width, inputMat.Height), Emgu.CV.CvEnum.DepthType.Cv8U, 1);

            CvInvoke.Threshold(src, _LightThresholdImage, antiBrightNoise, 255, Emgu.CV.CvEnum.ThresholdType.BinaryInv);

            Process.ScratchProcessFile("Threshold.bmp", _LightThresholdImage);

            Mat element = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Cross,
                    new Size(3, 3), new Point(-1, -1));
            CvInvoke.Erode(_LightThresholdImage, _LightThresholdImage, element, new Point(-1, -1), 1,
                    Emgu.CV.CvEnum.BorderType.Default, new MCvScalar(0, 0, 0));

            Process.ScratchProcessFile("LightThreshold.bmp", _LightThresholdImage);


            double xv = Xv * directionX;
            float[,] k =
            {   {(float)xv, 0, (float)-xv},
                {(float)xv, 0, (float)-xv},
                {(float)xv, 0, (float)-xv},
                {(float)xv, 0, (float)-xv},
                {(float)xv, 0, (float)-xv},
                {(float)xv, 0, (float)-xv},
                {(float)xv, 0, (float)-xv},
                {(float)xv, 0, (float)-xv},
                {(float)xv, 0, (float)-xv}};

            ConvolutionKernelF kernel = new ConvolutionKernelF(k);

            CvInvoke.Filter2D(src, _Temp16S, kernel, new Point(-1, -1));

            CvInvoke.ConvertScaleAbs(_Temp16S, _EdgeX, 1, 128);
            Process.ScratchProcessFile("EdgeX.bmp", _EdgeX);

            CvInvoke.Threshold(_EdgeX, _EdgeX, 192, 255, Emgu.CV.CvEnum.ThresholdType.Binary);
            Process.ScratchProcessFile("EdgeXThreshold.bmp", _EdgeX);

            CvInvoke.BitwiseAnd(_EdgeX, _LightThresholdImage, _EdgeX);

            LineSegment2D[] linesX = CvInvoke.HoughLinesP(
                _EdgeX, 1,
                Math.PI / 180 / 2,
                houghLinesThrehold,
                houghLinesMinLen,
                0);

            double yv = Yv * directionY;
            float[,] kY =
            {   {(float)yv, (float)yv, (float)yv, (float)yv, (float)yv, (float)yv, (float)yv, (float)yv, (float)yv},
                {0, 0, 0, 0, 0, 0, 0, 0, 0},
                {(float)-yv, (float)-yv, (float)-yv, (float)-yv, (float)-yv, (float)-yv, (float)-yv, (float)-yv, (float)-yv},
            };

            ConvolutionKernelF kernelY = new ConvolutionKernelF(kY);

            CvInvoke.Filter2D(src, _Temp16S, kernelY, new Point(-1, -1));
            CvInvoke.ConvertScaleAbs(_Temp16S, _EdgeY, 1, 128);
            Process.ScratchProcessFile("EdgeY.bmp", _EdgeY);

            CvInvoke.Threshold(_EdgeY, _EdgeY, 192, 255, Emgu.CV.CvEnum.ThresholdType.Binary);
            Process.ScratchProcessFile("EdgeYThreshold.bmp", _EdgeY);

            CvInvoke.BitwiseAnd(_EdgeY, _LightThresholdImage, _EdgeY);

            LineSegment2D[] linesY = CvInvoke.HoughLinesP(
                _EdgeY, 1,
                Math.PI / 180 / 2,
                houghLinesThrehold,
                houghLinesMinLen,
                0);
            LineSegment2D[] totolLine = new LineSegment2D[linesX.Length + linesY.Length];

            for (int i = 0; i < linesX.Length; i++)
                totolLine[i] = linesX[i];

            for (int i = 0; i < linesY.Length; i++)
                totolLine[i + linesX.Length] = linesY[i];


            LineSegment2D[] mergedLines = MergeLines((LineSegment2D[])totolLine.Clone(), minlineGap);
            List<LineSegment2D[]> linePairList;
            Point[] corners = GetRightAngleCorners(mergedLines, 5, directionX, directionY, out linePairList);
            Point insideCorner = FindInsideCorner(inputMat, linePairList, corners, directionX, directionY);

            #region SaveImage
            #region Draw
            #region Draw edge by before merge
            Mat paint1 = inputMat.Clone();
            for (int i = 0; i < totolLine.Length; i++)
            {
                if (Math.Abs(totolLine[i].Direction.X) > Math.Abs(totolLine[i].Direction.Y))
                    CvInvoke.Line(paint1, totolLine[i].P1, totolLine[i].P2, new MCvScalar(255, 0, 0), 1, Emgu.CV.CvEnum.LineType.AntiAlias); //LineType.AntiAlias表示抗锯齿
                else
                    CvInvoke.Line(paint1, totolLine[i].P1, totolLine[i].P2, new MCvScalar(0, 0, 255), 1, Emgu.CV.CvEnum.LineType.AntiAlias); //LineType.AntiAlias表示抗锯齿
            }
            Process.ScratchProcessFile("BeforeMerge.bmp", paint1);
            #endregion

            #region Draw Corners
            Mat paint = inputMat.Clone();
            for (int i = 0; i < mergedLines.Length; i++)
            {
                if (Math.Abs(mergedLines[i].Direction.X) > Math.Abs(mergedLines[i].Direction.Y))
                    CvInvoke.Line(paint, mergedLines[i].P1, mergedLines[i].P2, new MCvScalar(255, 0, 0), 1, Emgu.CV.CvEnum.LineType.AntiAlias); //LineType.AntiAlias表示抗锯齿
                else
                    CvInvoke.Line(paint, mergedLines[i].P1, mergedLines[i].P2, new MCvScalar(0, 0, 255), 1, Emgu.CV.CvEnum.LineType.AntiAlias); //LineType.AntiAlias表示抗锯齿
            }
            for (int i = 0; i < corners.Length; i++)
            {
                CvInvoke.Circle(paint, corners[i], 5, new MCvScalar(0, 0, 255));
            }
            CvInvoke.Circle(paint, insideCorner, 5, new MCvScalar(255, 0, 0));
            Process.ScratchProcessFile("Paint.bmp", paint);
            #endregion
            #endregion

            #region Draw Cross
            Mat Imgresult = inputMat.Clone();
            int CrossSize = 50;
            Point[] Cross = new Point[]
            {
                    new Point(insideCorner.X - CrossSize, insideCorner.Y),
                    new Point(insideCorner.X + CrossSize, insideCorner.Y),
                    new Point(insideCorner.X , insideCorner.Y - CrossSize),
                    new Point(insideCorner.X , insideCorner.Y + CrossSize),
            };

            CvInvoke.Line(Imgresult, Cross[0], Cross[1], new MCvScalar(0, 0, 255));
            CvInvoke.Line(Imgresult, Cross[2], Cross[3], new MCvScalar(0, 0, 255));
            #endregion

            Process.ScratchProcessFile("Result.bmp", Imgresult);
            #endregion

            return insideCorner;
        }

        private static Point FindInsideCorner(Mat inputImage, List<LineSegment2D[]> linePairList, Point[] corners, int directionX, int directionY)
        {
            Point insideCorner = new Point(-1, -1);

            for (int i = 0; i < corners.Count(); i++)
            {

                List<Point> p = new List<Point>();

                for (int j = 0; j < linePairList.Count(); j++)
                {
                    if (j != i)
                    {


                        if (Math.Abs(linePairList[j][0].P1.Y - linePairList[i][0].P1.Y) > 2)
                        {
                            // p.Add(LinePairList[j][0].P1);
                            // p.Add(LinePairList[j][0].P2);

                            p.Add(new Point((linePairList[j][0].P1.X + linePairList[j][0].P2.X) / 2, (linePairList[j][0].P1.Y + linePairList[j][0].P2.Y) / 2));
                            p.Add(new Point((linePairList[j][0].P1.X * 1 / 10 + linePairList[j][0].P2.X * 9 / 10), (linePairList[j][0].P1.Y * 1 / 10 + linePairList[j][0].P2.Y * 9 / 10)));
                            p.Add(new Point((linePairList[j][0].P1.X * 9 / 10 + linePairList[j][0].P2.X * 1 / 10), (linePairList[j][0].P1.Y * 9 / 10 + linePairList[j][0].P2.Y * 1 / 10)));
                        }

                        if (Math.Abs(linePairList[j][1].P1.X - linePairList[i][1].P1.X) > 2)
                        {

                            //p.Add(LinePairList[j][1].P1);
                            //p.Add(LinePairList[j][1].P2);
                            p.Add(new Point((linePairList[j][1].P1.X * 1 / 10 + linePairList[j][1].P2.X * 9 / 10), (linePairList[j][1].P1.Y * 1 / 10 + linePairList[j][1].P2.Y * 9 / 10)));
                            p.Add(new Point((linePairList[j][1].P1.X * 9 / 10 + linePairList[j][1].P2.X * 1 / 10), (linePairList[j][1].P1.Y * 9 / 10 + linePairList[j][1].P2.Y * 1 / 10)));
                            p.Add(new Point((linePairList[j][1].P1.X + linePairList[j][1].P2.X) / 2, (linePairList[j][1].P1.Y + linePairList[j][1].P2.Y) / 2));
                        }

                        p.Add(corners[j]);
                    }
                }

                PointF edgeV = new PointF();
                PointF edgeH = new PointF();
                Point originP = new Point();
                if (directionX == -1)
                {
                    edgeV = GetIntersection(linePairList[i][0], new LineSegment2D(new Point(inputImage.Width - 1, 0), new Point(inputImage.Width - 1, corners[i].Y)));

                    if (directionY == 1)
                    {
                        edgeH = GetIntersection(linePairList[i][1], new LineSegment2D(new Point(0, 0), new Point(corners[i].X, 0)));
                        originP = new Point(inputImage.Width - 1, 0);
                    }
                    else
                    {
                        edgeH = GetIntersection(linePairList[i][1], new LineSegment2D(new Point(0, 0), new Point(corners[i].X, inputImage.Height - 1)));
                        originP = new Point(inputImage.Width - 1, inputImage.Height - 1);
                    }
                }
                else if (directionX == 1)
                {
                    edgeV = GetIntersection(linePairList[i][0], new LineSegment2D(new Point(0, 0), new Point(0, corners[i].Y)));

                    if (directionY == 1)
                    {
                        edgeH = GetIntersection(linePairList[i][1], new LineSegment2D(new Point(0, 0), new Point(corners[i].X, 0)));
                        originP = new Point(0, 0);
                    }
                    else
                    {
                        edgeH = GetIntersection(linePairList[i][1], new LineSegment2D(new Point(0, 0), new Point(corners[i].X, inputImage.Height - 1)));
                        originP = new Point(0, inputImage.Height - 1);
                    }
                }


                if (IsOnTop(corners[i],
                        new Point((int)edgeV.X, (int)edgeV.Y),
                        originP,
                        new Point((int)edgeH.X, (int)edgeH.Y),
                        p))
                {
                    insideCorner = corners[i];
                    break;
                }
            }

            return insideCorner;
        }

        private static bool IsOnTop(Point P1, Point P2, Point P3, Point P4, List<Point> JudgePoints)
        {
            System.Drawing.Drawing2D.GraphicsPath myGraphicsPath = new System.Drawing.Drawing2D.GraphicsPath();
            Region myRegion = new Region();
            myGraphicsPath.Reset();
            //添家多边形点      

            myGraphicsPath.AddPolygon(new Point[] { P1, P2, P3, P4 });
            myRegion.MakeEmpty();
            myRegion.Union(myGraphicsPath);
            //返回判断点是否在多边形里
            for (int i = 0; i < JudgePoints.Count(); i++)
            {
                if (myRegion.IsVisible(JudgePoints[i]))
                    return false;
            }
            return true;
        }

        private static Point[] GetRightAngleCorners(LineSegment2D[] lines, double tolerance, int directionX, int directionY, out List<LineSegment2D[]> cornerLines)
        {
            if (tolerance < 0)
                throw new Exception("Invalid tolerance value.");
            List<Point> corner = new List<Point>();
            cornerLines = new List<LineSegment2D[]>();
            cornerLines.Clear();
            for (int i = 0; i < lines.Length; i++)
            {
                if (Math.Abs(lines[i].Direction.X) > Math.Abs(lines[i].Direction.Y)) //horizontal
                {
                    for (int j = 0; j < lines.Length; j++)
                    {
                        if (i != j)
                        {
                            if (Math.Abs(lines[j].Direction.Y) > Math.Abs(lines[j].Direction.X)) //vertical
                            {
                                if (Math.Abs(Math.Abs(GetTwoLineAngle(lines[i], lines[j])) - 90) < tolerance ||
                                    Math.Abs(Math.Abs(GetTwoLineAngle(lines[i], lines[j])) - 270) < tolerance)
                                {
                                    Point intersection = Point.Round(GetIntersection(lines[i], lines[j]));
                                    Point horizontalLineMiddlePoint = new Point(
                                    (lines[i].P1.X + lines[i].P2.X) / 2, (lines[i].P1.Y + lines[i].P2.Y) / 2);
                                    Point verticalLineMiddlePoint = new Point(
                                    (lines[j].P1.X + lines[j].P2.X) / 2, (lines[j].P1.Y + lines[j].P2.Y) / 2);
                                    if ((directionY == 1 && horizontalLineMiddlePoint.Y > verticalLineMiddlePoint.Y)
                                        || directionY == -1 && horizontalLineMiddlePoint.Y < verticalLineMiddlePoint.Y)
                                    {
                                        if (directionX == -1)
                                        {
                                            //if (horizontalLineMiddlePoint.X * 1.5 > verticalLineMiddlePoint.X)
                                            {
                                                corner.Add(intersection);
                                                cornerLines.Add(new LineSegment2D[] { lines[i], lines[j] });
                                            }
                                        }
                                        if (directionX == 1)
                                        {
                                            //if (horizontalLineMiddlePoint.X < verticalLineMiddlePoint.X)
                                            //if (lines[i].P1.X < 50 || lines[i].P2.X < 50 || Math.Abs(lines[i].P2.X - lines[i].P1.X) > HoughLinesMinLen*2)
                                            {
                                                corner.Add(intersection);
                                                cornerLines.Add(new LineSegment2D[] { lines[i], lines[j] });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return corner.ToArray();
        }




        /// <summary>求兩線交點</summary>
        /// <param name="line1"></param>
        /// <param name="line2"></param>
        /// <returns></returns>
        private static PointF GetIntersection(LineSegment2D line1, LineSegment2D line2)
        {
            PointF lineFirstStar = line1.P1;
            PointF lineFirstEnd = line1.P2;
            PointF lineSecondStar = line2.P1;
            PointF lineSecondEnd = line2.P2;
            /*
             * L1，L2都存在斜率的情況：
             * 直線方程L1: ( y - y1 ) / ( y2 - y1 ) = ( x - x1 ) / ( x2 - x1 ) 
             * => y = [ ( y2 - y1 ) / ( x2 - x1 ) ]( x - x1 ) + y1
             * 令 a = ( y2 - y1 ) / ( x2 - x1 )
             * 有 y = a * x - a * x1 + y1   .........1
             * 直線方程L2: ( y - y3 ) / ( y4 - y3 ) = ( x - x3 ) / ( x4 - x3 )
             * 令 b = ( y4 - y3 ) / ( x4 - x3 )
             * 有 y = b * x - b * x3 + y3 ..........2
             * 
             * 如果 a = b，則兩直線平等，否則， 聯解方程 1,2，得:
             * x = ( a * x1 - b * x3 - y1 + y3 ) / ( a - b )
             * y = a * x - a * x1 + y1
             * 
             * L1存在斜率, L2平行Y軸的情況：
             * x = x3
             * y = a * x3 - a * x1 + y1
             * 
             * L1 平行Y軸，L2存在斜率的情況：
             * x = x1
             * y = b * x - b * x3 + y3
             * 
             * L1與L2都平行Y軸的情況：
             * 如果 x1 = x3，那麼L1與L2重合，否則平等
             * 
            */
            float a = 0, b = 0;
            int state = 0;
            if (lineFirstStar.X != lineFirstEnd.X)
            {
                a = (lineFirstEnd.Y - lineFirstStar.Y) / (lineFirstEnd.X - lineFirstStar.X);
                state |= 1;
            }
            if (lineSecondStar.X != lineSecondEnd.X)
            {
                b = (lineSecondEnd.Y - lineSecondStar.Y) / (lineSecondEnd.X - lineSecondStar.X);
                state |= 2;
            }
            switch (state)
            {
                case 0: //L1與L2都平行Y軸
                    {
                        if (lineFirstStar.X == lineSecondStar.X)
                        {
                            //throw new Exception("兩條直線互相重合，且平行於Y軸，無法計算交點。");
                            return new PointF(0, 0);
                        }
                        else
                        {
                            //throw new Exception("兩條直線互相平行，且平行於Y軸，無法計算交點。");
                            return new PointF(0, 0);
                        }
                    }
                case 1: //L1存在斜率, L2平行Y軸
                    {
                        float x = lineSecondStar.X;
                        float y = (lineFirstStar.X - x) * (-a) + lineFirstStar.Y;
                        return new PointF(x, y);
                    }
                case 2: //L1 平行Y軸，L2存在斜率
                    {
                        float x = lineFirstStar.X;
                        //網上有相似代碼的，這一處是錯誤的。你可以對比case 1 的邏輯 進行分析
                        //源code:lineSecondStar * x + lineSecondStar * lineSecondStar.X + p3.Y;
                        float y = (lineSecondStar.X - x) * (-b) + lineSecondStar.Y;
                        return new PointF(x, y);
                    }
                case 3: //L1，L2都存在斜率
                    {
                        if (a == b)
                        {
                            // throw new Exception("兩條直線平行或重合，無法計算交點。");
                            return new PointF(0, 0);
                        }
                        float x = (a * lineFirstStar.X - b * lineSecondStar.X - lineFirstStar.Y + lineSecondStar.Y) / (a - b);
                        float y = a * x - a * lineFirstStar.X + lineFirstStar.Y;
                        return new PointF(x, y);
                    }
            }
            // throw new Exception("不可能發生的情況");
            return new PointF(0, 0);
        }

        private static Mat GetAImageAfterShift(Mat orgImage, int x, int y)
        {
            Image<Bgr, byte> inputImage = orgImage.ToImage<Bgr, byte>();
            Image<Bgr, byte> outputImage = new Image<Bgr, byte>(inputImage.Width, inputImage.Height);

            Rectangle inputRoi = new Rectangle();
            Rectangle outputRoi = new Rectangle();

            if (x >= 0 && y >= 0)
            {
                inputRoi = new Rectangle(0, 0, inputImage.Width - x, inputImage.Height - y);
                outputRoi = new Rectangle(x, y, outputImage.Width - x, outputImage.Height - y);
            }
            if (x >= 0 && y < 0)
            {
                inputRoi = new Rectangle(0, -y, inputImage.Width - x, inputImage.Height + y);
                outputRoi = new Rectangle(x, 0, outputImage.Width - x, outputImage.Height + y);
            }
            if (x <= 0 && y >= 0)
            {
                inputRoi = new Rectangle(-x, 0, inputImage.Width + x, inputImage.Height - y);
                outputRoi = new Rectangle(0, y, outputImage.Width + x, outputImage.Height - y);
            }
            if (x <= 0 && y <= 0)
            {
                inputRoi = new Rectangle(-x, -y, inputImage.Width + x, inputImage.Height + y);
                outputRoi = new Rectangle(0, 0, outputImage.Width + x, outputImage.Height + y);
            }
            inputImage.ROI = inputRoi;
            outputImage.ROI = outputRoi;
            inputImage.CopyTo(outputImage);
            outputImage.ROI = Rectangle.Empty;
            inputImage.ROI = Rectangle.Empty;
            Mat mat = outputImage.Mat.Clone();
            outputImage.Dispose();
            inputImage.Dispose();
            return mat.Clone();
        }

        private static LineSegment2D[] MergeLines(LineSegment2D[] lines, double minDistanceBetween2Lines)
        {
            List<LineSegment2D> mergedLines = new List<LineSegment2D>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].P1.X == 0 & lines[i].P1.Y == 0 && lines[i].P2.X == 0 & lines[i].P2.Y == 0)
                    continue;
                List<LineSegment2D> tempLine = new List<LineSegment2D>();


                //收集要合併的線段
                tempLine.Add(lines[i]);
                if (Math.Abs(lines[i].Direction.X) > Math.Abs(lines[i].Direction.Y)) //horizontal
                {
                    for (int j = 0; j < lines.Length; j++)
                    {
                        if (Math.Abs(lines[i].Direction.X) > Math.Abs(lines[i].Direction.Y)) //horizontal
                        {
                            if (lines[j].P1.X == 0 & lines[j].P1.Y == 0 && lines[j].P2.X == 0 & lines[j].P2.Y == 0)
                                continue;
                            if ((GetDistanceFromPointToLine(lines[j].P1, lines[i]) +
                                GetDistanceFromPointToLine(lines[j].P2, lines[i])) / 2
                                < minDistanceBetween2Lines)
                            {
                                if (i != j)
                                {
                                    tempLine.Add(lines[j]);
                                    lines[j] = new LineSegment2D(new Point(0, 0), new Point(0, 0));
                                }
                            }
                        }
                    }
                }
                if (Math.Abs(lines[i].Direction.Y) > Math.Abs(lines[i].Direction.X)) //vertical
                {
                    for (int j = 0; j < lines.Length; j++)
                    {
                        if (Math.Abs(lines[i].Direction.Y) > Math.Abs(lines[i].Direction.X)) //vertical
                        {
                            if (lines[j].P1.X == 0 & lines[j].P1.Y == 0 && lines[j].P2.X == 0 & lines[j].P2.Y == 0)
                                continue;
                            if ((GetDistanceFromPointToLine(lines[j].P1, lines[i]) +
                                GetDistanceFromPointToLine(lines[j].P2, lines[i])) / 2
                                < minDistanceBetween2Lines)
                            {
                                if (i != j)
                                {
                                    tempLine.Add(lines[j]);
                                    lines[j] = new LineSegment2D(new Point(0, 0), new Point(0, 0));
                                }
                            }
                        }
                    }
                }

                //整理合併的線段
                List<Point> points = new List<Point>();
                foreach (var item in tempLine)
                {
                    points.Add(item.P1);
                    points.Add(item.P2);
                }
                IEnumerable<Point> queryX = points.OrderBy(p => p.X);
                IEnumerable<Point> queryY = points.OrderBy(p => p.Y);
                LineSegment2D mergedLine = new LineSegment2D();
                if (Math.Abs(lines[i].Direction.X) > Math.Abs(lines[i].Direction.Y)) //horizontal
                    mergedLine = new LineSegment2D(queryX.First(), queryX.Last());
                if (Math.Abs(lines[i].Direction.Y) > Math.Abs(lines[i].Direction.X)) //vertical
                    mergedLine = new LineSegment2D(queryY.First(), queryY.Last());
                mergedLines.Add(mergedLine);

                lines[i] = new LineSegment2D(new Point(0, 0), new Point(0, 0));
            }
            return mergedLines.ToArray();
        }

        /// <summary>求點到線距離</summary>
        /// <param name="point"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        private static double GetDistanceFromPointToLine(Point point, LineSegment2D line)
        {
            // x - x1 / x2 - x1 = y - y1 / y2 - y1
            //ax + by + c = 0
            // (y2-y1)x + (-x2+x1)y + (x2y1 - x1y2) = 0
            //neither vertical and horizontal

            double slope = (double)(line.P2.Y - line.P1.Y) / (double)(line.P2.X - line.P1.X);

            if (double.IsInfinity(slope))  //vertical
                return Math.Abs(point.X - line.P1.X);
            else if (slope == 0)   //horizontal
                return Math.Abs(point.Y - line.P1.Y);
            else
            {
                double a = line.P2.Y - line.P1.Y;
                double b = -line.P2.X + line.P1.X;
                double c = line.P2.X * line.P1.Y - line.P1.X * line.P2.Y;

                //distance from point to line
                // |ax + by + c| / sqrt(a^2 +b^2)

                double distance = Math.Abs(a * point.X + b * point.Y + c) / Math.Sqrt(a * a + b * b);
                return distance;
            }
        }

        /// <summary>求兩線角度</summary>
        /// <param name="line1"></param>
        /// <param name="line2"></param>
        /// <returns></returns>
        private static double GetTwoLineAngle(LineSegment2D line1, LineSegment2D line2)
        {
            Double rad = Math.Atan2(line1.P2.Y - line1.P1.Y, line1.P2.X - line1.P1.X) - Math.Atan2(line2.P2.Y - line2.P1.Y, line2.P2.X - line2.P1.X);
            double degree = rad * 180 / Math.PI;
            return degree;
        }

        /// <summary>
        /// src = 來源影像, Threshold = 灰階門檻, MinArea = 物件最小面積, MaxArea = 物件最大面積,  WhiteClass = 是否為白色物件 回傳物件中心
        /// </summary>
        
    }




























    public class MatchDef : IDisposable
    {

        private Image<Gray, byte> _ImageMatchModel;
        private Image<Gray, byte> _ImageMatchRotateModel;
        private UMat _templateMask;
        private Point _ModelLeftTop;
        private PointF _ResultLeftTop;
        private double _Score;
        private bool _MatchSuccess;
        public MatchDef()
        {
            _Score = 0.8;

            _ImageMatchModel = null;
            _ImageMatchRotateModel = null;
            _templateMask = null;
            _MatchSuccess = false;


        }

        public void Dispose()
        {
            if (_templateMask != null)
            {
                _templateMask.Dispose();
                _templateMask = null;
            }


            if (_ImageMatchModel != null)
            {
                _ImageMatchModel.Dispose();
                _ImageMatchModel = null;
            }

            if (_ImageMatchRotateModel != null)
            {
                _ImageMatchRotateModel.Dispose();
                _ImageMatchRotateModel = null;
            }
        }
        public Image<Gray, byte> GetImage()
        {
            return _ImageMatchModel;
        }

        public void SetMask(Image<Gray, byte> cSrcImage)
        {
            if (_templateMask == null)
                _templateMask = new UMat();
            cSrcImage.ToUMat().CopyTo(_templateMask);
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
        void SubPixFitParabola(ref PointF Result, PointF P1, PointF P2, PointF P3)
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


        public void SetMatchCenterPos(System.Drawing.Point MatchCenter)
        {
            _ModelLeftTop.X = MatchCenter.X - _ImageMatchModel.Width / 2;
            _ModelLeftTop.Y = MatchCenter.Y - _ImageMatchModel.Height / 2;
        }


        public void SetMatchModel(Image<Gray, byte> cSrcImage, System.Drawing.Point stLeftTop, bool bLearnRotate = false)
        {
            _ImageMatchModel = cSrcImage.Copy();
            _ModelLeftTop = stLeftTop;
            _MatchSuccess = false;

            if (!bLearnRotate)
                return;

            int nX = cSrcImage.ROI.X - 20;
            int nY = cSrcImage.ROI.Y - 20;
            int nW = cSrcImage.ROI.Width + 40;
            int nH = cSrcImage.ROI.Height + 40;

            cSrcImage.ROI = Rectangle.Empty;

            if (nX < 0)
                nX = 0;
            if (nY < 0)
                nY = 0;

            if (nX + nW > cSrcImage.Width)
                nW = cSrcImage.Width - nX;
            if (nY + nH > cSrcImage.Height)
                nH = cSrcImage.Height - nY;

            cSrcImage.ROI = new Rectangle(nX, nY, nW, nH);
            _ImageMatchRotateModel = cSrcImage.Copy();
        }

        public void SetScore(double fScore)
        {
            _Score = fScore;
        }


        public double GetScore()
        {
            return _Score;
        }


        public int GetWidth()
        {
            if (_ImageMatchModel == null)
                return 0;

            return _ImageMatchModel.Width;
        }

        public int GetHeight()
        {
            if (_ImageMatchModel == null)
                return 0;

            return _ImageMatchModel.Height;
        }

        public void MatchByAngle(Image<Gray, byte> cSrcImage, double fAngle)
        {
            if (_ImageMatchModel == null || cSrcImage == null)
                return;

            Image<Gray, byte> cImageRotate = _ImageMatchRotateModel.Rotate(fAngle, new PointF(_ImageMatchRotateModel.Width / 2, _ImageMatchRotateModel.Height / 2), Inter.Linear, new Gray(0), true);
            cImageRotate.ROI = new Rectangle(cImageRotate.ROI.X + 20, cImageRotate.ROI.Y + 20, cImageRotate.ROI.Width - 40, cImageRotate.Height - 40);

            Image<Gray, float> ret = cSrcImage.MatchTemplate(cImageRotate, TemplateMatchingType.CcoeffNormed);

            double fMin = 0, fMax = 0;
            Point stMaxp = new Point(0, 0);
            Point stMinp = new Point(0, 0);
            CvInvoke.MinMaxLoc(ret, ref fMin, ref fMax, ref stMinp, ref stMaxp);

            MinMaxLocSubPix(stMaxp, ret.Mat, ref _ResultLeftTop);
            ret.Dispose();

            if (fMax > _Score - 0.1)
                _MatchSuccess = true;
            else
                _MatchSuccess = false;
        }

        public Image<Gray, float> ScaleImage(Image<Gray, float> inputImage)
        {
            double[] minValue;
            double[] maxValue;
            Point[] minLocation;
            Point[] maxLocation;

            Image<Gray, float> scaledImage = inputImage.Clone();

            scaledImage.MinMax(out minValue, out maxValue, out minLocation, out maxLocation);

            double midValue = (minValue[0] + maxValue[0]) / 2;
            double rangeValue = (maxValue[0]) - (minValue[0]);
            double scaleFactor = 1 / rangeValue;
            double shiftFactor = midValue;

            Image<Gray, float> scaledImage1 = scaledImage.ConvertScale<float>(1.0, Math.Abs(minValue[0]));
            Image<Gray, float> scaledImage2 = scaledImage1.ConvertScale<float>(scaleFactor * 255, 0);

            return scaledImage2;
        }






        public void Match(Image<Gray, byte> cSrcImage)
        {
            if (_ImageMatchModel == null || cSrcImage == null)
                return;



            // cSrcImage.ROI = new Rectangle(nX, nY, nW, nH);

            // Matrix<float> ret = new Matrix<float>(  cSrcImage.Cols - _ImageMatchModel.Width + 1, cSrcImage.Rows - _ImageMatchModel.Height + 1);

            Image<Gray, float> ret = new Image<Gray, float>(cSrcImage.Width - _ImageMatchModel.Width + 1, cSrcImage.Height - _ImageMatchModel.Height + 1);

            CvInvoke.MatchTemplate(cSrcImage, _ImageMatchModel, ret, Emgu.CV.CvEnum.TemplateMatchingType.CcorrNormed, _templateMask);
            double fMin = 0, fMax = 0;
            Point stMaxp = new Point(0, 0);
            Point stMinp = new Point(0, 0);
            //   CvInvoke.Imshow("ret", ret);
            CvInvoke.MinMaxLoc(ret, ref fMin, ref fMax, ref stMinp, ref stMaxp);


            //    CvInvoke.ConvertScaleAbs(ret, ret, 255 / fMax, 0);
            //    CvInvoke.Imshow("ConvertScaleAbs", ret);
            //      Image<Gray, byte> img2 = ret.Convert<Gray, byte>();

            //       CvInvoke.Imwrite("D:\\1.bmp", img2);
            //     CvInvoke.Imshow("img2", ret);
            //  CvInvoke.MinMaxLoc(ret, ref fMin, ref fMax, ref stMinp, ref stMaxp);
            //ret.MinMax(out fMin,out fMax,out stMinp,out stMaxp);
            ret.Dispose();


            if (fMax > _Score)
                _MatchSuccess = true;
            else
                _MatchSuccess = false;

            _ResultLeftTop = stMaxp;
            //_ResultLeftTop.X += cSrcImage.ROI.X;
            //_ResultLeftTop.Y += cSrcImage.ROI.Y;


            //cSrcImage.ROI = Rectangle.Empty;

        }

        public bool MatchTest(Image<Gray, byte> cSrcImage, double fLowerScore, ref double fResultScore)
        {
            if (_ImageMatchModel == null || cSrcImage == null)
                return false;

            //Image<Gray, float> cSobelImageFH = _ImageMatchModel.Sobel(1, 0, 3);
            //Image<Gray, float> cSobelImageFV = _ImageMatchModel.Sobel(0, 1, 3);
            //Image<Gray, float> cSobelImageF = cSobelImageFH + cSobelImageFV;
            //Image<Gray, byte> cSobelImage = cSobelImageF.ConvertScale<byte>(1, 0);

            //Image<Gray, byte> cThImage = cSobelImage.ThresholdBinary(new Gray(100), new Gray(255));

            //int nCount = CvInvoke.CountNonZero(cThImage);

            //cSobelImageFH.Dispose();
            //cSobelImageFV.Dispose();
            //cSobelImageF.Dispose();
            //cSobelImage.Dispose();
            //cThImage.Dispose();

            //if (nCount < _ImageMatchModel.Width * _ImageMatchModel.Height * 0.01)
            //    return false;

            Image<Gray, byte> cTestImage = cSrcImage.Copy();

            cTestImage.ROI = new Rectangle(_ModelLeftTop.X, _ModelLeftTop.Y, _ImageMatchModel.Width, _ImageMatchModel.Height);
            cTestImage.SetZero();
            cTestImage.ROI = Rectangle.Empty;

            Image<Gray, float> ret = new Image<Gray, float>(cSrcImage.Cols, cSrcImage.Rows);

            //     CvInvoke.MatchTemplate(cTestImage, _ImageMatchModel, ret, Emgu.CV.CvEnum.TemplateMatchingType.CcorrNormed, _templateMask);

            ret = cTestImage.MatchTemplate(_ImageMatchModel, TemplateMatchingType.CcoeffNormed);

            double fMin = 0, fMax = 0;
            Point stMaxp = new Point(0, 0);
            Point stMinp = new Point(0, 0);
            CvInvoke.MinMaxLoc(ret, ref fMin, ref fMax, ref stMinp, ref stMaxp);

            cTestImage.Dispose();
            ret.Dispose();

            fResultScore = fMax;
            if (fMax < fLowerScore)
                return true;

            return false;
        }

        public bool Success()
        {
            return _MatchSuccess;
        }

        public PointF GetResultCenterPoint()
        {
            if (!_MatchSuccess)
                return new Point(0, 0);

            return new PointF(_ResultLeftTop.X + _ImageMatchModel.Width / 2, _ResultLeftTop.Y + _ImageMatchModel.Height / 2);
        }

        public Point GetLearnLeftTopPoint()
        {
            return _ModelLeftTop;
        }

        public Point GetLearnCenterPoint()
        {
            return new Point(_ModelLeftTop.X + _ImageMatchModel.Width / 2, _ModelLeftTop.Y + _ImageMatchModel.Height / 2);
        }





        public bool Load(String sFolderPath)
        {
            String sImageModelPath = sFolderPath + "\\Match.bmp";
            String sImageModelRotatePath = sFolderPath + "\\MatchRotate.bmp";
            String sImageModelResultPath = sFolderPath + "\\MatchResult.bmp";
            String sImageModelMaskPath = sFolderPath + "\\Mask.bmp";
            String sSettinPath = sFolderPath + "\\Match.ini";
            if (!File.Exists(sSettinPath))
                return false;
            if (!File.Exists(sImageModelPath))
                return false;


            try
            {
                //    _ResultMatchModel= new Image<Gray, byte>(sImageModelResultPath);
                _ImageMatchModel = new Image<Gray, byte>(sImageModelPath);
                _ImageMatchRotateModel = new Image<Gray, byte>(sImageModelRotatePath);
                if (File.Exists(sImageModelMaskPath))
                {
                    if (_templateMask != null)
                        _templateMask.Dispose();

                    _templateMask = CvInvoke.Imread(sImageModelMaskPath, ImreadModes.Grayscale).GetUMat(AccessType.Fast);
                }
            }
            catch
            {
                return false;
            }

            FileStreamLibrary.IniFile cIni = new FileStreamLibrary.IniFile(sSettinPath, true);

            _Score = cIni.ReadDouble("Match", "Score", (double)0.8);
            _ModelLeftTop.X = cIni.ReadInt("Match", "LeftTopX", 0);
            _ModelLeftTop.Y = cIni.ReadInt("Match", "LeftTopY", 0);


            //string roi= cIni.ReadStr("Match", "ROI", "0,0,0,0");
            //String[] split = roi.Split(',');
            //_ROI = new Rectangle(Int32.Parse(split[0]), Int32.Parse(split[1]), Int32.Parse (split[2]), Int32.Parse (split[3]));
            cIni.FileClose();
            cIni.Dispose();
            return true;

        }

        public bool Save(String sFolderPath)
        {
            if (!Directory.Exists(sFolderPath))
                Directory.CreateDirectory(sFolderPath);

            String sImageModelPath = sFolderPath + "\\Match.bmp";
            String sImageModelRotatePath = sFolderPath + "\\MatchRotate.bmp";
            String sImageModelMaskPath = sFolderPath + "\\Mask.bmp";
            String sSettinPath = sFolderPath + "\\Match.ini";

            try
            {
                if (_ImageMatchModel == null)
                    return false;


                _ImageMatchModel.Save(sImageModelPath);
                _ImageMatchRotateModel.Save(sImageModelRotatePath);

                if (_templateMask != null)
                    _templateMask.Save(sImageModelMaskPath);
            }
            catch
            {
                return false;
            }

            FileStreamLibrary.IniFile cIni = new FileStreamLibrary.IniFile(sSettinPath, false);

            cIni.WriteDouble("Match", "Score", (double)_Score);
            cIni.WriteInt("Match", "LeftTopX", _ModelLeftTop.X);
            cIni.WriteInt("Match", "LeftTopY", _ModelLeftTop.Y);

            cIni.FileClose();
            cIni.Dispose();

            return true;

        }
        public bool SaveThreshold(String sFolderPath)
        {
            if (!Directory.Exists(sFolderPath))
                Directory.CreateDirectory(sFolderPath);

            String sSettinPath = sFolderPath + "\\Match.ini";

            try
            {
                FileStreamLibrary.IniFile cIni = new FileStreamLibrary.IniFile(sSettinPath, false);

                cIni.FileClose();
                cIni.Dispose();

                return true;
            }
            catch
            {
                return false;
            }

        }
    }
}

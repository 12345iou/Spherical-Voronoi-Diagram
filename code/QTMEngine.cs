using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MME.Globe.Core.MathLib;
using Microsoft.DirectX;

namespace MME.Global.QTM
{
    /// <summary>
    /// 基于顺序编码的三邻近
    /// </summary>
    public struct EdgeNeighbours
    {
        public int TopNeighbour;
        public int LeftNeighbour;
        public int RightNeighbour;
    }
    /// <summary>
    /// 基于四进制编码的三邻近
    /// </summary>
    public struct EdgeNeighbour
    {
        public string Top;
        public string Left;
        public string Right;
    }
    /// <summary>
    /// 三角形结构体（编码为四进制）
    /// </summary>
    public struct Triangle
    {
        public Vector3 Point1;
        public Vector3 Point2;
        public Vector3 Point3;
        /// <summary>
        /// 三角形的中心点坐标
        /// </summary>
        public Vector3 Center;
        /// <summary>
        /// 三角形的编码
        /// </summary>
        public string Code;
        /// <summary>
        /// 三角形归属种子点
        /// </summary>
        public int zhongzidian;
        /// <summary>
        /// 三角形距最近种子点距离
        /// </summary>
        public double juli;
   
        public Triangle(Vector3 p1, Vector3 p2, Vector3 p3, string code)
        {
            this.Point1 = p1;
            this.Point2 = p2;
            this.Point3 = p3;
            this.Center = new Vector3((p1.X + p2.X + p3.X) / 3.0f, (p1.Y + p2.Y + p3.Y) / 3.0f, (p1.Z + p2.Z + p3.Z) / 3.0f);
            this.Code = code;
                /////
            this.zhongzidian = -1;
            this.juli = 999999999999999999;
            /////
        }
       
    }


    public class QTMEngine
    {
        #region 顺序编码的相关算法
        /// <summary>
        /// 由八分码和经度计算在该八分体内的相对经度
        /// </summary>
        /// <param name="octa">八分码</param>
        /// <param name="lon">绝对经度</param>
        /// <returns></returns>
        public static double LonInOcta(int octa, double realLon)
        {
            double lon = realLon;
            switch (octa)
            {
                case 0:
                    lon = realLon; break;
                case 1:
                    lon = realLon - 90; break;
                case 2:
                    lon = realLon + 180; break;
                case 3:
                    lon = realLon + 90; break;
                case 4:
                    lon = 90 - realLon; break;
                case 5:
                    lon = 180 - realLon; break;
                case 6:
                    lon = -90 - realLon; break;
                case 7:
                    lon = -realLon; break;
            }
            return lon;
        }
        /// <summary>
        /// 计算八分体内的相对纬度
        /// </summary>
        /// <param name="lat">绝对纬度</param>
        /// <returns></returns>
        public static double LatInOcta(double lat)
        {
            return Math.Abs(lat);
        }
        /// <summary>
        /// 从经纬度得到八分体号
        /// </summary>
        /// <param name="lat">纬度</param>
        /// <param name="lon">经度</param>
        /// <returns>八分体号</returns>
        public static int GetOctaFromLatLon(double lat, double lon)
        {
            if (lat >= 0)
            {
                if (lon >= 0 && lon <= 90)
                    return 0;
                else if (lon > 90 && lon <= 180)
                    return 1;
                else if (lon >= -180 && lon <= -90)
                    return 2;
                else if (lon > -90 && lon < 0)
                    return 3;
            }
            else
            {
                if (lon >= 0 && lon <= 90)
                    return 4;
                else if (lon > 90 && lon <= 180)
                    return 5;
                else if (lon >= -180 && lon <= -90)
                    return 6;
                else if (lon > -90 && lon < 0)
                    return 7;
            }
            return 0;
        }
        /// <summary>
        /// 根据经纬度得到编码
        /// </summary>
        /// <param name="lat">经度</param>
        /// <param name="lon">纬度</param>
        /// <param name="level">格网层次</param>
        /// <returns>编码</returns>
        public static int GetCodeFromLatLon(double lat, double lon, int level)
        {
            int octaTriCount = (int)Math.Pow(4, level);
            int octa = GetOctaFromLatLon(lat, lon);
            double octaLat = LatInOcta(lat);
            double octaLon = LonInOcta(octa, lon);
            int rowCount = (int)Math.Pow(2, level);
            int row = (int)((90.0 - octaLat) / 90.0 * rowCount);
            int tempCol = (int)(octaLon / 90.0 * (row + 1));
            double north = 90.0 - 90.0 * row / rowCount;//y2
            double south = 90.0 - 90.0 * (row + 1) / rowCount;//y1
            double upLon = 90.0 / row * tempCol;//x2
            double leftLon = 90.0 / (row + 1) * tempCol;//x1
            double rightLon = 90.0 / (row + 1) * (tempCol + 1);

            double A = north - south;
            double B1 = leftLon - upLon;
            double C1 = south * upLon - north * leftLon;

            double B2 = rightLon - upLon;
            double C2 = south * upLon - north * rightLon;
            double D1 = A * octaLon + B1 * octaLat + C1;
            double D2 = A * octaLon + B2 * octaLat + C2;
            int col = -1;
            if (D1 <= 0)
            {
                col = 2 * tempCol - 1; ;
            }
            else if (D1 > 0 && D2 <= 0)
            {
                col = 2 * tempCol;
            }
            else if (D2 > 0)
            {
                col = 2 * tempCol + 1;
            }
            int code = row * row + col;
            code += octa * octaTriCount;
            return code;
        }
        public static List<int> GetTrianglesOnLine(double startLat, double startLon, double endLat, double endLon, int level)
        {
            double length = MathEngine.SphericalDistanceDegrees(startLat, startLon, endLat, endLon);
            double triLength = 90.0 / Math.Pow(2, level);
            int count = (int)(length / triLength * 2);
            double deltaLat = (endLat - startLat) / count;
            double deltaLon = (endLon - startLon) / count;
            List<int> triangleList = new List<int>();
            int startCode = GetCodeFromLatLon(startLat, startLon, level);
            if (!triangleList.Contains(startCode))
            {
                triangleList.Add(startCode);
            }
            for (int i = 0; i < count; i++)
            {
                double curLat = startLat + deltaLat * i;
                double curLon = startLon + deltaLon * i;
                int code = GetCodeFromLatLon(curLat, curLon, level);
                if (code != triangleList[triangleList.Count - 1])
                {
                    triangleList.Add(code);
                }
            }
            int endCode = GetCodeFromLatLon(endLat, endLon, level);
            if (endCode != triangleList[triangleList.Count - 1])
            {
                triangleList.Add(endCode);
            }
            return triangleList;
        }
        /// <summary>
        /// 三邻近搜索
        /// </summary>
        /// <param name="level">层次</param>
        /// <param name="center">中心三角形编号</param>
        /// <returns>边邻近三角形</returns>
        public static EdgeNeighbours EdgeNeighbourSearch(int level, int center)
        {
            int octaTriCount = 1 << (level << 1);
            int octa = center >> (2 * level);//八分体编号
            int octaID = center - octa * octaTriCount;//该ID在八分体内的编号
            int row = (int)Math.Sqrt((double)octaID); //在八分体内的行号
            int col = octaID - row * row; //在八分体内的列号
            int rowCount = 1 << level; //八分体的总行数
            int colCount = 2 * row + 1; //所在行的总列数)
            int leftTri = -1, rightTri = -1, topTri = -1;
            int rightOcta = -1;
            if (octa == 0 || octa == 1 || octa == 2)
                rightOcta = octa + 1;
            if (octa == 5 || octa == 6 || octa == 7)
                rightOcta = octa - 1;
            else if (octa == 3)
                rightOcta = 0;
            else if (octa == 4)
                rightOcta = 7;

            int leftOcta = -1;
            if (octa == 1 || octa == 2 || octa == 3)
                leftOcta = octa - 1;
            if (octa == 4 || octa == 5 || octa == 6)
                leftOcta = octa + 1;
            else if (octa == 0)
                leftOcta = 3;
            else if (octa == 7)
                leftOcta = 4;

            int topOcta = -1;
            if (octa < 4)
            {
                topOcta = octa + 4;
            }
            else
            {
                topOcta = octa - 4;
            }

            if (row == 0)//三角形B
            {
                rightTri = rightOcta * octaTriCount;
                leftTri = leftOcta * octaTriCount;
                topTri = center + 2;
            }
            else if (row > 0 && row < rowCount - 1 && col == 0)//三角形E
            {
                rightTri = center + 1;
                topTri = center + colCount + 1;
                leftTri = leftOcta * octaTriCount + octaID + colCount - 1;
            }
            else if (row == rowCount - 1 && col == 0)//三角形C
            {
                rightTri = center + 1;
                leftTri = leftOcta * octaTriCount + octaID + colCount - 1;
                topTri = topOcta * octaTriCount + octaTriCount - 1;
            }
            else if (row == rowCount - 1 && col > 0 && col < colCount - 1 && col % 2 == 0)//三角形G
            {
                leftTri = center - 1;
                rightTri = center + 1;
                topTri = topOcta * octaTriCount + octaTriCount - col - 1;
            }
            else if (row == rowCount - 1 && col == colCount - 1)//三角形D
            {
                leftTri = center - 1;
                rightTri = rightOcta * octaTriCount + octaTriCount - colCount;
                topTri = topOcta * octaTriCount + octaTriCount - colCount;
            }
            else if (row > 0 && row < rowCount - 1 && col == colCount - 1)//三角形F
            {
                leftTri = center - 1;
                topTri = center + colCount + 1;
                rightTri = rightOcta * octaTriCount + octaID - colCount + 1;
            }
            else
            {
                leftTri = center - 1;
                rightTri = center + 1;
                if (col % 2 == 0)
                {
                    topTri = center + colCount + 1;
                }
                else
                {
                    topTri = center - colCount + 1;
                }
            }
            EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
            edgeNeighbours.TopNeighbour = topTri;
            edgeNeighbours.LeftNeighbour = leftTri;
            edgeNeighbours.RightNeighbour = rightTri;
            return edgeNeighbours;
        }
        /// <summary>
        /// 十二（十）邻近搜索
        /// </summary>
        /// <param name="level">层次</param>
        /// <param name="center">中心三角形编号</param>
        /// <returns>十二（十）邻近三角形</returns>
        public static List<int> AllNeighbourSearch(int level, int center)
        {
            int octaTriCount = 1 << (level << 1);
            int octa = center >> (2 * level);//八分体编号
            int octaID = center - octa * octaTriCount;//该ID在八分体内的编号
            int row = (int)Math.Sqrt((double)octaID); //在八分体内的行号
            int col = octaID - row * row; //在八分体内的列号
            int rowCount = 1 << level; //八分体的总行数
            int colCount = 2 * row + 1; //所在行的总列数)
            List<int> allNeighbours = new List<int>();
            EdgeNeighbours edgeNeighbours = EdgeNeighbourSearch(level, center);
            allNeighbours.Add(edgeNeighbours.LeftNeighbour);
            allNeighbours.Add(edgeNeighbours.RightNeighbour);
            allNeighbours.Add(edgeNeighbours.TopNeighbour);
            EdgeNeighbours rightTriNeighbour = EdgeNeighbourSearch(level, edgeNeighbours.RightNeighbour);
            EdgeNeighbours leftTriNeighbour = EdgeNeighbourSearch(level, edgeNeighbours.LeftNeighbour);
            EdgeNeighbours topTriNeighbour = EdgeNeighbourSearch(level, edgeNeighbours.TopNeighbour);
            allNeighbours.Add(rightTriNeighbour.TopNeighbour);
            allNeighbours.Add(rightTriNeighbour.RightNeighbour);
            allNeighbours.Add(leftTriNeighbour.TopNeighbour);
            allNeighbours.Add(leftTriNeighbour.LeftNeighbour);
            allNeighbours.Add(topTriNeighbour.LeftNeighbour);
            allNeighbours.Add(topTriNeighbour.RightNeighbour);

            EdgeNeighbours leftTriTopNeighbour = EdgeNeighbourSearch(level, leftTriNeighbour.TopNeighbour);
            if ((col % 2 == 1) && row == rowCount - 1)
                allNeighbours.Add(leftTriTopNeighbour.LeftNeighbour);
            else if (col == 0 && row == rowCount - 1)
            {
                int leftTriLeftTopNeighbour = EdgeNeighbourSearch(level, leftTriNeighbour.LeftNeighbour).TopNeighbour;
                allNeighbours.Add(leftTriLeftTopNeighbour);
            }
            else
                allNeighbours.Add(leftTriTopNeighbour.RightNeighbour);


            EdgeNeighbours topTriRightNeighbour = EdgeNeighbourSearch(level, topTriNeighbour.RightNeighbour);
            if (col == colCount - 2)
            {
                allNeighbours.Add(topTriRightNeighbour.TopNeighbour);
            }
            else
            {
                if (!(col == 0 && row == rowCount - 1))
                {
                    allNeighbours.Add(topTriRightNeighbour.RightNeighbour);
                }
            }

            if ((!(row == 0 && col == 0)) && (!(row == rowCount - 1 && col == colCount - 1)))
            {
                EdgeNeighbours topTriLeftNeighbour = EdgeNeighbourSearch(level, topTriNeighbour.LeftNeighbour);
                if (col == 1)
                {
                    allNeighbours.Add(topTriLeftNeighbour.TopNeighbour);
                }
                else
                {
                    allNeighbours.Add(topTriLeftNeighbour.LeftNeighbour);
                }
            }
            if (col == 0 && row != 0 && row != rowCount - 1)
            {
                int leftTriLeftTopNeighbour = EdgeNeighbourSearch(level, leftTriNeighbour.LeftNeighbour).TopNeighbour;
                allNeighbours.Add(leftTriLeftTopNeighbour);
            }

            return allNeighbours;
        }
        public static bool IsBoundary(int center, int level, int[] voronoiIDs)
        {
            int octaTriCount = 1 << (level << 1);
            int octa = center >> (2 * level);//八分体编号
            int octaID = center - octa * octaTriCount;//该ID在八分体内的编号
            int row = (int)Math.Sqrt((double)octaID); //在八分体内的行号
            int col = octaID - row * row; //在八分体内的列号
            int rowCount = 1 << level; //八分体的总行数
            int colCount = 2 * row + 1; //所在行的总列数)
            int leftTri = -1, rightTri = -1, topTri = -1;
            int rightOcta = -1;
            if (octa == 0 || octa == 1 || octa == 2)
                rightOcta = octa + 1;
            if (octa == 5 || octa == 6 || octa == 7)
                rightOcta = octa - 1;
            else if (octa == 3)
                rightOcta = 0;
            else if (octa == 4)
                rightOcta = 7;

            int leftOcta = -1;
            if (octa == 1 || octa == 2 || octa == 3)
                leftOcta = octa - 1;
            if (octa == 4 || octa == 5 || octa == 6)
                leftOcta = octa + 1;
            else if (octa == 0)
                leftOcta = 3;
            else if (octa == 7)
                leftOcta = 4;

            int topOcta = -1;
            if (octa < 4)
            {
                topOcta = octa + 4;
            }
            else
            {
                topOcta = octa - 4;
            }

            if (row == 0)//三角形B
            {
                rightTri = rightOcta * octaTriCount;
                leftTri = leftOcta * octaTriCount;
                topTri = center + 2;
            }
            else if (row > 0 && row < rowCount - 1 && col == 0)//三角形E
            {
                rightTri = center + 1;
                topTri = center + colCount + 1;
                leftTri = leftOcta * octaTriCount + octaID + colCount - 1;
            }
            else if (row == rowCount - 1 && col == 0)//三角形C
            {
                rightTri = center + 1;
                leftTri = leftOcta * octaTriCount + octaID + colCount - 1;
                topTri = topOcta * octaTriCount + octaTriCount - 1;
            }
            else if (row == rowCount - 1 && col > 0 && col < colCount - 1 && col % 2 == 0)//三角形G
            {
                leftTri = center - 1;
                rightTri = center + 1;
                topTri = topOcta * octaTriCount + octaTriCount - col - 1;
            }
            else if (row == rowCount - 1 && col == colCount - 1)//三角形D
            {
                leftTri = center - 1;
                rightTri = rightOcta * octaTriCount + octaTriCount - colCount;
                topTri = topOcta * octaTriCount + octaTriCount - colCount;
            }
            else if (row > 0 && row < rowCount - 1 && col == colCount - 1)//三角形F
            {
                leftTri = center - 1;
                topTri = center + colCount + 1;
                rightTri = rightOcta * octaTriCount + octaID - colCount + 1;
            }
            else
            {
                leftTri = center - 1;
                rightTri = center + 1;
                if (col % 2 == 0)
                {
                    topTri = center + colCount + 1;
                }
                else
                {
                    topTri = center - colCount + 1;
                }
            }
            int c = voronoiIDs[center];
            int l = voronoiIDs[leftTri];
            int r = voronoiIDs[rightTri];
            int t = voronoiIDs[topTri];
            if (c == l && l == r && r == t)
                return false;
            return true;
        }
        #endregion

        #region 四进制编码的相关算法
        #region 邻近搜索子方法
        private static string GetInnerTop(string address, int level)
        {
            char[] innerTop = address.ToCharArray();
            for (int i = level; i > 0; i--)
            {
                if (address[i] == '0')
                {
                    innerTop[i] = '1';
                    break;
                }
                else if (address[i] == '1')
                {
                    innerTop[i] = '0';
                    break;
                }
            }
            string result = "";
            for (int i = 0; i < innerTop.Length; i++)
            {
                result += innerTop[i];
            }
            return result;
        }
        private static string GetInnerLeft(string address, int level)
        {
            char[] innerLeft = address.ToCharArray();
            for (int i = level; i > 0; i--)
            {
                if (address[i] == '1')
                {
                    innerLeft[i] = '3';
                }
                else if (address[i] == '2')
                {
                    innerLeft[i] = '1';
                }
                else if (address[i] == '0')
                {
                    innerLeft[i] = '2';
                    break;
                }
                else if (address[i] == '3')
                {
                    innerLeft[i] = '0';
                    break;
                }
            }
            string result = "";
            for (int i = 0; i < innerLeft.Length; i++)
            {
                result += innerLeft[i];
            }
            return result;
        }
        private static string GetInnerRight(string address, int level)
        {
            char[] innerRight = address.ToCharArray();
            for (int i = level; i > 0; i--)
            {
                if (address[i] == '1')
                {
                    innerRight[i] = '2';
                }
                else if (address[i] == '3')
                {
                    innerRight[i] = '1';
                }
                else if (address[i] == '0')
                {
                    innerRight[i] = '3';
                    break;
                }
                else if (address[i] == '2')
                {
                    innerRight[i] = '0';
                    break;
                }
            }
            string result = "";
            for (int i = 0; i < innerRight.Length; i++)
            {
                result += innerRight[i];
            }
            return result;
        }
        private static string GetOuterTop(string address, int level)
        {
            char[] outerTop = address.ToCharArray();
            if (address[0] <= '3')
                outerTop[0] += (char)4;
            else if (address[0] >= '4')
                outerTop[0] -= (char)4;
            string result = "";
            for (int i = 0; i < outerTop.Length; i++)
            {
                result += outerTop[i];
            }
            return result;
        }
        private static string GetOuterLeft(string address, int level)
        {
            char[] outerLeft = address.ToCharArray();
            if (address[0] == '0' || address[0] == '4')
                outerLeft[0] += (char)3;
            else
                outerLeft[0] -= (char)1;
            for (int i = 1; i < level + 1; i++)
            {
                if (address[i] == '2')
                {
                    outerLeft[i] = '3';
                }
            }
            string result = "";
            for (int i = 0; i < outerLeft.Length; i++)
            {
                result += outerLeft[i];
            }
            return result;
        }
        private static string GetOuterRight(string address, int level)
        {
            char[] outerRight = address.ToCharArray();
            if (address[0] == '3' || address[0] == '7')
                outerRight[0] -= (char)3;
            else
                outerRight[0] += (char)1;
            for (int i = 1; i < level + 1; i++)
            {
                if (address[i] == '3')
                {
                    outerRight[i] = '2';
                }
            }
            string result = "";
            for (int i = 0; i < outerRight.Length; i++)
            {
                result += outerRight[i];
            }
            return result;
        }
        #endregion
        /// <summary>
        /// 基于四进制编码的三邻近搜索算法
        /// </summary>
        /// <param name="address">地址码</param>
        /// <param name="level">格网层次</param>
        /// <returns>三邻近格网的编码</returns>
        public static EdgeNeighbour GetEdgeNeighbours(string address, int level)
        {
            EdgeNeighbour neighbour;
            string subAddress = address.Substring(1);
            if (subAddress.Contains('0'))
            {
                neighbour.Top = GetInnerTop(address, level);
                neighbour.Left = GetInnerLeft(address, level);
                neighbour.Right = GetInnerRight(address, level);
            }
            else if (subAddress.Contains('1'))
            {
                if (!subAddress.Contains('2'))
                {
                    if (!subAddress.Contains('3'))
                    {
                        neighbour.Top = GetInnerTop(address, level);
                        neighbour.Left = GetOuterLeft(address, level);
                        neighbour.Right = GetOuterRight(address, level);//1
                    }
                    else
                    {
                        neighbour.Top = GetInnerTop(address, level);
                        neighbour.Left = GetInnerLeft(address, level);
                        neighbour.Right = GetOuterRight(address, level);//13
                    }
                }
                else
                {
                    if (!subAddress.Contains('3'))
                    {
                        neighbour.Top = GetInnerTop(address, level);
                        neighbour.Left = GetOuterLeft(address, level);
                        neighbour.Right = GetInnerRight(address, level);//12
                    }
                    else
                    {
                        neighbour.Top = GetInnerTop(address, level);
                        neighbour.Left = GetInnerLeft(address, level);
                        neighbour.Right = GetInnerRight(address, level);//123
                    }
                }
            }
            else
            {
                if (!subAddress.Contains('2'))
                {
                    if (subAddress.Contains('3'))
                    {
                        neighbour.Top = GetOuterTop(address, level);
                        neighbour.Left = GetInnerLeft(address, level);
                        neighbour.Right = GetOuterRight(address, level);//3
                    }
                    else
                    {
                        neighbour.Top = GetInnerTop(address, level);
                        neighbour.Left = GetInnerLeft(address, level);
                        neighbour.Right = GetInnerRight(address, level);//只包含0，不会被执行到
                    }
                }
                else
                {
                    if (subAddress.Contains('3'))
                    {
                        neighbour.Top = GetOuterTop(address, level);
                        neighbour.Left = GetInnerLeft(address, level);
                        neighbour.Right = GetInnerRight(address, level);//23
                    }
                    else
                    {
                        neighbour.Top = GetOuterTop(address, level);
                        neighbour.Left = GetOuterLeft(address, level);
                        neighbour.Right = GetInnerRight(address, level);//2
                    }
                }
            }
            return neighbour;
        }       
        /// <summary>
        /// 基于四进制编码的十二邻近搜索算法
        /// </summary>
        /// <param name="address">地址码</param>
        /// <param name="level">格网层次</param>
        /// <returns>十二邻近格网的编码列表</returns>
        public static List<string> GetAllNeighbours(string address, int level)
        {
            string subAddress = address.Substring(1);
            List<string> allNeighbour = new List<string>();
            EdgeNeighbour edgeNeighbour = GetEdgeNeighbours(address, level);
            allNeighbour.Add(edgeNeighbour.Left);
            allNeighbour.Add(edgeNeighbour.Right);
            allNeighbour.Add(edgeNeighbour.Top);
            if (subAddress.Contains('1') && (!subAddress.Contains('0')) && (!subAddress.Contains('2')) && (!subAddress.Contains('3')))
            {
                EdgeNeighbour T = GetEdgeNeighbours(edgeNeighbour.Top, level);
                allNeighbour.Add(T.Left);
                allNeighbour.Add(T.Right);
                EdgeNeighbour L = GetEdgeNeighbours(edgeNeighbour.Left, level);
                allNeighbour.Add(L.Top);
                allNeighbour.Add(L.Left);
                EdgeNeighbour R = GetEdgeNeighbours(edgeNeighbour.Right, level);
                allNeighbour.Add(R.Top);
                EdgeNeighbour TL = GetEdgeNeighbours(T.Left, level);
                allNeighbour.Add(TL.Left);
                EdgeNeighbour TR = GetEdgeNeighbours(T.Right, level);
                allNeighbour.Add(TR.Right);
            }
            else if (subAddress.Contains('2') && (!subAddress.Contains('0')) && (!subAddress.Contains('1')) && (!subAddress.Contains('3')))
            {
                EdgeNeighbour T = GetEdgeNeighbours(edgeNeighbour.Top, level);
                allNeighbour.Add(T.Left);
                allNeighbour.Add(T.Right);
                EdgeNeighbour L = GetEdgeNeighbours(edgeNeighbour.Left, level);
                allNeighbour.Add(L.Left);
                EdgeNeighbour R = GetEdgeNeighbours(edgeNeighbour.Right, level);
                allNeighbour.Add(R.Top);
                allNeighbour.Add(R.Right);
                EdgeNeighbour TR = GetEdgeNeighbours(T.Right, level);
                allNeighbour.Add(TR.Right);
                EdgeNeighbour RT = GetEdgeNeighbours(R.Top, level);
                allNeighbour.Add(RT.Left);
            }
            else if (subAddress.Contains('3') && (!subAddress.Contains('0')) && (!subAddress.Contains('1')) && (!subAddress.Contains('2')))
            {
                EdgeNeighbour T = GetEdgeNeighbours(edgeNeighbour.Top, level);
                allNeighbour.Add(T.Left);
                allNeighbour.Add(T.Right);
                EdgeNeighbour L = GetEdgeNeighbours(edgeNeighbour.Left, level);
                allNeighbour.Add(L.Top);
                allNeighbour.Add(L.Left);
                EdgeNeighbour R = GetEdgeNeighbours(edgeNeighbour.Right, level);
                allNeighbour.Add(R.Right);
                EdgeNeighbour TL = GetEdgeNeighbours(T.Left, level);
                allNeighbour.Add(TL.Left);
                EdgeNeighbour RR = GetEdgeNeighbours(R.Right, level);
                allNeighbour.Add(RR.Top);
            }
            else
            {
                EdgeNeighbour T = GetEdgeNeighbours(edgeNeighbour.Top, level);
                allNeighbour.Add(T.Left);
                allNeighbour.Add(T.Right);
                EdgeNeighbour L = GetEdgeNeighbours(edgeNeighbour.Left, level);
                allNeighbour.Add(L.Top);
                allNeighbour.Add(L.Left);
                EdgeNeighbour R = GetEdgeNeighbours(edgeNeighbour.Right, level);
                allNeighbour.Add(R.Top);
                allNeighbour.Add(R.Right);
                string subAddress2 = address.Substring(1, level - 1);
                string subLeftEdge = edgeNeighbour.Left.Substring(1);
                string subRightEdge = edgeNeighbour.Right.Substring(1);
                //次顶三角形
                if (subAddress2.Contains('1') && (!subAddress2.Contains('0')) && (!subAddress2.Contains('2')) && (!subAddress2.Contains('3')) && address[level] == '0')
                {
                    EdgeNeighbour TL = GetEdgeNeighbours(T.Left, level);
                    allNeighbour.Add(TL.Top);
                    EdgeNeighbour TR = GetEdgeNeighbours(T.Right, level);
                    allNeighbour.Add(TR.Top);
                    EdgeNeighbour LT = GetEdgeNeighbours(L.Top, level);
                    allNeighbour.Add(LT.Right);
                }
                //左次边三角形 根据左邻近判断
                else if (subLeftEdge.Contains('2') && (!subLeftEdge.Contains('0')) && (!subLeftEdge.Contains('3')))
                {
                    EdgeNeighbour TL = GetEdgeNeighbours(T.Left, level);
                    allNeighbour.Add(TL.Top);
                    EdgeNeighbour TR = GetEdgeNeighbours(T.Right, level);
                    allNeighbour.Add(TR.Right);
                    EdgeNeighbour LT = GetEdgeNeighbours(L.Top, level);
                    allNeighbour.Add(LT.Right);
                }
                //右次边三角形 根据右邻近判断
                else if (subRightEdge.Contains('3') && (!subRightEdge.Contains('0')) && (!subRightEdge.Contains('2')))
                {
                    EdgeNeighbour TL = GetEdgeNeighbours(T.Left, level);
                    allNeighbour.Add(TL.Left);
                    EdgeNeighbour TR = GetEdgeNeighbours(T.Right, level);
                    allNeighbour.Add(TR.Top);
                    EdgeNeighbour LT = GetEdgeNeighbours(L.Top, level);
                    allNeighbour.Add(LT.Right);
                }
                //左边三角形
                else if (subAddress.Contains('1') && (subAddress.Contains('2')) && (!subAddress.Contains('0')) && (!subAddress.Contains('3')))
                {
                    EdgeNeighbour TL = GetEdgeNeighbours(T.Left, level);
                    allNeighbour.Add(TL.Left);
                    EdgeNeighbour TR = GetEdgeNeighbours(T.Right, level);
                    allNeighbour.Add(TR.Right);
                    EdgeNeighbour LL = GetEdgeNeighbours(L.Left, level);
                    allNeighbour.Add(LL.Top);
                }
                //右边三角形 与 内三角形相同
                //else if (subAddress.Contains('1') && (subAddress.Contains('3')) && (!subAddress.Contains('0')) && (!subAddress.Contains('2')))
                //{
                //    EdgeNeighbour TL = GetEdgeNeighbours(T.Left, level);
                //    allNeighbour.Add(TL.Left);
                //    EdgeNeighbour TR = GetEdgeNeighbours(T.Right, level);
                //    allNeighbour.Add(TR.Right);
                //    EdgeNeighbour LT = GetEdgeNeighbours(L.Top, level);
                //    allNeighbour.Add(LT.Right);
                //}
                else
                {
                    EdgeNeighbour TL = GetEdgeNeighbours(T.Left, level);
                    allNeighbour.Add(TL.Left);
                    EdgeNeighbour TR = GetEdgeNeighbours(T.Right, level);
                    allNeighbour.Add(TR.Right);
                    EdgeNeighbour LT = GetEdgeNeighbours(L.Top, level);
                    allNeighbour.Add(LT.Right);
                }
            }
            return allNeighbour;
        }
        #endregion
    }
}

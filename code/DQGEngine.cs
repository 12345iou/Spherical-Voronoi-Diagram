using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MME.Globe.Core.MathLib;
using MME.Globe.Core.DEM;

namespace MME.Globe.Core.DQG
{
    public class DQGEngine
    {
        /// <summary>
        /// 判断一个字符串中是否只含有某个字符
        /// </summary>
        /// <param name="ch">字符</param>
        /// <param name="oriString">字符串</param>
        private static bool OnlyContains(char ch, string oriString)
        {
            for (int i = 0; i < oriString.Length; i++)
            {
                if (!oriString[i].Equals(ch))
                    return false;
            }
            return true;
        }
        /// <summary>
        /// 判断一个字符串中是否含有且仅含有某两个字符
        /// </summary>
        /// <param name="ch1">字符1</param>
        /// <param name="ch2">字符2</param>
        /// <param name="oriString">字符串</param>
        private static bool OnlyContains(char ch1, char ch2, string oriString)
        {
            if ((!oriString.Contains(ch1)) || (!oriString.Contains(ch2)))
                return false;
            for (int i = 0; i < oriString.Length; i++)
            {
                if ((!oriString[i].Equals(ch1)) && (!oriString[i].Equals(ch2)))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 计算Morton码
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public static string CalMortonCode(int row, int col, int level)
        {
            if (row == 0 && col == 1)
                col = 0;
            string rr = Convert.ToString(row, 2);
            string cc = Convert.ToString(col, 2);
            string arow = rr.PadLeft(level, '0');
            string acol = cc.PadLeft(level, '0');
            string morton = "";
            for (int i = 0; i < level; i++)
            {
                morton = morton + (Convert.ToInt32(arow.Substring(i, 1)) * 2 + Convert.ToInt32(acol.Substring(i, 1))).ToString();
            }
            return morton;
        }
        /// <summary>
        /// 由行号和层次计算经差
        /// </summary>
        /// <param name="level">层次</param>
        /// <param name="row">行号</param>
        /// <returns></returns>
        public static double CaldeltaLon(int level, int row)
        {
            int colCount = MaxCol(level, row) + 1;
            return 90.0 / (double)colCount;
        }
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
        /// 由Morton码计算行列号
        /// </summary>
        public static RowColNumber CalRowCol(string morton)
        {
            RowColNumber rc; rc.Row = 0; rc.Col = 0;
            if (OnlyContains('0', morton))
            {
                return rc;
            }
            Int32 Dmorton = 0; string Bmorton = ""; string _row = "", _col = "";
            //将morton变成十进制
            for (int i = 0; i < morton.Length; i++)
            {
                Dmorton += Convert.ToInt32(morton.Substring(i, 1)) * (int)Math.Pow(4.0, morton.Length - 1 - i);
            }
            //将十进制Morton变成二进制
            Bmorton = Convert.ToString(Dmorton, 2);
            for (int i = 0; i <= Bmorton.Length - 2; i = i + 2)
            {
                _row = Bmorton.Substring(Bmorton.Length - 2 - i, 1) + _row;
                _col = Bmorton.Substring(Bmorton.Length - 1 - i, 1) + _col;
            }
            //将二进制行列变成十进制
            for (int i = 0; i < _row.Length; i++)
            {
                rc.Row += Convert.ToInt32(_row.Substring(i, 1)) * (int)Math.Pow(2.0, _row.Length - 1 - i);
            }
            for (int i = 0; i < _col.Length; i++)
            {
                rc.Col += Convert.ToInt32(_col.Substring(i, 1)) * (int)Math.Pow(2.0, _col.Length - 1 - i);
            }
            return rc;
        }
        /// <summary>
        /// 根据Morton码计算格网的类型
        /// </summary>
        public static GridType GetTypeFromMorton(string morton)
        {
            if (double.Parse(morton) == 0)
                return GridType.A;
            else if (OnlyContains('2', morton))
                return GridType.B;
            else if (OnlyContains('3', morton))
                return GridType.C;
            else if (OnlyContains('0', '2', morton))
                return GridType.D;
            else if (OnlyContains('2', '3', morton))
                return GridType.F;
            int level = morton.Length;
            RowColNumber rc = CalRowCol(morton);
            if (rc.Col == MaxCol(level, rc.Row))
                return GridType.E;
            else return GridType.G;
        }
        /// <summary>
        /// Morton码中左第1位起至遇到非0时0的个数
        /// </summary>
        /// <param name="morton"></param>
        /// <returns></returns>
        private static int ZeroCount(string morton)
        {
            int count = 0;
            for (int i = 0; i < morton.Length; i++)
            {
                if (morton.Substring(i, 1) != "0")
                    break;
                else
                    count++;
            }
            return count;
        }
        /// <summary>
        /// 根据层号和行号得到该行最大的列号
        /// </summary>
        /// <param name="level">层号</param>
        /// <param name="row">行号</param>
        /// <returns>最大列号</returns>
        public static int MaxCol(int level, int row)
        {
            int[] maxCol = new int[(int)Math.Pow(2, level)];
            for (int i = 0; i < level; i++)
            {
                for (int j = (int)Math.Pow(2, i) + 1; j <= (int)Math.Pow(2, i + 1); j++)
                {
                    maxCol[j - 1] = (int)Math.Pow(2, i + 1) - 1;
                }
            }
            maxCol[0] = 1;
            return maxCol[row];
        }
        /// <summary>
        /// 根据点的经纬度及所在层次，得到该点所在瓦片的地址码
        /// </summary>
        /// <param name="lat">纬度</param>
        /// <param name="lon">经度</param>
        /// <param name="level">层次</param>
        /// <returns>地址码</returns>
        public static string GetAdressFromLatLon(double lat, double lon, int level)
        {
            string morton = GetMortonFromLatLon(lat, lon, level);
            int octa = GetOctaFromLatLon(lat, lon);
            return octa.ToString() + morton;
        }
        /// <summary>
        /// 在一个八分体内计算线段经过的DQG格网
        /// </summary>
        /// <param name="startP">起始点</param>
        /// <param name="endP">终止点</param>
        /// <param name="level">层号</param>
        /// <returns>线段所经过的格网的地址码</returns>
        public static List<string> GetDQGGridFromLine(LatLon startP, LatLon endP, int level)
        {
            List<string> adresses = new List<string>();
            RowColNumber startRC = GetRowColFromLatLon(startP.Latitude, startP.Longitude, level);
            RowColNumber endRC = GetRowColFromLatLon(endP.Latitude, endP.Longitude, level);
            int rowCount = endRC.Row - startRC.Row;
            int colCount = endRC.Col - startRC.Col;
            double factor = 0;
            int pointCount = 0;
            double deltaLat = 0;
            double deltaLon = 0;
            int OctaMaxCol = (int)Math.Pow(2, level);

            if (Math.Abs(rowCount) >= Math.Abs(colCount))
            {
                factor = Math.Abs((double)colCount / (double)rowCount);
                pointCount = Math.Abs(rowCount);
                deltaLat = rowCount / Math.Abs(rowCount);
                deltaLon = colCount / Math.Abs(colCount) * factor;
            }
            else
            {
                factor = Math.Abs((double)rowCount / (double)colCount);
                pointCount = Math.Abs(colCount);
                deltaLat = rowCount / Math.Abs(rowCount) * factor;
                deltaLon = colCount / Math.Abs(colCount);
            }

            for (int i = 0; i <= pointCount; i++)
            {
                int row = startRC.Row + (int)(deltaLat * i + 0.5);
                int col = startRC.Col + (int)(deltaLon * i + 0.5);
                int rowMaxCol = MaxCol(level, row);
                int index = OctaMaxCol / rowMaxCol;
                col /= index;
                string adress = GetOctaFromLatLon(startP.Latitude, startP.Longitude).ToString() + CalMortonCode(row, col, level);
                adresses.Add(adress);
            }
            //points.Add(GetAdressFromLatLon(endP.Latitude, endP.Longitude, level));
            return adresses;
        }


        private static double GetLatFromLon(LatLon startP, LatLon endP, double lon)
        {
            double lat = 0;
            double deltaLat = endP.Latitude - startP.Latitude;
            double deltaLon = endP.Longitude - startP.Longitude;
            if (Math.Abs(deltaLon) < double.Epsilon)
            {
                return -1000;
            }
            double k = deltaLat / deltaLon;
            lat = k * (lon - startP.Longitude) + startP.Latitude;
            return lat;
        }

        private static double GetLonFromLat(LatLon startP, LatLon endP, double lat)
        {
            double lon = 0;
            double deltaLat = endP.Latitude - startP.Latitude;
            double deltaLon = endP.Longitude - startP.Longitude;
            if (Math.Abs(deltaLat) < double.Epsilon)
            {
                return -1000;
            }
            double k = deltaLon / deltaLat;
            lon = k * (lat - startP.Latitude) + startP.Longitude;
            return lon;
        }

        private static double GetLatFromLon(LatLon startP, double k, double lon)
        {
            return k * (lon - startP.Longitude) + startP.Latitude;
        }

        private static double GetLonFromLat(LatLon startP, double k, double lat)
        {
            return (1.0 / k) * (lat - startP.Latitude) + startP.Longitude;
        }
        /// <summary>
        /// 在一个八分体内计算线段经过的DQG格网
        /// </summary>
        /// <param name="startP">起始点</param>
        /// <param name="endP">终止点</param>
        /// <param name="level">层号</param>
        /// <returns>线段所经过的格网的地址码</returns>
        public static List<GeoPoint> GetPointsFromLine(LatLon startP, LatLon endP, int level)
        {
            List<GeoPoint> points = new List<GeoPoint>();
            double deltaLon = endP.Longitude - startP.Longitude;
            double deltaLat = endP.Latitude - startP.Latitude;
            double quadCount = Math.Pow(2.0, DQGTileSet.BaseLevel);
            MinMaxLatLon minMaxLatLon = GetMinMaxFromLatLon(startP.Latitude, startP.Longitude, level);

            if (Math.Abs(deltaLon) > Math.Abs(deltaLat))
            {
                double tempLon = 0;
                double tempLat = 0;
                if (deltaLon > 0)
                {
                    tempLon = minMaxLatLon.MaxLon;
                    double lat = GetLatFromLon(startP, endP, tempLon);
                    if (lat >= minMaxLatLon.MinLat && lat < minMaxLatLon.MaxLat)
                    {
                        if (tempLon < endP.Longitude && tempLon > startP.Longitude)
                        {
                            double height = TerrainProvider.GetElevationAt(lat, tempLon, level);
                            points.Add(new GeoPoint(lat, tempLon, height));
                            points.AddRange(GetPointsFromLine(new LatLon(lat, tempLon + 0.000001), endP, level));
                        }
                    }
                    else
                    {
                        if (lat >= minMaxLatLon.MaxLat)
                        {
                            tempLat = minMaxLatLon.MaxLat;
                            if (tempLat < endP.Latitude && tempLat > startP.Latitude)
                            {
                                double lon = GetLonFromLat(startP, endP, tempLat);
                                double height = TerrainProvider.GetElevationAt(tempLat, lon, level);
                                points.Add(new GeoPoint(tempLat, lon, height));

                                //MinMaxLatLon tempMinMax = GetMinMaxFromLatLon(tempLat + 0.00001, lon, level);
                                //double latRange = tempMinMax.MaxLat - tempMinMax.MinLat;
                                //double lonRange = tempMinMax.MaxLon - tempMinMax.MinLon;

                                //double littleLat = latRange / quadCount;
                                //double littleLon = lonRange / quadCount;

                                //double innerLat = tempMinMax.MinLat + littleLat;
                                //double innerLon = GetLonFromLat(startP, endP, innerLat);
                                //while (innerLat > tempMinMax.MinLat && innerLat <= tempMinMax.MaxLat
                                //    && innerLon > tempMinMax.MinLon && innerLon <= tempMinMax.MaxLat)
                                //{
                                //    height=TerrainProvider.GetElevationAt(innerLat,innerLon,level);
                                //    points.Add(new GeoPoint(innerLat, innerLon, height));
                                //    int curCol = (int)((innerLon - tempMinMax.MinLon) / littleLon) + 1;//当前点所在的列号，从1开始
                                //    if(innerLon<=littleLon*curCol)
                                //    {

                                //    }
                                //}


                                points.AddRange(GetPointsFromLine(new LatLon(tempLat + 0.00001, lon), endP, level));
                            }
                        }
                        else if (lat < minMaxLatLon.MinLat)
                        {
                            tempLat = minMaxLatLon.MinLat;
                            if (tempLat > endP.Latitude && tempLat < startP.Latitude)
                            {
                                double lon = GetLonFromLat(startP, endP, tempLat);
                                double height = TerrainProvider.GetElevationAt(tempLat, lon, level);
                                points.Add(new GeoPoint(tempLat, lon, height));
                                points.AddRange(GetPointsFromLine(new LatLon(tempLat - 0.000001, lon), endP, level));
                            }
                        }

                    }
                }







                //else
                //{
                //    tempLon = minMaxLatLon.MinLon;
                //    double lat = GetLatFromLon(startP, endP, tempLon);
                //    if (lat >= minMaxLatLon.MinLat && lat < minMaxLatLon.MaxLat)
                //    {
                //        points.Add(new LatLon(lat, tempLon));
                //    }
                //    else
                //    {
                //        if (lat >= minMaxLatLon.MaxLat)
                //        {
                //            tempLat = minMaxLatLon.MaxLat;
                //        }
                //        else if (lat < minMaxLatLon.MinLat)
                //        {
                //            tempLat = minMaxLatLon.MinLat;
                //        }
                //        double lon = GetLonFromLat(startP, endP, tempLat);
                //        points.Add(new LatLon(tempLat, lon));
                //    }
                //}
            }
            //RowColNumber startRC = GetRowColFromLatLon(startP.Latitude, startP.Longitude, level);
            //RowColNumber endRC = GetRowColFromLatLon(endP.Latitude, endP.Longitude, level);
            //int rowCount = endRC.Row - startRC.Row;
            //int colCount = endRC.Col - startRC.Col;
            //double factor = 0;
            //int pointCount = 0;
            //double deltaLat = 0;
            //double deltaDeg = 0;
            //int OctaMaxCol = (int)Math.Pow(2, level);

            //if (Math.Abs(rowCount) >= Math.Abs(colCount))
            //{
            //    factor = Math.Abs((double)colCount / (double)rowCount);
            //    pointCount = Math.Abs(rowCount);
            //    deltaLat = rowCount / Math.Abs(rowCount);
            //    deltaDeg = colCount / Math.Abs(colCount) * factor;
            //}
            //else
            //{
            //    factor = Math.Abs((double)rowCount / (double)colCount);
            //    pointCount = Math.Abs(colCount);
            //    deltaLat = rowCount / Math.Abs(rowCount) * factor;
            //    deltaDeg = colCount / Math.Abs(colCount);
            //}

            //for (int i = 0; i <= pointCount; i++)
            //{
            //    int row = startRC.Row + (int)(deltaLat * i + 0.5);
            //    int col = startRC.Col + (int)(deltaDeg * i + 0.5);
            //    int rowMaxCol = MaxCol(level, row);
            //    int index = OctaMaxCol / rowMaxCol;
            //    col /= index;
            //    string adress = GetOctaFromLatLon(startP.Latitude, startP.Longitude).ToString() + CalMortonCode(row, col, level);
            //    points.Add(adress);
            //}
            //points.Add(GetAdressFromLatLon(endP.Latitude, endP.Longitude, level));
            return points;
        }
        private static double NextLineDegree(double curDeg, double startDeg, double deltaDeg)
        {
            int col = (int)((curDeg - startDeg) / deltaDeg) + 1;
            double nextLon = col * deltaDeg + startDeg;
            return nextLon;
        }

        private static double RightBottom(MinMaxLatLon grid, LatLon P, int level)
        {
            double height1 = TerrainProvider.GetElevationAt(grid.MaxLat, grid.MaxLon, level);
            double height2 = TerrainProvider.GetElevationAt(grid.MinLat, grid.MaxLon, level);
            double height3 = TerrainProvider.GetElevationAt(grid.MinLat, grid.MinLon, level);
            GeoPoint pa = new GeoPoint(grid.MaxLat, grid.MaxLon, height1);
            GeoPoint pb = new GeoPoint(grid.MinLat, grid.MaxLon, height2);
            GeoPoint pc = new GeoPoint(grid.MinLat, grid.MinLon, height3);
            GeoPoint p = new GeoPoint(P.Latitude, P.Longitude, 0);
            double height = GetPointHeightInTriangle(pa, pb, pc, p);
            return height;
        }

        private static double LeftTop(MinMaxLatLon grid, LatLon P, int level)
        {
            double height1 = TerrainProvider.GetElevationAt(grid.MaxLat, grid.MaxLon, level);
            double height2 = TerrainProvider.GetElevationAt(grid.MaxLat, grid.MinLon, level);
            double height3 = TerrainProvider.GetElevationAt(grid.MinLat, grid.MinLon, level);
            GeoPoint pa = new GeoPoint(grid.MaxLat, grid.MaxLon, height1);
            GeoPoint pb = new GeoPoint(grid.MaxLat, grid.MinLon, height2);
            GeoPoint pc = new GeoPoint(grid.MinLat, grid.MinLon, height3);
            GeoPoint p = new GeoPoint(P.Latitude, P.Longitude, 0);
            double height = GetPointHeightInTriangle(pa, pb, pc, p);
            return height;
        }
        /// <summary>
        /// 得到与对角线的交点
        /// </summary>
        /// <returns></returns>
        private static GeoPoint GetPointOnDiagonal(LatLon startP, LatLon endP, MinMaxLatLon grid, int level)
        {
            double aboveTerrain = 5;
            double x1 = grid.MinLon;
            double y1 = grid.MinLat;
            double k1 = (grid.MaxLat - grid.MinLat) / (grid.MaxLon - grid.MinLon);
            double x2 = startP.Longitude;
            double y2 = startP.Latitude;
            double delLat = endP.Latitude - startP.Latitude;
            double delLon = endP.Longitude - startP.Longitude;
            double height1, height2, P1, P2, height,curLat=0,curLon=0;
            GeoPoint point;
            if (Math.Abs(delLon) > double.Epsilon)
            {
                double k2 = delLat/delLon;                
                curLon = (k1 * x1 - y1 - k2 * x2 + y2) / (k1 - k2);                            
            }
            else 
            {
                curLon = startP.Longitude;
            }
            curLat = k1 * (curLon - x1) + y1; 
            height1 = TerrainProvider.GetElevationAt(grid.MaxLat, grid.MaxLon, level);
            height2 = TerrainProvider.GetElevationAt(grid.MinLat, grid.MinLon, level);
            P1 = curLat - grid.MinLat;
            P2 = grid.MaxLat - curLat;
            height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
            point = new GeoPoint(curLat, curLon, height);
            return point;
        }

        /// <summary>
        /// 下边进
        /// </summary>
        /// <param name="startP"></param>
        /// <param name="endP"></param>
        /// <param name="k"></param>
        /// <param name="grid"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        private static List<GeoPoint> BottomIn(LatLon startP, LatLon endP, MinMaxLatLon grid, int level)
        {
            double aboveTerrain = 5;
            List<GeoPoint> points = new List<GeoPoint>();
            //double lon = GetLonFromLat(startP, k, grid.MaxLat);
            double lon = GetLonFromLat(startP, endP, grid.MaxLat);
            double curLon = 0;
            double curLat = 0;
            if (lon > grid.MaxLon)//右边出
            {
                curLon = grid.MaxLon;
                curLat = GetLatFromLon(startP, endP, curLon);
                if (curLat <= endP.Latitude)
                {
                    double height1 = TerrainProvider.GetElevationAt(grid.MaxLat, curLon, level);
                    double height2 = TerrainProvider.GetElevationAt(grid.MinLat, curLon, level);
                    double P2 = grid.MaxLat - curLat;
                    double P1 = curLat - grid.MinLat;
                    double height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    GeoPoint point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);
                    MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat, grid.MaxLat, grid.MaxLon, grid.MaxLon * 2 - grid.MinLon);
                    points.AddRange(LeftIn(startP, endP,nextGrid, level));
                }
                else
                {
                    double height = RightBottom(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, height));
                    return points;
                }
            }
            else if (lon > grid.MinLon && lon <= grid.MaxLon)//上边出
            {
                double height1 = 0, height2, P1, P2, height;
                GeoPoint point = GetPointOnDiagonal(startP, endP, grid, level);
                curLon = point.Longitude;
                curLat = point.Latitude;
                if (curLat <= endP.Latitude)
                {
                    height1 = TerrainProvider.GetElevationAt(grid.MaxLat, grid.MaxLon, level);
                    height2 = TerrainProvider.GetElevationAt(grid.MinLat, grid.MinLon, level);
                    P1 = curLat - grid.MinLat;
                    P2 = grid.MaxLat - curLat;
                    height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);//与对角线的交点
                }
                else
                {
                    double h = RightBottom(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }

                curLon = lon;
                curLat = grid.MaxLat;
                if (curLat <= endP.Latitude)
                {
                    height1 = TerrainProvider.GetElevationAt(curLat, grid.MaxLon, level);
                    height2 = TerrainProvider.GetElevationAt(curLat, grid.MinLon, level);
                    P1 = curLon - grid.MinLon;
                    P2 = grid.MaxLon - curLon;
                    height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);//与上边的交点
                    double nextGridMidLat = grid.MaxLat + (grid.MaxLat - grid.MinLat) / 2;
                    MinMaxLatLon minMax = GetMinMaxFromLatLon(nextGridMidLat, curLon, level);//所在DQG格网的边界经纬度
                    double littleLon = (minMax.MaxLon - minMax.MinLon) / Math.Pow(2, DQGTileSet.BaseLevel);
                    if (Math.Abs(littleLon - (grid.MaxLon - grid.MinLon)) < double.Epsilon)//上下格网经差相同
                    {
                        MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MaxLat, grid.MaxLat * 2 - grid.MinLat, grid.MinLon, grid.MaxLon);
                        points.AddRange(BottomIn(startP, endP, nextGrid, level));
                    }
                    else
                    {
                        int col = (int)((curLon - minMax.MinLon) / littleLon);
                        double minLon = col * littleLon + minMax.MinLon;
                        double maxLon = minLon + littleLon;
                        MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MaxLat, grid.MaxLat * 2 - grid.MinLat, minLon, maxLon);
                        points.AddRange(BottomIn(startP, endP,nextGrid, level));
                    }
                }
                else
                {
                    double h = LeftTop(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
            }
            else if (lon <= grid.MinLon)//左边出
            {
                
                double height1 = 0, height2, P1, P2, height;
                GeoPoint point = GetPointOnDiagonal(startP, endP, grid, level);
                curLon = point.Longitude;
                curLat = point.Latitude;
                if (curLat <= endP.Latitude)
                {
                    height1 = TerrainProvider.GetElevationAt(grid.MaxLat, grid.MaxLon, level);
                    height2 = TerrainProvider.GetElevationAt(grid.MinLat, grid.MinLon, level);
                    P1 = curLat - grid.MinLat;
                    P2 = grid.MaxLat - curLat;
                    height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);//与对角线的交点
                }
                else
                {
                    double h = RightBottom(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
                curLon = grid.MinLon;
                curLat = GetLatFromLon(startP, endP, curLon);
                if (curLat <= endP.Latitude)
                {
                    height1 = TerrainProvider.GetElevationAt(grid.MaxLat, curLon, level);
                    height2 = TerrainProvider.GetElevationAt(grid.MinLat, curLon, level);
                    P2 = grid.MaxLat - curLat;
                    P1 = curLat - grid.MinLat;
                    height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);//与左边交点
                    MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat, grid.MaxLat, grid.MinLon * 2 - grid.MaxLon, grid.MinLon);
                    points.AddRange(RightIn(startP, endP, nextGrid, level));
                }
                else
                {
                    double h = LeftTop(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
            }
            return points;
        }

        private static bool IsBetweenTwoValue(double num, double num1, double num2)
        {
            return (num >= Math.Min(num1, num2) && num <= Math.Max(num1, num2));
        }

        private static bool IsOnLeftTop(LatLon P1,LatLon P2,LatLon P)
        {
            double K = (P2.Latitude - P1.Latitude) / (P2.Longitude - P1.Longitude);
            if (P.Latitude - (K * (P.Longitude - P1.Longitude) + P1.Latitude) > 0)
                return true;
            return false;
        }
        /// <summary>
        /// 插值得到三角形内一点的高程
        /// </summary>
        /// <param name="Pa">A点</param>
        /// <param name="Pb">B点</param>
        /// <param name="Pc">C点</param>
        /// <param name="p">未知点</param>
        /// <returns>未知点的高程值</returns>
        private static double GetPointHeightInTriangle(GeoPoint Pa, GeoPoint Pb, GeoPoint Pc, GeoPoint p)
        {
            double Xba = Pb.Longitude - Pa.Longitude;
            double Yba = Pb.Latitude - Pa.Latitude;
            double Zba = Pb.Altitude - Pa.Altitude;
            double Xca = Pc.Longitude - Pa.Longitude;
            double Yca = Pc.Latitude - Pa.Latitude;
            double Zca = Pc.Altitude - Pa.Altitude;
            double X = p.Longitude, Xa = Pa.Longitude;
            double Y = p.Latitude, Ya = Pa.Latitude;
            double Za = Pa.Altitude;
            double Z = (Zba * Yca * (X - Xa) + Xba * Zca * (Y - Ya) - Yba * Zca * (X - Xa) - Xca * Zba * (Y - Ya)) / (Xba * Yca - Yba * Xca) + Za;
            return Z;
        }
        public static List<GeoPoint> GetLinePoints(LatLon startP, LatLon endP, int level)
        {
            double aboveTerrain = 5;
            List<GeoPoint> points = new List<GeoPoint>();
            MinMaxLatLon minMaxDQG = GetMinMaxFromLatLon(startP.Latitude, startP.Longitude, level);
            double littleLon = (minMaxDQG.MaxLon - minMaxDQG.MinLon) / Math.Pow(2, DQGTileSet.BaseLevel);
            double littleLat = (minMaxDQG.MaxLat - minMaxDQG.MinLat) / Math.Pow(2, DQGTileSet.BaseLevel);
            int row = (int)((startP.Latitude - minMaxDQG.MinLat) / littleLat);
            double bottomLat = row * littleLat + minMaxDQG.MinLat;
            double topLat = bottomLat + littleLat;
            int col = (int)((startP.Longitude - minMaxDQG.MinLon) / littleLon);
            double leftLon = col * littleLon + minMaxDQG.MinLon;
            double rightLon = leftLon + littleLon;
            MinMaxLatLon grid = new MinMaxLatLon(bottomLat, topLat, leftLon, rightLon);
            double curLat = 0;
            double curLon = 0;
            LatLon Point1 = new LatLon(bottomLat, leftLon);
            LatLon Point2 = new LatLon(topLat, rightLon);
            bool startSide = IsOnLeftTop(Point1, Point2, startP);//判断起点在四边形的哪个三角形中
            double h = 0;
            if (startSide)
            {
                h = LeftTop(grid, startP, level);
            }
            else
            {
                h = RightBottom(grid, startP, level);
            }
            points.Add(new GeoPoint(startP.Latitude, startP.Longitude, h));
            //if(Math.Abs(endP.Latitude-startP.Latitude)<double.Epsilon)//纬差为0 
            //{

            //}
          
            


                if (Math.Abs(endP.Latitude - startP.Latitude) > double.Epsilon)
                {
                    curLat = topLat;
                    //curLon = GetLonFromLat(startP, k, topLat);
                    curLon = GetLonFromLat(startP, endP, topLat);
                    if (curLon >= grid.MinLon && curLon <= grid.MaxLon) //从上边出
                    {
                        if (IsBetweenTwoValue(curLat, startP.Latitude, endP.Latitude))
                        {
                            if (!startSide)//如果起点在右下三角形内，先加入与对角线的交点
                            {
                                points.Add(GetPointOnDiagonal(startP, endP, grid, level));
                            }
                            double height1 = TerrainProvider.GetElevationAt(curLat, grid.MaxLon, level);
                            double height2 = TerrainProvider.GetElevationAt(curLat, grid.MinLon, level);
                            double P1 = curLon - grid.MinLon;
                            double P2 = grid.MaxLon - curLon;
                            double height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                            GeoPoint point = new GeoPoint(curLat, curLon, height);
                            points.Add(point);//与上边的交点
                            double nextGridMidLat = grid.MaxLat + (grid.MaxLat - grid.MinLat) / 2;
                            MinMaxLatLon minMax1 = GetMinMaxFromLatLon(nextGridMidLat, curLon, level);//所在DQG格网的边界经纬度
                            double littleLon1 = (minMax1.MaxLon - minMax1.MinLon) / Math.Pow(2, DQGTileSet.BaseLevel);
                            if (Math.Abs(littleLon1 - (grid.MaxLon - grid.MinLon)) < double.Epsilon)//上下格网经差相同
                            {
                                MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MaxLat, grid.MaxLat * 2 - grid.MinLat, grid.MinLon, grid.MaxLon);
                                points.AddRange(BottomIn(startP, endP,nextGrid, level));
                                return points;
                            }
                            else
                            {
                                int col1 = (int)((curLon - minMax1.MinLon) / littleLon1);
                                double minLon = col1 * littleLon1 + minMax1.MinLon;
                                double maxLon = minLon + littleLon1;
                                MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MaxLat, grid.MaxLat * 2 - grid.MinLat, minLon, maxLon);
                                points.AddRange(BottomIn(startP, endP,nextGrid, level));
                                return points;
                            }
                        }
                    }

                    curLat = bottomLat;
                    //curLon = GetLonFromLat(startP, k, curLat);
                    curLon = GetLonFromLat(startP, endP, curLat);
                    if (curLon >= grid.MinLon && curLon <= grid.MaxLon) //从下边出
                    {
                        if (IsBetweenTwoValue(curLat, startP.Latitude, endP.Latitude))
                        {
                            if (startSide)//如果起点在左上三角形内，先加入与对角线的交点
                            {
                                points.Add(GetPointOnDiagonal(startP, endP, grid, level));
                            }
                            double height1 = TerrainProvider.GetElevationAt(curLat, grid.MaxLon, level);
                            double height2 = TerrainProvider.GetElevationAt(curLat, grid.MinLon, level);
                            double P1 = curLon - grid.MinLon;
                            double P2 = grid.MaxLon - curLon;
                            double height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                            GeoPoint point = new GeoPoint(curLat, curLon, height);
                            points.Add(point);//与下边的交点
                            double nextGridMidLat = grid.MinLat - (grid.MaxLat - grid.MinLat) / 2;
                            MinMaxLatLon minMax = GetMinMaxFromLatLon(nextGridMidLat, curLon, level);//所在DQG格网的边界经纬度
                            double littleLon1 = (minMax.MaxLon - minMax.MinLon) / Math.Pow(2, DQGTileSet.BaseLevel);
                            if (Math.Abs(littleLon1 - (grid.MaxLon - grid.MinLon)) < double.Epsilon)//上下格网经差相同
                            {
                                MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat * 2 - grid.MaxLat, grid.MinLat, grid.MinLon, grid.MaxLon);
                                points.AddRange(TopIn(startP, endP, nextGrid, level));
                                return points;
                            }
                            else
                            {
                                int col1 = (int)((curLon - minMax.MinLon) / littleLon1);
                                double minLon = col1 * littleLon1 + minMax.MinLon;
                                double maxLon = minLon + littleLon1;
                                MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat * 2 - grid.MaxLat, grid.MinLat, minLon, maxLon);
                                points.AddRange(TopIn(startP, endP,nextGrid, level));
                                return points;
                            }
                        }
                    }
                }

                if (Math.Abs(endP.Longitude - startP.Longitude) > double.Epsilon)
                {
                    //从左边出
                    curLon = grid.MinLon;
                    curLat = GetLatFromLon(startP, endP, curLon);
                    if (curLat >= grid.MinLat && curLat <= grid.MaxLat)
                    {
                        if (IsBetweenTwoValue(curLon, startP.Longitude, endP.Longitude))
                        {
                            if (!startSide)//如果起点在右下三角形内，先加入与对角线的交点
                            {
                                points.Add(GetPointOnDiagonal(startP, endP, grid, level));
                            }
                            double height1 = TerrainProvider.GetElevationAt(grid.MaxLat, curLon, level);
                            double height2 = TerrainProvider.GetElevationAt(grid.MinLat, curLon, level);
                            double P2 = grid.MaxLat - curLat;
                            double P1 = curLat - grid.MinLat;
                            double height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                            GeoPoint point = new GeoPoint(curLat, curLon, height);
                            points.Add(point);//与左边交点
                            MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat, grid.MaxLat, grid.MinLon * 2 - grid.MaxLon, grid.MinLon);
                            points.AddRange(RightIn(startP, endP, nextGrid, level));
                            return points;
                        }
                    }

                    //从右边出
                    curLon = grid.MaxLon;
                    curLat = GetLatFromLon(startP, endP, curLon);
                    if (curLat >= grid.MinLat && curLat <= grid.MaxLat)
                    {
                        if (IsBetweenTwoValue(curLon, startP.Longitude, endP.Longitude))
                        {
                            if (startSide)//如果起点在左上三角形内，先加入与对角线的交点
                            {
                                points.Add(GetPointOnDiagonal(startP, endP, grid, level));
                            }
                            double height1 = TerrainProvider.GetElevationAt(grid.MaxLat, curLon, level);
                            double height2 = TerrainProvider.GetElevationAt(grid.MinLat, curLon, level);
                            double P2 = grid.MaxLat - curLat;
                            double P1 = curLat - grid.MinLat;
                            double height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                            GeoPoint point = new GeoPoint(curLat, curLon, height);
                            points.Add(point);
                            MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat, grid.MaxLat, grid.MaxLon, grid.MaxLon * 2 - grid.MinLon);
                            points.AddRange(LeftIn(startP, endP, nextGrid, level));
                            return points;
                        }
                    }
                }

                bool endSide = IsOnLeftTop(Point1, Point2, endP);
                if (startSide != endSide)
                {
                    GeoPoint point = GetPointOnDiagonal(startP, endP, grid, level);
                    points.Add(point);
                }
                if (endSide)
                {
                    h = LeftTop(grid, endP, level);
                }
                else
                {
                    h = RightBottom(grid, endP, level);
                }
                points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));

            
            return points;
        }

        private static List<GeoPoint> LeftIn(LatLon startP, LatLon endP,MinMaxLatLon grid, int level)
        {
            List<GeoPoint> points = new List<GeoPoint>();
            double aboveTerrain = 5;
            double lat = GetLatFromLon(startP, endP, grid.MaxLon);
            double curLon = 0;
            double curLat = 0;
            if (lat > grid.MaxLat)  //上边出
            {
                curLat = grid.MaxLat;
                curLon = GetLonFromLat(startP, endP, curLat);
                //curLon = GetLonFromLat(startP, k, curLat);
                if (curLon <= endP.Longitude)
                {
                    double height1 = TerrainProvider.GetElevationAt(grid.MaxLat, grid.MaxLon, level);
                    double height2 = TerrainProvider.GetElevationAt(grid.MaxLat, grid.MinLon, level);
                    double P1 = curLon - grid.MinLon;
                    double P2 = grid.MaxLon - curLon;
                    double height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    GeoPoint point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);
                    double nextGridMidLat = grid.MaxLat + (grid.MaxLat - grid.MinLat) / 2;
                    MinMaxLatLon minMax = GetMinMaxFromLatLon(nextGridMidLat, curLon, level);//所在DQG格网的边界经纬度
                    double littleLon = (minMax.MaxLon - minMax.MinLon) / Math.Pow(2, DQGTileSet.BaseLevel);
                    if (Math.Abs(littleLon - (grid.MaxLon - grid.MinLon)) < double.Epsilon)//上下格网经差相同
                    {
                        MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MaxLat, grid.MaxLat * 2 - grid.MinLat, grid.MinLon, grid.MaxLon);
                        points.AddRange(BottomIn(startP, endP, nextGrid, level));
                    }
                    else
                    {
                        int col = (int)((curLon - minMax.MinLon) / littleLon);
                        double minLon = col * littleLon + minMax.MinLon;
                        double maxLon = minLon + littleLon;
                        MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MaxLat, grid.MaxLat * 2 - grid.MinLat, minLon, maxLon);
                        points.AddRange(BottomIn(startP, endP,  nextGrid, level));
                    }
                }
                else
                {
                    double h = LeftTop(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
            }
            else if (lat <= grid.MaxLat && lat > grid.MinLat)//右边出
            {
                GeoPoint point = GetPointOnDiagonal(startP, endP, grid, level);
                curLon = point.Longitude;
                curLat = point.Latitude;
                if (curLon <= endP.Longitude)
                {
                    points.Add(point);//与对角线的交点
                }
                else
                {
                    double h = LeftTop(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
                curLat = lat;
                curLon = grid.MaxLon;
                if (curLon <= endP.Longitude)
                {
                    double height1 = TerrainProvider.GetElevationAt(grid.MaxLat, grid.MaxLon, level);
                    double height2 = TerrainProvider.GetElevationAt(grid.MinLat, grid.MaxLon, level);
                    double P1 = curLat - grid.MinLat;
                    double P2 = grid.MaxLat - curLat;
                    double height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;                    
                    points.Add(new GeoPoint(curLat, curLon, height));//与右边交点
                    MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat, grid.MaxLat, grid.MaxLon, grid.MaxLon * 2 - grid.MinLon);
                    points.AddRange(LeftIn(startP, endP, nextGrid, level));
                }
                else
                {
                    double h = RightBottom(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
            }
            else if (lat <= grid.MinLat)//下边出
            {
                GeoPoint p = GetPointOnDiagonal(startP, endP, grid, level);
                curLon = p.Longitude;
                curLat = p.Latitude;
                if (curLon <= endP.Longitude)
                {
                    points.Add(p);//与对角线的交点
                }
                else
                {
                    double h = LeftTop(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
                curLat = grid.MinLat;
                //curLon = GetLonFromLat(startP, k, curLat);
                curLon = GetLonFromLat(startP, endP, curLat);
                if (curLat >= endP.Latitude)
                {
                    double height1 = TerrainProvider.GetElevationAt(curLat, grid.MaxLon, level);
                    double height2 = TerrainProvider.GetElevationAt(curLat, grid.MinLon, level);
                    double P1 = curLon - grid.MinLon;
                    double P2 = grid.MaxLon - curLon;
                    double height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    GeoPoint point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);//与下边的交点
                    double nextGridMidLat = grid.MinLat - (grid.MaxLat - grid.MinLat) / 2;
                    MinMaxLatLon minMax = GetMinMaxFromLatLon(nextGridMidLat, curLon, level);//所在DQG格网的边界经纬度
                    double littleLon = (minMax.MaxLon - minMax.MinLon) / Math.Pow(2, DQGTileSet.BaseLevel);
                    if (Math.Abs(littleLon - (grid.MaxLon - grid.MinLon)) < double.Epsilon)//上下格网经差相同
                    {
                        MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat * 2 - grid.MaxLat, grid.MinLat, grid.MinLon, grid.MaxLon);
                        points.AddRange(TopIn(startP, endP,nextGrid, level));
                    }
                    else
                    {
                        int col = (int)((curLon - minMax.MinLon) / littleLon);
                        double minLon = col * littleLon + minMax.MinLon;
                        double maxLon = minLon + littleLon;
                        MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat * 2 - grid.MaxLat, grid.MinLat, minLon, maxLon);
                        points.AddRange(TopIn(startP, endP, nextGrid, level));
                    }
                }
                else
                {
                    double h = RightBottom(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
            }
            return points;
        }
        /// <summary>
        /// 上边进 
        /// </summary>
        private static List<GeoPoint> TopIn(LatLon startP, LatLon endP,  MinMaxLatLon grid, int level)
        {
            double aboveTerrain = 5;
            List<GeoPoint> points = new List<GeoPoint>();
            //double lon = GetLonFromLat(startP, k, grid.MinLat);
            double lon = GetLonFromLat(startP, endP, grid.MinLat);
            double curLon = 0;
            double curLat = 0;
            if (lon <= grid.MinLon)//左边出
            {
                curLon = grid.MinLon;
                curLat = GetLatFromLon(startP, endP, curLon);
                if (curLat >= endP.Latitude)
                {
                    double height1 = TerrainProvider.GetElevationAt(grid.MaxLat, curLon, level);
                    double height2 = TerrainProvider.GetElevationAt(grid.MinLat, curLon, level);
                    double P1 = curLat - grid.MinLat;
                    double P2 = grid.MaxLat - curLat;
                    double height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    GeoPoint point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);
                    MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat, grid.MaxLat, grid.MinLon * 2 - grid.MaxLon, grid.MinLon);
                    points.AddRange(RightIn(startP, endP,nextGrid, level));
                }
                else
                {
                    double h = LeftTop(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
            }
            else if (lon > grid.MinLon && lon <= grid.MaxLon)
            {
                GeoPoint p = GetPointOnDiagonal(startP, endP, grid, level);
                curLon = p.Longitude;
                curLat = p.Latitude;
                if (curLat >= endP.Latitude)
                {
                    points.Add(p);//与对角线的交点
                }
                else
                {
                    double h = LeftTop(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
                curLat = grid.MinLat;
                curLon = lon;
                if (curLat >= endP.Latitude)
                {
                    double height1 = TerrainProvider.GetElevationAt(curLat, grid.MaxLon, level);
                    double height2 = TerrainProvider.GetElevationAt(curLat, grid.MinLon, level);
                    double P1 = curLon - grid.MinLon;
                    double P2 = grid.MaxLon - curLon;
                    double height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    GeoPoint point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);//与下边的交点
                    double nextGridMidLat = grid.MinLat - (grid.MaxLat - grid.MinLat) / 2;
                    MinMaxLatLon minMax = GetMinMaxFromLatLon(nextGridMidLat, curLon, level);//所在DQG格网的边界经纬度
                    double littleLon = (minMax.MaxLon - minMax.MinLon) / Math.Pow(2, DQGTileSet.BaseLevel);
                    if (Math.Abs(littleLon - (grid.MaxLon - grid.MinLon)) < double.Epsilon)//上下格网经差相同
                    {
                        MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat * 2 - grid.MaxLat, grid.MinLat, grid.MinLon, grid.MaxLon);
                        points.AddRange(TopIn(startP, endP, nextGrid, level));
                    }
                    else
                    {
                        int col = (int)((curLon - minMax.MinLon) / littleLon);
                        double minLon = col * littleLon + minMax.MinLon;
                        double maxLon = minLon + littleLon;
                        MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat * 2 - grid.MaxLat, grid.MinLat, minLon, maxLon);
                        points.AddRange(TopIn(startP, endP, nextGrid, level));
                    }
                }
                else
                {
                    double h = RightBottom(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
            }
            else if (lon > grid.MaxLon)//右边出
            {
                
                double height1 = 0, height2, P1, P2, height;
                GeoPoint point;
                GeoPoint p = GetPointOnDiagonal(startP, endP, grid, level);
                curLon = p.Longitude;
                curLat = p.Latitude;
                if (curLat >= endP.Latitude)
                {

                    points.Add(p);//与对角线的交点
                }
                else
                {
                    double h = LeftTop(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }

                curLon = grid.MaxLon;
                curLat = GetLatFromLon(startP, endP, curLon);
                if (curLat >= endP.Latitude)
                {
                    height1 = TerrainProvider.GetElevationAt(grid.MaxLat, curLon, level);
                    height2 = TerrainProvider.GetElevationAt(grid.MinLat, curLon, level);
                    P2 = grid.MaxLat - curLat;
                    P1 = curLat - grid.MinLat;
                    height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);//与右边交点
                    MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat, grid.MaxLat, grid.MaxLon, grid.MaxLon * 2 - grid.MinLon);
                    points.AddRange(LeftIn(startP, endP, nextGrid, level));
                }
                else
                {
                    double h = RightBottom(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
            }

            return points;
        }

        private static List<GeoPoint> RightIn(LatLon startP, LatLon endP, MinMaxLatLon grid, int level)
        {
            List<GeoPoint> points = new List<GeoPoint>();
            double aboveTerrain = 5;
            double lat = GetLatFromLon(startP, endP, grid.MinLon);
            double curLat = 0, curLon = 0;
            double height1 = 0, height2, P1, P2, height;
            GeoPoint point;
            if (lat <= grid.MinLat)//下边出
            {
                curLat = grid.MinLat;
                //curLon = GetLonFromLat(startP, k, curLat);
                curLon = GetLonFromLat(startP, endP, curLat);
                if (curLon >= endP.Longitude)
                {
                    height1 = TerrainProvider.GetElevationAt(curLat, grid.MaxLon, level);
                    height2 = TerrainProvider.GetElevationAt(curLat, grid.MinLon, level);
                    P1 = curLon - grid.MinLon;
                    P2 = grid.MaxLon - curLon;
                    height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);//与下边的交点
                    double nextGridMidLat = grid.MinLat - (grid.MaxLat - grid.MinLat) / 2;
                    MinMaxLatLon minMax = GetMinMaxFromLatLon(nextGridMidLat, curLon, level);//所在DQG格网的边界经纬度
                    double littleLon = (minMax.MaxLon - minMax.MinLon) / Math.Pow(2, DQGTileSet.BaseLevel);
                    if (Math.Abs(littleLon - (grid.MaxLon - grid.MinLon)) < double.Epsilon)//上下格网经差相同
                    {
                        MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat * 2 - grid.MaxLat, grid.MinLat, grid.MinLon, grid.MaxLon);
                        points.AddRange(TopIn(startP, endP, nextGrid, level));
                    }
                    else
                    {
                        int col = (int)((curLon - minMax.MinLon) / littleLon);
                        double minLon = col * littleLon + minMax.MinLon;
                        double maxLon = minLon + littleLon;
                        MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat * 2 - grid.MaxLat, grid.MinLat, minLon, maxLon);
                        points.AddRange(TopIn(startP, endP, nextGrid, level));
                    }
                }
                else
                {
                    double h = RightBottom(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
            }
            else if (lat > grid.MinLat && lat <= grid.MaxLat)//左边出
            {

                GeoPoint p = GetPointOnDiagonal(startP, endP, grid, level);
                curLat = p.Latitude;
                curLon = p.Longitude;
                if (curLon >= endP.Longitude)
                {
                    points.Add(p);//与对角线的交点
                }
                else
                {
                    double h = RightBottom(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
                curLon = grid.MinLon;
                curLat = lat;
                if (curLon >= endP.Longitude)
                {
                    height1 = TerrainProvider.GetElevationAt(grid.MaxLat, curLon, level);
                    height2 = TerrainProvider.GetElevationAt(grid.MinLat, curLon, level);
                    P1 = curLat - grid.MinLat;
                    P2 = grid.MaxLat - curLat;
                    height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);
                    MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MinLat, grid.MaxLat, grid.MinLon * 2 - grid.MaxLon, grid.MinLon);
                    points.AddRange(RightIn(startP, endP, nextGrid, level));
                }
                else
                {
                    double h = LeftTop(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
            }
            else if (lat > grid.MaxLat)//上边出
            {
                GeoPoint p = GetPointOnDiagonal(startP, endP, grid, level);
                curLon = p.Longitude;
                curLat = p.Latitude;
                if (curLon >= endP.Longitude)
                {
                    points.Add(p);//与对角线的交点
                }
                else
                {
                    double h = RightBottom(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }

                curLat = grid.MaxLat;
                //curLon = GetLonFromLat(startP, k, curLat);
                curLon = GetLonFromLat(startP, endP, curLat);
                if (curLon >= endP.Longitude)
                {
                    height1 = TerrainProvider.GetElevationAt(curLat, grid.MaxLon, level);
                    height2 = TerrainProvider.GetElevationAt(curLat, grid.MinLon, level);
                    P1 = curLon - grid.MinLon;
                    P2 = grid.MaxLon - curLon;
                    height = (height1 * P1 + height2 * P2) / (P1 + P2) + aboveTerrain;
                    point = new GeoPoint(curLat, curLon, height);
                    points.Add(point);//与上边的交点
                    double nextGridMidLat = grid.MaxLat + (grid.MaxLat - grid.MinLat) / 2;
                    MinMaxLatLon minMax = GetMinMaxFromLatLon(nextGridMidLat, curLon, level);//所在DQG格网的边界经纬度
                    double littleLon = (minMax.MaxLon - minMax.MinLon) / Math.Pow(2, DQGTileSet.BaseLevel);
                    if (Math.Abs(littleLon - (grid.MaxLon - grid.MinLon)) < double.Epsilon)//上下格网经差相同
                    {
                        MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MaxLat, grid.MaxLat * 2 - grid.MinLat, grid.MinLon, grid.MaxLon);
                        points.AddRange(BottomIn(startP, endP, nextGrid, level));
                    }
                    else
                    {
                        int col = (int)((curLon - minMax.MinLon) / littleLon);
                        double minLon = col * littleLon + minMax.MinLon;
                        double maxLon = minLon + littleLon;
                        MinMaxLatLon nextGrid = new MinMaxLatLon(grid.MaxLat, grid.MaxLat * 2 - grid.MinLat, minLon, maxLon);
                        points.AddRange(BottomIn(startP, endP, nextGrid, level));
                    }
                }
                else
                {
                    double h = LeftTop(grid, endP, level);
                    points.Add(new GeoPoint(endP.Latitude, endP.Longitude, h));
                    return points;
                }
            }
            return points;
        }
        public static List<GeoPoint> GetAllPointsFromLine(LatLon startP, LatLon endP, int level)
        {
            List<GeoPoint> points = new List<GeoPoint>();
            double deltaLon = endP.Longitude - startP.Longitude;
            double deltaLat = endP.Latitude - startP.Latitude;
            double quadCount = Math.Pow(2.0, DQGTileSet.BaseLevel);
            MinMaxLatLon minMaxLatLon = GetMinMaxFromLatLon(startP.Latitude, startP.Longitude, level);//起点所在的DQG格网的边界经纬度
            double littleLon = (minMaxLatLon.MaxLon - minMaxLatLon.MinLon) / quadCount;
            double littleLat = (minMaxLatLon.MaxLat - minMaxLatLon.MinLat) / quadCount;
            if (Math.Abs(deltaLon) > Math.Abs(deltaLat))
            {
                double tempLon = 0;
                double tempLat = 0;
                if (deltaLon > 0)
                {
                    tempLon = NextLineDegree(startP.Longitude, minMaxLatLon.MinLon, littleLon);
                    double lat = GetLatFromLon(startP, endP, tempLon);
                    double nextLat = NextLineDegree(lat, minMaxLatLon.MinLat, littleLat);
                    if (lat >= nextLat - littleLat && lat < nextLat)
                    {   //与右经线相交
                        if (tempLon < endP.Longitude && tempLon > startP.Longitude)
                        {
                            double height1 = TerrainProvider.GetElevationAt(nextLat - littleLat, tempLon, level);
                            double height2 = TerrainProvider.GetElevationAt(nextLat, tempLon, level);
                            double P1 = nextLat - lat;
                            double P2 = littleLat - P1;
                            double height = (height1 * P1 + height2 * P2) / littleLat + 3;
                            points.Add(new GeoPoint(lat, tempLon, height));
                            points.AddRange(GetAllPointsFromLine(new LatLon(lat, tempLon + 0.00000001), endP, level));
                        }
                    }
                    else
                    {
                        if (lat >= nextLat)
                        {
                            //与上纬线相交
                            tempLat = nextLat;
                            if (tempLat < endP.Latitude && tempLat > startP.Latitude)
                            {
                                double lon = GetLonFromLat(startP, endP, tempLat);
                                //minMaxLatLon=GetMinMaxFromLatLon(tempLat-0.0000001,lon,level);
                                double nextLon = NextLineDegree(lon, minMaxLatLon.MinLon, littleLon);
                                double height1 = TerrainProvider.GetElevationAt(nextLat, nextLon - littleLon, level);
                                double height2 = TerrainProvider.GetElevationAt(nextLat, nextLon, level);
                                double P1 = nextLon - lon;
                                double P2 = littleLon - P1;
                                double height = (height1 * P1 + height2 * P2) / littleLat + 3;

                                points.Add(new GeoPoint(tempLat, lon, height));

                                points.AddRange(GetAllPointsFromLine(new LatLon(tempLat + 0.0000001, lon), endP, level));
                            }
                        }
                        else if (lat < nextLat - littleLat)
                        {
                            tempLat = nextLat - littleLat;
                            if (tempLat > endP.Latitude && tempLat < startP.Latitude)
                            {
                                double lon = GetLonFromLat(startP, endP, tempLat);
                                //minMaxLatLon=GetMinMaxFromLatLon(tempLat-0.0000001,lon,level);
                                double nextLon = NextLineDegree(lon, minMaxLatLon.MinLon, littleLon);
                                double height1 = TerrainProvider.GetElevationAt(nextLat - littleLat, nextLon - littleLon, level);
                                double height2 = TerrainProvider.GetElevationAt(nextLat - littleLat, nextLon, level);
                                double P1 = nextLon - lon;
                                double P2 = littleLon - P1;
                                double height = (height1 * P1 + height2 * P2) / littleLat + 3;
                                points.Add(new GeoPoint(tempLat, lon, height));
                                points.AddRange(GetAllPointsFromLine(new LatLon(tempLat - 0.0000001, lon), endP, level));
                            }
                        }

                    }
                }
            }

            return points;
        }

        /// <summary>
        /// 由地址码得到瓦片的起止经纬度
        /// </summary>
        /// <param name="adress">地址码</param>
        public static MinMaxLatLon GetMinMaxLatLon(string adress)
        {
            MinMaxLatLon minMaxLatLon = new MinMaxLatLon();
            int level = adress.Length - 1;
            string morton = adress.Substring(1);
            int octa = int.Parse(adress.Substring(0, 1));
            int n = ZeroCount(morton);
            double deltaLon = 90 * Math.Pow(2.0, n - level);
            double deltaLat = 90 * Math.Pow(2.0, -level);
            RowColNumber RC = CalRowCol(morton);
            int row = RC.Row;
            int col = RC.Col;
            //计算八分体内的经纬度
            double octagonLon = deltaLon * col;
            double octagonLat = 90 - deltaLat * row;
            LatLon topLeft = CalLonLat(octa, octagonLat, octagonLon);
            if (octa < 4)
            {
                minMaxLatLon.MinLat = topLeft.Latitude - deltaLat;
                minMaxLatLon.MinLon = topLeft.Longitude;
                minMaxLatLon.MaxLat = topLeft.Latitude;
                minMaxLatLon.MaxLon = topLeft.Longitude + deltaLon;
            }
            else
            {
                minMaxLatLon.MinLat = topLeft.Latitude;
                minMaxLatLon.MinLon = topLeft.Longitude - deltaLon;
                minMaxLatLon.MaxLat = topLeft.Latitude + deltaLat;
                minMaxLatLon.MaxLon = topLeft.Longitude;
            }
            return minMaxLatLon;
        }

        /// <summary>
        /// 得到一个经纬度点所在DQG格网的最大最小经纬度值
        /// </summary>
        /// <param name="lat">纬度</param>
        /// <param name="lon">经度</param>
        /// <param name="level">当前层次</param>
        /// <returns>最大最小经纬度值</returns>
        private static MinMaxLatLon GetMinMaxFromLatLon(double lat, double lon, int level)
        {
            int octa = GetOctaFromLatLon(lat, lon);
            double octagonLon1 = LonInOcta(octa, lon);//在八分体内的经度
            double octagonLat1 = LatInOcta(lat);//在八分体内的纬度
            double deltaLat = 90 * Math.Pow(2.0, -level);//由给定层次计算纬差
            int row = (int)Math.Pow(2.0, level) - (int)(octagonLat1 / deltaLat) - 1;//由层次和纬度、纬差求出行号
            double deltaLon = CaldeltaLon(level, row);//由行号计算出经差
            int col = (int)(octagonLon1 / deltaLon);//由经度和经差计算出列号
            double octagonLon = deltaLon * col;
            double octagonLat = 90 - deltaLat * row;
            MinMaxLatLon minMaxLatLon = new MinMaxLatLon();
            LatLon topLeft = CalLonLat(octa, octagonLat, octagonLon);
            if (octa < 4)
            {
                minMaxLatLon.MinLat = topLeft.Latitude - deltaLat;
                minMaxLatLon.MinLon = topLeft.Longitude;
                minMaxLatLon.MaxLat = topLeft.Latitude;
                minMaxLatLon.MaxLon = topLeft.Longitude + deltaLon;
            }
            else
            {
                minMaxLatLon.MinLat = topLeft.Latitude;
                minMaxLatLon.MinLon = topLeft.Longitude - deltaLon;
                minMaxLatLon.MaxLat = topLeft.Latitude + deltaLat;
                minMaxLatLon.MaxLon = topLeft.Longitude;
            }
            return minMaxLatLon;
        }


        private static RowColNumber GetRowColFromLatLon(double lat, double lon, int level)
        {
            int octa = GetOctaFromLatLon(lat, lon);
            double octagonLon = LonInOcta(octa, lon);//在八分体内的经度
            double octagonLat = LatInOcta(lat);//在八分体内的纬度
            double deltaLat = 90 * Math.Pow(2.0, -level);//由给定层次计算纬差
            int row = (int)Math.Pow(2.0, level) - (int)(octagonLat / deltaLat) - 1;//由层次和纬度、纬差求出行号
            double deltaLon = CaldeltaLon(level, row);//由行号计算出经差
            int col = (int)(octagonLon / deltaLon);//由经度和经差计算出列号
            RowColNumber rc = new RowColNumber(row, col);
            return rc;

        }

        /// <summary>
        /// 由经纬度计算Morton码
        /// </summary>
        /// <param name="lat">纬度</param>
        /// <param name="lon">经度</param>
        /// <param name="level">层号</param>
        public static string GetMortonFromLatLon(double lat, double lon, int level)
        {
            RowColNumber rc = GetRowColFromLatLon(lat, lon, level);
            int row = rc.Row;
            int col = rc.Col;
            string morton = CalMortonCode(row, col, level);//由行列号计算四进制Morton码
            if (morton == "1")
                return "1";
            return morton;
        }
        /// <summary>
        /// 查找八分体的邻近八分体
        /// </summary>
        /// <param name="Octa">原八分体编号</param>
        public static NeighborOcta FindNeighborOcta(int Octa)
        {
            NeighborOcta neighbor;
            neighbor.Right = (Octa + 9) % 4 + 4 * (int)(Octa / 4);
            neighbor.Left = (Octa + 7) % 4 + 4 * (int)(Octa / 4);
            if (Octa >= 0 && Octa < 8)
            {
                neighbor.Top = (Octa + 10 - 2 * (int)(Octa / 4)) % 4;
                neighbor.Bottom = 8 - (Octa + 10) % 4 - 2 * ((Octa + 1) % 2);
            }
            else
            {
                neighbor.Right = -1;
                neighbor.Left = -1;
                neighbor.Bottom = -1;
                neighbor.Top = -1;
            }
            if (Octa == 4)
                neighbor.Bottom = 6;
            else if (Octa == 5)
                neighbor.Bottom = 7;
            else if (Octa == 6)
                neighbor.Bottom = 4;
            else if (Octa == 7)
                neighbor.Bottom = 5;
            return neighbor;
        }
        /// <summary>
        /// 根据8分体编号及相对经纬度计算绝对经纬度
        /// </summary>
        /// <param name="octa">8分体号</param>
        /// <param name="_lon">相对纬度</param>
        /// <param name="_lat">相对经度</param>
        /// <returns></returns>
        public static LatLon CalLonLat(int octa, double octaLat, double octaLon)
        {
            LatLon ll;
            ll.Longitude = octaLon;
            ll.Latitude = octaLat;
            switch (octa)
            {
                case 0:
                    { break; }
                case 1:
                    { ll.Longitude += 90; break; }
                case 2:
                    { ll.Longitude -= 180; break; }
                case 3:
                    { ll.Longitude -= 90; break; }
                case 4:
                    { ll.Longitude = 90 - ll.Longitude; ll.Latitude = -ll.Latitude; break; }
                case 5:
                    { ll.Longitude = 180 - ll.Longitude; ll.Latitude = -ll.Latitude; break; }
                case 6:
                    { ll.Longitude = -ll.Longitude - 90; ll.Latitude = -ll.Latitude; break; }
                case 7:
                    { ll.Longitude = -ll.Longitude; ll.Latitude = -ll.Latitude; break; }
            }
            return ll;

        }
        /// <summary>
        /// 由地址码得到邻近块
        /// </summary>
        /// <param name="adress">地址码</param>
        public static List<string> AdjacentSearch(string adress)
        {
            List<string> adjacentList = new List<string>();
            if (adress == null)
                return adjacentList;
            int level = adress.Length - 1;
            string morton = adress.Substring(1);
            RowColNumber rc = CalRowCol(morton);
            int I = rc.Row;//行号
            int J = rc.Col;//列号
            int octa = int.Parse(adress.Substring(0, 1));//八分体编号
            NeighborOcta neighborOcta = FindNeighborOcta(octa);//该八分体的邻近八分体
            GridType type = GetTypeFromMorton(morton);//得到该格网的类型
            string row = Convert.ToString(I, 2);//将行号转成二进制
            bool special = OnlyContains('1', row);//判断行号是否为2的n次方减1
            string row1 = Convert.ToString(I - 1, 2);
            bool special1 = OnlyContains('1', row1);//判断行号是否为2的n次方
            if (I == 1)
            {
                special = true;
                special1 = true;
            }
            switch (type)//根据格网类型进行搜索
            {
                case GridType.A://顶三角形
                    adjacentList.Add(neighborOcta.Left.ToString() + CalMortonCode(I, J, level));//左边邻近
                    adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, J, level));//下边邻近
                    adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, J + 1, level));//下边邻近
                    adjacentList.Add(neighborOcta.Right.ToString() + CalMortonCode(I, J, level));//右边邻近
                    if (octa >= 4)
                        adjacentList.Add(neighborOcta.Bottom.ToString() + CalMortonCode(I, J, level));//顶角邻近
                    else
                        adjacentList.Add(neighborOcta.Top.ToString() + CalMortonCode(I, J, level));//顶角邻近
                    adjacentList.Add(neighborOcta.Left.ToString() + CalMortonCode(I + 1, MaxCol(level, I + 1), level));//左下角邻近
                    adjacentList.Add(neighborOcta.Right.ToString() + CalMortonCode(I + 1, 0, level));//右下角邻近
                    break;
                case GridType.B://左角四边形
                    adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, 0, level));//上边邻近
                    adjacentList.Add(neighborOcta.Left.ToString() + CalMortonCode(I, MaxCol(level, I), level));//左边邻近
                    adjacentList.Add(octa.ToString() + CalMortonCode(I, 1, level));//右边邻近
                    adjacentList.Add(neighborOcta.Left.ToString() + CalMortonCode(I - 1, MaxCol(level, I - 1), level));//左上角邻近
                    NeighborOcta leftNeighbor = FindNeighborOcta(neighborOcta.Left);//左边八分体的邻近八分体
                    if (octa >= 4)
                    {
                        adjacentList.Add(leftNeighbor.Top.ToString() + CalMortonCode(I, 0, level));//左下角邻近
                        adjacentList.Add(neighborOcta.Top.ToString() + CalMortonCode(I, MaxCol(level, I) - 1, level));//右下角邻近
                        adjacentList.Add(neighborOcta.Top.ToString() + CalMortonCode(I, MaxCol(level, I), level));//下边邻近
                    }
                    else
                    {
                        adjacentList.Add(leftNeighbor.Bottom.ToString() + CalMortonCode(I, 0, level));//左下角邻近
                        adjacentList.Add(neighborOcta.Bottom.ToString() + CalMortonCode(I, MaxCol(level, I) - 1, level));//右下角邻近
                        adjacentList.Add(neighborOcta.Bottom.ToString() + CalMortonCode(I, MaxCol(level, I), level));//下边邻近
                    }
                    adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, 1, level));//右上角邻近
                    break;
                case GridType.C://右角四边形
                    adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J, level));//上边邻近
                    adjacentList.Add(octa.ToString() + CalMortonCode(I, J - 1, level));//左边邻近
                    NeighborOcta rightNeighbor = FindNeighborOcta(neighborOcta.Right);
                    if (octa >= 4)
                    {
                        adjacentList.Add(neighborOcta.Top.ToString() + CalMortonCode(I, 0, level));//下边邻近
                        adjacentList.Add(neighborOcta.Top.ToString() + CalMortonCode(I, 1, level));//左下角邻近
                        adjacentList.Add(rightNeighbor.Top.ToString() + CalMortonCode(I, J, level));//右下角邻近
                    }
                    else
                    {
                        adjacentList.Add(neighborOcta.Bottom.ToString() + CalMortonCode(I, 0, level));//下边邻近
                        adjacentList.Add(neighborOcta.Bottom.ToString() + CalMortonCode(I, 1, level));//左下角邻近
                        adjacentList.Add(rightNeighbor.Bottom.ToString() + CalMortonCode(I, J, level));//右下角邻近
                    }
                    adjacentList.Add(neighborOcta.Right.ToString() + CalMortonCode(I, 0, level));//右边邻近
                    adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J - 1, level));//右上角邻近
                    adjacentList.Add(neighborOcta.Right.ToString() + CalMortonCode(I - 1, 0, level));//左上角邻近
                    break;
                case GridType.D://左边四边形
                    adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, 0, level));//上边邻近
                    adjacentList.Add(neighborOcta.Left.ToString() + CalMortonCode(I, MaxCol(level, I), level));//左边邻近
                    adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, 0, level));//下边邻近
                    if (special)
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, 1, level));//下边邻近
                    adjacentList.Add(octa.ToString() + CalMortonCode(I, 1, level));//右边邻近
                    adjacentList.Add(neighborOcta.Left.ToString() + CalMortonCode(I - 1, MaxCol(level, I - 1), level));//左上角邻近
                    adjacentList.Add(neighborOcta.Left.ToString() + CalMortonCode(I + 1, MaxCol(level, I + 1), level));//左下角邻近
                    if (!special)
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, 1, level));//右下角邻近
                    else
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, 2, level));//右下角邻近
                    if (!special1)
                        adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, 1, level));//右上角邻近
                    break;
                case GridType.F://底边四边形
                    adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J, level));//上边邻近
                    adjacentList.Add(octa.ToString() + CalMortonCode(I, J - 1, level));//左边邻近
                    if (octa >= 4)
                    {
                        adjacentList.Add(neighborOcta.Top.ToString() + CalMortonCode(I, MaxCol(level, I) - J, level));//下边邻近
                        adjacentList.Add(neighborOcta.Top.ToString() + CalMortonCode(I, MaxCol(level, I) - J + 1, level));//左下角邻近
                        adjacentList.Add(neighborOcta.Top.ToString() + CalMortonCode(I, MaxCol(level, I) - J - 1, level));//右下角邻近
                    }
                    else
                    {
                        adjacentList.Add(neighborOcta.Bottom.ToString() + CalMortonCode(I, MaxCol(level, I) - J, level));//下边邻近
                        adjacentList.Add(neighborOcta.Bottom.ToString() + CalMortonCode(I, MaxCol(level, I) - J + 1, level));//左下角邻近
                        adjacentList.Add(neighborOcta.Bottom.ToString() + CalMortonCode(I, MaxCol(level, I) - J - 1, level));//右下角邻近

                    }
                    adjacentList.Add(octa.ToString() + CalMortonCode(I, J + 1, level));//右边邻近
                    adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J - 1, level));//左上角邻近
                    adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J + 1, level));//右上角邻近
                    break;
                case GridType.E://右边三角形
                    if (!special1)
                        adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J, level));//上边邻近
                    else
                        adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, (J - 1) / 2, level));//上边邻近
                    adjacentList.Add(octa.ToString() + CalMortonCode(I, J - 1, level));//左边邻近
                    if (!special)
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, J, level));//下边邻近
                    else
                    {
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, 2 * J, level));//下边邻近
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, 2 * J + 1, level));//下边邻近
                    }
                    adjacentList.Add(neighborOcta.Right.ToString() + CalMortonCode(I, 0, level));//右边邻近
                    if (!special1)
                        adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J - 1, level));//左上角邻近
                    if (!special)
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, J - 1, level));//左下角邻近
                    else
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, 2 * J - 1, level));//左下角邻近
                    adjacentList.Add(neighborOcta.Right.ToString() + CalMortonCode(I + 1, 0, level));//右下角邻近
                    adjacentList.Add(neighborOcta.Right.ToString() + CalMortonCode(I - 1, 0, level));//右下角邻近
                    break;
                case GridType.G:
                    if (!special1)
                        adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J, level));//上边邻近
                    else
                        adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J / 2, level));//上边邻近
                    adjacentList.Add(octa.ToString() + CalMortonCode(I, J - 1, level));//左边邻近
                    if (!special)
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, J, level));//下边邻近
                    else
                    {
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, 2 * J, level));//下边邻近
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, 2 * J + 1, level));//下边邻近
                    }
                    adjacentList.Add(octa.ToString() + CalMortonCode(I, J + 1, level));//右边邻近
                    if (!special1)
                    {
                        adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J - 1, level));//左上角邻近
                        adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J + 1, level));//右上角邻近
                    }
                    else if (J % 2 == 0)
                        adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J / 2 - 1, level));//左上角邻近
                    else if (J % 2 == 1)
                        adjacentList.Add(octa.ToString() + CalMortonCode(I - 1, J / 2 + 1, level));//右上角邻近
                    if (!special)
                    {
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, J - 1, level));//左下角邻近
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, J + 1, level));//右下角邻近
                    }
                    else
                    {
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, 2 * J - 1, level));//左下角邻近
                        adjacentList.Add(octa.ToString() + CalMortonCode(I + 1, 2 * J + 2, level));//右下角邻近
                    }
                    break;
                default:
                    adjacentList.Clear();
                    break;
            }
            return adjacentList;
        }
    }
    /// <summary>
    /// 格网类型
    /// </summary>
    public enum GridType
    {
        A = 0,
        B,
        C,
        D,
        E,
        F,
        G
    }
    /// <summary>
    /// 行列号
    /// </summary>
    public struct RowColNumber
    {
        public int Row;
        public int Col;
        public RowColNumber(int row, int col)
        {
            this.Row = row;
            this.Col = col;
        }
    }
    /// <summary>
    /// 八分体的邻近八分体
    /// </summary>
    public struct NeighborOcta
    {
        public int Right;
        public int Left;
        public int Top;
        public int Bottom;
    }
    /// <summary>
    /// 起止经纬度
    /// </summary>
    public struct MinMaxLatLon
    {
        public double MinLat;
        public double MaxLat;
        public double MinLon;
        public double MaxLon;
        public MinMaxLatLon(double minLat,double maxLat,double minLon,double maxLon)
        {
            this.MinLat = minLat;
            this.MinLon = minLon;
            this.MaxLat = maxLat;
            this.MaxLon = maxLon;
        }
    }
    /// <summary>
    /// 经纬度
    /// </summary>
    public struct LatLon
    {
        public double Longitude;
        public double Latitude;
        public LatLon(double lat,double lon)
        {
            this.Latitude = lat;
            this.Longitude = lon;
        }
    }
}

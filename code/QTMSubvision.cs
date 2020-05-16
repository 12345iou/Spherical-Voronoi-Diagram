using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.DirectX.Direct3D;
using MME.Globe.Core;
using MME.Globe.Core.MathLib;
using MME.Globe.Core.Renderable;
using System.IO;
using System.Drawing;
using Microsoft.DirectX;
using MME.Globe.Core.DEM;

namespace MME.Global.QTM
{
    /// <summary>
    /// QTM三角形类，编码方法为顺序编码，剖分方式为经度等分，纬度等分，有共用顶点，使用索引
    /// </summary>
    public class QTMSubdivision : RenderableObject
    {
        #region 私有成员变量
        private int m_level;
        private int[] m_indices;
        private int m_oriTriangleColor;
        private float m_seaLevel = 0;
        private float m_terrainExaggeration = 15;
        private int m_seaColor = Color.Blue.ToArgb();
        int m_dqgLevel = 0;

        private CustomVertex.PositionColored[] m_vertices0;
        private CustomVertex.PositionColored[] m_vertices1;
        private CustomVertex.PositionColored[] m_vertices2;
        private CustomVertex.PositionColored[] m_vertices3;
        private CustomVertex.PositionColored[] m_vertices4;
        private CustomVertex.PositionColored[] m_vertices5;
        private CustomVertex.PositionColored[] m_vertices6;
        private CustomVertex.PositionColored[] m_vertices7;
        #endregion

        #region 构造方法
        /// <summary>
        /// 构造方法
        /// </summary>
        /// <param name="name">描述信息</param>
        /// <param name="level">格网层次</param>
        /// <param name="inIds">种子点编号数组</param>
        public QTMSubdivision(string name, int level)
            : base(name)
        {
            this.m_level = level;
            this.m_oriTriangleColor = Color.White.ToArgb();
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 判断某一三角形是否为边界三角形
        /// </summary>
        /// <param name="center">三角形的编号</param>
        /// <param name="level">层次</param>
        /// <param name="voronoiIDs">所有三角形的归属列表</param>
        /// <returns></returns>
        private bool IsBoundary(int center, int level, int[] voronoiIDs)
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

        #region 从RenderableObject继承方法

        public override void Initialize(DrawArgs drawArgs)
        {
            bool isOutputCenter = false;
            bool isOutputCord = true;

            double radius = World.Settings.WorldRadius;
            int I = (int)Math.Pow(2, m_level);//三角形行数
            int pointRowCount = I + 1;//顶点行数
            int pointCount = pointRowCount * (pointRowCount + 1) / 2;//总点数
            int octaTriCount = (int)Math.Pow(4, m_level);
            int allTriCount = octaTriCount * 8;

            #region 计算索引
            int[] startIndex = new int[pointRowCount];
            int[] endIndex = new int[pointRowCount];
            startIndex[0] = 0; endIndex[0] = 0;
            for (int i = 1; i < pointRowCount; i++)
            {
                startIndex[i] = startIndex[i - 1] + i;
                endIndex[i] = startIndex[i] + i;
            }
            List<int> indices = new List<int>();
            for (int i = 0; i < I; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    indices.Add(startIndex[i] + j);
                    indices.Add(startIndex[i + 1] + j + 1);
                    indices.Add(startIndex[i + 1] + j);
                    indices.Add(startIndex[i] + j);
                    indices.Add(startIndex[i] + j + 1);
                    indices.Add(startIndex[i + 1] + j + 1);
                }
                indices.Add(endIndex[i]);
                indices.Add(endIndex[i + 1]);
                indices.Add(endIndex[i + 1] - 1);
            }
            m_indices = indices.ToArray();
            #endregion

            StreamWriter sw = null;
            string fileName = m_level.ToString() + ".txt";
            if (!File.Exists(fileName))
            {
                sw = new StreamWriter(fileName);
                isOutputCenter = true;
            }
            StreamWriter sw1 = null;
            string fileName1 = m_level.ToString() + "_Cord.txt";

            if (!File.Exists(fileName1))
            {
                sw1 = new StreamWriter(fileName1);
                isOutputCord = true;
            }
            else
                isOutputCord = false;

            try
            {
                #region 0号八分体

                this.m_vertices0 = new CustomVertex.PositionColored[pointCount];
                this.m_vertices0[0].Position = MathEngine.SphericalToCartesian(90.0, 45.0, radius);
                this.m_vertices0[0].Color = m_oriTriangleColor;
                int curIndex = 1;

                //double lat_a = Math.PI / 2;//起始纬度(纬线环法)

                for (int i = 1; i < pointRowCount; i++)
                {
                    int J = i + 1;//第i行点的个数

                    //double lat_radian = Math.Acos(Math.Pow(1 - Math.Pow((Math.Pow(1 - Math.Pow(Math.Cos(lat_a), 2), 0.5) - (2 * i - 1) / Math.Pow(4, m_level)), 2), 0.5));//当前纬线环,单位弧度（纬线环法）
                    //lat_a = lat_radian;

                    for (int j = 0; j < J; j++)
                    {
                        
                        //double lat = MathEngine.RadiansToDegrees(lat_radian);


                        double lat = 90.0 - (i * 90.0 / I);
                        double lon = j * 90.0 / (J - 1);
                        float alt = TerrainProvider.GetElevationAt(lat, lon, m_dqgLevel) * m_terrainExaggeration;
                        Vector3 pos = MathEngine.SphericalToCartesian(lat, lon, radius+alt);
                        m_vertices0[curIndex].Position = pos;
                        if (alt > m_seaLevel)
                            m_vertices0[curIndex].Color = m_oriTriangleColor;//原始颜色
                        else
                            m_vertices0[curIndex].Color = Color.White.ToArgb();
                            //m_vertices0[curIndex].Color = m_seaColor;
                        curIndex++;
                    }
                }
                Vector3 center;
                if (isOutputCenter)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        center = 1.0f / 3.0f * (m_vertices0[m_indices[i * 3 + 0]].Position
                                                      + m_vertices0[m_indices[i * 3 + 1]].Position
                                                      + m_vertices0[m_indices[i * 3 + 2]].Position);
                        sw.WriteLine(center.X.ToString() + "," + center.Y.ToString() + "," + center.Z.ToString());
                    }
                }
                if (isOutputCord)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        sw1.WriteLine(m_vertices0[m_indices[i * 3 + 0]].Position.X.ToString() + "," + m_vertices0[m_indices[i * 3 + 0]].Position.Y.ToString() + "," + m_vertices0[m_indices[i * 3 + 0]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices0[m_indices[i * 3 + 1]].Position.X.ToString() + "," + m_vertices0[m_indices[i * 3 + 1]].Position.Y.ToString() + "," + m_vertices0[m_indices[i * 3 + 1]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices0[m_indices[i * 3 + 2]].Position.X.ToString() + "," + m_vertices0[m_indices[i * 3 + 2]].Position.Y.ToString() + "," + m_vertices0[m_indices[i * 3 + 2]].Position.Z.ToString());
                    }
                }
                #endregion

                #region 1号八分体

                this.m_vertices1 = new CustomVertex.PositionColored[pointCount];
                this.m_vertices1[0].Position = MathEngine.SphericalToCartesian(90.0, 45.0, radius);
                this.m_vertices1[0].Color = m_oriTriangleColor;
                curIndex = 1;
                for (int i = 1; i < pointRowCount; i++)
                {
                    int J = i + 1;//第i行点的个数
                    for (int j = 0; j < J; j++)
                    {
                        double lat = 90.0 - (i * 90.0 / I);
                        double lon = j * 90.0 / (J - 1) + 90;
                        float alt = TerrainProvider.GetElevationAt(lat, lon, m_dqgLevel) * m_terrainExaggeration;
                        Vector3 pos = MathEngine.SphericalToCartesian(lat, lon, radius + alt);
                        m_vertices1[curIndex].Position = pos;
                        if (alt > m_seaLevel)
                            m_vertices1[curIndex].Color = m_oriTriangleColor;//原始颜色
                        else
                            m_vertices1[curIndex].Color = Color.White.ToArgb();
                        curIndex++;
                    }
                }
                if (isOutputCenter)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        center = 1.0f / 3.0f * (m_vertices1[m_indices[i * 3 + 0]].Position
                                                      + m_vertices1[m_indices[i * 3 + 1]].Position
                                                      + m_vertices1[m_indices[i * 3 + 2]].Position);
                        sw.WriteLine(center.X.ToString() + "," + center.Y.ToString() + "," + center.Z.ToString());
                    }
                }
                if (isOutputCord)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        sw1.WriteLine(m_vertices1[m_indices[i * 3 + 0]].Position.X.ToString() + "," + m_vertices1[m_indices[i * 3 + 0]].Position.Y.ToString() + "," + m_vertices1[m_indices[i * 3 + 0]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices1[m_indices[i * 3 + 1]].Position.X.ToString() + "," + m_vertices1[m_indices[i * 3 + 1]].Position.Y.ToString() + "," + m_vertices1[m_indices[i * 3 + 1]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices1[m_indices[i * 3 + 2]].Position.X.ToString() + "," + m_vertices1[m_indices[i * 3 + 2]].Position.Y.ToString() + "," + m_vertices1[m_indices[i * 3 + 2]].Position.Z.ToString());
                    }
                }

                #endregion

                #region 2号八分体

                this.m_vertices2 = new CustomVertex.PositionColored[pointCount];
                this.m_vertices2[0].Position = MathEngine.SphericalToCartesian(90.0, 45.0, radius);
                this.m_vertices2[0].Color = m_oriTriangleColor;
                curIndex = 1;
                for (int i = 1; i < pointRowCount; i++)
                {
                    int J = i + 1;//第i行点的个数
                    for (int j = 0; j < J; j++)
                    {
                        double lat = 90.0 - (i * 90.0 / I);
                        double lon = j * 90.0 / (J - 1) - 180;
                        float alt = TerrainProvider.GetElevationAt(lat, lon, m_dqgLevel) * m_terrainExaggeration;
                        Vector3 pos = MathEngine.SphericalToCartesian(lat, lon, radius + alt);
                        m_vertices2[curIndex].Position = pos;
                        if (alt > m_seaLevel)
                            m_vertices2[curIndex].Color = m_oriTriangleColor;//原始颜色
                        else
                            m_vertices2[curIndex].Color = Color.White.ToArgb();
                        curIndex++;
                    }
                }
                if (isOutputCenter)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        center = 1.0f / 3.0f * (m_vertices2[m_indices[i * 3 + 0]].Position
                                                      + m_vertices2[m_indices[i * 3 + 1]].Position
                                                      + m_vertices2[m_indices[i * 3 + 2]].Position);
                        sw.WriteLine(center.X.ToString() + "," + center.Y.ToString() + "," + center.Z.ToString());
                    }
                }
                if (isOutputCord)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        sw1.WriteLine(m_vertices2[m_indices[i * 3 + 0]].Position.X.ToString() + "," + m_vertices2[m_indices[i * 3 + 0]].Position.Y.ToString() + "," + m_vertices2[m_indices[i * 3 + 0]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices2[m_indices[i * 3 + 1]].Position.X.ToString() + "," + m_vertices2[m_indices[i * 3 + 1]].Position.Y.ToString() + "," + m_vertices2[m_indices[i * 3 + 1]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices2[m_indices[i * 3 + 2]].Position.X.ToString() + "," + m_vertices2[m_indices[i * 3 + 2]].Position.Y.ToString() + "," + m_vertices2[m_indices[i * 3 + 2]].Position.Z.ToString());
                    }
                }
                #endregion

                #region 3号八分体

                this.m_vertices3 = new CustomVertex.PositionColored[pointCount];
                this.m_vertices3[0].Position = MathEngine.SphericalToCartesian(90.0, 45.0, radius);
                this.m_vertices3[0].Color = m_oriTriangleColor;
                curIndex = 1;
                for (int i = 1; i < pointRowCount; i++)
                {
                    int J = i + 1;//第i行点的个数
                    for (int j = 0; j < J; j++)
                    {
                        double lat = 90.0 - (i * 90.0 / I);
                        double lon = j * 90.0 / (J - 1) - 90;
                        float alt = TerrainProvider.GetElevationAt(lat, lon, m_dqgLevel) * m_terrainExaggeration;
                        Vector3 pos = MathEngine.SphericalToCartesian(lat, lon, radius + alt);
                        m_vertices3[curIndex].Position = pos;
                        if (alt > m_seaLevel)
                            m_vertices3[curIndex].Color = m_oriTriangleColor;//原始颜色
                        else
                            m_vertices3[curIndex].Color = Color.White.ToArgb();
                        curIndex++;
                    }
                }
                if (isOutputCenter)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        center = 1.0f / 3.0f * (m_vertices3[m_indices[i * 3 + 0]].Position
                                                      + m_vertices3[m_indices[i * 3 + 1]].Position
                                                      + m_vertices3[m_indices[i * 3 + 2]].Position);
                        sw.WriteLine(center.X.ToString() + "," + center.Y.ToString() + "," + center.Z.ToString());
                    }
                }
                if (isOutputCord)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        sw1.WriteLine(m_vertices3[m_indices[i * 3 + 0]].Position.X.ToString() + "," + m_vertices3[m_indices[i * 3 + 0]].Position.Y.ToString() + "," + m_vertices3[m_indices[i * 3 + 0]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices3[m_indices[i * 3 + 1]].Position.X.ToString() + "," + m_vertices3[m_indices[i * 3 + 1]].Position.Y.ToString() + "," + m_vertices3[m_indices[i * 3 + 1]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices3[m_indices[i * 3 + 2]].Position.X.ToString() + "," + m_vertices3[m_indices[i * 3 + 2]].Position.Y.ToString() + "," + m_vertices3[m_indices[i * 3 + 2]].Position.Z.ToString());
                    }
                }




                #endregion

                #region 4号八分体

                this.m_vertices4 = new CustomVertex.PositionColored[pointCount];
                this.m_vertices4[0].Position = MathEngine.SphericalToCartesian(-90.0, 45.0, radius);
                this.m_vertices4[0].Color = m_oriTriangleColor;
                curIndex = 1;
                for (int i = 1; i < pointRowCount; i++)
                {
                    int J = i + 1;//第i行点的个数
                    for (int j = 0; j < J; j++)
                    {
                        double lat = -90.0 + (i * 90.0 / I);
                        double lon = 90.0 - j * 90.0 / (J - 1);
                        float alt = TerrainProvider.GetElevationAt(lat, lon, m_dqgLevel) * m_terrainExaggeration;
                        Vector3 pos = MathEngine.SphericalToCartesian(lat, lon, radius + alt);
                        m_vertices4[curIndex].Position = pos;
                        if (alt > m_seaLevel)
                            m_vertices4[curIndex].Color = m_oriTriangleColor;//原始颜色
                        else
                            m_vertices4[curIndex].Color = Color.White.ToArgb();
                        curIndex++;
                    }
                }
                if (isOutputCenter)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        center = 1.0f / 3.0f * (m_vertices4[m_indices[i * 3 + 0]].Position
                                                      + m_vertices4[m_indices[i * 3 + 1]].Position
                                                      + m_vertices4[m_indices[i * 3 + 2]].Position);
                        sw.WriteLine(center.X.ToString() + "," + center.Y.ToString() + "," + center.Z.ToString());
                    }
                }

                if (isOutputCord)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        sw1.WriteLine(m_vertices4[m_indices[i * 3 + 0]].Position.X.ToString() + "," + m_vertices4[m_indices[i * 3 + 0]].Position.Y.ToString() + "," + m_vertices4[m_indices[i * 3 + 0]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices4[m_indices[i * 3 + 1]].Position.X.ToString() + "," + m_vertices4[m_indices[i * 3 + 1]].Position.Y.ToString() + "," + m_vertices4[m_indices[i * 3 + 1]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices4[m_indices[i * 3 + 2]].Position.X.ToString() + "," + m_vertices4[m_indices[i * 3 + 2]].Position.Y.ToString() + "," + m_vertices4[m_indices[i * 3 + 2]].Position.Z.ToString());
                    }
                }

                #endregion

                #region 5号八分体

                this.m_vertices5 = new CustomVertex.PositionColored[pointCount];
                this.m_vertices5[0].Position = MathEngine.SphericalToCartesian(-90.0, 45.0, radius);
                this.m_vertices5[0].Color = m_oriTriangleColor;
                curIndex = 1;
                for (int i = 1; i < pointRowCount; i++)
                {
                    int J = i + 1;//第i行点的个数
                    for (int j = 0; j < J; j++)
                    {
                        double lat = -90.0 + (i * 90.0 / I);
                        double lon = 90.0 - j * 90.0 / (J - 1) + 90.0;
                        float alt = TerrainProvider.GetElevationAt(lat, lon, m_dqgLevel) * m_terrainExaggeration;
                        Vector3 pos = MathEngine.SphericalToCartesian(lat, lon, radius + alt);
                        m_vertices5[curIndex].Position = pos;
                        if (alt > m_seaLevel)
                            m_vertices5[curIndex].Color = m_oriTriangleColor;//原始颜色
                        else
                            m_vertices5[curIndex].Color = Color.White.ToArgb();
                        curIndex++;
                    }
                }
                if (isOutputCenter)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        center = 1.0f / 3.0f * (m_vertices5[m_indices[i * 3 + 0]].Position
                                                      + m_vertices5[m_indices[i * 3 + 1]].Position
                                                      + m_vertices5[m_indices[i * 3 + 2]].Position);
                        sw.WriteLine(center.X.ToString() + "," + center.Y.ToString() + "," + center.Z.ToString());
                    }
                }

                #endregion

                #region 6号八分体

                this.m_vertices6 = new CustomVertex.PositionColored[pointCount];
                this.m_vertices6[0].Position = MathEngine.SphericalToCartesian(-90.0, 45.0, radius);
                this.m_vertices6[0].Color = m_oriTriangleColor;
                curIndex = 1;
                for (int i = 1; i < pointRowCount; i++)
                {
                    int J = i + 1;//第i行点的个数
                    for (int j = 0; j < J; j++)
                    {
                        double lat = -90.0 + (i * 90.0 / I);
                        double lon = 90.0 - j * 90.0 / (J - 1) - 180.0;
                        float alt = TerrainProvider.GetElevationAt(lat, lon, m_dqgLevel) * m_terrainExaggeration;
                        Vector3 pos = MathEngine.SphericalToCartesian(lat, lon, radius + alt);
                        m_vertices6[curIndex].Position = pos;
                        if (alt > m_seaLevel)
                            m_vertices6[curIndex].Color = m_oriTriangleColor;//原始颜色
                        else
                            m_vertices6[curIndex].Color = Color.White.ToArgb();
                        curIndex++;
                    }
                }
                if (isOutputCenter)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        center = 1.0f / 3.0f * (m_vertices6[m_indices[i * 3 + 0]].Position
                                                      + m_vertices6[m_indices[i * 3 + 1]].Position
                                                      + m_vertices6[m_indices[i * 3 + 2]].Position);
                        sw.WriteLine(center.X.ToString() + "," + center.Y.ToString() + "," + center.Z.ToString());
                    }
                }
                if (isOutputCord)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        sw1.WriteLine(m_vertices6[m_indices[i * 3 + 0]].Position.X.ToString() + "," + m_vertices6[m_indices[i * 3 + 0]].Position.Y.ToString() + "," + m_vertices6[m_indices[i * 3 + 0]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices6[m_indices[i * 3 + 1]].Position.X.ToString() + "," + m_vertices6[m_indices[i * 3 + 1]].Position.Y.ToString() + "," + m_vertices6[m_indices[i * 3 + 1]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices6[m_indices[i * 3 + 2]].Position.X.ToString() + "," + m_vertices6[m_indices[i * 3 + 2]].Position.Y.ToString() + "," + m_vertices6[m_indices[i * 3 + 2]].Position.Z.ToString());
                    }
                }

                #endregion

                #region 7号八分体

                this.m_vertices7 = new CustomVertex.PositionColored[pointCount];
                this.m_vertices7[0].Position = MathEngine.SphericalToCartesian(-90.0, 45.0, radius);
                this.m_vertices7[0].Color = m_oriTriangleColor;
                curIndex = 1;
                for (int i = 1; i < pointRowCount; i++)
                {
                    int J = i + 1;//第i行点的个数
                    for (int j = 0; j < J; j++)
                    {
                        double lat = -90.0 + (i * 90.0 / I);
                        double lon = 90.0 - j * 90.0 / (J - 1) - 90.0;
                        float alt = TerrainProvider.GetElevationAt(lat, lon, m_dqgLevel) * m_terrainExaggeration;
                        Vector3 pos = MathEngine.SphericalToCartesian(lat, lon, radius + alt);
                        m_vertices7[curIndex].Position = pos;
                        if (alt > m_seaLevel)
                            m_vertices7[curIndex].Color = m_oriTriangleColor;//原始颜色
                        else
                            m_vertices7[curIndex].Color = Color.White.ToArgb();
                        curIndex++;
                    }
                }
                if (isOutputCenter)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        center = 1.0f / 3.0f * (m_vertices7[m_indices[i * 3 + 0]].Position
                                                      + m_vertices7[m_indices[i * 3 + 1]].Position
                                                      + m_vertices7[m_indices[i * 3 + 2]].Position);
                        sw.WriteLine(center.X.ToString() + "," + center.Y.ToString() + "," + center.Z.ToString());
                    }
                }
                if (isOutputCord)
                {
                    for (int i = 0; i < m_indices.Length / 3; i++)
                    {
                        sw1.WriteLine(m_vertices7[m_indices[i * 3 + 0]].Position.X.ToString() + "," + m_vertices7[m_indices[i * 3 + 0]].Position.Y.ToString() + "," + m_vertices7[m_indices[i * 3 + 0]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices7[m_indices[i * 3 + 1]].Position.X.ToString() + "," + m_vertices7[m_indices[i * 3 + 1]].Position.Y.ToString() + "," + m_vertices7[m_indices[i * 3 + 1]].Position.Z.ToString());
                        sw1.WriteLine(m_vertices7[m_indices[i * 3 + 2]].Position.X.ToString() + "," + m_vertices7[m_indices[i * 3 + 2]].Position.Y.ToString() + "," + m_vertices7[m_indices[i * 3 + 2]].Position.Z.ToString());
                    }
                }
                #endregion
                if (sw != null)
                    sw.Close();
                if (sw1 != null)
                    sw1.Close();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            this.m_isInitialized = true;
        }

        public override void Update(DrawArgs drawArgs)
        {
            if (m_isInitialized)
                return;
            this.Initialize(drawArgs);
        }

        public override void Render(DrawArgs drawArgs)
        {
            if (!this.m_isInitialized || !this.IsOn)
                return;
            //drawArgs.device.Clear(ClearFlags.Target, Color.White, 0, 1);
            TextureOperation oriColorOperation = drawArgs.device.TextureState[0].ColorOperation;
            VertexFormats oriFormat = drawArgs.device.VertexFormat;
            bool oriLighting = drawArgs.device.RenderState.Lighting;
            Cull oriCullMode = drawArgs.device.RenderState.CullMode;
            Matrix oriWorldMatrix = drawArgs.device.Transform.World;
            ShadeMode oriShadeMode = drawArgs.device.RenderState.ShadeMode;
            FillMode oriFillMode = drawArgs.device.RenderState.FillMode;
            try
            {
                drawArgs.device.TextureState[0].ColorOperation = TextureOperation.Disable;
                drawArgs.device.VertexFormat = CustomVertex.PositionColored.Format;
                drawArgs.device.RenderState.ShadeMode = ShadeMode.Phong;
                drawArgs.device.RenderState.CullMode = Cull.None;
                drawArgs.device.RenderState.Lighting = false;
                drawArgs.device.RenderState.FillMode = FillMode.WireFrame;
                drawArgs.device.SetTransform(TransformType.World, Matrix.Translation(-drawArgs.WorldCamera.ReferenceCenter.Vector3));
                drawArgs.device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0, m_vertices0.Length, m_indices.Length / 3, m_indices, false, m_vertices0);
                drawArgs.device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0, m_vertices1.Length, m_indices.Length / 3, m_indices, false, m_vertices1);
                drawArgs.device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0, m_vertices2.Length, m_indices.Length / 3, m_indices, false, m_vertices2);
                drawArgs.device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0, m_vertices3.Length, m_indices.Length / 3, m_indices, false, m_vertices3);
                drawArgs.device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0, m_vertices4.Length, m_indices.Length / 3, m_indices, false, m_vertices4);
                drawArgs.device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0, m_vertices5.Length, m_indices.Length / 3, m_indices, false, m_vertices5);
                drawArgs.device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0, m_vertices6.Length, m_indices.Length / 3, m_indices, false, m_vertices6);
                drawArgs.device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0, m_vertices7.Length, m_indices.Length / 3, m_indices, false, m_vertices7);
            }
            catch (System.Exception ex)
            {
                drawArgs.device.TextureState[0].ColorOperation = oriColorOperation;
                drawArgs.device.VertexFormat = oriFormat;
                drawArgs.device.RenderState.Lighting = oriLighting;
                drawArgs.device.RenderState.CullMode = oriCullMode;
                drawArgs.device.Transform.World = oriWorldMatrix;
                drawArgs.device.RenderState.ShadeMode = oriShadeMode;
                drawArgs.device.RenderState.FillMode = oriFillMode;
            }
        }

        public override void Dispose()
        {
            this.m_isInitialized = false;
            if (this.m_vertices0 != null)
                this.m_vertices0 = null;
            if (this.m_vertices1 != null)
                this.m_vertices1 = null;
            if (this.m_vertices2 != null)
                this.m_vertices2 = null;
            if (this.m_vertices3 != null)
                this.m_vertices3 = null;
            if (this.m_vertices4 != null)
                this.m_vertices4 = null;
            if (this.m_vertices5 != null)
                this.m_vertices5 = null;
            if (this.m_vertices6 != null)
                this.m_vertices6 = null;
            if (this.m_vertices7 != null)
                this.m_vertices7 = null;
            if (this.m_indices != null)
                this.m_indices = null;
        }
        #endregion
    }
}

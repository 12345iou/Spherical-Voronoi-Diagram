using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MME.Globe.Core.MathLib;
using Microsoft.DirectX.Direct3D;
using MME.Globe.Core.DEM;
using System.Drawing;
using Microsoft.DirectX;
using System.IO;
using MME.Globe.Core.Renderable;
using System.Windows.Forms;

namespace MME.Globe.Core.DQG
{
    /// <summary>
    /// DQG四边形瓦片类
    /// </summary>
    public class DQGQuadTile : IDisposable
    {
        #region 私有字段
        private double m_west;
        private double m_east;
        private double m_north;
        private double m_south;
        private Angle m_centerLatitude;
        private Angle m_centerLongitude;
        private double m_latitudeSpan;
        private double m_longitudeSpan;
        private int m_level;        
        private bool m_isInitialized;
        private Texture m_texture;
        private int vertexCount = (int)Math.Pow(2, DQGTileSet.BaseLevel);
        private double m_layerRadius = 6378137.0;
        private DQGQuadTile m_northWestChild;
        private DQGQuadTile m_southWestChild;
        private DQGQuadTile m_northEastChild;
        private DQGQuadTile m_southEastChild;
        private CustomVertex.PositionTextured[] m_northWestVertices;
        private CustomVertex.PositionTextured[] m_southWestVertices;
        private CustomVertex.PositionTextured[] m_northEastVertices;
        private CustomVertex.PositionTextured[] m_southEastVertices;
        private short[] m_vertexIndexesNormal;
        private short[] m_vertexIndexesCrack;
        private Vector3d m_localOrigin;
        private string m_texturePath;
        private string m_address;
        private bool m_haveCrack;
        private float m_verticalExaggeration;
        private float m_minElevation = 0;
        private float m_maxElevation = 0;
        private Effect m_effect = null;
        private string m_effectPath;
        #endregion

        #region 公共成员

        /// <summary>
        /// 瓦片的包围盒
        /// </summary>
        public BoundingBox BoundingBox;
        #endregion

        #region 构造方法
        /// <summary>
        /// 构造方法
        /// </summary>
        /// <param name="address">瓦片的地址码</param>
        /// <param name="south">南</param>
        /// <param name="north">北</param>
        /// <param name="west">西</param>
        /// <param name="east">东</param>
        /// <param name="level">层次</param>
        public DQGQuadTile(string address, double south, double north, double west, double east, int level)
        {
            this.m_address = address;
            this.m_south = south;
            this.m_north = north;
            this.m_west = west;
            this.m_east = east;
            m_centerLatitude = Angle.FromDegrees(0.5f * (m_north + m_south));
            m_centerLongitude = Angle.FromDegrees(0.5f * (m_west + m_east));
            m_latitudeSpan = Math.Abs(m_north - m_south);
            m_longitudeSpan = Math.Abs(m_east - m_west);
            this.m_level = level;
            BoundingBox = new BoundingBox((float)south, (float)north, (float)west, (float)east,
                                (float)m_layerRadius, (float)m_layerRadius + 5000000f);
            m_localOrigin = new Vector3d();
            m_localOrigin = MathEngine.SphericalToCartesianD(m_centerLatitude, m_centerLongitude, m_layerRadius);
            m_localOrigin.X = (float)(Math.Round(m_localOrigin.X / 10000) * 10000);
            m_localOrigin.Y = (float)(Math.Round(m_localOrigin.Y / 10000) * 10000);
            m_localOrigin.Z = (float)(Math.Round(m_localOrigin.Z / 10000) * 10000);
            m_haveCrack = HaveCrack();
            m_maxElevation = TerrainProvider.GetElevationAt(m_centerLatitude.Degrees, m_centerLongitude.Degrees, this.m_level);
            m_minElevation = m_maxElevation;
            this.m_texturePath = World.Settings.ImagePath + @"\" + m_level.ToString() + @"\" + this.m_address[0] + @"\" + this.m_address + ".jpg";
            this.m_effectPath = Application.StartupPath + @"\Data\Shaders\grayscale.fx1";
            
        }
        #endregion

        #region 私有方法
        private DQGQuadTile ComputeChild(string address, double childSouth, double childNorth, double childWest, double childEast)
        {
            DQGQuadTile child = new DQGQuadTile(
                address,
                childSouth,
                childNorth,
                childWest,
                childEast,
                this.m_level + 1);
            return child;
        }
        private void ComputeChildren(DrawArgs drawArgs)
        {
            if (m_level + 1 >= DQGTileSet.LevelCount)
                return;

            double CenterLat = 0.5f * (m_south + m_north);
            double CenterLon = 0.5f * (m_east + m_west);
            if (CenterLat > 0)
            {
                if (m_northWestChild == null)
                    m_northWestChild = ComputeChild(m_address + "0", CenterLat, m_north, m_west, CenterLon);

                if (m_northEastChild == null)
                    m_northEastChild = ComputeChild(m_address + "1", CenterLat, m_north, CenterLon, m_east);

                if (m_southWestChild == null)
                    m_southWestChild = ComputeChild(m_address + "2", m_south, CenterLat, m_west, CenterLon);

                if (m_southEastChild == null)
                    m_southEastChild = ComputeChild(m_address + "3", m_south, CenterLat, CenterLon, m_east);
            }
            else
            {
                if (m_northWestChild == null)
                    m_northWestChild = ComputeChild(m_address + "3", CenterLat, m_north, m_west, CenterLon);

                if (m_northEastChild == null)
                    m_northEastChild = ComputeChild(m_address + "2", CenterLat, m_north, CenterLon, m_east);

                if (m_southWestChild == null)
                    m_southWestChild = ComputeChild(m_address + "1", m_south, CenterLat, m_west, CenterLon);

                if (m_southEastChild == null)
                    m_southEastChild = ComputeChild(m_address + "0", m_south, CenterLat, CenterLon, m_east);
            }
        }        
        private bool HaveCrack()
        {
            int Dmorton = 0; string Bmorton = ""; string _row = "";
            string morton = this.m_address.Substring(1);
            //将morton变成十进制
            for (int i = 0; i < morton.Length; i++)
            {
                Dmorton += Convert.ToInt32(morton.Substring(i, 1)) * (int)Math.Pow(4.0, morton.Length - 1 - i);
            }
            //将十进制Morton变成二进制
            Bmorton = Convert.ToString(Dmorton, 2);
            for (int i = 0; i < Bmorton.Length; i = i + 2)
            {
                _row = Bmorton.Substring(Bmorton.Length - 2 - i, 1) + _row;
            }
            if ((!_row.Contains("0")) && m_north != 0 && m_south != 0)
                return true;
            else
                return false;
        }
        //private void CalculateNormals(ref CustomVertex.PositionTextured[] vertices, short[] indices)
        //{
        //    System.Collections.ArrayList[] normal_buffer = new System.Collections.ArrayList[vertices.Length];
        //    for (int i = 0; i < vertices.Length; i++)
        //    {
        //        normal_buffer[i] = new System.Collections.ArrayList();
        //    }
        //    for (int i = 0; i < indices.Length; i += 3)
        //    {
        //        Vector3 p1 = vertices[indices[i + 0]].Position;
        //        Vector3 p2 = vertices[indices[i + 1]].Position;
        //        Vector3 p3 = vertices[indices[i + 2]].Position;

        //        Vector3 v1 = p2 - p1;
        //        Vector3 v2 = p3 - p1;
        //        Vector3 normal = Vector3.Cross(v1, v2);

        //        normal.Normalize();

        //        normal_buffer[indices[i + 0]].Add(normal);
        //        normal_buffer[indices[i + 1]].Add(normal);
        //        normal_buffer[indices[i + 2]].Add(normal);
        //    }

        //    for (int i = 0; i < vertices.Length; ++i)
        //    {
        //        for (int j = 0; j < normal_buffer[i].Count; ++j)
        //        {
        //            Vector3 curNormal = (Vector3)normal_buffer[i][j];

        //            if (vertices[i].Normal == Vector3.Empty)
        //                vertices[i].Normal = curNormal;
        //            else
        //                vertices[i].Normal += curNormal;
        //        }
        //        vertices[i].Normal.Multiply(1.0f / normal_buffer[i].Count);
        //    }

        //}
        private void CreateTileMesh()
        {
            m_verticalExaggeration = World.Settings.VerticalExaggeration;
            if (Math.Abs(m_verticalExaggeration) > 1e-3)
                CreateElevatedEdgedMesh();
            else
                CreateFlatMesh();
                //CreateGPUFlatMesh();
        }
        private Vector3 ProjectOnMeshBase(Vector3 p)
        {
            float meshBaseRadius = (float)(m_layerRadius + m_minElevation * m_verticalExaggeration - 500 * m_verticalExaggeration);
            p += this.m_localOrigin.Vector3;
            p.Normalize();
            p = p * meshBaseRadius - this.m_localOrigin.Vector3;
            return p;
        }
        /// <summary>
        /// 创建有高程有侧边的网格
        /// </summary>
        private void CreateElevatedEdgedMesh()
        {
            DateTime start = DateTime.Now;
            double layerRadius = (double)m_layerRadius;
            double scaleFactor = 1.0 / (double)vertexCount;
            int thisVertexCount = vertexCount / 2;
            int thisVertexCountPlus3 = thisVertexCount + 3;
            int thisVertexCountPlus2 = thisVertexCount + 2;
            int thisVertexCountPlus1 = thisVertexCount + 1;
            int totalVertexCount = thisVertexCountPlus3 * thisVertexCountPlus3;

            m_northWestVertices = new CustomVertex.PositionTextured[totalVertexCount];
            m_southWestVertices = new CustomVertex.PositionTextured[totalVertexCount];
            m_northEastVertices = new CustomVertex.PositionTextured[totalVertexCount];
            m_southEastVertices = new CustomVertex.PositionTextured[totalVertexCount];
            if (m_haveCrack)
            {
                if (m_centerLatitude.Degrees > 0)
                {
                    m_southWestVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCountPlus2];
                    m_southEastVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCountPlus2];
                }
                else
                {
                    m_northWestVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCountPlus2];
                    m_northEastVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCountPlus2];
                }
            }            
            
            int baseIndex;
            double deltaLon = scaleFactor * m_longitudeSpan;
            double deltaLat = scaleFactor * m_latitudeSpan;
            baseIndex = 0;
            for (int i = -1; i < thisVertexCountPlus2; i++)
            {
                double lat = m_north - i * deltaLat;
                for (int j = -1; j < thisVertexCountPlus2; j++)
                {
                    double lon = m_west + j * deltaLon;
                    float height = TerrainProvider.GetElevationAt(lat, lon, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    double radius = layerRadius + height * m_verticalExaggeration;
                    m_northWestVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lon, radius) - m_localOrigin.Vector3;
                    m_northWestVertices[baseIndex].Tu = (float)(j * scaleFactor);
                    m_northWestVertices[baseIndex].Tv = (float)(i * scaleFactor);

                    baseIndex += 1;
                }
            }

            baseIndex = 0;
            for (int i = -1; i < thisVertexCountPlus2; i++)
            {
                double lat = 0.5 * (m_north + m_south) - i * deltaLat;
                for (int j = -1; j < thisVertexCountPlus2; j++)
                {
                    double lon = m_west + j * deltaLon;
                    float height = TerrainProvider.GetElevationAt(lat, lon, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    double radius = layerRadius + height * m_verticalExaggeration;
                    m_southWestVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lon, radius) - m_localOrigin.Vector3;
                    m_southWestVertices[baseIndex].Tu = (float)(j * scaleFactor);
                    m_southWestVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);
                    baseIndex += 1;
                }
            }

            baseIndex = 0;
            for (int i = -1; i < thisVertexCountPlus2; i++)
            {
                double lat = m_north - i * deltaLat;
                for (int j = -1; j < thisVertexCountPlus2; j++)
                {
                    double lon = 0.5 * (m_west + m_east) + j * deltaLon;
                    float height = TerrainProvider.GetElevationAt(lat, lon, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    double radius = layerRadius + height * m_verticalExaggeration;
                    m_northEastVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lon, radius) - m_localOrigin.Vector3;
                    m_northEastVertices[baseIndex].Tu = (float)((j + thisVertexCount) * scaleFactor);
                    m_northEastVertices[baseIndex].Tv = (float)(i * scaleFactor);

                    baseIndex += 1;
                }
            }

            baseIndex = 0;
            for (int i = -1; i < thisVertexCountPlus2; i++)
            {
                double lat = 0.5 * (m_north + m_south) - i * deltaLat;
                for (int j = -1; j < thisVertexCountPlus2; j++)
                {
                    double lon = 0.5 * (m_west + m_east) + j * deltaLon;
                    float height = TerrainProvider.GetElevationAt(lat, lon, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    double radius = layerRadius + height * m_verticalExaggeration;
                    m_southEastVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lon, radius) - m_localOrigin.Vector3;
                    m_southEastVertices[baseIndex].Tu = (float)((j + thisVertexCount) * scaleFactor);
                    m_southEastVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);

                    baseIndex += 1;
                }
            }

            for (int i = 0; i < thisVertexCountPlus3;i +=thisVertexCountPlus2 )
            {
                for(int j=0;j<thisVertexCountPlus3;j++)
                {
                    int index = 1;
                    if (i != 0)
                        index = -1;
                    m_northWestVertices[thisVertexCountPlus3 * i + j].Position = ProjectOnMeshBase(m_northWestVertices[thisVertexCountPlus3 * (i + index) + j].Position);
                    m_northEastVertices[thisVertexCountPlus3 * i + j].Position = ProjectOnMeshBase(m_northEastVertices[thisVertexCountPlus3 * (i + index) + j].Position);
                    m_southWestVertices[thisVertexCountPlus3 * i + j].Position = ProjectOnMeshBase(m_southWestVertices[thisVertexCountPlus3 * (i + index) + j].Position);
                    m_southEastVertices[thisVertexCountPlus3 * i + j].Position = ProjectOnMeshBase(m_southEastVertices[thisVertexCountPlus3 * (i + index) + j].Position);
                }
            }
            for (int i = 0; i < thisVertexCountPlus3; i++)
            {
                for (int j = 0; j < thisVertexCountPlus3; j += thisVertexCountPlus2)
                {
                    int index = 1;
                    if (j != 0)
                        index = -1; 
                    m_northWestVertices[thisVertexCountPlus3 * i + j].Position = ProjectOnMeshBase(m_northWestVertices[thisVertexCountPlus3 * i + j + index].Position);
                    m_northEastVertices[thisVertexCountPlus3 * i + j].Position = ProjectOnMeshBase(m_northEastVertices[thisVertexCountPlus3 * i + j + index].Position);
                    m_southWestVertices[thisVertexCountPlus3 * i + j].Position = ProjectOnMeshBase(m_southWestVertices[thisVertexCountPlus3 * i + j + index].Position);
                    m_southEastVertices[thisVertexCountPlus3 * i + j].Position = ProjectOnMeshBase(m_southEastVertices[thisVertexCountPlus3 * i + j + index].Position);                    
                }
            }

            m_vertexIndexesNormal = new short[2 * thisVertexCountPlus2 * thisVertexCountPlus2 * 3];
            baseIndex = 0;
            for (int i = 0; i < thisVertexCountPlus2; i++)
            {
                for (int j = 0; j < thisVertexCountPlus2; j++)
                {
                    m_vertexIndexesNormal[baseIndex] = (short)(i * thisVertexCountPlus3 + j);
                    m_vertexIndexesNormal[baseIndex + 1] = (short)((i + 1) * thisVertexCountPlus3 + j);
                    m_vertexIndexesNormal[baseIndex + 2] = (short)(i * thisVertexCountPlus3 + j + 1);

                    m_vertexIndexesNormal[baseIndex + 3] = (short)(i * thisVertexCountPlus3 + j + 1);
                    m_vertexIndexesNormal[baseIndex + 4] = (short)((i + 1) * thisVertexCountPlus3 + j);
                    m_vertexIndexesNormal[baseIndex + 5] = (short)((i + 1) * thisVertexCountPlus3 + j + 1);

                    baseIndex += 6;
                }
            }
            if (m_haveCrack)
            {
                int normalCount = 2 * thisVertexCount * thisVertexCountPlus2 * 3;
                m_vertexIndexesCrack = new short[normalCount + 2 * thisVertexCountPlus2 * 9];
                if (m_centerLatitude.Degrees > 0)
                {
                    //北半同层球裂缝处理
                    //顶点处理
                    baseIndex = totalVertexCount;
                    for (int j = -1; j < thisVertexCountPlus2 - 1; j++)
                    {
                        double lon = m_west + (0.5 + j) * deltaLon;
                        if (j == -1)
                            lon = m_west;
                        else if (j == thisVertexCount)
                            lon = (m_west + m_east) * 0.5;
                        float height = TerrainProvider.GetElevationAt(m_south, lon, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = layerRadius + height * m_verticalExaggeration;
                        m_southWestVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_south, lon, radius) - m_localOrigin.Vector3;
                        m_southWestVertices[baseIndex].Tu = (float)((j + 0.5) * scaleFactor);
                        m_southWestVertices[baseIndex].Tv = 1.0f;

                        lon = (m_west + m_east) * 0.5 + (0.5 + j) * deltaLon;
                        if (j == -1)
                            lon = (m_west + m_east) * 0.5;
                        else if (j == thisVertexCount)
                            lon = m_east;
                        height = TerrainProvider.GetElevationAt(m_south, lon, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        radius = layerRadius + height * m_verticalExaggeration;
                        m_southEastVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_south, lon, radius) - m_localOrigin.Vector3;
                        m_southEastVertices[baseIndex].Tu = (float)((j + thisVertexCount + 0.5) * scaleFactor);
                        m_southEastVertices[baseIndex].Tv = 1.0f;
                        baseIndex += 1;
                    }

                    //索引处理
                    for (int i = 0; i < normalCount; i++)
                    {
                        m_vertexIndexesCrack[i] = m_vertexIndexesNormal[i];
                    }
                    baseIndex = normalCount;
                    for (int j = 0; j < thisVertexCountPlus2; j++)
                    {
                        m_vertexIndexesCrack[baseIndex] = (short)(thisVertexCount * thisVertexCountPlus3 + j);
                        m_vertexIndexesCrack[baseIndex + 1] = (short)(thisVertexCountPlus1 * thisVertexCountPlus3 + j);
                        m_vertexIndexesCrack[baseIndex + 2] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 3] = (short)(thisVertexCount * thisVertexCountPlus3 + j);
                        m_vertexIndexesCrack[baseIndex + 4] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 5] = (short)(thisVertexCount * thisVertexCountPlus3 + j + 1);

                        m_vertexIndexesCrack[baseIndex + 6] = (short)(thisVertexCount * thisVertexCountPlus3 + j + 1);
                        m_vertexIndexesCrack[baseIndex + 7] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 8] = (short)(thisVertexCountPlus1 * thisVertexCountPlus3 + j + 1);
                        baseIndex += 9;
                    }
                    baseIndex = normalCount + 9 * thisVertexCountPlus2;
                    for (int j = 0; j < thisVertexCountPlus2; j++)
                    {
                        m_vertexIndexesCrack[baseIndex] = (short)(thisVertexCountPlus1 * thisVertexCountPlus3 + j);
                        m_vertexIndexesCrack[baseIndex + 1] = (short)(thisVertexCountPlus2 * thisVertexCountPlus3 + j);
                        m_vertexIndexesCrack[baseIndex + 2] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 3] = (short)(thisVertexCountPlus2 * thisVertexCountPlus3 + j);
                        m_vertexIndexesCrack[baseIndex + 4] = (short)(thisVertexCountPlus2 * thisVertexCountPlus3 + j + 1);
                        m_vertexIndexesCrack[baseIndex + 5] = (short)(totalVertexCount + j);


                        m_vertexIndexesCrack[baseIndex + 6] = (short)((thisVertexCountPlus1) * thisVertexCountPlus3 + j + 1);
                        m_vertexIndexesCrack[baseIndex + 7] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 8] = (short)(thisVertexCountPlus2 * thisVertexCountPlus3 + j + 1);
                        baseIndex += 9;
                    }
                }
                else
                {
                    //南半球裂缝处理
                    //顶点处理
                    baseIndex = totalVertexCount;
                    for (int j = -1; j < thisVertexCountPlus2 - 1; j++)
                    {
                        double lon = m_west + (0.5 + j) * deltaLon;
                        if (j == -1)
                            lon = m_west;
                        if (j == thisVertexCount)
                            lon = (m_west + m_east) * 0.5;
                        float height = TerrainProvider.GetElevationAt(m_north, lon, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = layerRadius + height * m_verticalExaggeration;
                        m_northWestVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_north, lon, radius) - m_localOrigin.Vector3;
                        m_northWestVertices[baseIndex].Tu = (float)((j + 0.5) * scaleFactor);
                        m_northWestVertices[baseIndex].Tv = 0.0f;

                        lon = (m_west + m_east) * 0.5 + (0.5 + j) * deltaLon;
                        if (j == -1)
                            lon = (m_west + m_east) * 0.5;
                        else if (j == thisVertexCount)
                            lon = m_east;
                        height = TerrainProvider.GetElevationAt(m_north, lon, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        radius = layerRadius + height * m_verticalExaggeration;
                        m_northEastVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_north, lon, radius) - m_localOrigin.Vector3;
                        m_northEastVertices[baseIndex].Tu = (float)((j + thisVertexCount + 0.5) * scaleFactor);
                        m_northEastVertices[baseIndex].Tv = 0.0f;
                        baseIndex += 1;
                    }

                    //索引处理
                    for (int i = 0; i < normalCount; i++)
                    {
                        m_vertexIndexesCrack[9 * thisVertexCountPlus2 * 2 + i] = m_vertexIndexesNormal[6 * thisVertexCountPlus2 * 2 + i];
                    }

                    baseIndex = 0;
                    for (int j = 0; j < thisVertexCountPlus2; j++)
                    {
                        m_vertexIndexesCrack[baseIndex] = (short)j;
                        m_vertexIndexesCrack[baseIndex + 1] = (short)(thisVertexCountPlus3 + j);
                        m_vertexIndexesCrack[baseIndex + 2] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 3] = (short)j;
                        m_vertexIndexesCrack[baseIndex + 4] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 5] = (short)(j + 1);

                        m_vertexIndexesCrack[baseIndex + 6] = (short)(j + 1);
                        m_vertexIndexesCrack[baseIndex + 7] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 8] = (short)(thisVertexCountPlus3 + j + 1);
                        baseIndex += 9;
                    }
                    baseIndex = 9 * thisVertexCountPlus2;
                    for (int j = 0; j < thisVertexCountPlus2; j++)
                    {
                        m_vertexIndexesCrack[baseIndex] = (short)(thisVertexCountPlus3 + j);
                        m_vertexIndexesCrack[baseIndex + 1] = (short)(thisVertexCountPlus3 * 2 + j);
                        m_vertexIndexesCrack[baseIndex + 2] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 3] = (short)(thisVertexCountPlus3 * 2 + j);
                        m_vertexIndexesCrack[baseIndex + 4] = (short)(thisVertexCountPlus3 * 2 + j + 1);
                        m_vertexIndexesCrack[baseIndex + 5] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 6] = (short)(thisVertexCountPlus3 + j + 1);
                        m_vertexIndexesCrack[baseIndex + 7] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 8] = (short)(thisVertexCountPlus3 * 2 + j + 1);
                        baseIndex += 9;
                    }
                }
            }
            #region 写入文件
            //if (this.m_address.Equals("130"))
            //{
            //    string fileName = @"D:\130.dqg";
            //    FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            //    BinaryWriter bw = new BinaryWriter(fs);
            //    bw.Write(m_address);
            //    bw.Write(1);
            //    bw.Write(m_haveCrack);
            //    bw.Write(m_localOrigin.X);
            //    bw.Write(m_localOrigin.Y);
            //    bw.Write(m_localOrigin.Z);
            //    bw.Write(m_northEastVertices.Length);
            //    for (int i = 0; i < m_northEastVertices.Length; i++)
            //    {
            //        bw.Write(m_northEastVertices[i].X);
            //        bw.Write(m_northEastVertices[i].Y);
            //        bw.Write(m_northEastVertices[i].Z);
            //        bw.Write(m_northEastVertices[i].Tu);
            //        bw.Write(m_northEastVertices[i].Tv);
            //    }
            //    bw.Write(m_northWestVertices.Length);
            //    for (int i = 0; i < m_northWestVertices.Length; i++)
            //    {
            //        bw.Write(m_northWestVertices[i].X);
            //        bw.Write(m_northWestVertices[i].Y);
            //        bw.Write(m_northWestVertices[i].Z);
            //        bw.Write(m_northWestVertices[i].Tu);
            //        bw.Write(m_northWestVertices[i].Tv);
            //    }
            //    bw.Write(m_southEastVertices.Length);
            //    for (int i = 0; i < m_southEastVertices.Length; i++)
            //    {
            //        bw.Write(m_southEastVertices[i].X);
            //        bw.Write(m_southEastVertices[i].Y);
            //        bw.Write(m_southEastVertices[i].Z);
            //        bw.Write(m_southEastVertices[i].Tu);
            //        bw.Write(m_southEastVertices[i].Tv);
            //    }
            //    bw.Write(m_southWestVertices.Length);
            //    for (int i = 0; i < m_southWestVertices.Length; i++)
            //    {
            //        bw.Write(m_southWestVertices[i].X);
            //        bw.Write(m_southWestVertices[i].Y);
            //        bw.Write(m_southWestVertices[i].Z);
            //        bw.Write(m_southWestVertices[i].Tu);
            //        bw.Write(m_southWestVertices[i].Tv);
            //    }
            //    bw.Write(m_vertexIndexesNormal.Length);
            //    for (int i = 0; i < m_vertexIndexesNormal.Length; i++)
            //    {
            //        bw.Write(m_vertexIndexesNormal[i]);
            //    }
            //    if (m_haveCrack)
            //    {
            //        if (m_centerLatitude.Degrees > 0)
            //        {
            //            bw.Write(false);
            //            bw.Write(false);
            //            bw.Write(true);
            //            bw.Write(true);
            //        }
            //        else
            //        {
            //            bw.Write(true);
            //            bw.Write(true);
            //            bw.Write(false);
            //            bw.Write(false);
            //        }
            //        bw.Write(m_vertexIndexesCrack.Length);
            //        for (int i = 0; i < m_vertexIndexesCrack.Length; i++)
            //        {
            //            bw.Write(m_vertexIndexesCrack[i]);
            //        }
            //    }
            //    bw.Close();
            //    fs.Close();
            //    bw = null;
            //    fs = null;
            //}
            #endregion
            TimeSpan last = DateTime.Now - start;
            Console.WriteLine(last.Milliseconds);

            

        }
        
        
        
        
        
        
        /// <summary>
        /// 创建有高程的网格
        /// </summary>
        private void CreateElevatedMesh()
        {
            double layerRadius = (double)m_layerRadius;
            double scaleFactor = 1.0 / (double)vertexCount;
            int thisVertexCount = vertexCount / 2;
            int thisVertexCountPlus1 = thisVertexCount + 1;
            int totalVertexCount = thisVertexCountPlus1 * thisVertexCountPlus1;

            m_northWestVertices = new CustomVertex.PositionTextured[totalVertexCount];
            m_southWestVertices = new CustomVertex.PositionTextured[totalVertexCount];
            m_northEastVertices = new CustomVertex.PositionTextured[totalVertexCount];
            m_southEastVertices = new CustomVertex.PositionTextured[totalVertexCount];
            if (m_haveCrack)
            {
                if (m_centerLatitude.Degrees > 0)
                {
                    m_southWestVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
                    m_southEastVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
                }
                else
                {
                    m_northWestVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
                    m_northEastVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
                }
            }
            int baseIndex;
            double deltaLon = scaleFactor * m_longitudeSpan;
            double deltaLat = scaleFactor * m_latitudeSpan;
            baseIndex = 0;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                double lat = m_north - i * deltaLat;
                for (int j = 0; j < thisVertexCountPlus1; j++)
                {
                    double lon = m_west + j * deltaLon;
                    float height = TerrainProvider.GetElevationAt(lat, lon, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    double radius = layerRadius + height * m_verticalExaggeration;
                    m_northWestVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lon, radius) - m_localOrigin.Vector3;
                    m_northWestVertices[baseIndex].Tu = (float)(j * scaleFactor);
                    m_northWestVertices[baseIndex].Tv = (float)(i * scaleFactor);
                    //m_northWestVertices[baseIndex].Normal = new Vector3(m_northWestVertices[baseIndex].X + (float)m_localOrigin.X, m_northWestVertices[baseIndex].Y + (float)m_localOrigin.Y, m_northWestVertices[baseIndex].Z + (float)m_localOrigin.Z);
                    //m_northWestVertices[baseIndex].Normal.Normalize();

                    baseIndex += 1;
                }
            }

            baseIndex = 0;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                double lat = 0.5 * (m_north + m_south) - i * deltaLat;
                for (int j = 0; j < thisVertexCountPlus1; j++)
                {
                    double lon = m_west + j * deltaLon;
                    float height = TerrainProvider.GetElevationAt(lat, lon, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    double radius = layerRadius + height * m_verticalExaggeration;
                    m_southWestVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lon, radius) - m_localOrigin.Vector3;
                    m_southWestVertices[baseIndex].Tu = (float)(j * scaleFactor);
                    m_southWestVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);
                    baseIndex += 1;
                }
            }

            baseIndex = 0;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                double lat = m_north - i * deltaLat;
                for (int j = 0; j < thisVertexCountPlus1; j++)
                {
                    double lon = 0.5 * (m_west + m_east) + j * deltaLon;
                    float height = TerrainProvider.GetElevationAt(lat, lon, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    double radius = layerRadius + height * m_verticalExaggeration;
                    m_northEastVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lon, radius) - m_localOrigin.Vector3;
                    m_northEastVertices[baseIndex].Tu = (float)((j + thisVertexCount) * scaleFactor);
                    m_northEastVertices[baseIndex].Tv = (float)(i * scaleFactor);

                    baseIndex += 1;
                }
            }

            baseIndex = 0;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                double lat = 0.5 * (m_north + m_south) - i * deltaLat;
                for (int j = 0; j < thisVertexCountPlus1; j++)
                {
                    double lon = 0.5 * (m_west + m_east) + j * deltaLon;
                    float height = TerrainProvider.GetElevationAt(lat, lon, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    double radius = layerRadius + height * m_verticalExaggeration;
                    m_southEastVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lon, radius) - m_localOrigin.Vector3;
                    m_southEastVertices[baseIndex].Tu = (float)((j + thisVertexCount) * scaleFactor);
                    m_southEastVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);
                    //m_southEastVertices[baseIndex].Normal = new Vector3(m_southEastVertices[baseIndex].X + (float)m_localOrigin.X, m_southEastVertices[baseIndex].Y + (float)m_localOrigin.Y, m_southEastVertices[baseIndex].Z + (float)m_localOrigin.Z);
                    //m_southEastVertices[baseIndex].Normal.Normalize();

                    baseIndex += 1;
                }

            }
            m_vertexIndexesNormal = new short[2 * thisVertexCount * thisVertexCount * 3];

            for (int i = 0; i < thisVertexCount; i++)
            {
                baseIndex = (2 * 3 * i * thisVertexCount);

                for (int j = 0; j < thisVertexCount; j++)
                {
                    m_vertexIndexesNormal[baseIndex] = (short)(i * thisVertexCountPlus1 + j);
                    m_vertexIndexesNormal[baseIndex + 1] = (short)((i + 1) * thisVertexCountPlus1 + j);
                    m_vertexIndexesNormal[baseIndex + 2] = (short)(i * thisVertexCountPlus1 + j + 1);

                    m_vertexIndexesNormal[baseIndex + 3] = (short)(i * thisVertexCountPlus1 + j + 1);
                    m_vertexIndexesNormal[baseIndex + 4] = (short)((i + 1) * thisVertexCountPlus1 + j);
                    m_vertexIndexesNormal[baseIndex + 5] = (short)((i + 1) * thisVertexCountPlus1 + j + 1);

                    baseIndex += 6;
                }
            }
            if (m_haveCrack)
            {
                m_vertexIndexesCrack = new short[(2 * thisVertexCount + 1) * thisVertexCount * 3];
                int normalCount = 2 * (thisVertexCount - 1) * thisVertexCount * 3;
                if (m_centerLatitude.Degrees > 0)
                {
                    //北半同层球裂缝处理
                    //顶点处理
                    baseIndex = totalVertexCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        double lon = m_west + (0.5 + j) * deltaLon;
                        float height = TerrainProvider.GetElevationAt(m_south, lon, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = layerRadius + height * m_verticalExaggeration;
                        m_southWestVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_south, lon, radius) - m_localOrigin.Vector3;
                        m_southWestVertices[baseIndex].Tu = (float)((j + 0.5) * scaleFactor);
                        m_southWestVertices[baseIndex].Tv = 1.0f;
                        baseIndex += 1;
                    }

                    baseIndex = totalVertexCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        double lon = (m_west + m_east) * 0.5 + (0.5 + j) * deltaLon;
                        float height = TerrainProvider.GetElevationAt(m_south, lon, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = layerRadius + height * m_verticalExaggeration;
                        m_southEastVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_south, lon, radius) - m_localOrigin.Vector3;
                        m_southEastVertices[baseIndex].Tu = (float)((j + thisVertexCount + 0.5) * scaleFactor);
                        m_southEastVertices[baseIndex].Tv = 1.0f;
                        baseIndex += 1;
                    }
                    //索引处理
                    for (int i = 0; i < normalCount; i++)
                    {
                        m_vertexIndexesCrack[i] = m_vertexIndexesNormal[i];
                    }
                    baseIndex = normalCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        m_vertexIndexesCrack[baseIndex] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 1] = (short)(thisVertexCount * thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 2] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 3] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 4] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 5] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j + 1);

                        m_vertexIndexesCrack[baseIndex + 6] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j + 1);
                        m_vertexIndexesCrack[baseIndex + 7] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 8] = (short)(thisVertexCount * thisVertexCountPlus1 + j + 1);
                        baseIndex += 9;
                    }
                }
                else
                {
                    //南半球裂缝处理
                    //顶点处理

                    baseIndex = totalVertexCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        double lon = m_west + (0.5 + j) * deltaLon;
                        float height = TerrainProvider.GetElevationAt(m_north, lon, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = layerRadius + height * m_verticalExaggeration;
                        m_northWestVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_north, lon, radius) - m_localOrigin.Vector3;
                        m_northWestVertices[baseIndex].Tu = (float)((j + 0.5) * scaleFactor);
                        m_northWestVertices[baseIndex].Tv = 0.0f;
                        baseIndex += 1;
                    }

                    baseIndex = totalVertexCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        double lon = (m_west + m_east) * 0.5 + (0.5 + j) * deltaLon;
                        float height = TerrainProvider.GetElevationAt(m_north, lon, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = layerRadius + height * m_verticalExaggeration;
                        m_northEastVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_north, lon, radius) - m_localOrigin.Vector3;
                        m_northEastVertices[baseIndex].Tu = (float)((j + thisVertexCount + 0.5) * scaleFactor);
                        m_northEastVertices[baseIndex].Tv = 0.0f;
                        baseIndex += 1;
                    }
                    //索引处理
                    for (int i = 0; i < normalCount; i++)
                    {
                        m_vertexIndexesCrack[9 * thisVertexCount + i] = m_vertexIndexesNormal[6 * thisVertexCount + i];
                    }

                    baseIndex = 0;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        m_vertexIndexesCrack[baseIndex] = (short)j;
                        m_vertexIndexesCrack[baseIndex + 1] = (short)(thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 2] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 3] = (short)(thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 4] = (short)(thisVertexCountPlus1 + j + 1);
                        m_vertexIndexesCrack[baseIndex + 5] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 6] = (short)(j + 1);
                        m_vertexIndexesCrack[baseIndex + 7] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 8] = (short)(thisVertexCountPlus1 + j + 1);
                        baseIndex += 9;
                    }
                }
            }

        }
        /// <summary>
        /// 创建平面网格
        /// </summary>
        private void CreateFlatMesh()
        {
            double layerRadius = (double)m_layerRadius;
            double scaleFactor = 1.0 / (double)vertexCount;
            int thisVertexCount = vertexCount / 2;
            int thisVertexCountPlus1 = thisVertexCount + 1;
            int totalVertexCount = thisVertexCountPlus1 * thisVertexCountPlus1;

            m_northWestVertices = new CustomVertex.PositionTextured[totalVertexCount];
            m_southWestVertices = new CustomVertex.PositionTextured[totalVertexCount];
            m_northEastVertices = new CustomVertex.PositionTextured[totalVertexCount];
            m_southEastVertices = new CustomVertex.PositionTextured[totalVertexCount];
            if (m_haveCrack)
            {
                if (m_centerLatitude.Degrees > 0)
                {
                    m_southWestVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
                    m_southEastVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
                }
                else
                {
                    m_northWestVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
                    m_northEastVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
                }

            }
            const double Degrees2Radians = System.Math.PI / 180.0;

            double[] sinLon = new double[thisVertexCountPlus1];
            double[] cosLon = new double[thisVertexCountPlus1];

            int baseIndex;
            double angle = m_west * Degrees2Radians;
            double deltaAngle = scaleFactor * m_longitudeSpan * Degrees2Radians;

            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                angle = m_west * Degrees2Radians + i * deltaAngle;
                sinLon[i] = Math.Sin(angle);
                cosLon[i] = Math.Cos(angle);
            }

            baseIndex = 0;
            angle = m_north * Degrees2Radians;
            deltaAngle = -scaleFactor * m_latitudeSpan * Degrees2Radians;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                angle = m_north * Degrees2Radians + i * deltaAngle;
                double sinLat = Math.Sin(angle);
                double radCosLat = Math.Cos(angle) * layerRadius;

                for (int j = 0; j < thisVertexCountPlus1; j++)
                {
                    m_northWestVertices[baseIndex].X = (float)(radCosLat * cosLon[j] - m_localOrigin.X);
                    m_northWestVertices[baseIndex].Y = (float)(radCosLat * sinLon[j] - m_localOrigin.Y);
                    m_northWestVertices[baseIndex].Z = (float)(layerRadius * sinLat - m_localOrigin.Z);
                    m_northWestVertices[baseIndex].Tu = (float)(j * scaleFactor);
                    m_northWestVertices[baseIndex].Tv = (float)(i * scaleFactor);
                    //m_northWestVertices[baseIndex].Normal = new Vector3(m_northWestVertices[baseIndex].X + (float)m_localOrigin.X, m_northWestVertices[baseIndex].Y + (float)m_localOrigin.Y, m_northWestVertices[baseIndex].Z + (float)m_localOrigin.Z);
                    //m_northWestVertices[baseIndex].Normal.Normalize();

                    baseIndex += 1;
                }
            }

            baseIndex = 0;
            angle = 0.5 * (m_north + m_south) * Degrees2Radians;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                angle = 0.5 * (m_north + m_south) * Degrees2Radians + i * deltaAngle;
                double sinLat = Math.Sin(angle);
                double radCosLat = Math.Cos(angle) * layerRadius;

                for (int j = 0; j < thisVertexCountPlus1; j++)
                {
                    m_southWestVertices[baseIndex].X = (float)(radCosLat * cosLon[j] - m_localOrigin.X);
                    m_southWestVertices[baseIndex].Y = (float)(radCosLat * sinLon[j] - m_localOrigin.Y);
                    m_southWestVertices[baseIndex].Z = (float)(layerRadius * sinLat - m_localOrigin.Z);
                    m_southWestVertices[baseIndex].Tu = (float)(j * scaleFactor);
                    m_southWestVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);
                    //m_southWestVertices[baseIndex].Normal = new Vector3(m_southWestVertices[baseIndex].X + (float)m_localOrigin.X, m_southWestVertices[baseIndex].Y + (float)m_localOrigin.Y, m_southWestVertices[baseIndex].Z + (float)m_localOrigin.Z);
                    //m_southWestVertices[baseIndex].Normal.Normalize();
                    baseIndex += 1;
                }
            }

            angle = 0.5 * (m_west + m_east) * Degrees2Radians;
            deltaAngle = scaleFactor * m_longitudeSpan * Degrees2Radians;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                angle = 0.5 * (m_west + m_east) * Degrees2Radians + i * deltaAngle;
                sinLon[i] = Math.Sin(angle);
                cosLon[i] = Math.Cos(angle);
            }

            baseIndex = 0;
            angle = m_north * Degrees2Radians;
            deltaAngle = -scaleFactor * m_latitudeSpan * Degrees2Radians;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                angle = m_north * Degrees2Radians + i * deltaAngle;
                double sinLat = Math.Sin(angle);
                double radCosLat = Math.Cos(angle) * layerRadius;

                for (int j = 0; j < thisVertexCountPlus1; j++)
                {
                    m_northEastVertices[baseIndex].X = (float)(radCosLat * cosLon[j] - m_localOrigin.X);
                    m_northEastVertices[baseIndex].Y = (float)(radCosLat * sinLon[j] - m_localOrigin.Y);
                    m_northEastVertices[baseIndex].Z = (float)(layerRadius * sinLat - m_localOrigin.Z);
                    m_northEastVertices[baseIndex].Tu = (float)((j + thisVertexCount) * scaleFactor);
                    m_northEastVertices[baseIndex].Tv = (float)(i * scaleFactor);
                    //m_northEastVertices[baseIndex].Normal = new Vector3(m_northEastVertices[baseIndex].X + (float)m_localOrigin.X, m_northEastVertices[baseIndex].Y + (float)m_localOrigin.Y, m_northEastVertices[baseIndex].Z + (float)m_localOrigin.Z);
                    //m_northEastVertices[baseIndex].Normal.Normalize();

                    baseIndex += 1;
                }
            }

            baseIndex = 0;
            angle = 0.5f * (m_north + m_south) * Degrees2Radians;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                angle = 0.5 * (m_north + m_south) * Degrees2Radians + i * deltaAngle;
                double sinLat = Math.Sin(angle);
                double radCosLat = Math.Cos(angle) * layerRadius;

                for (int j = 0; j < thisVertexCountPlus1; j++)
                {
                    m_southEastVertices[baseIndex].X = (float)(radCosLat * cosLon[j] - m_localOrigin.X);
                    m_southEastVertices[baseIndex].Y = (float)(radCosLat * sinLon[j] - m_localOrigin.Y);
                    m_southEastVertices[baseIndex].Z = (float)(layerRadius * sinLat - m_localOrigin.Z);
                    m_southEastVertices[baseIndex].Tu = (float)((j + thisVertexCount) * scaleFactor);
                    m_southEastVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);
                    //m_southEastVertices[baseIndex].Normal = new Vector3(m_southEastVertices[baseIndex].X + (float)m_localOrigin.X, m_southEastVertices[baseIndex].Y + (float)m_localOrigin.Y, m_southEastVertices[baseIndex].Z + (float)m_localOrigin.Z);
                    //m_southEastVertices[baseIndex].Normal.Normalize();

                    baseIndex += 1;
                }

            }
            m_vertexIndexesNormal = new short[2 * thisVertexCount * thisVertexCount * 3];

            for (int i = 0; i < thisVertexCount; i++)
            {
                baseIndex = (2 * 3 * i * thisVertexCount);

                for (int j = 0; j < thisVertexCount; j++)
                {
                    m_vertexIndexesNormal[baseIndex] = (short)(i * thisVertexCountPlus1 + j);
                    m_vertexIndexesNormal[baseIndex + 1] = (short)((i + 1) * thisVertexCountPlus1 + j);
                    m_vertexIndexesNormal[baseIndex + 2] = (short)(i * thisVertexCountPlus1 + j + 1);

                    m_vertexIndexesNormal[baseIndex + 3] = (short)(i * thisVertexCountPlus1 + j + 1);
                    m_vertexIndexesNormal[baseIndex + 4] = (short)((i + 1) * thisVertexCountPlus1 + j);
                    m_vertexIndexesNormal[baseIndex + 5] = (short)((i + 1) * thisVertexCountPlus1 + j + 1);

                    baseIndex += 6;
                }
            }
            if (m_haveCrack)
            {
                m_vertexIndexesCrack = new short[(2 * thisVertexCount + 1) * thisVertexCount * 3];
                int normalCount = 2 * (thisVertexCount - 1) * thisVertexCount * 3;
                if (m_centerLatitude.Degrees > 0)
                {
                    //北半同层球裂缝处理
                    //顶点处理
                    double sinSouth = Math.Sin(m_south * Degrees2Radians);
                    double radCosSouth = Math.Cos(m_south * Degrees2Radians) * layerRadius;

                    baseIndex = totalVertexCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        double lon = (m_west + (0.5 + j) * scaleFactor * m_longitudeSpan) * Degrees2Radians;
                        m_southWestVertices[baseIndex].X = (float)(radCosSouth * Math.Cos(lon) - m_localOrigin.X);
                        m_southWestVertices[baseIndex].Y = (float)(radCosSouth * Math.Sin(lon) - m_localOrigin.Y);
                        m_southWestVertices[baseIndex].Z = (float)(layerRadius * sinSouth - m_localOrigin.Z);
                        m_southWestVertices[baseIndex].Tu = (float)((j + 0.5) * scaleFactor);
                        m_southWestVertices[baseIndex].Tv = 1.0f;
                        baseIndex += 1;
                    }

                    baseIndex = totalVertexCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        double lon = ((m_west + m_east) * 0.5 + (0.5 + j) * scaleFactor * m_longitudeSpan) * Degrees2Radians;
                        m_southEastVertices[baseIndex].X = (float)(radCosSouth * Math.Cos(lon) - m_localOrigin.X);
                        m_southEastVertices[baseIndex].Y = (float)(radCosSouth * Math.Sin(lon) - m_localOrigin.Y);
                        m_southEastVertices[baseIndex].Z = (float)(layerRadius * sinSouth - m_localOrigin.Z);
                        m_southEastVertices[baseIndex].Tu = (float)((j + thisVertexCount + 0.5) * scaleFactor);
                        m_southEastVertices[baseIndex].Tv = 1.0f;
                        baseIndex += 1;
                    }
                    //索引处理
                    for (int i = 0; i < normalCount; i++)
                    {
                        m_vertexIndexesCrack[i] = m_vertexIndexesNormal[i];
                    }
                    baseIndex = normalCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        m_vertexIndexesCrack[baseIndex] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 1] = (short)(thisVertexCount * thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 2] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 3] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 4] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 5] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j + 1);

                        m_vertexIndexesCrack[baseIndex + 6] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j + 1);
                        m_vertexIndexesCrack[baseIndex + 7] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 8] = (short)(thisVertexCount * thisVertexCountPlus1 + j + 1);
                        baseIndex += 9;
                    }
                }
                else
                {
                    //南半球裂缝处理
                    //顶点处理
                    double sinNorth = Math.Sin(m_north * Degrees2Radians);
                    double radCosNorth = Math.Cos(m_north * Degrees2Radians) * layerRadius;

                    baseIndex = totalVertexCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        double lon = (m_west + (0.5 + j) * scaleFactor * m_longitudeSpan) * Degrees2Radians;
                        m_northWestVertices[baseIndex].X = (float)(radCosNorth * Math.Cos(lon) - m_localOrigin.X);
                        m_northWestVertices[baseIndex].Y = (float)(radCosNorth * Math.Sin(lon) - m_localOrigin.Y);
                        m_northWestVertices[baseIndex].Z = (float)(layerRadius * sinNorth - m_localOrigin.Z);
                        m_northWestVertices[baseIndex].Tu = (float)((j + 0.5) * scaleFactor);
                        m_northWestVertices[baseIndex].Tv = 0.0f;
                        baseIndex += 1;
                    }

                    baseIndex = totalVertexCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        double lon = ((m_west + m_east) * 0.5 + (0.5 + j) * scaleFactor * m_longitudeSpan) * Degrees2Radians;
                        m_northEastVertices[baseIndex].X = (float)(radCosNorth * Math.Cos(lon) - m_localOrigin.X);
                        m_northEastVertices[baseIndex].Y = (float)(radCosNorth * Math.Sin(lon) - m_localOrigin.Y);
                        m_northEastVertices[baseIndex].Z = (float)(layerRadius * sinNorth - m_localOrigin.Z);
                        m_northEastVertices[baseIndex].Tu = (float)((j + thisVertexCount + 0.5) * scaleFactor);
                        m_northEastVertices[baseIndex].Tv = 0.0f;
                        baseIndex += 1;
                    }
                    //索引处理
                    for (int i = 0; i < normalCount; i++)
                    {
                        m_vertexIndexesCrack[9 * thisVertexCount + i] = m_vertexIndexesNormal[6 * thisVertexCount + i];
                    }

                    baseIndex = 0;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        m_vertexIndexesCrack[baseIndex] = (short)j;
                        m_vertexIndexesCrack[baseIndex + 1] = (short)(thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 2] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 3] = (short)(thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 4] = (short)(thisVertexCountPlus1 + j + 1);
                        m_vertexIndexesCrack[baseIndex + 5] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 6] = (short)(j + 1);
                        m_vertexIndexesCrack[baseIndex + 7] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 8] = (short)(thisVertexCountPlus1 + j + 1);
                        baseIndex += 9;
                    }
                }
            }

        }

        private void CreateGPUFlatMesh()
        {
            DateTime start = DateTime.Now;
            double layerRadius = (double)m_layerRadius;
            double scaleFactor = 1.0 / (double)vertexCount;
            int thisVertexCount = vertexCount / 2;
            int thisVertexCountPlus1 = thisVertexCount + 1;
            int totalVertexCount = thisVertexCountPlus1 * thisVertexCountPlus1;

            m_northWestVertices = new CustomVertex.PositionTextured[totalVertexCount];
            m_southWestVertices = new CustomVertex.PositionTextured[totalVertexCount];
            m_northEastVertices = new CustomVertex.PositionTextured[totalVertexCount];
            m_southEastVertices = new CustomVertex.PositionTextured[totalVertexCount];
            if (m_haveCrack)
            {
                if (m_centerLatitude.Degrees > 0)
                {
                    m_southWestVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
                    m_southEastVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
                }
                else
                {
                    m_northWestVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
                    m_northEastVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
                }

            }
            const double Degrees2Radians = System.Math.PI / 180.0;

            double[] sinLon = new double[thisVertexCountPlus1];
            double[] cosLon = new double[thisVertexCountPlus1];
            double[] Lon = new double[thisVertexCountPlus1];

            int baseIndex;
            double angle = m_west * Degrees2Radians;
            double deltaAngle = scaleFactor * m_longitudeSpan * Degrees2Radians;

            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                angle = m_west * Degrees2Radians + i * deltaAngle;
                sinLon[i] = Math.Sin(angle);
                cosLon[i] = Math.Cos(angle);
                Lon[i] = angle;
            }

            baseIndex = 0;
            angle = m_north * Degrees2Radians;
            deltaAngle = -scaleFactor * m_latitudeSpan * Degrees2Radians;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                angle = m_north * Degrees2Radians + i * deltaAngle;
                double sinLat = Math.Sin(angle);
                double radCosLat = Math.Cos(angle) * layerRadius;

                for (int j = 0; j < thisVertexCountPlus1; j++)
                {
                    m_northWestVertices[baseIndex].X = (float)Lon[j];
                    m_northWestVertices[baseIndex].Y = (float)angle;
                    m_northWestVertices[baseIndex].Z = (float)layerRadius;
                    m_northWestVertices[baseIndex].Tu = (float)(j * scaleFactor);
                    m_northWestVertices[baseIndex].Tv = (float)(i * scaleFactor);
                    //m_northWestVertices[baseIndex].Normal = new Vector3(m_northWestVertices[baseIndex].X + (float)m_localOrigin.X, m_northWestVertices[baseIndex].Y + (float)m_localOrigin.Y, m_northWestVertices[baseIndex].Z + (float)m_localOrigin.Z);
                    //m_northWestVertices[baseIndex].Normal.Normalize();

                    baseIndex += 1;
                }
            }

            baseIndex = 0;
            angle = 0.5 * (m_north + m_south) * Degrees2Radians;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                angle = 0.5 * (m_north + m_south) * Degrees2Radians + i * deltaAngle;
                double sinLat = Math.Sin(angle);
                double radCosLat = Math.Cos(angle) * layerRadius;

                for (int j = 0; j < thisVertexCountPlus1; j++)
                {
                    m_southWestVertices[baseIndex].X = (float)Lon[j];
                    m_southWestVertices[baseIndex].Y = (float)angle;
                    m_southWestVertices[baseIndex].Z = (float)layerRadius;
                    m_southWestVertices[baseIndex].Tu = (float)(j * scaleFactor);
                    m_southWestVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);
                    //m_southWestVertices[baseIndex].Normal = new Vector3(m_southWestVertices[baseIndex].X + (float)m_localOrigin.X, m_southWestVertices[baseIndex].Y + (float)m_localOrigin.Y, m_southWestVertices[baseIndex].Z + (float)m_localOrigin.Z);
                    //m_southWestVertices[baseIndex].Normal.Normalize();
                    baseIndex += 1;
                }
            }

            angle = 0.5 * (m_west + m_east) * Degrees2Radians;
            deltaAngle = scaleFactor * m_longitudeSpan * Degrees2Radians;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                angle = 0.5 * (m_west + m_east) * Degrees2Radians + i * deltaAngle;
                sinLon[i] = Math.Sin(angle);
                cosLon[i] = Math.Cos(angle);
                Lon[i] = angle;
            }

            baseIndex = 0;
            angle = m_north * Degrees2Radians;
            deltaAngle = -scaleFactor * m_latitudeSpan * Degrees2Radians;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                angle = m_north * Degrees2Radians + i * deltaAngle;
                double sinLat = Math.Sin(angle);
                double radCosLat = Math.Cos(angle) * layerRadius;

                for (int j = 0; j < thisVertexCountPlus1; j++)
                {
                    m_northEastVertices[baseIndex].X = (float)Lon[j];
                    m_northEastVertices[baseIndex].Y = (float)angle;
                    m_northEastVertices[baseIndex].Z = (float)layerRadius;
                    m_northEastVertices[baseIndex].Tu = (float)((j + thisVertexCount) * scaleFactor);
                    m_northEastVertices[baseIndex].Tv = (float)(i * scaleFactor);
                    //m_northEastVertices[baseIndex].Normal = new Vector3(m_northEastVertices[baseIndex].X + (float)m_localOrigin.X, m_northEastVertices[baseIndex].Y + (float)m_localOrigin.Y, m_northEastVertices[baseIndex].Z + (float)m_localOrigin.Z);
                    //m_northEastVertices[baseIndex].Normal.Normalize();

                    baseIndex += 1;
                }
            }

            baseIndex = 0;
            angle = 0.5f * (m_north + m_south) * Degrees2Radians;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                angle = 0.5 * (m_north + m_south) * Degrees2Radians + i * deltaAngle;
                double sinLat = Math.Sin(angle);
                double radCosLat = Math.Cos(angle) * layerRadius;

                for (int j = 0; j < thisVertexCountPlus1; j++)
                {
                    m_southEastVertices[baseIndex].X = (float)Lon[j];
                    m_southEastVertices[baseIndex].Y = (float)angle;
                    m_southEastVertices[baseIndex].Z = (float)layerRadius;
                    m_southEastVertices[baseIndex].Tu = (float)((j + thisVertexCount) * scaleFactor);
                    m_southEastVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);
                    //m_southEastVertices[baseIndex].Normal = new Vector3(m_southEastVertices[baseIndex].X + (float)m_localOrigin.X, m_southEastVertices[baseIndex].Y + (float)m_localOrigin.Y, m_southEastVertices[baseIndex].Z + (float)m_localOrigin.Z);
                    //m_southEastVertices[baseIndex].Normal.Normalize();

                    baseIndex += 1;
                }

            }
            m_vertexIndexesNormal = new short[2 * thisVertexCount * thisVertexCount * 3];

            for (int i = 0; i < thisVertexCount; i++)
            {
                baseIndex = (2 * 3 * i * thisVertexCount);

                for (int j = 0; j < thisVertexCount; j++)
                {
                    m_vertexIndexesNormal[baseIndex] = (short)(i * thisVertexCountPlus1 + j);
                    m_vertexIndexesNormal[baseIndex + 1] = (short)((i + 1) * thisVertexCountPlus1 + j);
                    m_vertexIndexesNormal[baseIndex + 2] = (short)(i * thisVertexCountPlus1 + j + 1);

                    m_vertexIndexesNormal[baseIndex + 3] = (short)(i * thisVertexCountPlus1 + j + 1);
                    m_vertexIndexesNormal[baseIndex + 4] = (short)((i + 1) * thisVertexCountPlus1 + j);
                    m_vertexIndexesNormal[baseIndex + 5] = (short)((i + 1) * thisVertexCountPlus1 + j + 1);

                    baseIndex += 6;
                }
            }
            if (m_haveCrack)
            {
                m_vertexIndexesCrack = new short[(2 * thisVertexCount + 1) * thisVertexCount * 3];
                int normalCount = 2 * (thisVertexCount - 1) * thisVertexCount * 3;
                if (m_centerLatitude.Degrees > 0)
                {
                    //北半同层球裂缝处理
                    //顶点处理
                    double sinSouth = Math.Sin(m_south * Degrees2Radians);
                    double radCosSouth = Math.Cos(m_south * Degrees2Radians) * layerRadius;
                    double southInRadians = m_south * Degrees2Radians;
                    baseIndex = totalVertexCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        double lon = (m_west + (0.5 + j) * scaleFactor * m_longitudeSpan) * Degrees2Radians;
                        m_southWestVertices[baseIndex].X = (float)lon;
                        m_southWestVertices[baseIndex].Y = (float)southInRadians;
                        m_southWestVertices[baseIndex].Z = (float)layerRadius;
                        m_southWestVertices[baseIndex].Tu = (float)((j + 0.5) * scaleFactor);
                        m_southWestVertices[baseIndex].Tv = 1.0f;
                        baseIndex += 1;
                    }

                    baseIndex = totalVertexCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        double lon = ((m_west + m_east) * 0.5 + (0.5 + j) * scaleFactor * m_longitudeSpan) * Degrees2Radians;
                        m_southEastVertices[baseIndex].X = (float)lon;
                        m_southEastVertices[baseIndex].Y = (float)southInRadians;
                        m_southEastVertices[baseIndex].Z = (float)layerRadius;
                        m_southEastVertices[baseIndex].Tu = (float)((j + thisVertexCount + 0.5) * scaleFactor);
                        m_southEastVertices[baseIndex].Tv = 1.0f;
                        baseIndex += 1;
                    }
                    //索引处理
                    for (int i = 0; i < normalCount; i++)
                    {
                        m_vertexIndexesCrack[i] = m_vertexIndexesNormal[i];
                    }
                    baseIndex = normalCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        m_vertexIndexesCrack[baseIndex] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 1] = (short)(thisVertexCount * thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 2] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 3] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 4] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 5] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j + 1);

                        m_vertexIndexesCrack[baseIndex + 6] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j + 1);
                        m_vertexIndexesCrack[baseIndex + 7] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 8] = (short)(thisVertexCount * thisVertexCountPlus1 + j + 1);
                        baseIndex += 9;
                    }
                }
                else
                {
                    //南半球裂缝处理
                    //顶点处理
                    double sinNorth = Math.Sin(m_north * Degrees2Radians);
                    double radCosNorth = Math.Cos(m_north * Degrees2Radians) * layerRadius;
                    double northInRadians = m_north * Degrees2Radians;
                    baseIndex = totalVertexCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        double lon = (m_west + (0.5 + j) * scaleFactor * m_longitudeSpan) * Degrees2Radians;
                        m_northWestVertices[baseIndex].X = (float)lon;
                        m_northWestVertices[baseIndex].Y = (float)northInRadians;
                        m_northWestVertices[baseIndex].Z = (float)layerRadius;
                        m_northWestVertices[baseIndex].Tu = (float)((j + 0.5) * scaleFactor);
                        m_northWestVertices[baseIndex].Tv = 0.0f;
                        baseIndex += 1;
                    }

                    baseIndex = totalVertexCount;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        double lon = ((m_west + m_east) * 0.5 + (0.5 + j) * scaleFactor * m_longitudeSpan) * Degrees2Radians;
                        m_northEastVertices[baseIndex].X = (float)lon;
                        m_northEastVertices[baseIndex].Y = (float)northInRadians;
                        m_northEastVertices[baseIndex].Z = (float)layerRadius;
                        m_northEastVertices[baseIndex].Tu = (float)((j + thisVertexCount + 0.5) * scaleFactor);
                        m_northEastVertices[baseIndex].Tv = 0.0f;
                        baseIndex += 1;
                    }
                    //索引处理
                    for (int i = 0; i < normalCount; i++)
                    {
                        m_vertexIndexesCrack[9 * thisVertexCount + i] = m_vertexIndexesNormal[6 * thisVertexCount + i];
                    }

                    baseIndex = 0;
                    for (int j = 0; j < thisVertexCount; j++)
                    {
                        m_vertexIndexesCrack[baseIndex] = (short)j;
                        m_vertexIndexesCrack[baseIndex + 1] = (short)(thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 2] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 3] = (short)(thisVertexCountPlus1 + j);
                        m_vertexIndexesCrack[baseIndex + 4] = (short)(thisVertexCountPlus1 + j + 1);
                        m_vertexIndexesCrack[baseIndex + 5] = (short)(totalVertexCount + j);

                        m_vertexIndexesCrack[baseIndex + 6] = (short)(j + 1);
                        m_vertexIndexesCrack[baseIndex + 7] = (short)(totalVertexCount + j);
                        m_vertexIndexesCrack[baseIndex + 8] = (short)(thisVertexCountPlus1 + j + 1);
                        baseIndex += 9;
                    }
                }
            }
            TimeSpan time = DateTime.Now - start;
            Console.WriteLine(time.Milliseconds);

        }        

        /// <summary>
        /// 创建没有细分的DQG网格
        /// </summary>
        private void CreateOriFlatMesh()
        {
            m_northWestVertices = new CustomVertex.PositionTextured[4];
            m_southWestVertices = new CustomVertex.PositionTextured[4];
            m_northEastVertices = new CustomVertex.PositionTextured[4];
            m_southEastVertices = new CustomVertex.PositionTextured[4];
            m_vertexIndexesNormal = new short[] { 0, 1, 2, 1, 3, 2 };

            m_northWestVertices[0].Position = MathEngine.SphericalToCartesian(m_north, m_west, m_layerRadius) - m_localOrigin.Vector3;
            m_northWestVertices[0].Tu = 0.0f;
            m_northWestVertices[0].Tv = 0.0f;
            m_northWestVertices[1].Position = MathEngine.SphericalToCartesian(m_north, m_centerLongitude.Degrees, m_layerRadius) - m_localOrigin.Vector3;
            m_northWestVertices[1].Tu = 0.5f;
            m_northWestVertices[1].Tv = 0.0f;
            m_northWestVertices[2].Position = MathEngine.SphericalToCartesian(m_centerLatitude.Degrees, m_west, m_layerRadius) - m_localOrigin.Vector3;
            m_northWestVertices[2].Tu = 0.0f;
            m_northWestVertices[2].Tv = 0.5f;
            m_northWestVertices[3].Position = MathEngine.SphericalToCartesian(m_centerLatitude, m_centerLongitude, m_layerRadius) - m_localOrigin.Vector3;
            m_northWestVertices[3].Tu = 0.5f;
            m_northWestVertices[3].Tv = 0.5f;


            m_northEastVertices[0] = m_northWestVertices[1];
            m_northEastVertices[1].Position = MathEngine.SphericalToCartesian(m_north, m_east, m_layerRadius) - m_localOrigin.Vector3;
            m_northEastVertices[1].Tu = 1.0f;
            m_northEastVertices[1].Tv = 0.0f;
            m_northEastVertices[2] = m_northWestVertices[3];
            m_northEastVertices[3].Position = MathEngine.SphericalToCartesian(m_centerLatitude.Degrees, m_east, m_layerRadius) - m_localOrigin.Vector3;
            m_northEastVertices[3].Tu = 1.0f;
            m_northEastVertices[3].Tv = 0.5f;

            m_southWestVertices[0] = m_northWestVertices[2];
            m_southWestVertices[1] = m_northWestVertices[3];
            m_southWestVertices[2].Position = MathEngine.SphericalToCartesian(m_south, m_west, m_layerRadius) - m_localOrigin.Vector3;
            m_southWestVertices[2].Tu = 0.0f;
            m_southWestVertices[2].Tv = 1.0f;
            m_southWestVertices[3].Position = MathEngine.SphericalToCartesian(m_south, m_centerLongitude.Degrees, m_layerRadius) - m_localOrigin.Vector3;
            m_southWestVertices[3].Tu = 0.5f;
            m_southWestVertices[3].Tv = 1.0f;

            m_southEastVertices[0] = m_northWestVertices[3];
            m_southEastVertices[1] = m_northEastVertices[3];
            m_southEastVertices[2] = m_southWestVertices[3];
            m_southEastVertices[3].Position = MathEngine.SphericalToCartesian(m_south, m_east, m_layerRadius) - m_localOrigin.Vector3;
            m_southEastVertices[3].Tu = 1.0f;
            m_southEastVertices[3].Tv = 1.0f;
        }
        private void Render(Device device, CustomVertex.PositionTextured[] verts, short[] indexes)
        {
            device.RenderState.ZBufferEnable = true;
            //device.RenderState.CullMode = Cull.CounterClockwise;
            if (this.m_effect != null)
            {
                #region GPU渲染
                m_effect.Technique = m_effect.GetTechnique(0);
                m_effect.SetValue("WorldViewProj", Matrix.Multiply(device.Transform.World, Matrix.Multiply(device.Transform.View, device.Transform.Projection)));
                //m_effect.SetValue("localCenterX", (float)m_localOrigin.X);
                //m_effect.SetValue("localCenterY", (float)m_localOrigin.Y);
                //m_effect.SetValue("localCenterZ", (float)m_localOrigin.Z);
                try
                {
                    m_effect.SetValue("Tex1", m_texture);
                }
                catch (System.Exception ex)
                {

                }
                device.VertexFormat = CustomVertex.PositionTextured.Format;
                int numPasses = m_effect.Begin(0);
                for (int i = 0; i < numPasses; i++)
                {
                    m_effect.BeginPass(i);
                    device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0,
                        verts.Length, indexes.Length / 3, indexes, true, verts);
                    m_effect.EndPass();
                }
                m_effect.End();
            #endregion
            }
            else
            {

                try
                {
                    bool oriZBuffer = device.RenderState.ZBufferEnable;
                    device.SetTexture(0, m_texture);
                    device.RenderState.Lighting = false;
                    device.VertexFormat = CustomVertex.PositionTextured.Format;
                    device.TextureState[0].ColorOperation = TextureOperation.SelectArg1;
                    device.TextureState[0].ColorArgument1 = TextureArgument.TextureColor;
                    device.TextureState[0].ColorArgument2 = TextureArgument.Diffuse;
                    device.RenderState.CullMode = Cull.Clockwise;
                    device.RenderState.ZBufferEnable = true;
                    device.SamplerState[0].MagFilter = TextureFilter.Linear;
                    device.SamplerState[0].MinFilter = TextureFilter.Linear;
                    device.SamplerState[0].MipFilter = TextureFilter.Linear;
                    device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0,
                        verts.Length, indexes.Length / 3, indexes, true, verts);//
                    device.RenderState.ZBufferEnable = oriZBuffer;
                }
                catch
                {

                }
                finally
                {
                    if (DrawArgs.ImageLevel < this.m_level)
                        DrawArgs.ImageLevel = this.m_level;
                }
            }
            
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 释放内存方法
        /// </summary>
        public void Dispose()
        {
            try
            {
                m_isInitialized = false;

                if (m_texture != null && !m_texture.Disposed)
                {
                    m_texture.Dispose();
                    m_texture = null;
                }

                if (m_northWestChild != null)
                {
                    m_northWestChild.Dispose();
                    m_northWestChild = null;
                }
                if (m_southWestChild != null)
                {
                    m_southWestChild.Dispose();
                    m_southWestChild = null;
                }
                if (m_northEastChild != null)
                {
                    m_northEastChild.Dispose();
                    m_northEastChild = null;
                }
                if (m_southEastChild != null)
                {
                    m_southEastChild.Dispose();
                    m_southEastChild = null;
                }
                if(m_southEastVertices!=null)
                {
                    this.m_southEastVertices = null;
                }
                if(m_southWestVertices!=null)
                {
                    m_southWestVertices = null;
                }
                if(m_northEastVertices!=null)
                {
                    m_northEastVertices = null;
                }
                if(m_northWestVertices!=null)
                {
                    m_northEastVertices = null;
                }
                if (m_effect != null)
                {
                    m_effect.Dispose();
                    m_effect = null;
                }
                if (DrawArgs.ImageLevel >= this.m_level)
                {
                    DrawArgs.ImageLevel = this.m_level-1;
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 初始化方法
        /// </summary>
        /// <param name="drawArgs"></param>
        public void Initialize(DrawArgs drawArgs)
        {
            try
            {

                if (File.Exists(m_texturePath))
                {
                    m_texture = TextureLoader.FromFile(drawArgs.device, m_texturePath);
                }
                else
                    m_texture = null;

                if (File.Exists(m_effectPath))
                {
                    string errs;
                    m_effect = Effect.FromFile(drawArgs.device, m_effectPath, null, ShaderFlags.None, null);

                }
                else
                    m_effect = null;

                CreateTileMesh();
            }
            catch (Exception ex)
            {

            }
            finally
            {
                m_isInitialized = true;
            }
            
        }

        /// <summary>
        /// 更新方法
        /// </summary>
        public void Update(DrawArgs drawArgs)
        {
            try
            {
                double tileSize = m_north - m_south;

                if (!m_isInitialized)
                {
                    if (DrawArgs.Camera.ViewRange * 0.5f < Angle.FromDegrees(DQGTileSet.TileDrawDistance * tileSize)
    && MathEngine.SphericalDistance(m_centerLatitude, m_centerLongitude,
                            DrawArgs.Camera.Latitude, DrawArgs.Camera.Longitude) < Angle.FromDegrees(DQGTileSet.TileDrawSpread * tileSize * 1.25f)
    && DrawArgs.Camera.ViewFrustum.Intersects(BoundingBox)
                        )
                        Initialize(drawArgs);//如果没有初始化并且不满足进入下一层的条件，则初始化当前瓦片
                }
                //如果渲染状态改变，则重新构建瓦片
                if (m_isInitialized && World.Settings.VerticalExaggeration != m_verticalExaggeration)
                {
                    CreateTileMesh();
                }

                if (m_isInitialized)
                {
                    if (DrawArgs.Camera.ViewRange < Angle.FromDegrees(DQGTileSet.TileDrawDistance * tileSize)
    && MathEngine.SphericalDistance(m_centerLatitude, m_centerLongitude,
                            DrawArgs.Camera.Latitude, DrawArgs.Camera.Longitude) < Angle.FromDegrees(DQGTileSet.TileDrawSpread * tileSize)
    && DrawArgs.Camera.ViewFrustum.Intersects(BoundingBox)
                        )//满足进入下一层的条件，则构建下一层瓦片
                    {
                        if (m_northEastChild == null || m_northWestChild == null || m_southEastChild == null || m_southWestChild == null)
                        {
                            ComputeChildren(drawArgs);
                        }

                        if (m_northEastChild != null)
                        {
                            m_northEastChild.Update(drawArgs);
                        }

                        if (m_northWestChild != null)
                        {
                            m_northWestChild.Update(drawArgs);
                        }

                        if (m_southEastChild != null)
                        {
                            m_southEastChild.Update(drawArgs);
                        }

                        if (m_southWestChild != null)
                        {
                            m_southWestChild.Update(drawArgs);
                        }
                    }
                    else//不满足进入下一层的条件，则将现有的下一层瓦片释放掉
                    {
                        if (m_northWestChild != null)
                        {
                            m_northWestChild.Dispose();
                            m_northWestChild = null;
                        }

                        if (m_northEastChild != null)
                        {
                            m_northEastChild.Dispose();
                            m_northEastChild = null;
                        }

                        if (m_southEastChild != null)
                        {
                            m_southEastChild.Dispose();
                            m_southEastChild = null;
                        }

                        if (m_southWestChild != null)
                        {
                            m_southWestChild.Dispose();
                            m_southWestChild = null;
                        }
                    }
                }

                if (m_isInitialized)
                {
                    if (DrawArgs.Camera.ViewRange / 2 > Angle.FromDegrees(DQGTileSet.TileDrawDistance * tileSize * 1.5f)
                            || MathEngine.SphericalDistance(m_centerLatitude, m_centerLongitude, DrawArgs.Camera.Latitude, DrawArgs.Camera.Longitude) > Angle.FromDegrees(DQGTileSet.TileDrawSpread * tileSize * 1.5f))
                    {
                        this.Dispose();
                    }
                }
            }
            catch
            {
            }
        }
        
        /// <summary>
        /// 渲染方法
        /// </summary>
        /// <param name="drawArgs"></param>
        /// <returns></returns>
        public bool Render(DrawArgs drawArgs)
        {
            Matrix oriWorld = drawArgs.device.Transform.World;
            VertexFormats oriFormats = drawArgs.device.VertexFormat;
            bool oriLighting = drawArgs.device.RenderState.Lighting;
            FillMode oriFillMode = drawArgs.device.RenderState.FillMode;
            Cull oriCullMode = drawArgs.device.RenderState.CullMode;
            TextureAddress oriTextureAdressU = drawArgs.device.SamplerState[0].AddressU;
            TextureAddress oriTextureAdressV = drawArgs.device.SamplerState[0].AddressV;
            TextureOperation oriColorOperation = drawArgs.device.TextureState[0].ColorOperation;
            TextureFilter oriMipFilter = drawArgs.device.SamplerState[0].MipFilter;
            TextureFilter oriMagFilter = drawArgs.device.SamplerState[0].MagFilter;
            TextureFilter oriMinFilter = drawArgs.device.SamplerState[0].MinFilter;
            bool oriAlphaBlendEnable = drawArgs.device.RenderState.AlphaBlendEnable;
            bool oriNormalizeNormals = drawArgs.device.RenderState.NormalizeNormals;
            Material oriMaterial = drawArgs.device.Material;
            int oriTextureFactor = drawArgs.device.RenderState.TextureFactor;
            int oriAmbientColor = drawArgs.device.RenderState.AmbientColor;
            Matrix oriWorldMatrix = drawArgs.device.Transform.World;
            TextureOperation oriTextureOperation = drawArgs.device.TextureState[0].ColorOperation;
            TextureArgument oriColorArgument1 = drawArgs.device.TextureState[0].ColorArgument1;
            TextureArgument oriColorArgument2 = drawArgs.device.TextureState[0].ColorArgument2;
            TextureOperation oriAlphaOperation = drawArgs.device.TextureState[0].AlphaOperation;
            TextureArgument oriAlphaArgument1 = drawArgs.device.TextureState[0].AlphaArgument1;
            TextureArgument oriAlphaArgument2 = drawArgs.device.TextureState[0].AlphaArgument2;
            ColorSource oriDiffuseMatSource = drawArgs.device.RenderState.DiffuseMaterialSource;
            try
            {
                if (!m_isInitialized ||
                    this.m_northWestVertices == null ||
                    this.m_northEastVertices == null ||
                    this.m_southEastVertices == null ||
                    this.m_southWestVertices == null)
                    return false;

                if (!DrawArgs.Camera.ViewFrustum.Intersects(BoundingBox))

                    return false;

                bool northWestChildRendered = false;
                bool northEastChildRendered = false;
                bool southWestChildRendered = false;
                bool southEastChildRendered = false;

                if (m_northWestChild != null)
                    if (m_northWestChild.Render(drawArgs))
                        northWestChildRendered = true;

                if (m_southWestChild != null)
                    if (m_southWestChild.Render(drawArgs))
                        southWestChildRendered = true;

                if (m_northEastChild != null)
                    if (m_northEastChild.Render(drawArgs))
                        northEastChildRendered = true;

                if (m_southEastChild != null)
                    if (m_southEastChild.Render(drawArgs))
                        southEastChildRendered = true;

                if (northWestChildRendered && northEastChildRendered && southWestChildRendered && southEastChildRendered)
                {
                    return true;
                }
                Device device = drawArgs.device;
                if (m_texture == null || m_texture.Disposed)
                    return false;
                //device.SetTexture(0, m_texture);
                device.Transform.World = Matrix.Translation((m_localOrigin-drawArgs.WorldCamera.ReferenceCenter).Vector3);
                    

                if (!northWestChildRendered)
                {
                    if (m_haveCrack && m_centerLatitude.Degrees < 0)
                        Render(device, m_northWestVertices, m_vertexIndexesCrack);
                    else
                        Render(device, m_northWestVertices, m_vertexIndexesNormal);
                }
                if (!southWestChildRendered)
                {
                    if (m_haveCrack && m_centerLatitude.Degrees > 0)
                        Render(device, m_southWestVertices, m_vertexIndexesCrack);
                    else
                        Render(device, m_southWestVertices, m_vertexIndexesNormal);
                }
                if (!northEastChildRendered)
                {
                    if (m_haveCrack && m_centerLatitude.Degrees < 0)
                        Render(device, m_northEastVertices, m_vertexIndexesCrack);
                    else
                        Render(device, m_northEastVertices, m_vertexIndexesNormal);
                }
                if (!southEastChildRendered)
                {
                    if (m_haveCrack && m_centerLatitude.Degrees > 0)
                        Render(device, m_southEastVertices, m_vertexIndexesCrack);
                    else
                        Render(device, m_southEastVertices, m_vertexIndexesNormal);
                }
                return true;
            }
            catch (DirectXException)
            {
                return false;
            }
            finally 
            {
                //DrawArgs.Device.Transform.World=oriWorld;
                drawArgs.device.Transform.World = oriWorld;
                drawArgs.device.VertexFormat = oriFormats;
                drawArgs.device.RenderState.Lighting = oriLighting;
                drawArgs.device.RenderState.CullMode = oriCullMode;
                drawArgs.device.TextureState[0].ColorOperation = oriColorOperation;
                drawArgs.device.SamplerState[0].AddressU = oriTextureAdressU;
                drawArgs.device.SamplerState[0].AddressV = oriTextureAdressV;

                drawArgs.device.RenderState.FillMode = oriFillMode;
                //drawArgs.device.RenderState.FillMode = FillMode.WireFrame;

                drawArgs.device.SamplerState[0].MipFilter = oriMipFilter;
                drawArgs.device.SamplerState[0].MagFilter = oriMagFilter;
                drawArgs.device.SamplerState[0].MinFilter = oriMinFilter;
                drawArgs.device.RenderState.Lighting = oriLighting;
                drawArgs.device.RenderState.AlphaBlendEnable = oriAlphaBlendEnable;
                drawArgs.device.RenderState.NormalizeNormals = oriNormalizeNormals;
                drawArgs.device.Material = oriMaterial;
                drawArgs.device.RenderState.TextureFactor = oriTextureFactor;
                drawArgs.device.RenderState.AmbientColor = oriAmbientColor;
                drawArgs.device.TextureState[0].ColorOperation = oriTextureOperation;
                drawArgs.device.TextureState[0].ColorArgument1 = oriColorArgument1;
                drawArgs.device.TextureState[0].ColorArgument2 = oriColorArgument2;
                drawArgs.device.TextureState[0].AlphaOperation = oriAlphaOperation;
                drawArgs.device.TextureState[0].AlphaArgument1 = oriAlphaArgument1;
                drawArgs.device.TextureState[0].AlphaArgument2 = oriAlphaArgument2;
                drawArgs.device.Transform.World = oriWorldMatrix;
                drawArgs.device.RenderState.CullMode = oriCullMode;
                drawArgs.device.RenderState.DiffuseMaterialSource = oriDiffuseMatSource;
            }
            
        }
        #endregion
    }
}

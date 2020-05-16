using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MME.Globe.Core.MathLib;
using Microsoft.DirectX.Direct3D;
using Microsoft.DirectX;
using System.Drawing;
using MME.Globe.Core.DEM;
using System.IO;
using MME.Globe.Core.Renderable;
using System.Windows.Forms;

namespace MME.Globe.Core.DQG
{
    /// <summary>
    /// DQG三角形瓦片类
    /// </summary>
    public class DQGTriangleTile : IDisposable
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
        private static int m_vertexCount = (int)Math.Pow(2, DQGTileSet.BaseLevel);
        private static double m_layerRadius = 6378137.0;
        private DQGTriangleTile m_topChild;
        private DQGQuadTile m_leftChild;
        private DQGQuadTile m_rightChild;
        private CustomVertex.PositionTextured[] m_topVertices;
        private CustomVertex.PositionTextured[] m_leftVertices;
        private CustomVertex.PositionTextured[] m_rightVertices;
        private short[] m_vertexIndexesNormal;
        private short[] m_vertexIndexesQ;
        private short[] m_vertexIndexesT;
        private Vector3d m_localOrigin;
        private string m_texturePath;
        private float m_verticalExaggeration;//垂直夸张系数
        private float m_minElevation = 0;
        private float m_maxElevation = 0;
        private Effect m_effect = null;
        private string m_effectPath;

        #endregion

        #region 公有字段

        /// <summary>
        /// 瓦片的地址码
        /// </summary>
        public string Address;
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
        /// <param name="level">层次</param>
        /// <param name="south">北</param>
        /// <param name="north">南</param>
        /// <param name="west">西</param>
        /// <param name="east">东</param>
        public DQGTriangleTile(string address, int level, double south, double north, double west, double east)
        {
            this.Address = address;
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
            m_maxElevation = TerrainProvider.GetElevationAt(m_centerLatitude.Degrees, m_centerLongitude.Degrees, this.m_level);
            m_minElevation = m_maxElevation;
            m_texturePath = World.Settings.ImagePath +@"\"+ m_level.ToString() + @"\" + this.Address[0] + @"\" + this.Address + ".jpg";
            this.m_effectPath = Application.StartupPath + @"\Data\Shaders\grayscale.fx1";
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

                if (m_topChild != null)
                {
                    m_topChild.Dispose();
                    m_topChild = null;
                }
                if (m_leftChild != null)
                {
                    m_leftChild.Dispose();
                    m_leftChild = null;
                }
                if (m_rightChild != null)
                {
                    m_rightChild.Dispose();
                    m_rightChild = null;
                }
                if(m_topVertices!=null)
                {
                    m_topVertices = null;
                }
                if(m_leftVertices!=null)
                {
                    m_leftVertices = null;
                }
                if(m_rightVertices!=null)
                {
                    m_rightVertices = null;
                }
                if (m_effect != null)
                {
                    m_effect.Dispose();
                    m_effect = null;
                }
                if (DrawArgs.ImageLevel > this.m_level)
                {
                    DrawArgs.ImageLevel = this.m_level - 1;
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
                    m_effect = Effect.FromFile(drawArgs.device, m_effectPath, null, ShaderFlags.None, null);
                }
                else
                    m_effect = null;

                CreateTileMesh();
            }
            catch (Exception)
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
        /// <param name="drawArgs"></param>
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
    && DrawArgs.Camera.ViewFrustum.Intersects(BoundingBox))
                        Initialize(drawArgs);
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
                        if (m_topChild == null || m_leftChild == null || m_rightChild == null)
                        { 
                            ComputeChildren(drawArgs);
                        }

                        if (m_topChild != null)
                        {
                            m_topChild.Update(drawArgs);
                        }

                        if (m_leftChild != null)
                        {
                            m_leftChild.Update(drawArgs);
                        }

                        if (m_rightChild != null)
                        {
                            m_rightChild.Update(drawArgs);
                        }
                    }
                    else//不满足进入下一层的条件，则将现有的下一层瓦片释放掉
                    {
                        if (m_topChild != null)
                        {
                            m_topChild.Dispose();
                            m_topChild = null;
                        }

                        if (m_leftChild != null)
                        {
                            m_leftChild.Dispose();
                            m_leftChild = null;
                        }

                        if (m_rightChild != null)
                        {
                            m_rightChild.Dispose();
                            m_rightChild = null;
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
            Matrix oriWorldMatrix = drawArgs.device.Transform.World;
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
                    this.m_topVertices == null ||
                    this.m_rightVertices == null ||
                    this.m_leftVertices == null
                    )
                    return false;

                if (!DrawArgs.Camera.ViewFrustum.Intersects(BoundingBox))
                    return false;

                bool TopChildRendered = false;
                bool RightChildRendered = false;
                bool LeftChildRendered = false;

                if (m_topChild != null)
                    if (m_topChild.Render(drawArgs))
                        TopChildRendered = true;

                if (m_leftChild != null)
                    if (m_leftChild.Render(drawArgs))
                        LeftChildRendered = true;

                if (m_rightChild != null)
                    if (m_rightChild.Render(drawArgs))
                        RightChildRendered = true;

                if (TopChildRendered && RightChildRendered && LeftChildRendered)
                {
                    return true;
                }


                Device device = drawArgs.device;

                //device.Lights[0].Type = LightType.Directional;
                //device.Lights[0].Diffuse = Color.White; 
                //device.Lights[0].Direction = m_location-drawArgs.WorldCamera.Position;                 
                //device.Lights[0].Enabled = true;


                //Material mtrl = new Material();
                //mtrl.Diffuse = Color.White;
                //mtrl.Ambient = Color.White;
                //device.Material = mtrl;

                if (m_texture == null || m_texture.Disposed)
                    return false;
                device.SetTexture(0, m_texture);
                device.Transform.World = Matrix.Translation((m_localOrigin - drawArgs.WorldCamera.ReferenceCenter).Vector3);
                if (!TopChildRendered)
                    Render(device, m_topVertices, m_vertexIndexesT);
                if (!LeftChildRendered)
                    Render(device, m_leftVertices, m_vertexIndexesQ);
                if (!RightChildRendered)
                    Render(device, m_rightVertices, m_vertexIndexesQ);                
                return true;
            }
            catch (DirectXException)
            {
                return false;
            }
            finally
            {
                //DrawArgs.Device.Transform.World = oriWorldMatrix;
                //drawArgs.device.Transform.World = oriWorldMatrix;
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

        #region 私有方法
        private void CreateTileMesh()
        {
            m_verticalExaggeration = World.Settings.VerticalExaggeration;
            if (Math.Abs(m_verticalExaggeration) > 1e-3)
                CreateElevatedEdgedMesh();
            else
                CreateFlatMesh();
            //CalculateNormals(ref m_topVertices, m_vertexIndexesT);
            //CalculateNormals(ref m_leftVertices, m_vertexIndexesQ);
            //CalculateNormals(ref m_rightVertices,m_vertexIndexesQ);
        }
        /// <summary>
        /// 创建平面瓦片
        /// </summary>
        private void CreateFlatMesh()
        {
            int thisVertexCount = m_vertexCount / 2;//8
            int thisVertexCountPlus1 = thisVertexCount + 1;//9
            int totalVertexCount = thisVertexCountPlus1 * thisVertexCountPlus1;//81
            const double Degrees2Radians = System.Math.PI / 180.0;
            double scaleFactor = 1.0 / (double)m_vertexCount;
            int baseIndex = 0;

            #region m_topVertices

            #region 顶点数组
            int[] QuadCountPerRow = new int[thisVertexCount];//每行四边形数(退化三角形看做一个四边形)
            int[] PointCountPerRow = new int[thisVertexCountPlus1];//每行点数
            int TriQuadCount = 1;//四边形总数
            for (int i = 0; i < DQGTileSet.BaseLevel - 1; i++)
            {
                for (int j = (int)Math.Pow(2, i) + 1; j <= (int)Math.Pow(2, i + 1); j++)
                {
                    QuadCountPerRow[j - 1] = (int)Math.Pow(2, i + 1);
                    TriQuadCount += QuadCountPerRow[j - 1];
                }
            }
            QuadCountPerRow[0] = 1;
            PointCountPerRow[0] = 1;
            int TriPointCount = 1;//总点数
            for (int i = 1; i < thisVertexCount; i++)
            {
                PointCountPerRow[i] = QuadCountPerRow[i] + 1;
                TriPointCount += PointCountPerRow[i];
            }
            PointCountPerRow[thisVertexCount] = PointCountPerRow[thisVertexCount - 1] * 2 - 1;
            TriPointCount += PointCountPerRow[thisVertexCount];

            m_topVertices = new CustomVertex.PositionTextured[TriPointCount];
            if (m_centerLatitude.Degrees > 0)
            {
                m_topVertices[0].Position=new Vector3(0.0f, 0.0f, (float)m_layerRadius) - m_localOrigin.Vector3;
                m_topVertices[0].Tu=0.5f;
                m_topVertices[0].Tv=0.0f;
            }
            else
            {
                m_topVertices[0].Position = new Vector3(0.0f, 0.0f, -(float)m_layerRadius) - m_localOrigin.Vector3;
                m_topVertices[0].Tu=0.5f;
                m_topVertices[0].Tv=1.0f;
            }

            baseIndex = 1;
            for (int i = 1; i < thisVertexCountPlus1; i++)
            {
                double lonFactor = 1.0 / (double)(PointCountPerRow[i] - 1);
                double lat, sinLat, radCosLat;
                if (m_centerLatitude.Degrees > 0)//计算顶点的世界坐标及纹理坐标
                {
                    lat = (m_north - scaleFactor * m_latitudeSpan * i) * Degrees2Radians;
                    sinLat = Math.Sin(lat);
                    radCosLat = Math.Cos(lat) * m_layerRadius;
                    for (int j = 0; j < PointCountPerRow[i]; j++)
                    {
                        double lon = (m_west + lonFactor * j * m_longitudeSpan) * Degrees2Radians;
                        m_topVertices[baseIndex].X = (float)(radCosLat * Math.Cos(lon) - m_localOrigin.X);
                        m_topVertices[baseIndex].Y = (float)(radCosLat * Math.Sin(lon) - m_localOrigin.Y);
                        m_topVertices[baseIndex].Z = (float)(m_layerRadius * sinLat - m_localOrigin.Z);
                        m_topVertices[baseIndex].Tu = (float)(j * lonFactor);
                        m_topVertices[baseIndex].Tv = (float)(i * scaleFactor);
                        baseIndex += 1;
                    }
                }
                else
                {
                    lat = (m_south + scaleFactor * m_latitudeSpan * i) * Degrees2Radians;
                    sinLat = Math.Sin(lat);
                    radCosLat = Math.Cos(lat) * m_layerRadius;
                    for (int j = 0; j < PointCountPerRow[i]; j++)
                    {
                        double lon = (m_east - lonFactor * j * m_longitudeSpan) * Degrees2Radians;
                        m_topVertices[baseIndex].X = (float)(radCosLat * Math.Cos(lon) - m_localOrigin.X);
                        m_topVertices[baseIndex].Y = (float)(radCosLat * Math.Sin(lon) - m_localOrigin.Y);
                        m_topVertices[baseIndex].Z = (float)(m_layerRadius * sinLat - m_localOrigin.Z);
                        m_topVertices[baseIndex].Tu = 1.0f - (float)(j * lonFactor);
                        m_topVertices[baseIndex].Tv = 1.0f - (float)(i * scaleFactor);
                        baseIndex += 1;
                    }
                }
            }
            #endregion

            #region 索引数组

            int[] startIndex = new int[thisVertexCountPlus1];
            startIndex[0] = 0;
            for (int i = 1; i < thisVertexCountPlus1; i++)
            {
                startIndex[i] = PointCountPerRow[i - 1] + startIndex[i - 1];
            }

            List<short> indexList = new List<short>();
            indexList.AddRange(new short[] { 0, 1, 2, 0, 2, 3 });
            for (int i = 1; i < thisVertexCount; i++)
            {

                for (int j = 0; j < QuadCountPerRow[i]; j++)
                {
                    if (PointCountPerRow[i] == PointCountPerRow[i + 1])
                    {
                        indexList.Add((short)(startIndex[i] + j));
                        indexList.Add((short)(startIndex[i + 1] + j));
                        indexList.Add((short)(startIndex[i + 1] + j + 1));

                        indexList.Add((short)(startIndex[i] + j));
                        indexList.Add((short)(startIndex[i + 1] + j + 1));
                        indexList.Add((short)(startIndex[i] + j + 1));
                    }
                    else
                    {

                        indexList.Add((short)(startIndex[i] + j));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j + 1));

                        indexList.Add((short)(startIndex[i] + j));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j + 1));
                        indexList.Add((short)(startIndex[i] + j + 1));

                        indexList.Add((short)(startIndex[i] + j + 1));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j + 1));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j + 2));
                    }
                }
            }
            m_vertexIndexesT = indexList.ToArray();

            #endregion

            #endregion

            #region m_leftVertices m_rightVertices

            #region 顶点数组
            m_leftVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
            m_rightVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
            double deltaLat = scaleFactor * m_latitudeSpan * Degrees2Radians;
            double deltaLon = scaleFactor * m_longitudeSpan * Degrees2Radians;
            baseIndex = 0;
            if (m_centerLatitude.Degrees > 0)
            {
                for (int i = 0; i < thisVertexCountPlus1; i++)
                {
                    double lat = 0.5 * (m_north + m_south) * Degrees2Radians - deltaLat * i;
                    double sinLat = Math.Sin(lat);
                    double radCosLat = Math.Cos(lat) * m_layerRadius;
                    for (int j = 0; j < thisVertexCountPlus1; j++)
                    {
                        double lonWest = m_west * Degrees2Radians + deltaLon * j;
                        m_leftVertices[baseIndex].X = (float)(radCosLat * Math.Cos(lonWest) - m_localOrigin.X);
                        m_leftVertices[baseIndex].Y = (float)(radCosLat * Math.Sin(lonWest) - m_localOrigin.Y);
                        m_leftVertices[baseIndex].Z = (float)(m_layerRadius * sinLat - m_localOrigin.Z);
                        m_leftVertices[baseIndex].Tu = (float)(j * scaleFactor);
                        m_leftVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);

                        double lonEast = 0.5 * (m_west + m_east) * Degrees2Radians + deltaLon * j;
                        m_rightVertices[baseIndex].X = (float)(radCosLat * Math.Cos(lonEast) - m_localOrigin.X);
                        m_rightVertices[baseIndex].Y = (float)(radCosLat * Math.Sin(lonEast) - m_localOrigin.Y);
                        m_rightVertices[baseIndex].Z = (float)(m_layerRadius * sinLat - m_localOrigin.Z);
                        m_rightVertices[baseIndex].Tu = (float)((j + thisVertexCount) * scaleFactor);
                        m_rightVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);
                        baseIndex += 1;
                    }
                }
            }
            else
            {
                for (int i = 0; i < thisVertexCountPlus1; i++)
                {
                    double lat = 0.5 * (m_north + m_south) * Degrees2Radians + deltaLat * i;
                    double sinLat = Math.Sin(lat);
                    double radCosLat = Math.Cos(lat) * m_layerRadius;
                    for (int j = 0; j < thisVertexCountPlus1; j++)
                    {
                        double lonEast = m_east * Degrees2Radians - deltaLon * j;
                        m_leftVertices[baseIndex].X = (float)(radCosLat * Math.Cos(lonEast) - m_localOrigin.X);
                        m_leftVertices[baseIndex].Y = (float)(radCosLat * Math.Sin(lonEast) - m_localOrigin.Y);
                        m_leftVertices[baseIndex].Z = (float)(m_layerRadius * sinLat - m_localOrigin.Z);
                        m_leftVertices[baseIndex].Tu = 1.0f - (float)(j * scaleFactor);
                        m_leftVertices[baseIndex].Tv = 0.5f - (float)(i * scaleFactor);

                        double lonWest = 0.5 * (m_west + m_east) * Degrees2Radians - deltaLon * j;
                        m_rightVertices[baseIndex].X = (float)(radCosLat * Math.Cos(lonWest) - m_localOrigin.X);
                        m_rightVertices[baseIndex].Y = (float)(radCosLat * Math.Sin(lonWest) - m_localOrigin.Y);
                        m_rightVertices[baseIndex].Z = (float)(m_layerRadius * sinLat - m_localOrigin.Z);
                        m_rightVertices[baseIndex].Tu = 0.5f - (float)(j * scaleFactor);
                        m_rightVertices[baseIndex].Tv = 0.5f - (float)(i * scaleFactor);
                        baseIndex += 1;
                    }
                }
            }
            #endregion

            #region 同层裂缝处理
            baseIndex = totalVertexCount;
            if (m_centerLatitude.Degrees > 0)
            {
                double sinLat = Math.Sin(m_south * Degrees2Radians);
                double radCosLat = Math.Cos(m_south * Degrees2Radians) * m_layerRadius;
                for (int j = 0; j < thisVertexCount; j++)
                {
                    double lonWest = m_west * Degrees2Radians + deltaLon * (0.5 + j);
                    m_leftVertices[baseIndex].X = (float)(radCosLat * Math.Cos(lonWest) - m_localOrigin.X);
                    m_leftVertices[baseIndex].Y = (float)(radCosLat * Math.Sin(lonWest) - m_localOrigin.Y);
                    m_leftVertices[baseIndex].Z = (float)(m_layerRadius * sinLat - m_localOrigin.Z);
                    m_leftVertices[baseIndex].Tu = (float)((j + 0.5) * scaleFactor);
                    m_leftVertices[baseIndex].Tv = 1.0f;

                    double lonEast = 0.5 * (m_west + m_east) * Degrees2Radians + deltaLon * (0.5 + j);
                    m_rightVertices[baseIndex].X = (float)(radCosLat * Math.Cos(lonEast) - m_localOrigin.X);
                    m_rightVertices[baseIndex].Y = (float)(radCosLat * Math.Sin(lonEast) - m_localOrigin.Y);
                    m_rightVertices[baseIndex].Z = (float)(m_layerRadius * sinLat - m_localOrigin.Z);
                    m_rightVertices[baseIndex].Tu = (float)((j + 0.5 + thisVertexCount) * scaleFactor);
                    m_rightVertices[baseIndex].Tv = 1.0f;
                    baseIndex += 1;
                }
            }
            else
            {
                double sinLat = Math.Sin(m_north * Degrees2Radians);
                double radCosLat = Math.Cos(m_north * Degrees2Radians) * m_layerRadius;

                for (int j = 0; j < thisVertexCount; j++)
                {
                    double lonEast = m_east * Degrees2Radians - deltaLon * (0.5 + j);
                    m_leftVertices[baseIndex].X = (float)(radCosLat * Math.Cos(lonEast) - m_localOrigin.X);
                    m_leftVertices[baseIndex].Y = (float)(radCosLat * Math.Sin(lonEast) - m_localOrigin.Y);
                    m_leftVertices[baseIndex].Z = (float)(m_layerRadius * sinLat - m_localOrigin.Z);
                    m_leftVertices[baseIndex].Tu = 1.0f - (float)((j + 0.5) * scaleFactor);
                    m_leftVertices[baseIndex].Tv = 0.0f;

                    double lonWest = 0.5 * (m_west + m_east) * Degrees2Radians - deltaLon * (0.5 + j);
                    m_rightVertices[baseIndex].X = (float)(radCosLat * Math.Cos(lonWest) - m_localOrigin.X);
                    m_rightVertices[baseIndex].Y = (float)(radCosLat * Math.Sin(lonWest) - m_localOrigin.Y);
                    m_rightVertices[baseIndex].Z = (float)(m_layerRadius * sinLat - m_localOrigin.Z);
                    m_rightVertices[baseIndex].Tu = 0.5f - (float)((j + 0.5) * scaleFactor);
                    m_rightVertices[baseIndex].Tv = 0.0f;
                    baseIndex += 1;
                }
            }

            #endregion

            #region 索引数组
            int normalCount = 2 * (thisVertexCount - 1) * thisVertexCount * 3;//三角形索引长度（除最后一行三角形）
            m_vertexIndexesQ = new short[(2 * thisVertexCount + 1) * thisVertexCount * 3];
            baseIndex = 0;
            for (int i = 0; i < thisVertexCount - 1; i++)
            {
                for (int j = 0; j < thisVertexCount; j++)
                {
                    m_vertexIndexesQ[baseIndex] = (short)(i * thisVertexCountPlus1 + j);
                    m_vertexIndexesQ[baseIndex + 1] = (short)((i + 1) * thisVertexCountPlus1 + j);
                    m_vertexIndexesQ[baseIndex + 2] = (short)(i * thisVertexCountPlus1 + j + 1);

                    m_vertexIndexesQ[baseIndex + 3] = (short)(i * thisVertexCountPlus1 + j + 1);
                    m_vertexIndexesQ[baseIndex + 4] = (short)((i + 1) * thisVertexCountPlus1 + j);
                    m_vertexIndexesQ[baseIndex + 5] = (short)((i + 1) * thisVertexCountPlus1 + j + 1);

                    baseIndex += 6;
                }
            }
            baseIndex = normalCount;
            for (int j = 0; j < thisVertexCount; j++)
            {
                m_vertexIndexesQ[baseIndex] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j);
                m_vertexIndexesQ[baseIndex + 1] = (short)(thisVertexCount * thisVertexCountPlus1 + j);
                m_vertexIndexesQ[baseIndex + 2] = (short)(totalVertexCount + j);//？

                m_vertexIndexesQ[baseIndex + 3] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j);
                m_vertexIndexesQ[baseIndex + 4] = (short)(totalVertexCount + j);
                m_vertexIndexesQ[baseIndex + 5] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j + 1);

                m_vertexIndexesQ[baseIndex + 6] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j + 1);
                m_vertexIndexesQ[baseIndex + 7] = (short)(totalVertexCount + j);
                m_vertexIndexesQ[baseIndex + 8] = (short)(thisVertexCount * thisVertexCountPlus1 + j + 1);
                baseIndex += 9;
            }
            #endregion

            #endregion
        }
        /// <summary>
        /// 创建带有高程的瓦片
        /// </summary>
        private void CreateElevatedMesh()
        {
            int thisVertexCount = m_vertexCount / 2;
            int thisVertexCountPlus1 = thisVertexCount + 1;
            int totalVertexCount = thisVertexCountPlus1 * thisVertexCountPlus1;
            double scaleFactor = 1.0 / (double)m_vertexCount;
            int baseIndex = 0;

            #region m_topVertices

            #region 顶点数组
            int[] QuadCountPerRow = new int[thisVertexCount];
            int[] PointCountPerRow = new int[thisVertexCountPlus1];
            int TriQuadCount = 1;
            for (int i = 0; i < DQGTileSet.BaseLevel - 1; i++)
            {
                for (int j = (int)Math.Pow(2, i) + 1; j <= (int)Math.Pow(2, i + 1); j++)
                {
                    QuadCountPerRow[j - 1] = (int)Math.Pow(2, i + 1);
                    TriQuadCount += QuadCountPerRow[j - 1];
                }
            }
            QuadCountPerRow[0] = 1;
            PointCountPerRow[0] = 1;
            int TriPointCount = 1;
            for (int i = 1; i < thisVertexCount; i++)
            {
                PointCountPerRow[i] = QuadCountPerRow[i] + 1;
                TriPointCount += PointCountPerRow[i];
            }
            PointCountPerRow[thisVertexCount] = PointCountPerRow[thisVertexCount - 1] * 2 - 1;
            TriPointCount += PointCountPerRow[thisVertexCount];

            m_topVertices = new CustomVertex.PositionTextured[TriPointCount];
            if (m_centerLatitude.Degrees > 0)
            {
                double r = m_layerRadius + TerrainProvider.GetElevationAt(90, (m_south + m_west) * 0.5, this.m_level) * m_verticalExaggeration;
                Vector3 pos = MathEngine.SphericalToCartesian(90, (m_south + m_west) * 0.5, r) - m_localOrigin.Vector3;
                m_topVertices[0].Position = pos;
                m_topVertices[0].Tu=0.5f;
                m_topVertices[0].Tv=0.0f;
            }
            else
            {
                double r = m_layerRadius + TerrainProvider.GetElevationAt(-90, (m_south + m_west) * 0.5, this.m_level) * m_verticalExaggeration;
                Vector3 pos = MathEngine.SphericalToCartesian(-90, (m_south + m_west) * 0.5, r) - m_localOrigin.Vector3;
                m_topVertices[0].Position = pos;
                m_topVertices[0].Tu=0.5f;
                m_topVertices[0].Tv = 1.0f;
            }

            baseIndex = 1;
            for (int i = 1; i < thisVertexCountPlus1; i++)
            {
                double lonFactor = 1.0 / (double)(PointCountPerRow[i] - 1);
                if (m_centerLatitude.Degrees > 0)
                {
                    double lat = m_north - scaleFactor * m_latitudeSpan * i;
                    for (int j = 0; j < PointCountPerRow[i]; j++)
                    {
                        double lon = m_west + lonFactor * j * m_longitudeSpan;
                        float height = TerrainProvider.GetElevationAt(lat, lon, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = m_layerRadius + height * m_verticalExaggeration;
                        m_topVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lon, radius) - m_localOrigin.Vector3;
                        m_topVertices[baseIndex].Tu = (float)(j * lonFactor);
                        m_topVertices[baseIndex].Tv = (float)(i * scaleFactor);
                        baseIndex += 1;
                    }
                }
                else
                {
                    double lat = m_south + scaleFactor * m_latitudeSpan * i;
                    for (int j = 0; j < PointCountPerRow[i]; j++)
                    {
                        double lon = m_east - lonFactor * j * m_longitudeSpan;
                        float height = TerrainProvider.GetElevationAt(lat, lon, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = m_layerRadius + height * m_verticalExaggeration;
                        m_topVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lon, radius) - m_localOrigin.Vector3;
                        m_topVertices[baseIndex].Tu = 1.0f - (float)(j * lonFactor);
                        m_topVertices[baseIndex].Tv = 1.0f - (float)(i * scaleFactor);
                        baseIndex += 1;
                    }
                }
            }
            #endregion

            #region 索引数组

            int[] startIndex = new int[thisVertexCountPlus1];
            startIndex[0] = 0;
            for (int i = 1; i < thisVertexCountPlus1; i++)
            {
                startIndex[i] = PointCountPerRow[i - 1] + startIndex[i - 1];
            }

            List<short> indexList = new List<short>();
            indexList.AddRange(new short[] { 0, 1, 2, 0, 2, 3 });
            for (int i = 1; i < thisVertexCount; i++)
            {

                for (int j = 0; j < QuadCountPerRow[i]; j++)
                {
                    if (PointCountPerRow[i] == PointCountPerRow[i + 1])
                    {
                        indexList.Add((short)(startIndex[i] + j));
                        indexList.Add((short)(startIndex[i + 1] + j));
                        indexList.Add((short)(startIndex[i + 1] + j + 1));

                        indexList.Add((short)(startIndex[i] + j));
                        indexList.Add((short)(startIndex[i + 1] + j + 1));
                        indexList.Add((short)(startIndex[i] + j + 1));
                    }
                    else
                    {

                        indexList.Add((short)(startIndex[i] + j));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j + 1));

                        indexList.Add((short)(startIndex[i] + j));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j + 1));
                        indexList.Add((short)(startIndex[i] + j + 1));

                        indexList.Add((short)(startIndex[i] + j + 1));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j + 1));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j + 2));
                    }
                }
            }
            m_vertexIndexesT = indexList.ToArray();

            #endregion

            #endregion

            #region m_leftVertices m_rightVertices

            #region 顶点数组
            m_leftVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
            m_rightVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCount];
            double deltaLat = scaleFactor * m_latitudeSpan;
            double deltaLon = scaleFactor * m_longitudeSpan;
            baseIndex = 0;
            if (m_centerLatitude.Degrees > 0)
            {
                for (int i = 0; i < thisVertexCountPlus1; i++)
                {
                    double lat = 0.5 * (m_north + m_south) - deltaLat * i;
                    for (int j = 0; j < thisVertexCountPlus1; j++)
                    {
                        double lonWest = m_west + deltaLon * j;
                        float height = TerrainProvider.GetElevationAt(lat, lonWest, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = m_layerRadius + height * m_verticalExaggeration;
                        m_leftVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lonWest, radius) - m_localOrigin.Vector3;
                        m_leftVertices[baseIndex].Tu = (float)(j * scaleFactor);
                        m_leftVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);

                        double lonEast = 0.5 * (m_west + m_east) + deltaLon * j;
                        height = TerrainProvider.GetElevationAt(lat, lonEast, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        radius = m_layerRadius + height * m_verticalExaggeration;
                        m_rightVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lonEast, radius) - m_localOrigin.Vector3;
                        m_rightVertices[baseIndex].Tu = (float)((j + thisVertexCount) * scaleFactor);
                        m_rightVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);
                        baseIndex += 1;
                    }
                }
            }
            else
            {
                for (int i = 0; i < thisVertexCountPlus1; i++)
                {
                    double lat = 0.5 * (m_north + m_south) + deltaLat * i;
                    for (int j = 0; j < thisVertexCountPlus1; j++)
                    {
                        double lonEast = m_east - deltaLon * j;
                        float height = TerrainProvider.GetElevationAt(lat, lonEast, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = m_layerRadius + height * m_verticalExaggeration;
                        m_leftVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lonEast, radius) - m_localOrigin.Vector3;
                        m_leftVertices[baseIndex].Tu = 1.0f - (float)(j * scaleFactor);
                        m_leftVertices[baseIndex].Tv = 0.5f - (float)(i * scaleFactor);

                        double lonWest = 0.5 * (m_west + m_east) - deltaLon * j;
                        height = TerrainProvider.GetElevationAt(lat, lonWest, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        radius = m_layerRadius + height * m_verticalExaggeration;
                        m_rightVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lonWest, radius) - m_localOrigin.Vector3;
                        m_rightVertices[baseIndex].Tu = 0.5f - (float)(j * scaleFactor);
                        m_rightVertices[baseIndex].Tv = 0.5f - (float)(i * scaleFactor);
                        baseIndex += 1;
                    }
                }
            }
            #endregion

            #region 同层裂缝处理
            baseIndex = totalVertexCount;
            if (m_centerLatitude.Degrees > 0)
            {
                for (int j = 0; j < thisVertexCount; j++)
                {
                    double lonWest = m_west + deltaLon * (0.5 + j);
                    float height = TerrainProvider.GetElevationAt(m_south, lonWest, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    double radius = m_layerRadius + height * m_verticalExaggeration;
                    m_leftVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_south, lonWest, radius) - m_localOrigin.Vector3;
                    m_leftVertices[baseIndex].Tu = (float)((j + 0.5) * scaleFactor);
                    m_leftVertices[baseIndex].Tv = 1.0f;

                    double lonEast = 0.5 * (m_west + m_east) + deltaLon * (0.5 + j);
                    height = TerrainProvider.GetElevationAt(m_south, lonEast, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    radius = m_layerRadius + height * m_verticalExaggeration;
                    m_rightVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_south, lonEast, radius) - m_localOrigin.Vector3;
                    m_rightVertices[baseIndex].Tu = (float)((j + 0.5 + thisVertexCount) * scaleFactor);
                    m_rightVertices[baseIndex].Tv = 1.0f;
                    baseIndex += 1;
                }
            }
            else
            {
                for (int j = 0; j < thisVertexCount; j++)
                {
                    double lonEast = m_east - deltaLon * (0.5 + j);
                    float height = TerrainProvider.GetElevationAt(m_north, lonEast, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    double radius = m_layerRadius + height * m_verticalExaggeration;
                    m_leftVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_north, lonEast, radius) - m_localOrigin.Vector3;
                    m_leftVertices[baseIndex].Tu = 1.0f - (float)((j + 0.5) * scaleFactor);
                    m_leftVertices[baseIndex].Tv = 0.0f;

                    double lonWest = 0.5 * (m_west + m_east) - deltaLon * (0.5 + j);
                    height = TerrainProvider.GetElevationAt(m_north, lonWest, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    radius = m_layerRadius + height * m_verticalExaggeration;
                    m_rightVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_north, lonWest, radius) - m_localOrigin.Vector3;
                    m_rightVertices[baseIndex].Tu = 0.5f - (float)((j + 0.5) * scaleFactor);
                    m_rightVertices[baseIndex].Tv = 0.0f;
                    baseIndex += 1;
                }
            }

            #endregion

            #region 索引数组
            int normalCount = 2 * (thisVertexCount - 1) * thisVertexCount * 3;
            m_vertexIndexesQ = new short[(2 * thisVertexCount + 1) * thisVertexCount * 3];
            baseIndex = 0;
            for (int i = 0; i < thisVertexCount - 1; i++)
            {
                for (int j = 0; j < thisVertexCount; j++)
                {
                    m_vertexIndexesQ[baseIndex] = (short)(i * thisVertexCountPlus1 + j);
                    m_vertexIndexesQ[baseIndex + 1] = (short)((i + 1) * thisVertexCountPlus1 + j);
                    m_vertexIndexesQ[baseIndex + 2] = (short)(i * thisVertexCountPlus1 + j + 1);

                    m_vertexIndexesQ[baseIndex + 3] = (short)(i * thisVertexCountPlus1 + j + 1);
                    m_vertexIndexesQ[baseIndex + 4] = (short)((i + 1) * thisVertexCountPlus1 + j);
                    m_vertexIndexesQ[baseIndex + 5] = (short)((i + 1) * thisVertexCountPlus1 + j + 1);

                    baseIndex += 6;
                }
            }
            baseIndex = normalCount;
            for (int j = 0; j < thisVertexCount; j++)
            {
                m_vertexIndexesQ[baseIndex] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j);
                m_vertexIndexesQ[baseIndex + 1] = (short)(thisVertexCount * thisVertexCountPlus1 + j);
                m_vertexIndexesQ[baseIndex + 2] = (short)(totalVertexCount + j);

                m_vertexIndexesQ[baseIndex + 3] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j);
                m_vertexIndexesQ[baseIndex + 4] = (short)(totalVertexCount + j);
                m_vertexIndexesQ[baseIndex + 5] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j + 1);

                m_vertexIndexesQ[baseIndex + 6] = (short)((thisVertexCount - 1) * thisVertexCountPlus1 + j + 1);
                m_vertexIndexesQ[baseIndex + 7] = (short)(totalVertexCount + j);
                m_vertexIndexesQ[baseIndex + 8] = (short)(thisVertexCount * thisVertexCountPlus1 + j + 1);
                baseIndex += 9;
            }
            #endregion

            #endregion
        }
        /// <summary>
        /// 创建没有细分的瓦片
        /// </summary>
        private void CreateElevatedEdgedMesh()
        {
            int thisVertexCount = m_vertexCount / 2;
            int thisVertexCountPlus1 = thisVertexCount + 1;
            int totalVertexCount = thisVertexCountPlus1 * thisVertexCountPlus1;
            double scaleFactor = 1.0 / (double)m_vertexCount;
            int baseIndex = 0;

            #region m_topVertices

            #region 顶点数组
            int[] QuadCountPerRow = new int[thisVertexCount];
            int[] PointCountPerRow = new int[thisVertexCountPlus1];
            int TriQuadCount = 1;
            for (int i = 0; i < DQGTileSet.BaseLevel - 1; i++)
            {
                for (int j = (int)Math.Pow(2, i) + 1; j <= (int)Math.Pow(2, i + 1); j++)
                {
                    QuadCountPerRow[j - 1] = (int)Math.Pow(2, i + 1);
                    TriQuadCount += QuadCountPerRow[j - 1];
                }
            }
            QuadCountPerRow[0] = 1;
            PointCountPerRow[0] = 1;
            int TriPointCount = 1;
            for (int i = 1; i < thisVertexCount; i++)
            {
                PointCountPerRow[i] = QuadCountPerRow[i] + 1;
                TriPointCount += PointCountPerRow[i];
            }
            PointCountPerRow[thisVertexCount] = PointCountPerRow[thisVertexCount - 1] * 2 - 1;
            TriPointCount += PointCountPerRow[thisVertexCount];

            m_topVertices = new CustomVertex.PositionTextured[TriPointCount + thisVertexCountPlus1 * 2 + PointCountPerRow[thisVertexCount - 1]];
            if (m_centerLatitude.Degrees > 0)
            {
                double r = m_layerRadius+TerrainProvider.GetElevationAt(89.9999, (m_south + m_west) * 0.5, this.m_level) * m_verticalExaggeration;
                Vector3 pos = MathEngine.SphericalToCartesian(90, (m_south + m_west) * 0.5, r) - m_localOrigin.Vector3;
                m_topVertices[0].Position = pos;
                m_topVertices[0].Tu = 0.5f;
                m_topVertices[0].Tv = 0.0f;
            }
            else
            {
                double r = m_layerRadius + TerrainProvider.GetElevationAt(-90, (m_south + m_west) * 0.5, this.m_level) * m_verticalExaggeration;
                Vector3 pos = MathEngine.SphericalToCartesian(-90, (m_south + m_west) * 0.5, r) - m_localOrigin.Vector3;
                m_topVertices[0].Position = pos;
                m_topVertices[0].Tu = 0.5f;
                m_topVertices[0].Tv = 1.0f;
            }

            baseIndex = 1;
            for (int i = 1; i < thisVertexCountPlus1; i++)
            {
                double lonFactor = 1.0 / (double)(PointCountPerRow[i] - 1);
                if (m_centerLatitude.Degrees > 0)
                {
                    double lat = m_north - scaleFactor * m_latitudeSpan * i;
                    for (int j = 0; j < PointCountPerRow[i]; j++)
                    {
                        double lon = m_west + lonFactor * j * m_longitudeSpan;
                        float height = TerrainProvider.GetElevationAt(lat, lon, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = m_layerRadius + height * m_verticalExaggeration;
                        m_topVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lon, radius) - m_localOrigin.Vector3;
                        m_topVertices[baseIndex].Tu = (float)(j * lonFactor);
                        m_topVertices[baseIndex].Tv = (float)(i * scaleFactor);
                        baseIndex += 1;
                    }
                }
                else
                {
                    double lat = m_south + scaleFactor * m_latitudeSpan * i;
                    for (int j = 0; j < PointCountPerRow[i]; j++)
                    {
                        double lon = m_east - lonFactor * j * m_longitudeSpan;
                        float height = TerrainProvider.GetElevationAt(lat, lon, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = m_layerRadius + height * m_verticalExaggeration;
                        m_topVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lon, radius) - m_localOrigin.Vector3;
                        m_topVertices[baseIndex].Tu = 1.0f - (float)(j * lonFactor);
                        m_topVertices[baseIndex].Tv = 1.0f - (float)(i * scaleFactor);
                        baseIndex += 1;
                    }
                }
            }

            #region 侧面顶点

            int[] startIndex = new int[thisVertexCountPlus1];
            startIndex[0] = 0;
            for (int i = 1; i < thisVertexCountPlus1; i++)
            {
                startIndex[i] = PointCountPerRow[i - 1] + startIndex[i - 1];
            }

            baseIndex = TriPointCount;
            for (int i = 0; i < thisVertexCountPlus1; i++)
            {
                m_topVertices[baseIndex].Position = ProjectOnMeshBase(m_topVertices[startIndex[i]].Position);
                m_topVertices[baseIndex].Tu = m_topVertices[startIndex[i]].Tu;
                m_topVertices[baseIndex].Tv = m_topVertices[startIndex[i]].Tv;

                m_topVertices[baseIndex + thisVertexCountPlus1].Position = ProjectOnMeshBase(m_topVertices[startIndex[i] + PointCountPerRow[i] - 1].Position);
                m_topVertices[baseIndex + thisVertexCountPlus1].Tu = m_topVertices[startIndex[i] + PointCountPerRow[i] - 1].Tu;
                m_topVertices[baseIndex + thisVertexCountPlus1].Tv = m_topVertices[startIndex[i] + PointCountPerRow[i] - 1].Tv;
                baseIndex += 1;
            }

            baseIndex = TriPointCount + thisVertexCountPlus1 * 2;
            for (int i = 0; i < PointCountPerRow[thisVertexCount]; i += 2)
            {
                m_topVertices[baseIndex].Position = ProjectOnMeshBase(m_topVertices[startIndex[thisVertexCount] + i].Position);
                m_topVertices[baseIndex].Tu = m_topVertices[startIndex[thisVertexCount] + i].Tu;
                m_topVertices[baseIndex].Tv = m_topVertices[startIndex[thisVertexCount] + i].Tv;
                baseIndex += 1;
            }

            #endregion

            #endregion

            #region 索引数组

            List<short> indexList = new List<short>();
            indexList.AddRange(new short[] { 0, 1, 2, 0, 2, 3 });
            indexList.Add(0);
            indexList.Add((short)TriPointCount);
            indexList.Add((short)1);
            indexList.Add((short)TriPointCount);
            indexList.Add((short)(TriPointCount + 1));
            indexList.Add((short)1);
            indexList.Add((short)(TriPointCount + thisVertexCountPlus1));
            indexList.Add(0);
            indexList.Add((short)(TriPointCount + thisVertexCountPlus1 + 1));
            indexList.Add(0);
            indexList.Add(3);
            indexList.Add((short)(TriPointCount + thisVertexCountPlus1 + 1));


            for (int i = 1; i < thisVertexCount; i++)
            {
                indexList.Add((short)startIndex[i]);
                indexList.Add((short)(TriPointCount + i));
                indexList.Add((short)startIndex[i + 1]);
                indexList.Add((short)(TriPointCount + i));
                indexList.Add((short)(TriPointCount + i + 1));
                indexList.Add((short)startIndex[i + 1]);

                indexList.Add((short)(TriPointCount + thisVertexCountPlus1 + i));
                indexList.Add((short)(startIndex[i] + PointCountPerRow[i] - 1));
                indexList.Add((short)(TriPointCount + thisVertexCountPlus1 + i + 1));
                indexList.Add((short)(startIndex[i] + PointCountPerRow[i] - 1));
                indexList.Add((short)(startIndex[i + 1] + PointCountPerRow[i + 1] - 1));
                indexList.Add((short)(TriPointCount + thisVertexCountPlus1 + i + 1));
                
                for (int j = 0; j < QuadCountPerRow[i]; j++)
                {
                    if (PointCountPerRow[i] == PointCountPerRow[i + 1])
                    {
                        indexList.Add((short)(startIndex[i] + j));
                        indexList.Add((short)(startIndex[i + 1] + j));
                        indexList.Add((short)(startIndex[i + 1] + j + 1));

                        indexList.Add((short)(startIndex[i] + j));
                        indexList.Add((short)(startIndex[i + 1] + j + 1));
                        indexList.Add((short)(startIndex[i] + j + 1));
                    }
                    else
                    {
                        indexList.Add((short)(startIndex[i] + j));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j + 1));

                        indexList.Add((short)(startIndex[i] + j));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j + 1));
                        indexList.Add((short)(startIndex[i] + j + 1));

                        indexList.Add((short)(startIndex[i] + j + 1));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j + 1));
                        indexList.Add((short)(startIndex[i + 1] + 2 * j + 2));
                    }
                }
            }
            int count1 = startIndex[thisVertexCount];
            int count2 = TriPointCount + thisVertexCountPlus1 * 2;
            for (int i = 0; i < QuadCountPerRow[thisVertexCount - 1];i++ )
            {
                indexList.Add((short)(count1 + 2 * i));
                indexList.Add((short)(count2 + i));
                indexList.Add((short)(count1 + 2 * i + 1));

                indexList.Add((short)(count1 + 2 * i + 1));
                indexList.Add((short)(count2 + i));
                indexList.Add((short)(count2 + i + 1));

                indexList.Add((short)(count1 + 2 * (i + 1)));
                indexList.Add((short)(count1 + 2 * i + 1));
                indexList.Add((short)(count2 + i + 1));                
            }

            m_vertexIndexesT = indexList.ToArray();

            #endregion

            #endregion

            #region m_leftVertices m_rightVertices

            #region 顶点数组
            int thisVertexCountPlus3 = thisVertexCount + 3;
            int thisVertexCountPlus2 = thisVertexCount + 2;
            totalVertexCount = thisVertexCountPlus3 * thisVertexCountPlus3;
            m_leftVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCountPlus2];
            m_rightVertices = new CustomVertex.PositionTextured[totalVertexCount + thisVertexCountPlus2];
            double deltaLat = scaleFactor * m_latitudeSpan;
            double deltaLon = scaleFactor * m_longitudeSpan;
            baseIndex = 0;
            if (m_centerLatitude.Degrees > 0)
            {
                for (int i = -1; i < thisVertexCountPlus2; i++)
                {
                    double lat = 0.5 * (m_north + m_south) - deltaLat * i;
                    for (int j = -1; j < thisVertexCountPlus2; j++)
                    {
                        double lonWest = m_west + deltaLon * j;
                        float height = TerrainProvider.GetElevationAt(lat, lonWest, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = m_layerRadius + height * m_verticalExaggeration;
                        m_leftVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lonWest, radius) - m_localOrigin.Vector3;
                        m_leftVertices[baseIndex].Tu = (float)(j * scaleFactor);
                        m_leftVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);

                        double lonEast = 0.5 * (m_west + m_east) + deltaLon * j;
                        height = TerrainProvider.GetElevationAt(lat, lonEast, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        radius = m_layerRadius + height * m_verticalExaggeration;
                        m_rightVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lonEast, radius) - m_localOrigin.Vector3;
                        m_rightVertices[baseIndex].Tu = (float)((j + thisVertexCount) * scaleFactor);
                        m_rightVertices[baseIndex].Tv = (float)((i + thisVertexCount) * scaleFactor);
                        baseIndex += 1;
                    }
                }
            }
            else
            {
                for (int i = -1; i < thisVertexCountPlus2; i++)
                {
                    double lat = 0.5 * (m_north + m_south) + deltaLat * i;
                    for (int j = -1; j < thisVertexCountPlus2; j++)
                    {
                        double lonEast = m_east - deltaLon * j;
                        float height = TerrainProvider.GetElevationAt(lat, lonEast, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        double radius = m_layerRadius + height * m_verticalExaggeration;
                        m_leftVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lonEast, radius) - m_localOrigin.Vector3;
                        m_leftVertices[baseIndex].Tu = 1.0f - (float)(j * scaleFactor);
                        m_leftVertices[baseIndex].Tv = 0.5f - (float)(i * scaleFactor);

                        double lonWest = 0.5 * (m_west + m_east) - deltaLon * j;
                        height = TerrainProvider.GetElevationAt(lat, lonWest, this.m_level);
                        if (height > m_maxElevation)
                            m_maxElevation = height;
                        if (height < m_minElevation)
                            m_minElevation = height;
                        radius = m_layerRadius + height * m_verticalExaggeration;
                        m_rightVertices[baseIndex].Position = MathEngine.SphericalToCartesian(lat, lonWest, radius) - m_localOrigin.Vector3;
                        m_rightVertices[baseIndex].Tu = 0.5f - (float)(j * scaleFactor);
                        m_rightVertices[baseIndex].Tv = 0.5f - (float)(i * scaleFactor);
                        baseIndex += 1;
                    }
                }
            }

            for (int i = 0; i < thisVertexCountPlus3; i += thisVertexCountPlus2)
            {
                for (int j = 0; j < thisVertexCountPlus3; j++)
                {
                    int index = 1;
                    if (i != 0)
                        index = -1;
                    m_leftVertices[thisVertexCountPlus3 * i + j].Position = ProjectOnMeshBase(m_leftVertices[thisVertexCountPlus3 * (i + index) + j].Position);
                    m_rightVertices[thisVertexCountPlus3 * i + j].Position = ProjectOnMeshBase(m_rightVertices[thisVertexCountPlus3 * (i + index) + j].Position);
                }
            }

            for (int i = 0; i < thisVertexCountPlus3; i++)
            {
                for (int j = 0; j < thisVertexCountPlus3; j += thisVertexCountPlus2)
                {
                    int index = 1;
                    if (j != 0)
                        index = -1;
                    m_leftVertices[thisVertexCountPlus3 * i + j].Position = ProjectOnMeshBase(m_leftVertices[thisVertexCountPlus3 * i + j + index].Position);
                    m_rightVertices[thisVertexCountPlus3 * i + j].Position = ProjectOnMeshBase(m_rightVertices[thisVertexCountPlus3 * i + j + index].Position);
                }
            }

            #endregion

            #region 同层裂缝处理
            baseIndex = totalVertexCount;
            if (m_centerLatitude.Degrees > 0)
            {
                for (int j = -1; j < thisVertexCountPlus1; j++)
                {
                    double lonWest = m_west + deltaLon * (0.5 + j);
                    if (j == -1)
                        lonWest = m_west;
                    else if (j == thisVertexCount)
                        lonWest = (m_west + m_east) * 0.5;
                    float height = TerrainProvider.GetElevationAt(m_south, lonWest, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    double radius = m_layerRadius + height * m_verticalExaggeration;
                    m_leftVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_south, lonWest, radius) - m_localOrigin.Vector3;
                    m_leftVertices[baseIndex].Tu = (float)((j + 0.5) * scaleFactor);
                    m_leftVertices[baseIndex].Tv = 1.0f;

                    double lonEast = 0.5 * (m_west + m_east) + deltaLon * (0.5 + j);
                    if (j == -1)
                        lonEast = (m_west + m_east) * 0.5;
                    else if (j == thisVertexCount)
                        lonEast = m_east;
                    height = TerrainProvider.GetElevationAt(m_south, lonEast, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    radius = m_layerRadius + height * m_verticalExaggeration;
                    m_rightVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_south, lonEast, radius) - m_localOrigin.Vector3;
                    m_rightVertices[baseIndex].Tu = (float)((j + 0.5 + thisVertexCount) * scaleFactor);
                    m_rightVertices[baseIndex].Tv = 1.0f;
                    baseIndex += 1;
                }
            }
            else
            {
                for (int j = -1; j < thisVertexCountPlus1; j++)
                {
                    double lonEast = m_east - deltaLon * (0.5 + j);
                    if (j == -1)
                        lonEast = m_east;
                    else if(j==thisVertexCount)
                        lonEast = (m_west + m_east) * 0.5;
                    float height = TerrainProvider.GetElevationAt(m_north, lonEast, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    double radius = m_layerRadius + height * m_verticalExaggeration;
                    m_leftVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_north, lonEast, radius) - m_localOrigin.Vector3;
                    m_leftVertices[baseIndex].Tu = 1.0f - (float)((j + 0.5) * scaleFactor);
                    m_leftVertices[baseIndex].Tv = 0.0f;

                    double lonWest = 0.5 * (m_west + m_east) - deltaLon * (0.5 + j);
                    if (j == -1)
                        lonWest = (m_west + m_east) * 0.5;
                    else if (j == thisVertexCount)
                        lonWest = m_west;
                    height = TerrainProvider.GetElevationAt(m_north, lonWest, this.m_level);
                    if (height > m_maxElevation)
                        m_maxElevation = height;
                    if (height < m_minElevation)
                        m_minElevation = height;
                    radius = m_layerRadius + height * m_verticalExaggeration;
                    m_rightVertices[baseIndex].Position = MathEngine.SphericalToCartesian(m_north, lonWest, radius) - m_localOrigin.Vector3;
                    m_rightVertices[baseIndex].Tu = 0.5f - (float)((j + 0.5) * scaleFactor);
                    m_rightVertices[baseIndex].Tv = 0.0f;
                    baseIndex += 1;
                }
            }

            #endregion

            #region 索引数组
            int normalCount = 2 * thisVertexCount * thisVertexCountPlus2 * 3;
            m_vertexIndexesQ = new short[normalCount + 2 * thisVertexCountPlus2 * 9];
            baseIndex = 0;
            for (int i = 0; i < thisVertexCount; i++)
            {
                for (int j = 0; j < thisVertexCountPlus2; j++)
                {
                    m_vertexIndexesQ[baseIndex] = (short)(i * thisVertexCountPlus3 + j);
                    m_vertexIndexesQ[baseIndex + 1] = (short)((i + 1) * thisVertexCountPlus3 + j);
                    m_vertexIndexesQ[baseIndex + 2] = (short)(i * thisVertexCountPlus3 + j + 1);

                    m_vertexIndexesQ[baseIndex + 3] = (short)(i * thisVertexCountPlus3 + j + 1);
                    m_vertexIndexesQ[baseIndex + 4] = (short)((i + 1) * thisVertexCountPlus3 + j);
                    m_vertexIndexesQ[baseIndex + 5] = (short)((i + 1) * thisVertexCountPlus3 + j + 1);

                    baseIndex += 6;
                }
            }
            baseIndex = normalCount;
            for (int j = 0; j < thisVertexCountPlus2; j++)
            {
                m_vertexIndexesQ[baseIndex] = (short)(thisVertexCount * thisVertexCountPlus3 + j);
                m_vertexIndexesQ[baseIndex + 1] = (short)(thisVertexCountPlus1 * thisVertexCountPlus3 + j);
                m_vertexIndexesQ[baseIndex + 2] = (short)(totalVertexCount + j);

                m_vertexIndexesQ[baseIndex + 3] = (short)(thisVertexCount * thisVertexCountPlus3 + j);
                m_vertexIndexesQ[baseIndex + 4] = (short)(totalVertexCount + j);
                m_vertexIndexesQ[baseIndex + 5] = (short)(thisVertexCount * thisVertexCountPlus3 + j + 1);

                m_vertexIndexesQ[baseIndex + 6] = (short)(thisVertexCount * thisVertexCountPlus3 + j + 1);
                m_vertexIndexesQ[baseIndex + 7] = (short)(totalVertexCount + j);
                m_vertexIndexesQ[baseIndex + 8] = (short)(thisVertexCountPlus1 * thisVertexCountPlus3 + j + 1);
                baseIndex += 9;
            }
            baseIndex = normalCount + 9 * thisVertexCountPlus2;
            for (int j = 0; j < thisVertexCountPlus2; j++)
            {
                m_vertexIndexesQ[baseIndex] = (short)(thisVertexCountPlus1 * thisVertexCountPlus3 + j);
                m_vertexIndexesQ[baseIndex + 1] = (short)(thisVertexCountPlus2 * thisVertexCountPlus3 + j);
                m_vertexIndexesQ[baseIndex + 2] = (short)(totalVertexCount + j);

                m_vertexIndexesQ[baseIndex + 3] = (short)(thisVertexCountPlus2 * thisVertexCountPlus3 + j);
                m_vertexIndexesQ[baseIndex + 4] = (short)(thisVertexCountPlus2 * thisVertexCountPlus3 + j + 1);
                m_vertexIndexesQ[baseIndex + 5] = (short)(totalVertexCount + j);


                m_vertexIndexesQ[baseIndex + 6] = (short)((thisVertexCountPlus1) * thisVertexCountPlus3 + j + 1);
                m_vertexIndexesQ[baseIndex + 7] = (short)(totalVertexCount + j);
                m_vertexIndexesQ[baseIndex + 8] = (short)(thisVertexCountPlus2 * thisVertexCountPlus3 + j + 1);
                baseIndex += 9;
            }
            #endregion

            #endregion
        }
        /// <summary>
        /// 创建没有细分的瓦片
        /// </summary>
        private void CreateOriFlatMesh()
        {
            m_topVertices = new CustomVertex.PositionTextured[4];
            m_leftVertices = new CustomVertex.PositionTextured[4];
            m_rightVertices = new CustomVertex.PositionTextured[4];
            m_vertexIndexesT = new short[] { 0, 2, 1, 0, 3, 2 };
            m_vertexIndexesNormal = new short[] { 0, 1, 2, 1, 3, 2 };
            if (m_centerLatitude.Degrees > 0)
            {
                m_topVertices[0].Position = MathEngine.SphericalToCartesian(90, 0, m_layerRadius) - m_localOrigin.Vector3;
                m_topVertices[0].Tu = 0.5f; m_topVertices[0].Tv = 0.0f;
                m_topVertices[1].Position = MathEngine.SphericalToCartesian(m_centerLatitude.Degrees, m_west, m_layerRadius) - m_localOrigin.Vector3;
                m_topVertices[1].Tu = 0.0f; m_topVertices[1].Tv = 0.5f;
                m_topVertices[2].Position = MathEngine.SphericalToCartesian(m_centerLatitude, m_centerLongitude, m_layerRadius) - m_localOrigin.Vector3;
                m_topVertices[2].Tu = 0.5f; m_topVertices[2].Tv = 0.5f;
                m_topVertices[3].Position = MathEngine.SphericalToCartesian(m_centerLatitude.Degrees, m_east, m_layerRadius) - m_localOrigin.Vector3;
                m_topVertices[3].Tu = 1.0f; m_topVertices[3].Tv = 0.5f;

                m_leftVertices[0] = m_topVertices[1];
                m_leftVertices[1] = m_topVertices[2];
                m_leftVertices[2].Position = MathEngine.SphericalToCartesian(m_south, m_west, m_layerRadius) - m_localOrigin.Vector3;
                m_leftVertices[2].Tu = 0.0f; m_leftVertices[2].Tv = 1.0f;
                m_leftVertices[3].Position = MathEngine.SphericalToCartesian(m_south, m_centerLongitude.Degrees, m_layerRadius) - m_localOrigin.Vector3;
                m_leftVertices[3].Tu = 0.5f; m_leftVertices[3].Tv = 1.0f;

                m_rightVertices[0] = m_leftVertices[1];
                m_rightVertices[1].Position = MathEngine.SphericalToCartesian(m_centerLatitude.Degrees, m_east, m_layerRadius) - m_localOrigin.Vector3;
                m_rightVertices[1].Tu = 1.0f; m_rightVertices[1].Tv = 0.5f;
                m_rightVertices[2] = m_leftVertices[3];
                m_rightVertices[3].Position = MathEngine.SphericalToCartesian(m_south, m_east, m_layerRadius) - m_localOrigin.Vector3;
                m_rightVertices[3].Tu = 1.0f; m_rightVertices[3].Tv = 1.0f;

            }
            else
            {
                m_topVertices[0].Position = MathEngine.SphericalToCartesian(-90, 0, m_layerRadius) - m_localOrigin.Vector3;
                m_topVertices[0].Tu = 0.5f; m_topVertices[0].Tv = 1.0f;
                m_topVertices[1].Position = MathEngine.SphericalToCartesian(m_centerLatitude.Degrees, m_east, m_layerRadius) - m_localOrigin.Vector3;
                m_topVertices[1].Tu = 1.0f; m_topVertices[1].Tv = 0.5f;
                m_topVertices[2].Position = MathEngine.SphericalToCartesian(m_centerLatitude, m_centerLongitude, m_layerRadius) - m_localOrigin.Vector3;
                m_topVertices[2].Tu = 0.5f; m_topVertices[2].Tv = 0.5f;
                m_topVertices[3].Position = MathEngine.SphericalToCartesian(m_centerLatitude.Degrees, m_west, m_layerRadius) - m_localOrigin.Vector3;
                m_topVertices[3].Tu = 0.0f; m_topVertices[3].Tv = 0.5f;

                m_leftVertices[0] = m_topVertices[1];
                m_leftVertices[1] = m_topVertices[2];
                m_leftVertices[2].Position = MathEngine.SphericalToCartesian(m_north, m_east, m_layerRadius) - m_localOrigin.Vector3;
                m_leftVertices[2].Tu = 1.0f; m_leftVertices[2].Tv = 0.0f;
                m_leftVertices[3].Position = MathEngine.SphericalToCartesian(m_north, m_centerLongitude.Degrees, m_layerRadius) - m_localOrigin.Vector3;
                m_leftVertices[3].Tu = 0.5f; m_leftVertices[3].Tv = 0.0f;

                m_rightVertices[0] = m_leftVertices[1];
                m_rightVertices[1].Position = MathEngine.SphericalToCartesian(m_centerLatitude.Degrees, m_west, m_layerRadius) - m_localOrigin.Vector3;
                m_rightVertices[1].Tu = 0.0f; m_rightVertices[1].Tv = 0.5f;
                m_rightVertices[2] = m_leftVertices[3];
                m_rightVertices[3].Position = MathEngine.SphericalToCartesian(m_north, m_west, m_layerRadius) - m_localOrigin.Vector3;
                m_rightVertices[3].Tu = 0.0f; m_rightVertices[3].Tv = 0.0f;
            }



        }
        //private void CalculateNormals(ref CustomVertex.PositionTextured[] vertices, short[] indices)
        //{
        //    for (int i = 0; i < indices.Length; i += 3)
        //    {
        //        Vector3 p1 = vertices[indices[i + 0]].Position;
        //        Vector3 p2 = vertices[indices[i + 1]].Position;
        //        Vector3 p3 = vertices[indices[i + 2]].Position;

        //        Vector3 v1 = p2 - p1;
        //        Vector3 v2 = p3 - p1;
        //        Vector3 normal = Vector3.Cross(v1, v2);

        //        normal.Normalize();

        //        vertices[indices[i + 0]].Normal += normal;
        //        vertices[indices[i + 1]].Normal += normal;
        //        vertices[indices[i + 2]].Normal += normal;
        //    }

        //    for (int i = 0; i < vertices.Length; i++)
        //    {
        //        vertices[i].Normal.Normalize();
        //    }


        //}
        
        private Vector3 ProjectOnMeshBase(Vector3 p)
        {
            float meshBaseRadius = (float)(m_layerRadius + m_minElevation * m_verticalExaggeration - 500 * m_verticalExaggeration);
            p += this.m_localOrigin.Vector3;
            p.Normalize();
            p = p * meshBaseRadius - this.m_localOrigin.Vector3;
            return p;
        }
        private void ComputeChildren(DrawArgs drawArgs)
        {
            if (m_level + 1 >= DQGTileSet.LevelCount)
                return;

            double CenterLat = 0.5f * (m_south + m_north);
            double CenterLon = 0.5f * (m_east + m_west);
            if (CenterLat > 0)
            {
                if (m_topChild == null)
                    m_topChild = new DQGTriangleTile(Address + "0", m_level + 1, CenterLat, m_north, m_west, m_east);

                if (m_leftChild == null)
                    m_leftChild = new DQGQuadTile(Address + "2", m_south, CenterLat, m_west, CenterLon, m_level + 1);

                if (m_rightChild == null)
                    m_rightChild = new DQGQuadTile(Address + "3", m_south, CenterLat, CenterLon, m_east, m_level + 1);
            }
            else
            {
                if (m_topChild == null)
                    m_topChild = new DQGTriangleTile(Address + "0", m_level + 1, m_south, CenterLat, m_west, m_east);

                if (m_leftChild == null)
                    m_leftChild = new DQGQuadTile(Address + "2", CenterLat, m_north, CenterLon, m_east, m_level + 1);

                if (m_rightChild == null)
                    m_rightChild = new DQGQuadTile(Address + "3", CenterLat, m_north, m_west, CenterLon, m_level + 1);
            }
        }
        private void Render(Device device, CustomVertex.PositionTextured[] verts, short[] indexes)
        {

            device.RenderState.ZBufferEnable = true;
            device.RenderState.Lighting = false;
            if (this.m_effect != null)
            {
                m_effect.Technique = m_effect.GetTechnique(0);
                m_effect.SetValue("WorldViewProj", Matrix.Multiply(device.Transform.World, Matrix.Multiply(device.Transform.View, device.Transform.Projection)));
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
            }
            else
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
                if (DrawArgs.ImageLevel < this.m_level)
                    DrawArgs.ImageLevel = this.m_level;
            }

        }
        #endregion
    }
}

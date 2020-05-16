using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using MME.Globe.Core.Renderable;
using MME.Globe.Core.MathLib;
using Microsoft.DirectX.Direct3D;
using MME.Globe.Core.Camera;

namespace MME.Globe.Core.DQG
{
    /// <summary>
    /// DQG瓦片集类（RenderableObject类对象）
    /// </summary>
    public class DQGTileSet : RenderableObject
    {
        #region 公共成员
        /// <summary>
        /// 瓦片细分层次
        /// </summary>
        public static int BaseLevel = 4;
        /// <summary>
        /// 瓦片最大层次
        /// </summary>
        public static int LevelCount = 30;
        /// <summary>
        /// 瓦片绘制范围参数
        /// </summary>
        public static double TileDrawDistance = 10;
        /// <summary>
        /// 瓦片绘制范围参数
        /// </summary>
        public static double TileDrawSpread = 1;
        #endregion

        #region 私有成员
        private Hashtable m_topmostTiles = new Hashtable();
        private double LevelZeroTileSizeDegrees = 90;
        private DQGTriangleTile[] m_triTile;
        #endregion

        #region 构造方法
        /// <summary>
        /// 构造方法
        /// </summary>
        /// <param name="name"></param>
        public DQGTileSet(string name)
            : base(name)
        {
            m_triTile = new DQGTriangleTile[8];
            m_triTile[0] = new DQGTriangleTile("0", 0, 0, 90, 0, 90);
            m_triTile[1] = new DQGTriangleTile("1", 0, 0, 90, 90, 180);
            m_triTile[2] = new DQGTriangleTile("2", 0, 0, 90, -180, -90);
            m_triTile[3] = new DQGTriangleTile("3", 0, 0, 90, -90, 0);
            m_triTile[4] = new DQGTriangleTile("4", 0, -90, 0, 0, 90);
            m_triTile[5] = new DQGTriangleTile("5", 0, -90, 0, 90, 180);
            m_triTile[6] = new DQGTriangleTile("6", 0, -90, 0, -180, -90);
            m_triTile[7] = new DQGTriangleTile("7", 0, -90, 0, -90, 0);

        }
        #endregion

        #region 私有方法
        private void RemoveInvisibleTiles(CameraBase camera)
        {
            ArrayList deletionList = new ArrayList();
            if (m_topmostTiles.Count == 0)
                return;
            lock (m_topmostTiles.SyncRoot)
            {
                foreach (string key in m_topmostTiles.Keys)
                {
                    DQGTriangleTile qt = (DQGTriangleTile)m_topmostTiles[key];
                    if (!camera.ViewFrustum.Intersects(qt.BoundingBox))
                        deletionList.Add(key);
                }

                foreach (string deleteThis in deletionList)
                {
                    DQGTriangleTile qt = (DQGTriangleTile)m_topmostTiles[deleteThis];
                    if (qt != null)
                    {
                        m_topmostTiles.Remove(deleteThis);
                        qt.Dispose();
                    }
                }
            }
        }
        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化方法
        /// </summary>
        /// <param name="drawArgs"></param>
        public override void Initialize(DrawArgs drawArgs)
        {
            try
            {
                lock (m_topmostTiles.SyncRoot)
                {
                    foreach (DQGTriangleTile qt in m_topmostTiles.Values)
                        qt.Initialize(drawArgs);
                }
            }
            catch
            {
            }

            m_isInitialized = true;
        }
        /// <summary>
        /// 更新方法
        /// </summary>
        /// <param name="drawArgs"></param>
        public override void Update(DrawArgs drawArgs)
        {
            if (!m_isInitialized)
                Initialize(drawArgs);


            if (DrawArgs.Camera.ViewRange * 0.5f >
                    Angle.FromDegrees(TileDrawDistance * LevelZeroTileSizeDegrees))
            {
                lock (m_topmostTiles.SyncRoot)
                {
                    foreach (DQGTriangleTile qt in m_topmostTiles.Values)
                        qt.Dispose();
                    m_topmostTiles.Clear();
                }

                return;
            }

            RemoveInvisibleTiles(DrawArgs.Camera);
            try
            {
                for (int i = 0; i < m_triTile.Length; i++)
                {
                    DQGTriangleTile tile = m_triTile[i];
                    if (drawArgs.WorldCamera.ViewFrustum.Intersects(tile.BoundingBox))
                    {

                        lock (m_topmostTiles.SyncRoot)
                        {
                            if (!m_topmostTiles.Contains(tile.Address))
                                m_topmostTiles.Add(tile.Address, tile);
                        }
                        tile.Update(drawArgs);

                    }
                }
            }
            catch (System.Threading.ThreadAbortException)
            {
            }
            catch (Exception caught)
            {

            }
        }
        /// <summary>
        /// 渲染方法
        /// </summary>
        /// <param name="drawArgs"></param>
        public override void Render(DrawArgs drawArgs)
        {
            try
            {
                lock (m_topmostTiles.SyncRoot)
                {
                    if (m_topmostTiles.Count <= 0)
                    {
                        return;
                    }

                    Device device = DrawArgs.Device;
                    //device.Clear(ClearFlags.ZBuffer, 0, 1.0f, 0);
                    //device.RenderState.ZBufferEnable = true;
                    //device.VertexFormat = CustomVertex.PositionColoredTextured.Format;
                    //device.SetTextureStageState(0, TextureStageStates.ColorOperation, (int)TextureOperation.SelectArg1);
                    //device.SetTextureStageState(0, TextureStageStates.ColorArgument1, (int)TextureArgument.TextureColor);
                    //device.SetTextureStageState(0, TextureStageStates.AlphaArgument1, (int)TextureArgument.TextureColor);
                    //device.SetTextureStageState(0, TextureStageStates.AlphaOperation, (int)TextureOperation.SelectArg1);

                     ////Be prepared for multi-texturing
                    //device.SetTextureStageState(1, TextureStageStates.ColorArgument2, (int)TextureArgument.Current);
                    //device.SetTextureStageState(1, TextureStageStates.ColorArgument1, (int)TextureArgument.TextureColor);
                    //device.SetTextureStageState(1, TextureStageStates.TextureCoordinateIndex, 0);

                    //device.VertexFormat = CustomVertex.PositionColoredTextured.Format;
                    foreach (DQGTriangleTile qt in m_topmostTiles.Values)
                        qt.Render(drawArgs);
                    // Restore device states
                    //device.SetTextureStageState(1, TextureStageStates.TextureCoordinateIndex, 1);
                    if (m_renderPriority < RenderPriority.TerrainMappedImages)
                        device.RenderState.ZBufferEnable = true;

                }
            }
            catch
            {
            }
            finally
            {

            }
        }
        /// <summary>
        /// 释放内存方法
        /// </summary>
        public override void Dispose()
        {
            m_isInitialized = false;
            foreach (DQGTriangleTile qt in m_topmostTiles.Values)
                qt.Dispose();
        }

        #endregion
    }
}

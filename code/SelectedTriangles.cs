using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MME.Globe.Core.Renderable;
using Microsoft.DirectX.Direct3D;
using System.Drawing;
using MME.Globe.Core;
using Microsoft.DirectX;

namespace MME.Global.QTM
{
    public class SelectedTriangles : RenderableObject
    {
        private CustomVertex.PositionColored[] m_vertices;
        private int m_color;

        public int Color
        {
            get { return m_color; }
        }
        public List<Triangle> TriangleList=new List<Triangle>();
        public SelectedTriangles(string name,int color)
            : base(name)
        {
            m_color = color;            
        }
        public override void Update(DrawArgs drawArgs)
        {
            this.m_vertices = new CustomVertex.PositionColored[TriangleList.Count * 3];
            for (int i = 0; i < TriangleList.Count; i++)
            {
                m_vertices[i * 3 + 0].Position = TriangleList[i].Point1;
                m_vertices[i * 3 + 1].Position = TriangleList[i].Point2;
                m_vertices[i * 3 + 2].Position = TriangleList[i].Point3;
                m_vertices[i * 3 + 0].Color = m_color;
                m_vertices[i * 3 + 1].Color = m_color;
                m_vertices[i * 3 + 2].Color = m_color;                
            }
            this.m_isInitialized = true;
        }
        public override void Render(DrawArgs drawArgs)
        {
            if (m_vertices == null || !this.m_isInitialized || m_vertices.Length < 3)
                return;
            TextureOperation oriColorOperation = drawArgs.device.TextureState[0].ColorOperation;
            VertexFormats oriFormat = drawArgs.device.VertexFormat;
            bool oriLighting = drawArgs.device.RenderState.Lighting;
            Cull oriCullMode = drawArgs.device.RenderState.CullMode;
            Matrix oriWorldMatrix = drawArgs.device.Transform.World;
            FillMode oriFill = drawArgs.device.RenderState.FillMode;
            try
            {
                drawArgs.device.Transform.World = Matrix.Translation(-drawArgs.WorldCamera.ReferenceCenter.Vector3);
                drawArgs.device.RenderState.Lighting = false;
                drawArgs.device.RenderState.CullMode = Cull.None;
                drawArgs.device.VertexFormat = CustomVertex.PositionColored.Format;
                drawArgs.device.TextureState[0].ColorOperation = TextureOperation.Disable;
                drawArgs.device.RenderState.FillMode = FillMode.Solid;
                drawArgs.device.DrawUserPrimitives(PrimitiveType.TriangleList, m_vertices.Length / 3, m_vertices);
                drawArgs.device.RenderState.FillMode = FillMode.WireFrame;
                drawArgs.device.RenderState.Lighting = true;
                drawArgs.device.DrawUserPrimitives(PrimitiveType.TriangleList, m_vertices.Length / 3, m_vertices);
            }
            catch (System.Exception ex)
            {
            }
            finally
            {
                drawArgs.device.TextureState[0].ColorOperation = oriColorOperation;
                drawArgs.device.VertexFormat = oriFormat;
                drawArgs.device.RenderState.Lighting = oriLighting;
                drawArgs.device.RenderState.CullMode = oriCullMode;
                drawArgs.device.Transform.World = oriWorldMatrix;
                drawArgs.device.RenderState.FillMode = oriFill;
            }
        }
        public override void Dispose()
        {
            if (m_vertices != null)
            {
                m_vertices = null;
            }
            this.m_isInitialized = false;
        }
    }
}

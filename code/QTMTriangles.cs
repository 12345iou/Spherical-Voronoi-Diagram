using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MME.Globe.Core.Renderable;
using Microsoft.DirectX.Direct3D;
using Microsoft.DirectX;
using MME.Globe.Core;
using System.Collections;
using MME.Globe.Core.MathLib;
using System.IO;
using System.Drawing;
using MME.Globe.Core.DEM;

namespace MME.Global.QTM
{
    /// <summary>
    /// QTM三角形类，编码方法为顺序编码，剖分方式为经度等分，纬度等分，每个三角形用三个顶点，不用索引
    /// 该类用于显示基于统一分辨率的QTM格网生成的Voronoi图
    /// </summary>
    public class QTMTriangles : RenderableObject
    {
        private int m_level;
        public int Level
        {
            get { return m_level; }
        }
        /// <summary>
        /// 三角形
        /// </summary>
        private Triangle[] m_triangles;
        private Triangle[] m_triangles1;
        private int m_triCount = 0;
       // private int gridRowCount ;
        public static int zzdsl = 5000;
        private int[] zzd = new int[zzdsl];       //输入种子点函数  
        public Color[] color = new Color[zzdsl];//颜色
        ///

        private CustomVertex.PositionColored[] m_vertices;
        private List<Vector3> m_centerList;
        public List<Vector3> CenterList
        {
            get { return m_centerList; }
        }
        private List<Vector3> m_triVertexList;
        private float m_terrainExaggeration;

        public QTMTriangles(string name, int level, float terrainExaggeration)
            : base(name)
        {
            this.m_level = level;
            m_terrainExaggeration = terrainExaggeration;
            /////
            int triCount = 8 * (int)Math.Pow(4, level);
            this.m_triangles = new Triangle[triCount];
            this.m_triangles1 = new Triangle[triCount];
            int gridRowCount = (int)Math.Pow(2, level);//三角形的总行数

            /////

        }

        //比较三向扫描算法和确定归属算法
        public int bijiao()
        {
            double danweijuli = jisuanjuli(0, 1);
            int erroroccor = 0;
            for (int i = 0; i < m_triCount; i++)
            {
                for (int j = 0; j < zzdsl; j++)
                {
                    double ijjuli = jisuanjuli(i, zzd[j]);
                    if (m_triangles[i].juli > ijjuli)
                    {
                        double error = (m_triangles[i].juli - ijjuli) / danweijuli;
                       // if (error > 1)
                        {
                         //   Console.WriteLine("出错：");
                        //    Console.WriteLine(i);
                        //    Console.WriteLine(error);
                            erroroccor++;

                        }

    //                    m_triangles[i].zhongzidian = zzd[j];
     //                   m_triangles[i].juli = ijjuli;
                    }
                }
                
              
                 //if (error > 1)
                 //{
                 //    Console.WriteLine("出错：");
                 //    Console.WriteLine( i);
                 //    Console.WriteLine(error);

              
            }
            
            Console.WriteLine(danweijuli);
            return erroroccor;
          //  Console.WriteLine("算法结束");
            /*
            if (erroroccor > 0)
            {
                return 1;
            }
            else
            {
                return 0; 
            }
            */
        }

        //距离计算函数
        double jisuanjuli(int a, int b)
        {
            double j = 0;
            Vector3 vcener1 = m_triangles[a].Center;
            Vector3 vcener2 = m_triangles[b].Center;

            j = (vcener1.X - vcener2.X) * (vcener1.X - vcener2.X) + (vcener1.Y - vcener2.Y) * (vcener1.Y - vcener2.Y) + (vcener1.Z - vcener2.Z) * (vcener1.Z - vcener2.Z);

            return j;
           

        }



        /// 三向扫描算法

        #region  三向扫描算法
        public void sanxiangsaomiao()
        {
            int gridRowCount = (int)Math.Pow(2, m_level);//三角形的总行数

            #region //一个八分体正向扫描
        /*
               for (int i = 0; i < 256; i++)//正向扫描
            {
                List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, i);//临近搜索
                for (int j = 0; j < neighbours.Count; j++)
                {
                    if (m_triangles[neighbours[j]].zhongzidian == -1)
                    continue;
                    double ijjuli = jisuanjuli(i, m_triangles[ neighbours[j] ].zhongzidian);
                    if (m_triangles[i].juli > ijjuli)
                    {
                        m_triangles[i].zhongzidian = m_triangles[neighbours[j]].zhongzidian;
                        m_triangles[i].juli = ijjuli;
                    }
                }
            }   */
               #endregion
         /* 
            #region 正向扫描
            ///正向扫描北半球(n)
         
           for(int i=0;i<gridRowCount;i++)//第i行
             
           {
               int n = i*i; //三角形编码n
               
               for(int j=0;j<(8*i+5);j++)//第i行第j个
                
               {
                   
                   List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, n);//临近搜索
                   for (int m = 0; m < neighbours.Count; m++)
                   {
                       if (m_triangles[neighbours[m]].zhongzidian == -1)
                           continue;
                       double ijjuli = jisuanjuli(n, m_triangles[neighbours[m]].zhongzidian);
                       if (m_triangles[n].juli > ijjuli)
                       {
                           m_triangles[n].zhongzidian = m_triangles[neighbours[m]].zhongzidian;
                           m_triangles[n].juli = ijjuli;
                       }
                   }
                   //n变为其右侧格网
                   EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                   edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n);
                   n = edgeNeighbours.RightNeighbour;
               }
           }
          
              ///正向扫描南半球(ni)
           int gi = 0; int ni = 0;
           
            for (int i = 0; i < gridRowCount; i++)//第i行
           {

                ni = 5* (int)Math.Pow(4, m_level) - 1 - gi; //三角形编码n
              
              // for (int j = 0; j < 124; j++)//第i行第j个
                for (int j = 0; j < (8 * (gridRowCount-i) - 3); j++)//第i行第j个
               {

                   List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, ni);//临近搜索
                   for (int m = 0; m < neighbours.Count; m++)
                   {
                       if (m_triangles[neighbours[m]].zhongzidian == -1)
                           continue;
                       double ijjuli = jisuanjuli(ni, m_triangles[neighbours[m]].zhongzidian);
                       if (m_triangles[ni].juli > ijjuli)
                       {
                           m_triangles[ni].zhongzidian = m_triangles[neighbours[m]].zhongzidian;
                           m_triangles[ni].juli = ijjuli;
                       }
                   }
                   //n变为其左侧格网
                   EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                   edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, ni);
                   ni = edgeNeighbours.LeftNeighbour;
               }
             
               gi = gi + (gridRowCount - i)*2-1;
           } 
            #endregion
           
            #region 反向扫描 
            ///反向扫描南半球(n)
           

             for (int i = 0; i < gridRowCount; i++)//第i行
             {

                int n6 = 4 * (int)Math.Pow(4, m_level) + (i+1)*(i+1)-1; //三角形编码n

                 // for (int j = 0; j < 124; j++)//第i行第j个
                 for (int j = 0; j < (8 * i + 5); j++)//第i行第j个
                 {

                     List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, n6);//临近搜索
                     for (int m = 0; m < neighbours.Count; m++)
                     {
                         if (m_triangles[neighbours[m]].zhongzidian == -1)
                             continue;
                         double ijjuli = jisuanjuli(n6, m_triangles[neighbours[m]].zhongzidian);
                         if (m_triangles[n6].juli > ijjuli)
                         {
                             m_triangles[n6].zhongzidian = m_triangles[neighbours[m]].zhongzidian;
                             m_triangles[n6].juli = ijjuli;
                         }
                     }
                     //n变为右侧格网
                     EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                     edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n6);
                     n6 = edgeNeighbours.RightNeighbour;
                 }
                
                // g6 = g6 + (gridRowCount - i) * 2 - 1;
             }
            
             ///反向扫描北半球(ni)
            
             for (int i = 0; i < gridRowCount; i++)//第i行
             {
                 int n7 = (gridRowCount-i-1) * (gridRowCount-i-1); //三角形编码n

                 for (int j = 0; j < (8 * (gridRowCount - i) - 3); j++)//第i行第j个
                 {

                     List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, n7);//临近搜索
                     for (int m = 0; m < neighbours.Count; m++)
                     {
                         if (m_triangles[neighbours[m]].zhongzidian == -1)
                             continue;
                         double ijjuli = jisuanjuli(n7, m_triangles[neighbours[m]].zhongzidian);
                         if (m_triangles[n7].juli > ijjuli)
                         {
                             m_triangles[n7].zhongzidian = m_triangles[neighbours[m]].zhongzidian;
                             m_triangles[n7].juli = ijjuli;
                         }
                     }
                     //n变为其左侧格网
                     EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                     edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n7);
                     n7 = edgeNeighbours.RightNeighbour;
                 }
             }
             #endregion
   
        */
           
             #region 从左至右扫描（第二周期）
             ///扫描1半球(n2)
           
             for (int i = 0; i < gridRowCount; i++)//第i行
             {
              int n2 = (int)Math.Pow(4, m_level) - (int)Math.Pow(2, m_level) * 2 + 1+2*i; //第二周期扫描西半球起始格网
                 for (int j = 0; j < (8 * i + 5); j++)//第i行第j个
                 {

                     List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, n2);//临近搜索
                     for (int m = 0; m < neighbours.Count; m++)
                     {
                         if (m_triangles[neighbours[m]].zhongzidian == -1)
                             continue;
                         double ijjuli = jisuanjuli(n2, m_triangles[neighbours[m]].zhongzidian);
                         if (m_triangles[n2].juli > ijjuli)
                           {
                             m_triangles[n2].zhongzidian = m_triangles[neighbours[m]].zhongzidian;
                             m_triangles[n2].juli = ijjuli;
                         }
                     }
                     if (j % 2 == 0)
                     {
                         //n变为其左侧格网
                         EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                         edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n2);
                         n2 = edgeNeighbours.LeftNeighbour;
                     }
                     else
                     {
                         //n变为其上侧格网
                         EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                         edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n2);
                         n2 = edgeNeighbours.TopNeighbour;
                     }
                   
                 }

                
             }
        
               ///扫描东半球（第二周期）(n3)
          
             
 
             for (int i = 0; i < gridRowCount; i++)//第i行
             {
                 int n3 = (int)Math.Pow(4, m_level)*2 - (int)Math.Pow(2, m_level) * 2 + 1+2*i; //第二周期扫描东半球起始格网
                 for (int j = 0; j < (8 * (gridRowCount - i) - 4); j++)//第i行第j个
                 {

                     List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, n3);//临近搜索
                     for (int m = 0; m < neighbours.Count; m++)
                     {
                         if (m_triangles[neighbours[m]].zhongzidian == -1)
                             continue;
                         double ijjuli = jisuanjuli(n3, m_triangles[neighbours[m]].zhongzidian);
                         if (m_triangles[n3].juli > ijjuli)
                           {
                             m_triangles[n3].zhongzidian = m_triangles[neighbours[m]].zhongzidian;
                             m_triangles[n3].juli = ijjuli;
                         }
                     }
                     if (j % 2 == 0)
                     {
                         //n变为其右侧格网
                         EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                         edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n3);
                         n3 = edgeNeighbours.RightNeighbour;
                     }
                     else
                     {
                         //n变为其上侧格网
                         EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                         edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n3);
                         n3 = edgeNeighbours.TopNeighbour;
                     }
                   
                 }

                
             }
           
             #endregion
             
           #region 反向扫描（第二周期）
            ///扫描1半球(n2)
  
            

             for (int i = 0; i < gridRowCount; i++)//第i行
             {
                 int n8 = (int)Math.Pow(4, m_level) * 2 - 1-2*i; //起始格网
                 for (int j = 0; j < (8 * i + 5); j++)//第i行第j个
                 {

                     List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, n8);//临近搜索
                     for (int m = 0; m < neighbours.Count; m++)
                     {
                         if (m_triangles[neighbours[m]].zhongzidian == -1)
                             continue;
                         double ijjuli = jisuanjuli(n8, m_triangles[neighbours[m]].zhongzidian);
                         if (m_triangles[n8].juli > ijjuli)
                         {
                             m_triangles[n8].zhongzidian = m_triangles[neighbours[m]].zhongzidian;
                             m_triangles[n8].juli = ijjuli;
                         }
                     }
                     if (j % 2 == 0)
                     {
                         //n变为其上侧格网
                         EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                         edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n8);
                         n8 = edgeNeighbours.TopNeighbour;
                     }
                     else
                     {
                         //n变为其zuo侧格网
                         EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                         edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n8);
                         n8 = edgeNeighbours.LeftNeighbour;
                         
                     }

                 }

               
             }
            
            
           ///扫描2半球（第二周期）(n3)

        

           for (int i = 0; i < gridRowCount; i++)//第i行
           {
               int n9 = (int)Math.Pow(4, m_level) - 1-2*i; //第二周期扫描东半球起始格网
               for (int j = 0; j < (8 * (gridRowCount - i) - 3); j++)//第i行第j个
               {

                   List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, n9);//临近搜索
                   for (int m = 0; m < neighbours.Count; m++)
                   {
                       if (m_triangles[neighbours[m]].zhongzidian == -1)
                           continue;
                       double ijjuli = jisuanjuli(n9, m_triangles[neighbours[m]].zhongzidian);
                       if (m_triangles[n9].juli > ijjuli)
                       {
                           m_triangles[n9].zhongzidian = m_triangles[neighbours[m]].zhongzidian;
                           m_triangles[n9].juli = ijjuli;
                       }
                   }
                   if (j % 2 == 0)
                   {
                       //n变为其上侧格网
                       EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                       edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n9);
                       n9 = edgeNeighbours.TopNeighbour;
                        
                   }
                   else
                   {
                       //n变为其you侧格网
                       EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                       edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n9);
                       n9 = edgeNeighbours.RightNeighbour;
                   }

               }

            
           }
           
           #endregion
/*
        
           #region 从右至左扫描（第三周期）
           ///扫描东半球(n4)
           
    

           for (int i = 0; i < gridRowCount; i++)//第i行
           {
               int n4 = (int)Math.Pow(4, m_level) - 1-2*i; //第二周期扫描西半球起始格网
               for (int j = 0; j < (8 * i + 4); j++)//第i行第j个
               {

                   List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, n4);//临近搜索
                   for (int m = 0; m < neighbours.Count; m++)
                   {
                       if (m_triangles[neighbours[m]].zhongzidian == -1)
                           continue;
                       double ijjuli = jisuanjuli(n4, m_triangles[neighbours[m]].zhongzidian);
                       if (m_triangles[n4].juli > ijjuli)
                       {
                           m_triangles[n4].zhongzidian = m_triangles[neighbours[m]].zhongzidian;
                           m_triangles[n4].juli = ijjuli;
                       }
                   }
                   if (j % 2 == 0)
                   {
                       //n变为其上侧格网
                       EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                       edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n4);
                       n4 = edgeNeighbours.TopNeighbour;
 
                   }
                   else
                   {
                       //n变为其左侧格网
                       EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                       edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n4);
                       n4 = edgeNeighbours.LeftNeighbour;
                   }

               }

              
           }

           ///扫描西半球（第三周期）(n5)
            
        

           for (int i = 0; i < gridRowCount; i++)//第i行
           {
               int n5 = (int)Math.Pow(4, m_level) * 4 - 1-2*i; //第二周期扫描东半球起始格网
               for (int j = 0; j < (8 * (gridRowCount - i) - 4); j++)//第i行第j个
               {

                   List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, n5);//临近搜索
                   for (int m = 0; m < neighbours.Count; m++)
                   {
                       if (m_triangles[neighbours[m]].zhongzidian == -1)
                           continue;
                       double ijjuli = jisuanjuli(n5, m_triangles[neighbours[m]].zhongzidian);
                       if (m_triangles[n5].juli > ijjuli)
                       {
                           m_triangles[n5].zhongzidian = m_triangles[neighbours[m]].zhongzidian;
                           m_triangles[n5].juli = ijjuli;
                       }
                   }
                   if (j % 2 == 0)
                   {
                       //n变为其上侧格网
                       EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                       edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n5);
                       n5 = edgeNeighbours.TopNeighbour;
                   }
                   else
                   {
                       //n变为其右侧格网
                       EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                       edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n5);
                       n5 = edgeNeighbours.RightNeighbour;
                       
                   }

               }

               
           }
         
           #endregion
         
           #region 反向扫描（第三周期）
           ///扫描东半球(n4)
           


           for (int i = 0; i < gridRowCount; i++)//第i行
           {
               int n10 = 3 * (int)Math.Pow(4, m_level) - 1 - 2 * i; //起始格网
               for (int j = 0; j < (8 * i + 5); j++)//第i行第j个
               {

                   List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, n10);//临近搜索
                   for (int m = 0; m < neighbours.Count; m++)
                   {
                       if (m_triangles[neighbours[m]].zhongzidian == -1)
                           continue;
                       double ijjuli = jisuanjuli(n10, m_triangles[neighbours[m]].zhongzidian);
                       if (m_triangles[n10].juli > ijjuli)
                       {
                           m_triangles[n10].zhongzidian = m_triangles[neighbours[m]].zhongzidian;
                           m_triangles[n10].juli = ijjuli;
                       }
                   }
                   if (j % 2 == 0)
                   {
                       //n变为其上侧格网
                       EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                       edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n10);
                       n10 = edgeNeighbours.TopNeighbour;
 
                   }
                   else
                   {
                       //n变为其左侧格网
                       EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                       edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n10);
                       n10 = edgeNeighbours.LeftNeighbour;
                   }

               }

           }

           ///扫描西半球（第三周期）()
            


           for (int i = 0; i < gridRowCount; i++)//第i行
           {
               int n11 = (int)Math.Pow(4, m_level) * 2 - 1 - 2 * i; //起始格网
               for (int j = 0; j < (8 * (gridRowCount - i) - 3); j++)//第i行第j个
               {

                   List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, n11);//临近搜索
                   for (int m = 0; m < neighbours.Count; m++)
                   {
                       if (m_triangles[neighbours[m]].zhongzidian == -1)
                           continue;
                       double ijjuli = jisuanjuli(n11, m_triangles[neighbours[m]].zhongzidian);
                       if (m_triangles[n11].juli > ijjuli)
                       {
                           m_triangles[n11].zhongzidian = m_triangles[neighbours[m]].zhongzidian;
                           m_triangles[n11].juli = ijjuli;
                       }
                   }
                   if (j % 2 == 0)
                   {
                       //n变为其上侧格网
                       EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                       edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n11);
                       n11 = edgeNeighbours.TopNeighbour;
                   }
                   else
                   {
                       //n变为其右侧格网
                       EdgeNeighbours edgeNeighbours = new EdgeNeighbours();
                       edgeNeighbours = QTMEngine.EdgeNeighbourSearch(m_level, n11);
                       n11 = edgeNeighbours.RightNeighbour;
                       
                   }

               }

           
           }
        
           #endregion
          */   
          /////////////    
              
            /* 
           #region /*测试
           
           int n1 = (gridRowCount-1)*(gridRowCount-1); //三角形编码n
          
           List<int> neighbours1 = QTMEngine.AllNeighbourSearch(m_level, n1);//临近搜索
  
         //  for (int m = 2; m < 3; m++)
           for (int m = 0; m < neighbours1.Count; m++)
           {
               if (m_triangles[neighbours1[m]].zhongzidian == -1)
                   continue;
               double ijjuli = jisuanjuli(n1, m_triangles[neighbours1[m]].zhongzidian);
               if (m_triangles[n1].juli > ijjuli)
               {
                   m_triangles[n1].zhongzidian = m_triangles[neighbours1[m]].zhongzidian;
                   m_triangles[n1].juli = ijjuli;
               }
           }
           
           EdgeNeighbours edgeNeighbours1 = new EdgeNeighbours();
           edgeNeighbours1 = QTMEngine.EdgeNeighbourSearch(m_level, n1);
           n1 = edgeNeighbours1.LeftNeighbour;
            
           neighbours1 = QTMEngine.AllNeighbourSearch(m_level, n1);//临近搜索
           for (int m = 0; m < neighbours1.Count; m++)
           {
               if (m_triangles[neighbours1[m]].zhongzidian == -1)
                   continue;
               double ijjuli = jisuanjuli(n1, m_triangles[neighbours1[m]].zhongzidian);
               if (m_triangles[n1].juli > ijjuli)
               {
                   m_triangles[n1].zhongzidian = m_triangles[neighbours1[m]].zhongzidian;
                   m_triangles[n1].juli = ijjuli;
               }
           }
          
           #endregion
 */
        }
        #endregion
      
        /// 确定归属算法
        
        #region 确定归属算法
        public void quedingguishu()
        {
            

        

            for (int i = 0; i < m_triCount; i++)
            {


                for (int j = 0; j < zzdsl; j++)
                {
                    double ijjuli = jisuanjuli(i, zzd[j]);
                    if (m_triangles[i].juli > ijjuli)
                    {
                        m_triangles[i].zhongzidian = zzd[j];
                        m_triangles[i].juli = ijjuli;
                    }
                }

            }
        }
        #endregion




        public override void Initialize(DrawArgs drawArgs)
        {
            try
            {
                float radius = (float)World.Settings.WorldRadius;
                int gridRowCount = (int)Math.Pow(2, m_level);//三角形的总行数
                int pointRowCount = gridRowCount + 1;//点的总行数                
                int octaTriCount = (int)Math.Pow(4, m_level);
                int allTriCount = octaTriCount * 8;

                #region 计算 索引
                int[] startIndex = new int[pointRowCount];
                int[] endIndex = new int[pointRowCount];
                startIndex[0] = 0; endIndex[0] = 0;
                for (int i = 1; i < pointRowCount; i++)
                {
                    startIndex[i] = startIndex[i - 1] + i;
                    endIndex[i] = startIndex[i] + i;
                }
                List<int> indices = new List<int>();
                for (int i = 0; i < gridRowCount; i++)
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
                #endregion

                #region 计算顶点
                double lat, lon;
                #region 计算并保存北半球顶点，按八分体保存
                List<Vector3>[] tempNVector3s = new List<Vector3>[4];
                for (int i = 0; i < 4; i++)
                {
                    tempNVector3s[i] = new List<Vector3>();
                    tempNVector3s[i].Add(MathEngine.SphericalToCartesian(90.0, 45.0, radius));
                }
                for (int i = 1; i < pointRowCount; i++)
                {
                    int J = i + 1;
                    lat = 90.0 - (i * 90.0 / gridRowCount);
                    for (int j = 0; j < J; j++)
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            lon = j * 90.0 / (J - 1) + 90 * k;
                            float alt = TerrainProvider.GetElevationAt(lat, lon, 0)*m_terrainExaggeration;
                            Vector3 pos = MathEngine.SphericalToCartesian(lat, lon, radius+alt);
                            tempNVector3s[k].Add(pos);
                        }
                    }
                }
                #endregion

                #region 计算并保存南半球顶点，按八分体保存
                List<Vector3>[] tempSVector3s = new List<Vector3>[4];
                for (int i = 0; i < 4; i++)
                {
                    tempSVector3s[i] = new List<Vector3>();
                    tempSVector3s[i].Add(MathEngine.SphericalToCartesian(-90.0, 45.0, radius));
                }
                for (int i = 1; i < pointRowCount; i++)
                {
                    int J = i + 1;
                    lat = -90.0 + (i * 90.0 / gridRowCount);
                    for (int j = 0; j < J; j++)
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            lon = 90.0 - j * 90.0 / (J - 1) + 90 * k;
                            float alt = TerrainProvider.GetElevationAt(lat, lon, 0) * m_terrainExaggeration;
                            Vector3 pos = MathEngine.SphericalToCartesian(lat, lon, radius + alt);
                            tempSVector3s[k].Add(pos);
                        }
                    }
                }
                #endregion

                #region 保存三角形顶点坐标和中心点坐标，按八分体保存
                this.m_centerList = new List<Vector3>();
                this.m_triVertexList = new List<Vector3>();
                Vector3 v1, v2, v3;
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < indices.Count / 3; j++)
                    {
                        v1 = tempNVector3s[i][indices[j * 3 + 0]];
                        v2 = tempNVector3s[i][indices[j * 3 + 1]];
                        v3 = tempNVector3s[i][indices[j * 3 + 2]];
                        m_triVertexList.Add(v1);
                        m_triVertexList.Add(v2);
                        m_triVertexList.Add(v3);
                        m_centerList.Add(1.0f / 3.0f * (v1 + v2 + v3));


                        ///
                        string code="123";
                        Triangle tempTriangle = new Triangle(v1, v2, v3,code);
                        m_triangles[m_triCount] = tempTriangle;
                        m_triCount++;
                        ////



                    }
                }
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < indices.Count / 3; j++)
                    {
                        v1 = tempSVector3s[i][indices[j * 3 + 0]];
                        v2 = tempSVector3s[i][indices[j * 3 + 1]];
                        v3 = tempSVector3s[i][indices[j * 3 + 2]];
                        m_triVertexList.Add(v1);
                        m_triVertexList.Add(v2);
                        m_triVertexList.Add(v3);
                        m_centerList.Add(1.0f / 3.0f * (v1 + v2 + v3));

                        ///
                        string code = "123";
                        Triangle tempTriangle = new Triangle(v1, v2, v3, code);
                        m_triangles[m_triCount] = tempTriangle;
                        m_triCount++;
                        ////


                    }
                }
                #endregion
                #endregion
                this.m_vertices = new CustomVertex.PositionColored[m_triVertexList.Count];   
             
                ///

                //初始化种子点
        
             
         

                Random rnd = new Random();
             //   int rndint = rnd.Next(0, m_triCount);
                for (int i = 0; i < QTMTriangles.zzdsl; i++)
                {

  ////////         
                    zzd[i] = rnd.Next(0, m_triCount);
                  //  zzd[0] = 100;
                  //  zzd[1] = 200;
                  //  zzd[2] = 300;
                   // zzd[i] = rndint + i * 10;
/////////                    
                    m_triangles[zzd[i]].zhongzidian = zzd[i];
                    m_triangles[zzd[i]].juli = 0;
                    int red = rnd.Next(0, 255);
                    int green = rnd.Next(0, 255);
                    int blue = rnd.Next(0, 255);
                    color[i] = Color.FromArgb(255, red, green, blue);
                }
 /// //              //
//////////
                DateTime start = DateTime.Now;
                int errornumber = 0;
   //             for (int ii = 0; ii < 50; ii++)
                {
                    
                   // sanxiangsaomiao();
                      quedingguishu();
                    
                   // errornumber += bijiao();
                }
/////
/////////
                DateTime end = DateTime.Now;
                TimeSpan span = end - start;
                double seconds = span.TotalSeconds;
                Console.WriteLine("算法出错次数：");
                Console.WriteLine(errornumber);
                Console.WriteLine("算法所需时间：");
                Console.WriteLine(seconds);
                Console.Read();
                ///
////

                
                # region 给不同归属格网上色
                
                for (int i = 0; i < m_vertices.Length; i++)
                {
                    this.m_vertices[i].Position = m_triVertexList[i];
                    this.m_vertices[i].Color = Color.Pink.ToArgb();//
                    
                    int t = i / 3;
                    for (int j = 0; j < QTMTriangles.zzdsl; j++)
                    {
                        if (m_triangles[t].zhongzidian == zzd[j])
                        {

                            this.m_vertices[t * 3].Color = color[j].ToArgb();//

                            this.m_vertices[t * 3 + 1].Color = color[j].ToArgb();//

                            this.m_vertices[t * 3 + 2].Color = color[j].ToArgb();//
                        }
                    }
                     
                    
                }
                #endregion



                
                #region /*测试

                int n1 = 0; //三角形编码n

                this.m_vertices[n1 * 3].Color = Color.Black.ToArgb();//顶点颜色函数

                this.m_vertices[n1 * 3 + 1].Color = Color.Black.ToArgb();//顶点颜色函数

                this.m_vertices[n1 * 3 + 2].Color = Color.Black.ToArgb();//顶点颜色函数

                int n2 =  (int)Math.Pow(4, m_level)-1; //三角形编码n

                this.m_vertices[n2 * 3].Color = Color.Black.ToArgb();//顶点颜色函数

                this.m_vertices[n2 * 3 + 1].Color = Color.Black.ToArgb();//顶点颜色函数

                this.m_vertices[n2 * 3 + 2].Color = Color.Black.ToArgb();//顶点颜色函数

                int n3 = (int)Math.Pow(4, m_level)  - (int)Math.Pow(2, m_level) * 2+1; //三角形编码n

                this.m_vertices[n3 * 3].Color = Color.Black.ToArgb();//顶点颜色函数

                this.m_vertices[n3 * 3 + 1].Color = Color.Black.ToArgb();//顶点颜色函数

                this.m_vertices[n3 * 3 + 2].Color = Color.Black.ToArgb();//顶点颜色函数
 /*               EdgeNeighbours edgeNeighbours1 = new EdgeNeighbours();
              
                edgeNeighbours1 = QTMEngine.EdgeNeighbourSearch(m_level, n1);
                 n1 = edgeNeighbours1.LeftNeighbour;
                 this.m_vertices[n1 * 3].Color = Color.Yellow.ToArgb();//顶点颜色函数

                 this.m_vertices[n1 * 3 + 1].Color = Color.Yellow.ToArgb();//顶点颜色函数

                 this.m_vertices[n1 * 3 + 2].Color = Color.Yellow.ToArgb();//顶点颜色函数
                EdgeNeighbours rightTriNeighbour = new EdgeNeighbours();
                rightTriNeighbour = QTMEngine.EdgeNeighbourSearch(m_level, n1);
  
                n1 = rightTriNeighbour.TopNeighbour;
                int n1 = 4 * (int)Math.Pow(4, m_level) + 8;
                this.m_vertices[n1 * 3].Color = Color.Yellow.ToArgb();//顶点颜色函数

                this.m_vertices[n1 * 3 + 1].Color = Color.Yellow.ToArgb();//顶点颜色函数

                this.m_vertices[n1 * 3 + 2].Color = Color.Yellow.ToArgb();//顶点颜色函数
                
                List<int> neighbours = QTMEngine.AllNeighbourSearch(m_level, n1);//临近搜索

               // for (int j = 0; j < 5; j++)
                for (int j = 0; j < neighbours.Count; j++)
                {

                   
                    this.m_vertices[neighbours[j] * 3].Color = Color.Yellow.ToArgb();//顶点颜色函数

                    this.m_vertices[neighbours[j] * 3 + 1].Color = Color.Yellow.ToArgb();//顶点颜色函数

                    this.m_vertices[neighbours[j] * 3 + 2].Color = Color.Yellow.ToArgb();//顶点颜色函数
                }

               */

                ///
                #endregion

                #region 种子点为白色

                /*
                for (int i = 0; i < QTMTriangles.zzdsl; i++)
                {
                    this.m_vertices[zzd[i] * 3].Color = Color.White.ToArgb();//顶点颜色函数

                    this.m_vertices[zzd[i] * 3 + 1].Color = Color.White.ToArgb();//顶点颜色函数

                    this.m_vertices[zzd[i] * 3 + 2].Color = Color.White.ToArgb();//顶点颜色函数

                }


                this.m_vertices[0].Color = Color.Yellow.ToArgb();//顶点颜色函数

                this.m_vertices[1].Color = Color.Yellow.ToArgb();//顶点颜色函数

                this.m_vertices[2].Color = Color.Yellow.ToArgb();//顶点颜色函数 
*/
               
                #endregion

                this.m_isInitialized = true;
            }
            catch (System.Exception ex)
            {
                this.m_isInitialized = false;
                throw new Exception(ex.ToString());
            }
        
        }

        public override void Update(DrawArgs drawArgs)
        {
            if (this.m_isInitialized)
                return;
            this.Initialize(drawArgs);
        }

        public override void Render(DrawArgs drawArgs)
        {
            if (!this.m_isInitialized)
                return;
            VertexFormats oriFormat = drawArgs.device.VertexFormat;
            bool oriLighting = drawArgs.device.RenderState.Lighting;
            FillMode oriFillMode = drawArgs.device.RenderState.FillMode;
            Cull oriCullMode = drawArgs.device.RenderState.CullMode;
            try
            {
                drawArgs.device.Clear(ClearFlags.Target, Color.White, 0, 1);
                drawArgs.device.VertexFormat = CustomVertex.PositionColored.Format;
                drawArgs.device.RenderState.Lighting = false;//灯光关闭
                drawArgs.device.RenderState.FillMode = FillMode.Solid;

               // drawArgs.device.RenderState.CullMode = Cull.CounterClockwise;
                drawArgs.device.RenderState.CullMode = Cull.None;
                drawArgs.device.TextureState[0].ColorOperation = TextureOperation.Disable;
                drawArgs.device.SetTransform(TransformType.World, Matrix.Translation(-drawArgs.WorldCamera.ReferenceCenter.Vector3));
                drawArgs.device.DrawUserPrimitives(PrimitiveType.TriangleList, m_vertices.Length / 3, m_vertices);//绘制三角形
             
                //drawArgs.device.RenderState.Lighting = true;
               // drawArgs.device.RenderState.FillMode = FillMode.WireFrame;
                //drawArgs.device.DrawUserPrimitives(PrimitiveType.TriangleList, m_vertices.Length / 3, m_vertices);
            }
            catch (System.Exception ex)
            {
                throw new Exception(ex.ToString());
            }
            finally
            {
                drawArgs.device.VertexFormat = oriFormat;
                drawArgs.device.RenderState.Lighting = oriLighting;
                drawArgs.device.RenderState.FillMode = oriFillMode;
                drawArgs.device.RenderState.CullMode = oriCullMode;
            }
        }

        public override void Dispose()
        {
            this.m_isInitialized = false;
            if (this.m_vertices != null)
            {
                this.m_vertices = null;
            }
        }

       
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MME.Globe.Core;

namespace MME.Global.QTM
{
    public class SelectedTriangles_Object : SelectedTriangles
    {
        public SelectedTriangles_Object(string name, int color)
            : base(name, color)
        {

        }
        public override void Update(DrawArgs drawArgs)
        {
            base.Update(drawArgs);
        }
        public override void Render(DrawArgs drawArgs)
        {
            base.Render(drawArgs);
        }
        public override void Dispose()
        {
            base.Dispose();
        }

    }
}

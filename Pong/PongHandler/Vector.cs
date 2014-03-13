using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Pong.PongHandler
{
    /// <summary>
    /// Helper vector class
    /// </summary>
    public class Vector
    {
        public static readonly Vector  NW = new Vector(-Math.Sqrt(2), -Math.Sqrt(2));
        public static readonly Vector SW = new Vector(-Math.Sqrt(2), Math.Sqrt(2));
        public static readonly Vector NE = new Vector(Math.Sqrt(2), -Math.Sqrt(2));
        public static readonly Vector SE = new Vector(Math.Sqrt(2), Math.Sqrt(2));
        public static readonly Vector[] Directions = new Vector[] { NW, SW, NE, SE };
        
        public double X { get; set; }
        public double Y { get; set; }

        public Vector(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }
        
        public static Vector operator +(Vector a, Vector b)
        { 
            return new Vector(a.X + b.X, a.Y + b.Y);
        }

        public static Vector operator *(Vector a, double num)
        {
            return new Vector(a.X * num, a.Y * num);
        }

        public Vector MirrorX()
        {
            return new Vector(-X, Y);
        }

        public Vector MirrorY()
        {
            return new Vector(X, -Y);
        }
    }
}
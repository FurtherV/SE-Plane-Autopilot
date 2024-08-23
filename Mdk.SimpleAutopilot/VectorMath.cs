using System;
using VRageMath;

namespace IngameScript
{
    public static class VectorMath
    {
        /// <summary>
        /// Computes cosine of the angle between 2 vectors.
        /// </summary>
        public static double CosBetween(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);
        }

        /// <summary>
        /// Computes angle between 2 vectors in radians.
        /// </summary>
        public static double AngleBetween(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return Math.Acos(CosBetween(a, b));
        }

        /// <summary>
        /// Projects vector a onto vector b
        /// </summary>
        public static Vector3D Projection(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return Vector3D.Zero;

            if (Vector3D.IsUnit(ref b))
                return a.Dot(b) * b;

            return a.Dot(b) / b.LengthSquared() * b;
        }

        /// <summary>
        /// Rejects vector a on vector b
        /// </summary>
        public static Vector3D Rejection(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return Vector3D.Zero;

            return a - a.Dot(b) / b.LengthSquared() * b;
        }

        /// <summary>
        /// Scalar projection of a onto b
        /// </summary>
        public static double ScalarProjection(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;

            if (Vector3D.IsUnit(ref b))
                return a.Dot(b);

            return a.Dot(b) / b.Length();
        }
    }
}

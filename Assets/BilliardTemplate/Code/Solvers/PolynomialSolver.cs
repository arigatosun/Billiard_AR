/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using System;
using Unity.Mathematics;

namespace ibc.solvers
{

    public struct Interval
    {
        public readonly double Low, High;

        public Interval(double low, double high)
        {
            Low = low;
            High = high;
        }

        public static Interval Zero => new Interval(0, 0);
    }

    public struct Poly2
    {
        public readonly double c2, c1, c0;

        public Poly2(double c2, double c1, double c0)
        {
            this.c2 = c2;
            this.c1 = c1;
            this.c0 = c0;
        }

        public readonly double Evaluate(double x)
        {
            return (c2 * x + c1) * x + c0;
        }

        public readonly bool SmallestPositiveRoot(double upperBound, out double root)
        {
            root = 0;

            if (math.abs(c2) <= math.EPSILON_DBL)
            {
                if (math.abs(c1) <= math.EPSILON_DBL) return false; //no solution

                root = -c0 / c1;
                return true;
            }

            double D = c1 * c1 - 4 * c2 * c0;
            if (D < 0) return false;
            double r1 = -(c1 + math.sign(c1) * math.sqrt(D)) / (2 * c2);
            double r2 = c0 / (c2 * r1);
            if (r1 * r2 > 0 && r1 < 0) //both roots are negative
                root = math.max(r1, r2);
            else if (r1 * r2 > 0) //both roots are positive
                root = math.min(r1, r2);
            else root = math.max(r1, r2); //one root is negative and one is positive
            return true;
        }
        public readonly bool LargestPositiveRoot(out double root)
        {
            root = 0;

            if (math.abs(c2) <= math.EPSILON_DBL)
            {
                if (math.abs(c1) <= math.EPSILON_DBL) return false;

                root = -c0 / c1;
                return true;
            }

            double D = c1 * c1 - 4 * c2 * c0;
            if (D < 0) return false;
            double r1 = -(c1 + math.sign(c1) * math.sqrt(D)) / (2 * c2);
            double r2 = c0 / (c2 * r1);
            if (r1 * r2 > 0 && r1 < 0) //both roots are negative
                root = math.max(r1, r2);
            else if (r1 * r2 > 0) //both roots are positive
                root = math.max(r1, r2);
            else root = math.max(r1, r2); //one root is negative and one is positive
            return true;
        }
    }

    public struct Poly4
    {
        public readonly double c4, c3, c2, c1, c0;

        public Poly4(double c4, double c3, double c2, double c1, double c0)
        {
            this.c4 = c4;
            this.c3 = c3;
            this.c2 = c2;
            this.c1 = c1;
            this.c0 = c0;
        }

        public readonly double Evaluate(double x)
        {
            return (((c4 * x + c3) * x + c2) * x + c1) * x + c0;
        }

        public bool SmallestPositiveRoot(double upperBound, double errorBound, out double root)
        {
            if (math.abs(c4) <= math.EPSILON_DBL && math.abs(c3) <= math.EPSILON_DBL)
                return new Poly2(c2, c1, c0).SmallestPositiveRoot(0, out root);
            return Poly4Solver.SmallestRealRoot(upperBound, this, out root, errorBound);
        }
    }

    public struct Poly4Solver
    {
        private static int CheckInterval(double a, double b, in Poly4 poly)
        {
            double a2 = a * a;
            double a3 = a * a2;
            double a4 = a * a3;

            double b2 = b * b;
            double b3 = b * b2;
            double b4 = b * b3;

            double s4 = poly.c4 * a4 + poly.c3 * a3 + poly.c2 * a2 + poly.c1 * a + poly.c0;
            double s3 = 4 * poly.c0 + 3 * a * poly.c1 + b * poly.c1 + 2 * a2 * poly.c2 + a3 * poly.c3 +
                        2 * a * b * poly.c2 +
                        3 * a2 * b * poly.c3 +
                        4 * a3 * b * poly.c4;
            double s2 = 6 * poly.c4 * a2 * b2 + 3 * poly.c3 * a2 * b + poly.c2 * a2 + 3 * poly.c3 * a * b2 +
                        4 * poly.c2 * a * b +
                        3 * poly.c1 * a +
                        poly.c2 * b2 + 3 * poly.c1 * b + 6 * poly.c0;
            double s1 = 4 * poly.c0 + a * poly.c1 + 3 * b * poly.c1 + 2 * b2 * poly.c2 + b3 * poly.c3 +
                        2 * a * b * poly.c2 +
                        3 * a * b2 * poly.c3 +
                        4 * a * b3 * poly.c4;
            double s0 = (poly.c4 * b4 + poly.c3 * b3 + poly.c2 * b2 + poly.c1 * b + poly.c0);

            int v = 0;
            if (s0 > 0 != s1 > 0) v++;
            if (s1 > 0 != s2 > 0) v++;
            if (s2 > 0 != s3 > 0) v++;
            if (s3 > 0 != s4 > 0) v++;
            return v;
        }

        private static Interval Subdivide(double hi, double lo, in Poly4 poly, out int v)
        {
            v = CheckInterval(lo, hi, in poly);
            if (v == 1) return new Interval(lo, hi);
            if (v == 0) return Interval.Zero;
            Interval i1 = Subdivide((hi + lo) / 2.0, lo, in poly, out var v1);
            if (v1 == 1) { v = v1; return i1; }
            Interval i2 = Subdivide(hi, (hi + lo) / 2, in poly, out var v2);
            if (v2 == 1) { v = v2; return i2; }
            v = 0;
            return Interval.Zero;
        }

        private static double Bisect(double hi, double lo, int signHi, int signLow, in Poly4 poly, double errorBound)
        {
            double mid = (hi + lo) * 0.5;
            if (math.abs(hi - lo) * 0.5 < errorBound) return mid;
            double midValue = poly.Evaluate(mid);
            if (math.abs(midValue) < math.EPSILON_DBL) return mid;
            int midSign = (int)math.sign(midValue);
            if (signHi != midSign) return Bisect(hi, mid, signHi, midSign, poly, errorBound);
            if (signLow != midSign) return Bisect(mid, lo, midSign, signLow, poly, errorBound);
            return midValue;
        }

        public static bool SmallestRealRoot(double hi, Poly4 poly, out double root, double errorBound = 1E-5)
        {
            root = 0;
            if (hi <= 0)
                return false;
            Interval i = Subdivide(hi, 0, in poly, out var v);
            if (v == 0) return false;
            var signHi = (int)math.sign(poly.Evaluate(i.High));
            var signLow = (int)math.sign(poly.Evaluate(i.Low));
            root = Bisect(i.High, i.Low, signHi, signLow, in poly, errorBound);
            return true;
        }
    }
}
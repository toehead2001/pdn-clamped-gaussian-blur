namespace DistanceTransformation
{
    // DistanceTransform by MJW.
    // Based on an algorithm by A. Meijster, et al., and later by
    // Pedro F. Felzenszwalb and Daniel P. Huttenlocher.
    // This version combines elements from both papers along with some modifications.
    // 
    // Suppose we have an image consisting of two disjoint subsets: "set" pixels and "not set"
    // pixels. For each not-set pixel, find the nearest set pixel, or the squared Euclidean
    // distance to the nearest set pixel.
    //
    // This problem can be solved using an O(n) algorithm which relies on the following facts:
    //
    // First, if (X, Y) is a pixel, and (Xs, Ys) is the nearest set pixel, there is no
    // pixel, (Xs, Ys'), such that |Ys' - Y| < |Ys - Y|; in other words, for the pixels within
    // a given row, it is only necessary to check the set of pixels which, for some X, minimizes
    // the Y distance. This set of pixels -- one for each X -- will be referred to as the
    // "candidate" pixels. (For a given X, there may be two pixels which are the same mininmal Y
    // distance. Since these two pixels are the same distance from every pixel in the row, either
    // can be used.)
    //
    // Second, the set of pixels in a row that are nearest to a given candidate pixel is
    // contiguous. This set will be referred to as the candidate's "span."
    //
    // Third, on any given row, the spans for two differnt candidate pixels occur in the same
    // X order as the pixels; that is, if S the span for P and S' is the span for P', and P is
    // left of P', then S is left of S'. (A pixel may be the same distance from two candidates;
    // in that case, it can either be the rightmost pixel of the left candidate's span, or the
    // leftmost pixel of the right candidate's span.)
    //
    // The first fact follows from observing that if (Xs, Ys) is the nearest pixel to (X, Y),
    // there can be no pixel (X, Ys') where |Ys' - Y| < |Ys - Y|, since such a pixel would be
    // nearer to (X, Y).
    //
    // The second and third facts follow from observing that if (Xs, Ys) and (Xs', Ys') are two
    // candidates, Xs < Xs', then there is some (real-valued) point on the Y row which is
    // equidistance from the two points. All the pixels on the row to the left are nearer to
    // (Xs, Ys) and all the pixels to the right are nearer to (Xs', Ys'). The spans for each
    // candidate must lie on the respective side of the equidistance point. 


    //----------------------------------------------------------------------------------------
    // This class determines the nearest pixel rather than the distance to the nearest pixel.
    // The simplest use is to create a new instance specifying the size, then call Transform
    // with a callback delegate that determines which pixels belong in the set to be measured
    // from. The class can then be indexed like a two-dimensional to get the coordinates of
    // the nearest pixels.
    //
    // Note: This class and the distance class share many methods which could be implemented
    // in an intermediate class which they both could be derived from. At least for now, I've
    // chosen not to do that. I'm not sure the distance class is that useful. It's less
    // flexible, and I'm not sure it's much faster. If I decide to eliminate it, the
    // intermediate class would just add complexity.
    class NearestPixelTransform : Table2D<PointInt16>
    {
        public const int MaxValue = 32767;
        const int MaxValueSquared = MaxValue * MaxValue;

        public NearestPixelTransform(int left, int top, int width, int height) :
            base(left, top, width, height)
        {
        }

        public bool Transform()
        {
            TransformColumns();
            return TransformRows();
        }

        public void TransformColumns()
        {
            TransformColumns(Left, Right);
        }

        public void TransformColumns(int left, int right)
        {
            // The array pixels are initially set to their own coordinates if set, and other
            // coordinates if not set.
            // Replace each element with the distance to the nearest element in the same column
            // that's within the set.
            for (int x = left; x < right; x++)
            {
                // Set the elements to the value to the nearest set-member in the same column.
                short nearestY = -MaxValue;
                for (int y = Top; y < Bottom; y++)
                {
                    if ((this[x, y].X == x) && (this[x, y].Y == y))
                    {
                        nearestY = (short)y;

                        // Check previous pixels to see if this Y is nearer.
                        int by = y;
                        while ((--by >= Top) && (nearestY - by < by - this[x, by].Y))
                            this[x, by] = new PointInt16(x, nearestY);
                    }
                    else
                    {
                        this[x, y] = new PointInt16(x, nearestY);
                    }
                }
            }
        }

        struct SpanXY
        {
            public PointInt16 NearestPixel;    // Nearest pixel associated with span.
            public int SpanX;                  // Beginning of span.

            public SpanXY(PointInt16 nearestPixel, int spanX)
            {
                NearestPixel = nearestPixel;
                SpanX = spanX;
            }
        }

        public bool TransformRows()
        {
            return TransformRows(Top, Bottom);
        }

        public bool TransformRows(int top, int bottom)
        {
            SpanXY[] span = new SpanXY[Width + 1];

            // Each entry in the image contains the nearest set pixel in the same column. These are the
            // candidate pixels.
            for (int y = top; y < bottom; y++)
            {
                // Taking each candidate pixel one at a time from left to right, determine its span. Each new
                // candidate pixel will be nearest to the pixels in the row from some point onward. Since the
                // spans are in the same X order as the candidate pixels, the previous spans are checked from
                // right to left. If the new candidate is closer than a previous candidate for that pixel's
                // entire span, the previous candidate's span is eliminated. Once a candidate is found that's
                // closer at the beginning of its span than the new candidate, the point at which the new
                // candidate becomes closer is appended to the span list as the end of the previous span and
                // the beginning of the new span.
                int spanIndex = 0;
                span[0] = new SpanXY(this[Left, y], Left);
                for (int x = Left + 1; x < Right; x++)
                {
                    int dist2 = Sq(x) + Sq(this[x, y].Y - y);

                    // If the distance to beyond the maximum image distance, don't bother checking further,
                    // since it can't produce a valid span. (This test isn't necessary to the algorithm.)
                    if (dist2 >= MaxValueSquared)
                        continue;

                    // Loop until the previous span can't be eliminated or there are no previous spans.
                    while (true)
                    {
                        // Compare the distance of the new candidate at the beginning of the previous span
                        // to the distance of its associated candidate.
                        SpanXY prevSpan = span[spanIndex];
                        int prevPixelX = prevSpan.NearestPixel.X;
                        int dist2Diff = dist2 - Sq(prevPixelX) - Sq(y - prevSpan.NearestPixel.Y);
                        int twiceXDiff = 2 * (x - prevPixelX);
                        if (dist2Diff > twiceXDiff * prevSpan.SpanX)
                        {
                            // The span for the new candidate starts after the previous span.
                            // Add a new span to the list.
                            int spanX = (dist2Diff + twiceXDiff - 1) / twiceXDiff; // Ceiling integer division.
                            if (spanX < Right)
                                span[++spanIndex] = new SpanXY(this[x, y], spanX);
                            break;
                        }
                        else if (--spanIndex < 0)
                        {
                            // No more previous spans, so start over.
                            spanIndex = 0;
                            span[0] = new SpanXY(this[x, y], Left);
                            break;
                        }
                    }
                }

                // The spans have been determined. Move through the list of spans, filling in nearest pixel for each pixel.
                span[spanIndex + 1].SpanX = Right; // Avoid extra test for end-of-list.
                int endX = Left;
                for (int i = 0; i <= spanIndex; i++)
                {
                    int startX = endX;
                    endX = span[i + 1].SpanX;
                    PointInt16 nearestPixel = span[i].NearestPixel;
                    for (int x = startX; x < endX; x++)
                        this[x, y] = nearestPixel;
                }
            }

            // Return a flag which is false if there were no set pixels in the original image.
            // If there weren't, the nearest-pixel coordinates will be invalid and outside the image range.
            return (this[Left, top].Y >= 0);
        }

        static int Sq(int x)
        {
            return x * x;
        }

        public delegate bool IsInSetCallback(int x, int y);

        public void Include(IsInSetCallback isIncluded)
        {
            Include(Left, Top, Width, Height, isIncluded);
        }

        public void Include(int left, int top, int width, int height, IsInSetCallback isIncluded)
        {
            int right = left + width, bottom = top + height;
            for (int y = top; y < bottom; y++)
                for (int x = left; x < right; x++)
                    this[x, y] = isIncluded(x, y) ? new PointInt16(x, y) : PointInt16.MaxValue;
        }
    }

    public struct PointInt16
    {
        public short x, y;

        public PointInt16(int x, int y)
        {
            this.x = (short)x; this.y = (short)y;
        }

        public short X
        {
            get { return x; }
            set { x = value; }
        }

        public short Y
        {
            get { return y; }
            set { y = value; }
        }

        public override bool Equals(object obj)
        {
            return (obj is PointInt16) && (this == (PointInt16)obj);
        }
        public override int GetHashCode()
        {
            return (0xffff & x) | (y << 16);
        }
        public static bool operator ==(PointInt16 p0, PointInt16 p1)
        {
            return (p0.x == p1.x) && (p0.y == p1.y);
        }
        public static bool operator !=(PointInt16 p0, PointInt16 p1)
        {
            return !(p0 == p1);
        }
        public static implicit operator System.Drawing.Point(PointInt16 p)
        {
            return new System.Drawing.Point(p.X, p.Y);
        }
        public static explicit operator PointInt16(System.Drawing.Point p)
        {
            return new PointInt16(p.X, p.Y);
        }

        public static readonly PointInt16 MaxValue = new PointInt16(32767, 32767);
    }

    // Addressed like an array, but the starting points can be nonzero.
    // The indices aren't range checked.
    public class Table2D<ElementType>
    {
        int left, top, width, height;
        int right, bottom;
        int size, arraySize;
        int offset;

        public ElementType[] Array;

        public Table2D(int left, int top, int width, int height)
        {
            Resize(left, top, width, height);
        }

        public void Resize(int left, int top, int width, int height, bool reallocate = false)
        {
            this.left = left; this.top = top; this.width = width; this.height = height;
            right = left + width; bottom = top + height;

            size = width * height;
            offset = left + width * top;
            if (reallocate || (Array == null) || (arraySize < size))
                CreateArray(size);
        }

        void CreateArray(int arraySize)
        {
            this.arraySize = arraySize;
            Array = new ElementType[arraySize];
        }

        public ElementType this[int x, int y]
        {
            get { return Array[x + y * width - offset]; }
            set { Array[x + y * width - offset] = value; }
        }

        public int Left { get { return left;  } }
        public int Top { get { return top; } }
        public int Width { get { return width; } }
        public int Height { get { return height; } }
        public int Right { get { return right; } }
        public int Bottom { get { return bottom; } }
    }
}

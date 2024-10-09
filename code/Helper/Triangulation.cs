using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace Saandy {

    public class Triangulation {

        public class Polygon {
            public List<Math2d.Line> lineSegments;
            public PointCollection contour;

            public Polygon() {
                lineSegments = new List<Math2d.Line>();
                contour = new PointCollection();
            }

            public void Add(Vector2 a, Vector2 b) { Add(new Math2d.Line(a, b)); }
            public void Add(Math2d.Line line) { lineSegments.Add(line); contour.Add(line.pointB); }

            public void RemoveAt(int index) {
                lineSegments.RemoveAt(index);
            }

            public void Clear() { lineSegments.Clear(); contour.points.Clear(); }

            public void AddLineSegment(Math2d.Line line) {
                lineSegments.Add(line);
            }

            public void Draw(Color color, float duration) {
                int i = lineSegments.Count;
                foreach (Math2d.Line l in lineSegments) {
                    float v = (float)i / lineSegments.Count;
                    i--;

                    l.Draw(color, duration);
                }
            }

            public void GenerateFromOrderedPoints(PointCollection points) {
                Clear();
                for (int i = 1; i < points.Count; i++) { Add(new Math2d.Line(points.points[i - 1], points.points[i])); }
                Add(new Math2d.Line(points.points[points.Count - 1], points.points[0]));
            }

            public bool Contains(Math2d.Line line) { return Contains(line, out int index); }

            public bool Contains(Math2d.Line line, out int index) {
                index = -1;

                for (int i = 0; i < lineSegments.Count; i++) {
                    if (lineSegments[i].Equals(line)) {
                        index = i;
                        return true;
                    }
                }

                return false;
            }

        }

        public class PointCollection {
            public List<Vector2> points;

            public int Count => points.Count;

            public PointCollection() {
                points = new List<Vector2>();
            }

            public void Add(Vector2 point) {
                points.Add(point);
            }

            public void BevelPoint(int index) {
                int b = Math2d.ClampListIndex(index - 1, Count);
                int c = Math2d.ClampListIndex(index + 1, Count);

                Vector2 ab = points[b] - points[index];
                Vector2 ac = points[c] - points[index];

                Vector2 d = points[index] + (ab / 2);
                Vector2 e = points[index] + (ac / 2);

                points.Insert(b, d);
                points[index] = e;

            }

            public float GetArea() {

                float A = 0.0f;

                for (int p = Count - 1, q = 0; q < Count; p = q++) {
                    A += points[p].x * points[q].y - points[q].x * points[p].y;
                }

                return A * 0.5f;
            }

			//TODO: Sbox implementation
			//public void Draw(Color c, float duration, float size = 0.025f) {
			//    int i = points.Count;
			//    foreach (Vector2 p in points) {
			//        Color color = System.Random.ColorHSV(1, 1, 1, 1, (float)i / points.Count, (float)i / points.Count, 1, 1);
			//        Bounds b = new Bounds(p, Vector2.one * size);
			//        Debug.DrawLine(b.min, b.max, c, duration);
			//        Debug.DrawLine(new Vector2(b.min.x, b.max.y), new Vector2(b.max.x, b.min.y), c, duration);
			//        i--;
			//    }
			//}

			//TODO: Sbox implementation
			//public void DrawOrder(float duration, float size = 0.025f) {
			//    int i = points.Count;
			//    foreach (Vector2 p in points) {
			//        Color color = Random.ColorHSV(1, 1, 1, 1, (float)i / points.Count, (float)i / points.Count, 1, 1);
			//        Bounds b = new Bounds(p, Vector2.one * size);
			//        Debug.DrawLine(b.min, b.max, color, duration);
			//        Debug.DrawLine(new Vector2(b.min.x, b.max.y), new Vector2(b.max.x, b.min.y), color, duration);
			//        i--;
			//    }
			//}

		}

        //From http://totologic.blogspot.se/2014/01/accurate-point-in-triangle-test.html
        //p is the testpoint, and the other points are corners in the triangle
        public static bool IsPointInTriangle(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p) {
            bool isWithinTriangle = false;

            //Based on Barycentric coordinates
            float denominator = ((p2.y - p3.y) * (p1.x - p3.x) + (p3.x - p2.x) * (p1.y - p3.y));

            float a = ((p2.y - p3.y) * (p.x - p3.x) + (p3.x - p2.x) * (p.y - p3.y)) / denominator;
            float b = ((p3.y - p1.y) * (p.x - p3.x) + (p1.x - p3.x) * (p.y - p3.y)) / denominator;
            float c = 1 - a - b;

            //The point is within the triangle or on the border if 0 <= a <= 1 and 0 <= b <= 1 and 0 <= c <= 1
            //if (a >= 0f && a <= 1f && b >= 0f && b <= 1f && c >= 0f && c <= 1f)
            //{
            //    isWithinTriangle = true;
            //}

            //The point is within the triangle
            if (a > 0f && a < 1f && b > 0f && b < 1f && c > 0f && c < 1f) {
                isWithinTriangle = true;
            }

            return isWithinTriangle;
        }

        //Orient triangles so they have the correct orientation
        public static void OrientTrianglesClockwise(List<Triangle> triangles) {
            for (int i = 0; i < triangles.Count; i++) {
                Triangle tri = triangles[i];

                Vector2 v1 = new Vector2(tri.A.x, tri.A.y);
                Vector2 v2 = new Vector2(tri.B.x, tri.B.y);
                Vector2 v3 = new Vector2(tri.C.x, tri.C.y);

                if (!IsTriangleOrientedClockwise(tri.A, tri.B, tri.C)) {
                    tri.ChangeOrientation();
                }
            }
        }

        //Is a triangle in 2d space oriented clockwise or counter-clockwise
        //https://math.stackexchange.com/questions/1324179/how-to-tell-if-3-connected-points-are-connected-clockwise-or-counter-clockwise
        //https://en.wikipedia.org/wiki/Curve_orientation
        public static bool IsTriangleOrientedClockwise(Vector2 p1, Vector2 p2, Vector2 p3) {
            bool isClockWise = true;

            float determinant = p1.x * p2.y + p3.x * p1.y + p2.x * p3.y - p1.x * p3.y - p3.x * p2.y - p2.x * p1.y;

            if (determinant > 0f) {
                isClockWise = false;
            }

            return isClockWise;
        }

    }

    public class Vertex {
        public Vector2 position;

        //The outgoing halfedge (a halfedge that starts at this vertex)
        //Doesnt matter which edge we connect to it
        public HalfEdge halfEdge;

        //Which triangle is this vertex a part of?
        public Triangle triangle;

        //The previous and next vertex this vertex is attached to
        public Vertex prevVertex;
        public Vertex nextVertex;

        //Properties this vertex may have
        //Reflex is concave
        public bool isReflex;
        public bool isConvex;
        public bool isEar;

        public Vertex(Vector2 position) {
            this.position = position;
        }
    }

    public class HalfEdge {
        //The vertex the edge points to
        public Vertex v;

        //The face this edge is a part of
        public Triangle t;

        //The next edge
        public HalfEdge nextEdge;
        //The previous
        public HalfEdge prevEdge;
        //The edge going in the opposite direction
        public HalfEdge oppositeEdge;

        //This structure assumes we have a vertex class with a reference to a half edge going from that vertex
        //and a face (triangle) class with a reference to a half edge which is a part of this face 
        public HalfEdge(Vertex v) {
            this.v = v;
        }
    }

    public class Triangle {
        //Corners
        public Vector3 A;
        public Vector3 B;
        public Vector3 C;

		public Vector3 AB => B - A;
		public Vector3 BC => C - B;
		public Vector3 CA => A - C;

        public Triangle(Vector3 a, Vector3 b, Vector3 c) {
            this.A = a;
            this.B = b;
            this.C = c;
        }

        //Change orientation of triangle from cw -> ccw or ccw -> cw
        public void ChangeOrientation() {
            Vector2 temp = this.A;

            this.A = this.B;

            this.B = temp;
        }

        public bool InsideTriangle(Vector2 p) {
            bool isWithinTriangle = false;

            //Based on Barycentric coordinates
            float denominator = ((B.y - C.y) * (A.x - C.x) + (C.x - B.x) * (A.y - C.y));

            float a = ((B.y - C.y) * (p.x - C.x) + (C.x - B.x) * (p.y - C.y)) / denominator;
            float b = ((C.y - A.y) * (p.x - C.x) + (A.x - C.x) * (p.y - C.y)) / denominator;
            float c = 1 - a - b;

            //The point is within the triangle
            if (a > 0f && a < 1f && b > 0f && b < 1f && c > 0f && c < 1f) {
                isWithinTriangle = true;
            }

            return isWithinTriangle;
        }

        public void Draw() {
			Gizmo.Draw.Line( A, B );
			Gizmo.Draw.Line( B, C );
			Gizmo.Draw.Line( C, A );

		}

		public BBox GetAABB()
		{
			Vector3 mins = new();
			Vector3 maxs = new();

			mins.x = MathF.Min( A.x, B.x );
			mins.x = MathF.Min( mins.x, C.x );

			mins.y = MathF.Min( A.y, B.y );
			mins.y = MathF.Min( mins.y, C.y );

			mins.z = MathF.Min( A.z, B.z );
			mins.z = MathF.Min( mins.z, C.z );

			//

			maxs.x = MathF.Max( A.x, B.x );
			maxs.x = MathF.Max( maxs.x, C.x );

			maxs.y = MathF.Max( A.y, B.y );
			maxs.y = MathF.Max( maxs.y, C.y );

			maxs.z = MathF.Max( A.z, B.z );
			maxs.z = MathF.Max( maxs.z, C.z );

			return new BBox( mins, maxs );

		}

		/// <summary>
		/// Does a ray going from rayOrigin in direction rayDirection intersect this triangle?
		/// https://stackoverflow.com/questions/51797766/how-to-find-the-intersection-point-of-a-ray-and-a-triangle
		/// </summary>
		public bool GetRayIntersection( Vector3 rayOrigin, Vector3 rayDirection, out Vector3 intersection )
		{
			intersection = 0;

			Vector3 edge1 = B - A;
			Vector3 edge2 = C - A;

			Vector3 pvec = Vector3.Cross( rayDirection, edge2 );
			double dot = Vector3.Dot( edge1, pvec );

			// opposite direction?
			if ( dot > -double.Epsilon && dot < double.Epsilon )
			{
				return false;
			}

			double invDot = 1d / dot;
			Vector3 tvec = rayOrigin - A;

			double t, u, v;
			u = Vector3.Dot( tvec, pvec ) * invDot;

			// going wrong direction?
			if ( u < 0 || u > 1 )
			{
				return false;
			}

			Vector3 qvec = Vector3.Cross( tvec, edge1 );

			v = Vector3.Dot( rayDirection, qvec ) * invDot;

			// ??? lol
			if ( v < 0 || u + v > 1 )
			{
				return false;
			}

			t = Vector3.Dot( edge2, qvec ) * invDot;

			intersection = (A * (float)( 1 - u - v)) + (B * ( float)u) + (C * (float)v);
			return true;

		}

	}

    //And edge between two vertices
    public class Edge {
        public Vertex v1;
        public Vertex v2;

        //Is this edge intersecting with another edge?
        public bool isIntersecting = false;

        public Edge(Vertex v1, Vertex v2) {
            this.v1 = v1;
            this.v2 = v2;
        }

        public Edge(Vector3 v1, Vector3 v2) {
            this.v1 = new Vertex(v1);
            this.v2 = new Vertex(v2);
        }

        //Get vertex in 2d space (assuming x, z)
        public Vector2 GetVertex2D(Vertex v) {
            return new Vector2(v.position.x, v.position.y);
        }

        //Flip edge
        public void FlipEdge() {
            Vertex temp = v1;

            v1 = v2;

            v2 = temp;
        }
    }

    public class Plane {
        public Vector3 pos;

        public Vector3 normal;

        public Plane(Vector3 pos, Vector3 normal) {
            this.pos = pos;

            this.normal = normal;
        }
		public Vector3 GetLineIntersection( Vector3 linePoint, Vector3 lineDirection )
		{
			// Line does not intersect plane.
			if ( Vector3.Dot( normal, lineDirection.Normal ) == 0 )
			{
				return Vector3.Zero;
			}

			Vector3.Dot( normal, lineDirection.Normal );
			float t = (Vector3.Dot( normal, pos ) - Vector3.Dot( normal, linePoint )) / Vector3.Dot( normal, lineDirection.Normal );
			return linePoint + (lineDirection.Normal * t);
		}

	}

    public class EarClipping {

        private static bool InsideTriangle(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p) {
            bool isWithinTriangle = false;

            //Based on Barycentric coordinates
            float denominator = ((p2.y - p3.y) * (p1.x - p3.x) + (p3.x - p2.x) * (p1.y - p3.y));

            float a = ((p2.y - p3.y) * (p.x - p3.x) + (p3.x - p2.x) * (p.y - p3.y)) / denominator;
            float b = ((p3.y - p1.y) * (p.x - p3.x) + (p1.x - p3.x) * (p.y - p3.y)) / denominator;
            float c = 1 - a - b;

            //The point is within the triangle or on the border if 0 <= a <= 1 and 0 <= b <= 1 and 0 <= c <= 1
            //if (a >= 0f && a <= 1f && b >= 0f && b <= 1f && c >= 0f && c <= 1f)
            //{
            //    isWithinTriangle = true;
            //}

            //The point is within the triangle
            if (a > 0f && a < 1f && b > 0f && b < 1f && c > 0f && c < 1f) {
                isWithinTriangle = true;
            }

            return isWithinTriangle;
        }

        private static bool Snip(Triangulation.PointCollection contour, int u, int v, int w, int n, ref int[] V) {
            int p;

            Vector2 A = new Vector2(contour.points[V[u]].x, contour.points[V[u]].y);
            Vector2 B = new Vector2(contour.points[V[v]].x, contour.points[V[v]].y);
            Vector2 C = new Vector2(contour.points[V[w]].x, contour.points[V[w]].y);

            if (float.Epsilon > (((B.x - A.x) * (C.y - A.y)) - ((B.y - A.y) * (C.x - A.x))))
                return false;

            for (p = 0; p < n; p++) {
                if ((p == u) || (p == v) || (p == w))
                    continue;

                Vector2 P = new Vector2(contour.points[V[p]].x, contour.points[V[p]].y);

                if (InsideTriangle(A, B, C, P))
                    return false;
            }

            return true;
        }

        public static bool Process(Triangulation.PointCollection contour, out List<Triangle> result) {
            /* allocate and initialize list of Vertices in polygon */

            result = new List<Triangle>();

            int n = contour.Count;
            if (n < 3)
                return false;

            int[] V = new int[n];

            /* we want a counter-clockwise polygon in V */

            if (0.0f < contour.GetArea())
                for (int v = 0; v < n; v++)
                    V[v] = v;
            else
                for (int v = 0; v < n; v++)
                    V[v] = (n - 1) - v;

            int nv = n;

            /*  remove nv-2 Vertices, creating 1 triangle every time */
            int count = 2 * nv;   /* error detection */

            for (int m = 0, v = nv - 1; nv > 2;) {
                /* if we loop, it is probably a non-simple polygon */
                if (0 >= (count--)) {
                    //** Triangulate: ERROR - probable bad polygon!
                    return false;
                }

                /* three consecutive vertices in current polygon, <u,v,w> */
                int u = v;
                if (nv <= u)
                    u = 0;     /* previous */
                v = u + 1;
                if (nv <= v)
                    v = 0;     /* new v    */
                int w = v + 1;
                if (nv <= w)
                    w = 0;     /* next     */

                if (Snip(contour, u, v, w, nv, ref V)) {
                    int a, b, c, s, t;

                    /* true names of the vertices */
                    a = V[u];
                    b = V[v];
                    c = V[w];

                    /* output Triangle */
                    result.Add(new Triangle(contour.points[a], contour.points[b], contour.points[c]));

                    m++;

                    /* remove v from remaining polygon */
                    for (s = v, t = v + 1; t < nv; s++, t++)
                        V[s] = V[t];
                    nv--;

                    /* resest error detection counter */
                    count = 2 * nv;
                }
            }

            return true;
        }
    }

}

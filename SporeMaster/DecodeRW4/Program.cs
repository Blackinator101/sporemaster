using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using System.Xml;
using Gibbed.Spore.Helpers;
using SporeMaster.RenderWare4;

namespace DecodeRW4
{
    struct Vector
    {
        public double x, y, z;
        public Vector(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public double this[int i] {
            get { if (i == 0) return x; else if (i == 1) return y; else if (i == 2) return z; else throw new ArgumentException(); }
            set { if (i == 0) x = value; else if (i == 1) y = value; else if (i == 2) z = value; else throw new ArgumentException(); }
        }
        public Vector Normalized()
        {
            double m = Math.Sqrt( x*x+y*y+z*z );
            if (m == 0) return new Vector();
            m = 1.0 / m;
            return new Vector(x * m, y * m, z * m);
        }
        public static Vector operator -(Vector v)
        {
            return new Vector(-v.x, -v.y, -v.z);
        }
        public static Vector operator +(Vector left, Vector right)
        {
            return new Vector(left.x + right.x, left.y + right.y, left.z + right.z);
        }
        public static Vector operator -(Vector left, Vector right)
        {
            return new Vector(left.x - right.x, left.y - right.y, left.z - right.z);
        }
        public static Vector operator *(Vector left, double right)
        {
            return new Vector(left.x * right, left.y * right, left.z * right);
        }
        public static Vector operator *(double right, Vector left)
        {
            return new Vector(left.x * right, left.y * right, left.z * right);
        }
        public static double operator *(Vector left, Vector right)
        {
            return left.x * right.x + left.y * right.y + left.z * right.z;
        }
        public XAttribute[] toXML()
        {
            return new XAttribute[] {
                new XAttribute("x", x), new XAttribute("y", y), new XAttribute("z",z) };
        }
    };
    struct Point
    {
        public double x, y, z;
        public Point(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public double this[int i]
        {
            get { if (i == 0) return x; else if (i == 1) return y; else if (i == 2) return z; else throw new ArgumentException(); }
            set { if (i == 0) x = value; else if (i == 1) y = value; else if (i == 2) z = value; else throw new ArgumentException(); }
        }
    };

    class TMatrix
    {
        // Represents a concatenation of rotations and translations
        double[] m;   // column major order

        public TMatrix()
        {
            this.m = new double[16];
            this.m[15] = 1;
        }
        public TMatrix(double[] m)
        {
            this.m = m;
            this.m[15] = 1;
        }
        public TMatrix(float[] m)
        {
            this.m = (from x in m select (double)x).ToArray();
            this.m[15] = 1;
        }
        public static TMatrix Translation(Vector v)
        {
            return new TMatrix( new double[] { 1,0,0,0, 0,1,0,0, 0,0,1,0, v.x,v.y,v.z,1 } );
        }
        public static TMatrix FromMat4(Mat4x4 mat4)
        {
            return new TMatrix(mat4.m);
        }
        public TMatrix Translated(Vector v)
        {
            var x = new TMatrix( (double[])this.m.Clone() );
            x.m[12] += v.x;
            x.m[13] += v.y;
            x.m[14] += v.z;
            return x;
        }
        public Vector GetColumn(int c)
        {
            c = c * 4;
            return new Vector(m[c], m[c + 1], m[c + 2]);
        }
        public static TMatrix operator * (TMatrix left, TMatrix right) {
            TMatrix res = new TMatrix();
            res.m[15] = 0;
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    for (int r = 0; r < 4; r++)
                        res.m[i * 4 + j] += left.m[r * 4 + j] * right.m[i * 4 + r];
            return res;
        }
        public static Vector operator *(TMatrix left, Vector right)
        {
            return new Vector(left.m[0] * right.x + left.m[4] * right.y + left.m[8] * right.z,
                              left.m[1] * right.x + left.m[5] * right.y + left.m[9] * right.z,
                              left.m[2] * right.x + left.m[6] * right.y + left.m[10] * right.z);
        }
        public static Point operator *(TMatrix left, Point right)
        {
            return new Point(left.m[0] *right.x + left.m[4] * right.y + left.m[8] * right.z + left.m[12],
                             left.m[1] * right.x + left.m[5] * right.y + left.m[9] * right.z + left.m[13],
                             left.m[2] * right.x + left.m[6] * right.y + left.m[10] * right.z + left.m[14]);
        }
        public TMatrix Inverse()
        {
            // Since this matrix has the form (T * R), the inverse is
            //   R^-1 * T^-1 = (R^T) * -T = T' * (R^T), where
            //   T' = [ I R^T * (-t) ]
            TMatrix res = new TMatrix();
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    res.m[i * 4 + j] = m[i + j * 4];
            Vector t = res * new Vector(this.m[12], this.m[13], this.m[14]);
            for (int i = 0; i < 3; i++)
                res.m[12 + i] = -t[i];
            return res;
        }
        public Quaternion GetRotation()
        {
            // Decompose the transformation to a rotation followed by translation, and return
            //   the rotation.  Throws InvalidOperationException if the matrix is not of that form.
            if (m[3] != 0 || m[7] != 0 || m[11] != 0 || m[15] != 1) 
                throw new InvalidOperationException("Not a rotation+translation.");

            // Check for any scale, which we don't decompose (actually TMatrix is never supposed to
            // have a scale)
            for(int i=0; i<3; i++)
                if (Math.Abs(m[0+i]*m[0+i] + m[4+i]*m[4+i] + m[8+i]*m[8+i] - 1.0) > .0001)
                    throw new InvalidOperationException("Not a rotation+translation.");

            // Extract rotation matrix
            double[] r = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[i * 3 + j] = m[i * 4 + j];

            // Test that it is (approximately) special orthogonal.  If it isn't, the input matrix didn't meet the desired
            // form.
            double det = r[0] * (r[4] * r[8] - r[7] * r[5]) - r[1] * (r[3] * r[8] - r[6] * r[5]) + r[2] * (r[3] * r[7] - r[6] * r[4]);
            if (det < 0.99999 || det > 1.00001)
                throw new InvalidOperationException("Not a rotation+translation.");

            // Rotation -> quaternion
            var rotation = new Quaternion();

            var T = r[0] + r[3 + 1] + r[6 + 2];
            if (T > 0.0)
            {
                var S = 0.5 / Math.Sqrt(T + 1);
                rotation.w = 0.25 / S;
                rotation.x = (r[6 + 1] - r[3 + 2]) * S;
                rotation.y = (r[0 + 2] - r[6 + 0]) * S;
                rotation.z = (r[3 + 0] - r[0 + 1]) * S;
            }
            else if (r[0] > r[3 + 1] && r[0] > r[6 + 2])
            {
                var S = Math.Sqrt(1.0 + r[0] - r[3 + 1] - r[6 + 2]);
                rotation.x = S * 0.5;
                S = 0.5 / S;
                rotation.w = (r[6 + 1] - r[3 + 2]) * S;
                rotation.y = (r[0 + 1] + r[3 + 0]) * S;
                rotation.z = (r[0 + 2] + r[6 + 0]) * S;
            }
            else if (r[3 + 1] > r[6 + 2])
            {
                var S = Math.Sqrt(1.0 + r[3 + 1] - r[0 + 0] - r[6 + 2]);
                rotation.y = S * 0.5;
                S = 0.5 / S;
                rotation.w = (r[0 + 2] - r[6 + 0]) * S;
                rotation.x = (r[0 + 1] + r[3 + 0]) * S;
                rotation.z = (r[3 + 2] + r[6 + 1]) * S;
            }
            else
            {
                var S = Math.Sqrt(1.0 + r[6 + 2] - r[0 + 0] - r[3 + 1]);
                rotation.z = S * 0.5;
                S = 0.5 / S;
                rotation.w = (r[3 + 0] - r[0 + 1]) * S;
                rotation.x = (r[0 + 2] + r[6 + 0]) * S;
                rotation.y = (r[3 + 2] + r[6 + 1]) * S;
            }
            /*var tr = r[0] + r[4] + r[8];
            if (tr >= 0.0)
            {
                var s = Math.Sqrt(tr + 1.0);
                rotation.w = s * 0.5;
                s = 0.5 / s;
                rotation.x = (r[7] - r[5]) * s;
                rotation.y = (r[2] - r[6]) * s;
                rotation.z = (r[3] - r[1]) * s;
            } else {
                int h = 0;
                if (r[4] > */

            /*
            // Shoemake '85
            var rotation = new Quaternion();
            var w2 = 0.25 * (1 + r[0] + r[4] + r[8]);
            if (w2 > double.Epsilon)
            {
                rotation.w = Math.Sqrt(w2);
                rotation.x = (r[7] - r[5]) / (4 * rotation.w);
                rotation.y = (r[2] - r[6]) / (4 * rotation.w);
                rotation.z = (r[3] - r[1]) / (4 * rotation.w);
            }
            else
            {
                rotation.w = 0;
                var x2 = -0.5 * (r[4] + r[8]);
                if (x2 > double.Epsilon)
                {
                    rotation.x = Math.Sqrt(x2);
                    rotation.y = r[3] / (2 * rotation.x);
                    rotation.z = r[6] / (2 * rotation.x);
                }
                else
                {
                    rotation.x = 0;
                    var y2 = 0.5 * (1 - r[8]);
                    if (y2 > double.Epsilon)
                    {
                        rotation.y = Math.Sqrt(y2);
                        rotation.z = r[7] / (2 * rotation.y);
                    }
                    else
                    {
                        rotation.y = 0;
                        rotation.z = 1;
                    }
                }
            }
            rotation.Renormalize();*/

            var tv = new Vector(1,2,3);
            var tp = rotation.ToMatrix().Inverse() * this * tv;
            if ((tp-tv)*(tp-tv) > .0001)
                Console.WriteLine("GetRotation error.");

            rotation.Renormalize();
            return rotation;

            /*rotation.w = Math.Sqrt(Math.Max(0, 1 + r[0] + r[4] + r[8])) * 0.5;
            rotation.x = Math.Sqrt(Math.Max(0, 1 + r[0] - r[4] - r[8])) * 0.5;
            rotation.y = Math.Sqrt(Math.Max(0, 1 - r[0] + r[4] - r[8])) * 0.5;
            rotation.z = Math.Sqrt(Math.Max(0, 1 - r[0] - r[4] + r[8])) * 0.5;
            if (r[6 + 1] - r[3 + 2] < 0) rotation.x = -rotation.x;
            if (r[0 + 2] - r[6 + 0] < 0) rotation.y = -rotation.y;
            if (r[3 + 0] - r[0 + 1] < 0) rotation.z = -rotation.z;
            rotation.Renormalize();
            return rotation;*/

            /*var T = r[0] + r[3 + 1] + r[6 + 2] + 1;
            var rotation = new Quaternion();
            if (T > 0.00000001)
            {
                var S = 0.5 / Math.Sqrt(T);
                rotation.w = 0.25 / S;
                rotation.x = (r[6 + 1] - r[3 + 2]) * S;
                rotation.y = (r[0 + 2] - r[6 + 0]) * S;
                rotation.z = (r[3 + 0] - r[0 + 1]) * S;
            }
            else if (r[0] > r[3 + 1] && r[0] > r[6 + 2])
            {
                var S = 0.5 / Math.Sqrt(1.0 + r[0] - r[3 + 1] - r[6 + 2]);
                rotation.w = (r[6 + 1] - r[3 + 2]) * S;
                rotation.x = 0.25 / S;
                rotation.y = (r[0 + 1] + r[3 + 0]) * S;
                rotation.z = (r[0 + 2] + r[6 + 0]) * S;
            }
            else if (r[3 + 1] > r[6 + 2])
            {
                var S = 0.5 / Math.Sqrt(1.0 + r[3 + 1] - r[0 + 0] - r[6 + 2]);
                rotation.w = (r[0 + 2] - r[6 + 0]) * S;
                rotation.x = (r[0 + 1] + r[3 + 0]) * S;
                rotation.y = 0.25 * S;
                rotation.z = (r[3 + 2] + r[6 + 1]) * S;
            }
            else
            {
                var S = 0.5 / Math.Sqrt(1.0 + r[6 + 2] - r[0 + 0] - r[3 + 1]);
                rotation.w = (r[3 + 0] - r[0 + 1]) * S;
                rotation.x = (r[0 + 2] + r[6 + 0]) * S;
                rotation.y = (r[3 + 2] + r[6 + 1]) * S;
                rotation.z = 0.25 * S;
            }
            rotation.Renormalize();  // Rounding error can make w>1, yielding NAN for rotation.Angle
            return rotation;*/


            // translation = R^T * (-this.w_column)
            /*for (int i = 0; i < 3; i++)
                translation[i] = -(r[0 + i] * m[12] + r[3 + i] * m[13] + r[6 + i] * m[14]);*/
        }
    };

    struct Quaternion
    {
        public double x, y, z, w;
        public Quaternion(double angle, Vector unit_axis)
        {
            double sa = Math.Sin(angle * 0.5);
            w = Math.Cos(angle * 0.5);
            x = unit_axis.x * sa;
            y = unit_axis.y * sa;
            z = unit_axis.z * sa;
        }

        public double Angle { get {
            return Math.Acos(w)*2;
        } }
        public Vector Axis { get {
            double sin_a = Math.Sqrt( 1.0 - w*w );
            if (sin_a < .00001) sin_a = 1.0;
            return new Vector( x/sin_a, y/sin_a, z/sin_a );
        } }
        public void Renormalize()
        {
            double iw = 1.0 / Math.Sqrt(x * x + y * y + z * z + w * w);
            x *= iw;
            y *= iw;
            z *= iw;
            w *= iw;
        }
        public TMatrix ToMatrix()
        {
            var xx = x * x; var xy = x * y; var xz = x * z; var xw = x * w;
            var yy = y * y; var yz = y * z; var yw = y * w;
            var zz = z * z; var zw = z * w;
            return new TMatrix( new double[] { 
                1 - 2 * (yy + zz), 2 * (xy - zw), 2 * (xz + yw), 0,
                2 * (xy + zw), 1 - 2 * (xx + zz), 2 * (yz - xw), 0,
                2 * (xz - yw), 2 * (yz + xw), 1 - 2 * (xx + yy), 0,
                0,0,0,1 } );
        }
        public static Quaternion operator - (Quaternion q) {
            return new Quaternion {x=-q.x,y=-q.y,z=-q.z,w=q.w};
        }
    };

    class Program
    {
        static void CreateModel(string src_name, string out_name)
        {
            using (var stream = File.Create(out_name))
                new ModelPack(src_name, stream);

            Console.WriteLine("Checking model:");
            var tm = new RW4Model();
            using (var stream = File.Open(out_name, FileMode.Open))
                tm.Read(stream);
            foreach (var s in tm.Sections)
                Console.WriteLine(String.Format("  #{0} 0x{1:x} - 0x{2:x}: 0x{3:x} {4}", s.number, s.pos, s.pos + s.size, s.type_code, s.obj.ToString().Substring("SporeMaster.RenderWare4.".Length)));
        }

        /*
        static bool DecomposeMatrix(float[] m, out Quaternion rotation, double[] translation, double[] scale)
        {
            // Decomposes a mat4 matrix of the form (translation * rotation * scale)... note that the input
            //   is not a proper homogenous matrix, but [[rx 0][ry 0][rz 0][trans 0]]
            // This is NOT a general matrix decomposition - in general a scale can be applied in a different basis
            rotation = new Quaternion();
            if (m[3]!=0 || m[7]!=0 || m[11]!=0 || m[15]!=0) return false;
            //translation[0] = m[12]; translation[1] = m[13]; translation[2] = m[14];
            scale[0] = Math.Sqrt(m[0] * m[0] + m[4] * m[4] + m[8] * m[8]);
            scale[1] = Math.Sqrt(m[1] * m[1] + m[5] * m[5] + m[9] * m[9]);
            scale[2] = Math.Sqrt(m[2] * m[2] + m[6] * m[6] + m[10] * m[10]);
            if (scale[0] < .99 || scale[0] > 1.01 || scale[1] < .99 || scale[1] > 1.01 || scale[2] < .99 || scale[2] > 1.01)
                throw new NotSupportedException("Bone scale");

            // Extract rotation matrix
            double[] r = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[i * 3 + j] = m[i * 4 + j] / scale[j];

            // Test that it is (approximately) special orthogonal.  If it isn't, the input matrix didn't meet the desired
            // form.
            double det = r[0] * (r[4]*r[8] - r[7]*r[5]) - r[1] * (r[3]*r[8] - r[6]*r[5]) + r[2] * (r[3]*r[7] - r[6]*r[4]);
            if (det < 0.99999 || det > 1.00001) 
                return false;

            // Rotation -> quaternion
            var T = r[0] + r[3 + 1] + r[6 + 2] + 1;
            if (T > 0.00000001)
            {
                var S = 0.5 / Math.Sqrt(T);
                rotation.w = 0.25 / S;
                rotation.x = (r[6 + 1] - r[3 + 2]) * S;
                rotation.y = (r[0 + 2] - r[6 + 0]) * S;
                rotation.z = (r[3 + 0] - r[0 + 1]) * S;
            }
            else if (r[0] > r[3 + 1] && r[0] > r[6 + 2])
            {
                var S = 0.5 / Math.Sqrt(1.0 + r[0] - r[3 + 1] - r[6 + 2]);
                rotation.w = (r[6 + 1] - r[3 + 2]) * S;
                rotation.x = 0.25 / S;
                rotation.y = (r[0 + 1] + r[3 + 0]) * S;
                rotation.z = (r[0 + 2] + r[6 + 0]) * S;
            }
            else if (r[3 + 1] > r[6 + 2])
            {
                var S = 0.5 / Math.Sqrt(1.0 + r[3 + 1] - r[0 + 0] - r[6 + 2]);
                rotation.w = (r[0 + 2] - r[6 + 0]) * S;
                rotation.x = (r[0 + 1] + r[3 + 0]) * S;
                rotation.y = 0.25 * S;
                rotation.z = (r[3 + 2] + r[6 + 1]) * S;
            }
            else
            {
                var S = 0.5 / Math.Sqrt(1.0 + r[6 + 2] - r[0 + 0] - r[3 + 1]);
                rotation.w = (r[3 + 0] - r[0 + 1]) * S;
                rotation.x = (r[0 + 2] + r[6 + 0]) * S;
                rotation.y = (r[3 + 2] + r[6 + 1]) * S;
                rotation.z = 0.25 * S;
            }

            // translation = R^T * (-M.w_column)
            for(int i=0; i<3; i++)
                translation[i] = -(r[0+i]*m[12] + r[3+i]*m[13] + r[6+i]*m[14]);

            return true;
        }*/

        static XAttribute[] writeVector(float x, float y, float z)
        {
            return new XAttribute[] {
                new XAttribute("x", x), new XAttribute("y", y), new XAttribute("z",z) };
        }
        static XAttribute[] writeVector(double[] v)
        {
            return writeVector((float)v[0], (float)v[1], (float)v[2]);
        }

        static bool fail()
        {
            throw new ModelFormatException(null, "Cannot decompose bone bind matrix.", null);
        }

        static XElement WriteOgreSkeleton(RW4Model model)
        {
            var anims = (from anim in model.GetObjects(Anim.type_code) select (Anim)anim).ToArray();
            var rskels = model.GetObjects(RW4Skeleton.type_code);
            if (rskels.Count != 1) throw new NotSupportedException("Exactly one skeleton required.");
            var skel = (RW4Skeleton)rskels[0];

            Dictionary<uint, int> bone_name_lookup = new Dictionary<uint, int>();
            foreach (var j in skel.jointInfo.items) bone_name_lookup[j.name_fnv] = j.index;

            var inv_binds = (from m in skel.mat4.items select TMatrix.FromMat4(m)).ToArray();

            var rel_bind = (
                from joint in skel.jointInfo.items
                let inv_bind = TMatrix.FromMat4(skel.mat4.items[joint.index])
                let bind = inv_bind.Inverse()
                let parent_inv_bind = joint.parent == null ? null : TMatrix.FromMat4(skel.mat4.items[joint.parent.index])
                select parent_inv_bind == null ? bind : parent_inv_bind * bind
                ).ToArray();

            return new XElement("skeleton",
                new XElement("bones",
                    from joint in skel.jointInfo.items
                    let rel = rel_bind[joint.index]
                    let rotation = -rel.GetRotation()  // < TODO: Why inverse?
                    select new XElement("bone",
                        new XAttribute("id", joint.index),
                        new XAttribute("name", SporeMaster.NameRegistry.Files.toName(joint.name_fnv)),
                        new XElement("position", rel.GetColumn(3).toXML()),
                        new XElement("rotation",
                            new XAttribute("angle", rotation.Angle),
                            new XElement("axis", rotation.Axis.toXML()))
                        )
                    ),
                new XElement("bonehierarchy",
                    from joint in skel.jointInfo.items
                    where joint.parent != null
                    select new XElement("boneparent",
                        new XAttribute("bone", SporeMaster.NameRegistry.Files.toName(joint.name_fnv)),
                        new XAttribute("parent", SporeMaster.NameRegistry.Files.toName(joint.parent.name_fnv)))),
                new XElement("animations",
                    from anim in anims
                    select new XElement("animation",
                        new XAttribute("name", SporeMaster.NameRegistry.Files.toName(anim.hash_name)),
                        new XAttribute("length", anim.length),
                        new XElement("tracks",
                            from i in Enumerable.Range(0, anim.channel_names.Length)
                            let name = anim.channel_names[i]
                            let poses = anim.channel_frame_pose[i]
                            let rel = rel_bind[ bone_name_lookup[ name ] ]
                            let bind_rot = rel.Inverse()
                            let bind_pos = rel.GetColumn(3)
                            select new XElement("track",
                                new XAttribute("bone", SporeMaster.NameRegistry.Files.toName(name)),
                                new XElement("keyframes",
                                    from f in poses
                                    let scale = new Vector(f.sx, f.sy, f.sz)
                                    let pose = TMatrix.Translation(new Vector(f.tx, f.ty, f.tz)) *
                                        (-new Quaternion { x = f.qx, y = f.qy, z = f.qz, w = f.qs }).ToMatrix()
                                    let pose_world = bind_rot * pose
                                    let rot = -pose_world.GetRotation()
                                    let pos = pose.GetColumn(3) - bind_pos
                                    select new XElement("keyframe",
                                        new XAttribute("time", f.time),
                                        new XElement("translate", pos.toXML()),
                                        new XElement("rotate",
                                            new XAttribute("angle", rot.Angle),
                                            new XElement("axis", rot.Axis.toXML())),
                                        new XElement("scale", scale.toXML()))))))));
        }

        static void ReadAnim(RW4Model model, string outname, string ogreSkeletonFile)
        {
            var anims = (from anim in model.GetObjects(Anim.type_code) select (Anim)anim).ToArray();
            var skeletons = (from s in model.GetObjects(RW4Skeleton.type_code) select (RW4Skeleton)s).ToArray();
            if (anims.Length == 0 && skeletons.Length == 0) return;

            var only = new int[] { 1 };

            var x_skels =
                new XElement("skeletons",
                    from skel in skeletons
                    select new XElement("skeleton",
                        new XAttribute("name", SporeMaster.NameRegistry.Files.toName(skel.jointInfo.id)),
                        new XAttribute("unk1", "0x" + skel.unk1.ToString("X")),
                        new XElement("joints",
                            from joint in skel.jointInfo.items
                            let mat4 = skel.mat4.items[joint.index]
                            let inv_bind = TMatrix.FromMat4( mat4 )
                            let bind = inv_bind.Inverse()
                            let rotation = bind.Inverse().GetRotation()  //< TODO: Why inverse?
                            select new XElement("joint",
                                new XAttribute("name", SporeMaster.NameRegistry.Files.toName(joint.name_fnv)),
                                new XAttribute("flags", "0x" + joint.flags.ToString("X")),
                                from _ in only where joint.parent != null 
                                    select new XAttribute("parent", SporeMaster.NameRegistry.Files.toName(joint.parent.name_fnv)),
                                new XElement("Transform",
                                        new XElement("position", bind.GetColumn(3).toXML()),
                                        new XElement("rotation", new XAttribute("angle", rotation.Angle), rotation.Axis.toXML() )
                                        ),
                                new XElement("RawInverseBindMatrix",
                                        new XElement("rx", writeVector(mat4.m[0], mat4.m[1], mat4.m[2])),
                                        new XElement("ry", writeVector(mat4.m[4], mat4.m[5], mat4.m[6])),
                                        new XElement("rz", writeVector(mat4.m[8], mat4.m[9], mat4.m[10])),
                                        new XElement("t",  writeVector(mat4.m[12], mat4.m[13], mat4.m[15]))
                                        )
                                )
                            )
                        )
                    );

            var x_anims = 
                new XElement("anims",
                    from anim in anims
                    select new XElement("anim",
                        new XAttribute("name", SporeMaster.NameRegistry.Files.toName(anim.hash_name)),
                        new XAttribute("flags", "0x" + anim.flags.ToString("X")),
                        new XAttribute("length", anim.length),
                        new XAttribute("skeleton", SporeMaster.NameRegistry.Files.toName(anim.skeleton_id)),
                        //new XAttribute("padding", anim.padding),
                        new XAttribute("components", "0x"+anim.pose_components.ToString("X")),
                        new XElement("channels",
                            from i in Enumerable.Range(0, anim.channel_names.Length)
                            let name = anim.channel_names[i]
                            select new XElement("channel",
                                new XAttribute("name", SporeMaster.NameRegistry.Files.toName(name)),
                                new XAttribute("padding", anim.padding[i]),
                                new XElement("frames",
                                    from pose in anim.channel_frame_pose[i]
                                    select new XElement("frame",
                                        new XAttribute("time", pose.time),
                                        new XElement("Translation",
                                            new XAttribute("x", pose.tx),
                                            new XAttribute("y", pose.ty),
                                            new XAttribute("z", pose.tz)),
                                        new XElement("Rotation",
                                            new XAttribute("x", pose.qx),
                                            new XAttribute("y", pose.qy),
                                            new XAttribute("z", pose.qz),
                                            new XAttribute("s", pose.qs)),
                                        new XElement("Scale",
                                            new XAttribute("x", pose.sx),
                                            new XAttribute("y", pose.sy),
                                            new XAttribute("z", pose.sz))
                                        )
                                    )
                                )
                            )
                        )
                    );
            var output = new XElement("raw_animation_info",
                x_anims,
                x_skels);
            output.Save(outname);

            WriteOgreSkeleton(model).Save(ogreSkeletonFile);

            foreach(var s in skeletons)
                foreach(var mat4 in s.mat4.items)
                    if (mat4.m[3]!=0 || mat4.m[7]!=0 || mat4.m[15]!=0)
                        throw new Exception("Extra matrix cells nonzero.");
        }

        static void TestMath()
        {
            var rand = new Random();

            for (int i = 0; i < 1000; i++)
            {
                var u = new Vector( rand.NextDouble()-0.5, rand.NextDouble()-0.5, rand.NextDouble()-0.5 ).Normalized();
                var t1 = new Vector( rand.NextDouble()-0.5, rand.NextDouble()-0.5, rand.NextDouble()-0.5 ) * 10;
                var t2 = new Vector(rand.NextDouble() - 0.5, rand.NextDouble() - 0.5, rand.NextDouble() - 0.5) * 10;
                var p1 = new Vector(rand.NextDouble() - 0.5, rand.NextDouble() - 0.5, rand.NextDouble() - 0.5) * 10;

                var q = new Quaternion(rand.NextDouble() * Math.PI, u);
                var m = q.ToMatrix();
                var mt = TMatrix.Translation(t1) * m * TMatrix.Translation(t2);
                var q2 = mt.GetRotation();
                var m2 = q2.ToMatrix();

                if (((m * p1) - (m2 * p1)) * (m * p1 - m2 * p1) > .0001)
                    Console.WriteLine("math error.");
            }

            var badq = new Quaternion(3.1414934204542249,
                new Vector(-0.920567453, -0.390583634, -0));
            badq = badq.ToMatrix().GetRotation();
            Console.WriteLine(badq.Angle);
            Console.WriteLine(badq.Axis);

        }

        static void TestPack()
        {
            var pf = "C:\\my\\proj\\mods\\spore\\SporeMaster\\SporeMaster\\bin\\Release\\spore.unpacked\\gametuning~\\spacenpcai~.prop";

            var data = File.ReadAllBytes(pf);

            var file3 = new Gibbed.Spore.Properties.PropertyFile();
            file3.Read(new MemoryStream(data));

            var m = new MemoryStream();
            file3.Write(m);
            if (m.Length != data.Length)
                Console.WriteLine("Length changed.");
            else
                Console.WriteLine("Length OK.");
            for (int i = 0; i < m.Length && i < data.Length; i++)
                if (m.GetBuffer()[i] != data[i])
                {
                    Console.WriteLine(String.Format("Different at byte {0}: {1:x} {2:x}", i, data[i], m.GetBuffer()[i]));
                }


            /*var reader = XmlReader.Create(File.OpenText(pf + ".xml"));
            var file = new Gibbed.Spore.Properties.PropertyFile();
            file.ReadXML(reader);
            reader.Close();
            var output = new MemoryStream();
            file.Write(output);

            output.Seek(0, SeekOrigin.Begin);

            var file2 = new Gibbed.Spore.Properties.PropertyFile();
            file2.Read(output);*/
        }

        static void Main(string[] args)
        {
            SporeMaster.NameRegistry.Files = new SporeMaster.NameRegistry();
            SporeMaster.NameRegistry.Files.readRegistryFile("C:\\my\\proj\\mods\\spore\\SporeMaster\\SporeMaster\\bin\\Release\\reg_file.txt", true);
            SporeMaster.NameRegistry.Properties = new SporeMaster.NameRegistry();
            SporeMaster.NameRegistry.Properties.readRegistryFile("C:\\my\\proj\\mods\\spore\\SporeMaster\\SporeMaster\\bin\\Release\\reg_property.txt", true);
            for (int i = 0; i < 100; i++)
            {
                SporeMaster.NameRegistry.Files.toHash(String.Format("joint{0}", i));
                SporeMaster.NameRegistry.Files.toHash(String.Format("bone{0}", i));
            }

            TestMath();

            //DoRewriteTest();

            /*var f = "C:\\my\\proj\\mods\\spore\\SporeMaster\\SporeMaster\\bin\\Release\\mod_eye.package.unpacked\\part_models~\\ce_sense_eye_weird_01.rw4\\";
            using (var stream = File.OpenWrite(f + "raw.rw4"))
                new ModelPack(f + "model.mesh.xml", stream);*/

            DoConvert();
            /*ReadAnim("C:\\my\\proj\\mods\\spore\\SporeMaster\\SporeMaster\\bin\\Release\\spore.unpacked\\part_models~\\ce_sense_eye_weird_01.rw4\\raw.rw4",
                     "anim.xml");*/
        }

        static void DoRewriteTest()
        {
            var f = "C:\\my\\proj\\mods\\spore\\SporeMaster\\SporeMaster\\bin\\Release\\spore.unpacked\\part_models~\\ce_sense_eye_weird_01.rw4\\raw.rw4";
            var m = new RW4Model();
            using (var s = File.OpenRead(f))
                m.Read(s);
            var an = (Animations)m.GetObjects(Animations.type_code)[0];
            an.animations = new Anim[] { an.animations[0] };
            for(int i=0; i<an.animations[0].channel_frame_pose[0].Length; i++)
                an.animations[0].channel_frame_pose[0][i].tx = 0;
            var sections = new int[] { 1, 3, 4, 14, 15 };
            for (int i = sections.Length - 1; i >= 0; i--)
                m.RemoveSection(m.Sections[sections[i]]);
            foreach (var s in m.Sections)
                s.alignment = 0x10;
            m.Test();
            using (var s = File.Create("c:\\temp\\raw.rw4"))
                m.Write(s);
        }

        static void DoConvert() {
            Dictionary<string, int> errors = new Dictionary<string, int>();
            int okCount = 0;
            string[] files;
            string dir = "C:\\my\\proj\\mods\\spore\\SporeMaster\\SporeMaster\\bin\\Release\\spore.unpacked\\part_models~\\";
            //string dir = "C:\\my\\proj\\mods\\spore\\SporeMaster\\SporeMaster\\bin\\Release\\mod_eye.package.unpacked\\part_models~\\";

            Dictionary<string, List<string>> name_files = new Dictionary<string, List<string>>();

            files = Directory.GetFiles(dir, "raw.rw4", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                //var outdir = "c:\\my\\proj\\mods\\spore\\rw4test\\" + Path.GetFileNameWithoutExtension(f) + ".rw4";
                var outdir = Path.GetDirectoryName(f);
                try
                {
                    //Console.Write(f.Substring(dir.Length) + ": ");
                    var fn = Path.GetDirectoryName(f.Substring(dir.Length));
                    Console.WriteLine("  " + fn);
                    //if (fn != "ce_details_hair_01.rw4") continue;
                    //if (fn != "ce_details_wing_04.rw4") continue;
                    ModelUnpack up;
                    using (var stream = File.OpenRead(f))
                    {
                        Directory.CreateDirectory(outdir);
                        up = new ModelUnpack(stream,
                                    outdir);
                    }
                    if (up.Model.FileType == RW4Model.FileTypes.Model)
                    {
                        ReadAnim(up.Model, outdir + "\\anim.xml", outdir + "\\model.skeleton.xml");

                        var anims = (from anim in up.Model.GetObjects(Anim.type_code) select (Anim)anim).ToArray();
                        foreach (var a in anims)
                        {
                            var n = SporeMaster.NameRegistry.Files.toName(a.hash_name);
                            if (a.is_handle) n += " (H)";
                            if (!name_files.ContainsKey(n))
                                name_files.Add(n, new List<string>());
                            name_files[n].Add(fn);
                            Console.WriteLine("    " + n);
                        }
                    }
                    okCount++;
                    //Console.WriteLine(up.Type);
                }
                catch (SporeMaster.RenderWare4.ModelFormatException e)
                {
                    System.Console.WriteLine("  " + e.Message);
                    if (errors.ContainsKey(e.exception_type)) errors[e.exception_type]++;
                    else errors[e.exception_type] = 1;
                    /*try { Directory.Delete(outdir); }
                    catch (System.IO.IOException) { }*/
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("  " + e.Message);
                    if (errors.ContainsKey(e.Message)) errors[e.Message]++;
                    else errors[e.Message] = 1;
                    /*try { Directory.Delete(outdir); }
                    catch (System.IO.IOException) { }*/
                }
            }

            System.Console.WriteLine(String.Format("Converted {0}/{1} files.", okCount, files.Length));
            if (errors.Count() != 0)
            {
                System.Console.WriteLine("Errors:");
                foreach (var e in errors)
                    System.Console.WriteLine(String.Format("     {0}: {1}", e.Key, e.Value));
            }

            System.Console.WriteLine("Handles:");
            foreach (var n in name_files)
            {
                if (!n.Key.EndsWith("(H)")) continue;
                System.Console.WriteLine("  " + n.Key);
                foreach (var f in n.Value)
                    System.Console.WriteLine("    " + f);
            }

            System.Console.WriteLine("Animations:");
            foreach (var n in name_files)
            {
                if (n.Key.EndsWith("(H)")) continue;
                System.Console.WriteLine("  " + n.Key);
                foreach (var f in n.Value)
                    System.Console.WriteLine("    " + f);
            }
        }
    }
}

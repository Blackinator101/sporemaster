using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Gibbed.Spore.Helpers;

namespace SporeMaster.RenderWare4
{
    public class ModelFormatException : FormatException
    {
        public string exception_type;
        public ModelFormatException(Stream r, string description, object argument)
            : base( String.Format("{0} {2} @0x{1:x}", description, r==null?0:r.Position, argument) )
        {
            exception_type = description;
        }
    }

    public static class StreamHelpers
    {
        public static byte[] ReadBytes(this Stream r, int count)
        {
            var a = new byte[count];
            if (r.Read(a, 0, count) != count) throw new EndOfStreamException();
            return a;
        }
        public static void WriteU32s(this Stream w, UInt32[] values)
        {
            foreach (var v in values)
                w.WriteU32(v);
        }
        public static void ReadPadding(this Stream r, long pad)
        {
            if (pad < 0) throw new ModelFormatException(r, "Negative padding", pad);
            for(long i=0; i<pad; i++) 
                if (r.ReadByte()!=0) 
                    throw new ModelFormatException(r, "Nonzero padding", null);
        }
        public static void WritePadding(this Stream w, long pad)
        {
            if (pad < 0) throw new ModelFormatException(w, "Negative padding", pad);
            for (long i = 0; i < pad; i++) w.WriteByte(0);
        }
        public static void expect(this Stream r, UInt32 value, string error)
        {
            var actual = r.ReadU32();
            if (actual != value)
                throw new ModelFormatException(r, error, actual);
        }
        public static void expect(this Stream r, UInt32[] values, string error)
        {
            foreach (var v in values)
                r.expect(v, error);
        }
    };

    public abstract class RW4Object
    {
        public RW4Section section;
        public abstract void Read(RW4Model m, RW4Section s, Stream r);
        public abstract void Write(RW4Model m, RW4Section s, Stream w);
        public virtual int ComputeSize() { return -1;  }
    };

    interface IRW4Struct
    {
        void Read(Stream r);
        void Write(Stream w);
        uint Size();
    };

    struct Triangle : IRW4Struct
    {
        public UInt32 i,j,k;
        public byte unk1;           //< 4 bits found in "SimpleMesh", in a parallel section
        public void Read(Stream r)
        {
            i = r.ReadU16(); j = r.ReadU16(); k = r.ReadU16();
        }
        public void Write(Stream w){
            w.WriteU16((ushort)i); w.WriteU16((ushort)j); w.WriteU16((ushort)k);
        }
        public uint Size() { return 6; }
    }

    struct Vertex : IRW4Struct
    {
        public const int size = 36;
        public float x, y, z;
        public UInt32 normal, tangent;
        public float u, v;
        public UInt32 packed_bone_indices, packed_bone_weights;

        public uint Size() { return size; }
        public void Read(Stream r)
        {
            x = r.ReadF32(); y = r.ReadF32(); z = r.ReadF32();
            normal = r.ReadU32(); tangent = r.ReadU32();
            u = r.ReadF32(); v = r.ReadF32();
            packed_bone_indices = r.ReadU32(); packed_bone_weights = r.ReadU32();
        }
        public void Write(Stream w)
        {
            w.WriteF32(x); w.WriteF32(y); w.WriteF32(z);
            w.WriteU32(normal); w.WriteU32(tangent);
            w.WriteF32(u); w.WriteF32(v);
            w.WriteU32(packed_bone_indices); w.WriteU32(packed_bone_weights);
        }
        public void Read4(Stream r)
        {
            x = r.ReadF32(); y = r.ReadF32(); z = r.ReadF32(); r.expect(0, "4V001");
        }
        public void Write4(Stream w)
        {
            w.WriteF32(x); w.WriteF32(y); w.WriteF32(z); w.WriteU32(0);
        }

        public static UInt32 PackNormal(float x, float y, float z)
        {
            float invl = 127.5F;
            var xb = (byte)(x*invl + 127.5);
            var yb = (byte)(y*invl + 127.5);
            var zb = (byte)(z*invl + 127.5);
            return ((UInt32)xb) + ((UInt32)yb << 8) + ((UInt32)zb << 16);
        }
        public static float UnpackNormal(UInt32 packed, int dim)
        {
            byte b = (byte)((packed >> (dim * 8)) & 0xff);
            return (((float)b) - 127.5f) / 127.5f;
        }
    };

    struct Mat4x4 : IRW4Struct
    {
        public float[] m;
        public uint Size() { return 16 * 4; }
        public void Read(Stream s)
        {
            m = new float[16];
            for (int i = 0; i < m.Length; i++) m[i] = s.ReadF32();
        }
        public void Write(Stream s)
        {
            foreach (var f in m) s.WriteF32(f);
        }
    };

    struct Mat4x3 : IRW4Struct
    {
        public float[] m;
        public uint Size() { return 12 * 4; }
        public void Read(Stream s)
        {
            m = new float[12];
            for (int i = 0; i < m.Length; i++) m[i] = s.ReadF32();
        }
        public void Write(Stream s)
        {
            foreach (var f in m) s.WriteF32(f);
        }
    };

    class Buffer<T> : RW4Object, IEnumerable<T> where T : IRW4Struct, new() {
        public const uint type_code = 0x10030;
        static uint Tsize = (new T()).Size();
        T[] items;
        public Buffer(int size) { items = new T[size]; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return ((IEnumerable<T>)this).GetEnumerator(); }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            foreach (var t in items)
                yield return t;
        }
        public T this[int index]
        {
            get { return items[index]; }
            set { items[index] = value; }
        }
        public int Length {
            get { return items.Length; }
            set {
                var v = new T[value];
                for(int i=0; i<value && i<items.Length; i++)
                    v[i] = items[i];
                items = v;
            }
        }
        public void Assign(IList<T> data)
        {
            items = data.ToArray();
        }
        public override int ComputeSize() { return (int)(Tsize * items.Length); }
        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            if (s.type_code != type_code) throw new ModelFormatException(r, "VB000 Bad type code", s.type_code);
            for (int i = 0; i < items.Length; i++)
                items[i].Read(r);
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            for (int i = 0; i < items.Length; i++)
                items[i].Write(w);
        }
    };

    class Matrices<T> : RW4Object where T : IRW4Struct, new()
    {
        static int Tsize = (int)(new T()).Size();
        public T[] items;
        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            var p1 = r.ReadU32();
            var count = r.ReadU32();
            r.expect(0, "MS001");      /// These bytes are padding to 16 bytes, but this structure is itself always aligned
            r.expect(0, "MS002");
            if (p1 != r.Position)
                throw new ModelFormatException(r, "MS001", p1);
            items = new T[count];
            for (int i = 0; i < count; i++)
                items[i].Read(r);
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            var p1 = (uint)w.Position + 16;
            w.WriteU32(p1);
            w.WriteU32((uint)items.Length);
            w.WriteU32(0);
            w.WriteU32(0);
            foreach (var i in items)
                i.Write(w);
        }
        public override int ComputeSize()
        {
            return 16 + Tsize * items.Length;
        }
    };

    class Matrices4x4 : Matrices<Mat4x4>
    {
        public const int type_code = 0x70003;
    };

    class Matrices4x3 : Matrices<Mat4x3>
    {
        public const int type_code = 0x7000f;
    };

    class RW4TriangleArray : RW4Object
    {
        public const int type_code = 0x20007;
        public Buffer<Triangle> triangles;
        public uint unk1;

        public override int ComputeSize() { return 4 * 7; }
        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            if (s.type_code != type_code) throw new ModelFormatException(r, "TA000 Bad type code", s.type_code);
            unk1 = r.ReadU32();
            r.expect(0, "TA001");
            var ind_count = r.ReadU32();
            r.expect(8, "TA002");
            r.expect(101, "TA003");
            r.expect(4, "TA004");
            var section_number = r.ReadS32();
            if (ind_count % 3 != 0) throw new ModelFormatException(r, "TA010", ind_count);
            var tri_count = ind_count / 3;

            var section = m.Sections[section_number];
            section.LoadObject(m, triangles = new Buffer<Triangle>((int)tri_count), r);
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            w.WriteU32(unk1);
            w.WriteU32(0);
            w.WriteU32((uint)triangles.Length * 3);
            w.WriteU32(8);
            w.WriteU32(101);
            w.WriteU32(4);
            w.WriteU32(triangles.section.number);
        }
    }

    class RW4VertexArray : RW4Object
    {
        public const uint type_code = 0x20005;
        public VertexFormat format;  // Vertex format?
        public UInt32 unk2;             // Dangling pointer from data structure??
        public Buffer<Vertex> vertices;

        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            if (s.type_code != type_code) throw new ModelFormatException(r, "VA000 Bad type code", s.type_code);
            var section_1 = r.ReadS32();
            this.unk2 = r.ReadU32();
            r.expect(0, "VA001");
            var vcount = r.ReadU32();
            r.expect(8, "VA002");
            var vertex_size = r.ReadU32();
            if (vertex_size != Vertex.size)
                throw new ModelFormatException(r, "VA100 Unsupported vertex size", vertex_size);
            var section_number = r.ReadS32();

            var section = m.Sections[section_number];
            section.LoadObject(m, vertices=new Buffer<Vertex>((int)vcount), r);

            section = m.Sections[section_1];
            if (section.type_code != VertexFormat.type_code)
                throw new ModelFormatException(r, "VA101 Bad section type code", section.type_code);
            section.GetObject(m, r, out format);
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            w.WriteU32(this.format.section.number);
            w.WriteU32(this.unk2);
            w.WriteU32(0);
            w.WriteU32((uint)vertices.Length);
            w.WriteU32(8);
            w.WriteU32(Vertex.size);
            w.WriteU32(vertices.section.number);
        }
        public override int ComputeSize() { return 4 * 7; }
    };

    class RW4BBox : RW4Object
    {
        public const int type_code = 0x80005;  //< When found alone.  Also used in RW4SimpleMesh
        public float minx, miny, minz;
        public float maxx, maxy, maxz;
        public uint unk1, unk2;

        public bool IsIdentical (RW4BBox b) {
            return minx == b.minx && miny == b.miny && minz == b.minz && maxx == b.maxx && maxy == b.maxy && maxz == b.maxz &&
                unk1 == b.unk1 && unk2 == b.unk2;
        }

        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            minx = r.ReadF32(); miny = r.ReadF32(); minz = r.ReadF32(); unk1 = r.ReadU32();
            maxx = r.ReadF32(); maxy = r.ReadF32(); maxz = r.ReadF32(); unk2 = r.ReadU32();
            if (minx > maxx || miny > maxy || minz > maxz) throw new ModelFormatException(r,"BBOX011",null);
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            w.WriteF32(minx); w.WriteF32(miny); w.WriteF32(minz); w.WriteU32(unk1);
            w.WriteF32(maxx); w.WriteF32(maxy); w.WriteF32(maxz); w.WriteU32(unk2);
        }
        public override int ComputeSize() { return 4 * 8; }
    };

    class RW4SimpleMesh : RW4Object
    {
        public const int type_code = 0x80003;
        RW4BBox bbox = new RW4BBox();
        uint unk1;
        public Vertex[] vertices;
        public Triangle[] triangles;
        uint[] unknown_data_2;

        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            var p0 = r.Position;
            bbox.Read(m, null, r);
            r.expect(0xd59208, "SM001");      // most but not all creature models
            this.unk1 = r.ReadU32();
            var tri_count = r.ReadU32();
            r.expect(0, "SM002");
            var vertexcount = r.ReadU32();

            var p2 = r.ReadU32();
            var p1 = r.ReadU32();
            var p4 = r.ReadU32();
            var p3 = r.ReadU32();

            if (p1 != (uint)((r.Position + 15) & ~15)) throw new ModelFormatException(r, "SM010", null);

            r.ReadPadding(p1 - r.Position);

            if (p2 != p1 + vertexcount * 16) throw new ModelFormatException(r, "SM101", null);
            if (p3 != p2 + tri_count * 16) throw new ModelFormatException(r, "SM102", null);
            if (p4 != p3 + (((tri_count/2)+15)&~15)) throw new ModelFormatException(r, "SM103", null);

            vertices = new Vertex[vertexcount];
            for (int i = 0; i < vertexcount; i++)
                vertices[i].Read4(r);

            triangles = new Triangle[tri_count];
            for (int t = 0; t < tri_count; t++)
            {
                var index = r.ReadU32(); if (index >= vertexcount) throw new ModelFormatException(r, "SM200", t);
                triangles[t].i = (UInt16)index;
                index = r.ReadU32(); if (index >= vertexcount) throw new ModelFormatException(r, "SM200", t);
                triangles[t].j = (UInt16)index;
                index = r.ReadU32(); if (index >= vertexcount) throw new ModelFormatException(r, "SM200", t);
                triangles[t].k = (UInt16)index;
                r.expect(0, "SM201");
            }

            UInt32 x = 0;
            for (int t = 0; t < tri_count; t++)
            {
                if ((t & 7) == 0)
                    x = r.ReadU32();
                triangles[t].unk1 = (byte)((x >> ((t & 7) * 4)) & 0xf);
            }
            for (int t = (int)tri_count; t < ((tri_count + 7) & ~7); t++)
                if ((byte)((x >> ((t & 7) * 4)) & 0xf) != 0xf)
                    throw new ModelFormatException(r, "SM210", t);
            r.ReadPadding(p4 - r.Position);
            //if (r.Position != p4) throw new ModelFormatException(r, "SM299", r.Position - p4);

            r.expect(p1 - 8 * 4, "SM301");
            var u2_count = r.ReadU32();
            r.expect(tri_count, "SM302");
            r.expect(0, "SM303");

            var bbox2 = new RW4BBox();
            bbox2.Read(m, null, r);
            if (!bbox.IsIdentical(bbox2)) throw new ModelFormatException(r, "SM310", bbox2);

            unknown_data_2 = new uint[u2_count * 8];  // Actually this is int*6 + float*2
            for (int i = 0; i < unknown_data_2.Length; i++)
                unknown_data_2[i] = r.ReadU32();

        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            var tri_count = triangles.Length;

            bbox.Write(m, null, w);
            w.WriteU32(0xd59208);
            w.WriteU32(this.unk1);
            w.WriteS32(tri_count);
            w.WriteU32(0);
            w.WriteS32(vertices.Length);

            long p1 = (w.Position + 4*4 + 15) & ~15;
            var p4 = p1 + vertices.Length * 16 + tri_count * 16 + (((tri_count / 2) + 15) & ~15);
            w.WriteU32((uint)(p1 + vertices.Length * 16));
            w.WriteU32((uint)p1);
            w.WriteU32((uint)p4);
            w.WriteU32((uint)(p1 + vertices.Length * 16 + tri_count * 16));

            w.WritePadding(p1 - w.Position);

            for (int i = 0; i < vertices.Length; i++)
                vertices[i].Write4(w);
            for (int t = 0; t < tri_count; t++)
            {
                w.WriteU32(triangles[t].i);
                w.WriteU32(triangles[t].j);
                w.WriteU32(triangles[t].k);
                w.WriteU32(0);
            }
            UInt32 pack = 0;
            for (int t = 0; t < tri_count; t++)
            {
                pack |= ((UInt32)triangles[t].unk1) << ((t & 7) * 4);
                if ((t & 7) == 7)
                {
                    w.WriteU32(pack);
                    pack = 0;
                }
            }
            for (int t = (int)tri_count; t < ((tri_count + 7) & ~7); t++)
            {
                pack |= 0xfU << ((t & 7) * 4);
                if ((t & 7) == 7)
                    w.WriteU32(pack);
            }

            w.WritePadding(p4 - w.Position);

            w.WriteU32((uint)(p1 - 8 * 4));
            w.WriteU32((uint)(unknown_data_2.Length / 8));
            w.WriteU32((uint)tri_count);
            w.WriteU32(0);
            bbox.Write(m, null, w);
            w.WriteU32s(unknown_data_2);
        }
    };

    class RW4Mesh : RW4Object
    {
        public const int type_code = 0x20009;
        public RW4VertexArray vertices = null;
        public RW4TriangleArray triangles = null;
        uint vert_count, tri_count;

        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            // TODO: some files have multiple meshes referencing the same vertex and/or triangle buffers
            r.expect(40, "ME001");
            r.expect(4, "ME002");
            var tri_section = r.ReadS32();
            this.tri_count = r.ReadU32();
            r.expect(1, "ME003");
            r.expect(0, "ME004");
            r.expect(tri_count * 3, "ME005");
            r.expect(0, "ME006");
            this.vert_count = r.ReadU32();
            var vert_section = r.ReadS32();

            m.Sections[tri_section].GetObject(m, r, out triangles);
            if (triangles.triangles.Length != tri_count) throw new ModelFormatException(r, "ME100 Triangle count mismatch", new KeyValuePair<uint,int>(tri_count,triangles.triangles.Length));

            if (vert_section != 0x400000)
            {
                m.Sections[vert_section].GetObject(m, r, out vertices);
                if (vertices.vertices.Length != vert_count) throw new ModelFormatException(r, "ME200 Vertex count mismatch", new KeyValuePair<uint,int>(vert_count,vertices.vertices.Length));
            }
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            if (vertices != null) vert_count = (uint)vertices.vertices.Length;
            if (triangles != null) tri_count = (uint)triangles.triangles.Length;
            w.WriteU32(40);
            w.WriteU32(4);
            w.WriteU32(triangles.section.number);
            w.WriteU32(tri_count);
            w.WriteU32(1);
            w.WriteU32(0);
            w.WriteU32(tri_count * 3);
            w.WriteU32(0);
            w.WriteU32(vert_count);
            w.WriteU32(vertices.section.number);
        }
        public override int ComputeSize() { return 4 * 10; }
    };

    class Texture : RW4Object
    {
        public const int type_code = 0x20003;
        public const int DXT5 = 0x35545844;   // 'DXT5'
        public TextureBlob texData;
        public UInt32 textureType;
        public UInt32 unk1;  //< dead pointer?
        public UInt16 width, height;
        public UInt32 mipmapInfo;  // 0x708 for 64x64 (7 mipmap levels); 0x808 for 128x128 (8 mipmap levels)

        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            if (s.type_code != type_code) throw new ModelFormatException(r, "T000", s.type_code);
            textureType = r.ReadU32();
            r.expect(8, "T001");
            unk1 = r.ReadU32();
            width = r.ReadU16();
            height = r.ReadU16();
            mipmapInfo = r.ReadU32();
            r.expect(0, "T002");
            r.expect(0, "T003");
            var sec = r.ReadS32();

            m.Sections[sec].GetObject(m, r, out texData);
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            w.WriteU32(textureType);
            w.WriteU32(8);
            w.WriteU32(unk1);
            w.WriteU16(width);
            w.WriteU16(height);
            w.WriteU32(mipmapInfo);
            w.WriteU32(0);
            w.WriteU32(0);
            w.WriteU32(texData.section.number);
        }
        public override int ComputeSize() { return 4 * 7 + 2 * 2; }
    };

    class RW4TexMetadata : RW4Object
    {
        public const int type_code = 0x2000b;
        public Texture texture;
        public byte[] unk_data_1;
        public Int32[] unk_data_2 = new Int32[36];

        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            if (s.type_code != type_code) throw new ModelFormatException(r, "TM000", s.type_code);
            // This section is hard to really decode because it is almost constant across different models!
            var p = (long)s.size - (unk_data_2.Length) * 4 - 4;
            if (p < 0) throw new ModelFormatException(r, "TM001", p);
            unk_data_1 = r.ReadBytes((int)p);
            var sn = r.ReadS32();
            for (int i = 0; i < unk_data_2.Length; i++)
                unk_data_2[i] = r.ReadS32();

            m.Sections[sn].GetObject(m, r, out texture);
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            w.Write(unk_data_1, 0, unk_data_1.Length);
            w.WriteU32(texture.section.number);
            for (int i = 0; i < unk_data_2.Length; i++)
                w.WriteS32(unk_data_2[i]);
        }
        public override int ComputeSize()
        {
            return unk_data_1.Length + 4 + unk_data_2.Length * 4;
        }
    };

    class RWMeshMaterialAssignment : RW4Object
    {
        public const int type_code = 0x2001a;
        public RW4Mesh mesh;
        public RW4TexMetadata[] mat;

        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            var sn1 = r.ReadS32();
            var count = r.ReadU32();        // always exactly 1, so I am guessing
            var sns = new Int32[count];
            for (int i = 0; i < count; i++)
                sns[i] = r.ReadS32();

            m.Sections[sn1].GetObject(m, r, out mesh);
            mat = new RW4TexMetadata[count];
            for (int i = 0; i < count; i++)
                m.Sections[sns[i]].GetObject(m, r, out mat[i]);
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            w.WriteU32(mesh.section.number);
            w.WriteU32((uint)mat.Length);
            foreach (var x in mat)
                w.WriteU32(x.section.number);
        }
        public override int ComputeSize()
        {
            return 8 + 4 * mat.Length;
        }
    };

    class RW4Material : RW4Object
    {
        public const int type_code = 0x7000b;
        public RW4TexMetadata tex;
        public RW4HierarchyInfo names;   //< Always a size 1 hierarchy information? (same type as RW4Skeleton.jointInfo)
        public UInt32 unk1;

        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            if (s.type_code != type_code) throw new ModelFormatException(r, "MT000", s.type_code);
            r.expect(0x400000, "MT001");
            r.expect(unk1=0x63ffb0, "MT002");  //< TODO: Not universal, but not sure other decoding is robust with other values
            r.expect((uint)r.Position + 16, "MT003");
            r.expect(0x400000, "MT004");
            var s1 = r.ReadS32();
            r.expect(1, "MT005");
            var s2 = r.ReadS32();
            r.expect(0, "MT006");
            m.Sections[s1].GetObject(m, r, out names);
            m.Sections[s2].GetObject(m, r, out tex);

            if (names.items.Length != 1) throw new ModelFormatException(r, "MT100", names.items.Length);
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            w.WriteU32(0x400000);
            w.WriteU32(unk1);
            w.WriteU32((uint)w.Position + 16);
            w.WriteU32(0x400000);
            w.WriteU32(names.section.number);
            w.WriteU32(1);
            w.WriteU32(tex.section.number);
            w.WriteS32(0);
        }
        public override int ComputeSize()
        {
            return 4 * 8;
        }
    };

    class RW4HierarchyInfo : RW4Object
    {
        public const int type_code = 0x70002;
        public class Item
        {
            public int index;
            public UInt32 name_fnv;   // e.g. "joint1".fnv()
            public UInt32 flags;      // probably; values are all 0 to 3.   &1 might be "leaf"
            public Item parent;
        };
        public Item[] items;
        public UInt32 id;           // hash or guid, referenced by Anim to specify which bones to animate?

        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            if (s.type_code != type_code) throw new ModelFormatException(r, "HI000", s.type_code);
            var p2 = r.ReadU32();
            var p3 = r.ReadU32();
            var p1 = r.ReadU32();
            var c1 = r.ReadU32();
            id = r.ReadU32();
            r.expect(c1, "HI001");
            if (p1 != r.Position) throw new ModelFormatException(r, "HI010", p1);
            if (p2 != p1 + 4*c1) throw new ModelFormatException(r, "HI011", p2);
            if (p3 != p1 + 8*c1) throw new ModelFormatException(r, "HI012", p3);
            items = new Item[c1];
            for(int i=0; i<c1; i++)
                items[i] = new Item { index = i, name_fnv = r.ReadU32() };
            for(int i=0; i<c1; i++)
                items[i].flags = r.ReadU32();
            for (int i = 0; i < c1; i++)
            {
                var pind = r.ReadS32();
                if (pind == -1)
                    items[i].parent = null;
                else
                    items[i].parent = items[pind];
            }
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            var c1 = (uint)items.Length;
            var p1 = (uint)w.Position + 6*4;
            w.WriteU32(p1 + 4 * c1);
            w.WriteU32(p1 + 8 * c1);
            w.WriteU32(p1);
            w.WriteU32(c1);
            w.WriteU32(id);
            w.WriteU32(c1);
            for (int i = 0; i < c1; i++)
                w.WriteU32(items[i].name_fnv);
            for (int i = 0; i < c1; i++)
                w.WriteU32(items[i].flags);
            for (int i = 0; i < c1; i++)
                w.WriteS32(items[i].parent==null ? -1 : items[i].parent.index);
        }
        public override int ComputeSize()
        {
            return 4 * 6 + 12 * items.Length;
        }
    };

    class RW4Skeleton : RW4Object
    {
        public const int type_code = 0x7000c;
        public Matrices4x3 mat3;
        public Matrices4x4 mat4;
        public RW4HierarchyInfo jointInfo;
        public UInt32 unk1;
        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            if (s.type_code != type_code) throw new ModelFormatException(r, "SK000", s.type_code);
            r.expect(0x400000, "SK001");
            r.expect(unk1=0x8d6da0, "SK002");  //< TODO: Not universal, but not sure other decoding is robust with other values
            var sn1 = r.ReadS32();
            var sn2 = r.ReadS32();
            var sn3 = r.ReadS32();

            m.Sections[sn1].GetObject(m, r, out mat3);
            m.Sections[sn2].GetObject(m, r, out jointInfo);
            m.Sections[sn3].GetObject(m, r, out mat4);
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            w.WriteU32(0x400000);
            w.WriteU32(unk1);
            w.WriteU32(mat3.section.number);
            w.WriteU32(jointInfo.section.number);
            w.WriteU32(mat4.section.number);
        }
        public override int ComputeSize() { return 4 * 5; }
    };


    class RW4Blob : RW4Object
    {
        public byte[] blob;
        public long required_position = -1;

        protected virtual bool Relocatable() { return false; }
        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            if (!Relocatable())
                this.required_position = r.Position;
            blob = r.ReadBytes((int)s.size);
        }
        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            if (this.required_position >= 0 && w.Position != this.required_position)
                throw new ModelFormatException(w, "Unable to move unknown section", null);
            w.Write(blob, 0, blob.Length);
        }
        public override int ComputeSize() { return blob.Length; }
    };

    class UnreferencedSection : RW4Blob { };

    class TextureBlob : RW4Blob {
        public const uint type_code = 0x10030;
        protected override bool Relocatable() { return true; }
    };

    struct JointPose : IRW4Struct
    {
        public const uint size = 12 * 4;
        public float qx, qy, qz, qs;   //< Quaternion rotation.  qx*qx + qy*qy + qz*qz + qs*qs == 1.0
        public float tx, ty, tz;       //< Vector translation
        public float sx, sy, sz;       //< Vector scale
        public float time;
        public uint Size()
        {
            return size;
        }
        public void Read(Stream s)
        {
            qx = s.ReadF32(); qy = s.ReadF32(); qz = s.ReadF32(); qs = s.ReadF32();
            tx = s.ReadF32(); ty = s.ReadF32(); tz = s.ReadF32();
            sx = s.ReadF32(); sy = s.ReadF32(); sz = s.ReadF32();
            s.expect(0, "JP001");
            time = s.ReadF32();
        }
        public void Write(Stream s)
        {
            s.WriteF32(qx); s.WriteF32(qy); s.WriteF32(qz); s.WriteF32(qs);
            s.WriteF32(tx); s.WriteF32(ty); s.WriteF32(tz);
            s.WriteF32(sx); s.WriteF32(sy); s.WriteF32(sz);
            s.WriteU32(0);
            s.WriteF32(time);
        }
    };

    class Anim : RW4Object
    {
        public const int type_code = 0x70001;
        public UInt32 skeleton_id;  // matches Skeleton.jointInfo.id (or something else sometimes!)
        public float length;   // seconds?
        public UInt32 flags;   // probably; generally 0-3
        public UInt32[] channel_names;  // duplicate Skeleton.jointInfo, I believe
        public JointPose[,] channel_frame_pose;
        public UInt32 padding;

        public override void Read(RW4Model m, RW4Section s, Stream r)
        {
            if (s.type_code != type_code) throw new ModelFormatException(r, "AN000", s.type_code);
            var p1 = r.ReadU32();
            var channels = r.ReadU32();
            skeleton_id = r.ReadU32();
            r.expect(0, "AN001");      //< Usually zero.. always <= 10.  Maybe a count of something?
            var p3 = r.ReadU32();
            var p4 = r.ReadU32();
            r.expect(channels, "AN002");
            r.expect(0, "AN003");
            length = r.ReadF32();
            r.expect(12, "AN010");
            flags = r.ReadU32();
            var p2 = r.ReadU32();

            if (p1 != r.Position) throw new ModelFormatException(r, "AN100", p1);
            if (p2 != p1 + channels * 4) throw new ModelFormatException(r, "AN101", p2);
            if (p3 != p2 + channels * 12) throw new ModelFormatException(r, "AN102", p3);

            //var keyframe_count = (p4 - p3) / (channels * JointPose.size * 3);
            uint keyframe_count = 0;

            channel_names = new UInt32[channels];
            for (int i = 0; i < channels; i++) channel_names[i] = r.ReadU32();

            for (int i = 0; i < channels; i++)
            {
                var p = r.ReadU32() - (12*4 + channels*4 + channels*12);
                var pose_size = r.ReadU32();
                var pose_components = r.ReadU32();
                if (pose_components != 0x601 || pose_size != JointPose.size)
                    throw new ModelFormatException(r, "AN200 Pose format not supported", pose_components);
                if (i == 1)
                    keyframe_count = p / JointPose.size;
                else if (i >= 1 && p != i * JointPose.size * keyframe_count)
                    throw new ModelFormatException(r, "AN201", null);
            }

            if (channels == 1) keyframe_count = (p4 - p3) / (channels * JointPose.size);  // Yuck!  This is just a guess.

            channel_frame_pose = new JointPose[ channels, keyframe_count ];
            for(int c=0; c<channels; c++)
                for (int f = 0; f < keyframe_count; f++)
                {
                    channel_frame_pose[c, f].Read(r);
                    if (channels == 1 && channel_frame_pose[c, f].time == length)
                    {
                        // We had to guess about the length, so now fix our guess :-(
                        keyframe_count = (uint)f + 1;
                        var t = new JointPose[channels, keyframe_count];
                        for (int c1 = 0; c1 < channels; c1++)
                            for (int f1 = 0; f1 < keyframe_count; f1++)
                                t[c1, f1] = channel_frame_pose[c1, f1];
                        channel_frame_pose = t;
                        break;
                    }
                }

            padding = p4 - p3 - channels * keyframe_count * JointPose.size;
            r.ReadPadding( padding );  //< Huge number of zeros very common
        }

        public override void Write(RW4Model m, RW4Section s, Stream w)
        {
            var channels = (uint)channel_names.Length;
            var keyframe_count = (uint)channel_frame_pose.GetLength(1);
            var p1 = (uint)w.Position + 12 * 4;

            w.WriteU32(p1);
            w.WriteU32(channels);
            w.WriteU32(skeleton_id);
            w.WriteU32(0);
            w.WriteU32(p1 + channels * 4 + channels * 12);
            w.WriteU32(p1 + channels * 4 + channels * 12 + channels * keyframe_count * JointPose.size + padding);
            w.WriteU32(channels);
            w.WriteU32(0);
            w.WriteF32(length);
            w.WriteU32(12);
            w.WriteU32(flags);
            w.WriteU32(p1 + channels * 4);

            w.WriteU32s(channel_names);
            for (int i = 0; i < channels; i++)
            {
                w.WriteU32((uint)(12*4 + channels*4 + channels*12 + i*JointPose.size*keyframe_count));
                w.WriteU32(JointPose.size);
                w.WriteU32(0x601);
            }
            for (int c = 0; c < channels; c++)
                for (int f = 0; f < keyframe_count; f++)
                    channel_frame_pose[c, f].Write(w);

            w.WritePadding(padding);
        }

        public override int ComputeSize()
        {
            var channels = channel_names.Length;
            var keyframe_count = channel_frame_pose.GetLength(1);
            return (int) (12 * 4 + channels * 4 + channels * 12 + channels * keyframe_count * JointPose.size + padding);
        }
    };

    class Animations : RW4Blob
    {
        public const int type_code = 0xff0001;
        protected override bool Relocatable() { return true; }
    };

    class ModelHandles : RW4Blob
    {
        public const int type_code = 0xff0000;
        protected override bool Relocatable() { return true; }  // almost certainly
    };

    class VertexFormat : RW4Blob
    {
        public const int type_code = 0x20004;
        protected override bool Relocatable() { return true; }  // almost certainly
    };

    public class RW4Section
    {
        public UInt32 number;
        public UInt32 pos;
        public UInt32 size;
        public UInt32 alignment;
        public UInt32 type_code;
        public UInt32 type_code_indirect;      //< Index into header.section_types table
        public RW4Object obj;
        public List<UInt32> fixup_offsets = new List<UInt32>();         //< Something is inserted here at load time??

        public void LoadHeader( Stream r, UInt32 number, UInt32 index_end ) {
            this.number = number;
            pos = r.ReadU32();
            r.expect(0, "H201");
            size = r.ReadU32();
            alignment = r.ReadU32();
            this.type_code_indirect = r.ReadU32();
            this.type_code = r.ReadU32();
            if (type_code == 0x10030) pos += index_end;
        }
        public void WriteHeader(Stream w, UInt32 index_end)
        {
            w.WriteU32(pos - (type_code==0x10030 ? index_end : 0));
            w.WriteU32(0);
            w.WriteU32(size);
            w.WriteU32(alignment);
            w.WriteU32(type_code_indirect);
            w.WriteU32(type_code);
        }
        public void GetObject<T>(RW4Model model, Stream r, out T o) 
        where T : RW4Object, new()
        {
            if (obj != null)
            {
                o = (T)obj;
                return;
            }
            o = new T();
            LoadObject(model, o, r);
        }
        public void LoadObject(RW4Model model, RW4Object o, Stream r)
        {
            if (obj != null) throw new ModelFormatException(r, "Attempt to decode section twice.", number);
            long start = r.Position;
            obj = o;
            o.section = this;
            r.Seek(pos, SeekOrigin.Begin);
            obj.Read(model, this, r);
            if (r.Position != pos + size)
                throw new ModelFormatException(r, "Section incompletely read.", number);
            var cs = obj.ComputeSize();
            if (cs != -1 && cs != size)
                throw new ModelFormatException(r, "Section size doesn't match computed size.", number);
            r.Seek(start, SeekOrigin.Begin);
        }
        public void Write(Stream w, RW4Model model)
        {
            w.WritePadding(pos - w.Position);
            obj.Write(model, this, w);
            if (w.Position != pos + size)
                throw new ModelFormatException(w, "Section incompletely written.", number);
        }
    };

    class RW4Header
    {
        //"\x89RW4w32\x00\r\n\x1a\n\x00 \x04\x00454\x00000\x00\x00\x00\x00\x00";
        readonly byte[] RW4_Magic = new byte[] { 137, 82, 87, 52, 119, 51, 50, 0, 13, 10, 26, 10, 0, 32, 4, 0, 52, 53, 52, 0, 48, 48, 48, 0, 0, 0, 0, 0 };
        readonly UInt32[] fixed_section_types = new UInt32[] { 0, 0x10030, 0x10031, 0x10032, 0x10010 };

        public RW4Model.FileTypes file_type;

        public UInt32 unknown_bits_030;
        public UInt32 section_index_begin;
        public UInt32 section_index_padding;
        public UInt32 section_index_end { get { return (uint)(section_index_begin + 6 * 4 * sections.Length + 8 * getFixupCount() + section_index_padding); } }
        public RW4Section[] sections = new RW4Section[0];

        private uint getFixupCount()
        {
            uint total = 0;
            foreach (var s in sections)
                total += (uint)s.fixup_offsets.Count;
            return total;
        }

        public void Read(System.IO.Stream r)
        {
            var b = r.ReadBytes(RW4_Magic.Length);
            for (int i = 0; i < b.Length; i++)
                if (b[i] != RW4_Magic[i]) throw new ModelFormatException(r, "Not a RW4 file", b);

            var file_type_code = r.ReadU32();
            if (file_type_code == 1)
                file_type = RW4Model.FileTypes.Model;
            else if (file_type_code == 0x04000000)
                file_type = RW4Model.FileTypes.Texture;
            else
                throw new ModelFormatException(r, "Unknown file type", file_type_code);
            var ft_const1 = (file_type == RW4Model.FileTypes.Model ? 16U : 4U);

            var section_count = r.ReadU32();
            r.expect(section_count, "H001 Section count not repeated");
            r.expect(ft_const1, "H002");
            r.expect(0, "H003");

            section_index_begin = r.ReadU32();
            var first_header_section_begin = r.ReadU32();  // Always 0x98?
            r.expect(new UInt32[] {0, 0, 0}, "H010");
            var section_index_end1 = r.ReadU32();
            r.expect(ft_const1, "H011");
            //this.unknown_size_or_offset_013 = r.ReadU32();
            var file_size = r.ReadU32() + section_index_end1;
            if (r.Length != file_size) throw new ModelFormatException(r, "H012", file_size);  //< TODO

            r.expect(new UInt32[]{4,0,1,0,1}, "H020");

            this.unknown_bits_030 = r.ReadU32();

            r.expect(new UInt32[] { 4, 0, 1, 0, 1 }, "H040");
            r.expect(new UInt32[] { 0, 1, 0, 0, 0, 0, 0 }, "H041");

            if (r.Position != 0x98) throw new ModelFormatException(r, "H099", r.Position);

            r.Seek(first_header_section_begin, SeekOrigin.Begin);
            r.expect(0x10004, "H140");
            // Offsets of header sections relative to the 0x10004.  4, 12, 28, 28 + (12+4*section_type_count), ... + 36, ... + 28
            var offsets = new UInt32[6];
            for (int i = 0; i < offsets.Length; i++)
                offsets[i] = r.ReadU32() + first_header_section_begin;

            // A list of section types in the file?  If so, redundant with section index
            if (offsets[2] != r.Position) throw new ModelFormatException(r, "H145", r.Position);
            r.expect(0x10005, "H150");
            var count = r.ReadU32();
            r.expect(12, "H151");
            var section_types = new UInt32[count];
            for (int i = 0; i < count; i++)
                section_types[i] = r.ReadU32();

            if (offsets[3] != r.Position) throw new ModelFormatException(r, "H146", r.Position);
            r.expect(0x10006, "H160");
            // TODO: I think this is actually a variable length structure, with 12 byte header and 3 being the length in qwords
            r.expect(new UInt32[] { 3, 0x18, file_type_code, 0xffb00000, file_type_code, 0, 0, 0 }, "H161" );

            if (offsets[4] != r.Position) throw new ModelFormatException(r, "H147", r.Position);
            r.expect(0x10007, "H170");
            var fixup_count = r.ReadU32();
            r.expect(0, "H171");
            r.expect(0, "H172");
            // Fixup index always immediately follows section index
            r.expect(section_index_begin + section_count * 24 + fixup_count * 8, "H173");
            r.expect(section_index_begin + section_count * 24, "H174");
            r.expect(fixup_count, "H176");

            if (offsets[5] != r.Position) throw new ModelFormatException(r, "H148", r.Position);
            r.expect(0x10008, "H180");
            r.expect(new UInt32[] { 0, 0 }, "H181");

            r.Seek(section_index_begin,SeekOrigin.Begin);
            this.sections = new RW4Section[section_count];
            for (uint i = 0; i < section_count; i++)
            {
                this.sections[i] = new RW4Section();
                this.sections[i].LoadHeader(r, i, section_index_end1);
            }
            for (int i = 0; i < fixup_count; i++)
            {
                var sind = r.ReadU32();
                var offset = r.ReadU32();
                this.sections[sind].fixup_offsets.Add(offset);
            }
            section_index_padding = section_index_end1 - (uint)r.Position;
            r.ReadPadding(section_index_padding);

            bool[] used = new bool[section_types.Length];
            foreach (var s in sections) {
                used[s.type_code_indirect] = true;
                if (section_types[s.type_code_indirect] != s.type_code)
                    throw new ModelFormatException(r, "H300", s.type_code_indirect);
            }
            for (int i = 0; i < fixed_section_types.Length; i++)
                if (section_types[i] != fixed_section_types[i])
                    throw new ModelFormatException(r, "H301", i);
            for (int i = fixed_section_types.Length; i < section_types.Length; i++)
                if (!used[i])
                    throw new ModelFormatException(r, "H302", section_types[i]);
        }

        private UInt32[] getSectionTypes()
        {
            Dictionary<UInt32, UInt32> stype = new Dictionary<uint, uint>();
            for (uint i = 0; i < fixed_section_types.Length; i++) stype[fixed_section_types[i]] = i;
            for (int i = 0; i < sections.Length; i++)
                if (!stype.TryGetValue(sections[i].type_code, out sections[i].type_code_indirect))
                    stype.Add(sections[i].type_code, sections[i].type_code_indirect = (uint)stype.Count);
            return (from i in stype orderby i.Value select i.Key).ToArray();
        }

        public void Write(Stream w)
        {
            var section_types = getSectionTypes();

            uint file_type_code = file_type == RW4Model.FileTypes.Model ? 1U : 0x04000000U;

            w.Write(RW4_Magic, 0, RW4_Magic.Length);
            w.WriteU32(file_type_code);
            w.WriteU32((uint)sections.Length);
            w.WriteU32((uint)sections.Length);
            var ft_const1 = (file_type == RW4Model.FileTypes.Model ? 16U : 4U);
            w.WriteU32(ft_const1);
            w.WriteU32(0);

            w.WriteU32(section_index_begin);
            w.WriteU32(0x98);                            // pointer to section 10004
            w.WriteU32s(new UInt32[] { 0, 0, 0 });
            w.WriteU32((uint)section_index_end);
            w.WriteU32(ft_const1);

            var file_end = section_index_end;
            foreach (var s in sections)
                if (s.pos + s.size > file_end) file_end = s.pos + s.size;
            w.WriteU32(file_end - section_index_end);

            w.WriteU32s(new UInt32[] { 4, 0, 1, 0, 1 });

            w.WriteU32(this.unknown_bits_030);

            w.WriteU32s(new UInt32[] { 4, 0, 1, 0, 1 });
            w.WriteU32s(new UInt32[] { 0, 1, 0, 0, 0, 0, 0 });

            if (w.Position != 0x98) throw new ModelFormatException(w, "WH099", w.Position);

            w.WriteU32(0x10004);
            var hs_sizes = new UInt32[] { 4, 8, 16, 12 + 4 * (uint)section_types.Length, 36, 28 };
            for (int i = 1; i < hs_sizes.Length; i++)
                hs_sizes[i] += hs_sizes[i - 1];
            w.WriteU32s(hs_sizes);

            w.WriteU32(0x10005);
            w.WriteU32((uint)section_types.Length);
            w.WriteU32(12);
            w.WriteU32s(section_types);

            w.WriteU32(0x10006);
            w.WriteU32s(new UInt32[] { 3, 0x18, file_type_code, 0xffb00000, file_type_code, 0, 0, 0 });

            w.WriteU32(0x10007);
            int fixup_count = 0;
            foreach (var s in sections)
                fixup_count += s.fixup_offsets.Count;
            w.WriteU32((uint)fixup_count);
            w.WriteU32(0);
            w.WriteU32(0);
            w.WriteU32((uint)(section_index_begin + sections.Length * 24 + fixup_count * 8));
            w.WriteU32((uint)(section_index_begin + sections.Length * 24));
            w.WriteU32((uint)fixup_count);

            w.WriteU32(0x10008);
            w.WriteU32s(new UInt32[] { 0, 0 });
        }
        public void WriteIndex(Stream w)
        {
            if (w.Position != this.section_index_begin) throw new ModelFormatException(w, "WH200", null);
            foreach (var s in sections)
                s.WriteHeader(w, (uint)section_index_end);
            foreach (var s in sections)
                foreach (var f in s.fixup_offsets)
                {
                    w.WriteU32(s.number);
                    w.WriteU32(f);
                }
            w.WritePadding(section_index_end - w.Position);
            if (w.Position != section_index_end) throw new ModelFormatException(w, "WH299", w.Position);
        }
        public UInt32 GetHeaderEnd()
        {
            return 0x98 + 104 + 4 * (uint)getSectionTypes().Length + 12;
        }
    };

    public class RW4Model
    {
        RW4Header header;

        public enum FileTypes { Model, Texture };

        public IList<RW4Section> Sections { get { return header.sections; } }
        public FileTypes FileType { get { return header.file_type; } set { header.file_type = value; } }

        public RW4Model() { }

        public IList<RW4Object> GetObjects(UInt32 type_code)
        {
            return (from s in header.sections where s.type_code == type_code select s.obj).ToList();
        }

        public void AddObject(RW4Object obj, UInt32 type_code)
        {
            obj.section = InsertSection(header.sections.Length);
            obj.section.type_code = type_code;
            obj.section.obj = obj;
            obj.section.alignment = 0x10;  // by default
        }

        public void RemoveSection(RW4Section section)
        {
            if (header.sections[section.number] != section) throw new ArgumentException("Attempt to remove invalid section.");
            var new_sections = new RW4Section[header.sections.Length - 1];
            for (int i = 0; i < section.number; i++)
                new_sections[i] = header.sections[i];
            for (int i = (int)section.number; i < new_sections.Length; i++ )
            {
                new_sections[i] = header.sections[i + 1];
                new_sections[i].number = (uint)i;
            }
            header.sections = new_sections;
            section.number = 0xffffffff;
        }

        public RW4Section InsertSection(int index)
        {
            var new_sections = new RW4Section[header.sections.Length + 1];
            for (int i = 0; i < index; i++)
                new_sections[i] = header.sections[i];
            for (int i = index + 1; i < new_sections.Length; i++)
            {
                new_sections[i] = header.sections[i - 1];
                new_sections[i].number = (uint)i;
            }
            var s = new_sections[index] = new RW4Section();
            s.number = (uint)index;
            header.sections = new_sections;
            return s;
        }

        void Pack()
        {
            foreach (var s in header.sections)
            {
                var cs = s.obj.ComputeSize();
                if (cs != -1)
                    s.size = (uint)cs;
            }

            var sections = header.sections;   //(from s in header.sections orderby s.pos select s).ToArray();

            uint p;
            p = header.GetHeaderEnd();
            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i].type_code == 0x10030) continue;
                var np = (p + sections[i].alignment - 1) & ~(sections[i].alignment - 1);
                if (sections[i].pos != np)
                {
                    sections[i].pos = np;
                }
                p = sections[i].pos + sections[i].size;
            }
            header.section_index_begin = p;
            p = header.section_index_end;
            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i].type_code != 0x10030) continue;
                var np = (p + sections[i].alignment - 1) & ~(sections[i].alignment - 1);
                if (sections[i].pos != np)
                {
                    //System.Console.WriteLine(String.Format("Moving section {0} 0x{1:x} from 0x{2:x} to 0x{3:x}.", i, sections[i].type_code, sections[i].pos, np));
                    sections[i].pos = np;
                }
                p = sections[i].pos + sections[i].size;
            }

        }

        public void New()
        {
            header = new RW4Header();
        }

        public void Read(System.IO.Stream r)
        {
            header = new RW4Header();
            header.Read(r);

            foreach (var section in header.sections)
            {
                switch (section.type_code)
                {
                    case RW4Mesh.type_code:
                        {
                            RW4Mesh t;
                            section.GetObject(this, r, out t);
                            break;
                        }
                    case RW4Material.type_code:
                        {
                            RW4Material t;
                            section.GetObject(this, r, out t);
                            break;
                        }
                    case RW4Skeleton.type_code:
                        {
                            RW4Skeleton t;
                            section.GetObject(this, r, out t);
                            break;
                        }
                    case Texture.type_code:
                        {
                            Texture t;
                            section.GetObject(this, r, out t);
                            break;
                        }
                    case RW4HierarchyInfo.type_code:
                        {
                            RW4HierarchyInfo t;
                            section.GetObject(this, r, out t);
                            break;
                        }
                    case RWMeshMaterialAssignment.type_code:
                        {
                            RWMeshMaterialAssignment t;
                            section.GetObject(this, r, out t);
                            break;
                        }
                    case RW4BBox.type_code:
                        {
                            RW4BBox b;
                            section.GetObject(this, r, out b);
                            break;
                        }
                    case ModelHandles.type_code:
                        {
                            ModelHandles t;
                            section.GetObject(this, r, out t);
                            break;
                        }
                    case Anim.type_code:
                        {
                            Anim t;
                            section.GetObject(this, r, out t);
                            break;
                        }
                    case Animations.type_code:
                        {
                            Animations t;
                            section.GetObject(this, r, out t);
                            break;
                        }
                    
                    /*case Matrices4x4.type_code:
                        {
                            Matrices4x4 t;
                            section.GetObject(this, r, out t);
                            break;
                        }
                    case Matrices4x3.type_code:
                        {
                            Matrices4x3 t;
                            section.GetObject(this, r, out t);
                            break;
                        }*/
                }
            }
            foreach (var section in header.sections)
                if (section.obj == null)
                    section.LoadObject( this, new UnreferencedSection(), r );
        }
        public void Write(Stream w)
        {
            Pack();
            header.Write(w);
            var sections = (from s in header.sections orderby s.pos select s).ToArray();
            bool wrote_index = false;
            for(int i=0; i<sections.Length; i++)
            {
                sections[i].Write(w, this);
                if (wrote_index) continue;
                if (i + 1 == sections.Length ||
                    sections[i + 1].pos >= header.section_index_end)
                {
                    w.WritePadding(header.section_index_begin - w.Position);
                    header.WriteIndex(w);
                    wrote_index = true;
                }
            }
        }

        public void Test()
        {

        }
    }
}

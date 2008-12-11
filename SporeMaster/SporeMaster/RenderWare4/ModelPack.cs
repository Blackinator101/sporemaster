using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Gibbed.Spore.Helpers;

namespace SporeMaster.RenderWare4
{
    public class DDSPack
    {
        public DDSPack(string inputFileName, Stream output)
        {
            using (var src = File.OpenRead(inputFileName))
            {
                var model = new RW4Model();
                model.New();
                pack(src, model);
                model.Write(output);
            }
        }
        void pack(Stream src, RW4Model model)
        {
            // Read the DDS
            src.expect(0x20534444, "DDS000");  // 'DDS '
            int headerSize = src.ReadS32();
            if (headerSize < 0x7C) throw new ModelFormatException(src, "DDS001", headerSize);
            var flags = src.ReadU32();
            var height = src.ReadS32();
            var width = src.ReadS32();
            var pitchOrLinearSize = src.ReadU32();
            src.expect(0, "DDS002");  // < depth
            var mipmaps = src.ReadS32();

            src.Seek(src.Position + 11 * 4, SeekOrigin.Begin);
            var pfsize = src.ReadS32();
            if (pfsize < 32) throw new ModelFormatException(src, "DDS011", pfsize);
            var pf_flags = src.ReadU32();
            if ((pf_flags & 4) == 0) throw new ModelFormatException(src, "DDS012", pf_flags);
            var fourcc = src.ReadU32();
            if (fourcc != Texture.DXT5)
                throw new NotSupportedException("Texture packing currently only supports DXT5 compressed input textures.");

            src.Seek(headerSize+4, SeekOrigin.Begin);
            var sizes = (from i in Enumerable.Range(0, mipmaps)
                            select Math.Max(width>>i,4)*Math.Max(height>>i,4)  // DXT5: 16 bytes per 4x4=16 pixels
                            ).ToArray();
            var all_mipmaps = new byte[ sizes.Sum() ];
            for (int offset=0, i = 0; i < mipmaps; i++) {
                if (src.Read( all_mipmaps, offset, sizes[i] ) != sizes[i])
                    throw new ModelFormatException(src, "Unexpected EOF reading .DDS file", null);
                offset += sizes[i];
            }

            // Build the RW4
            model.FileType = RW4Model.FileTypes.Texture;
            var texture = new Texture()
            {
                width = (ushort)width,
                height = (ushort)height,
                mipmapInfo = (uint)(0x100 * mipmaps + 0x08),
                textureType = fourcc,
                texData = new TextureBlob { blob = all_mipmaps },
                unk1 = 0
            };
            model.AddObject(texture.texData, TextureBlob.type_code);
            model.AddObject(texture, Texture.type_code);
        }
    };

    public class ModelPack
    {
        public ModelPack(string inputFileName, Stream output)
        {
            var src_model = new OgreXmlReader(inputFileName);

            var model = new RW4Model();
            model.New();

            pack(src_model, model);

            model.Write(output);
        }

        void pack(OgreXmlReader src_model, RW4Model model)
        {
            model.FileType = RW4Model.FileTypes.Model;

            RW4Skeleton skel = new RW4Skeleton();
            skel.unk1 = 0x8d6da0;
            skel.mat3 = new Matrices4x3()
            {
                items = new Mat4x3[] { new Mat4x3() { m = new float[] { 
                1.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 1.0f, 0.0f
            } } }
            };
            skel.mat4 = new Matrices4x4()
            {
                items = new Mat4x4[] { new Mat4x4() { m = new float[] {
/*                1.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 1.0f, 0.0f,*/
                0.0f, 1.0f, 0.0f, 0.0f,
               -1.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 1.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 0.0f 
            } } }
            };
            skel.jointInfo = new RW4HierarchyInfo()
            {
                id = "skeleton1".FNV(),
                items = new RW4HierarchyInfo.Item[] {
                            new RW4HierarchyInfo.Item { index=0, name_fnv = "joint1".FNV(), flags=1, parent=null }
                        },
            };

            var anim = new Anim()
            {
                skeleton_id = skel.jointInfo.id,
                flags = 3,
                length = 1.25f,
                channel_names = (from j in skel.jointInfo.items select j.name_fnv).ToArray(),
            };
            anim.channel_frame_pose = new JointPose[,] {
                {
                    new JointPose{ qx=0, qy=0, qz=0, qs=1,
                                   tx=0, ty=0, tz=0,
                                   sx=1, sy=1, sz=1,
                                   time=1.25f }
                }
            };

            var mesh = new RW4Mesh()
            {
                vertices = new RW4VertexArray(),
                triangles = new RW4TriangleArray(),
            };
            mesh.vertices.unk2 = 0;  //< ?
            mesh.vertices.format = new VertexFormat() { blob = RW4Garbage.vertex_format };
            mesh.vertices.vertices = new Buffer<Vertex>(0);
            mesh.vertices.vertices.Assign(src_model.vertices);
            mesh.triangles.unk1 = 0;  //< ?
            mesh.triangles.triangles = new Buffer<Triangle>(0);
            mesh.triangles.triangles.Assign(src_model.triangles);

            var texture = new Texture()
            {
                width = 64,
                height = 64,
                mipmapInfo = 0x708,
                textureType = Texture.DXT5,
                unk1 = 0,
                texData = new TextureBlob() { blob = new byte[5488] }
            };
            var texture_format = new RW4TexMetadata()
            {
                unk_data_1 = RW4Garbage.texture_format_1,
                unk_data_2 = RW4Garbage.texture_format_2,
                texture = texture
            };
            var meshMaterial = new RWMeshMaterialAssignment()
            {
                mesh = mesh,
                mat = new RW4TexMetadata[] { texture_format }
            };

            model.AddObject(anim, Anim.type_code);
            model.AddObject(mesh.vertices.format, VertexFormat.type_code);
            model.AddObject(skel.mat4, Matrices4x4.type_code);
            model.AddObject(skel.mat3, Matrices4x3.type_code);
            skel.mat3.section.fixup_offsets.Add(16);   // !?
            model.AddObject(skel, RW4Skeleton.type_code);
            model.AddObject(mesh.triangles.triangles, Buffer<Triangle>.type_code);
            model.AddObject(mesh.triangles, RW4TriangleArray.type_code);
            model.AddObject(mesh, RW4Mesh.type_code);
            model.AddObject(texture_format, RW4TexMetadata.type_code);
            model.AddObject(meshMaterial, RWMeshMaterialAssignment.type_code);
            model.AddObject(skel.jointInfo, RW4HierarchyInfo.type_code);

            model.AddObject(mesh.vertices.vertices, Buffer<Vertex>.type_code);
            model.AddObject(mesh.vertices, RW4VertexArray.type_code);

            model.AddObject(texture.texData, TextureBlob.type_code);
            model.AddObject(texture, Texture.type_code);
        }
    }
}

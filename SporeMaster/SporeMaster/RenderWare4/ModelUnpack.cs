using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Gibbed.Spore.Helpers;

namespace SporeMaster.RenderWare4
{
    public class ModelUnpack
    {
        string type;
        public string Type { get { return type; } }
        public ModelUnpack(Stream input, string outputPath)
        {
            var model = new RW4Model();
            model.Read(input);
            if (model.FileType == RW4Model.FileTypes.Model)
                unpackModel(model, outputPath + "\\model.mesh.xml");
            else
                unpackTexture(model, outputPath + "\\texture.dds");
        }

        void unpackModel(RW4Model model, string outputFileName)
        {
            var meshes = model.GetObjects(RW4Mesh.type_code);
            if (meshes.Count != 1) throw new NotSupportedException("Only exactly one mesh supported.");
            var mesh = meshes[0] as RW4Mesh;

            if (mesh.vertices == null) throw new NotSupportedException("Only models with a vertex buffer can be unpacked.");

            new OgreXmlWriter(mesh.vertices.vertices.ToArray(), 
                              mesh.triangles.triangles.ToArray(), 
                              outputFileName);
            type = "mesh";
        }

        void unpackTexture(RW4Model model, string outputFileName)
        {
            var textures = model.GetObjects(Texture.type_code);
            if (textures.Count != 1)
                throw new NotSupportedException("Only exactly one texture supported in a texture rw4.");
            var texture = textures[0] as Texture;

            using (var stream = File.Create(outputFileName))
            {
                stream.WriteU32(0x20534444);  // 'DDS '
                stream.WriteU32(0x7C);  // header size
                stream.WriteU32(0xA1007);  // flags: 
                stream.WriteU32(texture.height);
                stream.WriteU32(texture.width);
                stream.WriteU32((uint)texture.height * (uint)texture.width);  // size of top mipmap level... at least in DXT5 for >4x4
                stream.WriteU32(0);
                stream.WriteU32(texture.mipmapInfo / 0x100);
                for (int i = 0; i < 11; i++)
                    stream.WriteU32(0);

                // pixel format
                stream.WriteU32(32);
                stream.WriteU32(4);  // DDPF_FOURCC?
                stream.WriteU32(texture.textureType);
                stream.WriteU32(32);
                stream.WriteU32(0xff0000);
                stream.WriteU32(0x00ff00);
                stream.WriteU32(0x0000ff);
                stream.WriteU32(0xff000000);
                stream.WriteU32(0);  // 0x41008
                for (int i = 0; i < 4; i++)
                    stream.WriteU32(0);

                stream.Write(texture.texData.blob, 0, texture.texData.blob.Length);
            }
            type = "texture";
        }
    }
}

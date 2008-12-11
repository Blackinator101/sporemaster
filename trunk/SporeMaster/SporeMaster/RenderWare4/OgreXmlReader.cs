using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;

namespace SporeMaster.RenderWare4
{
    class OgreXmlReader
    {
        public List<Vertex> vertices = new List<Vertex>();
        public List<Triangle> triangles = new List<Triangle>();

        void DecodeVertexBuffers(XElement geom)   // either <sharedgeometry> or <geometry>
        {
            if (geom == null) return;

            // In vertexbuffer, ignored: all attributes
            // In vertex, ignored: binormal?, colour_diffuse?, colour_specular?

            int v;
            var vertices = new Vertex[int.Parse(geom.Attribute("vertexcount").Value)];
            // Default values for things we don't support importing yet... these are actually supported by Ogre!
            for (v = 0; v < vertices.Length; v++)
            {
                vertices[v].packed_bone_indices = 0;     //   0,   0,   0,   0
                vertices[v].packed_bone_weights = 0xff;  // 1.0,   0,   0,   0
            }
            var vertex_elements = geom.Elements("vertexbuffer").Elements("vertex");

            v = 0;
            foreach (var p in vertex_elements.Elements("position"))
            {
                vertices[v].x = float.Parse(p.Attribute("x").Value);
                vertices[v].y = float.Parse(p.Attribute("y").Value);
                vertices[v].z = float.Parse(p.Attribute("z").Value);
                v++;
            }
            if (v != vertices.Length) throw new ModelFormatException(null, "Missing vertex position data.", v);

            v = 0;
            foreach (var p in vertex_elements.Elements("normal"))
            {
                vertices[v].normal = Vertex.PackNormal(
                    float.Parse(p.Attribute("x").Value),
                    float.Parse(p.Attribute("y").Value),
                    float.Parse(p.Attribute("z").Value));
                v++;
            }
            if (v != vertices.Length) throw new ModelFormatException(null, "Missing vertex normal data.", v);

            v = 0;
            foreach (var p in vertex_elements.Elements("tangent"))
            {
                vertices[v].tangent = Vertex.PackNormal(
                    float.Parse(p.Attribute("x").Value),
                    float.Parse(p.Attribute("y").Value),
                    float.Parse(p.Attribute("z").Value));
                v++;
            }
            if (v != vertices.Length) throw new ModelFormatException(null, "Missing vertex tangent data.", v);

            v = 0;
            // If there are multiple texture coordinates in the same vertex buffer, take just the first one for each
            // vertex.  If there are multiple vertex buffers with texture coordinates, we will break out after processing
            // the first one.  TODO: Better error checking?
            foreach (var p in (from vx in vertex_elements select vx.Element("texcoord")))
            {
                if (v >= vertices.Length) break;  //< Multiple vertex buffers?
                vertices[v].u = float.Parse(p.Attribute("u").Value);
                vertices[v].v = float.Parse(p.Attribute("v").Value);
                v++;
            }
            if (v != vertices.Length) throw new ModelFormatException(null, "Missing vertex texture coordinates.", v);

            this.vertices.AddRange(vertices);
        }

        public OgreXmlReader(string uri)
        {
            var mesh = XElement.Load(uri);

            // Ignored: skeletonlink?, boneassignments?, levelofdetail?, submeshnames?, poses?, animations?, extremes?

            DecodeVertexBuffers(mesh.Element("sharedgeometry"));

            foreach (var submesh in mesh.Element("submeshes").Elements("submesh"))
            {
                // Ignored: textures?, boneassignments?, @material, @use32bitindexes?

                var optype = submesh.Attribute("operationtype");
                if (optype != null && optype.Value != "triangle_list")
                    throw new ModelFormatException(null, "Unsupported face type", submesh.Attribute("operationtype").Value);

                uint first_vertex = 0;
                var shared = submesh.Attribute("sharedgeometry");
                if (shared == null || shared.Value != "true")
                    first_vertex = (uint)this.vertices.Count;

                // Ignored: faces.@count
                var triangles = from face in submesh.Element("faces").Elements("face")
                                select new Triangle
                                {
                                    i = first_vertex + uint.Parse(face.Attribute("v1").Value),
                                    j = first_vertex + uint.Parse(face.Attribute("v2").Value),
                                    k = first_vertex + uint.Parse(face.Attribute("v3").Value)
                                };
                this.triangles.AddRange(triangles);

                DecodeVertexBuffers(submesh.Element("geometry"));
            }
        }
    }
}

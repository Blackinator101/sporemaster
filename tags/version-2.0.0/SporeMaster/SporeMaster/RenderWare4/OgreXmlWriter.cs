using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SporeMaster.RenderWare4
{
    class OgreXmlWriter
    {
        public OgreXmlWriter(Vertex[] vertices, Triangle[] triangles, string uri)
        {
            // Look, ma, no semicolons!
            var mesh = new XElement("mesh", 
                new XElement("submeshes",
                    new XElement("submesh",
                        new XAttribute("material","BaseWhite"),
                        new XAttribute("usesharedvertices","false"),
                        new XElement("faces",
                            new XAttribute("count", triangles.Length.ToString()),
                            from t in triangles 
                                select new XElement("face",
                                    new XAttribute("v1", t.i),
                                    new XAttribute("v2", t.j),
                                    new XAttribute("v3", t.k))
                            ),
                        new XElement("geometry",
                            new XAttribute("vertexcount", vertices.Length.ToString()),
                            new XElement("vertexbuffer",
                                new XAttribute("positions", "true"),
                                new XAttribute("normals", "true"),
                                new XAttribute("texture_coords", 1),
                                new XAttribute("texture_coord_dimensions_0", 2),
                                new XAttribute("tangents", "true"),
                                from v in vertices
                                    select new XElement("vertex",
                                        new XElement("position",
                                            new XAttribute("x", v.x),
                                            new XAttribute("y", v.y),
                                            new XAttribute("z", v.z)),
                                        new XElement("normal",
                                            new XAttribute("x", Vertex.UnpackNormal(v.normal, 0)),
                                            new XAttribute("y", Vertex.UnpackNormal(v.normal, 1)),
                                            new XAttribute("z", Vertex.UnpackNormal(v.normal, 2))),
                                        new XElement("texcoord",
                                            new XAttribute("u", v.u),
                                            new XAttribute("v", v.v)),
                                        new XElement("tangent",
                                            new XAttribute("x", Vertex.UnpackNormal(v.tangent, 0)),
                                            new XAttribute("y", Vertex.UnpackNormal(v.tangent, 1)),
                                            new XAttribute("z", Vertex.UnpackNormal(v.tangent, 2)))
                                        )
                                )
                            ),
                        new XElement("boneassignments",
                            from pair in vertices.Select((v,index)=>new KeyValuePair<Vertex,int>(v,index))
                                let v = pair.Key
                                let i = pair.Value
                                from a in (from ind in Enumerable.Range(0,4)
                                   let bone = (v.packed_bone_indices >> (ind * 8)) & 0xff
                                   let weight = (v.packed_bone_weights >> (ind * 8)) & 0xff
                                   where weight != 0
                                   select new { bone, weight=weight/255.0f })
                                select new XElement("vertexboneassignment",
                                    new XAttribute("vertexindex", i),
                                    new XAttribute("boneindex", a.bone),
                                    new XAttribute("weight", a.weight))
                            )
                        )
                    )
                );
            mesh.Save(uri);
        }
    }
}

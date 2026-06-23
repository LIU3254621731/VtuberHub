using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace VtuberHubStudio.ModelViewer
{
    public static class ObjExporter
    {
        public static void Export(string objPath, Model3DGroup group)
        {
            Export(objPath, group, null);
        }

        // 新增：支持根据名称映射输出规范化的对象名（自动重编码）
        public static void Export(string objPath, Model3DGroup group, Dictionary<string, GeometryModel3D> nameMap)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            var mtlPath = Path.ChangeExtension(objPath, ".mtl");
            var mtlName = Path.GetFileName(mtlPath);

            var geoms = EnumerateGeometryModels(group).ToList();
            using var objWriter = new StreamWriter(objPath);
            using var mtlWriter = new StreamWriter(mtlPath);

            objWriter.WriteLine("# Exported by VtuberHubStudio OBJ Exporter");
            objWriter.WriteLine($"mtllib {mtlName}");

            int vertexOffset = 0;
            int normalOffset = 0;

            for (int i = 0; i < geoms.Count; i++)
            {
                var gm = geoms[i];
                var mesh = gm.Geometry as MeshGeometry3D;
                if (mesh == null) continue;
                string matName = $"mat_{i}";

                // Write material into MTL
                WriteMaterial(mtlWriter, matName, gm.Material);

                // Write vertices
                foreach (var p in mesh.Positions)
                {
                    objWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "v {0} {1} {2}", p.X, p.Y, p.Z));
                }
                // Write normals if available
                bool hasNormals = mesh.Normals != null && mesh.Normals.Count == mesh.Positions.Count;
                if (hasNormals)
                {
                    foreach (var n in mesh.Normals)
                    {
                        objWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "vn {0} {1} {2}", n.X, n.Y, n.Z));
                    }
                }

                objWriter.WriteLine($"usemtl {matName}");
                var groupName = ResolveGroupName(i, gm, nameMap);
                objWriter.WriteLine($"g {groupName}");

                // Faces (triangles)
                for (int t = 0; t < mesh.TriangleIndices.Count; t += 3)
                {
                    int a = mesh.TriangleIndices[t] + 1 + vertexOffset;
                    int b = mesh.TriangleIndices[t + 1] + 1 + vertexOffset;
                    int c = mesh.TriangleIndices[t + 2] + 1 + vertexOffset;
                    if (hasNormals)
                    {
                        int an = mesh.TriangleIndices[t] + 1 + normalOffset;
                        int bn = mesh.TriangleIndices[t + 1] + 1 + normalOffset;
                        int cn = mesh.TriangleIndices[t + 2] + 1 + normalOffset;
                        objWriter.WriteLine($"f {a}//{an} {b}//{bn} {c}//{cn}");
                    }
                    else
                    {
                        objWriter.WriteLine($"f {a} {b} {c}");
                    }
                }

                vertexOffset += mesh.Positions.Count;
                if (hasNormals) normalOffset += mesh.Normals.Count;
            }
        }

        private static string ResolveGroupName(int index, GeometryModel3D gm, Dictionary<string, GeometryModel3D> nameMap)
        {
            if (nameMap == null || nameMap.Count == 0) return $"object_{index}";
            var candidates = nameMap.Where(kv => ReferenceEquals(kv.Value, gm)).Select(kv => kv.Key).Distinct().ToList();
            if (candidates.Count == 0) return $"object_{index}";
            // 选择最短的（通常是规范化名，如 head/hips/leftupperarm 等）
            var name = candidates.OrderBy(s => s.Length).First();
            // OBJ 组名不包含空白
            name = new string(name.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
            return string.IsNullOrWhiteSpace(name) ? $"object_{index}" : name;
        }

        private static IEnumerable<GeometryModel3D> EnumerateGeometryModels(Model3D model)
        {
            if (model is GeometryModel3D gm)
            {
                yield return gm;
            }
            else if (model is Model3DGroup grp)
            {
                foreach (var child in grp.Children)
                {
                    foreach (var g in EnumerateGeometryModels(child))
                        yield return g;
                }
            }
        }

        private static void WriteMaterial(StreamWriter mtl, string name, Material material)
        {
            mtl.WriteLine($"newmtl {name}");
            mtl.WriteLine("Ns 96.078431");
            mtl.WriteLine("Ka 0.000000 0.000000 0.000000");
            // Diffuse from material
            if (material is MaterialGroup mg)
            {
                var diff = mg.Children.OfType<DiffuseMaterial>().FirstOrDefault();
                if (diff != null) WriteDiffuse(mtl, diff);
                var spec = mg.Children.OfType<SpecularMaterial>().FirstOrDefault();
                if (spec != null)
                {
                    var c = GetColorFromBrush(spec.Brush);
                    mtl.WriteLine(string.Format(CultureInfo.InvariantCulture, "Ks {0} {1} {2}", c.R / 255.0, c.G / 255.0, c.B / 255.0));
                }
                else
                {
                    mtl.WriteLine("Ks 0.000000 0.000000 0.000000");
                }
            }
            else if (material is DiffuseMaterial dm)
            {
                WriteDiffuse(mtl, dm);
                mtl.WriteLine("Ks 0.000000 0.000000 0.000000");
            }
            else
            {
                mtl.WriteLine("Kd 0.8 0.8 0.8");
                mtl.WriteLine("Ks 0.000000 0.000000 0.000000");
            }
            mtl.WriteLine();
        }

        private static void WriteDiffuse(StreamWriter mtl, DiffuseMaterial dm)
        {
            if (dm.Brush is ImageBrush ib)
            {
                var src = ib.ImageSource as BitmapImage;
                var path = src?.UriSource?.LocalPath;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    mtl.WriteLine("Kd 1.000000 1.000000 1.000000");
                    mtl.WriteLine($"map_Kd {Path.GetFileName(path)}");
                    // Try to copy texture next to OBJ
                    // Note: external copy omitted here to avoid permissions issues
                }
                else
                {
                    var c = Colors.White;
                    mtl.WriteLine(string.Format(CultureInfo.InvariantCulture, "Kd {0} {1} {2}", c.R / 255.0, c.G / 255.0, c.B / 255.0));
                }
            }
            else
            {
                var c = GetColorFromBrush(dm.Brush);
                mtl.WriteLine(string.Format(CultureInfo.InvariantCulture, "Kd {0} {1} {2}", c.R / 255.0, c.G / 255.0, c.B / 255.0));
            }
        }

        private static Color GetColorFromBrush(Brush brush)
        {
            if (brush is SolidColorBrush scb) return scb.Color;
            return Colors.White;
        }
    }
}
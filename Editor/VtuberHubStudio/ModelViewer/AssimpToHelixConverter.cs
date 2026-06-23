using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using Assimp;
using System.IO;

namespace VtuberHubStudio.ModelViewer
{
    // 轻量级 Assimp → WPF3D 转换器（网格/法线/UV + 基础材质与高光）
    public static class AssimpToHelixConverter
    {
        public static Model3DGroup ToModel3DGroup(Scene scene) => ToModel3DGroup(scene, null);

        // 新增：返回网格名称到 GeometryModel3D 的映射，便于后续变形控制
        public static Model3DGroup ToModel3DGroup(Scene scene, string textureBaseDir)
        {
            return ToModel3DGroup(scene, textureBaseDir, out _);
        }

        public static Model3DGroup ToModel3DGroup(Scene scene, string textureBaseDir, out Dictionary<string, GeometryModel3D> meshMap)
        {
            var group = new Model3DGroup();
            meshMap = new Dictionary<string, GeometryModel3D>(StringComparer.OrdinalIgnoreCase);
            if (scene == null) return group;

            var meshMaterials = BuildMaterials(scene, textureBaseDir);

            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                var mesh = scene.Meshes[i];
                var geo = new MeshGeometry3D
                {
                    Positions = new Point3DCollection(mesh.Vertices.Select(v => new Point3D(v.X, v.Y, v.Z))),
                    Normals = new System.Windows.Media.Media3D.Vector3DCollection(mesh.Normals.Select(n => new System.Windows.Media.Media3D.Vector3D(n.X, n.Y, n.Z))),
                };

                if (mesh.TextureCoordinateChannelCount > 0)
                {
                    var uvs = mesh.TextureCoordinateChannels[0].Select(tc => new System.Windows.Point(tc.X, 1.0 - tc.Y));
                    geo.TextureCoordinates = new System.Windows.Media.PointCollection(uvs);
                }

                var tris = new Int32Collection();
                foreach (var face in mesh.Faces)
                {
                    if (face.IndexCount == 3)
                    {
                        tris.Add(face.Indices[0]); tris.Add(face.Indices[1]); tris.Add(face.Indices[2]);
                    }
                }
                geo.TriangleIndices = tris;

                System.Windows.Media.Media3D.Material mat = meshMaterials.TryGetValue(mesh.MaterialIndex, out var m)
                    ? m
                    : CreateDefaultMaterial();

                var model = new GeometryModel3D { Geometry = geo, Material = mat, BackMaterial = mat };
                group.Children.Add(model);

                var meshName = string.IsNullOrWhiteSpace(mesh.Name) ? $"mesh_{i}" : mesh.Name;
                var normalized = NormalizeLabel(meshName);
                // 同时存储规范名与原始名，便于后续匹配与调试
                meshMap[normalized] = model;
                if (!meshMap.ContainsKey(meshName)) meshMap[meshName] = model;
            }

            return group;
        }

        // 构建材质：优先使用贴图，否则使用漫反射颜色；叠加高光以实现更立体的着色
        private static Dictionary<int, System.Windows.Media.Media3D.Material> BuildMaterials(Scene scene, string textureBaseDir)
        {
            var dict = new Dictionary<int, System.Windows.Media.Media3D.Material>();
            if (scene.Materials == null) return dict;
            for (int i = 0; i < scene.Materials.Count; i++)
            {
                var m = scene.Materials[i];

                Brush diffuseBrush = null;
                Brush specularBrush = null;

                // 尝试加载漫反射贴图（支持外部文件与嵌入纹理 *N）
                if (m.HasTextureDiffuse)
                {
                    var diffusePath = ResolveTexture(scene, m.TextureDiffuse, textureBaseDir);
                    if (diffusePath != null)
                    {
                        try
                        {
                            var bi = new BitmapImage();
                            bi.BeginInit();
                            bi.UriSource = new Uri(diffusePath, UriKind.Absolute);
                            bi.CacheOption = BitmapCacheOption.OnLoad;
                            bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                            bi.EndInit();
                        }
                        catch
                        {
                            // 贴图加载失败时回退到颜色
                        }
                    }
                }

                // 回退：若没有贴图，使用材质漫反射颜色
                if (diffuseBrush == null)
                {
                    var kd = m.HasColorDiffuse ? m.ColorDiffuse : new Color4D(0.78f, 0.78f, 0.78f, 1f);
                    diffuseBrush = new SolidColorBrush(Color.FromScRgb(1f, kd.R, kd.G, kd.B));
                }

                // 简单高光
                specularBrush = new SolidColorBrush(Colors.White);

                var mg = new MaterialGroup();
                mg.Children.Add(new DiffuseMaterial(diffuseBrush));
                mg.Children.Add(new SpecularMaterial(specularBrush, 64));
                dict[i] = mg;
            }
            return dict;
        }

        private static string ResolveTexture(Scene scene, TextureSlot slot, string textureBaseDir)
        {
            var fp = slot.FilePath;
            if (string.IsNullOrWhiteSpace(fp)) return null;

            // 嵌入纹理 *N
            if (fp.StartsWith("*"))
            {
                // 当前未处理嵌入纹理，返回 null 交由回退逻辑
                return null;
            }

            // 绝对路径存在则直接使用
            if (File.Exists(fp)) return fp;

            // 多级纹理目录候选
            var baseDir = textureBaseDir ?? string.Empty;
            var candidates = new List<string>();
            var fileName = Path.GetFileName(fp);
            if (!string.IsNullOrEmpty(baseDir))
            {
                candidates.Add(Path.Combine(baseDir, fp));
                candidates.Add(Path.Combine(baseDir, fileName));
                foreach (var folder in new[] { "tex", "textures", "Textures", "materials", "Materials" })
                {
                    candidates.Add(Path.Combine(baseDir, folder, fileName));
                }
            }

            // 向上两级查找常见纹理文件夹
            var dir = baseDir;
            for (int d = 0; d < 2; d++)
            {
                if (string.IsNullOrEmpty(dir)) break;
                var parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent)) break;
                foreach (var folder in new[] { "tex", "textures", "Textures", "materials", "Materials" })
                {
                    candidates.Add(Path.Combine(parent, folder, fileName));
                }
                dir = parent;
            }

            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }
            return null;
        }

        private static System.Windows.Media.Media3D.Material CreateDefaultMaterial()
        {
            var mg = new MaterialGroup();
            mg.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(200, 200, 200))));
            mg.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 64));
            return mg;
        }

        // 收集骨骼节点名称集合，便于窗口中绘制骨架（连线在窗口中生成）
        public static HashSet<string> CollectBoneNames(Scene scene)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mesh in scene.Meshes)
            {
                foreach (var b in mesh.Bones)
                {
                    if (!string.IsNullOrWhiteSpace(b.Name))
                    {
                        names.Add(b.Name);
                        names.Add(NormalizeLabel(b.Name));
                    }
                }
            }
            return names;
        }

        // 收集节点位置（世界空间平移分量），用于绘制骨架参考点
        public static Dictionary<string, Point3D> CollectNodePositions(Scene scene)
        {
            var dict = new Dictionary<string, Point3D>(StringComparer.OrdinalIgnoreCase);
            if (scene.RootNode == null) return dict;
            Traverse(scene.RootNode, Assimp.Matrix4x4.Identity, dict);
            return dict;
        }

        private static void Traverse(Node node, Matrix4x4 parent, Dictionary<string, Point3D> dict)
        {
            var world = parent * node.Transform;
            // 取平移分量作为节点位置（旋转缩放忽略）
            var pos = new Point3D(world.A4, world.B4, world.C4);
            var raw = node.Name ?? Guid.NewGuid().ToString();
            var norm = NormalizeLabel(raw);
            dict[norm] = pos;
            if (!dict.ContainsKey(raw)) dict[raw] = pos;
            foreach (var child in node.Children) Traverse(child, world, dict);
        }

        // 在材质目录中通过关键字搜索纹理文件
        private static string? FindTextureByKeywords(string baseDir, string[] keywords)
        {
            try
            {
                if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir)) return null;
                var files = Directory.EnumerateFiles(baseDir, "*.*", SearchOption.TopDirectoryOnly)
                                     .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                                     .ToList();
                foreach (var k in keywords)
                {
                    var hit = files.FirstOrDefault(f => Path.GetFileName(f).IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (hit != null) return hit;
                }
            }
            catch { }
            return null;
        }

        // 名称规范化：消除前后缀/大小写/中英文别称，输出统一标签
        private static string NormalizeLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var s = name.Trim().ToLowerInvariant();
            // 去除常见前缀
            foreach (var p in new[] { "mixamorig:", "armature", "bone", "rig", "rp_", "bip", "node", "mesh", "geo", "geometry" })
            {
                s = s.Replace(p, string.Empty);
            }
            s = s.Replace("-", " ").Replace("_", " ");
            s = System.Text.RegularExpressions.Regex.Replace(s, "\\s+", " ").Trim();

            bool isLeft = s.Contains("left") || s.Contains(" l ") || s.EndsWith(" l") || s.StartsWith("l ") || s.Contains("_l") || s.Contains("-l") || s.Contains(" 左");
            bool isRight = s.Contains("right") || s.Contains(" r ") || s.EndsWith(" r") || s.StartsWith("r ") || s.Contains("_r") || s.Contains("-r") || s.Contains(" 右");

            // 头颈与躯干
            if (s.Contains("head") || s.Contains(" 头") || s.Contains("头部")) return "head";
            if (s.Contains("neck") || s.Contains("脖") || s.Contains("颈")) return "neck";
            if (s.Contains("upperchest") || s.Contains("chest") || s.Contains("胸")) return "chest";
            if (s.Contains("spine") || s.Contains("背") || s.Contains("脊")) return "spine";
            if (s.Contains("hips") || s.Contains("pelvis") || s.Contains("腰") || s.Contains("臀")) return "hips";

            // 手臂
            if (s.Contains("forearm") || s.Contains("lowerarm") || s.Contains(" 前臂"))
            {
                if (isLeft) return "leftlowerarm";
                if (isRight) return "rightlowerarm";
                return "lowerarm";
            }
            if (s.Contains("shoulder") || s.Contains("upperarm") || (s.Contains("arm") && !s.Contains("forearm")))
            {
                if (isLeft) return "leftupperarm";
                if (isRight) return "rightupperarm";
                return "upperarm";
            }

            // 腿
            if (s.Contains("shin") || s.Contains("calf") || s.Contains("lowerleg") || s.Contains(" 小腿") || s.Contains("胫"))
            {
                if (isLeft) return "leftlowerleg";
                if (isRight) return "rightlowerleg";
                return "lowerleg";
            }
            if (s.Contains("thigh") || s.Contains("upleg") || s.Contains("upperleg") || s.Contains(" 大腿"))
            {
                if (isLeft) return "leftupperleg";
                if (isRight) return "rightupperleg";
                return "upperleg";
            }

            return s.Replace(" ", string.Empty);
        }
    }
}
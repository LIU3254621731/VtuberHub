using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows;

namespace VtuberHubStudio.ModelViewer
{
    public partial class ModelViewerWindow : Window
    {
        // 未识别部位统计与高亮，并给出建议别名规则
        private void AnalyzeUnrecognizedPartsAndSuggest()
        {
            if (_importedMeshMap == null || _importedMeshMap.Count == 0) return;
            string[] known = new[] { "skin", "body", "face", "cloth", "garment", "shirt", "skirt", "pant", "trouser", "dress", "hair", "eye", "iris", "头发", "眼", "皮肤", "衣" };
            var unknown = new Dictionary<string, GeometryModel3D>();
            foreach (var kv in _importedMeshMap)
            {
                var key = kv.Key.ToLowerInvariant();
                var cat = CategorizeByNameOrMaterial(kv.Key, kv.Value.Material);
                if (cat == PartCategory.Unknown && !known.Any(k => key.Contains(k)) && !IsGenericName(key)) unknown[key] = kv.Value;
            }
            foreach (var gm in unknown.Values)
            {
                var mg = gm.Material as MaterialGroup; if (mg == null) continue;
                var diff = mg.Children.OfType<DiffuseMaterial>().FirstOrDefault();
                if (diff != null) diff.Brush = new SolidColorBrush(Color.FromArgb(180, 200, 0, 200));
            }
            if (unknown.Count > 0)
            {
                var tips = new List<string>();
                foreach (var u in unknown.Keys.Take(12))
                {
                    if (u.Contains("torso") || u.Contains("chest") || u.Contains("rib")) tips.Add($"建议将 '{u}' 归类为 '胸部' (Chest)");
                    else if (u.Contains("pelvis") || u.Contains("hip")) tips.Add($"建议将 '{u}' 归类为 '臀部' (Hips)");
                    else if (u.Contains("spine") || u.Contains("back")) tips.Add($"建议将 '{u}' 归类为 '脊柱' (Spine)");
                    else if (u.Contains("finger") || u.Contains("hand")) tips.Add($"建议将 '{u}' 归类为 '手部' (Hands/Fingers)");
                    else if (u.Contains("foot") || u.Contains("toe")) tips.Add($"建议将 '{u}' 归类为 '脚部' (Feet/Toes)");
                    else tips.Add($"未识别：'{u}'，可在别名规则中添加匹配关键词。");
                }
                MessageBox.Show("存在未识别部位：\n" + string.Join("\n", tips), "别名建议", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // 导出 OBJ 同步输出 parts.json：包含规范化名称、原始别名列表、分类、贴图路径
        private void ExportPartsJson(string objPath)
        {
            try
            {
                var byGm = new Dictionary<GeometryModel3D, List<string>>();
                foreach (var kv in _importedMeshMap)
                {
                    if (!byGm.TryGetValue(kv.Value, out var list)) { list = new List<string>(); byGm[kv.Value] = list; }
                    if (!list.Contains(kv.Key)) list.Add(kv.Key);
                }
                var parts = new List<object>();
                foreach (var kv in byGm)
                {
                    var aliases = kv.Value.Distinct().OrderBy(s => s.Length).ToList();
                    var canonical = aliases.FirstOrDefault() ?? "part";
                    var cat = CategorizeByNameOrMaterial(canonical, kv.Key.Material).ToString();
                    string? tex = null;
                    var mg = kv.Key.Material as MaterialGroup;
                    var diff = mg?.Children.OfType<DiffuseMaterial>().FirstOrDefault();
                    if (diff?.Brush is ImageBrush ib && ib.ImageSource is BitmapImage bmp && bmp.UriSource != null)
                        tex = bmp.UriSource.LocalPath;
                    parts.Add(new { name = canonical, category = cat, aliases = aliases, texture = tex });
                }
                var jsonPath = System.IO.Path.ChangeExtension(objPath, ".parts.json");
                var json = JsonSerializer.Serialize(new { parts = parts }, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(jsonPath, json);
            }
            catch { }
        }
        private bool IsGenericName(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return true;
            if (key.Length <= 2) return true;
            // 纯数字或下划线
            if (key.All(ch => char.IsDigit(ch) || ch == '_')) return true;
            // 常见通用前缀（忽略大小写）
            string[] prefixes = new[] { "mesh", "node", "object", "group", "geometry", "model", "default", "root", "part", "polysurface", "poly", "pcube", "psphere", "pcylinder", "cube", "sphere", "cylinder", "plane", "armature", "bone" };
            foreach (var p in prefixes)
            {
                if (key.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace VtuberHubStudio.ModelViewer
{
    public partial class ModelViewerWindow : Window
    {
        // 识别到的关键骨骼坐标（标准化枚举）
        private Dictionary<JointName, Point3D> _detectedJoints = new();
        // 导入模型外观材质分类
        private List<GeometryModel3D> _clothModels = new();
        private List<GeometryModel3D> _skinModels = new();
        private List<GeometryModel3D> _hairModels = new();
        private List<GeometryModel3D> _eyeModels = new();

        private enum PartCategory { Unknown, Skin, Cloth, Hair, Eye }

        private void CategorizeAppearanceTargets()
        {
            _clothModels.Clear(); _skinModels.Clear(); _hairModels.Clear(); _eyeModels.Clear();
            foreach (var kv in _importedMeshMap)
            {
                var gm = kv.Value; var name = kv.Key;
                var cat = CategorizeByNameOrMaterial(name, gm.Material);
                switch (cat)
                {
                    case PartCategory.Skin: _skinModels.Add(gm); break;
                    case PartCategory.Cloth: _clothModels.Add(gm); break;
                    case PartCategory.Hair: _hairModels.Add(gm); break;
                    case PartCategory.Eye: _eyeModels.Add(gm); break;
                }
            }
        }

        private PartCategory CategorizeByNameOrMaterial(string name, System.Windows.Media.Media3D.Material material)
        {
            string l = name.ToLowerInvariant();
            // 中英文关键字覆盖更广
            if (l.Contains("cloth") || l.Contains("garment") || l.Contains("shirt") || l.Contains("blouse") || l.Contains("skirt") || l.Contains("pant") || l.Contains("trouser") || l.Contains("dress") || l.Contains("sleeve") || l.Contains("bow") || l.Contains("tie") || l.Contains("衣") || l.Contains("裙") || l.Contains("裤") || l.Contains("服"))
                return PartCategory.Cloth;
            if (l.Contains("skin") || l.Contains("body") || l.Contains("face") || l.Contains("dermis") || l.Contains("皮") || l.Contains("脸")) return PartCategory.Skin;
            if (l.Contains("hair") || l.Contains("头发") || l.Contains("发")) return PartCategory.Hair;
            if (l.Contains("eye") || l.Contains("iris") || l.Contains("眼") || l.Contains("瞳")) return PartCategory.Eye;
            var mg = material as MaterialGroup;
            var diff = mg?.Children.OfType<DiffuseMaterial>().FirstOrDefault();
            if (diff?.Brush is ImageBrush ib && ib.ImageSource is BitmapImage bmp && bmp.UriSource != null)
            {
                var fn = System.IO.Path.GetFileName(bmp.UriSource.LocalPath).ToLowerInvariant();
                // 常见纹理名：albedo/basecolor/diffuse + 对应语义词
                if (fn.Contains("cloth") || fn.Contains("garment") || fn.Contains("shirt") || fn.Contains("blouse") || fn.Contains("skirt") || fn.Contains("pant") || fn.Contains("trouser") || fn.Contains("dress") || fn.Contains("sleeve") || fn.Contains("bow") || fn.Contains("tie") || fn.Contains("衣") || fn.Contains("裙") || fn.Contains("裤") || fn.Contains("服")) return PartCategory.Cloth;
                if (fn.Contains("skin") || fn.Contains("body") || fn.Contains("face") || fn.Contains("dermis") || fn.Contains("皮") || fn.Contains("脸")) return PartCategory.Skin;
                if (fn.Contains("hair") || fn.Contains("头发") || fn.Contains("发")) return PartCategory.Hair;
                if (fn.Contains("eye") || fn.Contains("iris") || fn.Contains("眼") || fn.Contains("瞳")) return PartCategory.Eye;
            }
            return PartCategory.Unknown;
        }

        private void ApplyImportedMaterialColors()
        {
            var skin = GetSelectedSkinColor();
            var cloth = GetSelectedClothColor();
            var hair = GetSelectedHairColor();
            var eye = GetSelectedEyeColor();
            TintModels(_skinModels, skin, 0.5); // 皮肤颜色适度混合，保留原亮度
            TintModels(_clothModels, cloth, 0.9);
            TintModels(_hairModels, hair, 0.8);
            TintModels(_eyeModels, eye, 0.7);
        }

        // 批量替换贴图：按分类对所有模型套用同一纹理
        private void ReplaceTexturesForCategory(PartCategory cat, string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return;
            var list = cat switch
            {
                PartCategory.Skin => _skinModels,
                PartCategory.Cloth => _clothModels,
                PartCategory.Hair => _hairModels,
                PartCategory.Eye  => _eyeModels,
                _ => null
            };
            if (list == null) return;
            foreach (var gm in list)
            {
                var mg = gm.Material as MaterialGroup; if (mg == null) continue;
                var diff = mg.Children.OfType<DiffuseMaterial>().FirstOrDefault(); if (diff == null) continue;
                try
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bi.EndInit();
                    diff.Brush = new ImageBrush(bi) { Stretch = Stretch.Uniform };
                }
                catch { }
            }
        }

        private void TintModels(List<GeometryModel3D> models, Color tint, double strength)
        {
            foreach (var gm in models)
            {
                var mg = gm.Material as MaterialGroup; if (mg == null) continue;
                var diff = mg.Children.OfType<DiffuseMaterial>().FirstOrDefault();
                if (diff == null) continue;
                if (diff.Brush is ImageBrush ib && ib.ImageSource is BitmapImage bmp && bmp.UriSource != null)
                {
                    var path = bmp.UriSource.LocalPath;
                    var brush = CreateTintedImageBrush(path, tint, strength);
                    if (brush != null) diff.Brush = brush;
                }
                else if (diff.Brush is SolidColorBrush sb)
                {
                    sb.Color = tint;
                }
                else
                {
                    diff.Brush = new SolidColorBrush(tint);
                }
            }
        }

        private ImageBrush? CreateTintedImageBrush(string path, Color tint, double strength)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bi.EndInit();
                var fmt = new FormatConvertedBitmap(bi, PixelFormats.Bgra32, null, 0);
                int w = fmt.PixelWidth, h = fmt.PixelHeight, stride = w * 4;
                var pixels = new byte[h * stride];
                fmt.CopyPixels(pixels, stride, 0);
                // HSL 变换：用 tint 的 Hue/Saturation，保留原 Lightness
                double th, ts, tl;
                RgbToHsl(tint.R, tint.G, tint.B, out th, out ts, out tl);
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + x * 4;
                        byte b = pixels[i + 0];
                        byte g = pixels[i + 1];
                        byte r = pixels[i + 2];
                        byte a = pixels[i + 3];
                        double oh, os, ol; RgbToHsl(r, g, b, out oh, out os, out ol);
                        // 合成：Hue/Sat 替换为 tint（按 strength 过渡），Lightness 保留原值
                        double nh = Lerp(oh, th, strength);
                        double ns = Lerp(os, ts, strength);
                        double nr, ng, nb; HslToRgb(nh, ns, ol, out nr, out ng, out nb);
                        pixels[i + 0] = (byte)Math.Clamp(nb, 0, 255);
                        pixels[i + 1] = (byte)Math.Clamp(ng, 0, 255);
                        pixels[i + 2] = (byte)Math.Clamp(nr, 0, 255);
                        pixels[i + 3] = a;
                    }
                }
                var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
                return new ImageBrush(wb) { Stretch = Stretch.Uniform };
            }
            catch { return null; }
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
        private static void RgbToHsl(byte r, byte g, byte b, out double h, out double s, out double l)
        {
            double rr = r / 255.0, gg = g / 255.0, bb = b / 255.0;
            double max = Math.Max(rr, Math.Max(gg, bb)), min = Math.Min(rr, Math.Min(gg, bb));
            l = (max + min) / 2.0;
            if (Math.Abs(max - min) < 1e-6) { h = 0; s = 0; return; }
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == rr) h = ((gg - bb) / d + (gg < bb ? 6 : 0));
            else if (max == gg) h = ((bb - rr) / d + 2);
            else h = ((rr - gg) / d + 4);
            h /= 6.0;
        }
        private static void HslToRgb(double h, double s, double l, out double r, out double g, out double b)
        {
            if (s == 0) { r = g = b = l * 255.0; return; }
            Func<double, double, double, double> hue2rgb = (p, q, t) =>
            {
                if (t < 0) t += 1; if (t > 1) t -= 1;
                if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
                if (t < 1.0 / 2.0) return q;
                if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
                return p;
            };
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            double rr = hue2rgb(p, q, h + 1.0 / 3.0);
            double gg = hue2rgb(p, q, h);
            double bb = hue2rgb(p, q, h - 1.0 / 3.0);
            r = rr * 255.0; g = gg * 255.0; b = bb * 255.0;
        }
    }
}
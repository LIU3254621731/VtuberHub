using System;
using System.IO;
using System.Linq;

namespace VtuberHubStudio
{
    // 负责将 test/mediapipe 下的图与 modules 拷贝到应用运行目录，保证 include 与依赖就绪
    public static class MediapipeAssets
    {
        public static string EnsureAssets()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var destRoot = Path.Combine(baseDir, "mediapipe");
            try { Directory.CreateDirectory(destRoot); } catch { }

            // 源路径优先使用 test，再 fallback 到 mediapipe/dll
            string? srcTest = FindDirUp(baseDir, "test");
            string? srcDll = FindDirUp(baseDir, Path.Combine("mediapipe", "dll"));
            var candidates = new[] { srcTest, srcDll }.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p!)).ToArray();
            if (candidates.Length == 0) return destRoot;
            var srcRoot = candidates.First()!;

            // 复制 modules 整树，便于 pbtxt 中 #include
            var srcModules = Path.Combine(srcRoot, "mediapipe", "modules");
            var destModules = Path.Combine(destRoot, "modules");
            if (Directory.Exists(srcModules)) CopyDirectory(srcModules, destModules);

            // 复制常用顶层图
            CopyIfExists(Path.Combine(srcRoot, "holistic_tracking_cpu.pbtxt"), Path.Combine(destRoot, "holistic_tracking_cpu.pbtxt"));
            CopyIfExists(Path.Combine(srcRoot, "hand_tracking_desktop_live.pbtxt"), Path.Combine(destRoot, "hand_tracking_desktop_live.pbtxt"));

            return destRoot;
        }

        private static void CopyIfExists(string src, string dest)
        {
            try
            {
                if (File.Exists(src))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(src, dest, overwrite: true);
                }
            }
            catch { }
        }

        private static void CopyDirectory(string srcDir, string destDir)
        {
            try
            {
                Directory.CreateDirectory(destDir);
                foreach (var dir in Directory.GetDirectories(srcDir, "*", SearchOption.AllDirectories))
                {
                    var target = dir.Replace(srcDir, destDir);
                    try { Directory.CreateDirectory(target); } catch { }
                }
                foreach (var file in Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories))
                {
                    var target = file.Replace(srcDir, destDir);
                    try { File.Copy(file, target, true); } catch { }
                }
            }
            catch { }
        }

        private static string? FindDirUp(string start, string targetRel)
        {
            var dir = new DirectoryInfo(start);
            for (int i = 0; i < 8 && dir != null; i++)
            {
                var tryPath = Path.Combine(dir.FullName, targetRel);
                if (Directory.Exists(tryPath)) return tryPath;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace VtuberHubStudio
{
    public struct MpDetectResult
    {
        public bool Success;
        public int Rc;
        public int LeftArm;
        public int RightArm;
        public int LeftGesture;
        public int RightGesture;
    }

    // 封装 Mediapipe Holistic Tracking 初始化/检测/释放，统一像素格式转换与 DLL 搜索目录
    public class MediapipeService
    {
        public bool Ready { get; private set; } = false;
        public string? GraphPath { get; private set; }
        public int LastRc { get; private set; } = -999;
        public int[] LastResults { get; private set; } = new int[4];

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(uint DirectoryFlags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr AddDllDirectory(string NewDirectory);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool RemoveDllDirectory(IntPtr Cookie);
        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
        private const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;
        private const uint LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200;
        private const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;

        [DllImport("MediapipeHolisticTracking.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int MediapipeHolisticTrackingInit(string graphPath,
            [MarshalAs(UnmanagedType.I1)] bool needVideoOutput,
            [MarshalAs(UnmanagedType.I1)] bool needPose,
            [MarshalAs(UnmanagedType.I1)] bool needHand,
            [MarshalAs(UnmanagedType.I1)] bool needFace);

        [DllImport("MediapipeHolisticTracking.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int MediapipeHolisticTrackingDetectFrameDirect(int width, int height, IntPtr rgbData, [Out] int[] results, [MarshalAs(UnmanagedType.I1)] bool showResultImage);

        [DllImport("MediapipeHolisticTracking.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int MediapipeHolisticTrackingRelease();

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

        public void Initialize()
        {
            if (Ready) return;
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var runRoot = Path.Combine(baseDir, "mediapipe");
                try { Directory.CreateDirectory(runRoot); } catch { }
                var testDir = FindDirUp(baseDir, "test");
                var dllDir = FindDirUp(baseDir, Path.Combine("mediapipe", "dll"));
                var candidates = new string[] { runRoot, testDir ?? string.Empty, dllDir ?? string.Empty }
                    .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).ToArray();
                if (candidates.Length == 0)
                {
                    Ready = false;
                    return;
                }

                // 便于 pbtxt 中 #include 解析
                var selectedRoot = candidates.First();
                var modulesDir1 = Path.Combine(selectedRoot, "modules");
                var modulesDir2 = Path.Combine(selectedRoot, "mediapipe", "modules");
                if (Directory.Exists(modulesDir1) || Directory.Exists(modulesDir2))
                {
                    try { Directory.SetCurrentDirectory(selectedRoot); } catch { }
                }

                // 设置 DLL 搜索目录并尝试加载依赖
                SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_APPLICATION_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_USER_DIRS);
                var cookies = new System.Collections.Generic.List<IntPtr>();
                bool anyRegistered = false;
                foreach (var dir in candidates)
                {
                    try
                    {
                        var ck = AddDllDirectory(dir);
                        if (ck != IntPtr.Zero) { cookies.Add(ck); anyRegistered = true; }
                        void TryLoad(string name)
                        {
                            var p = Path.Combine(dir, name);
                            if (File.Exists(p)) System.Runtime.InteropServices.NativeLibrary.TryLoad(p, out _);
                        }
                        TryLoad("opencv_world3410.dll");
                        TryLoad("opencv_world3410d.dll");
                        TryLoad("opencv_ffmpeg3410_64.dll");
                        TryLoad("MediapipeHolisticTracking.dll");
                    }
                    catch { }
                }
                if (!anyRegistered)
                {
                    try
                    {
                        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                        var extra = string.Join(";", candidates);
                        if (!path.Contains(extra, StringComparison.OrdinalIgnoreCase))
                            Environment.SetEnvironmentVariable("PATH", extra + ";" + path, EnvironmentVariableTarget.Process);
                    }
                    catch { }
                }

                // 寻找 pbtxt（运行目录首选）
                GraphPath = Path.Combine(runRoot, "holistic_tracking_cpu.pbtxt");
                if (!File.Exists(GraphPath))
                {
                    GraphPath = candidates
                        .Select(d => Path.Combine(d, "holistic_tracking_cpu.pbtxt"))
                        .FirstOrDefault(File.Exists);
                }
                if (GraphPath == null)
                {
                    Ready = false;
                    return;
                }

                int rc = MediapipeHolisticTrackingInit(GraphPath, true, true, true, false);
                Ready = (rc == 0);
                LastRc = rc;
            }
            catch
            {
                Ready = false;
                LastRc = -1;
            }
        }

        public void Release()
        {
            try { MediapipeHolisticTrackingRelease(); } catch { }
            Ready = false;
        }

        // 输入为 BGR Mat；根据是否需要叠加选择单路径调用，避免双调用导致性能下降与状态抖动
        public MpDetectResult Detect(Mat matBgr, bool showOverlay)
        {
            var result = new MpDetectResult
            {
                Success = false,
                Rc = -1,
                LeftArm = -1,
                RightArm = -1,
                LeftGesture = -1,
                RightGesture = -1
            };
            if (!Ready || matBgr == null || matBgr.Empty()) return result;

            var detect = new int[4];
            try
            {
                int rc = -1;
                bool ok = false;

                if (showOverlay)
                {
                    // 叠加模式：走 BGRA 路径，让 DLL 在图像上绘制；成功后再转回 BGR 显示
                    using var rgba = new Mat();
                    Cv2.CvtColor(matBgr, rgba, ColorConversionCodes.BGR2BGRA);
                    var rgbaCont = rgba.IsContinuous() ? rgba : rgba.Clone();
                    rc = MediapipeHolisticTrackingDetectFrameDirect(rgbaCont.Cols, rgbaCont.Rows, rgbaCont.Data, detect, true);
                    ok = (rc == 0);
                    if (ok)
                    {
                        Cv2.CvtColor(rgbaCont, matBgr, ColorConversionCodes.BGRA2BGR);
                    }
                    if (rgbaCont != rgba) rgbaCont.Dispose();
                }
                else
                {
                    // 无叠加：仅检测，不改动原图，性能更好
                    using var rgb = new Mat();
                    Cv2.CvtColor(matBgr, rgb, ColorConversionCodes.BGR2RGB);
                    var rgbCont = rgb.IsContinuous() ? rgb : rgb.Clone();
                    rc = MediapipeHolisticTrackingDetectFrameDirect(rgbCont.Cols, rgbCont.Rows, rgbCont.Data, detect, false);
                    ok = (rc == 0);
                    if (rgbCont != rgb) rgbCont.Dispose();
                }

                LastRc = rc;
                if (ok)
                {
                    LastResults = detect;
                    result.Success = true;
                    result.Rc = 0;
                    result.LeftArm = detect[0];
                    result.RightArm = detect[1];
                    result.LeftGesture = detect[2];
                    result.RightGesture = detect[3];
                }
                else
                {
                    result.Rc = rc;
                }
            }
            catch
            {
                LastRc = -999;
                result.Rc = LastRc;
                result.Success = false;
            }
            return result;
        }
    }
}
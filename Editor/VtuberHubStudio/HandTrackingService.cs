using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace VtuberHubStudio
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PoseInfo
    {
        public float x;
        public float y;
    }

    // 管理 Mediapipe 手部追踪：初始化、帧检测（回调）、最新关键点缓存与绘制
    public class HandTrackingService
    {
        public bool Ready { get; private set; } = false;
        public string? GraphPath { get; private set; }
        public int LastRc { get; private set; } = -999;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void LandmarksCallback(int imageIndex, IntPtr infos, int count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GestureResultCallback(int imageIndex, IntPtr recognResult, int count);

        [DllImport("Mediapipe_Hand_Tracking.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Mediapipe_Hand_Tracking_Init(string model_path);
        [DllImport("Mediapipe_Hand_Tracking.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Mediapipe_Hand_Tracking_Reigeter_Landmarks_Callback(LandmarksCallback func);
        [DllImport("Mediapipe_Hand_Tracking.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Mediapipe_Hand_Tracking_Register_Gesture_Result_Callback(GestureResultCallback func);
        [DllImport("Mediapipe_Hand_Tracking.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Mediapipe_Hand_Tracking_Detect_Frame(int image_index, int image_width, int image_height, IntPtr image_data);
        [DllImport("Mediapipe_Hand_Tracking.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Mediapipe_Hand_Tracking_Release();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(uint DirectoryFlags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr AddDllDirectory(string NewDirectory);
        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
        private const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;
        private const uint LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200;
        private const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;

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

        private PoseInfo[]? _latestLeft;
        private PoseInfo[]? _latestRight;
        private DateTime _leftAt = DateTime.MinValue;
        private DateTime _rightAt = DateTime.MinValue;
        private LandmarksCallback? _lmCbRef;
        private GestureResultCallback? _gestCbRef;

        public void Initialize()
        {
            if (Ready) return;
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var runRoot = Path.Combine(baseDir, "mediapipe");
                var testDir = FindDirUp(baseDir, "test");
                var dllDir = FindDirUp(baseDir, Path.Combine("mediapipe", "dll"));
                var candidates = new string[] { runRoot, testDir ?? string.Empty, dllDir ?? string.Empty }
                    .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).ToArray();
                if (candidates.Length == 0)
                {
                    Ready = false; return;
                }

                // 设置 DLL 搜索目录
                SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_APPLICATION_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_USER_DIRS);
                foreach (var dir in candidates)
                {
                    try { AddDllDirectory(dir); } catch { }
                }

                // 切换工作目录以保证 pbtxt 中 #include 解析
                // 1) 运行目录 ./mediapipe 存在时，使用该目录
                if (Directory.Exists(runRoot))
                {
                    try { Directory.SetCurrentDirectory(runRoot); } catch { }
                }
                else
                {
                    // 2) 候选根若包含 modules 或 mediapipe/modules，也切到该根
                    var selectedRoot = candidates.First();
                    var modulesDir1 = Path.Combine(selectedRoot, "modules");
                    var modulesDir2 = Path.Combine(selectedRoot, "mediapipe", "modules");
                    if (Directory.Exists(modulesDir1) || Directory.Exists(modulesDir2))
                    {
                        try { Directory.SetCurrentDirectory(selectedRoot); } catch { }
                    }
                }

                // 优先使用 desktop_live 图，其次 CPU image 图
                GraphPath = Path.Combine(runRoot, "hand_tracking_desktop_live.pbtxt");
                if (!File.Exists(GraphPath))
                {
                    GraphPath = candidates
                        .Select(d => Path.Combine(d, Path.Combine("mediapipe", "modules", "hand_landmark", "hand_landmark_tracking_cpu_image.pbtxt")))
                        .FirstOrDefault(File.Exists);
                    if (GraphPath == null)
                    {
                        GraphPath = candidates
                            .Select(d => Path.Combine(d, "hand_landmark_tracking_cpu_image.pbtxt"))
                            .FirstOrDefault(File.Exists);
                    }
                }
                if (GraphPath == null)
                {
                    Ready = false; LastRc = -2; return;
                }

                // 注册回调（保持委托引用防止 GC）
                _lmCbRef = OnLandmarks;
                _gestCbRef = OnGesture;
                var rcCb1 = Mediapipe_Hand_Tracking_Reigeter_Landmarks_Callback(_lmCbRef);
                var rcCb2 = Mediapipe_Hand_Tracking_Register_Gesture_Result_Callback(_gestCbRef);

                int rc = Mediapipe_Hand_Tracking_Init(GraphPath);
                LastRc = rc;
                Ready = (rc == 0) && (rcCb1 == 0) && (rcCb2 == 0);
            }
            catch
            {
                Ready = false; LastRc = -1;
            }
        }

        public void Release()
        {
            try { Mediapipe_Hand_Tracking_Release(); } catch { }
            Ready = false;
        }

        public int Detect(Mat matBgr)
        {
            if (!Ready || matBgr == null || matBgr.Empty()) return -1;
            var cont = matBgr;
            if (!matBgr.IsContinuous()) cont = matBgr.Clone();
            try
            {
                int rc = Mediapipe_Hand_Tracking_Detect_Frame(0, cont.Cols, cont.Rows, cont.Data);
                LastRc = rc; return rc;
            }
            catch { LastRc = -999; return LastRc; }
            finally { if (cont != matBgr) cont.Dispose(); }
        }

        private void OnLandmarks(int imageIndex, IntPtr infos, int count)
        {
            try
            {
                var arr = new PoseInfo[count];
                var size = Marshal.SizeOf<PoseInfo>();
                for (int i = 0; i < count; i++)
                {
                    var ptr = IntPtr.Add(infos, i * size);
                    arr[i] = Marshal.PtrToStructure<PoseInfo>(ptr);
                }
                if (imageIndex == 0) { _latestLeft = arr; _leftAt = DateTime.UtcNow; }
                else { _latestRight = arr; _rightAt = DateTime.UtcNow; }
            }
            catch { }
        }

        private void OnGesture(int imageIndex, IntPtr recognResult, int count)
        {
            // 手势结果目前不在此类暴露；仅用于确认回调正常
        }

        public void Draw(Mat matBgr, Scalar color, int thickness)
        {
            if (matBgr == null || matBgr.Empty()) return;
            var now = DateTime.UtcNow;
            if (_latestLeft != null && (now - _leftAt).TotalMilliseconds < 400)
            {
                DrawHand(matBgr, _latestLeft, color, thickness);
            }
            if (_latestRight != null && (now - _rightAt).TotalMilliseconds < 400)
            {
                DrawHand(matBgr, _latestRight, color, thickness);
            }
        }

        private static readonly int[][] HandEdges = new int[][]
        {
            // 拇指 0-1-2-3-4
            new[]{0,1}, new[]{1,2}, new[]{2,3}, new[]{3,4},
            // 食指 0-5-6-7-8
            new[]{0,5}, new[]{5,6}, new[]{6,7}, new[]{7,8},
            // 中指 0-9-10-11-12
            new[]{0,9}, new[]{9,10}, new[]{10,11}, new[]{11,12},
            // 无名指 0-13-14-15-16
            new[]{0,13}, new[]{13,14}, new[]{14,15}, new[]{15,16},
            // 小指 0-17-18-19-20
            new[]{0,17}, new[]{17,18}, new[]{18,19}, new[]{19,20}
        };

        private static void DrawHand(Mat img, PoseInfo[] pts, Scalar color, int th)
        {
            if (pts == null || pts.Length < 21) return;
            // 点
            for (int i = 0; i < 21; i++)
            {
                var p = pts[i];
                Cv2.Circle(img, new Point(p.x, p.y), Math.Max(1, th + 1), color, -1);
            }
            // 线
            foreach (var e in HandEdges)
            {
                var p1 = pts[e[0]]; var p2 = pts[e[1]];
                Cv2.Line(img, new Point(p1.x, p1.y), new Point(p2.x, p2.y), color, th);
            }
        }
    }
}
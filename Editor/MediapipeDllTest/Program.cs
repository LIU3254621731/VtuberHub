using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;
using System.Linq;

namespace MediapipeDllTest
{
    internal static class Native
    {
        [DllImport("Mediapipe_Hand_Tracking.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Mediapipe_Hand_Tracking_Init(string graphPath);

        [DllImport("MediapipeHolisticTracking.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern int MediapipeHolisticTrackingInit(string graphPath);
    }

    // 添加 DLL 搜索目录注册与图路径解析工具
    internal static class DllSearchUtil
    {
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
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, targetRel);
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        private static string[] _candidates = Array.Empty<string>();

        public static void Configure()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var testDir = FindDirUp(baseDir, "test");
            var dllDir = FindDirUp(baseDir, Path.Combine("mediapipe", "dll"));
            _candidates = new[] { testDir ?? string.Empty, dllDir ?? string.Empty }
                .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d))
                .ToArray();
            if (_candidates.Length == 0) return;

            // 若存在 mediapipe/modules，则将当前工作目录切换到该根目录，便于 #include 解析
            var selectedRoot = _candidates.First();
            var modulesDir = Path.Combine(selectedRoot, "mediapipe", "modules");
            if (Directory.Exists(modulesDir))
            {
                try { Directory.SetCurrentDirectory(selectedRoot); } catch { }
            }

            // 注册 DLL 搜索目录，并尝试预加载依赖
            try
            {
                SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_APPLICATION_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_USER_DIRS);
            }
            catch { }

            bool anyRegistered = false;
            foreach (var dir in _candidates)
            {
                try
                {
                    var ck = AddDllDirectory(dir);
                    if (ck != IntPtr.Zero) anyRegistered = true;
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
                    var extra = string.Join(";", _candidates);
                    if (!path.Contains(extra, StringComparison.OrdinalIgnoreCase))
                        Environment.SetEnvironmentVariable("PATH", extra + ";" + path, EnvironmentVariableTarget.Process);
                }
                catch { }
            }
        }

        public static string? ResolveGraph(string fileName)
        {
            foreach (var d in _candidates)
            {
                var p = Path.Combine(d, fileName);
                if (File.Exists(p)) return p;
            }
            return null;
        }
    }

    public class MainForm : Form
    {
        private readonly TextBox _log = new TextBox { Multiline = true, Dock = DockStyle.Bottom, ScrollBars = ScrollBars.Vertical, Height = 120 };
        private readonly Panel _topBar = new Panel { Dock = DockStyle.Top, Height = 44 };
        private readonly PictureBox _preview = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.Black, SizeMode = PictureBoxSizeMode.Zoom };

        private readonly Button _btnCam0 = new Button { Text = "Start Cam 0", Width = 110 };
        private readonly Button _btnCam1 = new Button { Text = "Start Cam 1", Width = 110 };
        private readonly Button _btnCam2 = new Button { Text = "Start Cam 2", Width = 110 };
        private readonly Button _btnHand = new Button { Text = "Init Hand", Width = 110 };
        private readonly Button _btnHolistic = new Button { Text = "Init Holistic", Width = 110 };

        public MainForm()
        {
            Text = "Mediapipe DLL Test";
            Width = 960; Height = 600;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = true;

            BackColor = Color.Black;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f, FontStyle.Regular);

            Controls.Add(_preview);
            Controls.Add(_log);
            Controls.Add(_topBar);

            var buttons = new[] { _btnCam0, _btnCam1, _btnCam2, _btnHand, _btnHolistic };
            int x = 8;
            foreach (var b in buttons)
            {
                StyleButton(b);
                b.Left = x; b.Top = 6; b.Height = 32;
                _topBar.Controls.Add(b);
                x += b.Width + 8;
            }

            // 注册 DLL 搜索目录
            DllSearchUtil.Configure();
            _log.AppendText("[Init] DLL 搜索目录已配置（test/mediapipe\\dll）。\r\n");

            _btnCam0.Click += (s, e) => StartCamera(0);
            _btnCam1.Click += (s, e) => StartCamera(1);
            _btnCam2.Click += (s, e) => StartCamera(2);
            _btnHand.Click += (s, e) => TryInitHand();
            _btnHolistic.Click += (s, e) => TryInitHolistic();
        }

        private void StyleButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.FromArgb(32, 32, 32);
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderColor = Color.FromArgb(64, 64, 64);
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 48, 48);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 64, 64);
        }

        private void StartCamera(int index)
        {
            _log.AppendText($"[Camera] Start requested for index {index}. (TODO)\r\n");
        }

        private void TryInitHand()
        {
            try
            {
                string? path = DllSearchUtil.ResolveGraph("hand_tracking_desktop_live.pbtxt");
                if (path == null)
                {
                    _log.AppendText("[Mediapipe] Hand graph 未找到。\r\n");
                    return;
                }
                int rc = Native.Mediapipe_Hand_Tracking_Init(path);
                _log.AppendText($"[Mediapipe] Hand Init rc={rc} path={path}\r\n");
            }
            catch (Exception ex)
            {
                _log.AppendText("[Mediapipe] Hand Init error: " + ex + "\r\n");
            }
        }

        private void TryInitHolistic()
        {
            try
            {
                string? path = DllSearchUtil.ResolveGraph("holistic_tracking_cpu.pbtxt");
                if (path == null)
                {
                    _log.AppendText("[Mediapipe] Holistic graph 未找到。\r\n");
                    return;
                }
                int rc = Native.MediapipeHolisticTrackingInit(path);
                _log.AppendText($"[Mediapipe] Holistic Init rc={rc} path={path}\r\n");
            }
            catch (Exception ex)
            {
                _log.AppendText("[Mediapipe] Holistic Init error: " + ex + "\r\n");
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ApplicationExit += (s, e) => { };
            Application.Run(new MainForm());
        }
    }
}
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using System.Runtime.InteropServices;
using System.Linq;
using System.Diagnostics;
using VtuberHubStudio.ModelViewer;

namespace VtuberHubStudio;

public partial class MainWindow : System.Windows.Window
{
    private VideoCapture? _capture;
    private CancellationTokenSource? _cts;
    private bool _enableMediapipe = false;
    private bool _showOverlay = true;
    private bool _flipHorizontal = false;
    private Scalar _overlayColor = new Scalar(0, 255, 0, 255);
    private int _overlayThickness = 2;
    private readonly System.Diagnostics.Stopwatch _fpsWatch = new();
    private int _frameCount = 0;
    private int _lastFps = 0;
    private VideoCaptureAPIs _captureBackend = VideoCaptureAPIs.MSMF;
    private int _deviceIndex = 0;

    private bool _autoResolution = true;
    private (int w, int h)[] _resOptions = new (int, int)[] { (640, 480), (1280, 720) };
    private int _currentResIndex = 1;
    private WriteableBitmap? _wb;
    private byte[]? _buffer;
    private int _stride;
    private volatile int _renderBusy = 0;

    private bool _mediapipeReady = false;
    private string? _mediapipeGraphPath;
    private bool _lastMpOk = false;
    private DateTime _lastMpFrameAt = DateTime.MinValue;
    private int[] _lastMpResults = new int[4];
    private int _lastMpRc = -999;
    private MediapipeService _mp = new MediapipeService();
    private HandTrackingService _hand = new HandTrackingService();
    private SimpleFrameLogger _logger = new SimpleFrameLogger(30);

    // 高级设置偏好（null 表示自动）
    private VideoCaptureAPIs? _prefBackend = null; // DSHOW/MSMF
    private int? _prefFourCC = null; // FourCC int
    private (int w, int h)? _prefResolution = null;
    private int? _prefFps = null;

    // 资源监控采样缓存
    private readonly Process _proc = Process.GetCurrentProcess();
    private DateTime _lastCpuMeasureAt = DateTime.UtcNow;
    private TimeSpan _lastProcCpu = TimeSpan.Zero;

    public MainWindow()
    {
        InitializeComponent();
        TbStart.Click += (_, __) => StartCamera();
        TbStop.Click += (_, __) => StopCamera();
        TbRefreshDevices.Click += (_, __) => EnumerateDevices();
        TbOpen3D.Click += (_, __) => { var w = new ModelViewerWindow(); w.Show(); };
        MenuOpenModel.Click += (_, __) => { var w = new ModelViewerWindow(); w.Show(); };
        TbDeviceCombo.SelectionChanged += (_, __) =>
        {
            if (TbDeviceCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                if (int.TryParse(item.Tag?.ToString(), out var idx)) _deviceIndex = idx;
            }
        };
        ResolutionCombo.SelectionChanged += (_, __) =>
        {
            var text = (ResolutionCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "自动";
            _autoResolution = text == "自动";
            if (_capture != null && _capture.IsOpened())
            {
                if (_autoResolution) return;
                if (text == "1280x720") ApplyResolution(1);
                else if (text == "640x480") ApplyResolution(0);
            }
        };

        AdvApply.Click += (_, __) => ApplyAdvancedSettings();

        MpEnable.Click += (_, __) => SetMediapipe(true);
        MpDisable.Click += (_, __) => SetMediapipe(false);
        MpShowOverlay.Checked += (_, __) => { _showOverlay = true; _logger.LogEvent("OVERLAY", "show=true"); };
        MpShowOverlay.Unchecked += (_, __) => { _showOverlay = false; _logger.LogEvent("OVERLAY", "show=false"); };
        MpFlip.Checked += (_, __) => { _flipHorizontal = true; _logger.LogEvent("FLIP", "flip=true"); };
        MpFlip.Unchecked += (_, __) => { _flipHorizontal = false; _logger.LogEvent("FLIP", "flip=false"); };
        MpOverlayColor.SelectionChanged += (_, __) =>
        {
            var text = (MpOverlayColor.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "绿色";
            _overlayColor = text switch
            {
                "红色" => new Scalar(0, 0, 255, 255),
                "蓝色" => new Scalar(255, 0, 0, 255),
                "黄色" => new Scalar(0, 255, 255, 255),
                _ => new Scalar(0, 255, 0, 255)
            };
        };
        MpOverlayThickness.ValueChanged += (_, __) => _overlayThickness = (int)MpOverlayThickness.Value;

        MenuExit.Click += (_, __) => Close();
        MenuAbout.Click += (_, __) => MessageBox.Show("VtuberHub Studio\n预览版界面与功能原型", "关于");
        MenuLightTheme.Click += (_, __) => ApplyTheme(isDark:false);
        MenuDarkTheme.Click += (_, __) => ApplyTheme(isDark:true);

        // 初始化叠加与进度
        UpdateStatus("摄像头：未启动", fps:0, res:"-");
        SetProgress("系统", "就绪", 0);
        UpdateResourceOverlay();
        EnumerateDevices();
    }

    private void EnumerateDevices()
    {
        TbDeviceCombo.Items.Clear();
        for (int i = 0; i < 3; i++)
        {
            var item = new System.Windows.Controls.ComboBoxItem { Content = $"设备 {i}", Tag = i };
            if (i == _deviceIndex) item.IsSelected = true;
            TbDeviceCombo.Items.Add(item);
        }
    }

    private void ApplyResolution(int index)
    {
        _currentResIndex = index;
        var (w, h) = _resOptions[index];
        _capture?.Set(VideoCaptureProperties.FrameWidth, w);
        _capture?.Set(VideoCaptureProperties.FrameHeight, h);
    }

    // 启动摄像头时选择最大的分辨率范围（不强制降为640x480）
    private void ApplyMaxResolutionIfAuto()
    {
        if (_capture == null || !_capture.IsOpened()) return;

        // 若用户选择了手动分辨率或高级偏好，则尊重该设置
        if (!_autoResolution)
        {
            var (w, h) = _resOptions[_currentResIndex];
            _capture.Set(VideoCaptureProperties.FrameWidth, w);
            _capture.Set(VideoCaptureProperties.FrameHeight, h);
            return;
        }
        if (_prefResolution.HasValue)
        {
            var pr = _prefResolution.Value;
            _capture.Set(VideoCaptureProperties.FrameWidth, pr.w);
            _capture.Set(VideoCaptureProperties.FrameHeight, pr.h);
            return;
        }

        // 按从大到小的候选列表尝试，取第一个成功的最大分辨率
        var candidates = new (int w, int h)[] { (1920, 1080), (1280, 720), (640, 480) };
        foreach (var c in candidates)
        {
            _capture.Set(VideoCaptureProperties.FrameWidth, c.w);
            _capture.Set(VideoCaptureProperties.FrameHeight, c.h);
            int rw = (int)_capture.FrameWidth;
            int rh = (int)_capture.FrameHeight;
            if (Math.Abs(rw - c.w) <= 8 && Math.Abs(rh - c.h) <= 8)
            {
                // 成功应用最大可用分辨率
                UpdateStatus(null, null, $"{rw}x{rh}");
                break;
            }
        }
    }

    private void ApplyAdvancedSettings()
    {
        // 读取 UI 选择并保存偏好
        string backend = (AdvBackend.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "自动";
        string codec = (AdvCodec.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "自动";
        string res = (AdvResolution.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "自动";
        string fps = (AdvFps.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "自动";

        _prefBackend = backend switch
        {
            "DSHOW" => VideoCaptureAPIs.DSHOW,
            "MSMF" => VideoCaptureAPIs.MSMF,
            _ => (VideoCaptureAPIs?)null
        };

        _prefFourCC = codec switch
        {
            "MJPG" => FourCC.MJPG,
            "YUY2" => FourCC.FromString("YUY2"),
            "H264" => FourCC.FromString("H264"),
            _ => (int?)null
        };

        _prefResolution = res switch
        {
            "640x480" => (640, 480),
            "1280x720" => (1280, 720),
            "1920x1080" => (1920, 1080),
            _ => ((int, int)?)null
        };

        _prefFps = int.TryParse(fps, out var fpsNum) ? fpsNum : (int?)null;

        AdvHint.Text = "配置已保存，重启摄像头生效";
        _logger.LogEvent("ADV_APPLY", $"backend={_prefBackend?.ToString() ?? "自动"} codec={_prefFourCC?.ToString() ?? "自动"} res={( _prefResolution.HasValue ? ($"{_prefResolution.Value.w}x{_prefResolution.Value.h}") : "自动")} fps={_prefFps?.ToString() ?? "自动"}");

        // 若摄像头正在运行，自动重启以应用设置
        bool wasRunning = _capture != null && _capture.IsOpened();
        if (wasRunning)
        {
            StopCamera();
            StartCamera();
        }
    }

    private void RenderToMonitor(Mat mat)
    {
        // 移除渲染阶段的水平翻转，改为在采集/检测阶段统一处理
        using var rgba = new Mat();
        Cv2.CvtColor(mat, rgba, ColorConversionCodes.BGR2BGRA);
        int w = rgba.Cols, h = rgba.Rows;
        int stride = (int)rgba.Step();
        int len = stride * h;

        // 渲染跳帧保护：若上一帧仍在 UI 线程写入，则直接丢弃当前帧，避免阻塞采集线程
        if (System.Threading.Interlocked.Exchange(ref _renderBusy, 1) == 1)
        {
            return;
        }

        // 先在采集线程拷贝到托管缓冲，避免 BeginInvoke 中使用已释放的 Mat 数据
        var frameBytes = new byte[len];
        Marshal.Copy(rgba.Data, frameBytes, 0, len);

        // 异步调度到 UI 线程写入，降低阻塞
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (_wb == null || _wb.PixelWidth != w || _wb.PixelHeight != h)
                {
                    _wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                    _stride = stride;
                    MonitorImage.Source = _wb;
                }
                _wb!.WritePixels(new Int32Rect(0, 0, w, h), frameBytes, stride, 0);
            }
            finally
            {
                _renderBusy = 0;
            }
        }), System.Windows.Threading.DispatcherPriority.Render);
    }

    private void StartCamera()
    {
        if (_capture != null) return;
        if (_deviceIndex < 0) { MessageBox.Show("未检测到摄像头设备。", "摄像头错误"); return; }
        _cts = new CancellationTokenSource();
        // 外置摄像头：若设置了偏好后端则优先使用，否则 DSHOW → MSMF
        _captureBackend = _prefBackend ?? VideoCaptureAPIs.DSHOW;
        _capture = new VideoCapture(_deviceIndex, _captureBackend);
        SetProgress("摄像头", "打开设备", 20);
        // 提前设置期望参数，部分后端会在 Open 前读取
        try
        {
            // 分辨率优先使用高级设置；否则遵循现有自动/手动
            var resToUse = _prefResolution;
            if (resToUse.HasValue)
            {
                _autoResolution = false;
                _capture.Set(VideoCaptureProperties.FrameWidth, resToUse.Value.w);
                _capture.Set(VideoCaptureProperties.FrameHeight, resToUse.Value.h);
            }
            else if (!_autoResolution)
            {
                var (w, h) = _resOptions[_currentResIndex];
                _capture.Set(VideoCaptureProperties.FrameWidth, w);
                _capture.Set(VideoCaptureProperties.FrameHeight, h);
            }
            _capture.Set(VideoCaptureProperties.Fps, _prefFps ?? 30);
            // 指定编码（若设置了偏好）
            if (_prefFourCC.HasValue)
            {
                _capture.Set(VideoCaptureProperties.FourCC, _prefFourCC.Value);
            }
        }
        catch { }
        _capture.Open(_deviceIndex, _captureBackend);
        if (!_capture.IsOpened())
        {
            _captureBackend = VideoCaptureAPIs.MSMF;
            _capture?.Release();
            _capture?.Dispose();
            _capture = new VideoCapture(_deviceIndex, _captureBackend);
            _capture.Open(_deviceIndex, _captureBackend);
        }
        if (!_capture.IsOpened())
        {
            UpdateStatus("摄像头：打开失败", fps:0, res:"-");
            MessageBox.Show($"无法打开设备 {_deviceIndex}，请在工具栏选择其他设备或刷新列表。", "摄像头错误");
            _capture?.Dispose();
            _capture = null;
            _cts?.Dispose();
            _cts = null;
            SetProgress("摄像头", "打开失败", 0);
            return;
        }
        _capture.Set(VideoCaptureProperties.BufferSize, 1);
        _capture.Set(VideoCaptureProperties.ConvertRgb, 1);
        SetProgress("摄像头", "设置参数", 40);
    
    // 选择最大的分辨率范围，不自动降为640x480
    ApplyMaxResolutionIfAuto();
    
        // 快速预热：丢弃首批帧，规避自动曝光/白平衡导致的卡顿与首帧延迟
        try
        {
            using var warm = new Mat();
            for (int i = 0; i < 8; i++)
            {
                if (!_capture.Read(warm)) break;
            }
        }
        catch { }

        _fpsWatch.Restart();
        _frameCount = 0;
        _lastFps = 0;
        UpdateStatus($"摄像头：运行中({_captureBackend})", fps:0, res:$"{_capture.FrameWidth}x{_capture.FrameHeight}");
        _logger.LogSessionStart($"device={_deviceIndex}, backend={_captureBackend}, res={_capture.FrameWidth}x{_capture.FrameHeight}, fps={(int)_capture.Fps}, flip={_flipHorizontal}, overlay={_showOverlay}, pref_backend={_prefBackend?.ToString() ?? "自动"}, pref_fourcc={_prefFourCC?.ToString() ?? "自动"}, pref_res={( _prefResolution.HasValue ? ($"{_prefResolution.Value.w}x{_prefResolution.Value.h}") : "自动")}, pref_fps={_prefFps?.ToString() ?? "自动"}, mp_enabled={_enableMediapipe}");
        SetProgress("摄像头", "采集中", 80);
        Task.Run(() => CameraLoop(_cts.Token));
        SetProgress("摄像头", "运行中", 100);
    }

    private void CameraLoop(CancellationToken token)
    {
        using var mat = new Mat();
        int noFrameTicks = 0;
        while (!token.IsCancellationRequested && _capture != null && _capture.IsOpened())
        {
            bool got = _capture.Read(mat);
            if (got && !mat.Empty())
            {
                noFrameTicks = 0;
                _frameCount++;

                // 在检测前根据设置进行水平翻转，保持与 Mediapipe 图的自拍假设一致
                if (_flipHorizontal)
                {
                    Cv2.Flip(mat, mat, FlipMode.Y);
                }

                if (_enableMediapipe && _mediapipeReady)
                {
                    var sw = Stopwatch.StartNew();
                    var res = _mp.Detect(mat, _showOverlay);
                    _lastMpRc = res.Rc;
                    _lastMpOk = res.Success;
                    _lastMpFrameAt = DateTime.UtcNow;
                    if (res.Success)
                    {
                        _lastMpResults = new int[] { res.LeftArm, res.RightArm, res.LeftGesture, res.RightGesture };
                    }

                    int handRc = -1;
                    if (_hand.Ready)
                    {
                        handRc = _hand.Detect(mat);
                        if (_showOverlay)
                        {
                            _hand.Draw(mat, _overlayColor, _overlayThickness);
                        }
                    }
                    sw.Stop();
                    _logger.LogIfNeeded(_frameCount, _lastMpRc, handRc, sw.Elapsed.TotalMilliseconds, _lastMpResults);
                }

                RenderToMonitor(mat);

                if (_fpsWatch.ElapsedMilliseconds >= 1000)
                {
                    int fps = _frameCount;
                    _lastFps = fps;
                    AdjustResolutionIfAuto(fps);
                    Dispatcher.Invoke(() => { UpdateStatus(null, fps, $"{_capture!.FrameWidth}x{_capture!.FrameHeight}"); UpdateResourceOverlay(); });
                    _logger.LogTick(_frameCount, fps, (int)_capture!.FrameWidth, (int)_capture!.FrameHeight);
                    _frameCount = 0;
                    _fpsWatch.Restart();
                }
            }
            else
            {
                noFrameTicks++;
                Thread.Sleep(10);
                if (noFrameTicks > 200)
                {
                    var alt = _captureBackend == VideoCaptureAPIs.MSMF ? VideoCaptureAPIs.DSHOW : VideoCaptureAPIs.MSMF;
                    _capture?.Release(); _capture?.Dispose();
                    _capture = new VideoCapture(_deviceIndex, alt);
                    _capture.Open(_deviceIndex, alt);
                    _captureBackend = alt;
                    noFrameTicks = 0;
                    UpdateStatus($"摄像头：切换后端({_captureBackend})", fps:null, res:null);
                    _logger.LogEvent("CAM_BACKEND_SWITCH", $"new_backend={_captureBackend}");
                }
            }
        }
    }

    private void StopCamera()
    {
        _cts?.Cancel();
        _cts = null;
        _capture?.Release();
        _capture?.Dispose();
        _capture = null;
        UpdateStatus("摄像头：未启动", fps:0, res:"-");
        SetProgress("摄像头", "未启动", 0);
        _logger.LogEvent("CAM_STOP", "");
    }
    private void ApplyTheme(bool isDark)
    {
        var bg = isDark ? Color.FromRgb(30, 30, 30) : Color.FromRgb(245, 245, 245);
        Background = new SolidColorBrush(bg);
    }
    private void AdjustResolutionIfAuto(int fps)
    {
        // 保持最大分辨率，不做自动降级；启动时已尝试最大分辨率
        if (_capture == null) return;
    }

    private string GetMediapipeOverlayStatus()
    {
        if (!_enableMediapipe) return "Mediapipe: 未启用";
        if (!_mediapipeReady) return "Mediapipe: 初始化失败";
        var age = (DateTime.UtcNow - _lastMpFrameAt).TotalSeconds;
        if (age <= 1.5 && _lastMpOk)
        {
            var r = _lastMpResults;
            var la = MapArm(r[0]);
            var ra = MapArm(r[1]);
            var lg = MapGesture(r[2]);
            var rg = MapGesture(r[3]);
            // 附带原始返回值，便于诊断码表不一致或未识别
            return $"Mediapipe: 有结果 | LArm:{la} RArm:{ra} LGest:{lg} RGest:{rg} | raw:[{r[0]},{r[1]},{r[2]},{r[3]}] | rc:{_lastMpRc}";
        }
        return "Mediapipe: 检测中";
    }

    private void UpdateStatus(string? cam, int? fps, string? res)
    {
        if (cam != null) Title = $"VtuberHub Studio - {cam}";
        string mp = GetMediapipeOverlayStatus();
        if (fps != null)
        {
            if (res != null) OverlayFps.Text = $"FPS: {fps}  分辨率: {res}  |  {mp}";
            else OverlayFps.Text = $"FPS: {fps}  |  {mp}";
        }
        else if (res != null)
        {
            OverlayFps.Text = $"分辨率: {res}  |  {mp}";
        }
        else
        {
            // 强制刷新仅状态
            var currentRes = _capture != null ? $"{_capture.FrameWidth}x{_capture.FrameHeight}" : "-";
            OverlayFps.Text = $"FPS: {_lastFps}  分辨率: {currentRes}  |  {mp}";
        }
    }

    private void UpdateResourceOverlay()
    {
        try
        {
            var now = DateTime.UtcNow;
            var cpuDeltaMs = (_proc.TotalProcessorTime - _lastProcCpu).TotalMilliseconds;
            var elapsedMs = (now - _lastCpuMeasureAt).TotalMilliseconds;
            double cpu = elapsedMs > 0 ? (cpuDeltaMs / elapsedMs) * 100.0 / Environment.ProcessorCount : 0;
            _lastProcCpu = _proc.TotalProcessorTime;
            _lastCpuMeasureAt = now;
            var memMB = _proc.WorkingSet64 / (1024.0 * 1024.0);
            OverlayResourceText.Text = $"CPU: {cpu:0.0}%  Mem: {memMB:0}MB";
            _logger.LogEvent("RES", $"cpu={cpu:0.0}% mem={memMB:0}MB");
        }
        catch { }
    }

    private void SetProgress(string module, string step, int percent)
    {
        if (percent < 0) percent = 0; if (percent > 100) percent = 100;
        OverlayProgressBar.Value = percent;
        OverlayProgressText.Text = $"{module}: {step} ({percent}%)";
    }

    protected override void OnClosed(EventArgs e)
    {
        StopCamera();
        base.OnClosed(e);
    }

    private void SetMediapipe(bool enabled)
    {
        _enableMediapipe = enabled;
        if (enabled)
        {
            // 每次启用前强制清理并重置状态，避免上一次 Release 后 _mediapipeReady 残留
            try { _mp.Release(); } catch { }
            try { _hand.Release(); } catch { }
            _mediapipeReady = false;
            _lastMpOk = false;
            _lastMpFrameAt = DateTime.MinValue;
            _lastMpResults = new int[4];
            _lastMpRc = -999;
            _logger.LogEvent("MP_ENABLE", $"flip={_flipHorizontal} overlay={_showOverlay}");
            // 取消强制分辨率，按自动逻辑由程序选择最佳分辨率
            SetProgress("Mediapipe", "查找目录", 10);
            // 先确保拷贝图与 modules 到运行目录，保证 include 可解析
            var mpRoot = MediapipeAssets.EnsureAssets();
            EnsureMediapipeReady();
            _logger.LogEvent("MP_READY", $"ready={_mediapipeReady} graph={_mediapipeGraphPath} mp_rc={_mp.LastRc} hand_rc={_hand.LastRc}");
            if (!_mediapipeReady)
            {
                MpStatus.Text = "状态：初始化失败";
                SetProgress("Mediapipe", "初始化失败", 0);
                MessageBox.Show("Mediapipe 初始化失败，请确认 mediapipe/dll 及依赖已在 PATH。", "Mediapipe 错误");
                _enableMediapipe = false;
            }
            else
            {
                MpStatus.Text = "状态：已启用";
                SetProgress("Mediapipe", "初始化完成", 100);
                UpdateStatus(null, null, null);
            }
        }
        else
        {
            try { _mp.Release(); } catch { }
            try { _hand.Release(); } catch { }
            _mediapipeReady = false;
            _lastMpOk = false;
            _lastMpFrameAt = DateTime.MinValue;
            _lastMpResults = new int[4];
            _lastMpRc = -999;
            MpStatus.Text = "状态：未启用";
            SetProgress("Mediapipe", "未启用", 0);
            UpdateStatus(null, null, null);
            _logger.LogEvent("MP_DISABLE", "");
        }
    }

    private static string MapArm(int code)
    {
        return code switch
        {
            -1 => "Unknown",
            0 => "None",
            1 => "Up",
            2 => "Down",
            _ => "Unknown"
        };
    }

    private static string MapGesture(int code)
    {
        return code switch
        {
            0 => "None",
            1 => "One",
            2 => "Two",
            3 => "Three",
            4 => "Four",
            5 => "Five",
            6 => "Six",
            7 => "ThumbUp",
            8 => "Ok",
            9 => "Fist",
            _ => "Unknown"
        };
    }

    private static class MediapipeNative
    {
        [DllImport("MediapipeHolisticTracking.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern int MediapipeHolisticTrackingInit(string graphPath,
        [MarshalAs(UnmanagedType.I1)] bool needVideoOutput,
        [MarshalAs(UnmanagedType.I1)] bool needPose,
        [MarshalAs(UnmanagedType.I1)] bool needHand,
        [MarshalAs(UnmanagedType.I1)] bool needFace);
         [DllImport("MediapipeHolisticTracking.dll", CallingConvention = CallingConvention.Cdecl)]
         public static extern int MediapipeHolisticTrackingDetectFrameDirect(int width, int height, IntPtr rgbData, [Out] int[] results, [MarshalAs(UnmanagedType.I1)] bool showResultImage);
         [DllImport("MediapipeHolisticTracking.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int MediapipeHolisticTrackingRelease();
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    // 使用现代 DLL 搜索目录 API，支持多个目录
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint DirectoryFlags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern System.IntPtr AddDllDirectory(string NewDirectory);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool RemoveDllDirectory(System.IntPtr Cookie);
    private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
    private const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;
    private const uint LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200;
    private const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;
    private static string? FindDirUp(string start, string targetRel)
    {
        var dir = new DirectoryInfo(start);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            var tryPath = System.IO.Path.Combine(dir.FullName, targetRel);
            if (Directory.Exists(tryPath)) return tryPath;
            dir = dir.Parent;
        }
        return null;
    }

    private void EnsureMediapipeReady()
    {
        // 若服务已就绪则直接记录状态后返回；否则尝试重新初始化（避免启用/禁用后卡住）
        if (_mp.Ready || _hand.Ready)
        {
            _mediapipeReady = true;
            _mediapipeGraphPath = _mp.GraphPath ?? _hand.GraphPath;
            return;
        }
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var runMpRoot = System.IO.Path.Combine(baseDir, "mediapipe");
            var testDir = FindDirUp(baseDir, "test");
            var dllDir = FindDirUp(baseDir, System.IO.Path.Combine("mediapipe", "dll"));
            var candidates = new string[] { runMpRoot, testDir ?? string.Empty, dllDir ?? string.Empty };
            candidates = candidates.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).ToArray();
            if (candidates.Length == 0)
            {
                _mediapipeReady = false;
                return;
            }

            // 如果存在 modules 或 mediapipe/modules，则将当前工作目录切换到该根目录，便于图中 #include 路径解析
            var selectedRoot = candidates.First();
            var modulesDir1 = System.IO.Path.Combine(selectedRoot, "modules");
            var modulesDir2 = System.IO.Path.Combine(selectedRoot, "mediapipe", "modules");
            if (Directory.Exists(modulesDir1) || Directory.Exists(modulesDir2))
            {
                try { System.IO.Directory.SetCurrentDirectory(selectedRoot); } catch { }
            }

            SetProgress("Mediapipe", "设置搜索目录", 30);
            _mp.Initialize();
            _hand.Initialize();
            _mediapipeReady = _mp.Ready || _hand.Ready;
            _mediapipeGraphPath = _mp.GraphPath ?? _hand.GraphPath;
            SetProgress("Mediapipe", _mediapipeReady ? "初始化完成" : $"初始化失败(rc={_mp.LastRc}, hand_rc={_hand.LastRc})", _mediapipeReady ? 100 : 0);
            // 不强制分辨率，按自动策略运行
        }
        catch
        {
            _mediapipeReady = false;
            SetProgress("Mediapipe", "初始化异常", 0);
        }
    }
}
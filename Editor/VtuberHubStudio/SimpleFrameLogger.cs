using System;
using System.IO;
using System.Text;

namespace VtuberHubStudio
{
    // 轻量日志器：每 N 帧记录 rc 与耗时到文件，同时支持会话与事件日志
    public class SimpleFrameLogger
    {
        private readonly int _logEveryN;
        private readonly string _logFile;
        private int _counter = 0;
        private readonly object _lock = new object();

        public SimpleFrameLogger(int logEveryN = 30, string? baseDir = null)
        {
            _logEveryN = Math.Max(1, logEveryN);
            baseDir ??= AppDomain.CurrentDomain.BaseDirectory;
            var logDir = Path.Combine(baseDir, "logs");
            try { Directory.CreateDirectory(logDir); } catch { }
            _logFile = Path.Combine(logDir, $"mp_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            try
            {
                SafeAppend("# ts, frame_idx, mp_rc, hand_rc, detect_ms, raw\n");
            }
            catch { }
        }

        private void SafeAppend(string text)
        {
            try
            {
                lock (_lock) { File.AppendAllText(_logFile, text, Encoding.UTF8); }
            }
            catch { }
        }

        // 会话启动元数据
        public void LogSessionStart(string info)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");
            SafeAppend($"# session_start {ts} {info}\n");
        }

        // 任意事件日志
        public void LogEvent(string tag, string info)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");
            SafeAppend($"[{tag}] {ts} {info}\n");
        }

        // 每秒状态快照（可选）
        public void LogTick(int frameIndex, int fps, int width, int height)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");
            SafeAppend($"tick {ts}, frame_idx={frameIndex}, fps={fps}, res={width}x{height}\n");
        }

        // 兼容旧调用
        public void LogIfNeeded(int frameIndex, int mpRc, int handRc, double detectMs)
        {
            LogIfNeeded(frameIndex, mpRc, handRc, detectMs, null);
        }

        public void LogIfNeeded(int frameIndex, int mpRc, int handRc, double detectMs, int[]? raw)
        {
            _counter++;
            if (_counter % _logEveryN != 0) return;
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");
            string rawTxt = raw != null && raw.Length >= 4 ? $", [{raw[0]},{raw[1]},{raw[2]},{raw[3]}]" : string.Empty;
            var line = $"{ts}, {frameIndex}, {mpRc}, {handRc}, {detectMs:0.0}{rawTxt}\n";
            SafeAppend(line);
        }
    }
}
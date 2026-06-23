Mediapipe DLL Test (WinForms)

Build
- Prereq: .NET 6 SDK on Windows.
- In this folder: `dotnet build -c Release`

Run
- Ensure `Mediapipe_Hand_Tracking.dll` and `MediapipeHolisticTracking.dll` plus required native deps (e.g. `opencv_world*.dll`) are on PATH or copied next to the built exe under `bin/Release/net6.0-windows/`.
- Optional graph files are referenced from `../mediapipe/dll/*.pbtxt` relative to the exe; update paths in `Program.cs` if your layout differs.
- Start: `dotnet run -c Release` or run the produced `.exe`.

What it does
- P/Invoke calls to test exported functions:
  - `Mediapipe_Hand_Tracking_Init(...)`
  - `MediapipeHolisticTrackingInit(...)`
- Logs return codes and exceptions in the text box.
# VtuberHub Godot GDExtension 脚手架

本目录包含面向 Godot 4 的 GDExtension 集成与演示工程。

## 文件一览
- `VtuberHub.gdextension`：扩展配置，指向构建后的 Windows DLL。
- `project.godot`：最小可运行的 Godot 项目配置。
- `demo.tscn`：带有简单 UI 的测试场景，一键预览装配与材质覆盖。
- `demo_ui.gd`：驱动 `AvatarAssembler3D` 的示例 UI 脚本（加载基础、挂载服装、应用材质、应用预设）。
- `avatar_definition.gd`：`AvatarDefinition` 资源，描述基础场景、服装目录与默认材质参数。
- `avatar_preset.gd`：`AvatarPreset` 资源，描述所选服装与材质覆盖，引用一个 Definition。
- `addons/vtuberhub_editor/`：编辑器插件，包含右侧 Dock 面板与 `.avatar.json` 的自定义导入器。

## Windows 构建步骤
1. 安装依赖：Visual Studio 2022（MSVC）、git、CMake、Python 3。
2. 构建（或自动构建）`godot-cpp`：
   - 在仓库根目录执行脚本，可自动 clone/build（也可自备）：
   - `powershell -ExecutionPolicy Bypass -File ..\build.ps1 -GodotCppDir "C:\\path\\to\\godot-cpp" -BuildType Release`
3. 构建扩展 DLL：
   - 同上脚本会生成 DLL，产物路径：`GDExtension/bridge/build/bin/vtuberhub_bridge.windows.dll`
4. 放置 DLL 到 Godot 工程：
   - 复制到 `<项目根>/addons/vtuberhub/bin/vtuberhub_bridge.windows.dll`
5. 打开本目录作为 Godot 项目，直接运行 `demo.tscn`。

## 启用编辑器插件
- Godot 菜单：Project > Project Settings > Plugins > 启用 `VtuberHub Editor`。
- 右侧 Dock 功能：
  - 选择 `AvatarPreset`（`.tres/.res`）路径；
  - 对场景树中选中的 `AvatarAssembler3D` 节点执行“一键应用”。
- 自定义导入器：将 `.avatar.json` 放入工程，自动导入为 `AvatarPreset .res`。

### `.avatar.json` 导入示例
```json
{
  "definition_path": "res://avatar_def.tres",
  "wardrobe_selection": [
	"res://wardrobe/hair_a.tscn",
	"res://wardrobe/top_basic.tscn"
  ],
  "materials": {
	"skin_color": "#FFD9CC",
	"skin_tone": 0.5
  }
}
```
- 颜色可用字符串表示；更稳妥的方式是直接在 Godot 中创建 `.tres` 资源。

## 使用 `demo.tscn`
- Load Base：填写基础模型 `res://.../*.tscn` 路径并点击加载。
- Attach Wardrobe：每行填写一个服装 `res://.../*.tscn` 路径并挂载。
- Apply Materials：选择 `skin_color` 与 `skin_tone` 后应用到当前模型。
- Apply Preset：填写 `AvatarPreset` 的 `res://.../*.tres/.res`，一键应用 Definition + Wardrobe + 材质覆盖。

## CMake 的 OpenCV 开关
- 选项 `WITH_OPENCV`（默认 ON）。启用时 CMake 尝试：
  - `find_package(OpenCV CONFIG QUIET)`（vcpkg/官方包方式）；
  - 若找不到，回退到经典查找，并使用 `OpenCV_DIR`/`OPENCV_DIR`/环境变量与常见路径（包含 `../../OpenCV`）作为提示。
- 若仍未找到，请在配置时设置 `-DOpenCV_DIR=...` 或同名环境变量；也可将 `WITH_OPENCV=OFF` 以仅编译不依赖 Mediapipe 的模块。

## 注意事项
- `AvatarAssembler3D` 默认在基础场景下查找名为 `Skeleton3D` 的骨骼节点；若你的模型路径不同，可在 C++ 中调整检索逻辑。
- 服装场景当前直接作为子节点挂载；要正确驱动蒙皮，需要与基础模型使用同一套骨骼/BindPose（后续可加入骨骼索引重映射）。
- 材质覆盖要求 `ShaderMaterial` 含参数 `skin_color` 与 `skin_tone`。

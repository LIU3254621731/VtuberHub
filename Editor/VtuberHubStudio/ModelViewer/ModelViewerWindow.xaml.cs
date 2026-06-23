using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Assimp;
using VtuberHubStudio.ModelViewer;
using System.Windows.Input;
using Microsoft.Win32;

namespace VtuberHubStudio.ModelViewer
{
    public partial class ModelViewerWindow : Window
    {
        private readonly AssimpContext _assimp = new AssimpContext();
        private Model3DGroup _currentModel = new Model3DGroup();
        private List<ModelVisual3D> _skeletonVisuals = new List<ModelVisual3D>();
        private Transform3DGroup _modelTransform = new Transform3DGroup();
        private readonly string _defaultModelsDir = @"c:\Users\32546\Desktop\VtuberHub\3dmouduls";
        // 摄像头（相机）交互状态
        private double _camYawDeg = 0, _camPitchDeg = -10, _camDistance = 6;
        private Point3D _camTarget = new Point3D(0, 0, 0);
        private bool _isRotating = false, _isPanning = false;
        private Point _lastMouse;
        private AvatarInstance? _avatarInstance;
        private string? _suitTexturePath;
        private JointName _currentJoint = JointName.LeftUpperArm;
        private Model3DGroup _sceneExtras = new Model3DGroup();
        private GeometryModel3D _groundModel;
        private GeometryModel3D _backdropModel;
        // 新增：导入模型的网格映射 & 变形目标
        private Dictionary<string, GeometryModel3D> _importedMeshMap = new Dictionary<string, GeometryModel3D>(StringComparer.OrdinalIgnoreCase);
        private List<GeometryModel3D> _noseModels = new List<GeometryModel3D>();
        private List<GeometryModel3D> _earModels = new List<GeometryModel3D>();
        // 新增：导入模型的主要部位集合（用于近似形体调节）
        private List<GeometryModel3D> _headModels = new List<GeometryModel3D>();
        private List<GeometryModel3D> _shoulderModels = new List<GeometryModel3D>();
        private List<GeometryModel3D> _waistModels = new List<GeometryModel3D>();
        private List<GeometryModel3D> _hipModels = new List<GeometryModel3D>();
        private List<GeometryModel3D> _forearmModels = new List<GeometryModel3D>();
        private List<GeometryModel3D> _shinModels = new List<GeometryModel3D>();
        // 新增：导入模型的关节旋转缓存（避免叠加多次转换）
        private Dictionary<(GeometryModel3D gm, JointName joint), AxisAngleRotation3D> _importedJointRotations = new();
        private class AssetItem { public string Name { get; set; } = ""; public string Path { get; set; } = ""; }

        public ModelViewerWindow()
        {
            InitializeComponent();
            ViewportContainer.Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
            SceneRoot.Content = _currentModel;
            _modelTransform.Children.Add(new ScaleTransform3D(1, 1, 1));
            _modelTransform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(0, 1, 0), 0))); // yaw
            _modelTransform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(1, 0, 0), 0))); // pitch
            _modelTransform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(0, 0, 1), 0))); // roll
            _currentModel.Transform = _modelTransform;

            // 默认相机
            Viewport.Camera = new PerspectiveCamera
            {
                Position = new Point3D(0, 2, 6),
                LookDirection = new System.Windows.Media.Media3D.Vector3D(0, -0.2, -1),
                UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0),
                FieldOfView = 45
            };

            // 交互：拖拽旋转/平移，滚轮局部缩放
            Viewport.MouseWheel += Viewport_MouseWheel;
            Viewport.MouseDown += Viewport_MouseDown;
            Viewport.MouseMove += Viewport_MouseMove;
            Viewport.MouseUp += Viewport_MouseUp;

            BtnOpen.Click += (_, __) => OpenModel();
            BtnReset.Click += (_, __) => ResetView();
            ToggleSkeleton.Checked += (_, __) => UpdateSkeletonVisibility(true);
            ToggleSkeleton.Unchecked += (_, __) => UpdateSkeletonVisibility(false);
            CbBackground.SelectionChanged += (_, __) => ApplyBackground();
            LightIntensity.ValueChanged += (_, __) => ApplyLightIntensity();
            AmbientIntensity.ValueChanged += (_, __) => ApplyAmbientIntensity();
            FillIntensity.ValueChanged += (_, __) => ApplyFillIntensity();
            CbRenderPreset.SelectionChanged += (_, __) => ApplyRenderPreset();
            BtnRestoreRender.Click += (_, __) => RestoreDefaultRender();
            ScaleSlider.ValueChanged += (_, __) => ApplyScale();
            YawSlider.ValueChanged += (_, __) => ApplyRotation();
            PitchSlider.ValueChanged += (_, __) => ApplyRotation();
            RollSlider.ValueChanged += (_, __) => ApplyRotation();
            // 新建人物与材质参数联动
            BtnNewAvatar.Click += (_, __) => CreateNewAvatar();
            CbSkin.SelectionChanged += (_, __) => { ApplyAvatarMaterials(); RebuildAvatarIfAny(); };
            CbCloth.SelectionChanged += (_, __) => { ApplyAvatarMaterials(); RebuildAvatarIfAny(); };
            ShininessSlider.ValueChanged += (_, __) => { ApplyAvatarMaterials(); RebuildAvatarIfAny(); };
            // 新增：眼睛、头发、体型细分、纹理
            CbEye.SelectionChanged += (_, __) => RebuildAvatarIfAny();
            CbHair.SelectionChanged += (_, __) => RebuildAvatarIfAny();
            CbClothingStyle.SelectionChanged += (_, __) => RebuildAvatarIfAny();
            ShoulderScaleSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            WaistScaleSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            HipScaleSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            // 新增：素材列表与导入模型变形
            if (FindName("CbAssets") is ComboBox cbAssets)
            {
                cbAssets.DisplayMemberPath = "Name";
                cbAssets.SelectedValuePath = "Path";
                cbAssets.SelectionChanged += (_, __) =>
                {
                    if (cbAssets.SelectedValue is string path && !string.IsNullOrWhiteSpace(path))
                    {
                        LoadModelFromPath(path);
                    }
                    else if (cbAssets.SelectedItem is AssetItem item)
                    {
                        LoadModelFromPath(item.Path);
                    }
                };
            }
            if (FindName("BtnRefreshAssets") is Button btnRefreshAssets)
            {
                btnRefreshAssets.Click += (_, __) => LoadAssetsList();
            }
            if (FindName("ImportedNoseScale") is Slider sNose)
            {
                sNose.ValueChanged += (_, __) => ApplyImportedDeform();
            }
            if (FindName("ImportedEarScale") is Slider sEar)
            {
                sEar.ValueChanged += (_, __) => ApplyImportedDeform();
            }

            // 初始化素材列表
            LoadAssetsList();
            BtnSuitTexture.Click += (_, __) => SelectSuitTexture();
            // 新增：更精细化参数控件事件
            NoseSizeSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            MouthWidthSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            EarSizeSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            FingerLengthSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            FingerThicknessSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            FootLengthSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            FootWidthSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            // 姿态与关节：接入下拉与角度滑块
            CbJoint.SelectionChanged += (_, __) => UpdateCurrentJoint();
            JointAngleSlider.ValueChanged += (_, __) =>
            {
                if (_avatarInstance != null) ApplyJointAngle();
                else ApplyImportedJointAngle();
            };
            ChkEyelids.Checked += (_, __) => RebuildAvatarIfAny();
            ChkEyelids.Unchecked += (_, __) => RebuildAvatarIfAny();
            EyelidSizeSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            ChkToes.Checked += (_, __) => RebuildAvatarIfAny();
            ChkToes.Unchecked += (_, __) => RebuildAvatarIfAny();
            ToeLengthSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            ToeThicknessSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            EyeSeparationSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            IrisRadiusSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            PupilRadiusSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            BrowThicknessSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            BrowLengthSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            LipThicknessSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            EarAngleSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            ChinSizeSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            HeadWidthScaleSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            HeadHeightScaleSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
            HeadDepthScaleSlider.ValueChanged += (_, __) => RebuildAvatarIfAny();
        }


        private void ResetView()
        {
            ScaleSlider.Value = 1;
            YawSlider.Value = 0;
            PitchSlider.Value = 0;
            RollSlider.Value = 0;
            LightIntensity.Value = 1.0;
            AmbientIntensity.Value = 0.6;
            FillIntensity.Value = 0.8;
            CbRenderPreset.SelectedIndex = 0;
            CbBackground.SelectedIndex = 1; // 黑色
            CenterCameraToModel();
        }

        private void CenterCameraToModel()
        {
            try
            {
                // 简单包围盒居中与缩放视距
                Rect3D bounds = _currentModel.Bounds;
                var center = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
                double size = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
                _camTarget = center;
                _camDistance = size > 0 ? size * 2.5 : 6;
                _camPitchDeg = -10; _camYawDeg = 0;
                UpdateCamera();
            }
            catch { }
        }

        // 更新相机位置与朝向（围绕 _camTarget 的轨道）
        private void UpdateCamera()
        {
            var yaw = _camYawDeg * Math.PI / 180.0;
            var pitch = Math.Max(-89.0, Math.Min(89.0, _camPitchDeg)) * Math.PI / 180.0;
            var dir = new System.Windows.Media.Media3D.Vector3D(
                Math.Sin(yaw) * Math.Cos(pitch),
                Math.Sin(pitch),
                Math.Cos(yaw) * Math.Cos(pitch));
            var pos = new Point3D(
                _camTarget.X - dir.X * _camDistance,
                _camTarget.Y - dir.Y * _camDistance,
                _camTarget.Z - dir.Z * _camDistance);
            if (Viewport.Camera is PerspectiveCamera cam)
            {
                cam.Position = pos;
                cam.LookDirection = new System.Windows.Media.Media3D.Vector3D(dir.X * _camDistance, dir.Y * _camDistance, dir.Z * _camDistance);
                cam.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
            }
        }

        private void Viewport_MouseDown(object? sender, MouseButtonEventArgs e)
        {
            _lastMouse = e.GetPosition(Viewport);
            if (e.ChangedButton == MouseButton.Left)
            {
                // 若点击到模型表面，则将该点设为新的旋转中心
                var ht = VisualTreeHelper.HitTest(Viewport, _lastMouse);
                if (ht is RayMeshGeometry3DHitTestResult meshHit)
                {
                    _camTarget = meshHit.PointHit;
                }
                _isRotating = true;
                Mouse.Capture(Viewport);
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                _isRotating = true;
                Mouse.Capture(Viewport);
            }
            else if (e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = true;
                Mouse.Capture(Viewport);
            }
        }

        private void Viewport_MouseMove(object? sender, MouseEventArgs e)
        {
            var p = e.GetPosition(Viewport);
            var dx = p.X - _lastMouse.X;
            var dy = p.Y - _lastMouse.Y;
            if (_isRotating)
            {
                _camYawDeg += dx * 0.3;
                _camPitchDeg -= dy * 0.3;
                UpdateCamera();
            }
            else if (_isPanning)
            {
                if (Viewport.Camera is PerspectiveCamera cam)
                {
                    var f = cam.LookDirection; f.Normalize();
                    var up = cam.UpDirection; up.Normalize();
                    var right = System.Windows.Media.Media3D.Vector3D.CrossProduct(f, up); right.Normalize();
                    double panSpeed = _camDistance * 0.002;
                    _camTarget = new Point3D(
                        _camTarget.X - right.X * dx * panSpeed + up.X * dy * panSpeed,
                        _camTarget.Y - right.Y * dx * panSpeed + up.Y * dy * panSpeed,
                        _camTarget.Z - right.Z * dx * panSpeed + up.Z * dy * panSpeed);
                    UpdateCamera();
                }
            }
            _lastMouse = p;
        }

        private void Viewport_MouseUp(object? sender, MouseButtonEventArgs e)
        {
            _isRotating = false; _isPanning = false;
            Mouse.Capture(null);
        }

        private void Viewport_MouseWheel(object? sender, MouseWheelEventArgs e)
        {
            var p = e.GetPosition(Viewport);
            var ht = VisualTreeHelper.HitTest(Viewport, p);
            if (ht is RayMeshGeometry3DHitTestResult meshHit)
            {
                _camTarget = meshHit.PointHit;
            }
            double factor = Math.Pow(1.1, e.Delta / 120.0);
            _camDistance = Math.Max(0.05, _camDistance / factor);
            UpdateCamera();
        }

        private void ApplyBackground()
        {
            var text = (CbBackground.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "深灰";
            Color c = text switch
            {
                "黑色" => Color.FromRgb(10, 10, 10),
                "白色" => Color.FromRgb(240, 240, 240),
                "蓝色" => Color.FromRgb(20, 30, 60),
                "绿色" => Color.FromRgb(20, 60, 30),
                _ => Color.FromRgb(32,32,32)
            };
            ViewportContainer.Background = new SolidColorBrush(c);
        }

        private void ApplyLightIntensity()
        {
            double s = LightIntensity.Value;
            byte v = (byte)Math.Max(0, Math.Min(255, (int)(255 * s / 1.5)));
            if (Sun != null) Sun.Color = Color.FromRgb(v, v, v);
        }
        private void ApplyAmbientIntensity()
        {
            double a = AmbientIntensity.Value;
            byte v = (byte)Math.Max(0, Math.Min(255, (int)(255 * a / 1.5)));
            if (Ambient != null) Ambient.Color = Color.FromRgb(v, v, v);
        }
        private void ApplyFillIntensity()
        {
            double f = FillIntensity.Value;
            byte v = (byte)Math.Max(0, Math.Min(255, (int)(255 * f / 1.5)));
            if (FillLight != null) FillLight.Color = Color.FromRgb(v, v, v);
        }
        private void ApplyRenderPreset()
        {
            var preset = (CbRenderPreset.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "默认";
            switch (preset)
            {
                case "摄影棚":
                    CbBackground.SelectedIndex = 0; // 深灰
                    LightIntensity.Value = 1.2;
                    AmbientIntensity.Value = 0.6;
                    FillIntensity.Value = 1.1;
                    break;
                case "户外晴天":
                    CbBackground.SelectedIndex = 3; // 蓝色
                    LightIntensity.Value = 1.4;
                    AmbientIntensity.Value = 0.8;
                    FillIntensity.Value = 0.9;
                    break;
                case "高对比":
                    CbBackground.SelectedIndex = 1; // 黑色
                    LightIntensity.Value = 1.6;
                    AmbientIntensity.Value = 0.3;
                    FillIntensity.Value = 0.8;
                    break;
                case "柔光":
                    CbBackground.SelectedIndex = 0; // 深灰
                    LightIntensity.Value = 0.8;
                    AmbientIntensity.Value = 1.2;
                    FillIntensity.Value = 1.0;
                    break;
                default:
                    RestoreDefaultRender();
                    break;
            }
        }
        private void RestoreDefaultRender()
        {
            CbBackground.SelectedIndex = 0; // 深灰
            LightIntensity.Value = 1.0;
            AmbientIntensity.Value = 0.6;
            FillIntensity.Value = 0.8;
        }
        private void ApplyScale()
        {
            if (_modelTransform.Children[0] is ScaleTransform3D st)
            {
                st.ScaleX = st.ScaleY = st.ScaleZ = ScaleSlider.Value;
            }
        }

        private void ApplyRotation()
        {
            if (_modelTransform.Children[1] is RotateTransform3D yaw && yaw.Rotation is AxisAngleRotation3D rYaw) rYaw.Angle = YawSlider.Value;
            if (_modelTransform.Children[2] is RotateTransform3D pitch && pitch.Rotation is AxisAngleRotation3D rPitch) rPitch.Angle = PitchSlider.Value;
            if (_modelTransform.Children[3] is RotateTransform3D roll && roll.Rotation is AxisAngleRotation3D rRoll) rRoll.Angle = RollSlider.Value;
        }

        // Avatar: 创建与材质更新
        private void CreateNewAvatar()
        {
            try
            {
                _avatarInstance = null;
                var root = _defaultModelsDir;
                if (!Directory.Exists(root))
                {
                    MessageBox.Show($"未找到模型目录：{root}", "导入错误");
                    return;
                }

                // 选取一个优先级更高的 FBX（带 rig/anim），否则回退为任意 FBX
                string? fbxPath = Directory.EnumerateFiles(root, "*.fbx", SearchOption.AllDirectories)
                    .OrderByDescending(p => (p.Contains("rig") || p.Contains("animated") || p.Contains("u3d") || p.Contains("ue4")) ? 1 : 0)
                    .FirstOrDefault();
                if (string.IsNullOrEmpty(fbxPath) || !File.Exists(fbxPath))
                {
                    MessageBox.Show("在提供的 3dmouduls 目录中未找到 FBX 文件", "导入错误");
                    return;
                }

                LoadModelFromPath(fbxPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入默认人物失败：{ex.Message}", "导入错误");
            }
        }

        private void RebuildAvatarIfAny()
        {
            if (_avatarInstance == null)
            {
                // 非程序化人物：对导入模型应用可识别的形体调节
                ApplyImportedShapeAdjustments();
                return;
            }
            var p = new AvatarParams
            {
                Height = HeightSlider.Value,
                BodyRadius = BodyRadiusSlider.Value,
                LimbRadius = LimbRadiusSlider.Value,
                SkinColor = GetSelectedSkinColor(),
                ClothColor = GetSelectedClothColor(),
                EyeColor = GetSelectedEyeColor(),
                HairColor = GetSelectedHairColor(),
                Shininess = ShininessSlider.Value,
                ShoulderWidthScale = ShoulderScaleSlider.Value,
                WaistScale = WaistScaleSlider.Value,
                HipScale = HipScaleSlider.Value,
                ForearmLengthScale = ForearmScaleSlider.Value,
                ShinLengthScale = ShinScaleSlider.Value,
                HeadRadiusScale = HeadScaleSlider.Value,
                SuitTexturePath = _suitTexturePath,
                Style = GetSelectedClothingStyle(),
                // 绑定新增参数
                NoseSize = NoseSizeSlider.Value,
                MouthWidth = MouthWidthSlider.Value,
                EarSize = EarSizeSlider.Value,
                FingerLengthScale = FingerLengthSlider.Value,
                FingerThicknessScale = FingerThicknessSlider.Value,
                FootLengthScale = FootLengthSlider.Value,
                FootWidthScale = FootWidthSlider.Value,
                GenerateEyelids = ChkEyelids.IsChecked == true,
                EyelidSizeScale = EyelidSizeSlider.Value,
                GenerateToes = ChkToes.IsChecked == true,
                ToeLengthScale = ToeLengthSlider.Value,
                ToeThicknessScale = ToeThicknessSlider.Value,
                EyeSeparationScale = EyeSeparationSlider.Value,
                IrisRadiusScale = IrisRadiusSlider.Value,
                PupilRadiusScale = PupilRadiusSlider.Value,
                BrowThicknessScale = BrowThicknessSlider.Value,
                BrowLengthScale = BrowLengthSlider.Value,
                LipThicknessScale = LipThicknessSlider.Value,
                EarAngleDeg = EarAngleSlider.Value,
                ChinSizeScale = ChinSizeSlider.Value,
                HeadWidthScale = HeadWidthScaleSlider.Value,
                HeadHeightScale = HeadHeightScaleSlider.Value,
                HeadDepthScale = HeadDepthScaleSlider.Value,
            };
            _avatarInstance = AvatarBuilder.Build(p);
            _currentModel.Children.Clear();
            _currentModel.Children.Add(_avatarInstance.Model);
            ApplyAvatarMaterials();
            SetProceduralControlsEnabled(true);
        }

        private void ApplyAvatarMaterials()
        {
            if (_avatarInstance == null) return;
            var skin = GetSelectedSkinColor();
            var cloth = GetSelectedClothColor();
            ((SolidColorBrush)_avatarInstance.SkinDiffuse.Brush).Color = skin;
            // 纹理优先：如果选择了纹理，ClothDiffuse可能是ImageBrush，不覆盖；否则用颜色
            if (_avatarInstance.ClothDiffuse.Brush is SolidColorBrush sb)
            {
                sb.Color = cloth;
            }
            _avatarInstance.Specular.SpecularPower = Math.Max(2.0, ShininessSlider.Value);
        }

        private Color GetSelectedSkinColor()
        {
            var text = (CbSkin.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "自然";
            return text switch
            {
                "浅肤" => Color.FromRgb(245, 222, 200),
                "自然" => Color.FromRgb(233, 210, 190),
                "中肤" => Color.FromRgb(200, 170, 150),
                "深肤" => Color.FromRgb(150, 110, 90),
                _ => Color.FromRgb(233, 210, 190)
            };
        }

        private Color GetSelectedClothColor()
        {
            var text = (CbCloth.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "黑";
            return text switch
            {
                "白" => Color.FromRgb(240,240,240),
                "蓝" => Color.FromRgb(70,70,160),
                "红" => Color.FromRgb(160,60,60),
                _ => Color.FromRgb(40,40,45)
            };
        }

        private ClothingStyle GetSelectedClothingStyle()
        {
            var txt = (CbClothingStyle.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "西装";
            return txt switch
            {
                "西装" => ClothingStyle.Suit,
                "T恤" => ClothingStyle.TShirt,
                "夹克" => ClothingStyle.Jacket,
                "裙装" => ClothingStyle.Dress,
                "无" => ClothingStyle.None,
                _ => ClothingStyle.Suit
            };
        }
        private Color GetSelectedEyeColor()
        {
            var text = (CbEye.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "黑";
            return text switch
            {
                "黑" => Color.FromRgb(40, 40, 40),
                "棕" => Color.FromRgb(90, 70, 50),
                "蓝" => Color.FromRgb(60, 100, 200),
                "绿" => Color.FromRgb(70, 140, 90),
                _ => Color.FromRgb(40, 40, 40)
            };
        }

        private Color GetSelectedHairColor()
        {
            var text = (CbHair.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "黑";
            return text switch
            {
                "黑" => Color.FromRgb(30, 30, 30),
                "棕" => Color.FromRgb(100, 70, 40),
                "金" => Color.FromRgb(200, 180, 90),
                "灰" => Color.FromRgb(140, 140, 140),
                _ => Color.FromRgb(30, 30, 30)
            };
        }

        private void SelectSuitTexture()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                _suitTexturePath = ofd.FileName;
                // 导入模型：将所选布料贴图批量应用到衣物分类
                if (_avatarInstance == null)
                {
                    ReplaceTexturesForCategory(PartCategory.Cloth, _suitTexturePath);
                }
                RebuildAvatarIfAny();
            }
        }

        private void UpdateCurrentJoint()
        {
            var text = (CbJoint.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "左上臂";
            _currentJoint = text switch
            {
                "左上臂" => JointName.LeftUpperArm,
                "左前臂" => JointName.LeftLowerArm,
                "右上臂" => JointName.RightUpperArm,
                "右前臂" => JointName.RightLowerArm,
                "左大腿" => JointName.LeftUpperLeg,
                "左小腿" => JointName.LeftLowerLeg,
                "右大腿" => JointName.RightUpperLeg,
                "右小腿" => JointName.RightLowerLeg,
                "头部" => JointName.Head,
                "胸部" => JointName.Chest,
                "脊柱" => JointName.Spine,
                "臀部" => JointName.Hips,
                _ => JointName.LeftUpperArm
            };
            JointAngleSlider.Value = 0;
        }

        private void ApplyJointAngle()
        {
            if (_avatarInstance == null) return;
            if (_avatarInstance.JointRotations.TryGetValue(_currentJoint, out var rot))
            {
                rot.Angle = JointAngleSlider.Value;
            }
        }

        private void ExportCurrentModel()
        {
            if (_currentModel == null || _currentModel.Children.Count == 0)
            {
                MessageBox.Show("没有可导出的模型。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var sfd = new SaveFileDialog
            {
                Filter = "Wavefront OBJ|*.obj",
                FileName = "avatar.obj"
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    // 若存在导入映射，则使用规范化名称导出，实现自动重编码
                    if (_importedMeshMap != null && _importedMeshMap.Count > 0)
                        ObjExporter.Export(sfd.FileName, _currentModel, _importedMeshMap);
                    else
                        ObjExporter.Export(sfd.FileName, _currentModel);

                    // 同步输出 parts.json：规范化名称、原始别名、分类
                    if (_importedMeshMap != null && _importedMeshMap.Count > 0)
                    {
                        ExportPartsJson(sfd.FileName);
                    }

                    MessageBox.Show("导出完成：" + sfd.FileName, "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("导出失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateSkeletonVisibility(bool visible)
        {
            if (visible)
            {
                foreach (var v in _skeletonVisuals)
                {
                    if (!SkeletonRoot.Children.Contains(v)) SkeletonRoot.Children.Add(v);
                }
            }
            else
            {
                foreach (var v in _skeletonVisuals)
                {
                    SkeletonRoot.Children.Remove(v);
                }
            }
        }

        private ModelVisual3D CreateSphereVisual(Point3D center, double radius, Color color)
        {
            var geo = new MeshGeometry3D();
            const int t = 20;
            for (int i = 0; i <= t; i++)
            {
                double phi = Math.PI * i / t;
                for (int j = 0; j <= t; j++)
                {
                    double theta = 2 * Math.PI * j / t;
                    double x = center.X + radius * Math.Sin(phi) * Math.Cos(theta);
                    double y = center.Y + radius * Math.Cos(phi);
                    double z = center.Z + radius * Math.Sin(phi) * Math.Sin(theta);
                    geo.Positions.Add(new Point3D(x, y, z));
                    var nx = Math.Sin(phi) * Math.Cos(theta);
                    var ny = Math.Cos(phi);
                    var nz = Math.Sin(phi) * Math.Sin(theta);
                    geo.Normals.Add(new System.Windows.Media.Media3D.Vector3D(nx, ny, nz));
                }
            }
            for (int i = 0; i < t; i++)
            {
                for (int j = 0; j < t; j++)
                {
                    int a = i * (t + 1) + j;
                    int b = a + t + 1;
                    int c = a + 1;
                    int d = b + 1;
                    geo.TriangleIndices.Add(a);
                    geo.TriangleIndices.Add(b);
                    geo.TriangleIndices.Add(c);
                    geo.TriangleIndices.Add(c);
                    geo.TriangleIndices.Add(b);
                    geo.TriangleIndices.Add(d);
                }
            }
            var mat = new DiffuseMaterial(new SolidColorBrush(color));
            var model = new GeometryModel3D(geo, mat) { BackMaterial = mat };
            var mv = new ModelVisual3D { Content = model };
            return mv;
        }

        private void OpenModel()
        {
            try
            {
                _avatarInstance = null; // 打开外部模型时清空当前 Avatar
                var dlg = new OpenFileDialog
                {
                    Filter = "3D 模型|*.fbx;*.obj;*.dae;*.gltf;*.glb;*.ply;*.stl|所有文件|*.*"
                };
                if (dlg.ShowDialog() != true) return;

                var scene = _assimp.ImportFile(
                    dlg.FileName,
                    PostProcessSteps.Triangulate |
                    PostProcessSteps.CalculateTangentSpace |
                    PostProcessSteps.GenerateSmoothNormals |
                    PostProcessSteps.JoinIdenticalVertices |
                    PostProcessSteps.FlipWindingOrder);

                _currentModel.Children.Clear();
                var baseDir = System.IO.Path.GetDirectoryName(dlg.FileName) ?? _defaultModelsDir;
                var texDir = System.IO.Path.Combine(baseDir, "tex");
                var textureBaseDir = System.IO.Directory.Exists(texDir) ? texDir : baseDir;
                // 使用带网格映射输出的转换方法
                var model = AssimpToHelixConverter.ToModel3DGroup(scene, textureBaseDir, out _importedMeshMap);
                foreach (var child in model.Children) _currentModel.Children.Add(child);

                BuildSkeleton(scene);
                Title = $"3D模型预览 - {System.IO.Path.GetFileName(dlg.FileName)}";
                CenterCameraToModel();
                // 刷新导入部位并应用初始变形；再调整可用控件
                RefreshDeformTargets();
                ApplyImportedDeform();
                SetControlsForImportedModel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入模型失败：{ex.Message}", "导入错误");
            }
        }

        private void BuildSkeleton(Scene scene)
        {
            try
            {
                foreach (var v in _skeletonVisuals) SkeletonRoot.Children.Remove(v);
                _skeletonVisuals.Clear();
                _detectedJoints.Clear();
                var boneNames = AssimpToHelixConverter.CollectBoneNames(scene);
                var nodePositions = AssimpToHelixConverter.CollectNodePositions(scene);
                // 关键骨骼别名表（Mixamo/RP 等 + 规范化标签）
                var alias = new Dictionary<JointName, string[]>
                {
                    { JointName.Head, new[]{ "head", "mixamorig:Head" } },
                    { JointName.Neck, new[]{ "neck", "mixamorig:Neck" } },
                    { JointName.Chest, new[]{ "chest", "upperchest", "mixamorig:Spine2" } },
                    { JointName.Spine, new[]{ "spine", "mixamorig:Spine", "mixamorig:Spine1" } },
                    { JointName.Hips, new[]{ "hips", "pelvis", "mixamorig:Hips", "waist" } },
                    { JointName.LeftUpperArm, new[]{ "leftupperarm", "leftarm", "leftshoulder", "mixamorig:LeftArm", "mixamorig:LeftShoulder" } },
                    { JointName.LeftLowerArm, new[]{ "leftlowerarm", "leftforearm", "mixamorig:LeftForeArm" } },
                    { JointName.RightUpperArm, new[]{ "rightupperarm", "rightarm", "rightshoulder", "mixamorig:RightArm", "mixamorig:RightShoulder" } },
                    { JointName.RightLowerArm, new[]{ "rightlowerarm", "rightforearm", "mixamorig:RightForeArm" } },
                    { JointName.LeftUpperLeg, new[]{ "leftupperleg", "leftupleg", "mixamorig:LeftUpLeg", "leftthigh" } },
                    { JointName.LeftLowerLeg, new[]{ "leftlowerleg", "leftleg", "mixamorig:LeftLeg", "leftshin" } },
                    { JointName.RightUpperLeg, new[]{ "rightupperleg", "rightupleg", "mixamorig:RightUpLeg", "rightthigh" } },
                    { JointName.RightLowerLeg, new[]{ "rightlowerleg", "rightleg", "mixamorig:RightLeg", "rightshin" } },
                };
                foreach (var kv in alias)
                {
                    var hit = nodePositions.FirstOrDefault(p => kv.Value.Any(a => p.Key.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0));
                    if (!string.IsNullOrEmpty(hit.Key)) _detectedJoints[kv.Key] = hit.Value;
                }
                // Fallback：若别名匹配失败，至少显示骨骼节点位置
                var points = _detectedJoints.Values.ToList();
                if (points.Count == 0)
                {
                    foreach (var bn in boneNames.Take(64))
                    {
                        if (nodePositions.TryGetValue(bn, out var p)) points.Add(p);
                    }
                }
                // 绘制参考点
                foreach (var p in points)
                {
                    var sphere = CreateSphereVisual(p, 0.02, Colors.Orange);
                    _skeletonVisuals.Add(sphere);
                    SkeletonRoot.Children.Add(sphere);
                }
                if (_skeletonVisuals.Count > 0 && ToggleSkeleton.IsChecked != true) ToggleSkeleton.IsChecked = true;
                UpdateSkeletonVisibility(ToggleSkeleton.IsChecked == true);
            }
            catch { }
        }

        private void UpdateSceneExtras()
        {
            _sceneExtras.Children.Clear();

            if (ToggleGround.IsChecked == true)
            {
                if (_groundModel == null) _groundModel = BuildGroundPlane(20.0, 20.0, 1.0);
                _sceneExtras.Children.Add(_groundModel);
            }

            if (ToggleBackdrop.IsChecked == true)
            {
                if (_backdropModel == null) _backdropModel = BuildBackdrop(24.0, 14.0, -8.0);
                _sceneExtras.Children.Add(_backdropModel);
            }
        }

        private GeometryModel3D BuildGroundPlane(double width, double depth, double gridStep)
        {
            var mesh = new MeshGeometry3D();
            double y = 0.0;
            double halfW = width / 2.0;
            double halfD = depth / 2.0;

            mesh.Positions.Add(new Point3D(-halfW, y, -halfD));
            mesh.Positions.Add(new Point3D(halfW, y, -halfD));
            mesh.Positions.Add(new Point3D(halfW, y, halfD));
            mesh.Positions.Add(new Point3D(-halfW, y, halfD));

            mesh.TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 };

            var gridBrush = CreateGridBrush(Colors.DarkGray, Colors.Gray, Colors.Transparent, gridStep);
            var mat = new DiffuseMaterial(gridBrush);
            var spec = new SpecularMaterial(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 8.0);

            var group = new MaterialGroup();
            group.Children.Add(mat);
            group.Children.Add(spec);

            return new GeometryModel3D(mesh, group);
        }

        private Brush CreateGridBrush(Color lineColorA, Color lineColorB, Color background, double gridStep)
        {
            var drawingGroup = new DrawingGroup();
            var bg = new GeometryDrawing(new SolidColorBrush(background), null, new RectangleGeometry(new Rect(0, 0, 1, 1)));
            drawingGroup.Children.Add(bg);

            var penA = new Pen(new SolidColorBrush(lineColorA), 0.002);
            var penB = new Pen(new SolidColorBrush(lineColorB), 0.0015);

            for (double x = 0; x <= 1.0; x += gridStep / 20.0)
            {
                drawingGroup.Children.Add(new GeometryDrawing(null, penA, new LineGeometry(new Point(x, 0), new Point(x, 1))));
                drawingGroup.Children.Add(new GeometryDrawing(null, penB, new LineGeometry(new Point(x + gridStep / 40.0, 0), new Point(x + gridStep / 40.0, 1))));
            }
            for (double y = 0; y <= 1.0; y += gridStep / 20.0)
            {
                drawingGroup.Children.Add(new GeometryDrawing(null, penA, new LineGeometry(new Point(0, y), new Point(1, y))));
                drawingGroup.Children.Add(new GeometryDrawing(null, penB, new LineGeometry(new Point(0, y + gridStep / 40.0), new Point(1, y + gridStep / 40.0))));
            }

            var brush = new DrawingBrush(drawingGroup)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 0.1, 0.1),
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Stretch = Stretch.Fill
            };
            return brush;
        }

        private GeometryModel3D BuildBackdrop(double width, double height, double z)
        {
            var mesh = new MeshGeometry3D();
            double halfW = width / 2.0;
            double halfH = height / 2.0;

            mesh.Positions.Add(new Point3D(-halfW, -0.2, z));
            mesh.Positions.Add(new Point3D(halfW, -0.2, z));
            mesh.Positions.Add(new Point3D(halfW, 2 * halfH - 0.2, z));
            mesh.Positions.Add(new Point3D(-halfW, 2 * halfH - 0.2, z));

            mesh.TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 };

            var bgBrush = new LinearGradientBrush(Colors.Black, Color.FromRgb(30, 30, 30), new Point(0, 0), new Point(0, 1));
            var mat = new DiffuseMaterial(bgBrush);
            var spec = new SpecularMaterial(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 6.0);
            var group = new MaterialGroup();
            group.Children.Add(mat);
            group.Children.Add(spec);

            return new GeometryModel3D(mesh, group);
        }
    
        // ==== 导入模型与变形 =====
        private void LoadModelFromPath(string fbxPath)
        {
            var scene = _assimp.ImportFile(
                fbxPath,
                PostProcessSteps.Triangulate |
                PostProcessSteps.CalculateTangentSpace |
                PostProcessSteps.GenerateSmoothNormals |
                PostProcessSteps.JoinIdenticalVertices |
                PostProcessSteps.FlipWindingOrder);

            _currentModel.Children.Clear();
            var baseDir = System.IO.Path.GetDirectoryName(fbxPath) ?? _defaultModelsDir;
            var textureBaseDir = ResolveTextureBaseDir(baseDir);
            var model = AssimpToHelixConverter.ToModel3DGroup(scene, textureBaseDir, out _importedMeshMap);
            foreach (var child in model.Children) _currentModel.Children.Add(child);

            BuildSkeleton(scene);
            Title = $"3D模型预览 - {System.IO.Path.GetFileName(fbxPath)}";
            CenterCameraToModel();
            RefreshDeformTargets();
            ApplyImportedDeform();
            // 导入模型：启用可识别部位的控件
            SetControlsForImportedModel();
        }

        private string ResolveTextureBaseDir(string baseDir)
        {
            foreach (var folder in new[] { "tex", "textures", "Textures", "materials", "Materials" })
            {
                var d = System.IO.Path.Combine(baseDir, folder);
                if (System.IO.Directory.Exists(d)) return d;
            }
            return baseDir;
        }

        private void RefreshDeformTargets()
        {
            _noseModels = _importedMeshMap.Where(kv => kv.Key.IndexOf("nose", StringComparison.OrdinalIgnoreCase) >= 0)
                                           .Select(kv => kv.Value).ToList();
            _earModels = _importedMeshMap.Where(kv => kv.Key.IndexOf("ear", StringComparison.OrdinalIgnoreCase) >= 0)
                                          .Select(kv => kv.Value).ToList();
            // 新增：更多部位匹配
            _headModels = _importedMeshMap.Where(kv => kv.Key.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0 || kv.Key.IndexOf("skull", StringComparison.OrdinalIgnoreCase) >= 0)
                                           .Select(kv => kv.Value).ToList();
            _shoulderModels = _importedMeshMap.Where(kv => kv.Key.IndexOf("shoulder", StringComparison.OrdinalIgnoreCase) >= 0 || kv.Key.IndexOf("upperarm", StringComparison.OrdinalIgnoreCase) >= 0)
                                              .Select(kv => kv.Value).ToList();
            _waistModels = _importedMeshMap.Where(kv => kv.Key.IndexOf("waist", StringComparison.OrdinalIgnoreCase) >= 0 || kv.Key.IndexOf("abdomen", StringComparison.OrdinalIgnoreCase) >= 0 || kv.Key.IndexOf("stomach", StringComparison.OrdinalIgnoreCase) >= 0 || kv.Key.IndexOf("belly", StringComparison.OrdinalIgnoreCase) >= 0)
                                           .Select(kv => kv.Value).ToList();
            _hipModels = _importedMeshMap.Where(kv => kv.Key.IndexOf("hip", StringComparison.OrdinalIgnoreCase) >= 0 || kv.Key.IndexOf("pelvis", StringComparison.OrdinalIgnoreCase) >= 0)
                                         .Select(kv => kv.Value).ToList();
            _forearmModels = _importedMeshMap.Where(kv => kv.Key.IndexOf("forearm", StringComparison.OrdinalIgnoreCase) >= 0 || kv.Key.IndexOf("lowerarm", StringComparison.OrdinalIgnoreCase) >= 0)
                                             .Select(kv => kv.Value).ToList();
            _shinModels = _importedMeshMap.Where(kv => kv.Key.IndexOf("shin", StringComparison.OrdinalIgnoreCase) >= 0 || kv.Key.IndexOf("calf", StringComparison.OrdinalIgnoreCase) >= 0 || kv.Key.IndexOf("lowerleg", StringComparison.OrdinalIgnoreCase) >= 0)
                                          .Select(kv => kv.Value).ToList();

            // 外观材质分类（衣服/皮肤/头发/眼睛）
            CategorizeAppearanceTargets();
            AnalyzeUnrecognizedPartsAndSuggest();
        }

        private void ApplyImportedDeform()
        {
            if (FindName("ImportedNoseScale") is Slider sNose)
            {
                ApplyScaleToModels(_noseModels, sNose.Value);
            }
            if (FindName("ImportedEarScale") is Slider sEar)
            {
                ApplyScaleToModels(_earModels, sEar.Value);
            }
        }

        // 新增：导入模型的形体调节（依据关键字匹配到的部位）
        private void ApplyImportedShapeAdjustments()
        {
            // 头部：可单独宽/高/深缩放
            ApplyAxisScaleToModels(_headModels, HeadWidthScaleSlider.Value, HeadHeightScaleSlider.Value, HeadDepthScaleSlider.Value);
            // 肩宽、腰围、臀部：近似用 X 方向缩放（若无法定位，忽略）
            ApplyAxisScaleToModels(_shoulderModels, ShoulderScaleSlider.Value, 1.0, 1.0);
            ApplyAxisScaleToModels(_waistModels, WaistScaleSlider.Value, 1.0, 1.0);
            ApplyAxisScaleToModels(_hipModels, HipScaleSlider.Value, 1.0, 1.0);
            // 前臂/小腿长度：近似用 Y 方向缩放（模型坐标系不同可能效果有限）
            ApplyAxisScaleToModels(_forearmModels, 1.0, ForearmScaleSlider.Value, 1.0);
            ApplyAxisScaleToModels(_shinModels, 1.0, ShinScaleSlider.Value, 1.0);
            // 鼻/耳（在“导入模型变形”分组里）
            ApplyImportedDeform();
            // 根据选择自动重新贴图上色（衣服/皮肤/头发/眼睛）
            ApplyImportedMaterialColors();
        }

        // 新增：轴向缩放（用于头部宽/高/深与其它近似部位）
        private void ApplyAxisScaleToModels(List<GeometryModel3D> models, double sx, double sy, double sz)
        {
            foreach (var m in models)
            {
                var tg = m.Transform as Transform3DGroup;
                if (tg == null)
                {
                    tg = new Transform3DGroup();
                    m.Transform = tg;
                }
                if (tg.Children.Count == 0 || tg.Children[0] is not ScaleTransform3D)
                {
                    var stNew = new ScaleTransform3D(sx, sy, sz);
                    tg.Children.Insert(0, stNew);
                }
                else
                {
                    var st = (ScaleTransform3D)tg.Children[0];
                    st.ScaleX = sx; st.ScaleY = sy; st.ScaleZ = sz;
                }
            }
        }

        private void ApplyScaleToModels(List<GeometryModel3D> models, double s)
        {
            foreach (var m in models)
            {
                var tg = m.Transform as Transform3DGroup;
                if (tg == null)
                {
                    tg = new Transform3DGroup();
                    m.Transform = tg;
                }
                // 将缩放插入到变换组前面，避免后续有旋转/位移时缩放失效
                ScaleTransform3D st = null;
                if (tg.Children.Count == 0 || tg.Children[0] is not ScaleTransform3D)
                {
                    st = new ScaleTransform3D(s, s, s);
                    tg.Children.Insert(0, st);
                }
                else
                {
                    st = (ScaleTransform3D)tg.Children[0];
                    st.ScaleX = st.ScaleY = st.ScaleZ = s;
                }
            }
        }

        private void LoadAssetsList()
        {
            if (FindName("CbAssets") is not ComboBox cb) return;
            var root = _defaultModelsDir;
            var list = new List<AssetItem>();
            try
            {
                if (System.IO.Directory.Exists(root))
                {
                    foreach (var f in System.IO.Directory.EnumerateFiles(root, "*.fbx", SearchOption.AllDirectories))
                    {
                        var name = System.IO.Path.GetFileNameWithoutExtension(f);
                        list.Add(new AssetItem { Name = name, Path = f });
                    }
                }
            }
            catch { }
            cb.ItemsSource = list.OrderBy(i => i.Name).ToList();
            if (cb.Items.Count > 0) cb.SelectedIndex = 0;
        }

        private void SetProceduralControlsEnabled(bool enabled)
        {
            foreach (var name in new[]
            {
                "CbSkin","CbCloth","CbEye","CbHair","ShininessSlider",
                "HeightSlider","BodyRadiusSlider","LimbRadiusSlider","ShoulderScaleSlider","WaistScaleSlider","HipScaleSlider",
                "ForearmScaleSlider","ShinScaleSlider","HeadScaleSlider","CbClothingStyle","BtnSuitTexture",
                "NoseSizeSlider","MouthWidthSlider","EarSizeSlider","FingerLengthSlider","FingerThicknessSlider",
                "FootLengthSlider","FootWidthSlider","ChkEyelids","EyelidSizeSlider","ChkToes","ToeLengthSlider","ToeThicknessSlider",
                "EyeSeparationSlider","IrisRadiusSlider","PupilRadiusSlider","BrowThicknessSlider","BrowLengthSlider","LipThicknessSlider",
                "EarAngleSlider","ChinSizeSlider","HeadWidthScaleSlider","HeadHeightScaleSlider","HeadDepthScaleSlider"
            })
            {
                if (FindName(name) is Control c) c.IsEnabled = enabled;
            }
        }

        // 新增：导入模型控件启用策略（仅启用可识别并可近似调整的项）
        private void SetControlsForImportedModel()
        {
            // 先默认全部禁用（避免与程序化头像的控件混用）
            SetProceduralControlsEnabled(false);

            // 导入模型的形体控件：统一启用（有匹配到部位则生效；未匹配则不产生变形但不阻止操作）
            AlwaysEnable(new[]{
                "HeadScaleSlider","HeadWidthScaleSlider","HeadHeightScaleSlider","HeadDepthScaleSlider","ChinSizeSlider",
                "ShoulderScaleSlider","WaistScaleSlider","HipScaleSlider","ForearmScaleSlider","ShinScaleSlider",
                "ImportedNoseScale","ImportedEarScale"
            });

            // 若匹配到了具体部位，依然保留更精细的启用逻辑（兼容旧行为）
            EnableIfFound(_headModels, new[]{ "HeadScaleSlider","HeadWidthScaleSlider","HeadHeightScaleSlider","HeadDepthScaleSlider","ChinSizeSlider" });
            EnableIfFound(_shoulderModels, new[]{ "ShoulderScaleSlider" });
            EnableIfFound(_waistModels, new[]{ "WaistScaleSlider" });
            EnableIfFound(_hipModels, new[]{ "HipScaleSlider" });
            EnableIfFound(_forearmModels, new[]{ "ForearmScaleSlider" });
            EnableIfFound(_shinModels, new[]{ "ShinScaleSlider" });
        }

        private void AlwaysEnable(string[] names)
        {
            foreach (var name in names)
            {
                if (FindName(name) is Control c) c.IsEnabled = true;
            }
        }

        private void EnableIfFound(List<GeometryModel3D> models, string[] controlNames)
        {
            if (models == null || models.Count == 0) return;
            foreach (var name in controlNames)
            {
                if (FindName(name) is Control c) c.IsEnabled = true;
            }
        }
    }

    internal static class AssimpNodeExtensions
    {
        public static IEnumerable<Node> GetAllNodes(this Node root)
        {
            yield return root;
            foreach (var c in root.Children)
            {
                foreach (var x in GetAllNodes(c)) yield return x;
            }
        }
    }
}
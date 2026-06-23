using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;

namespace VtuberHubStudio.ModelViewer
{
    public partial class ModelViewerWindow : Window
    {
        // 将关节角度应用到导入模型（基于已识别的部位集合进行近似旋转）
        private void ApplyImportedJointAngle()
        {
            if (_importedMeshMap == null || _importedMeshMap.Count == 0) return;
            var angle = JointAngleSlider.Value;
            var targets = GetImportedTargetsForJoint(_currentJoint);
            if (targets == null || targets.Count == 0) return;

            var center = EstimateCenter(targets);
            var axis = GetAxisForJoint(_currentJoint);
            foreach (var gm in targets)
            {
                if (!_importedJointRotations.TryGetValue((gm, _currentJoint), out var rot))
                {
                    rot = new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(axis.X, axis.Y, axis.Z), 0);
                    var rt = new RotateTransform3D(rot)
                    {
                        CenterX = center.X,
                        CenterY = center.Y,
                        CenterZ = center.Z
                    };
                    var grp = gm.Transform as Transform3DGroup;
                    if (grp == null)
                    {
                        grp = new Transform3DGroup();
                        if (gm.Transform != null) grp.Children.Add(gm.Transform);
                        gm.Transform = grp;
                    }
                    grp.Children.Add(rt);
                    _importedJointRotations[(gm, _currentJoint)] = rot;
                }
                rot.Angle = angle;
            }
        }

        private List<GeometryModel3D> GetImportedTargetsForJoint(JointName joint)
        {
            switch (joint)
            {
                case JointName.Head: return _headModels;
                case JointName.Chest: return _waistModels; // 近似用腰腹部集合
                case JointName.Spine: return _waistModels;
                case JointName.Hips: return _hipModels;
                case JointName.LeftUpperArm:
                case JointName.RightUpperArm: return _shoulderModels;
                case JointName.LeftLowerArm:
                case JointName.RightLowerArm: return _forearmModels;
                case JointName.LeftUpperLeg:
                case JointName.RightUpperLeg: return _hipModels; // 近似用髋部集合
                case JointName.LeftLowerLeg:
                case JointName.RightLowerLeg: return _shinModels;
                default: return new List<GeometryModel3D>();
            }
        }

        private Point3D EstimateCenter(List<GeometryModel3D> targets)
        {
            if (targets == null || targets.Count == 0) return new Point3D(0, 0, 0);
            Rect3D bounds = new Rect3D();
            bool first = true;
            foreach (var gm in targets)
            {
                var mesh = gm.Geometry as MeshGeometry3D; if (mesh == null) continue;
                var b = mesh.Bounds;
                if (first) { bounds = b; first = false; }
                else { bounds.Union(b); }
            }
            return new Point3D(bounds.X + bounds.SizeX / 2.0, bounds.Y + bounds.SizeY / 2.0, bounds.Z + bounds.SizeZ / 2.0);
        }

        private System.Windows.Media.Media3D.Vector3D GetAxisForJoint(JointName joint)
        {
            // 简化：按关节选择常见旋转轴（X: 屈伸，Z: 展收）
            switch (joint)
            {
                case JointName.Head: return new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
                case JointName.Chest:
                case JointName.Spine:
                case JointName.Hips: return new System.Windows.Media.Media3D.Vector3D(1, 0, 0);
                case JointName.LeftUpperArm:
                case JointName.RightUpperArm: return new System.Windows.Media.Media3D.Vector3D(0, 0, 1);
                case JointName.LeftLowerArm:
                case JointName.RightLowerArm: return new System.Windows.Media.Media3D.Vector3D(0, 0, 1);
                case JointName.LeftUpperLeg:
                case JointName.RightUpperLeg: return new System.Windows.Media.Media3D.Vector3D(1, 0, 0);
                case JointName.LeftLowerLeg:
                case JointName.RightLowerLeg: return new System.Windows.Media.Media3D.Vector3D(1, 0, 0);
                default: return new System.Windows.Media.Media3D.Vector3D(1, 0, 0);
            }
        }
    }
}
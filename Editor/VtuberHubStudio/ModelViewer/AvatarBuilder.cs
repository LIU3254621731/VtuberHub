using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace VtuberHubStudio.ModelViewer
{
    public enum JointName
    {
        Head,
        Neck,
        Chest,
        Spine,
        Hips,
        LeftUpperArm,
        LeftLowerArm,
        LeftHand,
        RightUpperArm,
        RightLowerArm,
        RightHand,
        LeftUpperLeg,
        LeftLowerLeg,
        LeftFoot,
        RightUpperLeg,
        RightLowerLeg,
        RightFoot
    }

    public enum ClothingStyle
    {
        None,
        Suit,
        TShirt,
        Jacket,
        Dress
    }

    public class AvatarParams
    {
        public double Height { get; set; } = 1.7; // 总高度（米）
        public double BodyRadius { get; set; } = 0.20;
        public double LimbRadius { get; set; } = 0.08;
        public Color SkinColor { get; set; } = Color.FromRgb(233, 210, 190);
        public Color ClothColor { get; set; } = Color.FromRgb(40, 40, 45); // 默认黑色西装
        public Color EyeColor { get; set; } = Color.FromRgb(40, 40, 40);
        public Color HairColor { get; set; } = Color.FromRgb(30, 30, 30);
        public double Shininess { get; set; } = 16.0; // 高光强度
        public double ShoulderWidthScale { get; set; } = 1.0; // 肩宽比例
        public double WaistScale { get; set; } = 1.0; // 腰围比例
        public double HipScale { get; set; } = 1.0; // 臀部比例
        public double ForearmLengthScale { get; set; } = 1.0;
        public double ShinLengthScale { get; set; } = 1.0;
        public double HeadRadiusScale { get; set; } = 1.0;
        public string? SuitTexturePath { get; set; } // 西装纹理（可选）
        // 新增：更精细化参数
        public double NoseSize { get; set; } = 1.0;
        public double MouthWidth { get; set; } = 1.0;
        public double EarSize { get; set; } = 1.0;
        public double FingerLengthScale { get; set; } = 1.0;
        public double FingerThicknessScale { get; set; } = 1.0;
        public double FootLengthScale { get; set; } = 1.0;
        public double FootWidthScale { get; set; } = 1.0;
        public ClothingStyle Style { get; set; } = ClothingStyle.Suit;
        // 面部与头部细化参数
        public Color ScleraColor { get; set; } = Color.FromRgb(240, 240, 240);
        public Color LipColor { get; set; } = Color.FromRgb(220, 120, 120);
        public double EyeRadiusScale { get; set; } = 1.0;
        public double EyeSeparationScale { get; set; } = 1.0;
        public double IrisRadiusScale { get; set; } = 1.0;
        public double PupilRadiusScale { get; set; } = 1.0;
        public double BrowThicknessScale { get; set; } = 1.0;
        public double BrowLengthScale { get; set; } = 1.0;
        public double NoseWidthScale { get; set; } = 1.0;
        public double NoseBridgeLengthScale { get; set; } = 1.0;
        public double NostrilSizeScale { get; set; } = 1.0;
        public double LipThicknessScale { get; set; } = 1.0;
        public double EarAngleDeg { get; set; } = 0.0;
        public double ChinSizeScale { get; set; } = 1.0;
        public double HeadWidthScale { get; set; } = 1.0;
        public double HeadHeightScale { get; set; } = 1.0;
        public double HeadDepthScale { get; set; } = 1.0;
        // 参考脚本选项
        public bool GenerateEyelids { get; set; } = true;
        public bool GenerateToes { get; set; } = true;
        public double EyelidSizeScale { get; set; } = 1.0;
        public double ToeLengthScale { get; set; } = 1.0;
        public double ToeThicknessScale { get; set; } = 1.0;
        public double Detail { get; set; } = 0.8;
    }

    public class AvatarInstance
    {
        public Model3DGroup Model { get; set; } = new Model3DGroup();
        public DiffuseMaterial SkinDiffuse { get; set; } = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(233,210,190)));
        public DiffuseMaterial ClothDiffuse { get; set; } = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(40,40,45)));
        public DiffuseMaterial EyeDiffuse { get; set; } = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(40,40,40))); // 虹膜
        public DiffuseMaterial HairDiffuse { get; set; } = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(30,30,30)));
        public SpecularMaterial Specular { get; set; } = new SpecularMaterial(new SolidColorBrush(Color.FromRgb(255,255,255)), 16.0);
        public DiffuseMaterial ScleraDiffuse { get; set; } = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(240,240,240))); // 巩膜
        public DiffuseMaterial LipDiffuse { get; set; } = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(220,120,120)));
        // 关节旋转句柄（用于交互控制）
        public Dictionary<JointName, AxisAngleRotation3D> JointRotations { get; } = new();
    }

    public static class AvatarBuilder
    {
        public static AvatarInstance Build(AvatarParams p)
        {
            var inst = new AvatarInstance();
            // 更新默认材质到参数颜色或纹理
            ((SolidColorBrush)inst.SkinDiffuse.Brush).Color = p.SkinColor;
            ((SolidColorBrush)inst.EyeDiffuse.Brush).Color = p.EyeColor;
            ((SolidColorBrush)inst.HairDiffuse.Brush).Color = p.HairColor;
            inst.Specular.SpecularPower = p.Shininess;
            ((SolidColorBrush)inst.ScleraDiffuse.Brush).Color = p.ScleraColor;
            ((SolidColorBrush)inst.LipDiffuse.Brush).Color = p.LipColor;

            // 西装纹理优先，其次纯色
            if (!string.IsNullOrEmpty(p.SuitTexturePath) && File.Exists(p.SuitTexturePath))
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(p.SuitTexturePath);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                inst.ClothDiffuse = new DiffuseMaterial(new ImageBrush(img) { Stretch = Stretch.Uniform });
            }
            else
            {
                ((SolidColorBrush)inst.ClothDiffuse.Brush).Color = p.ClothColor;
            }

            var group = new Model3DGroup();

            // 尺寸参数推导
            double height = p.Height;
            double bodyR = p.BodyRadius;
            double limbR = p.LimbRadius;
            double shoulderHalf = bodyR * 1.2 * p.ShoulderWidthScale;
            double chestY = height * 0.65; // 肩部高度
            double pelvisY = height * 0.25; // 髋部高度
            double headCenterY = height * 0.80;
            double headR = bodyR * 0.55 * p.HeadRadiusScale;

            // 躯干（近似）：单胶囊，可根据腰/臀比例调整半径（简单缩放）
            // 躯干分段：胸、腹、骨盆（更精细的体型）
            double chestLen = height * 0.22;
            double abdomenLen = height * 0.18;
            double pelvisLen = height * 0.16;
            double chestR = bodyR * 1.08 * p.WaistScale;
            double abdomenR = bodyR * 0.95 * p.WaistScale;
            double pelvisR = bodyR * 1.05 * p.HipScale;
            var chestCaps = CreateCapsule(new Point3D(0, chestY - chestLen * 0.40, 0), chestR, chestLen, inst.ClothDiffuse, inst.Specular);
            var abdomenCaps = CreateCapsule(new Point3D(0, chestY - chestLen - abdomenLen * 0.5, 0), abdomenR, abdomenLen, inst.ClothDiffuse, inst.Specular);
            var pelvisCaps = CreateCapsule(new Point3D(0, pelvisY + pelvisLen * 0.30, 0), pelvisR, pelvisLen, inst.ClothDiffuse, inst.Specular);
            group.Children.Add(chestCaps);
            group.Children.Add(abdomenCaps);
            group.Children.Add(pelvisCaps);
            // 颈部（皮肤）
            double neckTopY = headCenterY - headR;
            double neckLen = Math.Max(0.06 * height, Math.Abs(neckTopY - chestY) * 0.6);
            var neck = CreateCapsule(new Point3D(0, chestY + neckLen * 0.5, 0), bodyR * 0.35, neckLen, inst.SkinDiffuse, inst.Specular);
            group.Children.Add(neck);
            // 头部（支持缩放，提供头宽/高/深比例）
            var head = CreateSphere(new Point3D(0, headCenterY, 0), headR, inst.SkinDiffuse, inst.Specular);
            var headScale = new ScaleTransform3D(p.HeadWidthScale, p.HeadHeightScale, p.HeadDepthScale);
            var headTg = head.Transform as Transform3DGroup ?? new Transform3DGroup();
            headTg.Children.Add(headScale);
            head.Transform = headTg;
            group.Children.Add(head);

            // 眼睛（更精细：巩膜+虹膜+瞳孔）
            double eyeOffsetX = headR * 0.35 * p.EyeSeparationScale;
            double eyeOffsetY = headR * 0.10;
            double eyeOffsetZ = headR * 0.45;
            double scleraR = headR * 0.13 * p.EyeRadiusScale;
            double irisR = scleraR * 0.55 * p.IrisRadiusScale;
            double pupilR = scleraR * 0.25 * p.PupilRadiusScale;
            Point3D leftEyeCenter = new Point3D(-eyeOffsetX, headCenterY - eyeOffsetY, eyeOffsetZ);
            Point3D rightEyeCenter = new Point3D(eyeOffsetX, headCenterY - eyeOffsetY, eyeOffsetZ);
            group.Children.Add(CreateSphere(leftEyeCenter, scleraR, inst.ScleraDiffuse, inst.Specular));
            group.Children.Add(CreateSphere(rightEyeCenter, scleraR, inst.ScleraDiffuse, inst.Specular));
            group.Children.Add(CreateSphere(new Point3D(leftEyeCenter.X, leftEyeCenter.Y, leftEyeCenter.Z + scleraR*0.15), irisR, inst.EyeDiffuse, inst.Specular));
            group.Children.Add(CreateSphere(new Point3D(rightEyeCenter.X, rightEyeCenter.Y, rightEyeCenter.Z + scleraR*0.15), irisR, inst.EyeDiffuse, inst.Specular));
            var pupilMat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0,0,0)));
            group.Children.Add(CreateSphere(new Point3D(leftEyeCenter.X, leftEyeCenter.Y, leftEyeCenter.Z + scleraR*0.20), pupilR, pupilMat, inst.Specular));
            group.Children.Add(CreateSphere(new Point3D(rightEyeCenter.X, rightEyeCenter.Y, rightEyeCenter.Z + scleraR*0.20), pupilR, pupilMat, inst.Specular));
            // 眉毛（细长胶囊）
            double browLen = headR * 0.50 * p.BrowLengthScale;
            double browR = headR * 0.03 * p.BrowThicknessScale;
            var leftBrow = CreateCapsule(new Point3D(-eyeOffsetX, headCenterY - eyeOffsetY + headR*0.20, eyeOffsetZ - headR*0.02), browR, browLen, inst.HairDiffuse, inst.Specular);
            RotateToX(leftBrow);
            var rightBrow = CreateCapsule(new Point3D(eyeOffsetX, headCenterY - eyeOffsetY + headR*0.20, eyeOffsetZ - headR*0.02), browR, browLen, inst.HairDiffuse, inst.Specular);
            RotateToX(rightBrow);
            group.Children.Add(leftBrow);
            group.Children.Add(rightBrow);
            // 眼睑（参考脚本，可开关）：上/下眼睑用缩放球体近似
            if (p.GenerateEyelids)
            {
                double lidBase = scleraR * p.EyelidSizeScale;
                // 左眼上/下眼睑
                var leftUpper = CreateSphere(new Point3D(leftEyeCenter.X, leftEyeCenter.Y + lidBase * 0.18, leftEyeCenter.Z), lidBase, inst.SkinDiffuse, inst.Specular);
                var leftLower = CreateSphere(new Point3D(leftEyeCenter.X, leftEyeCenter.Y - lidBase * 0.12, leftEyeCenter.Z), lidBase, inst.SkinDiffuse, inst.Specular);
                var lUtg = new Transform3DGroup(); lUtg.Children.Add(new ScaleTransform3D(1.05, 0.45, 1.05)); leftUpper.Transform = lUtg;
                var lLtg = new Transform3DGroup(); lLtg.Children.Add(new ScaleTransform3D(1.05, 0.45, 1.05)); leftLower.Transform = lLtg;
                group.Children.Add(leftUpper); group.Children.Add(leftLower);
                // 右眼上/下眼睑
                var rightUpper = CreateSphere(new Point3D(rightEyeCenter.X, rightEyeCenter.Y + lidBase * 0.18, rightEyeCenter.Z), lidBase, inst.SkinDiffuse, inst.Specular);
                var rightLower = CreateSphere(new Point3D(rightEyeCenter.X, rightEyeCenter.Y - lidBase * 0.12, rightEyeCenter.Z), lidBase, inst.SkinDiffuse, inst.Specular);
                var rUtg = new Transform3DGroup(); rUtg.Children.Add(new ScaleTransform3D(1.05, 0.45, 1.05)); rightUpper.Transform = rUtg;
                var rLtg = new Transform3DGroup(); rLtg.Children.Add(new ScaleTransform3D(1.05, 0.45, 1.05)); rightLower.Transform = rLtg;
                group.Children.Add(rightUpper); group.Children.Add(rightLower);
            }
             
             // 头发（上半球薄壳）
             var hair = CreateHemisphere(new Point3D(0, headCenterY + headR * 0.02, 0), headR * 1.05, true, inst.HairDiffuse, inst.Specular);
             group.Children.Add(hair);
             // 锁骨连接（沿 X 方向），让肩部过渡更自然
             var leftClavicle = CreateCapsule(new Point3D(-shoulderHalf * 0.5, chestY + limbR * 0.10, 0), limbR * 0.55, shoulderHalf, inst.ClothDiffuse, inst.Specular);
             RotateToX(leftClavicle);
             var rightClavicle = CreateCapsule(new Point3D(shoulderHalf * 0.5, chestY + limbR * 0.10, 0), limbR * 0.55, shoulderHalf, inst.ClothDiffuse, inst.Specular);
             RotateToX(rightClavicle);
             group.Children.Add(leftClavicle);
             group.Children.Add(rightClavicle);

             // 上臂与前臂
             double upperArmLen = height * 0.28;
             double foreArmLen = upperArmLen * 0.9 * p.ForearmLengthScale;
             // 左右上臂
             var leftUpperArm = CreateCapsule(new Point3D(-shoulderHalf, chestY, 0), limbR, upperArmLen, inst.ClothDiffuse, inst.Specular);
             var rightUpperArm = CreateCapsule(new Point3D(shoulderHalf, chestY, 0), limbR, upperArmLen, inst.ClothDiffuse, inst.Specular);
             group.Children.Add(leftUpperArm);
             group.Children.Add(rightUpperArm);
             // 左右前臂（连接在上臂末端）
             var leftForeArm = CreateCapsule(new Point3D(-shoulderHalf, chestY - upperArmLen, 0), limbR * 0.95, foreArmLen, inst.ClothDiffuse, inst.Specular);
             var rightForeArm = CreateCapsule(new Point3D(shoulderHalf, chestY - upperArmLen, 0), limbR * 0.95, foreArmLen, inst.ClothDiffuse, inst.Specular);
             group.Children.Add(leftForeArm);
             group.Children.Add(rightForeArm);

             // 手（简化为球体）
             double handR = limbR * 1.1;
             var leftHand = CreateSphere(new Point3D(-shoulderHalf, chestY - upperArmLen - foreArmLen, 0), handR, inst.SkinDiffuse, inst.Specular);
             var rightHand = CreateSphere(new Point3D(shoulderHalf, chestY - upperArmLen - foreArmLen, 0), handR, inst.SkinDiffuse, inst.Specular);
             group.Children.Add(leftHand);
             group.Children.Add(rightHand);

             // 大腿与小腿
             double upperLegLen = height * 0.38;
             double shinLen = upperLegLen * 0.9 * p.ShinLengthScale;
             double hipHalf = bodyR * 0.6 * p.HipScale;
             var leftUpperLeg = CreateCapsule(new Point3D(-hipHalf, pelvisY, 0), limbR * 1.05, upperLegLen, inst.ClothDiffuse, inst.Specular);
             var rightUpperLeg = CreateCapsule(new Point3D(hipHalf, pelvisY, 0), limbR * 1.05, upperLegLen, inst.ClothDiffuse, inst.Specular);
             group.Children.Add(leftUpperLeg);
             group.Children.Add(rightUpperLeg);
             var leftLowerLeg = CreateCapsule(new Point3D(-hipHalf, pelvisY - upperLegLen, 0), limbR * 1.0, shinLen, inst.ClothDiffuse, inst.Specular);
             var rightLowerLeg = CreateCapsule(new Point3D(hipHalf, pelvisY - upperLegLen, 0), limbR * 1.0, shinLen, inst.ClothDiffuse, inst.Specular);
             group.Children.Add(leftLowerLeg);
             group.Children.Add(rightLowerLeg);

             // 足（简化为扁球）
             // 脚：用胶囊并旋转到 Z 方向，长度与宽度可调
             double footLen = limbR * 2.6 * p.FootLengthScale;
             double footRad = limbR * 1.1 * p.FootWidthScale;
             var leftFoot = CreateCapsule(new Point3D(-hipHalf, pelvisY - upperLegLen - shinLen, headR * -0.15), footRad, footLen, inst.ClothDiffuse, inst.Specular);
             var rightFoot = CreateCapsule(new Point3D(hipHalf, pelvisY - upperLegLen - shinLen, headR * -0.15), footRad, footLen, inst.ClothDiffuse, inst.Specular);
             RotateToZ(leftFoot);
             RotateToZ(rightFoot);
             group.Children.Add(leftFoot);
             group.Children.Add(rightFoot);
             // 脚趾（参考脚本，可开关）：每脚 5 趾，拇指 2 段，其余 3 段
             if (p.GenerateToes)
             {
                 void BuildFootToes(Point3D footCenter, bool isLeft)
                 {
                     double baseZ = footCenter.Z + footLen * 0.5 + limbR * 0.1;
                     double spread = footRad * 0.8;
                     double segR = footRad * 0.35 * p.ToeThicknessScale;
                     double proxLen = footRad * 0.9 * p.ToeLengthScale;
                     double midLen = footRad * 0.75 * p.ToeLengthScale;
                     double distLen = footRad * 0.65 * p.ToeLengthScale;
                     double side = isLeft ? -1.0 : 1.0;
                     double[] xOffsets = new double[] { side * -0.3 * spread, side * -0.15 * spread, 0, side * 0.15 * spread, side * 0.3 * spread };
                     for (int i = 0; i < 5; i++)
                     {
                         double x = footCenter.X + xOffsets[i];
                         int segments = (i == 0) ? 2 : 3; // 大脚趾较短
                         double curZ = baseZ;
                         for (int s = 0; s < segments; s++)
                         {
                             double len = s == 0 ? proxLen : (s == 1 ? midLen : distLen);
                             var seg = CreateCapsule(new Point3D(x, footCenter.Y, curZ), segR * (1.0 - 0.08 * s), len, inst.SkinDiffuse, inst.Specular);
                             RotateToZ(seg);
                             group.Children.Add(seg);
                             curZ += len * 0.85;
                         }
                     }
                 }
                 BuildFootToes(new Point3D(-hipHalf, pelvisY - upperLegLen - shinLen, headR * -0.15), true);
                 BuildFootToes(new Point3D(hipHalf, pelvisY - upperLegLen - shinLen, headR * -0.15), false);
             }

             // 关节旋转（为关键部件添加可调轴角旋转，中心在上端/连接处）
             // 定义并附加旋转到几何的 Transform（这里简化：沿Z轴弯曲）
             void AttachJointRotation(GeometryModel3D gm, JointName name, Point3D pivot)
             {
                 var tg = gm.Transform as Transform3DGroup;
                 if (tg == null)
                 {
                     tg = new Transform3DGroup();
                     gm.Transform = tg;
                 }
                 var rot = new AxisAngleRotation3D(new Vector3D(0,0,1), 0);
                 var rt = new RotateTransform3D(rot) { CenterX = pivot.X, CenterY = pivot.Y, CenterZ = pivot.Z };
                 tg.Children.Add(rt);
                 inst.JointRotations[name] = rot;
             }

             // 上臂的枢轴在上端（centerY + half）
             double uaHalf = upperArmLen * 0.5;
             AttachJointRotation(leftUpperArm, JointName.LeftUpperArm, new Point3D(-shoulderHalf, chestY + uaHalf, 0));
             AttachJointRotation(rightUpperArm, JointName.RightUpperArm, new Point3D(shoulderHalf, chestY + uaHalf, 0));
             // 前臂枢轴在上端（连接到上臂下端）
             double faHalf = foreArmLen * 0.5;
             AttachJointRotation(leftForeArm, JointName.LeftLowerArm, new Point3D(-shoulderHalf, chestY - upperArmLen + faHalf, 0));
             AttachJointRotation(rightForeArm, JointName.RightLowerArm, new Point3D(shoulderHalf, chestY - upperArmLen + faHalf, 0));
             // 手部枢轴在手的上端
             AttachJointRotation(leftHand, JointName.LeftHand, new Point3D(-shoulderHalf, chestY - upperArmLen - foreArmLen + handR, 0));
             AttachJointRotation(rightHand, JointName.RightHand, new Point3D(shoulderHalf, chestY - upperArmLen - foreArmLen + handR, 0));
             // 大腿枢轴在上端
             double ulHalf = upperLegLen * 0.5;
             AttachJointRotation(leftUpperLeg, JointName.LeftUpperLeg, new Point3D(-hipHalf, pelvisY + ulHalf, 0));
             AttachJointRotation(rightUpperLeg, JointName.RightUpperLeg, new Point3D(hipHalf, pelvisY + ulHalf, 0));
             // 小腿枢轴在上端
             double slHalf = shinLen * 0.5;
             AttachJointRotation(leftLowerLeg, JointName.LeftLowerLeg, new Point3D(-hipHalf, pelvisY - upperLegLen + slHalf, 0));
             AttachJointRotation(rightLowerLeg, JointName.RightLowerLeg, new Point3D(hipHalf, pelvisY - upperLegLen + slHalf, 0));
             // 足枢轴在上端
             AttachJointRotation(leftFoot, JointName.LeftFoot, new Point3D(-hipHalf, pelvisY - upperLegLen - shinLen + limbR * 0.6, 0));
             AttachJointRotation(rightFoot, JointName.RightFoot, new Point3D(hipHalf, pelvisY - upperLegLen - shinLen + limbR * 0.6, 0));
             // 头部/颈部：头在颈部枢轴处旋转（简化）
             AttachJointRotation(head, JointName.Head, new Point3D(0, chestY + upperArmLen + headR * 0.1, 0));

             // 面部更多细节：鼻梁、鼻尖、鼻翼、嘴唇、耳朵与下巴
             // 鼻梁（沿 Z 方向）
             double noseBridgeLen = headR * 0.30 * p.NoseBridgeLengthScale * p.NoseSize;
             double noseWidth = headR * 0.10 * p.NoseWidthScale * p.NoseSize;
             var noseBridge = CreateCapsule(new Point3D(0, headCenterY - headR * 0.05, headR * 0.45), noseWidth, noseBridgeLen, inst.SkinDiffuse, inst.Specular);
             RotateToZ(noseBridge);
             group.Children.Add(noseBridge);
             // 鼻尖
             var noseTip = CreateHemisphere(new Point3D(0, headCenterY - headR * 0.06, headR * 0.45 + noseBridgeLen*0.50), noseWidth*1.05, true, inst.SkinDiffuse, inst.Specular);
             group.Children.Add(noseTip);
             // 鼻翼（左右小胶囊，沿 X 方向）
             double nostrilR = noseWidth * 0.45 * p.NostrilSizeScale;
             double nostrilLen = noseWidth * 0.90 * p.NostrilSizeScale;
             var leftNostril = CreateCapsule(new Point3D(-noseWidth*0.8, headCenterY - headR * 0.10, headR * 0.45 + noseBridgeLen*0.35), nostrilR, nostrilLen, inst.SkinDiffuse, inst.Specular);
             RotateToX(leftNostril);
             var rightNostril = CreateCapsule(new Point3D(noseWidth*0.8, headCenterY - headR * 0.10, headR * 0.45 + noseBridgeLen*0.35), nostrilR, nostrilLen, inst.SkinDiffuse, inst.Specular);
             RotateToX(rightNostril);
             group.Children.Add(leftNostril);
             group.Children.Add(rightNostril);

             // 嘴唇（上唇+下唇，沿 X 方向）
             double mouthLen = headR * 0.50 * p.MouthWidth;
             double lipR = headR * 0.06 * p.LipThicknessScale;
             var mouthCenter = new Point3D(0, headCenterY - headR * 0.18, headR * 0.52);
             var upperLip = CreateCapsule(new Point3D(mouthCenter.X, mouthCenter.Y + lipR*0.30, mouthCenter.Z), lipR*0.9, mouthLen, inst.LipDiffuse, inst.Specular);
             RotateToX(upperLip);
             var lowerLip = CreateCapsule(new Point3D(mouthCenter.X, mouthCenter.Y - lipR*0.35, mouthCenter.Z), lipR, mouthLen*0.98, inst.LipDiffuse, inst.Specular);
             RotateToX(lowerLip);
             group.Children.Add(upperLip);
             group.Children.Add(lowerLip);

             // 耳朵（位置角度可调）
             double earR = headR * 0.14 * p.EarSize;
             double earAngle = p.EarAngleDeg * Math.PI / 180.0;
             double earY = headCenterY + Math.Sin(earAngle) * headR * 0.05;
             double earZ = Math.Sin(earAngle) * headR * 0.05; // 增强前后位移效果
             group.Children.Add(CreateSphere(new Point3D(-headR * 0.85, earY, earZ), earR, inst.SkinDiffuse, inst.Specular));
             group.Children.Add(CreateSphere(new Point3D(headR * 0.85, earY, earZ), earR, inst.SkinDiffuse, inst.Specular));

             // 下巴
             double chinR = headR * 0.12 * p.ChinSizeScale;
             var chin = CreateCapsule(new Point3D(0, headCenterY - headR * 0.40, headR * 0.00), chinR, chinR*1.6, inst.SkinDiffuse, inst.Specular);
             RotateToZ(chin);
             group.Children.Add(chin);

             // 手指（每手五指，每指三段胶囊，沿 Z 方向）
             void BuildHandFingers(Point3D handCenter, bool isLeft)
             {
                 double baseSpread = limbR * 0.9;
                 double baseZ = handCenter.Z + limbR * 0.5;
                 double segR = limbR * 0.35 * p.FingerThicknessScale;
                 double proxLen = limbR * 0.9 * p.FingerLengthScale;
                 double midLen = limbR * 0.75 * p.FingerLengthScale;
                 double distLen = limbR * 0.65 * p.FingerLengthScale;
                 double side = isLeft ? -1.0 : 1.0;
                 double[] xOffsets = new double[] { side * -0.3 * baseSpread, side * -0.1 * baseSpread, side * 0.1 * baseSpread, side * 0.3 * baseSpread };
                 for (int i = 0; i < 4; i++)
                 {
                     double x = handCenter.X + xOffsets[i];
                     var prox = CreateCapsule(new Point3D(x, handCenter.Y, baseZ), segR, proxLen, inst.SkinDiffuse, inst.Specular);
                     RotateToZ(prox);
                     var mid = CreateCapsule(new Point3D(x, handCenter.Y, baseZ + proxLen * 0.9), segR * 0.95, midLen, inst.SkinDiffuse, inst.Specular);
                     RotateToZ(mid);
                     var dist = CreateCapsule(new Point3D(x, handCenter.Y, baseZ + proxLen * 0.9 + midLen * 0.85), segR * 0.9, distLen, inst.SkinDiffuse, inst.Specular);
                     RotateToZ(dist);
                     group.Children.Add(prox);
                     group.Children.Add(mid);
                     group.Children.Add(dist);
                 }
                 // 拇指：偏向内侧并稍短
                 double thumbX = handCenter.X + side * -0.5 * baseSpread;
                 var t1 = CreateCapsule(new Point3D(thumbX, handCenter.Y, baseZ - limbR * 0.2), segR, proxLen * 0.8, inst.SkinDiffuse, inst.Specular);
                 RotateToZ(t1);
                 var t2 = CreateCapsule(new Point3D(thumbX, handCenter.Y, baseZ - limbR * 0.2 + proxLen * 0.7), segR * 0.95, midLen * 0.7, inst.SkinDiffuse, inst.Specular);
                 RotateToZ(t2);
                 var t3 = CreateCapsule(new Point3D(thumbX, handCenter.Y, baseZ - limbR * 0.2 + proxLen * 0.7 + midLen * 0.6), segR * 0.9, distLen * 0.6, inst.SkinDiffuse, inst.Specular);
                 RotateToZ(t3);
                 group.Children.Add(t1);
                 group.Children.Add(t2);
                 group.Children.Add(t3);
             }

             BuildHandFingers(new Point3D(-shoulderHalf, chestY - upperArmLen - foreArmLen, 0), true);
             BuildHandFingers(new Point3D(shoulderHalf, chestY - upperArmLen - foreArmLen, 0), false);

             // 服装外壳：根据风格在躯干外加一层简单外壳
             if (p.Style != ClothingStyle.None)
             {
                 double shellR = bodyR * p.WaistScale * 1.03;
                 double shellLen = height * 0.5;
                 var clothShell = CreateCapsule(new Point3D(0, height * 0.45, 0), shellR, shellLen, inst.ClothDiffuse, inst.Specular);
                 group.Children.Add(clothShell);
             }

             inst.Model = group;
             return inst;
         }

         private static GeometryModel3D CreateSphere(Point3D center, double radius, Material diffuse, SpecularMaterial spec)
         {
             var geo = new MeshGeometry3D();
             const int t = 24;
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
                     geo.Normals.Add(new Vector3D(nx, ny, nz));
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
             var matGroup = new MaterialGroup();
             matGroup.Children.Add(diffuse);
             matGroup.Children.Add(spec);
             return new GeometryModel3D(geo, matGroup) { BackMaterial = diffuse };
         }

         private static GeometryModel3D CreateCapsule(Point3D center, double radius, double length, Material diffuse, SpecularMaterial spec)
         {
             // 简易胶囊：两端半球 + 中间圆柱体，沿 Y 轴
             var geo = new MeshGeometry3D();
             int sides = 24;
             double half = length * 0.5;
             // 圆柱体部分
             for (int i = 0; i <= sides; i++)
             {
                 double theta = 2 * Math.PI * i / sides;
                 double x = radius * Math.Cos(theta);
                 double z = radius * Math.Sin(theta);
                 geo.Positions.Add(new Point3D(center.X + x, center.Y - half, center.Z + z));
                 geo.Positions.Add(new Point3D(center.X + x, center.Y + half, center.Z + z));
                 var n = new Vector3D(Math.Cos(theta), 0, Math.Sin(theta));
                 geo.Normals.Add(n); geo.Normals.Add(n);
             }
             for (int i = 0; i < sides; i++)
             {
                 int a = i * 2;
                 int b = a + 1;
                 int c = a + 2;
                 int d = a + 3;
                 geo.TriangleIndices.Add(a);
                 geo.TriangleIndices.Add(c);
                 geo.TriangleIndices.Add(b);
                 geo.TriangleIndices.Add(b);
                 geo.TriangleIndices.Add(c);
                 geo.TriangleIndices.Add(d);
             }
             // 上半球
             AppendHemisphere(geo, new Point3D(center.X, center.Y + half, center.Z), radius, true, 24);
             // 下半球
             AppendHemisphere(geo, new Point3D(center.X, center.Y - half, center.Z), radius, false, 24);

             var matGroup = new MaterialGroup();
             matGroup.Children.Add(diffuse);
             matGroup.Children.Add(spec);
             return new GeometryModel3D(geo, matGroup) { BackMaterial = diffuse };
         }

         private static void RotateToZ(GeometryModel3D gm)
         {
             var tg = gm.Transform as Transform3DGroup ?? new Transform3DGroup();
             var rot = new AxisAngleRotation3D(new Vector3D(1,0,0), 90);
             var rt = new RotateTransform3D(rot) { CenterX = 0, CenterY = 0, CenterZ = 0 };
             tg.Children.Add(rt);
             gm.Transform = tg;
         }

         private static void RotateToX(GeometryModel3D gm)
         {
             var tg = gm.Transform as Transform3DGroup ?? new Transform3DGroup();
             var rot = new AxisAngleRotation3D(new Vector3D(0,0,1), 90);
             var rt = new RotateTransform3D(rot) { CenterX = 0, CenterY = 0, CenterZ = 0 };
             tg.Children.Add(rt);
             gm.Transform = tg;
         }

         private static GeometryModel3D CreateHemisphere(Point3D center, double radius, bool up, Material diffuse, SpecularMaterial spec)
         {
             var geo = new MeshGeometry3D();
             int sides = 24;
             int t = sides / 2;
             for (int i = 0; i <= t; i++)
             {
                 double phi = (Math.PI / 2) * i / t;
                 for (int j = 0; j <= sides; j++)
                 {
                     double theta = 2 * Math.PI * j / sides;
                     double x = center.X + radius * Math.Cos(theta) * Math.Sin(phi);
                     double y = center.Y + (up ? 1 : -1) * radius * Math.Cos(phi);
                     double z = center.Z + radius * Math.Sin(theta) * Math.Sin(phi);
                     geo.Positions.Add(new Point3D(x, y, z));
                     var nx = Math.Cos(theta) * Math.Sin(phi);
                     var ny = (up ? 1 : -1) * Math.Cos(phi);
                     var nz = Math.Sin(theta) * Math.Sin(phi);
                     geo.Normals.Add(new Vector3D(nx, ny, nz));
                 }
             }
             for (int i = 0; i < t; i++)
             {
                 for (int j = 0; j < sides; j++)
                 {
                     int a = i * (sides + 1) + j;
                     int b = a + sides + 1;
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
             var matGroup = new MaterialGroup();
             matGroup.Children.Add(diffuse);
             matGroup.Children.Add(spec);
             return new GeometryModel3D(geo, matGroup) { BackMaterial = diffuse };
         }

         private static void AppendHemisphere(MeshGeometry3D geo, Point3D center, double radius, bool up, int sides)
         {
             int baseIndex = geo.Positions.Count;
             int t = sides / 2;
             for (int i = 0; i <= t; i++)
             {
                 double phi = (Math.PI / 2) * i / t;
                 for (int j = 0; j <= sides; j++)
                 {
                     double theta = 2 * Math.PI * j / sides;
                     double x = center.X + radius * Math.Cos(theta) * Math.Sin(phi);
                     double y = center.Y + (up ? 1 : -1) * radius * Math.Cos(phi);
                     double z = center.Z + radius * Math.Sin(theta) * Math.Sin(phi);
                     geo.Positions.Add(new Point3D(x, y, z));
                     var nx = Math.Cos(theta) * Math.Sin(phi);
                     var ny = (up ? 1 : -1) * Math.Cos(phi);
                     var nz = Math.Sin(theta) * Math.Sin(phi);
                     geo.Normals.Add(new Vector3D(nx, ny, nz));
                 }
             }
             for (int i = 0; i < t; i++)
             {
                 for (int j = 0; j < sides; j++)
                 {
                     int a = baseIndex + i * (sides + 1) + j;
                     int b = a + sides + 1;
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
         }
     }
 }
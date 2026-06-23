#include "humanoid_mapper_3d.h"
#include <godot_cpp/classes/scene_tree.hpp>
#include <godot_cpp/classes/node.hpp>
#include <godot_cpp/variant/utility_functions.hpp>

using namespace godot;

HumanoidMapper3D::HumanoidMapper3D() {}

void HumanoidMapper3D::_bind_methods() {
    // Disable method bindings temporarily to avoid template MethodBind issues on MSVC.
    // ClassDB::bind_method(D_METHOD("set_skeleton", "path"), &HumanoidMapper3D::set_skeleton);
    // ClassDB::bind_method(D_METHOD("apply_pose", "points"), &HumanoidMapper3D::apply_pose);

    ADD_PROPERTY(PropertyInfo(Variant::NODE_PATH, "skeleton_path"), "set_skeleton", StringName());
}

void HumanoidMapper3D::set_skeleton(const NodePath &path) {
    skeleton_path_ = path;
    Node *n = get_node_or_null(path);
    skeleton_ = Object::cast_to<Skeleton3D>(n);
    if (!skeleton_) UtilityFunctions::push_warning("HumanoidMapper3D: skeleton not found at path");
}

void HumanoidMapper3D::apply_pose(const PackedVector3Array &points) {
    if (!skeleton_) return;
    // Demo: map first 5 points to some bones if exist. Real mapping should be configured.
    static const char *bone_names[] = { "index", "middle", "ring", "thumb", "pinky" };
    int count = MIN(points.size(), (int)(sizeof(bone_names)/sizeof(bone_names[0])));
    for (int i = 0; i < count; ++i) {
        int bone_idx = skeleton_->find_bone(StringName(bone_names[i]));
        if (bone_idx < 0) continue;
        Vector3 p = points[i];
        if (invert_y_) p.y = 1.0f - p.y;
        if (flip_z_) p.z = -p.z;
        p.x *= scale_x_; p.y *= scale_y_; p.z += z_depth_;
        Transform3D t = skeleton_->get_bone_global_pose(bone_idx);
        t.origin = p;
        skeleton_->set_bone_global_pose_override(bone_idx, t, 1.0f, true);
    }
}
#pragma once
#include <godot_cpp/classes/node3d.hpp>
#include <godot_cpp/classes/skeleton3d.hpp>
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/variant/packed_vector3_array.hpp>
#include <godot_cpp/variant/dictionary.hpp>

namespace godot {

class HumanoidMapper3D : public Node3D {
    GDCLASS(HumanoidMapper3D, Node3D);

public:
    HumanoidMapper3D();
    ~HumanoidMapper3D() = default;

    void set_skeleton(const NodePath &path);
    void apply_pose(const PackedVector3Array &points);

protected:
    static void _bind_methods();

private:
    NodePath skeleton_path_;
    Skeleton3D *skeleton_ = nullptr;
    bool invert_y_ = true;
    bool flip_z_ = true;
    float scale_x_ = 1.0f;
    float scale_y_ = 1.0f;
    float z_depth_ = 0.0f;
};

}
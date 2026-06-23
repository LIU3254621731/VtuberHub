#pragma once
#include <godot_cpp/classes/node3d.hpp>
#include <godot_cpp/classes/resource_loader.hpp>
#include <godot_cpp/classes/packed_scene.hpp>
#include <godot_cpp/classes/skeleton3d.hpp>
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/variant/array.hpp>
#include <godot_cpp/variant/dictionary.hpp>

namespace godot {

class AvatarAssembler3D : public Node3D {
    GDCLASS(AvatarAssembler3D, Node3D);

public:
    AvatarAssembler3D();
    ~AvatarAssembler3D() = default;

    bool load_base(const String &scene_path);
    bool attach_wardrobe(const Array &items);
    void apply_material_overrides(const Dictionary &params);
    Dictionary check_bone_consistency();

protected:
    static void _bind_methods();

private:
    Node3D *base_instance_ = nullptr;
    Skeleton3D *skeleton_ = nullptr;
};

}
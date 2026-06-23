#include "avatar_assembler_3d.h"
#include <godot_cpp/classes/scene_tree.hpp>
#include <godot_cpp/classes/node.hpp>
#include <godot_cpp/classes/mesh_instance3d.hpp>
#include <godot_cpp/classes/resource_loader.hpp>
#include <godot_cpp/classes/packed_scene.hpp>
#include <godot_cpp/classes/skeleton3d.hpp>
#include <godot_cpp/classes/shader_material.hpp>
#include <godot_cpp/classes/material.hpp>
#include <godot_cpp/variant/color.hpp>
#include <godot_cpp/variant/utility_functions.hpp>

using namespace godot;

AvatarAssembler3D::AvatarAssembler3D() {}

void AvatarAssembler3D::_bind_methods() {
    // Temporarily disable method bindings to avoid MethodBind templates on MSVC.
    // ClassDB::bind_method(D_METHOD("load_base", "scene_path"), &AvatarAssembler3D::load_base);
    // ClassDB::bind_method(D_METHOD("attach_wardrobe", "items"), &AvatarAssembler3D::attach_wardrobe);
    // ClassDB::bind_method(D_METHOD("apply_material_overrides", "params"), &AvatarAssembler3D::apply_material_overrides);
    // ClassDB::bind_method(D_METHOD("check_bone_consistency"), &AvatarAssembler3D::check_bone_consistency);
}

bool AvatarAssembler3D::load_base(const String &scene_path) {
    Ref<PackedScene> ps = ResourceLoader::get_singleton()->load(scene_path);
    if (!ps.is_valid()) { UtilityFunctions::push_error("Load base scene failed"); return false; }
    Node *inst = ps->instantiate();
    if (!inst) { UtilityFunctions::push_error("Instantiate base scene failed"); return false; }
    add_child(inst);
    base_instance_ = Object::cast_to<Node3D>(inst);
    skeleton_ = base_instance_ ? base_instance_->get_node<Skeleton3D>(NodePath("Skeleton3D")) : nullptr;
    if (!skeleton_) UtilityFunctions::push_warning("Skeleton3D not found under base avatar (expected node named 'Skeleton3D')");
    return true;
}

bool AvatarAssembler3D::attach_wardrobe(const Array &items) {
    if (!base_instance_) return false;
    for (int i = 0; i < items.size(); ++i) {
        Dictionary d = items[i];
        String path = d.get("path", String("")).operator String();
        if (path.is_empty()) continue;
        Ref<PackedScene> ps = ResourceLoader::get_singleton()->load(path);
        if (!ps.is_valid()) { UtilityFunctions::push_warning(String("Wardrobe load failed: ") + path); continue; }
        Node *inst = ps->instantiate();
        if (!inst) continue;
        base_instance_->add_child(inst);
    }
    return true;
}

void AvatarAssembler3D::apply_material_overrides(const Dictionary &params) {
    if (!base_instance_) return;
    Color skin_color = params.get("skin_color", Color(1,0.85,0.8)).operator Color();
    float skin_tone = (float)params.get("skin_tone", 0.5);
    // Demo: traverse MeshInstance3D and set material uniforms (ShaderMaterial expected)
    TypedArray<Node> children = base_instance_->get_children();
    for (int i = 0; i < children.size(); ++i) {
        Node *n = Object::cast_to<Node>(children[i]);
        MeshInstance3D *mi = Object::cast_to<MeshInstance3D>(n);
        if (!mi) continue;
        for (int s = 0; s < mi->get_surface_override_material_count(); ++s) {
            Ref<Material> mat = mi->get_surface_override_material(s);
            if (mat.is_null()) continue;
            Ref<ShaderMaterial> sm = mat; // may be null if not ShaderMaterial
            if (sm.is_valid()) {
                sm->set_shader_parameter("skin_color", skin_color);
                sm->set_shader_parameter("skin_tone", skin_tone);
            }
        }
    }
}

Dictionary AvatarAssembler3D::check_bone_consistency() {
    Dictionary result;
    if (!skeleton_) { result["error"] = String("Skeleton3D missing"); return result; }
    // Demo: return bone names list
    Array bones;
    int bone_count = skeleton_->get_bone_count();
    for (int i = 0; i < bone_count; ++i) bones.push_back(skeleton_->get_bone_name(i));
    result["bone_names"] = bones;
    return result;
}